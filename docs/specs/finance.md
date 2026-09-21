# Finance

**Route:** `/finance` · **Status:** Mockup

## Overview

Personal balance tracker across bank accounts (currently mocked: Monobank, Pumb). Shows current balance per account and a combined total. Read-only — no transaction entry planned in this iteration.

## Data Model

- `BankAccount`: Id, Provider (enum: Monobank, Pumb, …), DisplayName, CurrencyCode, LastSyncedAt
- `BalanceSnapshot`: Id, BankAccountId, Balance, CapturedAt — one row per sync, so history/trend can be shown later without a schema change

## Integrations

- **Monobank**: public [Monobank API](https://api.monobank.ua/docs/) — personal token required (user-generated at `api.monobank.ua`), read-only balance endpoint
- **Pumb**: no confirmed public API yet — needs research; may require manual balance entry as a fallback if Pumb has no accessible API

## Persistence

TBD — see [README.md](README.md#cross-feature-open-decisions) (Postgres vs MongoDB not yet decided). Either works: this data is relational (accounts + time-series snapshots).

## UI

Keep the existing `PixelCard`-based layout (see current `Finance.razor`): one card per account showing provider, balance, currency. Replace hardcoded UAH values with live `BalanceSnapshot` data. Add a total-across-accounts line.

## Open Decisions

- Pumb API availability — needs confirming before this feature can be fully implemented (Monobank alone could ship first)
- Where the Monobank API token is stored (env var vs user secrets) — resolve when implementing, per `deploy-dzhus-shelter` skill guidance (never commit it)
- Persistence engine (see above)

## Out of Scope

- Transaction-level history/categorization
- Editing balances manually
- Multi-currency conversion (that's the Exchange feature)
