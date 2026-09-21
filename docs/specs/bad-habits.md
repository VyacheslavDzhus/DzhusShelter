# Bad Habits

**Route:** `/bad-habits` · **Status:** Spec'd (rewritten — see history below) · **Architecture:** [docs/architecture.md](../architecture.md)

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
- `GET /api/bad-habits/entries?from=&to=&type=&subType=` — returns entries for the chart. Called by Blazor.

Hosted in `DzhusShelter.Api`'s `BadHabitsController` (see [docs/architecture.md](../architecture.md) for why this isn't in the Blazor project directly).

## Telegram Bot Integration

Inline keyboard flow in `DzhusShelter.TelegramBot`: tap habit type (🚬 Куріння / 🍺 Алкоголь) → tap subtype → bot calls `POST /api/bad-habits/entries` with `OccurredAt = now` → bot confirms in chat. No manual date entry — logging is always "right now."

This bot process is the same one described in [bots.md](bots.md); reconcile the two specs when the standalone Bots feature is implemented (see architecture doc's open decisions).

## UI

`BadHabits.razor`: filter controls (date range, `HabitType`/`SubType` dropdowns) driving a `GET` to the API, rendered as a chart via **Blazor-ApexCharts** (chosen so we don't hand-write JS interop for charting). Chart shows occurrence counts over time, filterable by type/subtype.

## Persistence

PostgreSQL via EF Core, per [docs/architecture.md](../architecture.md) (the earlier Postgres-vs-MongoDB "TBD" for this feature is resolved: relational fits an event log with date-range/type filtering well, no reason to reach for Mongo here).

## Open Decisions

- Exact shape of `GetHabitEntriesQuery`'s response (raw entries vs server-side pre-aggregated per-day counts) — decide during implementation based on what's simplest for the chosen chart library to consume
- Whether `Notes` is ever surfaced in the bot flow (e.g. an optional follow-up message) or stays API-only for now — default to API-only, add a bot prompt later if wanted
- No auth between bot/Blazor/API yet — see [docs/architecture.md](../architecture.md)'s Security section for the condition that makes this acceptable (API never exposed outside the private Docker network)

## Out of Scope

- Editing or deleting a logged entry (log is append-only for now)
- Habit types beyond smoking and alcohol (add as new `HabitType`/`SubType` enum values when needed)
- Reminders/notifications
