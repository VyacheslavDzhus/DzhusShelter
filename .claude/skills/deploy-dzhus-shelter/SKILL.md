---
name: deploy-dzhus-shelter
description: Use when building, running, or deploying the DzhusShelter app via Docker — local run, docker-compose, and what to check before shipping a deploy.
---

# Deploy Dzhus Shelter

## Local run (no Docker)

```bash
dotnet run --project src/DzhusShelter.UI/DzhusShelter.UI.csproj
```

Uses `appsettings.Development.json`. Blazor Server with interactive server render mode — no separate frontend build step.

## Docker

`docker-compose.yml` is the deployment file — its `image:` entries point at prebuilt images on Docker Hub (`1928374650810/dzhusshelter-*:latest`), not local builds. `docker-compose.override.yml` sits next to it and adds `build:` back for local dev only — Compose auto-merges both files when neither is named explicitly, so local dev is unchanged:

```bash
docker compose up --build
```

As of [docs/architecture.md](../../../docs/architecture.md), this composes multiple services, not just one: `DzhusShelter.Api`, `DzhusShelter.UI`, `DzhusShelter.TelegramBot`, and PostgreSQL, all on the same private compose network. Check `docker-compose.yml` for exposed ports and env vars before assuming defaults — and per the architecture doc's Security section, `DzhusShelter.Api`'s port must stay internal to that network, never published to the host/internet, until auth is added.

## CI: build and push images

[.github/workflows/docker-build-push.yml](../../../.github/workflows/docker-build-push.yml) builds the 3 app images (api/ui/bot) and pushes them to Docker Hub as `1928374650810/dzhusshelter-{api,ui,bot}:latest` on every push to `main`. Requires a repo secret `DOCKERHUB_TOKEN` (a Docker Hub Access Token, not the account password). This is why `docker-compose.yml`'s `image:` tags can point straight at Docker Hub — a fresh `latest` is there right after a merge to `main`.

## Target: home server (CasaOS)

The real deployment target for this project is a home server running [CasaOS](https://casaos.io/). CasaOS manages Docker Compose apps through its own UI/app store on top of a normal Docker install. It only ever sees `docker-compose.yml` (never the `.override.yml`, which is dev-only) — so on CasaOS, updating means:

```bash
docker compose pull && docker compose up -d
```

No build happens on the CasaOS box itself — the point of the CI pipeline above is that this weak hardware never has to compile or build a Docker image, only pull finished ones.

Deployed and running since 2026-09-23 — full step-by-step commands, port assignments, `.env` format, backup commands, and known gotchas (including a CasaOS GUI bug that silently corrupts `docker-compose.yml`) live in [docs/casaos-runbook.md](../../../docs/casaos-runbook.md). Read that before making any deployment change on the actual server.

## Before shipping a deploy

- Confirm `appsettings.json` (production) doesn't contain secrets — API keys/connection strings for features like Finance or Weather must come from environment variables or a mounted secrets file, never committed values
- If a feature spec introduces a new database (PostgreSQL/MongoDB), add its connection service to `docker-compose.yml` and document the required env var in that feature's spec
- `app.UseHsts()` and `app.UseExceptionHandler` only run when `!Environment.IsDevelopment()` (see [Program.cs](../../../src/DzhusShelter.UI/Program.cs)) — verify `ASPNETCORE_ENVIRONMENT` is set correctly for the target environment
