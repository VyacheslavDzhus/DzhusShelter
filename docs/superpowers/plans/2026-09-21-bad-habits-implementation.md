# Bad Habits (Event Log + CQRS + API/Bot/UI split) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up `src/DzhusShelter.Api` (Clean Architecture + CQRS, PostgreSQL), `src/DzhusShelter.TelegramBot`, and rewire `src/DzhusShelter.UI`, so that logging a smoking/alcohol event via a Telegram bot button and viewing it as a filterable chart on the Blazor dashboard both work end-to-end through the new API.

**Architecture:** Three deployable .NET 10 projects — `src/DzhusShelter.Api` (Domain/Application/Infrastructure/Controllers layers, CQRS with hand-rolled `ICommandHandler`/`IQueryHandler`, no mediator library), `src/DzhusShelter.TelegramBot` (a `BackgroundService` calling the API), `src/DzhusShelter.UI` (Blazor Server calling the API). All three plus PostgreSQL run as separate `docker-compose` services on one private network; the API's port is never published to the host.

**Tech Stack:** .NET 10 (ASP.NET Core Web API + Blazor Server), EF Core + Npgsql (PostgreSQL), FluentValidation, xUnit + FluentAssertions + NSubstitute, Testcontainers.PostgreSql, Telegram.Bot, Blazor-ApexCharts, Docker Compose.

**Spec:** [docs/specs/bad-habits.md](../../specs/bad-habits.md) and [docs/architecture.md](../../architecture.md) — read both before starting; this plan implements them.

## Global Constraints

- Target framework: `net10.0`, `Nullable` and `ImplicitUsings` both `enable` on every new project (matches `src/DzhusShelter.UI/DzhusShelter.UI.csproj`).
- No authentication between bot/UI/API yet — acceptable only as long as `src/DzhusShelter.Api`'s container port is **not** published in `docker-compose.yml` (internal network only). Never skip this constraint to "make testing easier."
- Business-rule failures use the `Result`/`Result<T>` pattern, not exceptions. Domain invariants are enforced in constructors/factories, never left to callers to check.
- CQRS from the start for this feature: commands and queries are separate types with separate handlers behind `ICommandHandler<TCommand, TResponse>` / `IQueryHandler<TQuery, TResponse>` — no mediator library, handlers are resolved directly via DI.
- Don't use EF Core's InMemory provider anywhere — it doesn't enforce real relational behavior and hides bugs. Real database verification happens only via `Testcontainers` against real PostgreSQL.
- `HabitEntry` is append-only in this feature (no edit/delete) — don't add update/delete methods "for completeness."

---

### Task 1: Scaffold `src/DzhusShelter.Api` and wire it into the solution

**Files:**
- Create: `src/DzhusShelter.Api/DzhusShelter.Api.csproj`
- Create: `src/DzhusShelter.Api/Program.cs`
- Modify: `DzhusShelter.slnx`

**Interfaces:**
- Produces: a running, empty ASP.NET Core Web API (`public partial class Program` for later `WebApplicationFactory<Program>` use in tests), controllers + JSON enum-as-string convention wired up, ready for feature code.

- [ ] **Step 1: Create the project file and add the Swagger package**

Create `src/DzhusShelter.Api/DzhusShelter.Api.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

</Project>
```

Run: `dotnet add src/DzhusShelter.Api package Swashbuckle.AspNetCore`

- [ ] **Step 2: Create `Program.cs`**

Create `src/DzhusShelter.Api/Program.cs`:

```csharp
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();

public partial class Program;
```

- [ ] **Step 3: Add the project to the solution**

Modify `DzhusShelter.slnx` — replace the file with:

```xml
<Solution>
  <Project Path="src/DzhusShelter.UI/DzhusShelter.UI.csproj" />
  <Project Path="src/DzhusShelter.Api/DzhusShelter.Api.csproj" />
</Solution>
```

- [ ] **Step 4: Build to verify**

Run: `dotnet build src/DzhusShelter.Api/DzhusShelter.Api.csproj`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 5: Commit**

```bash
git add src/DzhusShelter.Api DzhusShelter.slnx
git commit -m "feat(api): scaffold src/DzhusShelter.Api project"
```

---

### Task 2: Domain — `Result` pattern + `HabitEntry` with invariants

**Files:**
- Create: `src/DzhusShelter.Api/Domain/Common/Error.cs`
- Create: `src/DzhusShelter.Api/Domain/Common/Result.cs`
- Create: `src/DzhusShelter.Api/Domain/BadHabits/HabitType.cs`
- Create: `src/DzhusShelter.Api/Domain/BadHabits/HabitSubType.cs`
- Create: `src/DzhusShelter.Api/Domain/BadHabits/HabitEntryErrors.cs`
- Create: `src/DzhusShelter.Api/Domain/BadHabits/HabitEntry.cs`
- Create: `src/DzhusShelter.Api.Tests/DzhusShelter.Api.Tests.csproj`
- Create: `src/DzhusShelter.Api.Tests/Domain/HabitEntryTests.cs`
- Modify: `DzhusShelter.slnx`

**Interfaces:**
- Produces:
  - `Error(string Code, string Message)` with `Error.None`
  - `Result` with `IsSuccess`/`IsFailure`/`Error`, static `Result.Success()`, `Result.Failure(Error)`, `Result.Success<T>(T)`, `Result.Failure<T>(Error)`
  - `Result<TValue> : Result` with `Value` (throws if accessed on failure)
  - `enum HabitType { Smoking = 1, Alcohol = 2 }`
  - `enum HabitSubType { Cigarette = 1, Vape = 2, Beer = 3, Wine = 4, Spirits = 5 }`
  - `HabitEntry.Create(HabitType, HabitSubType, DateTimeOffset occurredAt, string? notes, TimeProvider) : Result<HabitEntry>`
  - `HabitEntry` readonly properties: `Id (Guid)`, `HabitType`, `SubType`, `OccurredAt (DateTimeOffset)`, `Notes (string?)`

- [ ] **Step 1: Scaffold the test project**

Create `src/DzhusShelter.Api.Tests/DzhusShelter.Api.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\DzhusShelter.Api\DzhusShelter.Api.csproj" />
  </ItemGroup>

</Project>
```

Run:
```bash
dotnet add src/DzhusShelter.Api.Tests package Microsoft.NET.Test.Sdk
dotnet add src/DzhusShelter.Api.Tests package xunit
dotnet add src/DzhusShelter.Api.Tests package xunit.runner.visualstudio
dotnet add src/DzhusShelter.Api.Tests package FluentAssertions
dotnet add src/DzhusShelter.Api.Tests package NSubstitute
dotnet add src/DzhusShelter.Api.Tests package Microsoft.Extensions.TimeProvider.Testing
```

Add the test project to `DzhusShelter.slnx`:

```xml
<Solution>
  <Project Path="src/DzhusShelter.UI/DzhusShelter.UI.csproj" />
  <Project Path="src/DzhusShelter.Api/DzhusShelter.Api.csproj" />
  <Project Path="src/DzhusShelter.Api.Tests/DzhusShelter.Api.Tests.csproj" />
</Solution>
```

Create `src/DzhusShelter.Api.Tests/GlobalUsings.cs` (`ImplicitUsings` only covers BCL namespaces, not xunit — without this every test file needs its own `using Xunit;`):

```csharp
global using Xunit;
```

- [ ] **Step 2: Write the failing domain tests**

Create `src/DzhusShelter.Api.Tests/Domain/HabitEntryTests.cs`:

```csharp
using DzhusShelter.Api.Domain.BadHabits;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;

namespace DzhusShelter.Api.Tests.Domain;

public class HabitEntryTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private static FakeTimeProvider CreateTimeProvider() => new(Now);

    [Fact]
    public void Create_WithValidCigaretteEntry_Succeeds()
    {
        var result = HabitEntry.Create(HabitType.Smoking, HabitSubType.Cigarette, Now.AddMinutes(-5), "after lunch", CreateTimeProvider());

        result.IsSuccess.Should().BeTrue();
        result.Value.HabitType.Should().Be(HabitType.Smoking);
        result.Value.SubType.Should().Be(HabitSubType.Cigarette);
        result.Value.Notes.Should().Be("after lunch");
    }

    [Fact]
    public void Create_WithOccurredAtInTheFuture_Fails()
    {
        var result = HabitEntry.Create(HabitType.Alcohol, HabitSubType.Beer, Now.AddMinutes(5), null, CreateTimeProvider());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(HabitEntryErrors.OccurredAtInFuture);
    }

    [Fact]
    public void Create_WithSubTypeThatDoesNotBelongToHabitType_Fails()
    {
        var result = HabitEntry.Create(HabitType.Smoking, HabitSubType.Wine, Now, null, CreateTimeProvider());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HabitEntry.SubTypeMismatch");
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail to compile**

Run: `dotnet test src/DzhusShelter.Api.Tests`
Expected: build errors — `HabitEntry`, `HabitType`, `HabitSubType`, `HabitEntryErrors` don't exist yet.

- [ ] **Step 4: Implement `Error` and `Result`**

Create `src/DzhusShelter.Api/Domain/Common/Error.cs`:

```csharp
namespace DzhusShelter.Api.Domain.Common;

public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
}
```

Create `src/DzhusShelter.Api/Domain/Common/Result.cs`:

```csharp
namespace DzhusShelter.Api.Domain.Common;

public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
            throw new InvalidOperationException("A successful result cannot carry an error.");
        if (!isSuccess && error == Error.None)
            throw new InvalidOperationException("A failed result must carry an error.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);
    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
}

public class Result<TValue> : Result
{
    private readonly TValue? _value;

    protected internal Result(TValue? value, bool isSuccess, Error error) : base(isSuccess, error)
    {
        _value = value;
    }

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access the value of a failed result.");
}
```

- [ ] **Step 5: Implement the enums and error catalog**

Create `src/DzhusShelter.Api/Domain/BadHabits/HabitType.cs`:

```csharp
namespace DzhusShelter.Api.Domain.BadHabits;

public enum HabitType
{
    Smoking = 1,
    Alcohol = 2,
}
```

Create `src/DzhusShelter.Api/Domain/BadHabits/HabitSubType.cs`:

```csharp
namespace DzhusShelter.Api.Domain.BadHabits;

public enum HabitSubType
{
    Cigarette = 1,
    Vape = 2,
    Beer = 3,
    Wine = 4,
    Spirits = 5,
}
```

Create `src/DzhusShelter.Api/Domain/BadHabits/HabitEntryErrors.cs`:

```csharp
using DzhusShelter.Api.Domain.Common;

namespace DzhusShelter.Api.Domain.BadHabits;

public static class HabitEntryErrors
{
    public static readonly Error OccurredAtInFuture =
        new("HabitEntry.OccurredAtInFuture", "The occurrence time cannot be in the future.");

    public static Error SubTypeMismatch(HabitType habitType, HabitSubType subType) =>
        new("HabitEntry.SubTypeMismatch", $"'{subType}' is not a valid sub-type for habit type '{habitType}'.");
}
```

- [ ] **Step 6: Implement `HabitEntry`**

Create `src/DzhusShelter.Api/Domain/BadHabits/HabitEntry.cs`:

```csharp
using DzhusShelter.Api.Domain.Common;

namespace DzhusShelter.Api.Domain.BadHabits;

public sealed class HabitEntry
{
    private static readonly Dictionary<HabitType, HabitSubType[]> ValidSubTypes = new()
    {
        [HabitType.Smoking] = [HabitSubType.Cigarette, HabitSubType.Vape],
        [HabitType.Alcohol] = [HabitSubType.Beer, HabitSubType.Wine, HabitSubType.Spirits],
    };

    private HabitEntry()
    {
        // EF Core materialization
    }

    private HabitEntry(Guid id, HabitType habitType, HabitSubType subType, DateTimeOffset occurredAt, string? notes)
    {
        Id = id;
        HabitType = habitType;
        SubType = subType;
        OccurredAt = occurredAt;
        Notes = notes;
    }

    public Guid Id { get; private set; }
    public HabitType HabitType { get; private set; }
    public HabitSubType SubType { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string? Notes { get; private set; }

    public static Result<HabitEntry> Create(
        HabitType habitType,
        HabitSubType subType,
        DateTimeOffset occurredAt,
        string? notes,
        TimeProvider timeProvider)
    {
        if (occurredAt > timeProvider.GetUtcNow())
            return Result.Failure<HabitEntry>(HabitEntryErrors.OccurredAtInFuture);

        if (!ValidSubTypes.TryGetValue(habitType, out var allowedSubTypes) || !allowedSubTypes.Contains(subType))
            return Result.Failure<HabitEntry>(HabitEntryErrors.SubTypeMismatch(habitType, subType));

        return Result.Success(new HabitEntry(Guid.NewGuid(), habitType, subType, occurredAt, notes));
    }
}
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test src/DzhusShelter.Api.Tests`
Expected: `Passed! - Failed: 0, Passed: 3`

- [ ] **Step 8: Commit**

```bash
git add src/DzhusShelter.Api src/DzhusShelter.Api.Tests DzhusShelter.slnx
git commit -m "feat(api): add Result pattern and HabitEntry domain model"
```

---

### Task 3: Application — `LogHabitEntryCommand` (CQRS write side)

**Files:**
- Create: `src/DzhusShelter.Api/Application/Abstractions/ICommandHandler.cs`
- Create: `src/DzhusShelter.Api/Application/Abstractions/IQueryHandler.cs`
- Create: `src/DzhusShelter.Api/Application/BadHabits/IHabitEntryRepository.cs`
- Create: `src/DzhusShelter.Api/Application/BadHabits/LogHabitEntryCommand.cs`
- Create: `src/DzhusShelter.Api/Application/BadHabits/LogHabitEntryCommandValidator.cs`
- Create: `src/DzhusShelter.Api/Application/BadHabits/LogHabitEntryCommandHandler.cs`
- Create: `src/DzhusShelter.Api.Tests/Application/LogHabitEntryCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `HabitEntry.Create(...)`, `Result`/`Result<T>` from Task 2
- Produces:
  - `ICommandHandler<in TCommand, TResponse> { Task<TResponse> Handle(TCommand command, CancellationToken ct); }`
  - `IQueryHandler<in TQuery, TResponse> { Task<TResponse> Handle(TQuery query, CancellationToken ct); }`
  - `IHabitEntryRepository { Task AddAsync(HabitEntry entry, CancellationToken ct); Task<IReadOnlyList<HabitEntry>> GetAsync(DateTimeOffset from, DateTimeOffset to, HabitType? habitType, HabitSubType? subType, CancellationToken ct); }`
  - `record LogHabitEntryCommand(HabitType HabitType, HabitSubType SubType, DateTimeOffset OccurredAt, string? Notes)`
  - `LogHabitEntryCommandHandler : ICommandHandler<LogHabitEntryCommand, Result<Guid>>`

- [ ] **Step 1: Add the FluentValidation package**

Run: `dotnet add src/DzhusShelter.Api package FluentValidation`

- [ ] **Step 2: Write the failing handler test**

Create `src/DzhusShelter.Api.Tests/Application/LogHabitEntryCommandHandlerTests.cs`:

```csharp
using DzhusShelter.Api.Application.BadHabits;
using DzhusShelter.Api.Domain.BadHabits;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace DzhusShelter.Api.Tests.Application;

public class LogHabitEntryCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WithValidCommand_PersistsEntryAndReturnsItsId()
    {
        var repository = Substitute.For<IHabitEntryRepository>();
        var handler = new LogHabitEntryCommandHandler(repository, new LogHabitEntryCommandValidator(), new FakeTimeProvider(Now));
        var command = new LogHabitEntryCommand(HabitType.Alcohol, HabitSubType.Beer, Now.AddMinutes(-1), "with friends");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
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
}
```

- [ ] **Step 3: Run the tests to verify they fail to compile**

Run: `dotnet test src/DzhusShelter.Api.Tests`
Expected: build errors — the application types don't exist yet.

- [ ] **Step 4: Implement the CQRS abstractions**

Create `src/DzhusShelter.Api/Application/Abstractions/ICommandHandler.cs`:

```csharp
namespace DzhusShelter.Api.Application.Abstractions;

public interface ICommandHandler<in TCommand, TResponse>
{
    Task<TResponse> Handle(TCommand command, CancellationToken cancellationToken);
}
```

Create `src/DzhusShelter.Api/Application/Abstractions/IQueryHandler.cs`:

```csharp
namespace DzhusShelter.Api.Application.Abstractions;

public interface IQueryHandler<in TQuery, TResponse>
{
    Task<TResponse> Handle(TQuery query, CancellationToken cancellationToken);
}
```

- [ ] **Step 5: Implement the repository abstraction**

Create `src/DzhusShelter.Api/Application/BadHabits/IHabitEntryRepository.cs`:

```csharp
using DzhusShelter.Api.Domain.BadHabits;

namespace DzhusShelter.Api.Application.BadHabits;

public interface IHabitEntryRepository
{
    Task AddAsync(HabitEntry entry, CancellationToken cancellationToken);

    Task<IReadOnlyList<HabitEntry>> GetAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        HabitType? habitType,
        HabitSubType? subType,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 6: Implement the command, validator, and handler**

Create `src/DzhusShelter.Api/Application/BadHabits/LogHabitEntryCommand.cs`:

```csharp
using DzhusShelter.Api.Domain.BadHabits;

namespace DzhusShelter.Api.Application.BadHabits;

public sealed record LogHabitEntryCommand(HabitType HabitType, HabitSubType SubType, DateTimeOffset OccurredAt, string? Notes);
```

Create `src/DzhusShelter.Api/Application/BadHabits/LogHabitEntryCommandValidator.cs`:

```csharp
using FluentValidation;

namespace DzhusShelter.Api.Application.BadHabits;

public sealed class LogHabitEntryCommandValidator : AbstractValidator<LogHabitEntryCommand>
{
    public LogHabitEntryCommandValidator()
    {
        RuleFor(x => x.HabitType).IsInEnum();
        RuleFor(x => x.SubType).IsInEnum();
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
```

Create `src/DzhusShelter.Api/Application/BadHabits/LogHabitEntryCommandHandler.cs`:

```csharp
using DzhusShelter.Api.Application.Abstractions;
using DzhusShelter.Api.Domain.BadHabits;
using DzhusShelter.Api.Domain.Common;
using FluentValidation;

namespace DzhusShelter.Api.Application.BadHabits;

public sealed class LogHabitEntryCommandHandler : ICommandHandler<LogHabitEntryCommand, Result<Guid>>
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

    public async Task<Result<Guid>> Handle(LogHabitEntryCommand command, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Failure<Guid>(new Error("BadHabits.Validation", validationResult.Errors[0].ErrorMessage));

        var entryResult = HabitEntry.Create(command.HabitType, command.SubType, command.OccurredAt, command.Notes, _timeProvider);
        if (entryResult.IsFailure)
            return Result.Failure<Guid>(entryResult.Error);

        await _repository.AddAsync(entryResult.Value, cancellationToken);
        return Result.Success(entryResult.Value.Id);
    }
}
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test src/DzhusShelter.Api.Tests`
Expected: `Passed! - Failed: 0, Passed: 5`

- [ ] **Step 8: Commit**

```bash
git add src/DzhusShelter.Api src/DzhusShelter.Api.Tests
git commit -m "feat(api): add LogHabitEntryCommand with CQRS write handler"
```

---

### Task 4: Application — `GetHabitEntriesQuery` (CQRS read side)

**Files:**
- Create: `src/DzhusShelter.Api/Application/BadHabits/HabitEntryDto.cs`
- Create: `src/DzhusShelter.Api/Application/BadHabits/GetHabitEntriesQuery.cs`
- Create: `src/DzhusShelter.Api/Application/BadHabits/GetHabitEntriesQueryValidator.cs`
- Create: `src/DzhusShelter.Api/Application/BadHabits/GetHabitEntriesQueryHandler.cs`
- Create: `src/DzhusShelter.Api.Tests/Application/GetHabitEntriesQueryHandlerTests.cs`

**Interfaces:**
- Consumes: `IHabitEntryRepository`, `HabitEntry`, `Result<T>` from Tasks 2–3
- Produces:
  - `record HabitEntryDto(Guid Id, HabitType HabitType, HabitSubType SubType, DateTimeOffset OccurredAt, string? Notes)`
  - `record GetHabitEntriesQuery(DateTimeOffset From, DateTimeOffset To, HabitType? HabitType, HabitSubType? SubType)`
  - `GetHabitEntriesQueryHandler : IQueryHandler<GetHabitEntriesQuery, Result<IReadOnlyList<HabitEntryDto>>>`

- [ ] **Step 1: Write the failing query handler test**

Create `src/DzhusShelter.Api.Tests/Application/GetHabitEntriesQueryHandlerTests.cs`:

```csharp
using DzhusShelter.Api.Application.BadHabits;
using DzhusShelter.Api.Domain.BadHabits;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace DzhusShelter.Api.Tests.Application;

public class GetHabitEntriesQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WithEntriesInRange_ReturnsThemAsDtos()
    {
        var timeProvider = new FakeTimeProvider(Now);
        var entry = HabitEntry.Create(HabitType.Smoking, HabitSubType.Cigarette, Now.AddHours(-1), null, timeProvider).Value;
        var repository = Substitute.For<IHabitEntryRepository>();
        repository
            .GetAsync(Now.AddDays(-1), Now, HabitType.Smoking, null, Arg.Any<CancellationToken>())
            .Returns(new List<HabitEntry> { entry });
        var handler = new GetHabitEntriesQueryHandler(repository, new GetHabitEntriesQueryValidator());
        var query = new GetHabitEntriesQuery(Now.AddDays(-1), Now, HabitType.Smoking, null);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(dto => dto.Id == entry.Id && dto.SubType == HabitSubType.Cigarette);
    }

    [Fact]
    public async Task Handle_WithFromAfterTo_ReturnsFailure()
    {
        var repository = Substitute.For<IHabitEntryRepository>();
        var handler = new GetHabitEntriesQueryHandler(repository, new GetHabitEntriesQueryValidator());
        var query = new GetHabitEntriesQuery(Now, Now.AddDays(-1), null, null);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        await repository.DidNotReceive().GetAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<HabitType?>(), Arg.Any<HabitSubType?>(), Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail to compile**

Run: `dotnet test src/DzhusShelter.Api.Tests`
Expected: build errors — `HabitEntryDto`, `GetHabitEntriesQuery`, `GetHabitEntriesQueryValidator`, `GetHabitEntriesQueryHandler` don't exist yet.

- [ ] **Step 3: Implement the DTO, query, validator, and handler**

Create `src/DzhusShelter.Api/Application/BadHabits/HabitEntryDto.cs`:

```csharp
using DzhusShelter.Api.Domain.BadHabits;

namespace DzhusShelter.Api.Application.BadHabits;

public sealed record HabitEntryDto(Guid Id, HabitType HabitType, HabitSubType SubType, DateTimeOffset OccurredAt, string? Notes);
```

Create `src/DzhusShelter.Api/Application/BadHabits/GetHabitEntriesQuery.cs`:

```csharp
using DzhusShelter.Api.Domain.BadHabits;

namespace DzhusShelter.Api.Application.BadHabits;

public sealed record GetHabitEntriesQuery(DateTimeOffset From, DateTimeOffset To, HabitType? HabitType, HabitSubType? SubType);
```

Create `src/DzhusShelter.Api/Application/BadHabits/GetHabitEntriesQueryValidator.cs`:

```csharp
using FluentValidation;

namespace DzhusShelter.Api.Application.BadHabits;

public sealed class GetHabitEntriesQueryValidator : AbstractValidator<GetHabitEntriesQuery>
{
    public GetHabitEntriesQueryValidator()
    {
        RuleFor(x => x.From).LessThanOrEqualTo(x => x.To).WithMessage("'From' must be before or equal to 'To'.");
    }
}
```

Create `src/DzhusShelter.Api/Application/BadHabits/GetHabitEntriesQueryHandler.cs`:

```csharp
using DzhusShelter.Api.Application.Abstractions;
using DzhusShelter.Api.Domain.Common;
using FluentValidation;

namespace DzhusShelter.Api.Application.BadHabits;

public sealed class GetHabitEntriesQueryHandler : IQueryHandler<GetHabitEntriesQuery, Result<IReadOnlyList<HabitEntryDto>>>
{
    private readonly IHabitEntryRepository _repository;
    private readonly IValidator<GetHabitEntriesQuery> _validator;

    public GetHabitEntriesQueryHandler(IHabitEntryRepository repository, IValidator<GetHabitEntriesQuery> validator)
    {
        _repository = repository;
        _validator = validator;
    }

    public async Task<Result<IReadOnlyList<HabitEntryDto>>> Handle(GetHabitEntriesQuery query, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(query, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Failure<IReadOnlyList<HabitEntryDto>>(
                new Error("BadHabits.Validation", validationResult.Errors[0].ErrorMessage));
        }

        var entries = await _repository.GetAsync(query.From, query.To, query.HabitType, query.SubType, cancellationToken);
        IReadOnlyList<HabitEntryDto> dtos = entries
            .Select(e => new HabitEntryDto(e.Id, e.HabitType, e.SubType, e.OccurredAt, e.Notes))
            .ToList();

        return Result.Success(dtos);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test src/DzhusShelter.Api.Tests`
Expected: `Passed! - Failed: 0, Passed: 7`

- [ ] **Step 5: Commit**

```bash
git add src/DzhusShelter.Api src/DzhusShelter.Api.Tests
git commit -m "feat(api): add GetHabitEntriesQuery with CQRS read handler"
```

---

### Task 5: Infrastructure — EF Core + PostgreSQL repository

**Files:**
- Create: `src/DzhusShelter.Api/Infrastructure/AppDbContext.cs`
- Create: `src/DzhusShelter.Api/Infrastructure/BadHabits/HabitEntryConfiguration.cs`
- Create: `src/DzhusShelter.Api/Infrastructure/BadHabits/EfHabitEntryRepository.cs`
- Modify: `src/DzhusShelter.Api/Program.cs`
- Modify: `src/DzhusShelter.Api/appsettings.json` (create if absent)
- Modify: `src/DzhusShelter.Api/appsettings.Development.json` (create)

**Interfaces:**
- Consumes: `HabitEntry`, `IHabitEntryRepository` from Tasks 2–3
- Produces: `AppDbContext` (registered in DI), `EfHabitEntryRepository : IHabitEntryRepository` (registered in DI), an EF Core migration named `InitialCreate`

- [ ] **Step 1: Add EF Core / Npgsql packages**

Run:
```bash
dotnet add src/DzhusShelter.Api package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add src/DzhusShelter.Api package Microsoft.EntityFrameworkCore.Design
dotnet tool install --global dotnet-ef || dotnet tool update --global dotnet-ef
```

Then check which `Microsoft.EntityFrameworkCore` version `Npgsql.EntityFrameworkCore.PostgreSQL` actually pulls in:
```bash
dotnet list src/DzhusShelter.Api package --include-transitive
```
If `Microsoft.EntityFrameworkCore.Design` resolved to a *different* version than the `Microsoft.EntityFrameworkCore`/`.Relational` version Npgsql pulls (common with .NET 10 preview packages moving at different paces), pin Design down to match — otherwise a later project that references both (like the Task 7 integration test project) fails to build with `CS1705` (assembly version mismatch):
```bash
dotnet add src/DzhusShelter.Api package Microsoft.EntityFrameworkCore.Design --version <the version Npgsql pulls>
```

- [ ] **Step 2: Implement the DbContext and entity configuration**

Create `src/DzhusShelter.Api/Infrastructure/AppDbContext.cs`:

```csharp
using DzhusShelter.Api.Domain.BadHabits;
using Microsoft.EntityFrameworkCore;

namespace DzhusShelter.Api.Infrastructure;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<HabitEntry> HabitEntries => Set<HabitEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

Create `src/DzhusShelter.Api/Infrastructure/BadHabits/HabitEntryConfiguration.cs`:

```csharp
using DzhusShelter.Api.Domain.BadHabits;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DzhusShelter.Api.Infrastructure.BadHabits;

public sealed class HabitEntryConfiguration : IEntityTypeConfiguration<HabitEntry>
{
    public void Configure(EntityTypeBuilder<HabitEntry> builder)
    {
        builder.ToTable("HabitEntries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.HabitType).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(e => e.SubType).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(e => e.OccurredAt).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(500);
        builder.HasIndex(e => e.OccurredAt);
    }
}
```

- [ ] **Step 3: Implement the repository**

Create `src/DzhusShelter.Api/Infrastructure/BadHabits/EfHabitEntryRepository.cs`:

```csharp
using DzhusShelter.Api.Application.BadHabits;
using DzhusShelter.Api.Domain.BadHabits;
using Microsoft.EntityFrameworkCore;

namespace DzhusShelter.Api.Infrastructure.BadHabits;

public sealed class EfHabitEntryRepository : IHabitEntryRepository
{
    private readonly AppDbContext _dbContext;

    public EfHabitEntryRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(HabitEntry entry, CancellationToken cancellationToken)
    {
        await _dbContext.HabitEntries.AddAsync(entry, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HabitEntry>> GetAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        HabitType? habitType,
        HabitSubType? subType,
        CancellationToken cancellationToken)
    {
        IQueryable<HabitEntry> query = _dbContext.HabitEntries
            .Where(e => e.OccurredAt >= from && e.OccurredAt <= to);

        if (habitType is not null)
            query = query.Where(e => e.HabitType == habitType);

        if (subType is not null)
            query = query.Where(e => e.SubType == subType);

        return await query.OrderBy(e => e.OccurredAt).ToListAsync(cancellationToken);
    }
}
```

- [ ] **Step 4: Wire DI, configuration, and auto-migration into `Program.cs`**

Modify `src/DzhusShelter.Api/Program.cs` — replace its contents:

```csharp
using System.Text.Json.Serialization;
using DzhusShelter.Api.Application.Abstractions;
using DzhusShelter.Api.Application.BadHabits;
using DzhusShelter.Api.Domain.Common;
using DzhusShelter.Api.Infrastructure;
using DzhusShelter.Api.Infrastructure.BadHabits;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddScoped<IHabitEntryRepository, EfHabitEntryRepository>();
builder.Services.AddScoped<IValidator<LogHabitEntryCommand>, LogHabitEntryCommandValidator>();
builder.Services.AddScoped<IValidator<GetHabitEntriesQuery>, GetHabitEntriesQueryValidator>();
builder.Services.AddScoped<ICommandHandler<LogHabitEntryCommand, Result<Guid>>, LogHabitEntryCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetHabitEntriesQuery, Result<IReadOnlyList<HabitEntryDto>>>, GetHabitEntriesQueryHandler>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();

public partial class Program;
```

- [ ] **Step 5: Add configuration files**

Create `src/DzhusShelter.Api/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=dzhusshelter;Username=postgres;Password=postgres"
  }
}
```

Create `src/DzhusShelter.Api/appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

- [ ] **Step 6: Generate the initial migration**

Run: `dotnet ef migrations add InitialCreate --project src/DzhusShelter.Api --startup-project src/DzhusShelter.Api`
Expected: a `src/DzhusShelter.Api/Migrations/` folder is created containing `..._InitialCreate.cs` and `AppDbContextModelSnapshot.cs`, creating a `HabitEntries` table.

- [ ] **Step 7: Build to verify**

Run: `dotnet build src/DzhusShelter.Api`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

(Full round-trip against a real PostgreSQL is verified in Task 8's Testcontainers test — this task only needs to compile and produce a valid migration; don't reach for the EF Core InMemory provider to "test" it here.)

- [ ] **Step 8: Commit**

```bash
git add src/DzhusShelter.Api
git commit -m "feat(api): add EF Core + PostgreSQL infrastructure and initial migration"
```

---

### Task 6: API — `BadHabitsController`

**Files:**
- Create: `src/DzhusShelter.Api/Controllers/BadHabitsController.cs`
- Create: `src/DzhusShelter.Api.Tests/Controllers/BadHabitsControllerTests.cs`

**Interfaces:**
- Consumes: `ICommandHandler<LogHabitEntryCommand, Result<Guid>>`, `IQueryHandler<GetHabitEntriesQuery, Result<IReadOnlyList<HabitEntryDto>>>` from Tasks 3–4
- Produces: `POST /api/bad-habits/entries`, `GET /api/bad-habits/entries?from=&to=&habitType=&subType=`

- [ ] **Step 1: Write the failing controller tests**

Create `src/DzhusShelter.Api.Tests/Controllers/BadHabitsControllerTests.cs`:

```csharp
using DzhusShelter.Api.Application.Abstractions;
using DzhusShelter.Api.Application.BadHabits;
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
    public async Task LogEntry_WhenHandlerSucceeds_ReturnsCreated()
    {
        var commandHandler = Substitute.For<ICommandHandler<LogHabitEntryCommand, Result<Guid>>>();
        var entryId = Guid.NewGuid();
        commandHandler.Handle(Arg.Any<LogHabitEntryCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(entryId));
        var queryHandler = Substitute.For<IQueryHandler<GetHabitEntriesQuery, Result<IReadOnlyList<HabitEntryDto>>>>();
        var controller = new BadHabitsController(commandHandler, queryHandler);

        var response = await controller.LogEntry(
            new LogHabitEntryCommand(HabitType.Smoking, HabitSubType.Cigarette, Now, null), CancellationToken.None);

        response.Should().BeOfType<CreatedResult>();
    }

    [Fact]
    public async Task LogEntry_WhenHandlerFails_ReturnsBadRequest()
    {
        var commandHandler = Substitute.For<ICommandHandler<LogHabitEntryCommand, Result<Guid>>>();
        commandHandler.Handle(Arg.Any<LogHabitEntryCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<Guid>(new Error("BadHabits.Validation", "bad input")));
        var queryHandler = Substitute.For<IQueryHandler<GetHabitEntriesQuery, Result<IReadOnlyList<HabitEntryDto>>>>();
        var controller = new BadHabitsController(commandHandler, queryHandler);

        var response = await controller.LogEntry(
            new LogHabitEntryCommand(HabitType.Smoking, HabitSubType.Cigarette, Now, null), CancellationToken.None);

        response.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetEntries_WhenHandlerSucceeds_ReturnsOkWithEntries()
    {
        var commandHandler = Substitute.For<ICommandHandler<LogHabitEntryCommand, Result<Guid>>>();
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

- [ ] **Step 2: Run the tests to verify they fail to compile**

Run: `dotnet test src/DzhusShelter.Api.Tests`
Expected: build error — `BadHabitsController` doesn't exist yet.

- [ ] **Step 3: Implement the controller**

Create `src/DzhusShelter.Api/Controllers/BadHabitsController.cs`:

```csharp
using DzhusShelter.Api.Application.Abstractions;
using DzhusShelter.Api.Application.BadHabits;
using DzhusShelter.Api.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace DzhusShelter.Api.Controllers;

[ApiController]
[Route("api/bad-habits")]
public sealed class BadHabitsController : ControllerBase
{
    private readonly ICommandHandler<LogHabitEntryCommand, Result<Guid>> _logHandler;
    private readonly IQueryHandler<GetHabitEntriesQuery, Result<IReadOnlyList<HabitEntryDto>>> _getHandler;

    public BadHabitsController(
        ICommandHandler<LogHabitEntryCommand, Result<Guid>> logHandler,
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
            ? Created(string.Empty, new { id = result.Value })
            : BadRequest(new { error = result.Error.Message });
    }

    [HttpGet("entries")]
    public async Task<IActionResult> GetEntries([FromQuery] GetHabitEntriesQuery query, CancellationToken cancellationToken)
    {
        var result = await _getHandler.Handle(query, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error.Message });
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test src/DzhusShelter.Api.Tests`
Expected: `Passed! - Failed: 0, Passed: 10`

- [ ] **Step 5: Commit**

```bash
git add src/DzhusShelter.Api src/DzhusShelter.Api.Tests
git commit -m "feat(api): add BadHabitsController"
```

---

### Task 7: Integration test — real PostgreSQL via Testcontainers

**Files:**
- Create: `src/DzhusShelter.Api.IntegrationTests/DzhusShelter.Api.IntegrationTests.csproj`
- Create: `src/DzhusShelter.Api.IntegrationTests/ApiWebApplicationFactory.cs`
- Create: `src/DzhusShelter.Api.IntegrationTests/BadHabitsEndpointsTests.cs`
- Modify: `DzhusShelter.slnx`

**Interfaces:**
- Consumes: `Program` (entry point) from Task 1, `AppDbContext` from Task 5
- Produces: a passing end-to-end test proving `POST` then `GET` round-trips through a real PostgreSQL container

**Prerequisite:** Docker must be running locally (Docker Desktop or an equivalent daemon) — `Testcontainers` starts a real `postgres` container for this test.

- [ ] **Step 1: Scaffold the integration test project**

Create `src/DzhusShelter.Api.IntegrationTests/DzhusShelter.Api.IntegrationTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\DzhusShelter.Api\DzhusShelter.Api.csproj" />
  </ItemGroup>

</Project>
```

Run:
```bash
dotnet add src/DzhusShelter.Api.IntegrationTests package Microsoft.NET.Test.Sdk
dotnet add src/DzhusShelter.Api.IntegrationTests package xunit
dotnet add src/DzhusShelter.Api.IntegrationTests package xunit.runner.visualstudio
dotnet add src/DzhusShelter.Api.IntegrationTests package FluentAssertions
dotnet add src/DzhusShelter.Api.IntegrationTests package Microsoft.AspNetCore.Mvc.Testing
dotnet add src/DzhusShelter.Api.IntegrationTests package Testcontainers.PostgreSql
```

Add it to `DzhusShelter.slnx`:

```xml
<Solution>
  <Project Path="src/DzhusShelter.UI/DzhusShelter.UI.csproj" />
  <Project Path="src/DzhusShelter.Api/DzhusShelter.Api.csproj" />
  <Project Path="src/DzhusShelter.Api.Tests/DzhusShelter.Api.Tests.csproj" />
  <Project Path="src/DzhusShelter.Api.IntegrationTests/DzhusShelter.Api.IntegrationTests.csproj" />
</Solution>
```

Create `src/DzhusShelter.Api.IntegrationTests/GlobalUsings.cs`:

```csharp
global using Xunit;
```

- [ ] **Step 2: Write the test factory**

Create `src/DzhusShelter.Api.IntegrationTests/ApiWebApplicationFactory.cs`:

```csharp
using DzhusShelter.Api.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace DzhusShelter.Api.IntegrationTests;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.Single(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            services.Remove(descriptor);
            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(_postgres.GetConnectionString()));
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}
```

- [ ] **Step 3: Write the failing end-to-end test**

Create `src/DzhusShelter.Api.IntegrationTests/BadHabitsEndpointsTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DzhusShelter.Api.Application.BadHabits;
using DzhusShelter.Api.Domain.BadHabits;
using FluentAssertions;

namespace DzhusShelter.Api.IntegrationTests;

public class BadHabitsEndpointsTests : IClassFixture<ApiWebApplicationFactory>
{
    // The API serializes camelCase property names and string enums (see DzhusShelter.Api's
    // JsonStringEnumConverter registration + ASP.NET Core's default camelCase policy).
    // HttpClient's default JSON options are case-sensitive and don't include that converter —
    // without both settings here, deserialization silently produces an object with default
    // field values instead of throwing, which is easy to misdiagnose as "no entry was saved."
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

    [Fact]
    public async Task LoggingAnEntry_ThenFetchingIt_RoundTripsThroughPostgres()
    {
        var occurredAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var logResponse = await _client.PostAsJsonAsync("/api/bad-habits/entries",
            new LogHabitEntryCommand(HabitType.Alcohol, HabitSubType.Beer, occurredAt, "friday"));
        logResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var getResponse = await _client.GetAsync(
            $"/api/bad-habits/entries?from={Uri.EscapeDataString(occurredAt.AddMinutes(-1).ToString("O"))}" +
            $"&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"))}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var entries = await getResponse.Content.ReadFromJsonAsync<List<HabitEntryDto>>(JsonOptions);
        entries.Should().ContainSingle(e => e.HabitType == HabitType.Alcohol && e.SubType == HabitSubType.Beer && e.Notes == "friday");
    }
}
```

- [ ] **Step 4: Run the test to verify it fails**

Run: `dotnet test src/DzhusShelter.Api.IntegrationTests`
Expected: FAIL (or build error) before the factory/test wiring above exists — confirm it fails for the *expected* reason (missing types), then proceed; if Docker isn't running, this step instead fails with a Testcontainers connection error — start Docker before continuing.

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test src/DzhusShelter.Api.IntegrationTests`
Expected: `Passed! - Failed: 0, Passed: 1` (takes longer than unit tests — it's pulling/starting a real Postgres container)

- [ ] **Step 6: Commit**

```bash
git add src/DzhusShelter.Api.IntegrationTests DzhusShelter.slnx
git commit -m "test(api): add Testcontainers-based integration test for Bad Habits endpoints"
```

---

### Task 8: Docker — `src/DzhusShelter.Api` container + PostgreSQL in `docker-compose.yml`

**Files:**
- Create: `src/DzhusShelter.Api/Dockerfile`
- Modify: `docker-compose.yml`
- Create: `.env.example`

**Interfaces:**
- Produces: `api` and `postgres` services runnable via `docker compose up`, with `api`'s port kept internal to the compose network (per Global Constraints)

- [ ] **Step 1: Write the API Dockerfile**

Create `src/DzhusShelter.Api/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/DzhusShelter.Api/DzhusShelter.Api.csproj", "src/DzhusShelter.Api/"]
RUN dotnet restore "src/DzhusShelter.Api/DzhusShelter.Api.csproj"
COPY . .
WORKDIR "/src/src/DzhusShelter.Api"
RUN dotnet build "DzhusShelter.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "DzhusShelter.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DzhusShelter.Api.dll"]
```

- [ ] **Step 2: Update `docker-compose.yml`**

Modify `docker-compose.yml` — replace its contents:

```yaml
services:
  personal-site:
    build:
      context: .
      dockerfile: src/DzhusShelter.UI/Dockerfile
    image: personal-site:latest
    container_name: personal-site
    ports:
      - "8080:8080"
    environment:
      - Api__BaseUrl=http://api:8080
    depends_on:
      - api
    restart: unless-stopped

  api:
    build:
      context: .
      dockerfile: src/DzhusShelter.Api/Dockerfile
    image: dzhusshelter-api:latest
    container_name: dzhusshelter-api
    environment:
      - ConnectionStrings__Postgres=Host=postgres;Port=5432;Database=dzhusshelter;Username=postgres;Password=${POSTGRES_PASSWORD}
    depends_on:
      - postgres
    restart: unless-stopped
    # No "ports:" mapping here on purpose — the API must stay internal to this
    # compose network until authentication is added (see docs/architecture.md).

  postgres:
    image: postgres:16-alpine
    container_name: dzhusshelter-postgres
    environment:
      - POSTGRES_DB=dzhusshelter
      - POSTGRES_PASSWORD=${POSTGRES_PASSWORD}
    ports:
      # Published for local dev (running the API/bot with `dotnet run` outside
      # Docker needs to reach Postgres at localhost) — unlike the api service,
      # nothing in docs/architecture.md restricts exposing Postgres itself.
      - "5432:5432"
    volumes:
      - postgres-data:/var/lib/postgresql/data
    restart: unless-stopped

volumes:
  postgres-data:
```

- [ ] **Step 3: Document required environment variables**

Create `.env.example`:

```
POSTGRES_PASSWORD=change-me
```

(`.env` itself is already gitignored — copy this file to `.env` locally and fill in a real password; docker-compose reads `.env` automatically.)

- [ ] **Step 4: Verify manually**

Run:
```bash
cp .env.example .env
docker compose up --build
```
Expected: all three containers (`personal-site`, `dzhusshelter-api`, `dzhusshelter-postgres`) start without errors; `docker compose logs api` shows the API applying the `InitialCreate` migration and starting to listen on port 8080 (internal only — not reachable from the host).

Stop with `docker compose down` when confirmed.

- [ ] **Step 5: Commit**

```bash
git add src/DzhusShelter.Api/Dockerfile docker-compose.yml .env.example
git commit -m "feat(infra): add API and PostgreSQL services to docker-compose"
```

---

### Task 9: Telegram bot — scaffold + habit-logging keyboard logic

**Files:**
- Create: `src/DzhusShelter.TelegramBot/DzhusShelter.TelegramBot.csproj`
- Create: `src/DzhusShelter.TelegramBot/Program.cs` (minimal placeholder — Task 10 replaces its contents)
- Create: `src/DzhusShelter.TelegramBot/BadHabitsKeyboard.cs`
- Create: `src/DzhusShelter.TelegramBot.Tests/DzhusShelter.TelegramBot.Tests.csproj`
- Create: `src/DzhusShelter.TelegramBot.Tests/BadHabitsKeyboardTests.cs`
- Modify: `DzhusShelter.slnx`

**Interfaces:**
- Produces: `BadHabitsKeyboard` — pure logic for the three-step flow (`/start` → "🚫 Шкідливі звички" root button → habit type → sub-type) and parsing callback data back into `(HabitType, HabitSubType)`, kept separate from the `Telegram.Bot` client so it's unit-testable without hitting Telegram's API

**Prerequisite (yours to do, not automatable):** create a bot via [@BotFather](https://t.me/BotFather) in Telegram and note its token — needed starting Task 10, not this one.

- [ ] **Step 1: Scaffold the bot project**

Create `src/DzhusShelter.TelegramBot/DzhusShelter.TelegramBot.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Worker">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

</Project>
```

Run:
```bash
dotnet add src/DzhusShelter.TelegramBot package Telegram.Bot
dotnet add src/DzhusShelter.TelegramBot package Microsoft.Extensions.Hosting
```
(`Microsoft.NET.Sdk.Worker` doesn't bundle a shared framework the way `Microsoft.NET.Sdk.Web` does for ASP.NET Core — without `Microsoft.Extensions.Hosting` explicitly, the SDK's own auto-generated global-usings file fails to compile with `Microsoft.Extensions.Hosting`/`.Configuration`/`.Logging` not found, even before any of your own code references them.)

`Microsoft.NET.Sdk.Worker` also sets `OutputType=Exe`, which needs an entry point to compile at all — even for a project referenced only for its `BadHabitsKeyboard` type, like this one is until Task 10. Create a minimal placeholder `src/DzhusShelter.TelegramBot/Program.cs` now; Task 10 replaces its contents with the real bot wiring:

```csharp
var builder = Host.CreateApplicationBuilder(args);

var host = builder.Build();
host.Run();
```

- [ ] **Step 2: Scaffold the bot test project**

Create `src/DzhusShelter.TelegramBot.Tests/DzhusShelter.TelegramBot.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\DzhusShelter.TelegramBot\DzhusShelter.TelegramBot.csproj" />
  </ItemGroup>

</Project>
```

Run:
```bash
dotnet add src/DzhusShelter.TelegramBot.Tests package Microsoft.NET.Test.Sdk
dotnet add src/DzhusShelter.TelegramBot.Tests package xunit
dotnet add src/DzhusShelter.TelegramBot.Tests package xunit.runner.visualstudio
dotnet add src/DzhusShelter.TelegramBot.Tests package FluentAssertions
```

Add both new projects to `DzhusShelter.slnx`:

```xml
<Solution>
  <Project Path="src/DzhusShelter.UI/DzhusShelter.UI.csproj" />
  <Project Path="src/DzhusShelter.Api/DzhusShelter.Api.csproj" />
  <Project Path="src/DzhusShelter.Api.Tests/DzhusShelter.Api.Tests.csproj" />
  <Project Path="src/DzhusShelter.Api.IntegrationTests/DzhusShelter.Api.IntegrationTests.csproj" />
  <Project Path="src/DzhusShelter.TelegramBot/DzhusShelter.TelegramBot.csproj" />
  <Project Path="src/DzhusShelter.TelegramBot.Tests/DzhusShelter.TelegramBot.Tests.csproj" />
</Solution>
```

Create `src/DzhusShelter.TelegramBot.Tests/GlobalUsings.cs`:

```csharp
global using Xunit;
```

- [ ] **Step 3: Write the failing keyboard tests**

Create `src/DzhusShelter.TelegramBot.Tests/BadHabitsKeyboardTests.cs`:

```csharp
using DzhusShelter.TelegramBot;
using FluentAssertions;

namespace DzhusShelter.TelegramBot.Tests;

public class BadHabitsKeyboardTests
{
    [Fact]
    public void TopLevelCallbackData_ForSmoking_ParsesBackToHabitType()
    {
        var callbackData = BadHabitsKeyboard.HabitTypeCallbackData(HabitType.Smoking);

        var parsed = BadHabitsKeyboard.TryParseHabitType(callbackData);

        parsed.Should().Be(HabitType.Smoking);
    }

    [Fact]
    public void SubTypeCallbackData_ForCigarette_ParsesBackToHabitTypeAndSubType()
    {
        var callbackData = BadHabitsKeyboard.SubTypeCallbackData(HabitType.Smoking, HabitSubType.Cigarette);

        var parsed = BadHabitsKeyboard.TryParseSubType(callbackData);

        parsed.Should().Be((HabitType.Smoking, HabitSubType.Cigarette));
    }

    [Fact]
    public void SubTypesFor_Alcohol_ReturnsBeerWineSpirits()
    {
        var subTypes = BadHabitsKeyboard.SubTypesFor(HabitType.Alcohol);

        subTypes.Should().BeEquivalentTo([HabitSubType.Beer, HabitSubType.Wine, HabitSubType.Spirits]);
    }

    [Fact]
    public void TryParseHabitType_WithUnrelatedCallbackData_ReturnsNull()
    {
        var parsed = BadHabitsKeyboard.TryParseHabitType("something-else");

        parsed.Should().BeNull();
    }

    [Fact]
    public void OpenMenuCallbackData_DoesNotCollideWithHabitTypeOrSubTypeParsing()
    {
        BadHabitsKeyboard.TryParseHabitType(BadHabitsKeyboard.OpenMenuCallbackData).Should().BeNull();
        BadHabitsKeyboard.TryParseSubType(BadHabitsKeyboard.OpenMenuCallbackData).Should().BeNull();
    }
}
```

- [ ] **Step 4: Run the tests to verify they fail to compile**

Run: `dotnet test src/DzhusShelter.TelegramBot.Tests`
Expected: build errors — `HabitType`, `HabitSubType`, `BadHabitsKeyboard` don't exist in this project yet.

- [ ] **Step 5: Implement local `HabitType`/`HabitSubType` enums and the keyboard logic**

The bot is a separate deployable from the API and doesn't reference it — it keeps its own copy of these two small enums (they must stay in sync with `src/DzhusShelter.Api`'s by value; this duplication is an accepted, explicit trade-off of not sharing a library between the two processes yet).

Create `src/DzhusShelter.TelegramBot/BadHabitsKeyboard.cs`:

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
    Spirits = 5,
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
        [HabitType.Smoking] = [HabitSubType.Cigarette, HabitSubType.Vape],
        [HabitType.Alcohol] = [HabitSubType.Beer, HabitSubType.Wine, HabitSubType.Spirits],
    };

    public static IReadOnlyList<HabitSubType> SubTypesFor(HabitType habitType) => SubTypesByHabitType[habitType];

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

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test src/DzhusShelter.TelegramBot.Tests`
Expected: `Passed! - Failed: 0, Passed: 5`

- [ ] **Step 7: Commit**

```bash
git add src/DzhusShelter.TelegramBot src/DzhusShelter.TelegramBot.Tests DzhusShelter.slnx
git commit -m "feat(bot): scaffold src/DzhusShelter.TelegramBot with habit-logging keyboard logic"
```

---

### Task 10: Telegram bot — wire the live bot to `src/DzhusShelter.Api`

**Files:**
- Create: `src/DzhusShelter.TelegramBot/BadHabitsApiClient.cs`
- Create: `src/DzhusShelter.TelegramBot/BotHostedService.cs`
- Modify: `src/DzhusShelter.TelegramBot/Program.cs` (replaces Task 9's placeholder)
- Create: `src/DzhusShelter.TelegramBot/appsettings.json`
- Create: `src/DzhusShelter.TelegramBot/Dockerfile`

**Interfaces:**
- Consumes: `BadHabitsKeyboard`, `HabitType`, `HabitSubType` from Task 9; `POST /api/bad-habits/entries` from Task 6
- Produces: a running bot that logs a habit entry end-to-end when its buttons are tapped in a real Telegram chat

**Prerequisite:** the bot token from Task 9's BotFather step, and your own Telegram numeric chat ID (message [@userinfobot](https://t.me/userinfobot) to get it) so the bot only responds to you.

- [ ] **Step 1: Implement the typed API client**

Create `src/DzhusShelter.TelegramBot/BadHabitsApiClient.cs`:

```csharp
using System.Net.Http.Json;

namespace DzhusShelter.TelegramBot;

public sealed record LogHabitEntryRequest(HabitType HabitType, HabitSubType SubType, DateTimeOffset OccurredAt, string? Notes);

public sealed class BadHabitsApiClient
{
    private readonly HttpClient _httpClient;

    public BadHabitsApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> LogEntryAsync(HabitType habitType, HabitSubType subType, CancellationToken cancellationToken)
    {
        var request = new LogHabitEntryRequest(habitType, subType, DateTimeOffset.UtcNow, null);
        var response = await _httpClient.PostAsJsonAsync("/api/bad-habits/entries", request, cancellationToken);
        return response.IsSuccessStatusCode;
    }
}
```

- [ ] **Step 2: Implement the bot hosted service**

Create `src/DzhusShelter.TelegramBot/BotHostedService.cs`:

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DzhusShelter.TelegramBot;

// DI's AddSingleton overloads require a reference type — `long` can't be registered
// directly, so it travels through this tiny settings class instead.
public sealed class TelegramBotSettings
{
    public required long AllowedChatId { get; init; }
}

public sealed class BotHostedService : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly BadHabitsApiClient _apiClient;
    private readonly long _allowedChatId;
    private readonly ILogger<BotHostedService> _logger;

    public BotHostedService(
        ITelegramBotClient botClient,
        BadHabitsApiClient apiClient,
        TelegramBotSettings settings,
        ILogger<BotHostedService> logger)
    {
        _botClient = botClient;
        _apiClient = apiClient;
        _allowedChatId = settings.AllowedChatId;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _botClient.StartReceiving(HandleUpdateAsync, HandlePollingErrorAsync, cancellationToken: stoppingToken);
        _logger.LogInformation("Telegram bot started long polling.");
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is { Text: "/start" } message && message.Chat.Id == _allowedChatId)
        {
            await SendRootMenuAsync(message.Chat.Id, cancellationToken);
            return;
        }

        if (update.CallbackQuery is { Data: { } data, Message.Chat.Id: var chatId } callback && chatId == _allowedChatId)
        {
            await HandleCallbackAsync(callback.Id, chatId, data, cancellationToken);
        }
    }

    private async Task SendRootMenuAsync(long chatId, CancellationToken cancellationToken)
    {
        var keyboard = new InlineKeyboardMarkup(
            InlineKeyboardButton.WithCallbackData("🚫 Шкідливі звички", BadHabitsKeyboard.OpenMenuCallbackData));

        await _botClient.SendMessage(chatId, "Що фіксуємо?", replyMarkup: keyboard, cancellationToken: cancellationToken);
    }

    private async Task SendHabitTypeMenuAsync(long chatId, CancellationToken cancellationToken)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🚬 Куріння", BadHabitsKeyboard.HabitTypeCallbackData(HabitType.Smoking)),
                InlineKeyboardButton.WithCallbackData("🍺 Алкоголь", BadHabitsKeyboard.HabitTypeCallbackData(HabitType.Alcohol)),
            },
        });

        await _botClient.SendMessage(chatId, "Яка звичка?", replyMarkup: keyboard, cancellationToken: cancellationToken);
    }

    private async Task HandleCallbackAsync(string callbackId, long chatId, string data, CancellationToken cancellationToken)
    {
        if (data == BadHabitsKeyboard.OpenMenuCallbackData)
        {
            await _botClient.AnswerCallbackQuery(callbackId, cancellationToken: cancellationToken);
            await SendHabitTypeMenuAsync(chatId, cancellationToken);
            return;
        }

        var habitType = BadHabitsKeyboard.TryParseHabitType(data);
        if (habitType is not null)
        {
            var buttons = BadHabitsKeyboard.SubTypesFor(habitType.Value)
                .Select(subType => InlineKeyboardButton.WithCallbackData(
                    subType.ToString(), BadHabitsKeyboard.SubTypeCallbackData(habitType.Value, subType)))
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
            var succeeded = await _apiClient.LogEntryAsync(parsedHabitType, subType, cancellationToken);

            await _botClient.AnswerCallbackQuery(callbackId, cancellationToken: cancellationToken);
            await _botClient.SendMessage(
                chatId,
                succeeded ? $"Записано: {subType}" : "Не вдалося записати — спробуй ще раз.",
                cancellationToken: cancellationToken);
        }
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Telegram polling error from {Source}", source);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 3: Implement `Program.cs`**

Modify `src/DzhusShelter.TelegramBot/Program.cs` — replace Task 9's placeholder contents:

```csharp
using DzhusShelter.TelegramBot;
using Telegram.Bot;

var builder = Host.CreateApplicationBuilder(args);

var botToken = builder.Configuration["Telegram:BotToken"]
    ?? throw new InvalidOperationException("Telegram:BotToken is not configured.");
var allowedChatId = builder.Configuration.GetValue<long?>("Telegram:AllowedChatId")
    ?? throw new InvalidOperationException("Telegram:AllowedChatId is not configured.");
var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException("Api:BaseUrl is not configured.");

builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(botToken));
builder.Services.AddHttpClient<BadHabitsApiClient>(client => client.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddSingleton(new TelegramBotSettings { AllowedChatId = allowedChatId });
builder.Services.AddHostedService<BotHostedService>();

var host = builder.Build();
host.Run();
```

Run: `dotnet add src/DzhusShelter.TelegramBot package Microsoft.Extensions.Http` (`AddHttpClient` isn't available from `Microsoft.Extensions.Hosting` alone — it's a separate package, bundled automatically only in `Microsoft.NET.Sdk.Web` projects, not `Sdk.Worker`).

- [ ] **Step 4: Add local configuration (secrets stay out of git)**

Create `src/DzhusShelter.TelegramBot/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "Telegram": {
    "BotToken": "",
    "AllowedChatId": 0
  },
  "Api": {
    "BaseUrl": "http://localhost:5000"
  }
}
```

Set the real token and chat ID locally via user secrets instead of editing this file:

```bash
dotnet user-secrets init --project src/DzhusShelter.TelegramBot
dotnet user-secrets set "Telegram:BotToken" "<token from BotFather>" --project src/DzhusShelter.TelegramBot
dotnet user-secrets set "Telegram:AllowedChatId" "<your numeric chat id>" --project src/DzhusShelter.TelegramBot
```

- [ ] **Step 5: Add the bot's Dockerfile**

Create `src/DzhusShelter.TelegramBot/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/DzhusShelter.TelegramBot/DzhusShelter.TelegramBot.csproj", "src/DzhusShelter.TelegramBot/"]
RUN dotnet restore "src/DzhusShelter.TelegramBot/DzhusShelter.TelegramBot.csproj"
COPY . .
WORKDIR "/src/src/DzhusShelter.TelegramBot"
RUN dotnet build "DzhusShelter.TelegramBot.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "DzhusShelter.TelegramBot.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DzhusShelter.TelegramBot.dll"]
```

- [ ] **Step 6: Build to verify**

Run: `dotnet build src/DzhusShelter.TelegramBot`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`. If this fails on the `Telegram.Bot` API calls in `BotHostedService.cs` (method names on `ITelegramBotClient` do shift between package versions — e.g. `SendMessage` vs `SendTextMessageAsync`, `AnswerCallbackQuery` vs `AnswerCallbackQueryAsync`), check the installed package version's actual member names (IntelliSense/`dotnet-format` or the package's IntelliSense XML docs) and adjust the call sites to match — the logic/flow stays the same, only the exact method name changes.

- [ ] **Step 7: Verify manually end-to-end**

Run the API locally (`dotnet run --project src/DzhusShelter.Api`, with PostgreSQL reachable — e.g. via `docker compose up postgres`), then run the bot with `DOTNET_ENVIRONMENT=Development dotnet run --project src/DzhusShelter.TelegramBot`. The explicit `DOTNET_ENVIRONMENT=Development` matters: `Host.CreateApplicationBuilder` only auto-loads user secrets when the environment is Development, and it defaults to Production when unset — without it the bot silently reads the empty `BotToken` placeholder from `appsettings.json` instead of your real secret and fails with "Bot token invalid" (easy to misread as a bad token when it's actually just an unset environment). In Telegram, message your bot `/start`, tap "🚫 Шкідливі звички", tap "🚬 Куріння", then "Cigarette".
Expected: the bot replies "Записано: Cigarette", and `GET http://localhost:5000/api/bad-habits/entries?from=...&to=...` (via Swagger UI or curl) shows the new entry.

- [ ] **Step 8: Commit**

```bash
git add src/DzhusShelter.TelegramBot
git commit -m "feat(bot): wire live Telegram bot flow to src/DzhusShelter.Api"
```

---

### Task 11: Blazor UI — `BadHabits.razor` calling the API with filters + chart

**Files:**
- Create: `src/DzhusShelter.UI/Services/BadHabitsApiClient.cs`
- Modify: `src/DzhusShelter.UI/Components/Pages/BadHabits.razor`
- Modify: `src/DzhusShelter.UI/Program.cs`
- Modify: `src/DzhusShelter.UI/appsettings.json` / `src/DzhusShelter.UI/appsettings.Development.json`

**Interfaces:**
- Consumes: `GET /api/bad-habits/entries` from Task 6
- Produces: a working `/bad-habits` page with date-range + type/subtype filters and a chart

- [ ] **Step 1: Add the chart package**

Run: `dotnet add src/DzhusShelter.UI/DzhusShelter.UI.csproj package Blazor-ApexCharts`

- [ ] **Step 2: Implement the typed API client**

Create `src/DzhusShelter.UI/Services/BadHabitsApiClient.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DzhusShelter.UI.Services;

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
    Spirits = 5,
}

public sealed record HabitEntryDto(Guid Id, HabitType HabitType, HabitSubType SubType, DateTimeOffset OccurredAt, string? Notes);

public sealed class BadHabitsApiClient
{
    // The API serializes camelCase property names and string enums (see src/DzhusShelter.Api's
    // JsonStringEnumConverter registration + ASP.NET Core's default camelCase policy).
    // HttpClient's default JSON options are case-sensitive and don't include that converter —
    // without both settings here, deserialization silently produces an object with default
    // field values instead of throwing (learned this the hard way in Task 7's integration test).
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _httpClient;

    public BadHabitsApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<HabitEntryDto>> GetEntriesAsync(
        DateTimeOffset from, DateTimeOffset to, HabitType? habitType, HabitSubType? subType, CancellationToken cancellationToken)
    {
        var query = $"from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}";
        if (habitType is not null)
            query += $"&habitType={habitType}";
        if (subType is not null)
            query += $"&subType={subType}";

        var entries = await _httpClient.GetFromJsonAsync<List<HabitEntryDto>>(
            $"/api/bad-habits/entries?{query}", JsonOptions, cancellationToken);
        return entries ?? [];
    }
}
```

- [ ] **Step 3: Register the HttpClient in `Program.cs`**

Modify `src/DzhusShelter.UI/Program.cs` — add before `var app = builder.Build();`:

```csharp
using DzhusShelter.UI.Services;
```

at the top of the file, and add this registration alongside the existing `builder.Services.AddRazorComponents()` call:

```csharp
builder.Services.AddHttpClient<BadHabitsApiClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"]
        ?? throw new InvalidOperationException("Api:BaseUrl is not configured.")));
```

- [ ] **Step 4: Add `Api:BaseUrl` to configuration**

Modify `src/DzhusShelter.UI/appsettings.Development.json` — add:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore.DataProtection": "None"
    }
  },
  "Api": {
    "BaseUrl": "http://localhost:5000"
  }
}
```

(Adjust the `Logging` section to keep whatever is already there — only the `Api` section is new.)

- [ ] **Step 5: Rewrite `BadHabits.razor`**

Modify `src/DzhusShelter.UI/Components/Pages/BadHabits.razor` — replace its contents. Note the `@rendermode InteractiveServer` directive: none of this repo's other pages set a render mode, and `Program.cs`'s `AddInteractiveServerRenderMode()` only makes server interactivity *available*, it doesn't turn it on by default per page. Without this directive the page still compiles and does an initial static render (so a first look can be misleading — the filters and chart container appear in the HTML), but `@bind:after` never fires and the ApexChart component never gets a live circuit to run its JS interop in, so the chart div stays permanently empty with no error anywhere:

```razor
@page "/bad-habits"
@rendermode InteractiveServer
@using ApexCharts
@using DzhusShelter.UI.Services
@inject BadHabitsApiClient ApiClient

<PageTitle>Bad Habits</PageTitle>

<h1>Bad Habits</h1>

<div class="nes-container with-title is-dark" style="margin-top: 1rem;">
    <p class="title">Фільтри</p>
    <div style="display: flex; gap: 1rem; flex-wrap: wrap; align-items: center;">
        <label>
            Від: <input type="date" class="nes-input" @bind="_fromDate" @bind:after="ReloadAsync" />
        </label>
        <label>
            До: <input type="date" class="nes-input" @bind="_toDate" @bind:after="ReloadAsync" />
        </label>
        <label>
            Тип:
            <select class="nes-select" @bind="_selectedHabitType" @bind:after="ReloadAsync">
                <option value="">Всі</option>
                <option value="@HabitType.Smoking">Куріння</option>
                <option value="@HabitType.Alcohol">Алкоголь</option>
            </select>
        </label>
    </div>
</div>

<div class="nes-container with-title is-dark" style="margin-top: 1rem;">
    <p class="title">Графік</p>
    @if (_entries is null)
    {
        <p><em>Завантаження...</em></p>
    }
    else if (_entries.Count == 0)
    {
        <p>Немає записів за обраний період.</p>
    }
    else
    {
        @* @key forces Blazor to tear down and recreate the chart whenever the underlying data
           changes — ApexChart doesn't otherwise pick up a new Items collection reference on its
           own after the first render (confirmed by hand: changing filters updated the API call
           but left the chart showing stale data until this was added). *@
        <ApexChart @key="_renderKey" TItem="DailyCount" Title="Кількість подій за днями">
            <ApexPointSeries TItem="DailyCount"
                             Items="_dailyCounts"
                             Name="Записи"
                             XValue="e => e.Date"
                             YValue="e => e.Count"
                             SeriesType="SeriesType.Bar" />
        </ApexChart>
    }
</div>

@code {
    private DateTime _fromDate = DateTime.Today.AddDays(-30);
    private DateTime _toDate = DateTime.Today;
    private string _selectedHabitType = "";
    private List<HabitEntryDto>? _entries;
    private List<DailyCount> _dailyCounts = [];
    private int _renderKey;

    protected override async Task OnInitializedAsync() => await ReloadAsync();

    private async Task ReloadAsync()
    {
        HabitType? habitType = Enum.TryParse<HabitType>(_selectedHabitType, out var parsed) ? parsed : null;

        // DateTime.Today/@bind on <input type="date"> both produce DateTimeKind.Local values —
        // pairing a Local-kind DateTime with an explicit TimeSpan.Zero offset throws unless the
        // machine's local offset actually is zero. Treat the date as offset-agnostic instead.
        var from = DateTime.SpecifyKind(_fromDate, DateTimeKind.Unspecified);
        var to = DateTime.SpecifyKind(_toDate.AddDays(1).AddTicks(-1), DateTimeKind.Unspecified);

        _entries = await ApiClient.GetEntriesAsync(
            new DateTimeOffset(from, TimeSpan.Zero),
            new DateTimeOffset(to, TimeSpan.Zero),
            habitType,
            null,
            CancellationToken.None);

        _dailyCounts = _entries
            .GroupBy(e => e.OccurredAt.Date)
            .Select(g => new DailyCount(g.Key, g.Count()))
            .OrderBy(d => d.Date)
            .ToList();
        _renderKey++;
    }

    private sealed record DailyCount(DateTime Date, int Count);
}
```

- [ ] **Step 6: Register the ApexCharts service**

Modify `src/DzhusShelter.UI/Program.cs` — add alongside the other `builder.Services` calls:

```csharp
builder.Services.AddApexCharts();
```

and add `using ApexCharts;` to the top of the file.

- [ ] **Step 7: Build to verify**

Run: `dotnet build src/DzhusShelter.UI/DzhusShelter.UI.csproj`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`. If this fails on the `ApexChart`/`ApexPointSeries` component parameters in `BadHabits.razor` (Blazor-ApexCharts' exact parameter names can shift between versions), check the installed package version's sample usage (its GitHub README/NuGet page) and adjust the markup to match — same chart, same data binding, only parameter names may differ.

- [ ] **Step 8: Verify manually in the browser**

Run: `dotnet run --project src/DzhusShelter.Api` (in one terminal, with Postgres up via `docker compose up postgres`), then `dotnet run --project src/DzhusShelter.UI/DzhusShelter.UI.csproj` (in another).
Open `/bad-habits` in a browser.
Expected: the page loads without errors, shows "Немає записів за обраний період." if nothing's logged yet, or a bar chart once at least one entry exists (log one via the bot from Task 10, then change a filter to trigger `ReloadAsync` and confirm the chart updates).

- [ ] **Step 9: Commit**

```bash
git add src/DzhusShelter.UI/Services src/DzhusShelter.UI/Components/Pages/BadHabits.razor src/DzhusShelter.UI/Program.cs src/DzhusShelter.UI/appsettings.Development.json src/DzhusShelter.UI/DzhusShelter.UI.csproj
git commit -m "feat(ui): rewrite Bad Habits page to call the API with filters and a chart"
```

---

### Task 12: Wire the UI and bot into `docker-compose.yml`

**Files:**
- Create: `src/DzhusShelter.TelegramBot`'s compose environment entries (in `docker-compose.yml`)
- Modify: `docker-compose.yml`
- Modify: `.env.example`

**Interfaces:**
- Produces: all four services (`personal-site`, `api`, `bot`, `postgres`) runnable together via one `docker compose up`

- [ ] **Step 1: Add the bot service and finish wiring env vars**

Modify `docker-compose.yml` — replace its contents:

```yaml
services:
  personal-site:
    build:
      context: .
      dockerfile: src/DzhusShelter.UI/Dockerfile
    image: personal-site:latest
    container_name: personal-site
    ports:
      - "8080:8080"
    environment:
      - Api__BaseUrl=http://api:8080
    depends_on:
      - api
    restart: unless-stopped

  api:
    build:
      context: .
      dockerfile: src/DzhusShelter.Api/Dockerfile
    image: dzhusshelter-api:latest
    container_name: dzhusshelter-api
    environment:
      - ConnectionStrings__Postgres=Host=postgres;Port=5432;Database=dzhusshelter;Username=postgres;Password=${POSTGRES_PASSWORD}
    depends_on:
      - postgres
    restart: unless-stopped
    # No "ports:" mapping here on purpose — see docs/architecture.md's Security section.

  bot:
    build:
      context: .
      dockerfile: src/DzhusShelter.TelegramBot/Dockerfile
    image: dzhusshelter-bot:latest
    container_name: dzhusshelter-bot
    environment:
      - Telegram__BotToken=${TELEGRAM_BOT_TOKEN}
      - Telegram__AllowedChatId=${TELEGRAM_ALLOWED_CHAT_ID}
      - Api__BaseUrl=http://api:8080
    depends_on:
      - api
    restart: unless-stopped

  postgres:
    image: postgres:16-alpine
    container_name: dzhusshelter-postgres
    environment:
      - POSTGRES_DB=dzhusshelter
      - POSTGRES_PASSWORD=${POSTGRES_PASSWORD}
    ports:
      # Published for local dev (running the API/bot with `dotnet run` outside
      # Docker needs to reach Postgres at localhost) — unlike the api service,
      # nothing in docs/architecture.md restricts exposing Postgres itself.
      - "5432:5432"
    volumes:
      - postgres-data:/var/lib/postgresql/data
    restart: unless-stopped

volumes:
  postgres-data:
```

- [ ] **Step 2: Update `.env.example`**

Modify `.env.example`:

```
POSTGRES_PASSWORD=change-me
TELEGRAM_BOT_TOKEN=change-me
TELEGRAM_ALLOWED_CHAT_ID=change-me
```

- [ ] **Step 3: Verify the full stack manually**

Run:
```bash
docker compose up --build
```
(Make sure your local `.env` has real values for `TELEGRAM_BOT_TOKEN` and `TELEGRAM_ALLOWED_CHAT_ID` — copy them from Task 9/10's setup.)

Expected: all four containers start; messaging the bot `/start` and logging an entry makes it show up on `http://localhost:8080/bad-habits` after refreshing/changing a filter.

Stop with `docker compose down` when confirmed.

- [ ] **Step 4: Commit**

```bash
git add docker-compose.yml .env.example
git commit -m "feat(infra): wire Telegram bot into docker-compose alongside API and UI"
```

---

### Task 13: Update spec status and roadmap progress

**Files:**
- Modify: `docs/specs/bad-habits.md`
- Modify: `docs/specs/README.md`
- Modify: `docs/roadmap.md`

**Interfaces:**
- None — documentation only.

- [ ] **Step 1: Flip the Bad Habits spec status**

Modify `docs/specs/bad-habits.md` — change the header line:

```markdown
**Route:** `/bad-habits` · **Status:** Implemented · **Architecture:** [docs/architecture.md](../architecture.md)
```

- [ ] **Step 2: Update the feature status table**

Modify `docs/specs/README.md` — change the Bad Habits row:

```markdown
| Bad Habits | `/bad-habits` | [bad-habits.md](bad-habits.md) | Implemented |
```

- [ ] **Step 3: Add a roadmap progress log entry**

Modify `docs/roadmap.md` — add a new line under "Progress log" (keep the existing 2026-09-21 line above it):

```markdown
- **(today's date)** — Bad Habits implemented end-to-end: `src/DzhusShelter.Api` (Clean Architecture + CQRS + FluentValidation + EF Core/PostgreSQL), `src/DzhusShelter.TelegramBot` (long-polling bot logging events), `src/DzhusShelter.UI` (Blazor-ApexCharts dashboard with filters). First real use of `Testcontainers`. Topics exercised for real: Clean Architecture, CQRS, Result pattern, FluentValidation, PostgreSQL/EF Core, Docker Compose multi-service, xUnit/NSubstitute/Testcontainers.
```

- [ ] **Step 4: Commit**

```bash
git add docs/specs/bad-habits.md docs/specs/README.md docs/roadmap.md
git commit -m "docs: mark Bad Habits as implemented"
```
