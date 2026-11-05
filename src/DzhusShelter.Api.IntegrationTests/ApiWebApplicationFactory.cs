using System.Net.Http.Headers;
using System.Net.Http.Json;
using DzhusShelter.Api.Application.Birthdays;
using DzhusShelter.Api.Infrastructure;
using DzhusShelter.Api.Infrastructure.Birthdays;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace DzhusShelter.Api.IntegrationTests;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string BotApiKey = "test-bot-key";
    public const string ShortcutsApiKey = "test-shortcuts-key";
    public const string MainBotApiKey = "test-main-bot-key";
    public const string AdminPassword = "test-admin-password";

    // Real bcrypt hash of AdminPassword ("test-admin-password"), generated via:
    //   python -c "import bcrypt; print(bcrypt.hashpw(b'test-admin-password', bcrypt.gensalt()).decode())"
    public const string AdminPasswordHash = "$2b$12$GS4ulkH73L1HKVZTQCeM9eI7qbn/p.9zuaTiWCD6.2Dmn4OY3AY5m";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    // Logged in at most once per factory: the login endpoint is rate-limited (5/min per IP,
    // and every TestServer request shares one "unknown" partition), so logging in per test
    // would start returning 429 as soon as a class has more than a handful of UI tests.
    private readonly Lazy<Task<string>> _uiToken;

    public ApiWebApplicationFactory()
    {
        // Program.cs reads Jwt:SigningKey eagerly, before WebApplicationFactory's Build()
        // interception point runs — so a ConfigureAppConfiguration in-memory collection
        // (spliced in only at that interception point) is too late to be seen by it. Only
        // genuinely early sources, like environment variables (loaded by
        // WebApplication.CreateBuilder(args) at the very start), are visible in time.
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "test-signing-key-at-least-32-bytes-long!");
        Environment.SetEnvironmentVariable("Auth__AdminPasswordHash", AdminPasswordHash);
        Environment.SetEnvironmentVariable("Auth__ApiKeyBot", BotApiKey);
        Environment.SetEnvironmentVariable("Auth__ApiKeyShortcuts", ShortcutsApiKey);
        Environment.SetEnvironmentVariable("Auth__ApiKeyMainBot", MainBotApiKey);

        _uiToken = new Lazy<Task<string>>(LoginAsync);
    }

    /// <summary>
    /// Drives only the birthday clock (Kyiv "today" / 09:00) — JWT issuing and validation keep
    /// the real TimeProvider, so moving this never expires or pre-dates a token.
    /// Use AdjustTime (not SetUtcNow) to move it backwards between tests.
    /// </summary>
    public FakeTimeProvider BirthdayTime { get; } = new(new DateTimeOffset(2026, 11, 12, 7, 30, 0, TimeSpan.Zero));

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public async Task<HttpClient> CreateUiClientAsync()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await _uiToken.Value);
        return client;
    }

    public HttpClient CreateApiKeyClient(string key)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", key);
        return client;
    }

    private async Task<string> LoginAsync()
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { password = AdminPassword });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.Token;
    }

    private sealed record LoginResponse(string Token);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.Single(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            services.Remove(descriptor);
            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(_postgres.GetConnectionString()));

            services.RemoveAll<IBirthdayClock>();
            services.AddSingleton<IBirthdayClock>(
                new KyivBirthdayClock(BirthdayTime, Options.Create(new BirthdaysOptions())));
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}
