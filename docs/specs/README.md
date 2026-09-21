# Dzhus Shelter — Feature Specs

Personal Blazor Server dashboard, single user, no auth. Each feature below starts as a static mockup in `Components/Pages/` and gets built out one at a time, spec first — see the `add-feature-page` skill.

This repo doubles as a learning project — see [docs/roadmap.md](../roadmap.md) for the curriculum and the suggested order to build these features in.

## Feature status

| Feature | Route | Spec | Status |
|---|---|---|---|
| Finance | `/finance` | [finance.md](finance.md) | Mockup |
| Exchange | `/exchange` | [exchange.md](exchange.md) | Mockup |
| Gym | `/gym` | [gym.md](gym.md) | Mockup |
| Bad Habits | `/bad-habits` | [bad-habits.md](bad-habits.md) | Mockup |
| Screen Time | `/screen-time` | [screen-time.md](screen-time.md) | Mockup |
| Weather | `/weather` | [weather.md](weather.md) | Mockup |
| Bots | `/bots` | [bots.md](bots.md) | Mockup |
| Schedule | `/schedule` | [schedule.md](schedule.md) | Mockup |

Statuses: **Mockup** (current, hardcoded UI) → **Spec'd** (spec written, decisions resolved) → **Implemented** (real data/logic shipped).

## Cross-feature open decisions

These affect multiple specs and aren't resolved yet — each spec below flags where it's blocked on one of these:

- **Persistence split**: the project plans to run both PostgreSQL and MongoDB, but which feature's data lives in which database is **not decided yet**. Each spec's "Persistence" section is marked TBD until this is settled.
- **Secrets/config**: none of the external API integrations (Monobank, weather provider, Telegram Bot API, exchange rates) have keys or provider choices decided yet — flagged per spec.

## Adding a new feature spec

Copy the structure used in [finance.md](finance.md): Overview, Data Model, Integrations, Persistence, UI, Open Decisions, Out of Scope.
