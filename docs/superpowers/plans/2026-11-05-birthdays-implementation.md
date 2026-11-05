# Birthdays Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A birthdays list managed in the Blazor UI, with a new send-only "main" Telegram bot that reminds the user the day before and on the day at ~09:00 Kyiv time.

**Architecture:** Same shape as Bad Habits — `DzhusShelter.Api` owns data + all date logic (Clean Architecture + CQRS, EF Core/Postgres), `DzhusShelter.UI` and the new `DzhusShelter.MainBot` are HTTP clients. The main bot polls `GET /api/birthdays/notifications/pending` every 5 minutes, sends, then acks via `POST …/sent` (at-least-once, ack idempotent through a unique index). New per-client authorization policies restrict CRUD to the UI's JWT and notifications to the main-bot key.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core 10 + Npgsql, FluentValidation, `TimeProvider`/`FakeTimeProvider`, Telegram.Bot 22.10.3.1, Blazor Server, xUnit + FluentAssertions + NSubstitute + Testcontainers.

**Spec:** [docs/specs/birthdays.md](../../specs/birthdays.md) — read it alongside this plan.

## Global Constraints

- Target `net10.0`; package versions match what the repo already uses (`Telegram.Bot 22.10.3.1`, `Microsoft.Extensions.Hosting/Http 10.0.12`, `Microsoft.Extensions.TimeProvider.Testing 10.10.0`, `NSubstitute 6.2.0`, `FluentAssertions 8.11.0`, `xunit 2.9.3`, `Microsoft.NET.Test.Sdk 18.10.1`, `xunit.runner.visualstudio 4.0.0`).
- Time zone `Europe/Kyiv` (fallback `Europe/Kiev`), notify time `09:00`, main-bot poll interval `00:05:00`.
- "Today/tomorrow/09:00" is computed **only** in the Api, through `IBirthdayClock`. Domain methods take `DateOnly today` as a parameter and never read a clock.
- Name: trimmed, 1–100 chars. Year: optional, `>= 1900`, full date not in the future. Feb 29 allowed when `Year` is null; occurs on Feb 28 in non-leap years.
- Enums stored as strings (`HasConversion<string>().HasMaxLength(50)`), serialized as strings (Api already registers `JsonStringEnumConverter`). Every client's `JsonSerializerOptions` has `PropertyNameCaseInsensitive = true` + `JsonStringEnumConverter` (silent-default-values lesson).
- Error codes ending in `.NotFound` → 404, other failures → 400 with body `{ "error": "<message>" }` (Bad Habits' shape).
- New API key: config `Auth:ApiKeyMainBot`, env `API_KEY_MAIN_BOT`, `client_name = "main-bot"`. Main bot env: `TELEGRAM_MAIN_BOT_TOKEN`, chat ID reuses `TELEGRAM_ALLOWED_CHAT_ID`.
- UI copy is Ukrainian; bot message strings are exactly those in Task 7.
- Commit on `dev` after each task; messages in the repo's conventional style (`feat(api): …`), ending with the `Co-Authored-By` line the session gives.

## Review Focus

1. **Kyiv midnight vs UTC** — at 22:30 UTC on Dec 31 (00:30 Kyiv, Jan 1) "today" must be Jan 1. → Task 2 test `Today_AfterKyivMidnight_IsNextDayEvenThoughUtcIsStillPreviousDay`.
2. **Year boundary for "tomorrow"** — on Dec 31 a Jan 1 birthday is a `Tomorrow` reminder, with the age for the *new* year. → Task 4 test `Pending_OnDec31_JanFirstBirthdayIsTomorrowWithNewYearsAge`.
3. **Feb 29 people in a non-leap year** — appear on Feb 28 both in the UI list (`DaysUntil = 0` on Feb 28) and in reminders. → Task 1 test `NextOccurrence_Feb29InNonLeapYear_IsFeb28` + Task 4 test `Pending_Feb28NonLeap_IncludesFeb29Person`.
4. **Main bot reading the Api's JSON** — string enum `"Today"` and `DateOnly` `"2026-11-12"` must deserialize into real values, not defaults. → Task 8 test `GetPendingAsync_DeserializesStringEnumAndDateOnly`.
5. **One failed send must not block or fake the other reminder** — if `Today` fails to send, `Tomorrow` is still sent and only `Tomorrow` is acked. → Task 8 test `RunOnce_FirstSendFails_SecondStillSentAndOnlySecondMarked`.

---

### Task 1: `Birthday` domain model

**Files:**
- Create: `src/DzhusShelter.Api/Domain/Birthdays/Birthday.cs`, `BirthdayErrors.cs`, `BirthdayNotificationKind.cs`, `BirthdayNotification.cs`
- Test: `src/DzhusShelter.Api.Tests/Domain/BirthdayTests.cs`

**Interfaces:**
- Produces:
  - `enum BirthdayNotificationKind { Today = 1, Tomorrow = 2 }`
  - `Birthday` — props `Guid Id, string Name, int Day, int Month, int? Year, DateTimeOffset CreatedAt` (private setters + private parameterless ctor for EF, like `HabitEntry`)
  - `static Result<Birthday> Birthday.Create(string name, int day, int month, int? year, DateOnly today, DateTimeOffset createdAt)`
  - `Result Birthday.Update(string name, int day, int month, int? year, DateOnly today)`
  - `DateOnly Birthday.NextOccurrence(DateOnly today)`, `int? Birthday.AgeOn(DateOnly occurrence)`
  - `BirthdayErrors`: `NameRequired`, `NameTooLong`, `InvalidDate`, `YearTooEarly`, `DateInFuture` (static `Error`s, codes `Birthday.<Name>`), `static Error NotFound(Guid id)` with code `Birthday.NotFound`
  - `BirthdayNotification` — `Guid Id, BirthdayNotificationKind Kind, DateOnly ForDate, DateTimeOffset SentAt`; `static BirthdayNotification Create(BirthdayNotificationKind kind, DateOnly forDate, DateTimeOffset sentAt)`

- [ ] **Step 1: Write the failing tests** (`today` = `new DateOnly(2026, 11, 5)` unless stated)
  - `Create_TrimsName`: `"  Саша Пупкін "` → `Name == "Саша Пупкін"`
  - `Create_EmptyOrWhitespaceName_Fails` (`""`, `"   "`) → `Error == BirthdayErrors.NameRequired`
  - `Create_NameOver100Chars_Fails` → `NameTooLong`; exactly 100 chars → success
  - `Create_InvalidDayMonth_Fails` theory: (31,4,null), (30,2,null), (0,1,null), (1,13,null) → `InvalidDate`
  - `Create_Feb29WithoutYear_Succeeds`; `Create_Feb29InNonLeapYear_Fails` (29,2,2001) → `InvalidDate`; (29,2,2000) → success
  - `Create_YearBefore1900_Fails` (1,1,1899) → `YearTooEarly`
  - `Create_FullDateInFuture_Fails` (6,11,2026) → `DateInFuture`; (5,11,2026) → success (born today, age 0)
  - `Update_ChangesFields_AndValidatesLikeCreate`: update to (12,11,2001) succeeds and fields change; update with (31,4,null) fails and leaves fields unchanged
  - `NextOccurrence_TodayIsBirthday_ReturnsToday` (5,11) → 2026-11-05
  - `NextOccurrence_LaterThisYear` (12,11) → 2026-11-12; `NextOccurrence_AlreadyPassed_NextYear` (4,11) → 2027-11-04
  - `NextOccurrence_Dec31_TomorrowIsJan1`: today 2026-12-31, (1,1) → 2027-01-01
  - `NextOccurrence_Feb29InNonLeapYear_IsFeb28`: today 2027-02-01, (29,2,null) → 2027-02-28; today 2028-02-01 → 2028-02-29
  - `AgeOn_WithYear` (12,11,2001) on 2026-11-12 → 25; `AgeOn_WithoutYear_IsNull`

- [ ] **Step 2: Run** `dotnet test src/DzhusShelter.Api.Tests --filter BirthdayTests` — Expected: FAIL (types don't exist)

- [ ] **Step 3: Implement the four files.** One private `static Error? Validate(string trimmedName, int day, int month, int? year, DateOnly today)` shared by `Create` and `Update`. Date validity: `month` 1–12 and `day <= DateTime.DaysInMonth(year ?? 2000, month)` (2000 is a leap year, so Feb 29 passes when the year is unknown). `NextOccurrence`: build the date in `today.Year` (clamping Feb 29 → Feb 28 when that year isn't leap); if it's before `today`, do the same for `today.Year + 1`.

- [ ] **Step 4: Run** the same filter — Expected: PASS

- [ ] **Step 5: Commit** — `feat(api): add Birthday domain model with occurrence and age rules`

---

### Task 2: Kyiv clock

**Files:**
- Create: `src/DzhusShelter.Api/Application/Birthdays/BirthdaysOptions.cs`, `src/DzhusShelter.Api/Application/Birthdays/IBirthdayClock.cs`, `src/DzhusShelter.Api/Infrastructure/Birthdays/KyivBirthdayClock.cs`
- Modify: `src/DzhusShelter.Api/appsettings.json` (add `"Birthdays": { "TimeZone": "Europe/Kyiv", "NotifyAt": "09:00" }`)
- Test: `src/DzhusShelter.Api.Tests/Infrastructure/Birthdays/KyivBirthdayClockTests.cs`

**Interfaces:**
- Produces:
  - `sealed class BirthdaysOptions { const string SectionName = "Birthdays"; string TimeZone = "Europe/Kyiv"; TimeOnly NotifyAt = new(9, 0); }`
  - `interface IBirthdayClock { DateTimeOffset UtcNow { get; } DateOnly Today { get; } bool IsNotifyTimeReached { get; } }`
  - `sealed class KyivBirthdayClock(TimeProvider timeProvider, IOptions<BirthdaysOptions> options) : IBirthdayClock` — resolves the zone once in the ctor: `FindSystemTimeZoneById(options.TimeZone)`, on `TimeZoneNotFoundException` retry `"Europe/Kiev"`, if that also fails throw `InvalidOperationException` naming both ids (fail fast — never silently fall back to UTC).

- [ ] **Step 1: Write the failing tests** (`FakeTimeProvider` with the given UTC instant)
  - `Today_InWinter_UsesUtcPlus2`: 2026-01-15T21:59Z → `Today == 2026-01-15`; 22:00Z → 2026-01-16
  - `Today_AfterKyivMidnight_IsNextDayEvenThoughUtcIsStillPreviousDay`: 2026-12-31T22:30Z → 2027-01-01
  - `IsNotifyTimeReached_Winter`: 2026-01-15T06:59Z → false; 07:00Z → true
  - `IsNotifyTimeReached_Summer`: 2026-07-15T05:59Z → false; 06:00Z → true
  - `IsNotifyTimeReached_OnDstSwitchDay`: 2026-03-29T06:00Z (09:00 EEST) → true; 05:59Z → false
  - `UtcNow_PassesThroughTimeProvider`

- [ ] **Step 2: Run** `dotnet test src/DzhusShelter.Api.Tests --filter KyivBirthdayClockTests` — Expected: FAIL

- [ ] **Step 3: Implement** with `TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), zone)`; `Today = DateOnly.FromDateTime(local.DateTime)`; `IsNotifyTimeReached = TimeOnly.FromDateTime(local.DateTime) >= NotifyAt`.

- [ ] **Step 4: Run** — Expected: PASS

- [ ] **Step 5: Commit** — `feat(api): add Kyiv-time birthday clock on top of TimeProvider`

---

### Task 3: Birthdays CRUD — application layer

**Files:**
- Create (all under `src/DzhusShelter.Api/Application/Birthdays/`): `IBirthdayRepository.cs`, `Dtos/BirthdayDto.cs`, `Commands/CreateBirthdayCommand.cs` (+ `Handler`, `Validator`), `Commands/UpdateBirthdayCommand.cs` (+ `Handler`, `Validator`), `Commands/DeleteBirthdayCommand.cs` (+ `Handler`), `Queries/GetBirthdaysQuery.cs` (+ `Handler`)
- Test: `src/DzhusShelter.Api.Tests/Application/Birthdays/BirthdayCrudHandlersTests.cs`, `GetBirthdaysQueryHandlerTests.cs`

**Interfaces:**
- Consumes: Task 1 `Birthday`, `BirthdayErrors`; Task 2 `IBirthdayClock`
- Produces:
  - `interface IBirthdayRepository { Task AddAsync(Birthday b, CancellationToken ct); Task<Birthday?> GetByIdAsync(Guid id, CancellationToken ct); Task<IReadOnlyList<Birthday>> GetAllAsync(CancellationToken ct); Task DeleteAsync(Birthday b, CancellationToken ct); Task SaveChangesAsync(CancellationToken ct); }` (`AddAsync`/`DeleteAsync` persist immediately; `GetByIdAsync` returns a tracked entity, `SaveChangesAsync` persists `Update`)
  - `record BirthdayDto(Guid Id, string Name, int Day, int Month, int? Year, DateOnly NextOccurrence, int DaysUntil, int? TurningAge)`
  - `record CreateBirthdayCommand(string Name, int Day, int Month, int? Year)` → `ICommandHandler<CreateBirthdayCommand, Result<Guid>>`
  - `record UpdateBirthdayCommand(Guid Id, string Name, int Day, int Month, int? Year)` → `ICommandHandler<UpdateBirthdayCommand, Result>`
  - `record DeleteBirthdayCommand(Guid Id)` → `ICommandHandler<DeleteBirthdayCommand, Result>`
  - `record GetBirthdaysQuery` (no params) → `IQueryHandler<GetBirthdaysQuery, Result<IReadOnlyList<BirthdayDto>>>`
  - Validators (`AbstractValidator`, failures → `new Error("Birthdays.Validation", firstMessage)` like `LogHabitEntryCommandHandler`): `Name` not empty, max 100; `Day` 1–31; `Month` 1–12; `Year` 1900–9999 when set. Calendar rules stay in the domain.

- [ ] **Step 1: Write the failing tests** (NSubstitute repo, `IBirthdayClock` stub with `Today = 2026-11-05`, `UtcNow = 2026-11-05T10:00Z`)
  - `Create_Valid_AddsAndReturnsId`; `Create_DomainFailure_ReturnsErrorAndDoesNotAdd` (31,4) → `BirthdayErrors.InvalidDate`; `Create_ValidatorFailure` (Month 13) → code `Birthdays.Validation`
  - `Update_Existing_UpdatesAndSaves`; `Update_Missing_ReturnsNotFound` → code `Birthday.NotFound`, `SaveChangesAsync` not called
  - `Delete_Existing_Deletes`; `Delete_Missing_ReturnsNotFound`
  - `GetBirthdays_SortedByNextOccurrence_WithDaysUntilAndTurningAge`: entries (12,11,2001 "Саша"), (6,11,null "Оля"), (4,11,1990 "Іван"), (5,11,null "Борис") → order Борис (0 days), Оля (1, age null), Саша (7, 25), Іван (364, `NextOccurrence 2027-11-04`, 37)
  - `GetBirthdays_SameDay_TieBreaksByName`

- [ ] **Step 2: Run** `dotnet test src/DzhusShelter.Api.Tests --filter "BirthdayCrudHandlersTests|GetBirthdaysQueryHandlerTests"` — Expected: FAIL

- [ ] **Step 3: Implement** the records, validators and handlers (constructor injection like `LogHabitEntryCommandHandler`; `CreatedAt = clock.UtcNow`; `DaysUntil = next.DayNumber - today.DayNumber`).

- [ ] **Step 4: Run** — Expected: PASS

- [ ] **Step 5: Commit** — `feat(api): add Birthdays CRUD commands and sorted list query`

---

### Task 4: Pending notifications + mark-sent — application layer

**Files:**
- Create (under `src/DzhusShelter.Api/Application/Birthdays/`): `IBirthdayNotificationRepository.cs`, `Dtos/PendingBirthdayNotificationDto.cs`, `Queries/GetPendingBirthdayNotificationsQuery.cs` (+ `Handler`), `Commands/MarkBirthdayNotificationSentCommand.cs` (+ `Handler`, `Validator`)
- Test: `src/DzhusShelter.Api.Tests/Application/Birthdays/BirthdayNotificationHandlersTests.cs`

**Interfaces:**
- Consumes: Tasks 1–3 (`Birthday`, `BirthdayNotification`, `BirthdayNotificationKind`, `IBirthdayClock`, `IBirthdayRepository.GetAllAsync`)
- Produces:
  - `interface IBirthdayNotificationRepository { Task<bool> ExistsAsync(BirthdayNotificationKind kind, DateOnly forDate, CancellationToken ct); Task<bool> TryAddAsync(BirthdayNotification n, CancellationToken ct); }` — `TryAddAsync` returns `false` when the (kind, date) row already exists (unique violation), never throws for that case
  - `record BirthdayPersonDto(string Name, int? Age)`; `record PendingBirthdayNotificationDto(BirthdayNotificationKind Kind, DateOnly ForDate, IReadOnlyList<BirthdayPersonDto> People)`
  - `record GetPendingBirthdayNotificationsQuery` → `IQueryHandler<…, Result<IReadOnlyList<PendingBirthdayNotificationDto>>>`
  - `record MarkBirthdayNotificationSentCommand(BirthdayNotificationKind Kind, DateOnly ForDate)` → `ICommandHandler<…, Result>`; validator: `Kind` `IsInEnum`
- Rule (spec "Pending-notification rule"): nothing if `!clock.IsNotifyTimeReached`; `Today` = people with `NextOccurrence(today) == today`, `Tomorrow` = `== today.AddDays(1)`; a kind is included only if non-empty and `!ExistsAsync(kind, today)`; `ForDate = today` for both kinds; `Age = AgeOn(occurrence)`; order: `Today` then `Tomorrow`, people by `Name`.

- [ ] **Step 1: Write the failing tests** (in-memory list behind the NSubstitute repos; clock stub)
  - `Pending_BeforeNotifyTime_IsEmpty` (`IsNotifyTimeReached = false`, someone's birthday today)
  - `Pending_AfterNotifyTime_ReturnsTodayAndTomorrow`: today 2026-11-12, Саша (12,11,2001), Оля (13,11,null) → `[Today 2026-11-12 [Саша 25], Tomorrow 2026-11-12 [Оля null]]`
  - `Pending_AlreadySentToday_ExcludesThatKind` (`ExistsAsync(Today, 2026-11-12) = true`) → only `Tomorrow`
  - `Pending_NobodyDue_IsEmpty`
  - `Pending_OnDec31_JanFirstBirthdayIsTomorrowWithNewYearsAge`: today 2026-12-31, (1,1,2000) → `Tomorrow [age 27]`
  - `Pending_Feb28NonLeap_IncludesFeb29Person`: today 2027-02-28, (29,2,2000) → `Today [age 27]`
  - `Pending_SeveralSameDay_SortedByName`
  - `MarkSent_New_AddsRowWithClockUtcNow`; `MarkSent_Duplicate_StillSuccess` (`TryAddAsync` returns false → `Result.Success()`)

- [ ] **Step 2: Run** `dotnet test src/DzhusShelter.Api.Tests --filter BirthdayNotificationHandlersTests` — Expected: FAIL

- [ ] **Step 3: Implement** the records, validator and two handlers.

- [ ] **Step 4: Run** — Expected: PASS

- [ ] **Step 5: Commit** — `feat(api): add pending birthday notifications query and idempotent mark-sent`

---

### Task 5: Persistence (EF Core + migration)

**Files:**
- Create: `src/DzhusShelter.Api/Infrastructure/Birthdays/BirthdayConfiguration.cs`, `BirthdayNotificationConfiguration.cs`, `EfBirthdayRepository.cs`, `EfBirthdayNotificationRepository.cs`; migration `AddBirthdays` under `src/DzhusShelter.Api/Migrations/`
- Modify: `src/DzhusShelter.Api/Infrastructure/AppDbContext.cs` (add `DbSet<Birthday> Birthdays`, `DbSet<BirthdayNotification> BirthdayNotifications`)
- Test: `src/DzhusShelter.Api.IntegrationTests/BirthdayRepositoriesTests.cs`

**Interfaces:**
- Consumes: Task 3 `IBirthdayRepository`, Task 4 `IBirthdayNotificationRepository`
- Produces: tables `Birthdays` (`Name` max 100 required, `Day`, `Month`, `Year` nullable, `CreatedAt`) and `BirthdayNotifications` (`Kind` string max 50, `ForDate` `date`, `SentAt`, **unique index (`Kind`, `ForDate`)**)

- [ ] **Step 1: Write the failing integration tests** (use the existing `ApiWebApplicationFactory` fixture; resolve the repositories from `factory.Services.CreateScope()`)
  - `BirthdayRepository_AddUpdateDelete_RoundTrips`
  - `NotificationRepository_TryAddTwice_SecondReturnsFalse_AndOnlyOneRowExists`
  - `NotificationRepository_ExistsAsync_TrueOnlyForThatKindAndDate`

- [ ] **Step 2: Run** `dotnet test src/DzhusShelter.Api.IntegrationTests --filter BirthdayRepositoriesTests` (Docker running; needs `dangerouslyDisableSandbox` from the Bash tool) — Expected: FAIL (compile)

- [ ] **Step 3: Implement** configs + repos. `TryAddAsync`: `SaveChangesAsync` inside `try`; on `DbUpdateException { InnerException: PostgresException { SqlState: "23505" } }` detach the entry and return `false`.

- [ ] **Step 4: Register** both repos in `Program.cs` (`AddScoped`) and add the migration: `dotnet ef migrations add AddBirthdays --project src/DzhusShelter.Api` (install with `dotnet tool install --global dotnet-ef` if missing). Check the generated migration contains the unique index.

- [ ] **Step 5: Run** the tests — Expected: PASS

- [ ] **Step 6: Commit** — `feat(api): persist birthdays and sent notifications with a unique (Kind, ForDate) index`

---

### Task 6: `BirthdaysController`, DI and per-client access policies

**Files:**
- Create: `src/DzhusShelter.Api/Controllers/BirthdaysController.cs`, `src/DzhusShelter.Api/Infrastructure/Auth/AuthPolicies.cs`
- Modify: `src/DzhusShelter.Api/Program.cs` (DI for Tasks 2–4, options binding, policies, fallback), `src/DzhusShelter.Api/Infrastructure/Auth/ApiKeyAuthenticationHandler.cs` (third key), `src/DzhusShelter.Api.IntegrationTests/ApiWebApplicationFactory.cs`, `src/DzhusShelter.Api.IntegrationTests/DzhusShelter.Api.IntegrationTests.csproj` (add `Microsoft.Extensions.TimeProvider.Testing 10.10.0`)
- Test: `src/DzhusShelter.Api.IntegrationTests/BirthdaysEndpointsTests.cs`, `BirthdaysAccessTests.cs`

**Interfaces:**
- Consumes: Tasks 2–5
- Produces:
  - `static class AuthPolicies { const string UiOnly = "UiOnly"; const string MainBotNotifications = "MainBotNotifications"; const string ClientNameClaim = "client_name"; const string MainBotClient = "main-bot"; }`
  - `UiOnly`: `AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser()`; `MainBotNotifications`: `AddAuthenticationSchemes(ApiKeyAuthenticationHandler.SchemeName).RequireClaim("client_name", "main-bot")`
  - `FallbackPolicy` gains `.RequireAssertion(ctx => !ctx.User.HasClaim("client_name", "main-bot"))`
  - Routes (spec "API"): `GET/POST /api/birthdays`, `PUT/DELETE /api/birthdays/{id:guid}` with `[Authorize(Policy = AuthPolicies.UiOnly)]`; `GET /api/birthdays/notifications/pending`, `POST /api/birthdays/notifications/sent` (body = `MarkBirthdayNotificationSentCommand`) with `[Authorize(Policy = AuthPolicies.MainBotNotifications)]`. POST create → `201` `{ id }`; PUT → `204`; DELETE → `204`; mark-sent → `204`.
  - `IBirthdayClock` registered as singleton `KyivBirthdayClock`; `BirthdaysOptions` bound from section `Birthdays`.
  - Factory: `const string MainBotApiKey = "test-main-bot-key"` (env `Auth__ApiKeyMainBot`); `FakeTimeProvider BirthdayTime { get; } = new(new DateTimeOffset(2026, 11, 12, 7, 30, 0, TimeSpan.Zero))`; in `ConfigureServices` replace `IBirthdayClock` with `new KyivBirthdayClock(BirthdayTime, Options.Create(new BirthdaysOptions()))` (only the birthday clock is faked — JWT issuing/validation keeps the real clock); helpers `Task<HttpClient> CreateUiClientAsync()` (logs in with `AdminPassword`, sets `Authorization: Bearer`) and `HttpClient CreateApiKeyClient(string key)`.

- [ ] **Step 1: Write the failing tests**
  - `BirthdaysEndpointsTests` (each test first deletes all birthdays through the UI client):
    - `Crud_RoundTrip`: POST `{name:"Саша Пупкін",day:12,month:11,year:2001}` → 201; GET → one item with `daysUntil 0`, `turningAge 25`; PUT same with `year:null` → 204, GET `turningAge null`; DELETE → 204; GET → empty
    - `Create_InvalidDate_Returns400WithError` (`day:31, month:4`)
    - `UpdateAndDelete_UnknownId_Return404`
    - `Pending_ThenMarkSent_ThenOnlyTomorrowLeft`: Саша (12,11,2001) + Оля (13,11,null), clock 2026-11-12T07:30Z → pending `[Today [Саша 25], Tomorrow [Оля null]]`; POST sent `{kind:"Today", forDate:"2026-11-12"}` → 204 → pending only Tomorrow; same POST again → 204
    - `Pending_BeforeNineKyiv_IsEmpty` (clock 06:59Z)
  - `BirthdaysAccessTests` — one `[Theory]` over the spec's matrix (client ∈ {ui-jwt, bot, shortcuts, main-bot} × endpoint ∈ {`GET /api/birthdays`, `POST /api/birthdays`, `GET …/notifications/pending`, `POST …/notifications/sent`, `GET /api/bad-habits/entries?from=…&to=…`}); allowed cells assert 2xx, rejected cells assert 401 or 403

- [ ] **Step 2: Run** `dotnet test src/DzhusShelter.Api.IntegrationTests --filter "BirthdaysEndpointsTests|BirthdaysAccessTests"` — Expected: FAIL

- [ ] **Step 3: Implement** handler key, policies, fallback assertion, DI, controller (map `Result` failures: code ends with `.NotFound` → `NotFound(new { error })`, else `BadRequest(new { error })`).

- [ ] **Step 4: Run** the whole solution's tests: `dotnet test DzhusShelter.slnx` — Expected: PASS (existing Bad Habits/auth tests unchanged)

- [ ] **Step 5: Commit** — `feat(api): expose Birthdays endpoints with per-client authorization policies`

---

### Task 7: `DzhusShelter.MainBot` project + message formatter

**Files:**
- Create: `src/DzhusShelter.MainBot/DzhusShelter.MainBot.csproj` (Worker SDK, like the manager bot's, own `UserSecretsId`), `src/DzhusShelter.MainBot/Birthdays/BirthdayModels.cs`, `src/DzhusShelter.MainBot/Birthdays/BirthdayMessageFormatter.cs`, placeholder `Program.cs` (real wiring in Task 8 — Worker SDK needs an entry point to compile); `src/DzhusShelter.MainBot.Tests/DzhusShelter.MainBot.Tests.csproj` (+ `GlobalUsings.cs` with `Xunit`, NSubstitute added)
- Modify: `DzhusShelter.slnx` (add both projects)
- Test: `src/DzhusShelter.MainBot.Tests/BirthdayMessageFormatterTests.cs`

**Interfaces:**
- Produces:
  - `enum BirthdayNotificationKind { Today = 1, Tomorrow = 2 }`; `record BirthdayPerson(string Name, int? Age)`; `record PendingBirthdayNotification(BirthdayNotificationKind Kind, DateOnly ForDate, IReadOnlyList<BirthdayPerson> People)`
  - `static class BirthdayMessageFormatter { static string Format(PendingBirthdayNotification n); static string Years(int age); }`

- [ ] **Step 1: Write the failing tests** — exact strings (`\n` = newline):
  - Today, one, age 25: `"🎂 Привіт! Сьогодні у Саша Пупкін день народження — 25 років! 🥳"`
  - Today, one, no age: `"🎂 Привіт! Сьогодні у Оля Коваль день народження! 🥳"`
  - Today, two: `"🎂 Привіт! Сьогодні день народження у:\n• Саша Пупкін — 25 років 🥳\n• Оля Коваль 🥳"`
  - Tomorrow, one, age 25: `"⏰ Привіт! Завтра у Саша Пупкін день народження — 25 років 🎁"`
  - Tomorrow, one, no age: `"⏰ Привіт! Завтра у Оля Коваль день народження 🎁"`
  - Tomorrow, two: `"⏰ Привіт! Завтра день народження у:\n• Саша Пупкін — 25 років\n• Оля Коваль\n🎁 Не забудь привітати!"`
  - `Years` theory: 0→`0 років`, 1→`1 рік`, 2→`2 роки`, 4→`4 роки`, 5→`5 років`, 11→`11 років`, 12→`12 років`, 14→`14 років`, 21→`21 рік`, 22→`22 роки`, 25→`25 років`, 101→`101 рік`, 111→`111 років`

- [ ] **Step 2: Run** `dotnet test src/DzhusShelter.MainBot.Tests` — Expected: FAIL

- [ ] **Step 3: Implement** the models and formatter (plural: `n % 100` in 11–14 → `років`; else `n % 10 == 1` → `рік`; `n % 10` in 2–4 → `роки`; else `років`).

- [ ] **Step 4: Run** — Expected: PASS

- [ ] **Step 5: Commit** — `feat(main-bot): scaffold main bot with Ukrainian birthday message formatter`

---

### Task 8: Main bot worker (Api client, sender, polling service, Dockerfile)

**Files:**
- Create (under `src/DzhusShelter.MainBot/`): `Birthdays/IBirthdayNotificationsApi.cs`, `Birthdays/BirthdayNotificationsApiClient.cs`, `Birthdays/BirthdayNotificationService.cs`, `Telegram/INotificationSender.cs`, `Telegram/TelegramNotificationSender.cs`, `MainBotSettings.cs`, `appsettings.json` (placeholders), `Dockerfile` (same shape as `src/DzhusShelter.TelegramBot/Dockerfile`, entry `DzhusShelter.MainBot.dll`)
- Modify: `src/DzhusShelter.MainBot/Program.cs` (real wiring)
- Test: `src/DzhusShelter.MainBot.Tests/BirthdayNotificationsApiClientTests.cs`, `BirthdayNotificationServiceTests.cs`

**Interfaces:**
- Consumes: Task 7 models + formatter; Task 6 routes
- Produces:
  - `interface IBirthdayNotificationsApi { Task<IReadOnlyList<PendingBirthdayNotification>> GetPendingAsync(CancellationToken ct); Task MarkSentAsync(BirthdayNotificationKind kind, DateOnly forDate, CancellationToken ct); }` — client throws (`EnsureSuccessStatusCode`) on non-2xx
  - `interface INotificationSender { Task SendAsync(string text, CancellationToken ct); }` — Telegram impl calls `SendMessage(settings.ChatId, text)`
  - `sealed class MainBotSettings { long ChatId; TimeSpan PollInterval = TimeSpan.FromMinutes(5); }`
  - `sealed class BirthdayNotificationService : BackgroundService` with `public Task RunOnceAsync(CancellationToken ct)`; `ExecuteAsync` = `RunOnceAsync` immediately, then on a `PeriodicTimer(PollInterval)`; every exception from `RunOnceAsync` is caught and logged as a warning (host never crashes)
  - Config keys: `Telegram:BotToken`, `Telegram:ChatId`, `Api:BaseUrl`, `Api:ApiKey`, `MainBot:PollInterval` (missing required ones → `InvalidOperationException` at startup, like the manager bot)

- [ ] **Step 1: Write the failing tests**
  - `GetPendingAsync_DeserializesStringEnumAndDateOnly`: stub `HttpMessageHandler` returns `[{"kind":"Today","forDate":"2026-11-12","people":[{"name":"Саша Пупкін","age":25},{"name":"Оля Коваль","age":null}]}]` → `Kind == Today`, `ForDate == 2026-11-12`, ages `25`/`null`
  - `MarkSentAsync_PostsKindAndDate`: captured request is `POST /api/birthdays/notifications/sent` with JSON body `{"kind":"Today","forDate":"2026-11-12"}`
  - `RunOnce_SendsEachPendingThenMarksIt` (NSubstitute api + sender; Received in order: send Today → mark Today → send Tomorrow → mark Tomorrow)
  - `RunOnce_FirstSendFails_SecondStillSentAndOnlySecondMarked`
  - `RunOnce_MarkFails_DoesNotThrowOutOfService` (exception swallowed + logged; next item still processed)
  - `RunOnce_ApiUnreachable_NothingSent` (`GetPendingAsync` throws → no send, no throw)

- [ ] **Step 2: Run** `dotnet test src/DzhusShelter.MainBot.Tests` — Expected: FAIL

- [ ] **Step 3: Implement** client (JSON options per Global Constraints), sender, service, settings, `Program.cs` (`AddHttpClient<IBirthdayNotificationsApi, BirthdayNotificationsApiClient>` with `X-Api-Key`), Dockerfile.

- [ ] **Step 4: Run** `dotnet test DzhusShelter.slnx` — Expected: PASS; `docker build -f src/DzhusShelter.MainBot/Dockerfile .` — Expected: image builds

- [ ] **Step 5: Commit** — `feat(main-bot): poll pending birthday notifications, send via Telegram, ack after send`

---

### Task 9: Blazor `/birthdays` page (layout "A")

**Files:**
- Create: `src/DzhusShelter.UI/Services/BirthdaysApiClient.cs`, `src/DzhusShelter.UI/Components/Pages/Birthdays.razor`, `Birthdays.razor.css`
- Modify: `src/DzhusShelter.UI/Program.cs` (`AddHttpClient<BirthdaysApiClient>` + `.AddHttpMessageHandler<AuthHeaderHandler>()`, same as Bad Habits), `src/DzhusShelter.UI/Components/Layout/NavMenu.razor` (entry `🎂 Дні народження` → `birthdays`, right after Bad Habits)

**Interfaces:**
- Consumes: Task 6 routes and JSON shapes
- Produces:
  - `record BirthdayDto(Guid Id, string Name, int Day, int Month, int? Year, DateOnly NextOccurrence, int DaysUntil, int? TurningAge)`; `record BirthdayInput(string Name, int Day, int Month, int? Year)`
  - `BirthdaysApiClient`: `Task<List<BirthdayDto>> GetAllAsync(CancellationToken ct)`, `Task<string?> CreateAsync(BirthdayInput input, CancellationToken ct)`, `Task<string?> UpdateAsync(Guid id, BirthdayInput input, CancellationToken ct)`, `Task<string?> DeleteAsync(Guid id, CancellationToken ct)` — the `string?` is `null` on success, otherwise the Api's `error` message (or a generic Ukrainian message if the body has none)
- Page behaviour (spec "UI"): `@rendermode InteractiveServer`; `PixelCard`; add form (name, day, month, optional year, `+ ДОДАТИ`); list in Api order; per row `дд.мм[.рррр]`, `СЬОГОДНІ` (`DaysUntil == 0`) / `ЗАВТРА` (`== 1`) badge else `через N дн.`, `буде N` or `вік невідомий`; ✏️ inline edit (save/cancel), 🗑️ with an inline "Видалити? Так / Ні" confirmation; Api error text shown next to the form/row; list wrapped in `<ErrorBoundary>` with the Bad Habits fallback text.

- [ ] **Step 1: Run the stack locally** (Api + Postgres + UI; `docker compose up -d postgres` then `dotnet run` the Api and UI, or the full compose with `--build`), log in.
- [ ] **Step 2: Implement** client, page, nav entry.
- [ ] **Step 3: Verify in the built-in browser** at `/birthdays`: add `Саша Пупкін 12.11.2001` and `Оля Коваль` with today's day/month and no year → Оля is first with `СЬОГОДНІ`; edit Оля to add a year → age appears; add `31.04` → the Api's error shows; delete with confirmation → row disappears; reload → state persisted. Screenshot for the report.
- [ ] **Step 4: Run** `dotnet build DzhusShelter.slnx` — Expected: no errors/warnings introduced
- [ ] **Step 5: Commit** — `feat(ui): add Birthdays page with inline edit and confirmed delete`

---

### Task 10: Deployment wiring

**Files:**
- Modify: `docker-compose.yml` (service `main-bot`: image `1928374650810/dzhusshelter-main-bot:latest`, `container_name: dzhusshelter-main-bot`, env `Telegram__BotToken=${TELEGRAM_MAIN_BOT_TOKEN}`, `Telegram__ChatId=${TELEGRAM_ALLOWED_CHAT_ID}`, `Api__BaseUrl=http://api:8080`, `Api__ApiKey=${API_KEY_MAIN_BOT}`, `depends_on: api`, `restart: unless-stopped`, no ports; `api` gains `Auth__ApiKeyMainBot=${API_KEY_MAIN_BOT}`), `docker-compose.override.yml` (`build:` for `main-bot`), `.github/workflows/docker-build-push.yml` (matrix entry `main-bot` → `src/DzhusShelter.MainBot/Dockerfile`), `.env.example` (`TELEGRAM_MAIN_BOT_TOKEN=change-me`, `API_KEY_MAIN_BOT=change-me`), `docs/casaos-runbook.md` (new service in status/logs list; one-time "create bot in @BotFather + press Start" step; new `.env` keys)

- [ ] **Step 1: Make the edits.**
- [ ] **Step 2: Verify** `docker compose config` resolves the new service and `Auth__ApiKeyMainBot`; `docker compose build main-bot api` succeeds; `docker compose up -d postgres api` and `docker compose logs api` show a clean start — in particular no `InvalidOperationException` from `KyivBirthdayClock` (proves the Api image resolves `Europe/Kyiv`; if it doesn't, add `tzdata` to `src/DzhusShelter.Api/Dockerfile`'s base stage).
- [ ] **Step 3: Commit** — `chore(deploy): add main-bot service, CI image and env keys`

---

### Task 11: Docs + live end-to-end check

**Files:**
- Modify: `docs/architecture.md` (multiple bots table: manager vs main; remove "not a second bot"), `docs/specs/bots.md` (main bot now exists for notifications; `/bots` page still mockup), `docs/specs/README.md` (row `Birthdays | /birthdays | birthdays.md | Implemented`), `docs/specs/birthdays.md` (status → Implemented; note any decision changed during build), `docs/roadmap.md` (progress-log entry: topics actually exercised + bugs found only by running), `docs/specs/public-access-and-auth.md` (note that the `ApiClient` policy example was replaced by `FallbackPolicy` + `UiOnly`/`MainBotNotifications`)

- [ ] **Step 1: User prerequisite** — the main bot already exists in @BotFather (created by the user earlier); the user makes sure he has pressed Start in it, and gives the token via `dotnet user-secrets set Telegram:BotToken <token> --project src/DzhusShelter.MainBot` (never pasted into chat or committed); set `Telegram:ChatId`, `Api:BaseUrl`, `Api:ApiKey` the same way.
- [ ] **Step 2: Live run** — Api with env `Birthdays__NotifyAt=00:00` and a matching `Auth__ApiKeyMainBot`, MainBot with `DOTNET_ENVIRONMENT=Development`; add a birthday for today and one for tomorrow in the UI → both Telegram messages arrive in the real chat within one poll; restart the MainBot → no duplicate arrives.
- [ ] **Step 3: Update the docs** listed above.
- [ ] **Step 4: Commit** — `docs: mark Birthdays as implemented, document manager vs main bot`
