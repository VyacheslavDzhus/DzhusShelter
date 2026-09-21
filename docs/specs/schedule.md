# Schedule

**Route:** `/schedule` · **Status:** Mockup

## Overview

Weekly work schedule (Work / Rest per day of week). Currently a hardcoded Monday–Friday list. Needs to become editable and cover the full week.

## Data Model

- `ScheduleEntry`: Id, DayOfWeek (enum, Monday–Sunday), Status (enum: Work, Rest) — one row per day, upserted when the user changes a day's status

## Integrations

None — manually managed schedule, no external calendar sync planned.

## Persistence

TBD — see [README.md](README.md#cross-feature-open-decisions). Seven fixed rows; trivial fit for either engine.

## UI

Keep the existing list layout (day name + colored status label: `text-success` for Work, `text-secondary` for Rest). Extend to all 7 days (mockup currently stops at Friday) and make each row's status clickable/toggleable instead of fixed.

## Open Decisions

- Whether this is a fixed recurring weekly pattern (edit once, applies every week) or a real per-date calendar (different status per specific date, not just day-of-week) — the current mockup's "Monday/Tuesday/…" labels suggest the former, but that should be confirmed since it changes the data model (`ScheduleEntry` per day-of-week vs per date)
- Persistence engine (see above)

## Out of Scope

- Calendar sync (Google Calendar, etc.)
- Multi-week or holiday overrides
