# Bad Habits

**Route:** `/bad-habits` · **Status:** Mockup

## Overview

Tracks streaks for quitting bad habits — currently one hardcoded example ("No Smoking, 14 days free"). Needs to become a real streak tracker, and support more than one habit since the mockup's structure (single card) won't scale past one.

## Data Model

- `Habit`: Id, Name, Emoji (optional, for the card icon), StartedAt (streak start date), TargetDays (optional, for the progress bar's max)
- Streak length ("days free") = `DateTime.Today - StartedAt`, computed, not stored
- A "relapse" resets `StartedAt` to today rather than deleting history — keep it simple, no separate relapse log for now

## Integrations

None.

## Persistence

TBD — see [README.md](README.md#cross-feature-open-decisions).

## UI

Repeat the existing card layout (`display-3` number, "DAYS FREE" label, progress bar) once per `Habit`, instead of the single hardcoded card. Progress bar fill = days elapsed / `TargetDays` (or omit the bar if no target set).

## Open Decisions

- Persistence engine (see above)
- Whether the user can add/remove habits from the UI, or habits are seeded manually for now (affects whether a "create habit" form is in scope for the first implementation pass)

## Out of Scope

- Notifications/reminders
- Multi-user support (not relevant — single-user app)
