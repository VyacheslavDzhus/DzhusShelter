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

The three copies' member **names** always need to match (that's what round-trips as JSON in every direction). Their numeric **values** matter too, but only in the Bot→Api direction: the bot posts `LogHabitEntryCommand` with default `System.Text.Json` options (no `JsonStringEnumConverter`), so the enum crosses the wire as a raw integer there, and the Api's model binder happens to accept integers as well as names. Api→UI (fetching entries) only depends on names, since the Api serializes its responses as strings. Don't renumber the Bot's or Api's copy independently — check both files together.

The original catch-all `Spirits` subtype was removed and replaced with the specific spirits above; no production data referenced it, so no migration was needed (`SubType` is persisted as a string column, so removing an enum member is safe as long as no stored row still uses that string).

## Application (CQRS)

- `LogHabitEntryCommand` (`HabitType`, `SubType`, `OccurredAt`, `Notes?`) → validated via `FluentValidation` → persists a `HabitEntry`, unless one already exists (see **Dedup rule** below), in which case no new row is written
  - Returns `Result<LogHabitEntryResult>` where `LogHabitEntryResult(Guid Id, bool AlreadyLogged)` — `AlreadyLogged` lets callers (the bot) distinguish "just logged" from "already had one today" without a second round trip
- `GetHabitEntriesQuery` (`From`, `To`, `HabitType?`, `SubType?`) → returns matching entries (or a pre-aggregated per-day count, depending on what the chart needs — decide when building the query, not blocking the spec)

### Dedup rule

At most one `HabitEntry` per `(HabitType, SubType, calendar day)` — drinking 3 beers on the same day is one "Beer" entry, not three. "Calendar day" is the UTC date of `OccurredAt` (consistent with how the chart already groups `OccurredAt.Date`). `LogHabitEntryCommandHandler` checks for an existing entry via the existing `IHabitEntryRepository.GetAsync(dayStart, dayEnd, habitType, subType, ct)` before creating a new one — no repository contract change needed. This only governs the *event log*; if a subtype needs quantity tracking later, that's a new feature, not a change to this rule.

## API

- `POST /api/bad-habits/entries` — logs one occurrence (or no-ops if already logged today, per the dedup rule). Returns `200 OK { id, alreadyLogged }`. Called by the Telegram bot.
- `GET /api/bad-habits/entries?from=&to=&habitType=&subType=` — returns matching entries. Called by Blazor (each `PixelHabitCard` calls this once per render, see **UI** below).

Hosted in `DzhusShelter.Api`'s `BadHabitsController` (see [docs/architecture.md](../architecture.md) for why this isn't in the Blazor project directly).

## Telegram Bot Integration

Inline keyboard flow in `DzhusShelter.TelegramBot`: `/start` shows a single root button ("🚫 Шкідливі звички") → tap it to reveal habit type (🚬 Куріння / 🍺 Алкоголь) → tap subtype → bot calls `POST /api/bad-habits/entries` with `OccurredAt = now` → bot confirms in chat. No manual date entry — logging is always "right now." The root button exists because this bot process is meant to grow beyond Bad Habits (see below) — it gives a stable top-level menu to hang future feature buttons off of.

Subtype buttons and confirmation messages show Ukrainian display labels (`"Пиво"`, `"Віскі"`, …), not the raw enum member name — the bot is a Ukrainian-language chat surface, English enum identifiers leaking into it read as a bug, not a style choice. No icons on bot buttons: Telegram inline keyboard labels are plain text, so pixel-art SVG icons (used in the web UI) aren't renderable there; emoji would work but isn't part of this iteration's scope. Confirmation text branches on `AlreadyLogged`: `"Записано: {label}"` when newly logged, `"Вже зафіксовано на сьогодні: {label}"` when a same-day entry already existed, `"Не вдалося записати — спробуй ще раз."` on failure.

This bot process is the same one described in [bots.md](bots.md); reconcile the two specs when the standalone Bots feature is implemented (see architecture doc's open decisions).

## UI

`BadHabits.razor` renders two independent `PixelHabitCard` instances — one per `HabitType` (`Alcohol`, `Smoking`) — and nothing else. The page-level date-range/`HabitType` filter bar from the old single-chart layout is gone; each card owns its own state entirely.

**`PixelHabitCard.razor`** (`Components/Shared/`), one component parameterized by `HabitType` + `Title`, instantiated twice:
- A subtype filter (`nes-select`: "Всі" + that `HabitType`'s subtypes, from `HabitSubTypeCatalog.SubTypesFor(HabitType)`). This one filter drives *both* the calendar and the stats list below — there's no separate per-section filter.
- Month navigation (◀ / ▶ around a "Month Year" label, Ukrainian month names) plus a calendar grid for the displayed month, delegated to **`PixelMonthCalendar.razor`** (`Components/Shared/`) — a pure presentational component (`Year`, `Month`, `IReadOnlyDictionary<DateOnly, MarkupString>` of marked-day icons; no data fetching of its own, reusable for any future per-day marker feature). A day is marked when at least one entry matching the current subtype filter falls on it; the icon shown is that subtype's pixel-art icon, or — when the filter is "Всі" and more than one distinct subtype occurred that day — the lowest-`HabitSubType`-value one's icon with a small "+" badge.
- A stats period selector (`nes-select`: "Цей місяць" / "3 місяці" / "Рік" / "Свій період" — the last reveals two date inputs) and, below it, a list of `{icon} {label} ×{count}` lines for every subtype with `count > 0` in that period (respecting the same subtype filter), sorted by count descending.

**`HabitSubTypeCatalog.cs`** (`Components/Shared/`, new): the UI-side counterpart to the Bot's `BadHabitsKeyboard` — `SubTypesFor(HabitType)`, `DisplayName(HabitSubType)` (same Ukrainian strings as the bot), and `IconSvg(HabitSubType)` returning a hand-authored pixel-art icon (`~16×16` blocky `<rect>`-based SVG, as a plain `string` — callers concatenate it into a larger raw markup string before wrapping the whole thing in `MarkupString` once) for each of the 11 subtypes. This is now a **fourth** place to update when adding a subtype, alongside the three enum copies described under **Data Model** — a missing entry throws `KeyNotFoundException` at render time, not a compile error.

**Data:** no Api or Bot changes for this phase. Each `PixelHabitCard` makes one `BadHabitsApiClient.GetEntriesAsync` call per render — range = the union of the displayed month and the selected stats period — and computes both the calendar's marked days and the stats counts client-side from that one result set, the same client-side-aggregation pattern the old single chart already used.

`PixelChart` (the line/donut ApexCharts wrapper built for the old single-chart layout) stops being used by this page but stays in the codebase as a reusable component for a future feature (e.g. Gym trends) — it was explicitly designed generic, not Bad-Habits-specific.

## Persistence

PostgreSQL via EF Core, per [docs/architecture.md](../architecture.md) (the earlier Postgres-vs-MongoDB "TBD" for this feature is resolved: relational fits an event log with date-range/type filtering well, no reason to reach for Mongo here).

## Open Decisions (resolved during implementation)

- `GetHabitEntriesQuery` returns raw entries (`HabitEntryDto` list); day/subtype aggregation happens client-side (originally in `BadHabits.razor` for the single chart, now inside each `PixelHabitCard`), not server-side — kept the API generic in case another consumer wants unaggregated data later
- `Notes` stayed API-only — the bot flow doesn't prompt for it, always logs `null`
- The `Spirits` catch-all subtype was replaced by naming the specific spirits (`Vodka`/`Whiskey`/`Rum`/`Gin`/`Martini`) instead — no real data depended on the old value, so this was a plain enum edit, not a migration
- Dedup ("one entry per subtype per day") was added because logging every individual drink/cigarette produced noise the user didn't want tracked at that granularity — it changes `LogHabitEntryCommand`'s result shape (`LogHabitEntryResult` with `AlreadyLogged`) rather than silently dropping the duplicate, so the bot can tell the user what happened
- `HabitType`/`HabitSubType` duplication across the three projects was reconsidered (introduce a shared `Contracts` project?) and deliberately kept as-is for now — see the **Data Model** section
- One subtype filter per card drives both the calendar and the stats list (not two independent filters) — the user found a single filter simpler to reason about, and it maps directly onto "show me my Beer days" as one mental action
- Marked calendar days show a small pixel-art icon, not just a colored highlight — worth the extra per-subtype icon authoring because a bare highlight carries no information at a glance when the filter is "Всі"
- Stats period is independent of the calendar's displayed month (fixed presets: this month / 3 months / year / custom range) rather than always matching whatever month the calendar happens to show — the user specifically wanted to see "last 3 months" totals while still being able to browse the calendar month by month
- The old page-level date-range/`HabitType` filter bar was removed entirely rather than kept alongside the two cards' own filters — two sources of truth for "what am I looking at" would have been more confusing than each card being fully self-contained
- Still genuinely open: no auth between bot/Blazor/API — see [docs/architecture.md](../architecture.md)'s Security section for the condition that makes this acceptable in the current deployment (API's port isn't published in `docker-compose.yml`)

## Out of Scope

- Editing or deleting a logged entry (log is append-only for now)
- Habit types beyond smoking and alcohol (add as new `HabitType`/`SubType` enum values when needed)
- Reminders/notifications
- Quantity tracking within a day (dedup means "did it happen today", not "how many times") — would need a distinct feature/data shape if ever wanted
- Clicking/drilling into a marked calendar day for entry-level detail (day markers are read-only at a glance)
- Editing which subtype filter drives the calendar vs. the stats list independently — one filter always drives both
