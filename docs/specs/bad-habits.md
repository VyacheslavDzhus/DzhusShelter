# Bad Habits

**Route:** `/bad-habits` · **Status:** Implemented · **Architecture:** [docs/architecture.md](../architecture.md)

## Overview

An event log for bad habits (currently: smoking, alcohol), logged via a Telegram bot and visualized as filterable charts on the Blazor dashboard. Every occurrence (one cigarette, one drink) is its own timestamped event — not a daily aggregate — so charts can show any granularity (per day, per week, trend over time) without losing detail.

This replaces the original "streak counter" version of this spec (single habit, "days free" card) — the user redefined the feature around event logging + charts before any code was written against the old version.

## Data Model

- `HabitEntry` (Domain, rich model — invariant enforced in constructor: `OccurredAt` cannot be in the future):
  - `Id`
  - `HabitType` (enum: `Smoking`, `Alcohol`)
  - `SubType` (enum, scoped per `HabitType`: `Cigarette`/`Vape` for Smoking; `Beer`/`Wine`/`Spirits` for Alcohol)
  - `OccurredAt` (`DateTimeOffset`)
  - `Notes` (optional free text)

## Application (CQRS)

- `LogHabitEntryCommand` (`HabitType`, `SubType`, `OccurredAt`, `Notes?`) → validated via `FluentValidation` → persists a `HabitEntry`
- `GetHabitEntriesQuery` (`From`, `To`, `HabitType?`, `SubType?`) → returns matching entries (or a pre-aggregated per-day count, depending on what the chart needs — decide when building the query, not blocking the spec)

## API

- `POST /api/bad-habits/entries` — logs one occurrence. Called by the Telegram bot.
- `GET /api/bad-habits/entries?from=&to=&habitType=&subType=` — returns entries for the chart. Called by Blazor.

Hosted in `DzhusShelter.Api`'s `BadHabitsController` (see [docs/architecture.md](../architecture.md) for why this isn't in the Blazor project directly).

## Telegram Bot Integration

Inline keyboard flow in `DzhusShelter.TelegramBot`: `/start` shows a single root button ("🚫 Шкідливі звички") → tap it to reveal habit type (🚬 Куріння / 🍺 Алкоголь) → tap subtype → bot calls `POST /api/bad-habits/entries` with `OccurredAt = now` → bot confirms in chat. No manual date entry — logging is always "right now." The root button exists because this bot process is meant to grow beyond Bad Habits (see below) — it gives a stable top-level menu to hang future feature buttons off of.

This bot process is the same one described in [bots.md](bots.md); reconcile the two specs when the standalone Bots feature is implemented (see architecture doc's open decisions).

## UI

`BadHabits.razor`: filter controls (date range, `HabitType`/`SubType` dropdowns) driving a `GET` to the API, rendered as a chart via **Blazor-ApexCharts** (chosen so we don't hand-write JS interop for charting). Chart shows occurrence counts over time, filterable by type/subtype.

## Persistence

PostgreSQL via EF Core, per [docs/architecture.md](../architecture.md) (the earlier Postgres-vs-MongoDB "TBD" for this feature is resolved: relational fits an event log with date-range/type filtering well, no reason to reach for Mongo here).

## Open Decisions (resolved during implementation)

- `GetHabitEntriesQuery` returns raw entries (`HabitEntryDto` list); per-day aggregation for the chart happens client-side in `BadHabits.razor`, not server-side — kept the API generic in case another consumer wants unaggregated data later
- `Notes` stayed API-only — the bot flow doesn't prompt for it, always logs `null`
- Still genuinely open: no auth between bot/Blazor/API — see [docs/architecture.md](../architecture.md)'s Security section for the condition that makes this acceptable in the current deployment (API's port isn't published in `docker-compose.yml`)

## Out of Scope

- Editing or deleting a logged entry (log is append-only for now)
- Habit types beyond smoking and alcohol (add as new `HabitType`/`SubType` enum values when needed)
- Reminders/notifications
