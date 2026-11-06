# Birthdays

**Route:** `/birthdays` · **Status:** Implemented

## Overview

A personal list of birthdays plus Telegram reminders for them. Social networks only remind about people who published their own birth date; acquaintances who didn't simply fall through the cracks and the user forgets. This feature keeps the user's own list in the database and has a bot remind him — regardless of what anyone published anywhere.

Two reminders per birthday, both sent at ~09:00 Kyiv time:
- **the day before** — so there's time to prepare/buy something;
- **on the day** — so he congratulates on time.

Entries are managed (add / edit / delete) only from the Blazor UI. The birth year is often unknown and must be addable later.

It is also the **first feature of a second Telegram bot** — see [Bots: manager vs main](#bots-manager-vs-main).

### Goals
- Never miss a birthday that's in the list
- Show the age when the birth year is known, silently omit it when not
- Survive the home server's regular power cuts: a reminder due at 09:00 while the server is off is sent as soon as it's back the same day

### Non-goals
- Adding/editing people through the bot (UI only, for now)
- Importing contacts/birthdays from Telegram, Google, phone, etc.
- Any extra per-person data (notes, gift ideas, phone, photo) — user explicitly said name + date is enough
- Configurable reminder times/offsets in the UI (hardcoded day-before + same-day at 09:00)
- Messaging/RabbitMQ/Outbox — deliberately deferred; see [Learning notes](#learning-notes)

### Anti-goals (would be a failure even if it "works")
- The same reminder arriving twice on a normal day
- A reminder for yesterday arriving after a power cut (stale "today is X's birthday" when it was yesterday)
- Wrong "today"/"tomorrow" around midnight because something computed dates in UTC instead of Kyiv time
- Ungrammatical age in Ukrainian ("21 років")

## Bots: manager vs main

The project will have **several Telegram bots**, each its own BotFather bot (own token) and its own deployable process. This replaces the earlier assumption in [architecture.md](../architecture.md) ("one bot process gaining responsibilities") — update that doc as part of this feature.

| Bot | Project | Role |
|---|---|---|
| Manager bot | `DzhusShelter.TelegramBot` (existing) | Data entry: user taps buttons, bot writes to `Api` (Bad Habits) |
| **Main bot** | **`DzhusShelter.MainBot`** (new) | **Notifications**: the bot writes to the user unprompted. Birthdays are its first notification type; more will follow (not yet defined) |

Main bot decisions:
- **Separate project + container**, not a second `TelegramBotClient` inside the existing process — a crash/redeploy of one bot never affects the other, each has its own token and its own API key.
- **Send-only for now**: no long polling, no update handling, no keyboards. It only calls Telegram's `sendMessage`.
- **No shared "bots common" library yet**. Some plumbing (typed `HttpClient` to `Api`, settings classes) will be duplicated between the two bots; extract it only when a third consumer shows up.
- Sends to the same chat ID as the manager bot (`TELEGRAM_ALLOWED_CHAT_ID`): in a private chat the chat ID equals the user's Telegram user ID, so it's the same value for any bot. **Prerequisite (manual, by the user):** create the bot in @BotFather and press **Start** in it once — Telegram refuses to let a bot message a user first (`403 Forbidden: bot can't initiate conversation`).

## Data Model

`Birthday` (Domain, rich model — invariants enforced in factory/update methods returning `Result`):
- `Id` (`Guid`)
- `Name` (`string`, required, trimmed, 1–100 chars)
- `Day` (`int`), `Month` (`int`) — required
- `Year` (`int?`) — optional; when set: `>= 1900` and not in the future (relative to Kyiv "today")
- `CreatedAt` (`DateTimeOffset`)

Invariants:
- `Day`/`Month` form a valid calendar date. **Feb 29 is allowed when `Year` is null** (could be a leap-year birthday); when `Year` is set, the full date must exist in that year (no Feb 29 for a non-leap year).
- A full date with a known year must not be in the future.
- Duplicate names are allowed (two different "Саша" are a real thing).

Domain behaviour (pure, unit-tested, takes "today" as a parameter — never reads the clock itself):
- `NextOccurrence(DateOnly today) → DateOnly` — today if it's today, otherwise the next one; correct across year boundaries (31.12 → 01.01). **Feb 29 in a non-leap year occurs on Feb 28.**
- `AgeOn(DateOnly occurrence) → int?` — `occurrence.Year - Year`, null when `Year` is null.

`BirthdayNotification` (record of what has been sent — makes reminders "once per day" and survives restarts):
- `Kind` (`Today` | `Tomorrow`, stored as string like other enums in this repo)
- `ForDate` (`DateOnly`) — the Kyiv date the notification was *for being sent on* (i.e. "today" when it was due)
- `SentAt` (`DateTimeOffset`)
- **Unique index on (`Kind`, `ForDate`)** — the database, not a check-then-insert, guarantees one row per reminder.

## Time & time zone

All "today/tomorrow/09:00" logic is in **Europe/Kyiv** local time, computed **in the Api only** (the bots and the UI never decide what "today" is). Configuration (Api `appsettings.json`, overridable by env):
- `Birthdays:TimeZone` = `Europe/Kyiv`
- `Birthdays:NotifyAt` = `09:00`

The Api uses .NET's `TimeProvider` (injected, `TimeProvider.System` in prod, `FakeTimeProvider` from `Microsoft.Extensions.TimeProvider.Testing` in tests) so the 09:00 / midnight / DST edges are testable. Constraint for the builder: verify the Api container image can resolve `Europe/Kyiv` (`TimeZoneInfo.FindSystemTimeZoneById`); older tzdata only knows `Europe/Kiev` — fall back to it if needed and add `tzdata` to the image if neither resolves.

## Application (CQRS)

Commands:
- `CreateBirthdayCommand(Name, Day, Month, Year?)` → `Result<Guid>`
- `UpdateBirthdayCommand(Id, Name, Day, Month, Year?)` → `Result` (not found → error mapped to 404)
- `DeleteBirthdayCommand(Id)` → `Result` (not found → 404)
- `MarkBirthdayNotificationSentCommand(Kind, ForDate)` → `Result` — **idempotent**: a second call for the same pair is a success no-op (catch the unique-violation, Postgres `23505`)

Queries:
- `GetBirthdaysQuery` → list for the UI, **sorted by next occurrence**, each item: `Id, Name, Day, Month, Year?, NextOccurrence, DaysUntil, TurningAge?` (computed with Kyiv "today")
- `GetPendingBirthdayNotificationsQuery` → what the main bot should send right now (see rule below)

All validated with `FluentValidation`, same pattern as Bad Habits.

### Pending-notification rule

Evaluated on each call with `now` in Kyiv time, `today = now.Date`:
- If `now < NotifyAt` → nothing is pending.
- Otherwise, for each kind:
  - `Today`: people whose `NextOccurrence(today) == today`
  - `Tomorrow`: people whose `NextOccurrence(today) == today + 1`
  - The kind is pending if that list is non-empty **and** there is no `BirthdayNotification(kind, today)` row.
- Only `today`'s date is ever considered → after a power cut, yesterday's reminders are never sent late (anti-goal), but today's are sent as soon as the bot polls again.

Response: a list of `{ kind, forDate, people: [{ name, age? }] }` where `age` is the age on the birthday itself (for `Tomorrow`, the age they turn tomorrow).

Consequences (accepted, documented, not bugs):
- A person added for today *after* today's `Today` reminder was already sent is not reminded again that day.
- A person added for today *before* any `Today` reminder was sent today (e.g. nobody else had a birthday) is reminded at the next poll, even if it's 15:00.

### Delivery guarantee

Main bot flow: get pending → send message → mark sent. This is **at-least-once**: if the bot crashes (or the Api is unreachable) between a successful `sendMessage` and the "mark sent" call, the next poll sends the same reminder again. Chosen over at-most-once (mark first, then send), because a rare duplicate is better than a silently missed birthday. Duplicates are therefore possible only in that crash window — never on a normal day. If the send succeeded but the ack keeps failing (Api/DB down, contract drift), the bot remembers the sent `(kind, date)` in memory and retries only the ack on later polls instead of resending; that memory is lost on restart, so a duplicate can occur only after a bot crash/restart between send and ack. (True exactly-once would need Telegram-side idempotency, which `sendMessage` doesn't offer — a good concrete example for the later Outbox/Inbox topic.)

## API

Access is per client (see **Access per client** below): the `/api/birthdays` CRUD is behind the class-level `UiOnly` policy (JWT only) on `BirthdaysController`; the two notification endpoints are behind the class-level `MainBotNotifications` policy (main-bot API key only) on `BirthdayNotificationsController`; everything else (e.g. Bad Habits) stays under the global `FallbackPolicy` (JWT or API key, excluding the main-bot key). There is no `ApiClient` named policy — the public-access spec's example never became code.

- `GET    /api/birthdays` → `GetBirthdaysQuery` (UI)
- `POST   /api/birthdays` → create, returns `201` with id (UI)
- `PUT    /api/birthdays/{id}` → update (UI)
- `DELETE /api/birthdays/{id}` → delete, `204` (UI)
- `GET    /api/birthdays/notifications/pending` → pending list (main bot)
- `POST   /api/birthdays/notifications/sent` body `{ kind, forDate }` → mark sent, `204`, idempotent (main bot)

Auth: add a third API key `Auth:ApiKeyMainBot` (env `API_KEY_MAIN_BOT`) to `ApiKeyAuthenticationHandler`, `client_name = "main-bot"`.

### Access per client (decided by the user: scope by client, least privilege)

Today any authenticated caller can call anything (the `client_name` claim is set but never checked). From this feature on, access follows the client:

| Endpoint group | UI (JWT) | manager bot key | Shortcuts key | **main-bot key** |
|---|---|---|---|---|
| Birthdays CRUD (`GET/POST/PUT/DELETE /api/birthdays…`) | ✅ | ❌ | ❌ | ❌ |
| Birthday notifications (`…/notifications/pending`, `…/notifications/sent`) | ❌ | ❌ | ❌ | ✅ |
| Everything else (Bad Habits, …) — **unchanged** | ✅ | ✅ | ✅ | ❌ |

Implementation shape:
- Two named policies, e.g. `UiOnly` (JWT scheme only, authenticated) on the CRUD actions and `MainBotNotifications` (ApiKey scheme, `RequireClaim("client_name", "main-bot")`) on the two notification actions. An endpoint-level `[Authorize(Policy = …)]` replaces the `FallbackPolicy` for that endpoint, which is what makes this work without touching Bad Habits.
- The `FallbackPolicy` additionally rejects `client_name == "main-bot"`, so the send-only bot's key is useless everywhere except its two endpoints.
- Bad Habits access for the existing keys is deliberately **not** tightened here (Shortcuts/manager bot keep working exactly as today); revisiting that is a separate decision.
- Rejected = 401 or 403, whichever ASP.NET Core naturally produces for the scheme mismatch — builder's call, the test just asserts "not 2xx".
- Integration tests cover the whole matrix above (each client × each group), not just the happy path.

## Main bot (`DzhusShelter.MainBot`)

- Worker project (`Microsoft.NET.Sdk.Worker`, same gotchas as the manager bot: add `Microsoft.Extensions.Hosting`/`Microsoft.Extensions.Http` explicitly), `Telegram.Bot` for sending only.
- One `BackgroundService` (`BirthdayNotificationService`): every **5 minutes** (config `MainBot:PollInterval`) → `GET pending` → for each item format + `sendMessage` → `POST sent`. Polling every 5 min instead of hourly so the 09:00 message arrives at ~09:00, not up to 09:59; it's one cheap HTTP call. Also polls immediately on start (→ fast catch-up after a power cut).
- Failure handling: Api unreachable / Telegram error (incl. 403 "bot can't initiate conversation") → log a warning, don't mark sent, retry next poll. Never crash the host.
- Config: `Telegram:BotToken` (env `TELEGRAM_MAIN_BOT_TOKEN`), `Telegram:ChatId` (env `TELEGRAM_ALLOWED_CHAT_ID`, same as manager bot), `Api:BaseUrl`, `Api:ApiKey` (env `API_KEY_MAIN_BOT`). Local dev via `dotnet user-secrets` with `DOTNET_ENVIRONMENT=Development` (manager bot lesson).

### Message format (formatting lives in the bot, pure + unit-tested)

One message per pending kind; one person vs several:

```
🎂 Привіт! Сьогодні у Саша Пупкін день народження — 25 років! 🥳

🎂 Привіт! Сьогодні день народження у:
• Саша Пупкін — 25 років 🥳
• Оля Коваль 🥳

⏰ Привіт! Завтра у Саша Пупкін день народження — 25 років 🎁

⏰ Привіт! Завтра день народження у:
• Саша Пупкін — 25 років
• Оля Коваль
🎁 Не забудь привітати!
```

- No age part at all when `age` is null.
- Name is inserted as typed — no Ukrainian case declension ("у Саша Пупкін" is the user's own phrasing).
- **Ukrainian plural for age**: ends in 1 (not 11) → `рік`; ends in 2–4 (not 12–14) → `роки`; else `років` (21 рік, 22 роки, 25 років, 11–14 років, 111 років).
- Exact emoji/wording is the builder's call within this shape.

## UI (`/birthdays`, layout "A" chosen from mockups)

- Same pixel style as Bad Habits (`PixelCard`, dark theme, 'Press Start 2P' headings). `@rendermode InteractiveServer` (needed for the form — see the Bad Habits lesson about a silently static page).
- **Add form on top:** name, day+month, optional year, "+ ДОДАТИ".
- **List below, sorted by next birthday** (order comes from the Api): name, `дд.мм[.рррр]`, "через N дн." / "буде 25" / "вік невідомий"; **"СЬОГОДНІ"** and **"ЗАВТРА"** badges.
- **✏️ = inline edit in the row** (name, date, year — e.g. add the year later); save/cancel.
- **🗑️ = delete with a confirmation** (no single-click irreversible delete).
- Validation errors from the Api (400) shown next to the form/row, not swallowed.
- Nav menu: new "Дні народження" entry (icon is the builder's call).
- New typed client `Services/BirthdaysApiClient.cs` (with the case-insensitive + `JsonStringEnumConverter` JSON options lesson from Bad Habits).

## Persistence

Same PostgreSQL database as Bad Habits (`AppDbContext`): tables `Birthdays` and `BirthdayNotifications`, one EF Core migration. Covered automatically by the weekly backups (`backup` service).

## Deployment

- `docker-compose.yml`: new `main-bot` service (image `1928374650810/dzhusshelter-main-bot:latest`, `depends_on: api`, `restart: unless-stopped`, no ports); `docker-compose.override.yml`: its `build:`.
- CI `docker-build-push.yml`: build + push the 4th image.
- `.env.example`: `TELEGRAM_MAIN_BOT_TOKEN`, `API_KEY_MAIN_BOT`; `api` gets `Auth__ApiKeyMainBot`.
- `docs/casaos-runbook.md`: new service + the one-time "press Start in the new bot" step.

## Testing

- Unit: `Birthday` invariants, `NextOccurrence` (today, tomorrow, year boundary, Feb 29 leap/non-leap), `AgeOn`; pending rule with `FakeTimeProvider` (before 09:00, after 09:00, already sent, nothing due, 23:59→00:00 Kyiv, DST switch day); mark-sent idempotency; message formatter + plural rule.
- Integration (Testcontainers, existing pattern): CRUD round-trip; pending → mark sent → pending is empty; double mark-sent is still `204`; the full **Access per client** matrix (allowed and rejected cells).
- Live (like Bad Habits): real BotFather bot receives the message; UI add/edit/delete in a real browser.

## Docs to update with this feature

- [architecture.md](../architecture.md): multiple bots (table above) instead of "one bot gaining responsibilities"
- [bots.md](bots.md): note that the "main bot" is now real (notifications), still not the `/bots` dashboard
- [README.md](README.md): Birthdays row in the status table
- [roadmap.md](../roadmap.md): progress log once done

## Learning notes

Topics this exercises for real: time zones + `TimeProvider`/`FakeTimeProvider`; a scheduled `BackgroundService`; at-least-once delivery with an idempotent acknowledgement backed by a unique constraint; per-client authorization policies (least privilege, claim-based) on top of the existing multi-scheme auth. **Deliberately simple "variant 1"** (bot polls the Api). A later step can move the same feature to Outbox → RabbitMQ → Inbox (`BirthdayReminderDue` event; the Inbox on the bot side is what would stop the duplicate in the crash window above) — kept as a separate roadmap step so the difference is visible.

## Changes during implementation

Decisions that refine or differ from the spec above (the rest was built as written):

- **Two controllers, not one**: `BirthdaysController` (CRUD, class-level `UiOnly` policy, JWT only) and `BirthdayNotificationsController` (class-level `MainBotNotifications`, main-bot key only). Per-action `[Authorize]` would fail open for any future action added without it; class-level policies fail closed.
- **"виповнюється N" on the day**: on the birthday itself the UI shows the age being turned instead of "через 0 дн.".
- **Error messages split by cause** on the page: connection failure, expired session, and server/validation error each get their own text. Blazor InteractiveServer event handlers are not covered by the page's `ErrorBoundary`, so transport failures (timeouts) are caught explicitly instead of killing the circuit.
- **Zero-people notifications are skipped**: the pending query never returns a kind with an empty list, and the main bot skips such items.
- **`Telegram:ChatId` must be > 0**: `MainBotConfig` validates at startup; an appsettings placeholder `0` used to slip past a null check.
- **Time zone resolved at startup**: `KyivBirthdayClock` is resolved eagerly after `Build()`, so a missing `Europe/Kyiv` fails the Api at boot rather than on the first request. The Api image resolves the zone without extra tzdata.
- **Out-of-range years**: validation rejects them with 400 (previously `DateTime.DaysInMonth` threw, giving a 500).
- **Access matrix** is covered by a 28-cell client × endpoint integration test.

## Open Decisions

None blocking. Builder's calls are marked inline (emoji/wording within the shape, nav icon).

## Out of Scope

- Adding birthdays via the bot, reminders for anything other than birthdays
- The `/bots` dashboard page (see [bots.md](bots.md))
- Off-box notifications channels (email, push) — Telegram only
- A Birthdays card on the Home dashboard ([home.md](home.md)) — not asked for; easy follow-up reusing `GetBirthdaysQuery` *(builder-check call)*
