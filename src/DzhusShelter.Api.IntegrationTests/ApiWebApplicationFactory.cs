using DzhusShelter.Api.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace DzhusShelter.Api.IntegrationTests;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string BotApiKey = "test-bot-key";
    public const string ShortcutsApiKey = "test-shortcuts-key";
    public const string AdminPassword = "test-admin-password";

    // Real bcrypt hash of AdminPassword ("test-admin-password"), generated via:
    //   python -c "import bcrypt; print(bcrypt.hashpw(b'test-admin-password', bcrypt.gensalt()).decode())"
    public const string AdminPasswordHash = "$2b$12$GS4ulkH73L1HKVZTQCeM9eI7qbn/p.9zuaTiWCD6.2Dmn4OY3AY5m";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

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
    }

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
