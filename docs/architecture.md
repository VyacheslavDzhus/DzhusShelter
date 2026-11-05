# DzhusShelter — System Architecture

Cross-cutting architecture decisions that apply to every feature from here on, starting with Bad Habits (the first feature built this way — see [specs/bad-habits.md](specs/bad-habits.md)). Earlier specs written before this doc (Finance, Gym, etc.) describe the *feature*, not the hosting architecture; they'll be updated to match as each is actually implemented, per [roadmap.md](roadmap.md)'s progression order.

## Why a separate API

Bad Habits needs two independent clients: a Telegram bot (logs events) and the Blazor dashboard (reads/charts them). A bot is an external process — it cannot call into Blazor Server's in-process C# classes or touch the database directly. Once a real network-callable backend is required for the bot, it makes sense for Blazor to become just another client of that same backend too, rather than splitting business logic between "things Blazor does in-process" and "things exposed over HTTP for the bot." One backend, multiple clients.

This is scoped to **DzhusShelter only** — not a shared platform API for unrelated future projects. That's a bigger, separate decision (see Open Decisions in [specs/bots.md](specs/bots.md) history / roadmap topic 19, API Gateway/BFF) to make later if a real cross-project need appears.

## Solution structure

Four deployable projects (originally three; `DzhusShelter.MainBot` was added with Birthdays), replacing the original single Blazor Server app:

- **`DzhusShelter.Api`** (ASP.NET Core Web API) — owns all data and business logic behind Clean Architecture layers (below). Hosts one controller per feature (`BadHabitsController` first; `GymController`, `FinanceController`, etc. as those features get rebuilt).
- **`DzhusShelter.TelegramBot`** — a `BackgroundService` worker using the `Telegram.Bot` library, long polling (no public webhook needed). Calls `DzhusShelter.Api` over HTTP via a typed `HttpClient` for every write.
- **`DzhusShelter.UI`** (existing Blazor Server project) — becomes a pure presentation layer. No direct EF Core/DB access. Calls `DzhusShelter.Api` over HTTP via a typed `HttpClient` for every read.

- **`DzhusShelter.MainBot`** — a send-only `BackgroundService` worker for notifications (see [Multiple bots](#multiple-bots)).

All of them run as separate containers in `docker-compose.yml`, alongside PostgreSQL, on the same private Docker network (see Security below).

## Multiple bots

The project has several Telegram bots, each its own BotFather bot (own token), its own process/container and its own API key — a crash or redeploy of one never affects the other. Shared plumbing (typed `HttpClient`, settings) is deliberately duplicated until a third consumer appears.

| Bot | Project | Role |
|---|---|---|
| Manager bot | `DzhusShelter.TelegramBot` | Data entry: user taps buttons, bot writes to `Api` (Bad Habits) |
| Main bot | `DzhusShelter.MainBot` | Notifications: the bot writes to the user unprompted, send-only, polls `Api` every 5 min. Birthdays reminders are the first type ([specs/birthdays.md](specs/birthdays.md)) |

## Clean Architecture inside `DzhusShelter.Api`

Standard four-layer split (roadmap topic 4):

- **Domain** — rich entities with invariants enforced in their constructors/factories. No framework dependencies.
- **Application** — CQRS (roadmap topic 8): commands go through `ICommandHandler<TCommand>`, queries through `IQueryHandler<TQuery, TResult>`. A feature's writes and reads don't have to share a model — Bad Habits' natural read/write client split (bot writes, Blazor reads) is what makes CQRS worth adopting from this feature onward rather than waiting for Finance as originally planned.
- **Infrastructure** — EF Core `AppDbContext`, PostgreSQL, entity configurations.
- **Api** — thin controllers: parse request → dispatch command/query → return result. Input validation via `FluentValidation`.

## Data flow

**Write (bot):** user taps an inline-keyboard button → bot resolves `HabitType`/`SubType` → `POST` to `DzhusShelter.Api` with `OccurredAt = now` → API validates → `LogHabitEntryCommand` handler persists via EF Core → 200 OK → bot confirms in chat.

**Read (Blazor):** user sets filters (date range, type/subtype) on the dashboard page → `GET` to `DzhusShelter.Api` with query params → `GetHabitEntriesQuery` handler reads via EF Core → DTO list returned → Blazor renders the chart.

## Security (implemented)

`Api` and `UI` are reachable from the internet through the CasaOS host's existing Cloudflare Tunnel (originally set up for Home Assistant, host network mode — no second tunnel container). Every `Api` endpoint requires either a JWT (issued by `POST /api/auth/login` against a bcrypt-hashed admin password, used by the Blazor `UI`) or a per-client API key (`X-Api-Key`, used by the bot and by iOS Shortcuts) — see [docs/specs/public-access-and-auth.md](specs/public-access-and-auth.md) for the full design. `Api`'s port is still never published beyond `127.0.0.1` on the host (same for `UI`'s); the tunnel is the only way in from outside, and auth is required at that one entry point.

Since Birthdays, access is also scoped **per client (least privilege)**: the global `FallbackPolicy` accepts any JWT or API key except the main bot's, while `BirthdaysController` is `UiOnly` (JWT) and `BirthdayNotificationsController` is `MainBotNotifications` (main-bot key only), both as class-level policies so a future action can't fail open. Bad Habits access for the existing keys is unchanged.

## Testing

- Unit tests (xUnit + FluentAssertions) for domain invariants and command/query handlers, mocking `Infrastructure`.
- Integration tests via `Testcontainers` (spins up Postgres in Docker) hitting the real API endpoints end-to-end — first real use of `Testcontainers` per roadmap topic 15.

## Open decisions

- **Migrating existing specs**: Finance, Gym, Exchange, etc. were speced against the old "Blazor talks directly to its own services" shape. Each gets updated to the `DzhusShelter.Api` + controller shape when its turn comes in the roadmap progression, not all at once now.
- **Bots feature overlap**: [specs/bots.md](specs/bots.md) covers the `/bots` dashboard (status, test message), still a mockup. The bots themselves are settled — see [Multiple bots](#multiple-bots); reconcile the dashboard spec with them when the Bots feature gets implemented.
