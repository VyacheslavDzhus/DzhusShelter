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
sudo docker compose logs -f <service>   # api | bot | ui | postgres
```

## Current ports (host machine)

| Service | Host port | Notes |
|---|---|---|
| `ui` | `127.0.0.1:8082` | Bound to localhost only — reachable via the existing Cloudflare Tunnel (host network mode), never directly from the LAN/internet |
| `api` | `127.0.0.1:5279` | Same as `ui` — localhost-only, protected by auth (JWT or API key) once reached through the tunnel |
| `postgres` | `5432` | Published for local-dev convenience, not a security requirement |

## `.env` (secrets — never committed)

Lives at `~/dzhusshelter/.env`. Must contain exactly these seven lines, **no spaces around `=`, no quotes**:

```
POSTGRES_PASSWORD=...
TELEGRAM_BOT_TOKEN=...
TELEGRAM_ALLOWED_CHAT_ID=...
JWT_SIGNING_KEY=...
ADMIN_PASSWORD_HASH=...
API_KEY_BOT=...
API_KEY_SHORTCUTS=...
```

Generate `JWT_SIGNING_KEY`/`API_KEY_BOT`/`API_KEY_SHORTCUTS` as random strings, e.g.:
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

### Backups (do this periodically)

Logical backup — small, easy to restore, recommended for routine use:
```bash
sudo docker compose exec postgres pg_dump -U postgres dzhusshelter > ~/dzhusshelter-backup-$(date +%F).sql
```

Restore from one:
```bash
cat ~/dzhusshelter-backup-2026-09-23.sql | sudo docker compose exec -T postgres psql -U postgres dzhusshelter
```

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
