# Bad Habits

**Route:** `/bad-habits` · **Status:** Implemented · **Architecture:** [docs/architecture.md](../architecture.md)

## Overview

An event log for bad habits (currently: smoking, alcohol), logged via a Telegram bot and visualized as filterable charts on the Blazor dashboard. Each `HabitEntry` is a timestamped event, but at most one per `(HabitType, SubType, calendar day)` — see **Dedup rule** below. This trades "how many" for "which days" as the unit of tracking: the user cares whether he drank on a given day and what, not how many drinks, so charts/calendars operate at day granularity per subtype rather than a raw occurrence count.

This replaces the original "streak counter" version of this spec (single habit, "days free" card) — the user redefined the feature around event logging + charts before any code was written against the old version.

## Data Model

- `HabitEntry` (Domain, rich model — invariant enforced in constructor: `OccurredAt` cannot be in the future):
  - `Id`
  - `HabitType` (enum: `Smoking`, `Alcohol`)
  - `SubType` (enum, scoped per `HabitType`: `Cigarette`/`Vape`/`Iqos`/`Hookah` for Smoking; `Beer`/`Wine`/`Vodka`/`Whiskey`/`Rum`/`Gin`/`Martini` for Alcohol)
  - `OccurredAt` (`DateTimeOffset`)
  - `Notes` (optional free text)

`HabitType`/`HabitSubType` are duplicated (not shared via a common project) across `DzhusShelter.Api`, `DzhusShelter.TelegramBot`, and `DzhusShelter.UI` — a deliberate choice to keep the three deployables independent over the HTTP boundary. Adding a subtype means updating the enum (and, in Api/Bot, the `HabitType → HabitSubType[]` mapping) in every project that defines it. Revisit this duplication (e.g. a shared `Contracts` project) only if it becomes a recurring source of drift bugs — not before.

The original catch-all `Spirits` subtype was removed and replaced with the specific spirits above; no production data referenced it, so no migration was needed (`SubType` is persisted as a string column, so removing an enum member is safe as long as no stored row still uses that string).

## Application (CQRS)

- `LogHabitEntryCommand` (`HabitType`, `SubType`, `OccurredAt`, `Notes?`) → validated via `FluentValidation` → persists a `HabitEntry`, unless one already exists (see **Dedup rule** below), in which case no new row is written
  - Returns `Result<LogHabitEntryResult>` where `LogHabitEntryResult(Guid Id, bool AlreadyLogged)` — `AlreadyLogged` lets callers (the bot) distinguish "just logged" from "already had one today" without a second round trip
- `GetHabitEntriesQuery` (`From`, `To`, `HabitType?`, `SubType?`) → returns matching entries (or a pre-aggregated per-day count, depending on what the chart needs — decide when building the query, not blocking the spec)

### Dedup rule

At most one `HabitEntry` per `(HabitType, SubType, calendar day)` — drinking 3 beers on the same day is one "Beer" entry, not three. "Calendar day" is the UTC date of `OccurredAt` (consistent with how the chart already groups `OccurredAt.Date`). `LogHabitEntryCommandHandler` checks for an existing entry via the existing `IHabitEntryRepository.GetAsync(dayStart, dayEnd, habitType, subType, ct)` before creating a new one — no repository contract change needed. This only governs the *event log*; if a subtype needs quantity tracking later, that's a new feature, not a change to this rule.

## API

- `POST /api/bad-habits/entries` — logs one occurrence (or no-ops if already logged today, per the dedup rule). Returns `200 OK { id, alreadyLogged }`. Called by the Telegram bot.
- `GET /api/bad-habits/entries?from=&to=&habitType=&subType=` — returns entries for the chart. Called by Blazor.

Hosted in `DzhusShelter.Api`'s `BadHabitsController` (see [docs/architecture.md](../architecture.md) for why this isn't in the Blazor project directly).

## Telegram Bot Integration

Inline keyboard flow in `DzhusShelter.TelegramBot`: `/start` shows a single root button ("🚫 Шкідливі звички") → tap it to reveal habit type (🚬 Куріння / 🍺 Алкоголь) → tap subtype → bot calls `POST /api/bad-habits/entries` with `OccurredAt = now` → bot confirms in chat. No manual date entry — logging is always "right now." The root button exists because this bot process is meant to grow beyond Bad Habits (see below) — it gives a stable top-level menu to hang future feature buttons off of.

Subtype buttons and confirmation messages show Ukrainian display labels (`"Пиво"`, `"Віскі"`, …), not the raw enum member name — the bot is a Ukrainian-language chat surface, English enum identifiers leaking into it read as a bug, not a style choice. No icons on bot buttons: Telegram inline keyboard labels are plain text, so pixel-art SVG icons (used in the web UI) aren't renderable there; emoji would work but isn't part of this iteration's scope. Confirmation text branches on `AlreadyLogged`: `"Записано: {label}"` when newly logged, `"Вже зафіксовано на сьогодні: {label}"` when a same-day entry already existed, `"Не вдалося записати — спробуй ще раз."` on failure.

This bot process is the same one described in [bots.md](bots.md); reconcile the two specs when the standalone Bots feature is implemented (see architecture doc's open decisions).

## UI

`BadHabits.razor`: filter controls (date range, `HabitType`/`SubType` dropdowns) driving a `GET` to the API, rendered via `PixelChart` (a reusable pixel-art-themed line/donut chart component — see [Components/Shared/PixelChart.razor](../../src/DzhusShelter.UI/Components/Shared/PixelChart.razor)) wrapping **Blazor-ApexCharts**. Chart shows occurrence counts over time, filterable by type/subtype.

**Planned next (not yet built):** replace this single combined chart with two separate `PixelCard`-based cards, one per `HabitType` (Alcohol, Smoking), each with a subtype filter, a month calendar marking days with a matching entry, and a subtype-count summary over a chosen period (e.g. "last 2 months: Beer ×10, Whiskey ×3"). `PixelChart` stays as a reusable component for future features (e.g. Gym trends) but isn't the display for Bad Habits going forward. Custom pixel-art SVG icons per subtype belong to this phase, since Telegram bot buttons can't render them (plain text only).

## Persistence

PostgreSQL via EF Core, per [docs/architecture.md](../architecture.md) (the earlier Postgres-vs-MongoDB "TBD" for this feature is resolved: relational fits an event log with date-range/type filtering well, no reason to reach for Mongo here).

## Open Decisions (resolved during implementation)

- `GetHabitEntriesQuery` returns raw entries (`HabitEntryDto` list); per-day aggregation for the chart happens client-side in `BadHabits.razor`, not server-side — kept the API generic in case another consumer wants unaggregated data later
- `Notes` stayed API-only — the bot flow doesn't prompt for it, always logs `null`
- The `Spirits` catch-all subtype was replaced by naming the specific spirits (`Vodka`/`Whiskey`/`Rum`/`Gin`/`Martini`) instead — no real data depended on the old value, so this was a plain enum edit, not a migration
- Dedup ("one entry per subtype per day") was added because logging every individual drink/cigarette produced noise the user didn't want tracked at that granularity — it changes `LogHabitEntryCommand`'s result shape (`LogHabitEntryResult` with `AlreadyLogged`) rather than silently dropping the duplicate, so the bot can tell the user what happened
- `HabitType`/`HabitSubType` duplication across the three projects was reconsidered (introduce a shared `Contracts` project?) and deliberately kept as-is for now — see the **Data Model** section
- Still genuinely open: no auth between bot/Blazor/API — see [docs/architecture.md](../architecture.md)'s Security section for the condition that makes this acceptable in the current deployment (API's port isn't published in `docker-compose.yml`)

## Out of Scope

- Editing or deleting a logged entry (log is append-only for now)
- Habit types beyond smoking and alcohol (add as new `HabitType`/`SubType` enum values when needed)
- Reminders/notifications
- Quantity tracking within a day (dedup means "did it happen today", not "how many times") — would need a distinct feature/data shape if ever wanted
- The calendar + subtype-stats card redesign described under **UI → Planned next** — tracked as a separate, later implementation pass
