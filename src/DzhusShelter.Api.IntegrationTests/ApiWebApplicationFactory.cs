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

    // Real bcrypt hash of AdminPassword ("test-admin-password"), generated via:
    //   python -c "import bcrypt; print(bcrypt.hashpw(b'test-admin-password', bcrypt.gensalt()).decode())"
    public const string AdminPasswordHash = "$2b$12$GS4ulkH73L1HKVZTQCeM9eI7qbn/p.9zuaTiWCD6.2Dmn4OY3AY5m";

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
