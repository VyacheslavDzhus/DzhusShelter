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

Build and run via [docker-compose.yml](../../../docker-compose.yml):

```bash
docker compose up --build
```

As of [docs/architecture.md](../../../docs/architecture.md), this composes multiple services, not just one: `DzhusShelter.Api`, `DzhusShelter.UI`, `DzhusShelter.TelegramBot`, and PostgreSQL, all on the same private compose network. Check `docker-compose.yml` for exposed ports and env vars before assuming defaults — and per the architecture doc's Security section, `DzhusShelter.Api`'s port must stay internal to that network, never published to the host/internet, until auth is added.

## Target: home server (CasaOS)

The real deployment target for this project is a home server running [CasaOS](https://casaos.io/). CasaOS manages Docker Compose apps through its own UI/app store on top of a normal Docker install — so the `docker-compose.yml` in this repo is what CasaOS ultimately runs, no separate CasaOS-specific compose file needed. Exact access details (host address, how compose files get onto the server, reverse proxy/domain setup) aren't documented yet — resolve them with the user before writing deployment automation, don't assume a setup.

## Before shipping a deploy

- Confirm `appsettings.json` (production) doesn't contain secrets — API keys/connection strings for features like Finance or Weather must come from environment variables or a mounted secrets file, never committed values
- If a feature spec introduces a new database (PostgreSQL/MongoDB), add its connection service to `docker-compose.yml` and document the required env var in that feature's spec
- `app.UseHsts()` and `app.UseExceptionHandler` only run when `!Environment.IsDevelopment()` (see [Program.cs](../../../src/DzhusShelter.UI/Program.cs)) — verify `ASPNETCORE_ENVIRONMENT` is set correctly for the target environment
