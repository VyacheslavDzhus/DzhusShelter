---
name: deploy-dzhus-shelter
description: Use when building, running, or deploying the DzhusShelter app via Docker — local run, docker-compose, and what to check before shipping a deploy.
---

# Deploy Dzhus Shelter

## Local run (no Docker)

```bash
dotnet run --project DzhusShelter.UI.csproj
```

Uses `appsettings.Development.json`. Blazor Server with interactive server render mode — no separate frontend build step.

## Docker

Build and run via the existing [Dockerfile](../../../Dockerfile) and [docker-compose.yml](../../../docker-compose.yml):

```bash
docker compose up --build
```

Check `docker-compose.yml` for the exposed port and any env vars it passes through before assuming defaults.

## Before shipping a deploy

- Confirm `appsettings.json` (production) doesn't contain secrets — API keys/connection strings for features like Finance or Weather must come from environment variables or a mounted secrets file, never committed values
- If a feature spec introduces a new database (PostgreSQL/MongoDB), add its connection service to `docker-compose.yml` and document the required env var in that feature's spec
- `app.UseHsts()` and `app.UseExceptionHandler` only run when `!Environment.IsDevelopment()` (see [Program.cs](../../../Program.cs)) — verify `ASPNETCORE_ENVIRONMENT` is set correctly for the target environment
