using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace DzhusShelter.Api.IntegrationTests;

/// <summary>
/// The "Access per client" matrix from docs/specs/birthdays.md, cell by cell — a security
/// requirement (least privilege per client), so every rejected cell is asserted too, not
/// just the happy paths.
/// </summary>
public class BirthdaysAccessTests : IClassFixture<ApiWebApplicationFactory>
{
    private const string Ui = "ui-jwt";
    private const string Bot = "bot";
    private const string Shortcuts = "shortcuts";
    private const string MainBot = "main-bot";

    private const string ListBirthdays = "GET /api/birthdays";
    private const string CreateBirthday = "POST /api/birthdays";
    private const string PendingNotifications = "GET /api/birthdays/notifications/pending";
    private const string MarkNotificationSent = "POST /api/birthdays/notifications/sent";
    private const string BadHabitsEntries = "GET /api/bad-habits/entries";

    private readonly ApiWebApplicationFactory _factory;

    public BirthdaysAccessTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    // Birthdays CRUD: UI only.
    [InlineData(Ui, ListBirthdays, true)]
    [InlineData(Bot, ListBirthdays, false)]
    [InlineData(Shortcuts, ListBirthdays, false)]
    [InlineData(MainBot, ListBirthdays, false)]
    [InlineData(Ui, CreateBirthday, true)]
    [InlineData(Bot, CreateBirthday, false)]
    [InlineData(Shortcuts, CreateBirthday, false)]
    [InlineData(MainBot, CreateBirthday, false)]
    // Birthday notifications: main bot only.
    [InlineData(Ui, PendingNotifications, false)]
    [InlineData(Bot, PendingNotifications, false)]
    [InlineData(Shortcuts, PendingNotifications, false)]
    [InlineData(MainBot, PendingNotifications, true)]
    [InlineData(Ui, MarkNotificationSent, false)]
    [InlineData(Bot, MarkNotificationSent, false)]
    [InlineData(Shortcuts, MarkNotificationSent, false)]
    [InlineData(MainBot, MarkNotificationSent, true)]
    // Everything else (Bad Habits): unchanged for existing clients, closed to the main bot.
    [InlineData(Ui, BadHabitsEntries, true)]
    [InlineData(Bot, BadHabitsEntries, true)]
    [InlineData(Shortcuts, BadHabitsEntries, true)]
    [InlineData(MainBot, BadHabitsEntries, false)]
    public async Task Client_HasExactlyTheAccessTheSpecGrants(string clientName, string endpoint, bool allowed)
    {
        using var client = await CreateClientAsync(clientName);

        using var response = await client.SendAsync(CreateRequest(endpoint));

        if (allowed)
            response.IsSuccessStatusCode.Should().BeTrue($"{clientName} must be allowed to call {endpoint}, got {response.StatusCode}");
        else
            response.StatusCode.Should().BeOneOf([HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden],
                $"{clientName} must be rejected from {endpoint}");
    }

    private Task<HttpClient> CreateClientAsync(string clientName) => clientName switch
    {
        Ui => _factory.CreateUiClientAsync(),
        Bot => Task.FromResult(_factory.CreateApiKeyClient(ApiWebApplicationFactory.BotApiKey)),
        Shortcuts => Task.FromResult(_factory.CreateApiKeyClient(ApiWebApplicationFactory.ShortcutsApiKey)),
        MainBot => Task.FromResult(_factory.CreateApiKeyClient(ApiWebApplicationFactory.MainBotApiKey)),
        _ => throw new ArgumentOutOfRangeException(nameof(clientName), clientName, null),
    };

    // Every request is valid on its own, so an allowed cell can only fail on authorization.
    private static HttpRequestMessage CreateRequest(string endpoint) => endpoint switch
    {
        ListBirthdays => new HttpRequestMessage(HttpMethod.Get, "/api/birthdays"),
        CreateBirthday => new HttpRequestMessage(HttpMethod.Post, "/api/birthdays")
        {
            Content = JsonContent.Create(new { name = "Перевірка доступу", day = 1, month = 1, year = (int?)null }),
        },
        PendingNotifications => new HttpRequestMessage(HttpMethod.Get, "/api/birthdays/notifications/pending"),
        MarkNotificationSent => new HttpRequestMessage(HttpMethod.Post, "/api/birthdays/notifications/sent")
        {
            Content = JsonContent.Create(new { kind = "Today", forDate = "2026-11-12" }),
        },
        BadHabitsEntries => new HttpRequestMessage(HttpMethod.Get,
            "/api/bad-habits/entries?from=2026-11-01T00%3A00%3A00Z&to=2026-11-02T00%3A00%3A00Z"),
        _ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, null),
    };
}
