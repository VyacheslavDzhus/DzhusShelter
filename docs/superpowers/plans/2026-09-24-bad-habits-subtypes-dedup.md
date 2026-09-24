# Bad Habits: New Subtypes + Per-Day Dedup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expand `HabitSubType` with 7 new alcohol/smoking subtypes (dropping the `Spirits` catch-all), and enforce "at most one entry per subtype per calendar day" so the Telegram bot stops creating duplicate rows when the same drink/cigarette is logged twice in a day.

**Architecture:** No new projects or services. Changes ripple through the existing layers of `DzhusShelter.Api` (Domain enum + `ValidSubTypes` mapping, `LogHabitEntryCommandHandler` gains a dedup check via the existing `IHabitEntryRepository.GetAsync`, `BadHabitsController` returns a richer response), then out to the two HTTP consumers that keep their own copies of the enum: `DzhusShelter.TelegramBot` (new Ukrainian display names, button/message wiring) and `DzhusShelter.UI` (enum sync only — its own filter UI is unaffected, that's a separate future phase).

**Tech Stack:** .NET 10, xUnit, FluentAssertions, NSubstitute, FluentValidation, ASP.NET Core Web API, EF Core/PostgreSQL, Testcontainers (integration tests), Telegram.Bot.

**Spec:** [docs/specs/bad-habits.md](../../specs/bad-habits.md)

## Global Constraints

- `HabitType`/`HabitSubType` stay duplicated across `DzhusShelter.Api`, `DzhusShelter.TelegramBot`, and `DzhusShelter.UI` — no shared `Contracts` project (spec's explicit decision). Every enum edit must be applied identically in all three copies.
- Dedup key is `(HabitType, SubType, calendar day)`, where "calendar day" is the UTC date of `OccurredAt` — matches how the existing chart already groups `OccurredAt.Date`.
- No new `IHabitEntryRepository` method — the dedup check reuses the existing `GetAsync(from, to, habitType, subType, cancellationToken)`.
- `POST /api/bad-habits/entries` returns `200 OK { id, alreadyLogged }` (not `201 Created`) — a duplicate call creates nothing, so `Created` semantics would be misleading.
- Telegram bot buttons/messages show Ukrainian display labels (e.g. `"Пиво"`), never the raw C# enum member name. No icons on bot buttons — Telegram inline keyboard labels are plain text only.
- This plan does **not** touch `BadHabits.razor`'s UI beyond keeping its private `HabitSubType` enum copy in sync — the calendar/subtype-stats card redesign is a separate, later plan.

---

### Task 1: Domain — new subtypes and valid-subtype mapping

**Files:**
- Modify: `src/DzhusShelter.Api/Domain/BadHabits/HabitSubType.cs`
- Modify: `src/DzhusShelter.Api/Domain/BadHabits/HabitEntry.cs:7-11` (the `ValidSubTypes` dictionary)
- Test: `src/DzhusShelter.Api.Tests/Domain/HabitEntryTests.cs`

**Interfaces:**
- Produces: `HabitSubType` enum members `Cigarette=1, Vape=2, Beer=3, Wine=4, Iqos=6, Hookah=7, Vodka=8, Whiskey=9, Rum=10, Gin=11, Martini=12` (value `5`, formerly `Spirits`, is retired and left unused). `HabitEntry.Create(HabitType, HabitSubType, DateTimeOffset, string?, TimeProvider)` now accepts all of the above for their matching `HabitType`.

- [ ] **Step 1: Write the failing test**

Add this theory test to `src/DzhusShelter.Api.Tests/Domain/HabitEntryTests.cs` (keep the existing tests in that file untouched):

```csharp
[Theory]
[InlineData(HabitType.Smoking, HabitSubType.Cigarette)]
[InlineData(HabitType.Smoking, HabitSubType.Vape)]
[InlineData(HabitType.Smoking, HabitSubType.Iqos)]
[InlineData(HabitType.Smoking, HabitSubType.Hookah)]
[InlineData(HabitType.Alcohol, HabitSubType.Beer)]
[InlineData(HabitType.Alcohol, HabitSubType.Wine)]
[InlineData(HabitType.Alcohol, HabitSubType.Vodka)]
[InlineData(HabitType.Alcohol, HabitSubType.Whiskey)]
[InlineData(HabitType.Alcohol, HabitSubType.Rum)]
[InlineData(HabitType.Alcohol, HabitSubType.Gin)]
[InlineData(HabitType.Alcohol, HabitSubType.Martini)]
public void Create_WithEachValidHabitTypeSubTypePair_Succeeds(HabitType habitType, HabitSubType subType)
{
    var result = HabitEntry.Create(habitType, subType, Now.AddMinutes(-5), null, CreateTimeProvider());

    result.IsSuccess.Should().BeTrue();
    result.Value.HabitType.Should().Be(habitType);
    result.Value.SubType.Should().Be(subType);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/DzhusShelter.Api.Tests/DzhusShelter.Api.Tests.csproj --filter "FullyQualifiedName~Create_WithEachValidHabitTypeSubTypePair_Succeeds"`
Expected: build error — `HabitSubType.Iqos`/`Hookah`/`Vodka`/`Whiskey`/`Rum`/`Gin`/`Martini` don't exist yet.

- [ ] **Step 3: Add the new enum members**

Replace the contents of `src/DzhusShelter.Api/Domain/BadHabits/HabitSubType.cs`:

```csharp
namespace DzhusShelter.Api.Domain.BadHabits;

public enum HabitSubType
{
    Cigarette = 1,
    Vape = 2,
    Beer = 3,
    Wine = 4,
    // 5 (formerly Spirits) retired — replaced by the specific spirits below, no data referenced it.
    Iqos = 6,
    Hookah = 7,
    Vodka = 8,
    Whiskey = 9,
    Rum = 10,
    Gin = 11,
    Martini = 12,
}
```

- [ ] **Step 4: Update the valid-subtype mapping**

In `src/DzhusShelter.Api/Domain/BadHabits/HabitEntry.cs`, replace the `ValidSubTypes` dictionary:

```csharp
private static readonly Dictionary<HabitType, HabitSubType[]> ValidSubTypes = new()
{
    [HabitType.Smoking] = [HabitSubType.Cigarette, HabitSubType.Vape, HabitSubType.Iqos, HabitSubType.Hookah],
    [HabitType.Alcohol] = [HabitSubType.Beer, HabitSubType.Wine, HabitSubType.Vodka, HabitSubType.Whiskey, HabitSubType.Rum, HabitSubType.Gin, HabitSubType.Martini],
};
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test src/DzhusShelter.Api.Tests/DzhusShelter.Api.Tests.csproj`
Expected: PASS, all tests in `HabitEntryTests` (existing + the new theory) green.

- [ ] **Step 6: Commit**

```bash
git add src/DzhusShelter.Api/Domain/BadHabits/HabitSubType.cs src/DzhusShelter.Api/Domain/BadHabits/HabitEntry.cs src/DzhusShelter.Api.Tests/Domain/HabitEntryTests.cs
git commit -m "feat(api): add new alcohol/smoking subtypes, retire Spirits"
```

---

### Task 2: Application — dedup rule in LogHabitEntryCommandHandler

**Files:**
- Create: `src/DzhusShelter.Api/Application/BadHabits/Commands/LogHabitEntryResult.cs`
- Modify: `src/DzhusShelter.Api/Application/BadHabits/Commands/LogHabitEntryCommandHandler.cs`
- Test: `src/DzhusShelter.Api.Tests/Application/LogHabitEntryCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `IHabitEntryRepository.GetAsync(DateTimeOffset from, DateTimeOffset to, HabitType? habitType, HabitSubType? subType, CancellationToken)` (unchanged, from Task 1's dependencies — already exists in `src/DzhusShelter.Api/Application/BadHabits/IHabitEntryRepository.cs`), `HabitEntry.Create(...)` (Task 1).
- Produces: `LogHabitEntryResult(Guid Id, bool AlreadyLogged)`. `LogHabitEntryCommandHandler` now implements `ICommandHandler<LogHabitEntryCommand, Result<LogHabitEntryResult>>` (was `Result<Guid>`) — Task 3 (controller) and Task 6 (bot's expectations of the wire response) depend on this shape.

- [ ] **Step 1: Write the failing tests**

Replace `src/DzhusShelter.Api.Tests/Application/LogHabitEntryCommandHandlerTests.cs` in full:

```csharp
using DzhusShelter.Api.Application.BadHabits;
using DzhusShelter.Api.Application.BadHabits.Commands;
using DzhusShelter.Api.Domain.BadHabits;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace DzhusShelter.Api.Tests.Application;

public class LogHabitEntryCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WithValidCommand_PersistsEntryAndReturnsNewId()
    {
        var repository = Substitute.For<IHabitEntryRepository>();
        repository
            .GetAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<HabitType>(), Arg.Any<HabitSubType>(), Arg.Any<CancellationToken>())
            .Returns(new List<HabitEntry>());
        var handler = new LogHabitEntryCommandHandler(repository, new LogHabitEntryCommandValidator(), new FakeTimeProvider(Now));
        var command = new LogHabitEntryCommand(HabitType.Alcohol, HabitSubType.Beer, Now.AddMinutes(-1), "with friends");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBeEmpty();
        result.Value.AlreadyLogged.Should().BeFalse();
        await repository.Received(1).AddAsync(
            Arg.Is<HabitEntry>(e => e.HabitType == HabitType.Alcohol && e.SubType == HabitSubType.Beer),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithMismatchedSubType_DoesNotPersistAndReturnsFailure()
    {
        var repository = Substitute.For<IHabitEntryRepository>();
        var handler = new LogHabitEntryCommandHandler(repository, new LogHabitEntryCommandValidator(), new FakeTimeProvider(Now));
        var command = new LogHabitEntryCommand(HabitType.Smoking, HabitSubType.Wine, Now, null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        await repository.DidNotReceive().AddAsync(Arg.Any<HabitEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSameSubTypeAlreadyLoggedToday_DoesNotPersistAndReturnsAlreadyLogged()
    {
        var repository = Substitute.For<IHabitEntryRepository>();
        var existingEntry = HabitEntry.Create(HabitType.Alcohol, HabitSubType.Beer, Now.AddHours(-2), null, new FakeTimeProvider(Now)).Value;
        repository
            .GetAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), HabitType.Alcohol, HabitSubType.Beer, Arg.Any<CancellationToken>())
            .Returns(new List<HabitEntry> { existingEntry });
        var handler = new LogHabitEntryCommandHandler(repository, new LogHabitEntryCommandValidator(), new FakeTimeProvider(Now));
        var command = new LogHabitEntryCommand(HabitType.Alcohol, HabitSubType.Beer, Now.AddMinutes(-1), null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(existingEntry.Id);
        result.Value.AlreadyLogged.Should().BeTrue();
        await repository.DidNotReceive().AddAsync(Arg.Any<HabitEntry>(), Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/DzhusShelter.Api.Tests/DzhusShelter.Api.Tests.csproj --filter "FullyQualifiedName~LogHabitEntryCommandHandlerTests"`
Expected: build error — `LogHabitEntryResult` doesn't exist yet, and `result.Value.Id`/`result.Value.AlreadyLogged` don't compile against `Result<Guid>`.

- [ ] **Step 3: Create the result record**

```csharp
namespace DzhusShelter.Api.Application.BadHabits.Commands;

public sealed record LogHabitEntryResult(Guid Id, bool AlreadyLogged);
```

- [ ] **Step 4: Implement the dedup check in the handler**

Replace `src/DzhusShelter.Api/Application/BadHabits/Commands/LogHabitEntryCommandHandler.cs` in full:

```csharp
using DzhusShelter.Api.Application.Abstractions;
using DzhusShelter.Api.Application.BadHabits;
using DzhusShelter.Api.Domain.BadHabits;
using DzhusShelter.Api.Domain.Common;
using FluentValidation;

namespace DzhusShelter.Api.Application.BadHabits.Commands;

public sealed class LogHabitEntryCommandHandler : ICommandHandler<LogHabitEntryCommand, Result<LogHabitEntryResult>>
{
    private readonly IHabitEntryRepository _repository;
    private readonly IValidator<LogHabitEntryCommand> _validator;
    private readonly TimeProvider _timeProvider;

    public LogHabitEntryCommandHandler(
        IHabitEntryRepository repository,
        IValidator<LogHabitEntryCommand> validator,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _validator = validator;
        _timeProvider = timeProvider;
    }

    public async Task<Result<LogHabitEntryResult>> Handle(LogHabitEntryCommand command, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Failure<LogHabitEntryResult>(new Error("BadHabits.Validation", validationResult.Errors[0].ErrorMessage));

        var entryResult = HabitEntry.Create(command.HabitType, command.SubType, command.OccurredAt, command.Notes, _timeProvider);
        if (entryResult.IsFailure)
            return Result.Failure<LogHabitEntryResult>(entryResult.Error);

        var dayStart = new DateTimeOffset(command.OccurredAt.UtcDateTime.Date, TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1).AddTicks(-1);
        var existingEntries = await _repository.GetAsync(dayStart, dayEnd, command.HabitType, command.SubType, cancellationToken);
        if (existingEntries.Count > 0)
            return Result.Success(new LogHabitEntryResult(existingEntries[0].Id, AlreadyLogged: true));

        await _repository.AddAsync(entryResult.Value, cancellationToken);
        return Result.Success(new LogHabitEntryResult(entryResult.Value.Id, AlreadyLogged: false));
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test src/DzhusShelter.Api.Tests/DzhusShelter.Api.Tests.csproj`
Expected: PASS (note: the whole solution won't build yet — `BadHabitsController`/`Program.cs` still reference `Result<Guid>`. That's fixed in Task 3; it's fine for this task's own test project to fail to build the full solution as long as `dotnet test` on the `Api` + `Api.Tests` projects together — which is all this command touches — succeeds. If your tooling insists on building the whole `.slnx`, skip ahead and run Task 3's Step 1 first, then come back.)

- [ ] **Step 6: Commit**

```bash
git add src/DzhusShelter.Api/Application/BadHabits/Commands/LogHabitEntryResult.cs src/DzhusShelter.Api/Application/BadHabits/Commands/LogHabitEntryCommandHandler.cs src/DzhusShelter.Api.Tests/Application/LogHabitEntryCommandHandlerTests.cs
git commit -m "feat(api): dedup habit entries per subtype per calendar day"
```

---

### Task 3: Controller + DI — new response shape

**Files:**
- Modify: `src/DzhusShelter.Api/Controllers/BadHabitsController.cs`
- Modify: `src/DzhusShelter.Api/Program.cs:2-4,24` (using directives + the `AddScoped<ICommandHandler<...>>` line)
- Test: `src/DzhusShelter.Api.Tests/Controllers/BadHabitsControllerTests.cs`

**Interfaces:**
- Consumes: `LogHabitEntryResult` (Task 2), `ICommandHandler<LogHabitEntryCommand, Result<LogHabitEntryResult>>` (Task 2).
- Produces: `POST /api/bad-habits/entries` now returns `200 OK` with JSON body `{ "id": "<guid>", "alreadyLogged": <bool> }` on success (was `201 Created` with `{ "id": "<guid>" }`) — Task 4 (integration tests) and Task 6 (bot's HTTP client) depend on this exact shape.

- [ ] **Step 1: Write the failing tests**

Replace `src/DzhusShelter.Api.Tests/Controllers/BadHabitsControllerTests.cs` in full:

```csharp
using DzhusShelter.Api.Application.Abstractions;
using DzhusShelter.Api.Application.BadHabits.Commands;
using DzhusShelter.Api.Application.BadHabits.Dtos;
using DzhusShelter.Api.Application.BadHabits.Queries;
using DzhusShelter.Api.Controllers;
using DzhusShelter.Api.Domain.BadHabits;
using DzhusShelter.Api.Domain.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace DzhusShelter.Api.Tests.Controllers;

public class BadHabitsControllerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task LogEntry_WhenNewlyLogged_ReturnsOkWithAlreadyLoggedFalse()
    {
        var commandHandler = Substitute.For<ICommandHandler<LogHabitEntryCommand, Result<LogHabitEntryResult>>>();
        var entryId = Guid.NewGuid();
        commandHandler.Handle(Arg.Any<LogHabitEntryCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new LogHabitEntryResult(entryId, AlreadyLogged: false)));
        var queryHandler = Substitute.For<IQueryHandler<GetHabitEntriesQuery, Result<IReadOnlyList<HabitEntryDto>>>>();
        var controller = new BadHabitsController(commandHandler, queryHandler);

        var response = await controller.LogEntry(
            new LogHabitEntryCommand(HabitType.Smoking, HabitSubType.Cigarette, Now, null), CancellationToken.None);

        var okResult = response.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(new { id = entryId, alreadyLogged = false });
    }

    [Fact]
    public async Task LogEntry_WhenAlreadyLoggedToday_ReturnsOkWithAlreadyLoggedTrue()
    {
        var commandHandler = Substitute.For<ICommandHandler<LogHabitEntryCommand, Result<LogHabitEntryResult>>>();
        var existingId = Guid.NewGuid();
        commandHandler.Handle(Arg.Any<LogHabitEntryCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new LogHabitEntryResult(existingId, AlreadyLogged: true)));
        var queryHandler = Substitute.For<IQueryHandler<GetHabitEntriesQuery, Result<IReadOnlyList<HabitEntryDto>>>>();
        var controller = new BadHabitsController(commandHandler, queryHandler);

        var response = await controller.LogEntry(
            new LogHabitEntryCommand(HabitType.Smoking, HabitSubType.Cigarette, Now, null), CancellationToken.None);

        var okResult = response.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(new { id = existingId, alreadyLogged = true });
    }

    [Fact]
    public async Task LogEntry_WhenHandlerFails_ReturnsBadRequest()
    {
        var commandHandler = Substitute.For<ICommandHandler<LogHabitEntryCommand, Result<LogHabitEntryResult>>>();
        commandHandler.Handle(Arg.Any<LogHabitEntryCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<LogHabitEntryResult>(new Error("BadHabits.Validation", "bad input")));
        var queryHandler = Substitute.For<IQueryHandler<GetHabitEntriesQuery, Result<IReadOnlyList<HabitEntryDto>>>>();
        var controller = new BadHabitsController(commandHandler, queryHandler);

        var response = await controller.LogEntry(
            new LogHabitEntryCommand(HabitType.Smoking, HabitSubType.Cigarette, Now, null), CancellationToken.None);

        response.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetEntries_WhenHandlerSucceeds_ReturnsOkWithEntries()
    {
        var commandHandler = Substitute.For<ICommandHandler<LogHabitEntryCommand, Result<LogHabitEntryResult>>>();
        var queryHandler = Substitute.For<IQueryHandler<GetHabitEntriesQuery, Result<IReadOnlyList<HabitEntryDto>>>>();
        var dtos = new List<HabitEntryDto>
        {
            new(Guid.NewGuid(), HabitType.Smoking, HabitSubType.Cigarette, Now, null),
        };
        queryHandler.Handle(Arg.Any<GetHabitEntriesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<HabitEntryDto>>(dtos));
        var controller = new BadHabitsController(commandHandler, queryHandler);

        var response = await controller.GetEntries(
            new GetHabitEntriesQuery(Now.AddDays(-1), Now, null, null), CancellationToken.None);

        var okResult = response.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(dtos);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/DzhusShelter.Api.Tests/DzhusShelter.Api.Tests.csproj --filter "FullyQualifiedName~BadHabitsControllerTests"`
Expected: build error — `BadHabitsController`'s constructor still expects `ICommandHandler<LogHabitEntryCommand, Result<Guid>>`.

- [ ] **Step 3: Update the controller**

In `src/DzhusShelter.Api/Controllers/BadHabitsController.cs`, change the field/constructor type and the `LogEntry` action:

```csharp
private readonly ICommandHandler<LogHabitEntryCommand, Result<LogHabitEntryResult>> _logHandler;
private readonly IQueryHandler<GetHabitEntriesQuery, Result<IReadOnlyList<HabitEntryDto>>> _getHandler;

public BadHabitsController(
    ICommandHandler<LogHabitEntryCommand, Result<LogHabitEntryResult>> logHandler,
    IQueryHandler<GetHabitEntriesQuery, Result<IReadOnlyList<HabitEntryDto>>> getHandler)
{
    _logHandler = logHandler;
    _getHandler = getHandler;
}

[HttpPost("entries")]
public async Task<IActionResult> LogEntry([FromBody] LogHabitEntryCommand command, CancellationToken cancellationToken)
{
    var result = await _logHandler.Handle(command, cancellationToken);
    return result.IsSuccess
        ? Ok(new { id = result.Value.Id, alreadyLogged = result.Value.AlreadyLogged })
        : BadRequest(new { error = result.Error.Message });
}
```

(`GetEntries` is unchanged.)

- [ ] **Step 4: Update DI registration**

In `src/DzhusShelter.Api/Program.cs`, change:

```csharp
builder.Services.AddScoped<ICommandHandler<LogHabitEntryCommand, Result<Guid>>, LogHabitEntryCommandHandler>();
```

to:

```csharp
builder.Services.AddScoped<ICommandHandler<LogHabitEntryCommand, Result<LogHabitEntryResult>>, LogHabitEntryCommandHandler>();
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet build DzhusShelter.slnx` then `dotnet test src/DzhusShelter.Api.Tests/DzhusShelter.Api.Tests.csproj`
Expected: solution builds, all `Api.Tests` PASS.

- [ ] **Step 6: Commit**

```bash
git add src/DzhusShelter.Api/Controllers/BadHabitsController.cs src/DzhusShelter.Api/Program.cs src/DzhusShelter.Api.Tests/Controllers/BadHabitsControllerTests.cs
git commit -m "feat(api): return alreadyLogged flag from POST /api/bad-habits/entries"
```

---

### Task 4: Integration tests — response shape + dedup round-trip

**Files:**
- Modify: `src/DzhusShelter.Api.IntegrationTests/BadHabitsEndpointsTests.cs`

**Interfaces:**
- Consumes: the `200 OK { id, alreadyLogged }` response from Task 3, `LogHabitEntryCommand`/`HabitEntryDto` (unchanged shapes) from `DzhusShelter.Api.Application.BadHabits.Commands`/`Dtos`.

- [ ] **Step 1: Write the failing tests**

Replace `src/DzhusShelter.Api.IntegrationTests/BadHabitsEndpointsTests.cs` in full:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DzhusShelter.Api.Application.BadHabits.Commands;
using DzhusShelter.Api.Application.BadHabits.Dtos;
using DzhusShelter.Api.Domain.BadHabits;
using FluentAssertions;

namespace DzhusShelter.Api.IntegrationTests;

public class BadHabitsEndpointsTests : IClassFixture<ApiWebApplicationFactory>
{
    // The API serializes enums as strings (see DzhusShelter.Api's JsonStringEnumConverter
    // registration) — HttpClient's default JSON options don't include that converter, so
    // without it here, deserializing "Alcohol" into HabitType would throw a JsonException.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _client;

    public BadHabitsEndpointsTests(ApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private sealed record LogEntryResponse(Guid Id, bool AlreadyLogged);

    [Fact]
    public async Task LoggingAnEntry_ThenFetchingIt_RoundTripsThroughPostgres()
    {
        var occurredAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var logResponse = await _client.PostAsJsonAsync("/api/bad-habits/entries",
            new LogHabitEntryCommand(HabitType.Alcohol, HabitSubType.Beer, occurredAt, "friday"));
        logResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var logBody = await logResponse.Content.ReadFromJsonAsync<LogEntryResponse>(JsonOptions);
        logBody!.AlreadyLogged.Should().BeFalse();

        var getResponse = await _client.GetAsync(
            $"/api/bad-habits/entries?from={Uri.EscapeDataString(occurredAt.AddMinutes(-1).ToString("O"))}" +
            $"&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"))}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var entries = await getResponse.Content.ReadFromJsonAsync<List<HabitEntryDto>>(JsonOptions);
        entries.Should().ContainSingle(e => e.HabitType == HabitType.Alcohol && e.SubType == HabitSubType.Beer && e.Notes == "friday");
    }

    [Fact]
    public async Task LoggingTheSameSubTypeTwiceOnTheSameDay_DoesNotDuplicate()
    {
        var occurredAt = DateTimeOffset.UtcNow.AddMinutes(-20);
        var first = await _client.PostAsJsonAsync("/api/bad-habits/entries",
            new LogHabitEntryCommand(HabitType.Smoking, HabitSubType.Hookah, occurredAt, null));
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstBody = await first.Content.ReadFromJsonAsync<LogEntryResponse>(JsonOptions);

        var second = await _client.PostAsJsonAsync("/api/bad-habits/entries",
            new LogHabitEntryCommand(HabitType.Smoking, HabitSubType.Hookah, occurredAt.AddMinutes(5), null));
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondBody = await second.Content.ReadFromJsonAsync<LogEntryResponse>(JsonOptions);

        firstBody!.AlreadyLogged.Should().BeFalse();
        secondBody!.AlreadyLogged.Should().BeTrue();
        secondBody.Id.Should().Be(firstBody.Id);

        var getResponse = await _client.GetAsync(
            $"/api/bad-habits/entries?from={Uri.EscapeDataString(occurredAt.AddDays(-1).ToString("O"))}" +
            $"&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"))}" +
            $"&habitType={HabitType.Smoking}&subType={HabitSubType.Hookah}");
        var entries = await getResponse.Content.ReadFromJsonAsync<List<HabitEntryDto>>(JsonOptions);
        entries.Should().HaveCount(1);
    }
}
```

- [ ] **Step 2: Run tests to verify they pass**

Run: `dotnet test src/DzhusShelter.Api.IntegrationTests/DzhusShelter.Api.IntegrationTests.csproj`
Expected: PASS. (Task 3's controller change already landed, so both the updated round-trip test and the new dedup test go green immediately — there's no separate red step here, this task is about locking in end-to-end coverage for behavior Task 3 already implemented.)

- [ ] **Step 3: Commit**

```bash
git add src/DzhusShelter.Api.IntegrationTests/BadHabitsEndpointsTests.cs
git commit -m "test(api): cover the 200 OK response shape and same-day dedup end to end"
```

---

### Task 5: Telegram Bot — subtype enum, mapping, and Ukrainian display names

**Files:**
- Modify: `src/DzhusShelter.TelegramBot/BadHabitsKeyboard.cs`
- Test: `src/DzhusShelter.TelegramBot.Tests/BadHabitsKeyboardTests.cs`

**Interfaces:**
- Produces: `HabitSubType` enum in the `DzhusShelter.TelegramBot` namespace matching Task 1's values exactly (same names, values don't need to match the Api project's since they're never compared cross-process — only the string names round-trip over HTTP). `BadHabitsKeyboard.SubTypesFor(HabitType)` returns the updated per-type lists. New `BadHabitsKeyboard.DisplayName(HabitSubType)` returns the Ukrainian label — Task 7 (`BotHostedService`) depends on this method.

- [ ] **Step 1: Write the failing tests**

In `src/DzhusShelter.TelegramBot.Tests/BadHabitsKeyboardTests.cs`, replace the `SubTypesFor_Alcohol_ReturnsBeerWineSpirits` test and add new ones (keep the other existing tests as-is):

```csharp
[Fact]
public void SubTypesFor_Alcohol_ReturnsAllAlcoholSubTypes()
{
    var subTypes = BadHabitsKeyboard.SubTypesFor(HabitType.Alcohol);

    subTypes.Should().BeEquivalentTo([
        HabitSubType.Beer, HabitSubType.Wine, HabitSubType.Vodka,
        HabitSubType.Whiskey, HabitSubType.Rum, HabitSubType.Gin, HabitSubType.Martini,
    ]);
}

[Fact]
public void SubTypesFor_Smoking_ReturnsAllSmokingSubTypes()
{
    var subTypes = BadHabitsKeyboard.SubTypesFor(HabitType.Smoking);

    subTypes.Should().BeEquivalentTo([
        HabitSubType.Cigarette, HabitSubType.Vape, HabitSubType.Iqos, HabitSubType.Hookah,
    ]);
}

[Theory]
[InlineData(HabitSubType.Cigarette, "Цигарки")]
[InlineData(HabitSubType.Vape, "Вейп")]
[InlineData(HabitSubType.Iqos, "Айкос")]
[InlineData(HabitSubType.Hookah, "Кальян")]
[InlineData(HabitSubType.Beer, "Пиво")]
[InlineData(HabitSubType.Wine, "Вино")]
[InlineData(HabitSubType.Vodka, "Горілка")]
[InlineData(HabitSubType.Whiskey, "Віскі")]
[InlineData(HabitSubType.Rum, "Ром")]
[InlineData(HabitSubType.Gin, "Джин")]
[InlineData(HabitSubType.Martini, "Мартіні")]
public void DisplayName_ReturnsUkrainianLabel(HabitSubType subType, string expectedLabel)
{
    BadHabitsKeyboard.DisplayName(subType).Should().Be(expectedLabel);
}
```

Remove the old `SubTypesFor_Alcohol_ReturnsBeerWineSpirits` fact (superseded by `SubTypesFor_Alcohol_ReturnsAllAlcoholSubTypes`).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/DzhusShelter.TelegramBot.Tests/DzhusShelter.TelegramBot.Tests.csproj`
Expected: build error — `HabitSubType.Iqos` etc. don't exist in this project yet, and `BadHabitsKeyboard.DisplayName` doesn't exist.

- [ ] **Step 3: Update the enum and mapping, add DisplayName**

Replace `src/DzhusShelter.TelegramBot/BadHabitsKeyboard.cs` in full:

```csharp
namespace DzhusShelter.TelegramBot;

public enum HabitType
{
    Smoking = 1,
    Alcohol = 2,
}

public enum HabitSubType
{
    Cigarette = 1,
    Vape = 2,
    Beer = 3,
    Wine = 4,
    Iqos = 6,
    Hookah = 7,
    Vodka = 8,
    Whiskey = 9,
    Rum = 10,
    Gin = 11,
    Martini = 12,
}

public static class BadHabitsKeyboard
{
    private const string HabitTypePrefix = "habit-type:";
    private const string SubTypePrefix = "sub-type:";

    /// <summary>
    /// Callback data for the single root-menu button ("🚫 Шкідливі звички") shown on /start.
    /// Tapping it reveals the habit-type buttons — a fixed value, not parameterized, since
    /// there's only one root menu today.
    /// </summary>
    public const string OpenMenuCallbackData = "menu:bad-habits";

    private static readonly Dictionary<HabitType, HabitSubType[]> SubTypesByHabitType = new()
    {
        [HabitType.Smoking] = [HabitSubType.Cigarette, HabitSubType.Vape, HabitSubType.Iqos, HabitSubType.Hookah],
        [HabitType.Alcohol] = [HabitSubType.Beer, HabitSubType.Wine, HabitSubType.Vodka, HabitSubType.Whiskey, HabitSubType.Rum, HabitSubType.Gin, HabitSubType.Martini],
    };

    private static readonly Dictionary<HabitSubType, string> DisplayNames = new()
    {
        [HabitSubType.Cigarette] = "Цигарки",
        [HabitSubType.Vape] = "Вейп",
        [HabitSubType.Iqos] = "Айкос",
        [HabitSubType.Hookah] = "Кальян",
        [HabitSubType.Beer] = "Пиво",
        [HabitSubType.Wine] = "Вино",
        [HabitSubType.Vodka] = "Горілка",
        [HabitSubType.Whiskey] = "Віскі",
        [HabitSubType.Rum] = "Ром",
        [HabitSubType.Gin] = "Джин",
        [HabitSubType.Martini] = "Мартіні",
    };

    public static IReadOnlyList<HabitSubType> SubTypesFor(HabitType habitType) => SubTypesByHabitType[habitType];

    public static string DisplayName(HabitSubType subType) => DisplayNames[subType];

    public static string HabitTypeCallbackData(HabitType habitType) => $"{HabitTypePrefix}{(int)habitType}";

    public static string SubTypeCallbackData(HabitType habitType, HabitSubType subType) =>
        $"{SubTypePrefix}{(int)habitType}:{(int)subType}";

    public static HabitType? TryParseHabitType(string callbackData)
    {
        if (!callbackData.StartsWith(HabitTypePrefix, StringComparison.Ordinal))
            return null;

        var value = callbackData[HabitTypePrefix.Length..];
        return int.TryParse(value, out var raw) && Enum.IsDefined(typeof(HabitType), raw)
            ? (HabitType)raw
            : null;
    }

    public static (HabitType HabitType, HabitSubType SubType)? TryParseSubType(string callbackData)
    {
        if (!callbackData.StartsWith(SubTypePrefix, StringComparison.Ordinal))
            return null;

        var parts = callbackData[SubTypePrefix.Length..].Split(':');
        if (parts.Length != 2)
            return null;

        if (!int.TryParse(parts[0], out var habitTypeRaw) || !Enum.IsDefined(typeof(HabitType), habitTypeRaw))
            return null;

        if (!int.TryParse(parts[1], out var subTypeRaw) || !Enum.IsDefined(typeof(HabitSubType), subTypeRaw))
            return null;

        return ((HabitType)habitTypeRaw, (HabitSubType)subTypeRaw);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/DzhusShelter.TelegramBot.Tests/DzhusShelter.TelegramBot.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/DzhusShelter.TelegramBot/BadHabitsKeyboard.cs src/DzhusShelter.TelegramBot.Tests/BadHabitsKeyboardTests.cs
git commit -m "feat(bot): add new subtypes and Ukrainian display names"
```

---

### Task 6: Telegram Bot — API client reads the alreadyLogged flag

**Files:**
- Modify: `src/DzhusShelter.TelegramBot/BadHabitsApiClient.cs`

**Interfaces:**
- Consumes: `POST /api/bad-habits/entries` → `200 OK { id, alreadyLogged }` (Task 3/4).
- Produces: `LogEntryOutcome` enum (`Logged`, `AlreadyLogged`, `Failed`); `BadHabitsApiClient.LogEntryAsync(HabitType, HabitSubType, CancellationToken)` now returns `Task<LogEntryOutcome>` (was `Task<bool>`) — Task 7 (`BotHostedService`) depends on this.

- [ ] **Step 1: Update the API client**

Replace `src/DzhusShelter.TelegramBot/BadHabitsApiClient.cs` in full:

```csharp
using System.Net.Http.Json;
using System.Text.Json;

namespace DzhusShelter.TelegramBot;

public sealed record LogHabitEntryRequest(HabitType HabitType, HabitSubType SubType, DateTimeOffset OccurredAt, string? Notes);

public enum LogEntryOutcome
{
    Logged,
    AlreadyLogged,
    Failed,
}

public sealed class BadHabitsApiClient
{
    // See DzhusShelter.UI's BadHabitsApiClient for the same lesson: the API returns camelCase
    // property names, and HttpClient's default JSON options are case-sensitive — without this,
    // deserializing the response silently produces default field values instead of throwing.
    private static readonly JsonSerializerOptions ResponseJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed record LogHabitEntryResponse(Guid Id, bool AlreadyLogged);

    private readonly HttpClient _httpClient;

    public BadHabitsApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<LogEntryOutcome> LogEntryAsync(HabitType habitType, HabitSubType subType, CancellationToken cancellationToken)
    {
        var request = new LogHabitEntryRequest(habitType, subType, DateTimeOffset.UtcNow, null);
        var response = await _httpClient.PostAsJsonAsync("/api/bad-habits/entries", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return LogEntryOutcome.Failed;

        var body = await response.Content.ReadFromJsonAsync<LogHabitEntryResponse>(ResponseJsonOptions, cancellationToken);
        return body is { AlreadyLogged: true } ? LogEntryOutcome.AlreadyLogged : LogEntryOutcome.Logged;
    }
}
```

There's no existing unit test for `BadHabitsApiClient` (it makes a real `HttpClient` call and the project has no fake-`HttpMessageHandler` test infrastructure yet) — this task is verified manually in Task 8's smoke test instead of a new automated test, consistent with how this class was left untested before this change.

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/DzhusShelter.TelegramBot/DzhusShelter.TelegramBot.csproj`
Expected: build error at this point — `BotHostedService.cs` still expects `LogEntryAsync` to return `bool`. That's fixed in Task 7; if you want a green build right now, do Task 7's Step 1 before running this.

- [ ] **Step 3: Commit**

```bash
git add src/DzhusShelter.TelegramBot/BadHabitsApiClient.cs
git commit -m "feat(bot): surface alreadyLogged as a LogEntryOutcome from the API client"
```

---

### Task 7: Telegram Bot — wire display names and dedup messaging into BotHostedService

**Files:**
- Modify: `src/DzhusShelter.TelegramBot/BotHostedService.cs:88-113`

**Interfaces:**
- Consumes: `BadHabitsKeyboard.DisplayName(HabitSubType)` (Task 5), `BadHabitsApiClient.LogEntryAsync(...)` returning `Task<LogEntryOutcome>` (Task 6).

- [ ] **Step 1: Update the callback handler**

In `src/DzhusShelter.TelegramBot/BotHostedService.cs`, replace the body of `HandleCallbackAsync` from the `habitType is not null` branch onward:

```csharp
var habitType = BadHabitsKeyboard.TryParseHabitType(data);
if (habitType is not null)
{
    var buttons = BadHabitsKeyboard.SubTypesFor(habitType.Value)
        .Select(subType => InlineKeyboardButton.WithCallbackData(
            BadHabitsKeyboard.DisplayName(subType), BadHabitsKeyboard.SubTypeCallbackData(habitType.Value, subType)))
        .ToArray();
    var keyboard = new InlineKeyboardMarkup(buttons);

    await _botClient.AnswerCallbackQuery(callbackId, cancellationToken: cancellationToken);
    await _botClient.SendMessage(chatId, "Який саме?", replyMarkup: keyboard, cancellationToken: cancellationToken);
    return;
}

var subTypeSelection = BadHabitsKeyboard.TryParseSubType(data);
if (subTypeSelection is not null)
{
    var (parsedHabitType, subType) = subTypeSelection.Value;
    var outcome = await _apiClient.LogEntryAsync(parsedHabitType, subType, cancellationToken);
    var label = BadHabitsKeyboard.DisplayName(subType);

    var message = outcome switch
    {
        LogEntryOutcome.Logged => $"Записано: {label}",
        LogEntryOutcome.AlreadyLogged => $"Вже зафіксовано на сьогодні: {label}",
        _ => "Не вдалося записати — спробуй ще раз.",
    };

    await _botClient.AnswerCallbackQuery(callbackId, cancellationToken: cancellationToken);
    await _botClient.SendMessage(chatId, message, cancellationToken: cancellationToken);
}
```

(The `habitType`-branch keyboard construction line changes only in its button label — from `subType.ToString()` to `BadHabitsKeyboard.DisplayName(subType)`.)

- [ ] **Step 2: Run the full Telegram bot test project and build the whole solution**

Run: `dotnet test src/DzhusShelter.TelegramBot.Tests/DzhusShelter.TelegramBot.Tests.csproj && dotnet build DzhusShelter.slnx --configuration Release`
Expected: tests PASS, solution builds with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/DzhusShelter.TelegramBot/BotHostedService.cs
git commit -m "feat(bot): show Ukrainian labels and dedup feedback in chat"
```

---

### Task 8: UI — keep the enum copy in sync

**Files:**
- Modify: `src/DzhusShelter.UI/Services/BadHabitsApiClient.cs:6-19`

**Interfaces:**
- Produces: `HabitSubType` enum in the `DzhusShelter.UI.Services` namespace matching Task 1's member names exactly, so `GetEntriesAsync` correctly deserializes entries whose `SubType` is one of the new values instead of throwing a `JsonException`.

- [ ] **Step 1: Update the enum**

In `src/DzhusShelter.UI/Services/BadHabitsApiClient.cs`, replace the `HabitSubType` enum:

```csharp
public enum HabitSubType
{
    Cigarette = 1,
    Vape = 2,
    Beer = 3,
    Wine = 4,
    Iqos = 6,
    Hookah = 7,
    Vodka = 8,
    Whiskey = 9,
    Rum = 10,
    Gin = 11,
    Martini = 12,
}
```

`BadHabits.razor`'s filter dropdown only offers `HabitType`-level filtering today (no subtype dropdown), so no other UI file needs to change here — the new subtypes just need to deserialize without throwing.

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/DzhusShelter.UI/DzhusShelter.UI.csproj`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/DzhusShelter.UI/Services/BadHabitsApiClient.cs
git commit -m "chore(ui): sync HabitSubType with the new alcohol/smoking subtypes"
```

---

### Task 9: Final verification and manual bot smoke test

**Files:** none (verification only)

- [ ] **Step 1: Full solution build and test run**

Run:
```bash
dotnet build DzhusShelter.slnx --configuration Release
dotnet test DzhusShelter.slnx --configuration Release
```
Expected: 0 build errors/warnings, all tests PASS (unit + Testcontainers-based integration tests).

- [ ] **Step 2: Manual smoke test against a local stack**

Start Postgres (`docker compose up -d postgres`, with a local `.env` containing `POSTGRES_PASSWORD`), then run the Api (`dotnet run --project src/DzhusShelter.Api/DzhusShelter.Api.csproj`) and the Telegram bot (`dotnet run --project src/DzhusShelter.TelegramBot/DzhusShelter.TelegramBot.csproj`) with a real or test Telegram bot token in `.env`. In the chat:
1. `/start` → 🚫 Шкідливі звички → 🍺 Алкоголь — confirm the subtype keyboard shows all 7 Ukrainian labels (Пиво, Вино, Горілка, Віскі, Ром, Джин, Мартіні), not raw enum names.
2. Tap "Пиво" — confirm the bot replies `"Записано: Пиво"`.
3. Tap "Пиво" again — confirm the bot replies `"Вже зафіксовано на сьогодні: Пиво"` and no second row was created (check via `GET /api/bad-habits/entries?...` or a DB client per [docs/casaos-runbook.md](../../casaos-runbook.md)'s DBeaver section).
4. Repeat steps 1-3 for 🚬 Куріння, confirming Цигарки/Вейп/Айкос/Кальян all appear.

- [ ] **Step 3: Tear down local test resources**

```bash
docker compose down postgres
rm -f .env
```

- [ ] **Step 4: Push to dev**

```bash
git push
```

(No PR to `main` yet — per the repo's dev/main workflow, this stays on `dev` until the user decides to ship.)
