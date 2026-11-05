using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DzhusShelter.Api.Application.Birthdays.Dtos;
using DzhusShelter.Api.Domain.Birthdays;
using FluentAssertions;

namespace DzhusShelter.Api.IntegrationTests;

public class BirthdaysEndpointsTests : IClassFixture<ApiWebApplicationFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    // 09:30 in Kyiv (UTC+2 in November) on 2026-11-12 — past the 09:00 notify time.
    private static readonly DateTimeOffset Nov12AfterNine = new(2026, 11, 12, 7, 30, 0, TimeSpan.Zero);

    private readonly ApiWebApplicationFactory _factory;
    private HttpClient _ui = null!;
    private HttpClient _mainBot = null!;

    public BirthdaysEndpointsTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private sealed record CreatedResponse(Guid Id);

    private sealed record ErrorResponse(string Error);

    public async Task InitializeAsync()
    {
        // Tests in this class share one Postgres and one fake clock: reset both.
        // AdjustTime, not SetUtcNow — the latter refuses to move backwards.
        _factory.BirthdayTime.AdjustTime(Nov12AfterNine);
        _ui = await _factory.CreateUiClientAsync();
        _mainBot = _factory.CreateApiKeyClient(ApiWebApplicationFactory.MainBotApiKey);

        foreach (var birthday in await GetBirthdaysAsync())
            (await _ui.DeleteAsync($"/api/birthdays/{birthday.Id}")).EnsureSuccessStatusCode();
    }

    public Task DisposeAsync()
    {
        _ui.Dispose();
        _mainBot.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Crud_RoundTrip()
    {
        var create = await _ui.PostAsJsonAsync("/api/birthdays",
            new { name = "Саша Пупкін", day = 12, month = 11, year = 2001 });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await create.Content.ReadFromJsonAsync<CreatedResponse>(JsonOptions))!.Id;

        var afterCreate = await GetBirthdaysAsync();
        afterCreate.Should().ContainSingle();
        afterCreate[0].Id.Should().Be(id);
        afterCreate[0].Name.Should().Be("Саша Пупкін");
        afterCreate[0].DaysUntil.Should().Be(0);
        afterCreate[0].TurningAge.Should().Be(25);

        var update = await _ui.PutAsJsonAsync($"/api/birthdays/{id}",
            new { name = "Саша Пупкін", day = 12, month = 11, year = (int?)null });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterUpdate = await GetBirthdaysAsync();
        afterUpdate.Should().ContainSingle().Which.TurningAge.Should().BeNull();

        var delete = await _ui.DeleteAsync($"/api/birthdays/{id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await GetBirthdaysAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Create_InvalidDate_Returns400WithError()
    {
        var response = await _ui.PostAsJsonAsync("/api/birthdays",
            new { name = "Квітень", day = 31, month = 4, year = (int?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        body!.Error.Should().Be(BirthdayErrors.InvalidDate.Message);
    }

    [Fact]
    public async Task UpdateAndDelete_UnknownId_Return404()
    {
        var unknownId = Guid.NewGuid();

        var update = await _ui.PutAsJsonAsync($"/api/birthdays/{unknownId}",
            new { name = "Ніхто", day = 1, month = 1, year = (int?)null });
        var delete = await _ui.DeleteAsync($"/api/birthdays/{unknownId}");

        update.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await update.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions))!.Error.Should().Contain(unknownId.ToString());
        delete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Pending_ThenMarkSent_ThenOnlyTomorrowLeft()
    {
        await CreateAsync("Саша", 12, 11, 2001);
        await CreateAsync("Оля", 13, 11, null);

        var pending = await GetPendingAsync();
        pending.Should().HaveCount(2);
        pending[0].Kind.Should().Be(BirthdayNotificationKind.Today);
        pending[0].ForDate.Should().Be(new DateOnly(2026, 11, 12));
        pending[0].People.Should().Equal(new BirthdayPersonDto("Саша", 25));
        pending[1].Kind.Should().Be(BirthdayNotificationKind.Tomorrow);
        pending[1].ForDate.Should().Be(new DateOnly(2026, 11, 12));
        pending[1].People.Should().Equal(new BirthdayPersonDto("Оля", null));

        var markSent = new { kind = "Today", forDate = "2026-11-12" };
        (await _mainBot.PostAsJsonAsync("/api/birthdays/notifications/sent", markSent))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterMark = await GetPendingAsync();
        afterMark.Should().ContainSingle().Which.Kind.Should().Be(BirthdayNotificationKind.Tomorrow);

        // Idempotent: the at-least-once main bot may report the same reminder twice.
        (await _mainBot.PostAsJsonAsync("/api/birthdays/notifications/sent", markSent))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Pending_BeforeNineKyiv_IsEmpty()
    {
        // Оля's Tomorrow reminder is never marked sent in this class, so the only reason for
        // an empty list below is the 09:00 gate — the 07:00Z check proves it isn't vacuous.
        await CreateAsync("Оля", 13, 11, null);

        _factory.BirthdayTime.AdjustTime(new DateTimeOffset(2026, 11, 12, 6, 59, 0, TimeSpan.Zero));
        (await GetPendingAsync()).Should().BeEmpty();

        _factory.BirthdayTime.AdjustTime(new DateTimeOffset(2026, 11, 12, 7, 0, 0, TimeSpan.Zero));
        (await GetPendingAsync()).Should().ContainSingle().Which.Kind.Should().Be(BirthdayNotificationKind.Tomorrow);
    }

    private async Task CreateAsync(string name, int day, int month, int? year)
    {
        var response = await _ui.PostAsJsonAsync("/api/birthdays", new { name, day, month, year });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private async Task<List<BirthdayDto>> GetBirthdaysAsync()
    {
        var response = await _ui.GetAsync("/api/birthdays");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<BirthdayDto>>(JsonOptions))!;
    }

    private async Task<List<PendingBirthdayNotificationDto>> GetPendingAsync()
    {
        var response = await _mainBot.GetAsync("/api/birthdays/notifications/pending");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<PendingBirthdayNotificationDto>>(JsonOptions))!;
    }
}
