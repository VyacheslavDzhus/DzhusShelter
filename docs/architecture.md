# DzhusShelter — System Architecture

Cross-cutting architecture decisions that apply to every feature from here on, starting with Bad Habits (the first feature built this way — see [specs/bad-habits.md](specs/bad-habits.md)). Earlier specs written before this doc (Finance, Gym, etc.) describe the *feature*, not the hosting architecture; they'll be updated to match as each is actually implemented, per [roadmap.md](roadmap.md)'s progression order.

## Why a separate API

Bad Habits needs two independent clients: a Telegram bot (logs events) and the Blazor dashboard (reads/charts them). A bot is an external process — it cannot call into Blazor Server's in-process C# classes or touch the database directly. Once a real network-callable backend is required for the bot, it makes sense for Blazor to become just another client of that same backend too, rather than splitting business logic between "things Blazor does in-process" and "things exposed over HTTP for the bot." One backend, multiple clients.

This is scoped to **DzhusShelter only** — not a shared platform API for unrelated future projects. That's a bigger, separate decision (see Open Decisions in [specs/bots.md](specs/bots.md) history / roadmap topic 19, API Gateway/BFF) to make later if a real cross-project need appears.

## Solution structure

Three deployable projects, replacing the current single Blazor Server app:

- **`DzhusShelter.Api`** (ASP.NET Core Web API) — owns all data and business logic behind Clean Architecture layers (below). Hosts one controller per feature (`BadHabitsController` first; `GymController`, `FinanceController`, etc. as those features get rebuilt).
- **`DzhusShelter.TelegramBot`** — a `BackgroundService` worker using the `Telegram.Bot` library, long polling (no public webhook needed). Calls `DzhusShelter.Api` over HTTP via a typed `HttpClient` for every write.
- **`DzhusShelter.UI`** (existing Blazor Server project) — becomes a pure presentation layer. No direct EF Core/DB access. Calls `DzhusShelter.Api` over HTTP via a typed `HttpClient` for every read.

All three run as separate containers in `docker-compose.yml`, alongside PostgreSQL, on the same private Docker network (see Security below).

## Clean Architecture inside `DzhusShelter.Api`

Standard four-layer split (roadmap topic 4):

- **Domain** — rich entities with invariants enforced in their constructors/factories. No framework dependencies.
- **Application** — CQRS (roadmap topic 8): commands go through `ICommandHandler<TCommand>`, queries through `IQueryHandler<TQuery, TResult>`. A feature's writes and reads don't have to share a model — Bad Habits' natural read/write client split (bot writes, Blazor reads) is what makes CQRS worth adopting from this feature onward rather than waiting for Finance as originally planned.
- **Infrastructure** — EF Core `AppDbContext`, PostgreSQL, entity configurations.
- **Api** — thin controllers: parse request → dispatch command/query → return result. Input validation via `FluentValidation`.

## Data flow

**Write (bot):** user taps an inline-keyboard button → bot resolves `HabitType`/`SubType` → `POST` to `DzhusShelter.Api` with `OccurredAt = now` → API validates → `LogHabitEntryCommand` handler persists via EF Core → 200 OK → bot confirms in chat.

**Read (Blazor):** user sets filters (date range, type/subtype) on the dashboard page → `GET` to `DzhusShelter.Api` with query params → `GetHabitEntriesQuery` handler reads via EF Core → DTO list returned → Blazor renders the chart.

## Security (explicit, deferred)

No authentication between the bot, Blazor, and the API yet — that's roadmap topic 12, done properly later (JWT/OAuth), not bolted on piecemeal now. This is only acceptable under one condition, which every deployment of this architecture must honor: **`DzhusShelter.Api`'s port is never exposed outside the private Docker network** (bot, UI, and API all reach each other over the internal `docker-compose` network on the CasaOS host — see `deploy-dzhus-shelter` skill). If a public-facing use case ever requires exposing the API directly, auth must be added first — don't defer both at once.

## Testing

- Unit tests (xUnit + FluentAssertions) for domain invariants and command/query handlers, mocking `Infrastructure`.
- Integration tests via `Testcontainers` (spins up Postgres in Docker) hitting the real API endpoints end-to-end — first real use of `Testcontainers` per roadmap topic 15.

## Open decisions

- **Migrating existing specs**: Finance, Gym, Exchange, etc. were speced against the old "Blazor talks directly to its own services" shape. Each gets updated to the `DzhusShelter.Api` + controller shape when its turn comes in the roadmap progression, not all at once now.
- **Bots feature overlap**: [specs/bots.md](specs/bots.md) already covers a Telegram bot for a different purpose (relaying info, sending test messages). `DzhusShelter.TelegramBot` as introduced here is the same bot process gaining a second responsibility (habit logging), not a second bot — reconcile the two specs when the Bots feature itself gets implemented.
