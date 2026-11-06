# CasaOS Runbook

Practical cheat sheet for running DzhusShelter on the CasaOS home server. See [architecture.md](architecture.md) for the "why", and the `deploy-dzhus-shelter` skill for the broader context.

## Regular update (after a new push to `main`)

CI already built and pushed fresh images to Docker Hub — the server just needs to pull and restart:

```bash
cd ~/dzhusshelter
git pull
sudo docker compose pull
sudo docker compose up -d
```

## Check status

```bash
sudo docker compose ps
sudo docker compose logs -f <service>   # api | bot | main-bot | ui | postgres | backup
```

## Current ports (host machine)

| Service | Host port | Notes |
|---|---|---|
| `ui` | `127.0.0.1:8082` | Bound to localhost only — reachable via the existing Cloudflare Tunnel (host network mode), never directly from the LAN/internet |
| `api` | `127.0.0.1:5279` | Same as `ui` — localhost-only, protected by auth (JWT or API key) once reached through the tunnel |
| `postgres` | `5432` | Published for local-dev convenience, not a security requirement |

## `.env` (secrets — never committed)

Lives at `~/dzhusshelter/.env`. Must contain exactly these nine lines, **no spaces around `=`, no quotes**:

```
POSTGRES_PASSWORD=...
TELEGRAM_BOT_TOKEN=...
TELEGRAM_ALLOWED_CHAT_ID=...
JWT_SIGNING_KEY=...
ADMIN_PASSWORD_HASH=...
API_KEY_BOT=...
API_KEY_SHORTCUTS=...
TELEGRAM_MAIN_BOT_TOKEN=...
API_KEY_MAIN_BOT=...
```

Generate `JWT_SIGNING_KEY`/`API_KEY_BOT`/`API_KEY_SHORTCUTS`/`API_KEY_MAIN_BOT` as random strings, e.g.:
```bash
python3 -c "import secrets; print(secrets.token_urlsafe(32))"
```
Generate `ADMIN_PASSWORD_HASH` as a bcrypt hash of your chosen dashboard password — never store the plaintext:
```bash
pip install bcrypt
python3 -c "import bcrypt, getpass; print(bcrypt.hashpw(getpass.getpass().encode(), bcrypt.gensalt()).decode().replace('\$', '\$\$'))"
```
(If `pip install bcrypt` fails with an "externally managed environment" error — common on Debian/Ubuntu 12+, PEP 668 — `apt install python3-bcrypt` is the alternative that doesn't need `pip` at all.)

This prompts for the password interactively instead of taking it as a command-line literal — embedding the plaintext directly in the command would leave it sitting in `~/.bash_history` and visible to other processes via `ps` while it runs.

The `$$` (not a typo) is required because Docker Compose's `.env` parser treats a bare `$` as a variable reference — bcrypt hashes always contain literal `$` characters, so pasting one in unescaped silently corrupts it and breaks login.

### Main bot (`main-bot`, birthday notifications)

One-time prerequisite: the main bot already exists in @BotFather. Put its token into `TELEGRAM_MAIN_BOT_TOKEN`, then open the bot in Telegram (from the chat whose ID is `TELEGRAM_ALLOWED_CHAT_ID`) and press **Start** once. Without that, Telegram answers 403 "bot can't initiate conversation" and no notification is ever delivered. It reuses `TELEGRAM_ALLOWED_CHAT_ID` as the target chat and publishes no ports.

`API_KEY_MAIN_BOT` is its own key (generate it like the others above). It is accepted only by the two birthday-notification endpoints (list pending / acknowledge) — everything else answers 401/403 for it, so a leaked bot key can't read or change any other data.

The `dzhusshelter-main-bot` image only exists on Docker Hub after the CI run on `main` that first includes it; pulling earlier makes `docker compose pull` fail for the whole stack, so wait for that CI run before `pull`/`up -d`.

After adding the two new `.env` lines, apply with `sudo docker compose pull && sudo docker compose up -d`, then check `sudo docker compose logs main-bot`.

## Protecting the Postgres volume (important!)

All habit-tracking data lives in the named Docker volume `dzhusshelter_postgres-data`. Losing it means losing all logged data.

**Rules:**
- **Never run `docker compose down -v`** — the `-v` flag deletes volumes. Plain `docker compose down` / `up -d` never touches volumes, that's always safe.
- **Never open this app's Edit/Settings panel in the CasaOS dashboard GUI.** Confirmed on 2026-09-23: merely opening it silently rewrote `docker-compose.yml` on disk, stripped the `${POSTGRES_PASSWORD}` reference (baked in an empty password permanently) and turned the named Postgres volume into an anonymous one. Manage this stack **only** via terminal + `docker compose`.
- Sanity-check the volume exists whenever unsure:
  ```bash
  docker volume ls | grep postgres
  docker volume inspect dzhusshelter_postgres-data
  ```

### Backups (automatic, weekly)

The `backup` service in `docker-compose.yml` (script: [ops/postgres-backup/backup.sh](../ops/postgres-backup/backup.sh)) keeps **the 3 newest weekly dumps** in `~/dzhusshelter/backups/` (override with `BACKUP_DIR` in `.env`). It starts with the rest of the stack — nothing to schedule on the host.

- **When:** not a fixed cron slot — once an hour it checks whether the newest dump is older than 7 days, and if so makes one. A power cut at "backup time" therefore can't skip a week: the dump just happens on the first check after the server is back up. The very first `up -d` with this service makes a dump straight away.
- **Rotation:** after every successful dump, everything past the 3 newest is deleted. A dump is written to a hidden `.partial` file first and only renamed on success, so a failed dump never pushes a good one out.
- **Format:** `pg_dump --format=custom` (compressed, restored with `pg_restore`), files named `dzhusshelter-<UTC timestamp>.dump`, readable by root only.
- **Same disk as the database** — protects against bad migrations, accidental deletes, broken data; **not** against the disk itself dying. Copy a dump elsewhere now and then if that matters.

```bash
ls -lh ~/dzhusshelter/backups/                       # what's there
sudo docker compose logs backup                      # "created …" / "rotated out …" / "FAILED …"
sudo docker compose exec backup sh /usr/local/bin/backup.sh --once   # take one right now (e.g. before a risky deploy) — counts toward the 3
```

### Rolling back to a backup

This **replaces the whole database** with the state from the dump — everything logged after it is lost. Drop-and-recreate (rather than restoring on top of the live DB) is deliberate: restoring over existing tables either fails on conflicts or, with `--clean`, leaves behind any table a newer migration added, which then breaks the API's auto-migrate on startup.

```bash
cd ~/dzhusshelter
ls backups/                                           # pick the file to roll back to
sudo docker compose stop api bot                      # nothing writes while we swap
sudo docker compose exec backup dropdb --force dzhusshelter
sudo docker compose exec backup createdb dzhusshelter
sudo docker compose exec backup pg_restore --dbname=dzhusshelter /backups/dzhusshelter-<timestamp>.dump
sudo docker compose start api bot
```

The dump includes EF Core's `__EFMigrationsHistory`, so on start the API re-applies any migration newer than the backup by itself.

Full volume backup — for disaster recovery (whole data directory as a tarball):
```bash
docker run --rm -v dzhusshelter_postgres-data:/data -v ~/backups:/backup alpine tar czf /backup/postgres-volume-$(date +%F).tar.gz -C /data .
```

## If something breaks after `git pull` / `up -d`

1. `sudo docker compose ps` — is everything `Up`?
2. `sudo docker compose logs <service>` — read the actual exception, don't guess
3. `sudo docker compose config | grep -A2 ConnectionStrings` — confirms `.env` substitution is actually resolving (catches the CasaOS file-corruption bug below)
4. `git diff docker-compose.yml` — any unexpected diff (an `x-casaos:` block, `cpu_shares`, `deploy.resources.limits` appearing) means CasaOS silently overwrote the file again:
   ```bash
   git checkout -- docker-compose.yml
   sudo docker compose up -d
   ```

## Known gotchas (already hit once, 2026-09-23)

- **CasaOS's own "Edit app" GUI panel rewrites `docker-compose.yml` on disk** the moment you open it — expands `.env` variables into frozen literal values (and got the Postgres password wrong when it did), and breaks the named volume into an anonymous one. Avoid opening it for this app entirely.
- `.env` must have no BOM and no spaces around `=`. A file that "looks" correct when you `cat` it can still fail — always verify with `docker compose config`, not by eyeballing `.env`.
- Host port `8080` is taken by zigbee2mqtt on this specific server — that's why `ui` uses `8082`.
- `docker` commands need `sudo` unless you've run `sudo usermod -aG docker $USER` and logged back in.
- **Retiring a `HabitSubType` (or `HabitType`) enum value breaks the web UI for any row still storing the old string, but only that one card/query — it doesn't crash the whole app.** Hit once on 2026-09-26 when `Spirits` was retired (replaced by `Vodka`/`Whiskey`/`Rum`/`Gin`/`Martini`) but one real logged entry from 2026-09-23 still had `SubType='Spirits'` in Postgres (`SubType` is a string column via EF's `HasConversion<string>()`) — the Alcohol card's `PixelHabitCard` failed to deserialize that row and showed the `<ErrorBoundary>` fallback ("Не вдалося завантажити дані..."), while the Smoking card kept working fine. Fix was a one-line data migration, no restart needed:
  ```bash
  sudo docker compose exec postgres psql -U postgres dzhusshelter -c "UPDATE \"HabitEntries\" SET \"SubType\"='Vodka' WHERE \"SubType\"='Spirits';"
  ```
  Before ever retiring another subtype value, check for existing rows using it first:
  ```bash
  sudo docker compose exec postgres psql -U postgres dzhusshelter -c "SELECT \"SubType\", count(*) FROM \"HabitEntries\" GROUP BY \"SubType\";"
  ```

## Public access via Cloudflare Tunnel

`ui` and `api` are reachable from the internet through the CasaOS host's existing
`cloudflared` app (already used for Home Assistant, host network mode) — no second
tunnel container. Add two Public Hostname entries to that same tunnel (Cloudflare
Zero Trust dashboard → Networks → Tunnels → your tunnel → Public Hostname, or this
image's own web UI if it exposes the same setting):

| Public hostname | Service | Target |
|---|---|---|
| `dashboard.<your-domain>` | `ui` | `http://127.0.0.1:8082` |
| `api.<your-domain>` | `api` | `http://127.0.0.1:5279` |

### iOS Shortcuts: sending data to `api.<your-domain>`

In the Shortcuts app, on the "Get Contents of URL" (or "Get/Post Contents of URL")
action: open **Headers**, add a header named `X-Api-Key` with the value of this
deployment's `API_KEY_SHORTCUTS` secret. No other setup needed — this key is
separate from the bot's, so revoking/rotating one never affects the other.
