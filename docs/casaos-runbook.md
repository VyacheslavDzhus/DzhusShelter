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
| `ui` | `8082` | `8080` was already taken by zigbee2mqtt on this host |
| `postgres` | `5432` | published for local-dev convenience, not a security requirement |
| `api` | *(none)* | intentionally not published — see [architecture.md](architecture.md)'s Security section |

## `.env` (secrets — never committed)

Lives at `~/dzhusshelter/.env`. Must contain exactly these three lines, **no spaces around `=`, no quotes**:

```
POSTGRES_PASSWORD=...
TELEGRAM_BOT_TOKEN=...
TELEGRAM_ALLOWED_CHAT_ID=...
```

If `sudo docker compose config | grep -A2 ConnectionStrings` ever shows `Password=` empty despite `.env` looking right, see "If something breaks" below — that exact symptom already happened once (see Known Gotchas).

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
