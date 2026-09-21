# Weather

**Route:** `/weather` · **Status:** Mockup

## Overview

Currently the stock Blazor template's random-data weather forecast sample (`WeatherForecast` inline class, `Random.Shared`). This is the furthest from real of all the mockups — needs a real forecast provider and a real location.

## Data Model

No local persistence needed for a first pass — forecast is fetched live and rendered, not stored. If historical/offline caching is wanted later, add:

- `ForecastCache`: Id, Location, RetrievedAt, PayloadJson (raw provider response, short TTL)

## Integrations

- Candidate provider: [Open-Meteo](https://open-meteo.com/) — free, no API key required, good fit for a personal project
- Alternative: OpenWeatherMap (requires a free-tier API key)
- Location: needs a fixed lat/long or city (user's location) — not yet specified

## Persistence

Not needed for MVP (live fetch only). If caching is added later, TBD between Postgres/MongoDB per [README.md](README.md#cross-feature-open-decisions) — a simple key-value/document shape would favor MongoDB, but not decided.

## UI

Keep the existing table (Date / Temp C / Temp F / Summary), replacing the `WeatherForecast[]` random generator with a real call to the chosen provider's forecast endpoint. Remove the leftover template copy ("This component demonstrates showing data.") once real data is wired up.

## Open Decisions

- Provider choice (Open-Meteo vs OpenWeatherMap vs other)
- Fixed location vs configurable location
- Whether caching/persistence is worth adding now or later

## Out of Scope

- Multi-location weather
- Weather alerts/notifications
