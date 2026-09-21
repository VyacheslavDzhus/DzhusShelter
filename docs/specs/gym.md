# Gym Stats

**Route:** `/gym` · **Status:** Mockup

## Overview

Tracks how many gym sessions happened per week. Currently a single hardcoded number ("3 sessions per week"). Needs to become a real log of sessions the user can add to, with a weekly count derived from it.

## Data Model

- `GymSession`: Id, Date, Notes (optional, free text — e.g. "leg day")

Weekly count = number of `GymSession` rows in the current week; no separate aggregate table needed at this scale.

## Integrations

None — manually logged, no external API.

## Persistence

TBD — see [README.md](README.md#cross-feature-open-decisions). Small, simple relational table; fits Postgres well but not decided.

## UI

Keep the existing single-stat card (`display-3` number + "SESSIONS PER WEEK" label). Add a way to log a new session (e.g. a button that inserts a `GymSession` for today) — exact interaction (button vs form with date picker) to be decided during implementation, not a blocking decision for the spec.

## Open Decisions

- Persistence engine (see above)
- Whether past weeks' history/trend is wanted later (currently out of scope, but worth flagging since it affects whether `GymSession` needs indexing by date early)

## Out of Scope

- Workout/exercise-level tracking (sets, reps, weights) — this is session-count only
- Historical trend charts across weeks
