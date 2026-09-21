# Screen Time

**Route:** `/screen-time` · **Status:** Mockup

## Overview

Shows daily screen time and a small recent-days bar chart. Currently fully hardcoded (11:00 hours, static bars). The hard part of this feature isn't the UI — it's where the screen-time number comes from, since neither a Blazor Server app nor its host machine has direct access to a phone's screen-time data.

## Data Model

- `ScreenTimeEntry`: Id, Date, TotalMinutes

One row per day; the daily chart reads the last 7 `ScreenTimeEntry` rows.

## Integrations

No first-party API exists for iOS Screen Time or Android Digital Wellbeing that a server-side app can call directly. Realistic options, in order of effort:

1. **Manual entry** — user types today's screen time into the UI; simplest, ships first
2. **Android**: Digital Wellbeing data can be exported/scraped via a companion script (e.g. Tasker/automation app posting to a small ingest endpoint on this app) — bigger effort, separate mini-project
3. **iOS**: no known automation path without a jailbreak or manual Shortcuts automation posting to an ingest endpoint

## Persistence

TBD — see [README.md](README.md#cross-feature-open-decisions). Simple daily time-series; fits either engine.

## UI

Keep the existing stat card + bar layout. Bars render from the last 7 `ScreenTimeEntry.TotalMinutes` values instead of the hardcoded heights.

## Open Decisions

- Which ingestion method to build first (manual entry vs automation) — manual entry is the pragmatic default to unblock everything else, automation is a later stretch goal
- If an ingest endpoint is ever built, it needs its own auth (a shared secret token), since it'd be the first externally-callable endpoint in this app
- Persistence engine (see above)

## Out of Scope

- Per-app breakdown (only total daily minutes)
- Automated ingestion in the first implementation pass (start with manual entry)
