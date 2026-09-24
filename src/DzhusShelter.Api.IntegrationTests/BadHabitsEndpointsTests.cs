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
