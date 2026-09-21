# Exchange

**Route:** `/exchange` · **Status:** Mockup

## Overview

Currency exchange rate board — a table of currency pairs with rate and % change since last check. Current mockup uses pairs against "SB", an undefined currency — needs clarifying (see Open Decisions).

## Data Model

- `CurrencyPair`: Id, BaseCode, QuoteCode
- `RateSnapshot`: Id, CurrencyPairId, Rate, CapturedAt — % change is computed from the two most recent snapshots, not stored directly

## Integrations

- Candidate: [NBU (National Bank of Ukraine) exchange rate API](https://bank.gov.ua/ua/open-data/api-dev) — free, public, no key required, gives official UAH rates for major currencies
- If pairs against a custom/private currency ("SB") are required, that needs its own data source — unclear from the current mockup what "SB" represents

## Persistence

TBD — see [README.md](README.md#cross-feature-open-decisions). Time-series rate data fits either engine; leaning relational (Postgres) since % change is computed via ordered queries, but not decided.

## UI

Keep the existing dark striped table layout (`table-dark table-striped`). Columns stay Pair / Rate / Change, with `text-success`/`text-danger` coloring on change sign — that part of the mockup already matches the intended real behavior.

## Open Decisions

- What "SB" is in the current mockup (custom currency? typo? placeholder?) — must be resolved before picking pairs to track for real
- Data source(s) beyond NBU if non-UAH-based pairs are needed
- Poll/refresh frequency (e.g. hourly cron vs on page load)
- Persistence engine (see above)

## Out of Scope

- Manual rate entry/override
- Historical charting (only current rate + latest change shown)
