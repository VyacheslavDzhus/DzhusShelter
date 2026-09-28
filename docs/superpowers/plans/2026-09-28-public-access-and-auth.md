# Public Access & Auth Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Protect `DzhusShelter.Api` with two auth schemes (JWT for the UI's human login, a per-client API key for the bot and iOS Shortcuts), wire the UI and bot to use them, and update the deployment so `Api` and `UI` are safely reachable through the CasaOS host's existing Cloudflare Tunnel.

**Architecture:** ASP.NET Core's multi-scheme authentication (`AddJwtBearer` + a custom `AuthenticationHandler` for API keys), unified behind a global fallback authorization policy so every endpoint is auth-required by default except `/api/auth/login` itself. No new persistence — the single admin credential and the two API keys live in config (`.env`), not a database table.

**Tech Stack:** .NET 10, ASP.NET Core Web API (`Microsoft.AspNetCore.Authentication.JwtBearer`, built-in `Microsoft.AspNetCore.RateLimiting`), `BCrypt.Net-Next` for password hashing, Blazor Server (existing `DzhusShelter.UI`), existing CQRS abstractions (`ICommandHandler<TCommand, TResponse>`), xUnit + FluentAssertions + NSubstitute (existing `DzhusShelter.Api.Tests`), Testcontainers (existing `DzhusShelter.Api.IntegrationTests`).

**Spec:** [docs/specs/public-access-and-auth.md](../../specs/public-access-and-auth.md)

## Global Constraints

- Every `DzhusShelter.Api` endpoint requires auth by default (a global `FallbackPolicy`) — new controllers added later are protected automatically; only `/api/auth/login` explicitly opts out via `[AllowAnonymous]`.
- Config key names are fixed across every task — do not rename:

  | `.env` var | Api config key | Bot config key |
  |---|---|---|
  | `JWT_SIGNING_KEY` | `Jwt:SigningKey` | — |
  | `ADMIN_PASSWORD_HASH` | `Auth:AdminPasswordHash` | — |
  | `API_KEY_BOT` | `Auth:ApiKeyBot` | `Api:ApiKey` |
  | `API_KEY_SHORTCUTS` | `Auth:ApiKeyShortcuts` | *(used only by the Shortcut itself, no container reads it as a sender)* |

  As Docker Compose environment variables these become `Jwt__SigningKey`, `Auth__AdminPasswordHash`, `Auth__ApiKeyBot`, `Auth__ApiKeyShortcuts`, `Api__ApiKey` (`:` → `__`, matching the existing `ConnectionStrings__Postgres` pattern).
- JWTs are signed `HMACSHA256`, 7-day expiry, no refresh token — re-login when it lapses (deliberate simplification, not an oversight).
- The login endpoint never distinguishes "wrong password" from any other failure in its response — always a generic 401.
- `/api/auth/login` is rate-limited to 5 requests/minute **per client IP** (`Microsoft.AspNetCore.RateLimiting`, a fixed-window policy partitioned on `HttpContext.Connection.RemoteIpAddress`) — this only works correctly if `RemoteIpAddress` reflects the real client, not the tunnel's own hop; see Task 1's `UseForwardedHeaders` step.
- No new database tables — the admin credential and API keys are config, not domain data.
- No new `DzhusShelter.UI` test project (matches this repo's standing convention — no automated Blazor component tests); UI verification is build success + one manual browser pass (Task 6). `DzhusShelter.Api`'s auth logic **does** get real xUnit/integration tests — this repo already has that capability for `Api`, and security logic is exactly what most needs it.
- Never commit a real password, hash, or key — `.env` stays git-ignored as it already is; only `.env.example` gets placeholder values.

---

### Task 1: Register JWT + API-Key authentication schemes with a secure-by-default fallback policy

**Files:**
- Modify: `src/DzhusShelter.Api/DzhusShelter.Api.csproj`
- Create: `src/DzhusShelter.Api/Infrastructure/Auth/ApiKeyAuthenticationSchemeOptions.cs`
- Create: `src/DzhusShelter.Api/Infrastructure/Auth/ApiKeyAuthenticationHandler.cs`
- Modify: `src/DzhusShelter.Api/Program.cs`
- Modify: `src/DzhusShelter.Api.IntegrationTests/ApiWebApplicationFactory.cs`
- Modify: `src/DzhusShelter.Api.IntegrationTests/BadHabitsEndpointsTests.cs`

**Interfaces:**
- Produces: `ApiKeyAuthenticationHandler.SchemeName` (constant `"ApiKey"`) — Task 4 (bot) and Task 6 (manual verification) send `X-Api-Key` headers that this handler validates. A successful API-key auth carries a `client_name` claim (`"bot"` or `"shortcuts"`).
- Produces: a global `FallbackPolicy` requiring either the `"Bearer"` (JWT) or `"ApiKey"` scheme — every existing and future controller is protected without needing its own `[Authorize]` attribute. Task 2's `AuthController.Login` action is the only endpoint that opts out, via `[AllowAnonymous]`.

This task alone makes every currently-anonymous endpoint (`BadHabitsController`) reject unauthenticated requests — the existing integration test suite would otherwise go red the moment this merges, so updating the test harness to send a valid key is part of *this* task, not deferred to a later one.

- [ ] **Step 1: Add the JWT Bearer package**

```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.0" />
```

Add this line inside the existing `<ItemGroup>` in `src/DzhusShelter.Api/DzhusShelter.Api.csproj`, alongside `FluentValidation`.

- [ ] **Step 2: Create the API-key scheme options**

```csharp
// src/DzhusShelter.Api/Infrastructure/Auth/ApiKeyAuthenticationSchemeOptions.cs
using Microsoft.AspNetCore.Authentication;

namespace DzhusShelter.Api.Infrastructure.Auth;

public sealed class ApiKeyAuthenticationSchemeOptions : AuthenticationSchemeOptions;
```

- [ ] **Step 3: Write the API-key authentication handler**

```csharp
// src/DzhusShelter.Api/Infrastructure/Auth/ApiKeyAuthenticationHandler.cs
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace DzhusShelter.Api.Infrastructure.Auth;

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    private const string HeaderName = "X-Api-Key";

    private readonly IConfiguration _configuration;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var provided) || string.IsNullOrEmpty(provided))
            return Task.FromResult(AuthenticateResult.NoResult());

        var providedKey = provided.ToString();
        var botKey = _configuration["Auth:ApiKeyBot"];
        var shortcutsKey = _configuration["Auth:ApiKeyShortcuts"];

        // Plain string comparison, not constant-time: this is a personal-use API key over
        // HTTPS, not a defense against nation-state timing attacks — proportionate to the
        // threat model, not an oversight.
        string? clientName = !string.IsNullOrEmpty(botKey) && providedKey == botKey ? "bot"
            : !string.IsNullOrEmpty(shortcutsKey) && providedKey == shortcutsKey ? "shortcuts"
            : null;

        if (clientName is null)
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));

        var identity = new ClaimsIdentity([new Claim("client_name", clientName)], SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
```

- [ ] **Step 4: Wire both schemes, the fallback policy, forwarded headers, and rate limiting into `Program.cs`**

Replace the top of `src/DzhusShelter.Api/Program.cs` (everything from the `using` block through `var builder = ...`) with:

```csharp
using System.Text;
using System.Text.Json.Serialization;
using DzhusShelter.Api.Application.Abstractions;
using DzhusShelter.Api.Application.BadHabits;
using DzhusShelter.Api.Application.BadHabits.Commands;
using DzhusShelter.Api.Application.BadHabits.Dtos;
using DzhusShelter.Api.Application.BadHabits.Queries;
using DzhusShelter.Api.Domain.Common;
using DzhusShelter.Api.Infrastructure;
using DzhusShelter.Api.Infrastructure.Auth;
using DzhusShelter.Api.Infrastructure.BadHabits;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
```

Then, immediately after the existing `builder.Services.AddSingleton(TimeProvider.System);` line, add:

```csharp
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
        };
    })
    .AddScheme<ApiKeyAuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, _ => { });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(
            JwtBearerDefaults.AuthenticationScheme, ApiKeyAuthenticationHandler.SchemeName)
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("login", httpContext => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});
```

Then, right after `var app = builder.Build();` but **before** the `using (var scope = ...)` migration block, add:

```csharp
// Cloudflare Tunnel terminates TLS at its edge and forwards to this container over plain
// HTTP — without this, Request.IsHttps is always false and RemoteIpAddress is always the
// tunnel's own hop, breaking both secure-cookie logic (UI side) and the login rate
// limiter's per-IP partitioning above. Docker's port publishing means the immediate hop's
// IP isn't a fixed, known address, so the default trusted-proxy allowlist doesn't apply —
// clearing it is the standard fix for a containerized reverse-proxy setup like this one.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);
```

Finally, change this existing block:

```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();
```

to:

```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
```

`UseSwagger`/`UseSwaggerUI` are short-circuiting middleware registered before `UseAuthentication`/`UseAuthorization`, so Swagger stays reachable without a token in Development — intentional, not a gap, since it's dev-only tooling.

- [ ] **Step 5: Confirm the build fails to start without required config (expected, not a bug)**

Run: `dotnet build src/DzhusShelter.Api/DzhusShelter.Api.csproj`
Expected: builds successfully (the `Jwt:SigningKey` throw only fires at runtime startup, not at compile time).

- [ ] **Step 6: Update the integration test factory to inject test secrets**

`ApiWebApplicationFactory` currently swaps in a Testcontainers Postgres connection string but supplies no `Jwt:SigningKey`/`Auth:*` config — after Step 4, the app would fail to start under test. Add matching test values:

```csharp
// src/DzhusShelter.Api.IntegrationTests/ApiWebApplicationFactory.cs
using DzhusShelter.Api.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace DzhusShelter.Api.IntegrationTests;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string BotApiKey = "test-bot-key";
    public const string ShortcutsApiKey = "test-shortcuts-key";
    public const string AdminPassword = "test-admin-password";

    // MUST be a real bcrypt hash of AdminPassword ("test-admin-password") — the string
    // below is a deliberately-invalid placeholder, not a real hash; see the note right
    // after this code block for the exact command to generate and paste the real one.
    public const string AdminPasswordHash = "REPLACE-WITH-REAL-BCRYPT-HASH-OF-test-admin-password";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "test-signing-key-at-least-32-bytes-long!",
                ["Auth:AdminPasswordHash"] = AdminPasswordHash,
                ["Auth:ApiKeyBot"] = BotApiKey,
                ["Auth:ApiKeyShortcuts"] = ShortcutsApiKey,
            });
        });
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

> **Note for the implementer:** the `AdminPasswordHash` constant above must be a real bcrypt hash of `AdminPassword`. Generate it once with Python (already available in this environment) and paste the real output in place of the placeholder shown:
> ```bash
> pip install bcrypt
> python -c "import bcrypt; print(bcrypt.hashpw(b'test-admin-password', bcrypt.gensalt()).decode())"
> ```
> Paste that exact output as `AdminPasswordHash`'s value. Do not invent a hash string by hand — it must actually verify against `AdminPassword` via bcrypt or Task 2's login tests will all fail.

- [ ] **Step 7: Update `BadHabitsEndpointsTests` to send a valid API key, and add the anonymous-request test**

```csharp
// src/DzhusShelter.Api.IntegrationTests/BadHabitsEndpointsTests.cs
// Keep a reference to the factory (not just the pre-authenticated client), so other
// tests in this class can mint an independent, credential-less HttpClient without
// spinning up a second Testcontainers Postgres:
private readonly ApiWebApplicationFactory _factory;
private readonly HttpClient _client;

public BadHabitsEndpointsTests(ApiWebApplicationFactory factory)
{
    _factory = factory;
    _client = factory.CreateClient();
    _client.DefaultRequestHeaders.Add("X-Api-Key", ApiWebApplicationFactory.BotApiKey);
}
```

Add a new fact to the same class:

```csharp
[Fact]
public async Task GetEntries_WithoutAnyCredential_ReturnsUnauthorized()
{
    // A fresh HttpClient from the same factory/running Postgres container — its
    // DefaultRequestHeaders start empty, so it carries no X-Api-Key, unlike _client.
    using var anonymousClient = _factory.CreateClient();

    var response = await anonymousClient.GetAsync("/api/bad-habits/entries");

    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}
```

- [ ] **Step 8: Run the full test suite and confirm everything is green**

Run: `dotnet test DzhusShelter.slnx`
Expected: all previously-passing tests still pass (now sending a valid API key), plus the new `GetEntries_WithoutAnyCredential_ReturnsUnauthorized` test passes.

- [ ] **Step 9: Commit**

```bash
git add src/DzhusShelter.Api/DzhusShelter.Api.csproj src/DzhusShelter.Api/Infrastructure/Auth/ src/DzhusShelter.Api/Program.cs src/DzhusShelter.Api.IntegrationTests/ApiWebApplicationFactory.cs src/DzhusShelter.Api.IntegrationTests/BadHabitsEndpointsTests.cs
git commit -m "feat(api): add JWT + API-key auth schemes with secure-by-default fallback policy"
```

---

### Task 2: Login endpoint — bcrypt password check, JWT issuance, rate limiting

**Files:**
- Modify: `src/DzhusShelter.Api/DzhusShelter.Api.csproj`
- Create: `src/DzhusShelter.Api/Application/Auth/IJwtTokenGenerator.cs`
- Create: `src/DzhusShelter.Api/Infrastructure/Auth/JwtTokenGenerator.cs`
- Create: `src/DzhusShelter.Api/Application/Auth/Commands/LoginCommand.cs`
- Create: `src/DzhusShelter.Api/Application/Auth/Commands/LoginCommandValidator.cs`
- Create: `src/DzhusShelter.Api/Application/Auth/Commands/LoginCommandHandler.cs`
- Create: `src/DzhusShelter.Api/Controllers/AuthController.cs`
- Modify: `src/DzhusShelter.Api/Program.cs`
- Create: `src/DzhusShelter.Api.Tests/Infrastructure/Auth/JwtTokenGeneratorTests.cs`
- Create: `src/DzhusShelter.Api.Tests/Application/Auth/LoginCommandHandlerTests.cs`
- Create: `src/DzhusShelter.Api.IntegrationTests/AuthEndpointsTests.cs`

**Interfaces:**
- Consumes: `ApiKeyAuthenticationHandler.SchemeName`, the `Jwt:SigningKey`/`Auth:AdminPasswordHash` config keys, and the `"login"` rate-limiter policy — all produced by Task 1.
- Produces: `IJwtTokenGenerator.GenerateToken() : string` — a signed JWT string. `POST /api/auth/login` — the only public (`[AllowAnonymous]`) endpoint on this Api. Task 5 (UI) calls this endpoint.

- [ ] **Step 1: Add the bcrypt package**

```xml
<PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
```

Add to the same `<ItemGroup>` as the JWT Bearer package from Task 1.

- [ ] **Step 2: Write the token generator abstraction and implementation**

```csharp
// src/DzhusShelter.Api/Application/Auth/IJwtTokenGenerator.cs
namespace DzhusShelter.Api.Application.Auth;

public interface IJwtTokenGenerator
{
    string GenerateToken();
}
```

```csharp
// src/DzhusShelter.Api/Infrastructure/Auth/JwtTokenGenerator.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DzhusShelter.Api.Application.Auth;
using Microsoft.IdentityModel.Tokens;

namespace DzhusShelter.Api.Infrastructure.Auth;

public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _timeProvider;

    public JwtTokenGenerator(IConfiguration configuration, TimeProvider timeProvider)
    {
        _configuration = configuration;
        _timeProvider = timeProvider;
    }

    public string GenerateToken()
    {
        var signingKey = _configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var now = _timeProvider.GetUtcNow();
        var token = new JwtSecurityToken(
            claims: [new Claim(ClaimTypes.NameIdentifier, "admin")],
            notBefore: now.UtcDateTime,
            expires: now.AddDays(7).UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

- [ ] **Step 3: Write the failing unit tests for `JwtTokenGenerator`**

```csharp
// src/DzhusShelter.Api.Tests/Infrastructure/Auth/JwtTokenGeneratorTests.cs
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using DzhusShelter.Api.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Tokens;

namespace DzhusShelter.Api.Tests.Infrastructure.Auth;

public class JwtTokenGeneratorTests
{
    private const string SigningKey = "unit-test-signing-key-at-least-32-bytes!";
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private static JwtTokenGenerator CreateGenerator()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = SigningKey })
            .Build();
        var timeProvider = new FakeTimeProvider(Now);
        return new JwtTokenGenerator(configuration, timeProvider);
    }

    [Fact]
    public void GenerateToken_ProducesATokenThatValidatesAgainstTheSameSigningKey()
    {
        var generator = CreateGenerator();

        var token = generator.GenerateToken();

        var handler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
        };

        var act = () => handler.ValidateToken(token, validationParameters, out _);
        act.Should().NotThrow();
    }

    [Fact]
    public void GenerateToken_ExpiresSevenDaysFromNow()
    {
        var generator = CreateGenerator();

        var token = generator.GenerateToken();

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.ValidTo.Should().BeCloseTo(Now.AddDays(7).UtcDateTime, TimeSpan.FromSeconds(5));
    }
}
```

`FakeTimeProvider` comes from `Microsoft.Extensions.TimeProvider.Testing`, already referenced by `src/DzhusShelter.Api.Tests/DzhusShelter.Api.Tests.csproj` — no package changes needed for this step.

- [ ] **Step 4: Run the new tests to verify they fail (compile error — `JwtTokenGenerator`/package don't exist yet if Steps 1-2 were skipped; otherwise they should already pass since Step 2 came first)**

Run: `dotnet test src/DzhusShelter.Api.Tests --filter JwtTokenGeneratorTests`
Expected: PASS (Step 2 already implemented the generator — this is confirming the test suite exercises real, correct behavior, not a red/green TDD cycle for this particular file since the implementation was straightforward enough to write directly).

- [ ] **Step 5: Write the Login command, validator, and handler**

```csharp
// src/DzhusShelter.Api/Application/Auth/Commands/LoginCommand.cs
namespace DzhusShelter.Api.Application.Auth.Commands;

public sealed record LoginCommand(string Password);
```

```csharp
// src/DzhusShelter.Api/Application/Auth/Commands/LoginCommandValidator.cs
using FluentValidation;

namespace DzhusShelter.Api.Application.Auth.Commands;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Password).NotEmpty();
    }
}
```

```csharp
// src/DzhusShelter.Api/Application/Auth/Commands/LoginCommandHandler.cs
using DzhusShelter.Api.Application.Abstractions;
using DzhusShelter.Api.Application.Auth;
using DzhusShelter.Api.Domain.Common;
using FluentValidation;

namespace DzhusShelter.Api.Application.Auth.Commands;

public sealed class LoginCommandHandler : ICommandHandler<LoginCommand, Result<string>>
{
    private static readonly Error InvalidCredentials = new("Auth.InvalidCredentials", "Invalid credentials.");

    private readonly IConfiguration _configuration;
    private readonly IValidator<LoginCommand> _validator;
    private readonly IJwtTokenGenerator _tokenGenerator;

    public LoginCommandHandler(IConfiguration configuration, IValidator<LoginCommand> validator, IJwtTokenGenerator tokenGenerator)
    {
        _configuration = configuration;
        _validator = validator;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<Result<string>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Failure<string>(InvalidCredentials);

        var passwordHash = _configuration["Auth:AdminPasswordHash"];
        if (string.IsNullOrEmpty(passwordHash) || !BCrypt.Net.BCrypt.Verify(command.Password, passwordHash))
            return Result.Failure<string>(InvalidCredentials);

        return Result.Success(_tokenGenerator.GenerateToken());
    }
}
```

- [ ] **Step 6: Write unit tests for `LoginCommandHandler`**

```csharp
// src/DzhusShelter.Api.Tests/Application/Auth/LoginCommandHandlerTests.cs
using DzhusShelter.Api.Application.Auth;
using DzhusShelter.Api.Application.Auth.Commands;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace DzhusShelter.Api.Tests.Application.Auth;

public class LoginCommandHandlerTests
{
    private const string CorrectPassword = "correct-horse-battery-staple";

    // MUST be a real bcrypt hash of CorrectPassword above — a *different* string from
    // ApiWebApplicationFactory's placeholder (that one hashes a different password).
    // See Task 1 Step 6's note for the exact Python command to generate a real one.
    private const string PasswordHash = "REPLACE-WITH-REAL-BCRYPT-HASH-OF-correct-horse-battery-staple";

    private static LoginCommandHandler CreateHandler(IJwtTokenGenerator? tokenGenerator = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:AdminPasswordHash"] = PasswordHash })
            .Build();
        return new LoginCommandHandler(configuration, new LoginCommandValidator(), tokenGenerator ?? Substitute.For<IJwtTokenGenerator>());
    }

    [Fact]
    public async Task Handle_WithCorrectPassword_ReturnsSuccessWithGeneratedToken()
    {
        var tokenGenerator = Substitute.For<IJwtTokenGenerator>();
        tokenGenerator.GenerateToken().Returns("a-generated-jwt");
        var handler = CreateHandler(tokenGenerator);

        var result = await handler.Handle(new LoginCommand(CorrectPassword), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("a-generated-jwt");
    }

    [Fact]
    public async Task Handle_WithWrongPassword_ReturnsFailure()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new LoginCommand("definitely-not-it"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
    }

    [Fact]
    public async Task Handle_WithEmptyPassword_ReturnsFailureWithoutCallingBcrypt()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new LoginCommand(""), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
    }
}
```

- [ ] **Step 7: Run the new unit tests and confirm they pass**

Run: `dotnet test src/DzhusShelter.Api.Tests --filter LoginCommandHandlerTests`
Expected: PASS, 3/3.

- [ ] **Step 8: Write `AuthController` and register everything in `Program.cs`**

```csharp
// src/DzhusShelter.Api/Controllers/AuthController.cs
using DzhusShelter.Api.Application.Abstractions;
using DzhusShelter.Api.Application.Auth.Commands;
using DzhusShelter.Api.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DzhusShelter.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ICommandHandler<LoginCommand, Result<string>> _loginHandler;

    public AuthController(ICommandHandler<LoginCommand, Result<string>> loginHandler)
    {
        _loginHandler = loginHandler;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken cancellationToken)
    {
        var result = await _loginHandler.Handle(command, cancellationToken);
        return result.IsSuccess
            ? Ok(new { token = result.Value })
            : Unauthorized(new { error = "Invalid credentials." });
    }
}
```

`Program.cs` is top-level statements — every `using` directive must stay at the very top of the file, never inserted mid-file. Add these two lines to the **same top-of-file using block Task 1 Step 4 created**, in alphabetical position alongside the others already there:

```csharp
using DzhusShelter.Api.Application.Auth;
using DzhusShelter.Api.Application.Auth.Commands;
```

Then, in the body of `Program.cs`, add these registrations next to the existing `BadHabits` ones (same block as `AddScoped<IValidator<LogHabitEntryCommand>, ...>` etc. — this part is an ordinary statement, not a using directive, so it belongs with the rest of the service registrations, not at the top):

```csharp
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IValidator<LoginCommand>, LoginCommandValidator>();
builder.Services.AddScoped<ICommandHandler<LoginCommand, Result<string>>, LoginCommandHandler>();
```

- [ ] **Step 9: Write the integration test proving the full JWT round trip**

```csharp
// src/DzhusShelter.Api.IntegrationTests/AuthEndpointsTests.cs
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;

namespace DzhusShelter.Api.IntegrationTests;

public class AuthEndpointsTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public AuthEndpointsTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private sealed record LoginResponse(string Token);

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { password = "wrong" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithCorrectPassword_ReturnsATokenThatAuthorizesProtectedEndpoints()
    {
        using var client = _factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new { password = ApiWebApplicationFactory.AdminPassword });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        loginBody!.Token.Should().NotBeNullOrWhiteSpace();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody.Token);
        var protectedResponse = await client.GetAsync(
            $"/api/bad-habits/entries?from={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"))}" +
            $"&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"))}");
        protectedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

- [ ] **Step 10: Run the full test suite**

Run: `dotnet test DzhusShelter.slnx`
Expected: PASS, all tests including the two new `AuthEndpointsTests`.

- [ ] **Step 11: Commit**

```bash
git add src/DzhusShelter.Api/DzhusShelter.Api.csproj src/DzhusShelter.Api/Application/Auth/ src/DzhusShelter.Api/Infrastructure/Auth/JwtTokenGenerator.cs src/DzhusShelter.Api/Controllers/AuthController.cs src/DzhusShelter.Api/Program.cs src/DzhusShelter.Api.Tests/Infrastructure/Auth/ src/DzhusShelter.Api.Tests/Application/Auth/ src/DzhusShelter.Api.IntegrationTests/AuthEndpointsTests.cs
git commit -m "feat(api): add POST /api/auth/login issuing JWTs against a bcrypt-hashed password"
```

---

### Task 3: Deployment config — docker-compose, `.env.example`, and the CasaOS runbook

**Files:**
- Modify: `docker-compose.yml`
- Modify: `.env.example`
- Modify: `docs/casaos-runbook.md`

**Interfaces:**
- Consumes: the config key names fixed in this plan's Global Constraints table.
- Produces: the actual env vars Task 4 (bot) and Task 6 (manual verification) run against.

No automated test — this is pure configuration and documentation; Task 6 verifies it works end-to-end.

- [ ] **Step 1: Update `docker-compose.yml`**

Change the `ui` service's `ports:` entry:

```yaml
  ui:
    image: 1928374650810/dzhusshelter-ui:latest
    container_name: dzhusshelter-ui
    ports:
      # Bound to 127.0.0.1 only: the CasaOS host's existing Cloudflare Tunnel
      # (host network mode, used for Home Assistant) reaches this locally —
      # nothing on the LAN or internet can hit it directly except through
      # that tunnel. See docs/specs/public-access-and-auth.md.
      - "127.0.0.1:8082:8080"
    environment:
      - Api__BaseUrl=http://api:8080
    depends_on:
      - api
    restart: unless-stopped
```

Add a `ports:` mapping to the `api` service (replacing the "No ports mapping" comment) and the four new environment entries:

```yaml
  api:
    image: 1928374650810/dzhusshelter-api:latest
    container_name: dzhusshelter-api
    ports:
      # Bound to 127.0.0.1 only — same reasoning as ui above. Still never
      # reachable from the LAN/internet without going through the tunnel,
      # where auth (this file's Auth__*/Jwt__* env vars) is required.
      - "127.0.0.1:5279:8080"
    environment:
      - ConnectionStrings__Postgres=Host=postgres;Port=5432;Database=dzhusshelter;Username=postgres;Password=${POSTGRES_PASSWORD}
      - Jwt__SigningKey=${JWT_SIGNING_KEY}
      - Auth__AdminPasswordHash=${ADMIN_PASSWORD_HASH}
      - Auth__ApiKeyBot=${API_KEY_BOT}
      - Auth__ApiKeyShortcuts=${API_KEY_SHORTCUTS}
    depends_on:
      - postgres
    restart: unless-stopped
```

Add one environment entry to the `bot` service:

```yaml
  bot:
    image: 1928374650810/dzhusshelter-bot:latest
    container_name: dzhusshelter-bot
    environment:
      - Telegram__BotToken=${TELEGRAM_BOT_TOKEN}
      - Telegram__AllowedChatId=${TELEGRAM_ALLOWED_CHAT_ID}
      - Api__BaseUrl=http://api:8080
      - Api__ApiKey=${API_KEY_BOT}
    depends_on:
      - api
    restart: unless-stopped
```

- [ ] **Step 2: Update `.env.example`**

```
POSTGRES_PASSWORD=change-me
TELEGRAM_BOT_TOKEN=change-me
TELEGRAM_ALLOWED_CHAT_ID=change-me
JWT_SIGNING_KEY=change-me
ADMIN_PASSWORD_HASH=change-me
API_KEY_BOT=change-me
API_KEY_SHORTCUTS=change-me
```

- [ ] **Step 3: Update `docs/casaos-runbook.md`**

Replace the "Current ports" table:

```markdown
## Current ports (host machine)

| Service | Host port | Notes |
|---|---|---|
| `ui` | `127.0.0.1:8082` | Bound to localhost only — reachable via the existing Cloudflare Tunnel (host network mode), never directly from the LAN/internet |
| `api` | `127.0.0.1:5279` | Same as `ui` — localhost-only, protected by auth (JWT or API key) once reached through the tunnel |
| `postgres` | `5432` | Published for local-dev convenience, not a security requirement |
```

Replace the `.env` section's content list:

```markdown
## `.env` (secrets — never committed)

Lives at `~/dzhusshelter/.env`. Must contain exactly these seven lines, **no spaces around `=`, no quotes**:

```
POSTGRES_PASSWORD=...
TELEGRAM_BOT_TOKEN=...
TELEGRAM_ALLOWED_CHAT_ID=...
JWT_SIGNING_KEY=...
ADMIN_PASSWORD_HASH=...
API_KEY_BOT=...
API_KEY_SHORTCUTS=...
```

Generate `JWT_SIGNING_KEY`/`API_KEY_BOT`/`API_KEY_SHORTCUTS` as random strings, e.g.:
```bash
python3 -c "import secrets; print(secrets.token_urlsafe(32))"
```
Generate `ADMIN_PASSWORD_HASH` as a bcrypt hash of your chosen dashboard password — never store the plaintext:
```bash
pip install bcrypt
python3 -c "import bcrypt; print(bcrypt.hashpw(b'your-chosen-password', bcrypt.gensalt()).decode())"
```
```

Append a new section at the end of the file (after "Known Gotchas" if present, otherwise at the end):

```markdown
## Public access via Cloudflare Tunnel

`ui` and `api` are reachable from the internet through the CasaOS host's existing
`cloudflared` app (already used for Home Assistant, host network mode) — no second
tunnel container. Add two Public Hostname entries to that same tunnel (Cloudflare
Zero Trust dashboard → Networks → Tunnels → your tunnel → Public Hostname, or this
image's own web UI if it exposes the same setting):

| Public hostname | Service | Target |
|---|---|---|
| `dashboard.<your-domain>` | `ui` | `http://127.0.0.1:8082` |
| `api.<your-domain>` | `api` | `http://127.0.0.1:5279` |

### iOS Shortcuts: sending data to `api.<your-domain>`

In the Shortcuts app, on the "Get Contents of URL" (or "Get/Post Contents of URL")
action: open **Headers**, add a header named `X-Api-Key` with the value of this
deployment's `API_KEY_SHORTCUTS` secret. No other setup needed — this key is
separate from the bot's, so revoking/rotating one never affects the other.
```

- [ ] **Step 4: Commit**

```bash
git add docker-compose.yml .env.example docs/casaos-runbook.md
git commit -m "docs(deploy): document public access via the existing Cloudflare Tunnel, add auth env vars"
```

---

### Task 4: Bot sends its API key on every request

**Files:**
- Modify: `src/DzhusShelter.TelegramBot/Program.cs`

**Interfaces:**
- Consumes: `Api:ApiKey` config key (produced by Task 3's docker-compose change — `Api__ApiKey=${API_KEY_BOT}`), `ApiKeyAuthenticationHandler`'s expected header name (Task 1: `X-Api-Key`).

- [ ] **Step 1: Read the key and attach it as a default header**

In `src/DzhusShelter.TelegramBot/Program.cs`, add a required-config read next to the existing ones:

```csharp
var apiKey = builder.Configuration["Api:ApiKey"]
    ?? throw new InvalidOperationException("Api:ApiKey is not configured.");
```

Change the existing `AddHttpClient<BadHabitsApiClient>` registration:

```csharp
builder.Services.AddHttpClient<BadHabitsApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
});
```

- [ ] **Step 2: Confirm the bot project still builds**

Run: `dotnet build src/DzhusShelter.TelegramBot/DzhusShelter.TelegramBot.csproj`
Expected: builds successfully.

- [ ] **Step 3: Run the existing bot test suite to confirm nothing broke**

Run: `dotnet test src/DzhusShelter.TelegramBot.Tests`
Expected: PASS, same count as before this task (these tests exercise `BadHabitsApiClient`'s methods directly against a mocked handler, not `Program.cs`'s startup wiring — this step confirms that assumption held).

- [ ] **Step 4: Commit**

```bash
git add src/DzhusShelter.TelegramBot/Program.cs
git commit -m "feat(bot): send X-Api-Key on every Api request"
```

---

### Task 5: UI — login page, auth cookie, route guard, and authenticated Api calls

**Files:**
- Create: `src/DzhusShelter.UI/Services/AuthCookieService.cs`
- Create: `src/DzhusShelter.UI/Services/AuthApiClient.cs`
- Create: `src/DzhusShelter.UI/Services/AuthHeaderHandler.cs`
- Create: `src/DzhusShelter.UI/Components/Pages/Login.razor`
- Modify: `src/DzhusShelter.UI/Program.cs`
- Modify: `src/DzhusShelter.UI/Components/Layout/MainLayout.razor`
- Modify: `src/DzhusShelter.UI/Components/Layout/NavMenu.razor`

**Interfaces:**
- Consumes: `POST /api/auth/login` (Task 2), the `Authorization: Bearer` scheme the Api's JWT handler validates (Task 1).
- No automated tests (this repo's standing convention — see Global Constraints); Task 6 verifies this manually in a browser.

- [ ] **Step 1: Write the cookie service — the single source of truth for the cookie's name and options**

```csharp
// src/DzhusShelter.UI/Services/AuthCookieService.cs
namespace DzhusShelter.UI.Services;

public sealed class AuthCookieService
{
    private const string CookieName = "dzhus_auth_token";

    public string? GetToken(HttpContext? httpContext)
    {
        if (httpContext is null)
            return null;
        return httpContext.Request.Cookies.TryGetValue(CookieName, out var token) ? token : null;
    }

    public void SetToken(HttpContext httpContext, string token)
    {
        httpContext.Response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            // Reflects whether *this* request arrived over HTTPS. Behind the Cloudflare
            // Tunnel, that's only accurate because Program.cs's ForwardedHeaders
            // middleware (added for the Api project in Task 1; add the same for this UI
            // project below) restores the original scheme — otherwise Kestrel only ever
            // sees plain HTTP from the tunnel and this would always evaluate to false.
            Secure = httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7),
        });
    }

    public void ClearToken(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Delete(CookieName);
    }
}
```

- [ ] **Step 2: Write the auth API client (login only — no bearer token to attach yet at this point)**

```csharp
// src/DzhusShelter.UI/Services/AuthApiClient.cs
using System.Net.Http.Json;

namespace DzhusShelter.UI.Services;

public sealed class AuthApiClient
{
    private readonly HttpClient _httpClient;

    public AuthApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private sealed record LoginRequest(string Password);
    private sealed record LoginResponse(string Token);

    public async Task<string?> LoginAsync(string password, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/auth/login", new LoginRequest(password), cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: cancellationToken);
        return body?.Token;
    }
}
```

- [ ] **Step 3: Write the delegating handler that attaches the bearer token to every `BadHabitsApiClient` call**

```csharp
// src/DzhusShelter.UI/Services/AuthHeaderHandler.cs
using System.Net.Http.Headers;

namespace DzhusShelter.UI.Services;

public sealed class AuthHeaderHandler : DelegatingHandler
{
    private readonly AuthCookieService _cookieService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthHeaderHandler(AuthCookieService cookieService, IHttpContextAccessor httpContextAccessor)
    {
        _cookieService = cookieService;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = _cookieService.GetToken(_httpContextAccessor.HttpContext);
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return base.SendAsync(request, cancellationToken);
    }
}
```

- [ ] **Step 4: Register everything in `Program.cs`**

```csharp
// src/DzhusShelter.UI/Program.cs
using ApexCharts;
using DzhusShelter.UI.Components;
using DzhusShelter.UI.Services;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<AuthCookieService>();
builder.Services.AddScoped<AuthHeaderHandler>();

builder.Services.AddHttpClient<AuthApiClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"]
        ?? throw new InvalidOperationException("Api:BaseUrl is not configured.")));

builder.Services.AddHttpClient<BadHabitsApiClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"]
        ?? throw new InvalidOperationException("Api:BaseUrl is not configured.")))
    .AddHttpMessageHandler<AuthHeaderHandler>();

builder.Services.AddApexCharts();

var app = builder.Build();

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
```

`AuthCookieService` is stateless (just reads/writes whatever `HttpContext` it's handed) so `AddSingleton` is correct; `AuthHeaderHandler` is `AddScoped` because it captures `IHttpContextAccessor`, which is per-request.

- [ ] **Step 5: Write the login page**

```razor
@* src/DzhusShelter.UI/Components/Pages/Login.razor *@
@page "/login"
@using DzhusShelter.UI.Services
@inject AuthApiClient AuthApiClient
@inject AuthCookieService AuthCookieService
@inject IHttpContextAccessor HttpContextAccessor
@inject NavigationManager NavigationManager

<PageTitle>Вхід — Dzhus Shelter</PageTitle>

<div class="login-container">
    <EditForm Model="_model" OnValidSubmit="HandleLoginAsync" FormName="login">
        <DataAnnotationsValidator />
        <div class="login-field">
            <label for="password">Пароль</label>
            <InputText id="password" type="password" class="nes-input" @bind-Value="_model.Password" />
        </div>
        @if (_error is not null)
        {
            <p class="login-error">@_error</p>
        }
        <button type="submit" class="nes-btn is-primary">Увійти</button>
    </EditForm>
</div>

<style>
    .login-container {
        display: flex;
        justify-content: center;
        margin-top: 4rem;
    }

    .login-field {
        margin-bottom: 1rem;
    }

    .login-error {
        color: #e76e55;
    }
</style>

@code {
    private readonly LoginModel _model = new();
    private string? _error;

    private sealed class LoginModel
    {
        public string Password { get; set; } = string.Empty;
    }

    private async Task HandleLoginAsync()
    {
        var token = await AuthApiClient.LoginAsync(_model.Password, CancellationToken.None);
        if (token is null)
        {
            _error = "Невірний пароль.";
            return;
        }

        var httpContext = HttpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("Login must be handled during a static (non-interactive) request.");
        AuthCookieService.SetToken(httpContext, token);

        // forceLoad: a fresh full-page request, so the browser is guaranteed to send the
        // cookie just set above on the very next navigation.
        NavigationManager.NavigateTo("/", forceLoad: true);
    }
}
```

> **Note for the implementer:** `Login.razor` must NOT declare `@rendermode InteractiveServer` and must not use `[StreamRendering]` — `HttpContextAccessor.HttpContext` is only non-null during the initial static (server-rendered) request; once a component becomes interactive over its SignalR circuit, `HttpContext` is `null` and setting a cookie is no longer possible. Leaving this page as plain static SSR (the default, like `Home.razor`) is required, not incidental.

- [ ] **Step 6: Add the route guard to `MainLayout.razor`**

```razor
@* src/DzhusShelter.UI/Components/Layout/MainLayout.razor *@
@inherits LayoutComponentBase
@inject DzhusShelter.UI.Services.AuthCookieService AuthCookieService
@inject IHttpContextAccessor HttpContextAccessor
@inject NavigationManager NavigationManager

<div class="page">
    <div class="sidebar">
        <NavMenu />
    </div>

    <main>
        <div class="top-row px-4">
            <a href="https://learn.microsoft.com/aspnet/core/" target="_blank">About</a>
        </div>

        <article class="content px-4">
            @Body
        </article>
    </main>
</div>

<div id="blazor-error-ui" data-nosnippet>
    An unhandled error has occurred.
    <a href="." class="reload">Reload</a>
    <span class="dismiss">🗙</span>
</div>

@code {
    protected override void OnInitialized()
    {
        var httpContext = HttpContextAccessor.HttpContext;
        // Null means we're past the initial static render, already inside an interactive
        // circuit for a page that already passed this same check during that first pass —
        // nothing to re-verify, and there's no HttpContext left to redirect with anyway.
        if (httpContext is null)
            return;

        var isLoginPage = httpContext.Request.Path.StartsWithSegments("/login");
        var token = AuthCookieService.GetToken(httpContext);

        if (token is null && !isLoginPage)
            NavigationManager.NavigateTo("/login", forceLoad: true);
    }
}
```

> **Note for the implementer:** this only checks that a token *cookie is present* — it does not validate the JWT's signature or expiry client-side (that's the Api's job on every actual data request; an expired/invalid token just means every `BadHabitsApiClient` call starts returning 401, which the existing pages don't yet handle gracefully). Handling an expired-token 401 by redirecting back to `/login` is a reasonable follow-up but is out of scope for this task — noted, not forgotten.

- [ ] **Step 7: Add a logout link to `NavMenu.razor`**

Add this as the last `nav-item` in `src/DzhusShelter.UI/Components/Layout/NavMenu.razor`, right before the closing `</nav>`:

```razor
        <div class="nav-item px-3">
            <a class="nav-link" href="logout">
                🚪 <span class="ms-2">Вийти</span>
            </a>
        </div>
```

- [ ] **Step 8: Add the logout endpoint**

Blazor components can't clear a cookie and redirect in one static render pass as cleanly as a plain endpoint can — add a minimal minimal-API endpoint instead of a `.razor` page. In `Program.cs`, right after `app.MapStaticAssets();`:

```csharp
app.MapGet("/logout", (HttpContext httpContext, DzhusShelter.UI.Services.AuthCookieService cookieService) =>
{
    cookieService.ClearToken(httpContext);
    return Results.Redirect("/login");
});
```

- [ ] **Step 9: Build the UI project**

Run: `dotnet build src/DzhusShelter.UI/DzhusShelter.UI.csproj`
Expected: builds successfully, 0 errors.

- [ ] **Step 10: Commit**

```bash
git add src/DzhusShelter.UI/Services/ src/DzhusShelter.UI/Components/Pages/Login.razor src/DzhusShelter.UI/Program.cs src/DzhusShelter.UI/Components/Layout/MainLayout.razor src/DzhusShelter.UI/Components/Layout/NavMenu.razor
git commit -m "feat(ui): add login page, auth cookie, route guard, and authenticated Api calls"
```

---

### Task 6: End-to-end manual verification, docs status update

**Files:**
- Modify: `docs/specs/public-access-and-auth.md` (status → Implemented)
- Modify: `docs/specs/README.md` (cross-cutting specs table)
- Modify: `docs/architecture.md` (Security section — describe the shape actually shipped)
- Modify: `docs/roadmap.md` (progress log — topic 12 now genuinely exercised via real code)

**Interfaces:**
- Consumes: everything from Tasks 1-5, wired together for the first time as a full stack.

No new automated tests — this task is the manual verification pass this plan's Global Constraints call for, plus the doc updates that follow a clean pass (matching how prior plans in this repo close out).

- [ ] **Step 1: Generate real local test secrets**

```bash
python3 -c "import secrets; print(secrets.token_urlsafe(32))"   # run 3 times: JWT_SIGNING_KEY, API_KEY_BOT, API_KEY_SHORTCUTS
python3 -c "import bcrypt; print(bcrypt.hashpw(b'local-test-password', bcrypt.gensalt()).decode())"   # ADMIN_PASSWORD_HASH
```

Write a local `.env` (git-ignored, never committed) with all seven vars from `.env.example`, using these generated values and `local-test-password` as the password you'll actually type into the login form.

- [ ] **Step 2: Bring up the full stack**

```bash
docker compose up --build
```

Expected: all four containers (`postgres`, `api`, `ui`, `bot`) start without errors. Check `docker compose logs api` — no startup exception about missing `Jwt:SigningKey`/`Auth:*` config.

- [ ] **Step 3: Verify the Api rejects anonymous requests**

```bash
curl -i http://127.0.0.1:5279/api/bad-habits/entries
```

Expected: `401 Unauthorized`.

- [ ] **Step 4: Verify login and the API-key path work from the command line**

```bash
curl -i -X POST http://127.0.0.1:5279/api/auth/login -H "Content-Type: application/json" -d '{"password":"wrong"}'
# Expected: 401

curl -i -X POST http://127.0.0.1:5279/api/auth/login -H "Content-Type: application/json" -d '{"password":"local-test-password"}'
# Expected: 200, body has a "token" field — copy it for the next check

curl -i http://127.0.0.1:5279/api/bad-habits/entries -H "Authorization: Bearer <token from above>"
# Expected: 200

curl -i http://127.0.0.1:5279/api/bad-habits/entries -H "X-Api-Key: <your API_KEY_SHORTCUTS value>"
# Expected: 200 — this is the exact request Shortcuts will make
```

- [ ] **Step 5: Verify the UI login flow in a browser**

Open `http://127.0.0.1:8082/`. Expected: immediately redirected to `/login` (no cookie yet). Enter the wrong password → see the Ukrainian error message, stay on `/login`. Enter `local-test-password` → redirected to `/`, dashboard renders normally, `/bad-habits` loads real data (proving `BadHabitsApiClient`'s calls now carry a working bearer token via `AuthHeaderHandler`). Click "Вийти" → redirected to `/login`; reloading `/` redirects back to `/login` again (cookie is gone).

- [ ] **Step 6: Verify the bot still works**

Log a habit entry through the actual Telegram bot (or, if a live chat isn't convenient right now, confirm via `docker compose logs bot` that it starts without an `Api:ApiKey is not configured` exception, and via `docker compose logs api` that its request arrives as `200`, not `401`).

- [ ] **Step 7: Tear down**

```bash
docker compose down
```

Remove the local `.env` if it was created solely for this check and isn't the one you intend to keep using for further local dev.

- [ ] **Step 8: Update docs now that the spec is fully implemented**

In `docs/specs/public-access-and-auth.md`, change the header line's status from `Spec'd` to `Implemented`.

In `docs/specs/README.md`, change the cross-cutting specs table's status for `public-access-and-auth.md` from `Spec'd` to `Implemented`.

In `docs/architecture.md`, replace the "**Update:** that public-facing use case has arrived..." paragraph added during brainstorming with a description of the shape actually shipped, e.g.:

```markdown
**Update (implemented):** `Api` and `UI` are now reachable from the internet through the
CasaOS host's Cloudflare Tunnel. Every `Api` endpoint requires either a JWT (issued by
`POST /api/auth/login` against a bcrypt-hashed admin password, used by the Blazor UI) or a
per-client API key (`X-Api-Key`, used by the bot and by iOS Shortcuts) — see
[docs/specs/public-access-and-auth.md](specs/public-access-and-auth.md) for the full design.
`Api`'s port is still never published beyond `127.0.0.1` on the host; the tunnel is the only
way in from outside.
```

In `docs/roadmap.md`, update the progress log for roadmap topic 12 (Auth) to note it's been genuinely exercised: JWT bearer auth + a custom API-key scheme, both with real xUnit/integration test coverage, are now live in `DzhusShelter.Api`.

- [ ] **Step 9: Commit**

```bash
git add docs/specs/public-access-and-auth.md docs/specs/README.md docs/architecture.md docs/roadmap.md
git commit -m "docs: mark public-access-and-auth as Implemented, update architecture.md and roadmap.md"
```
