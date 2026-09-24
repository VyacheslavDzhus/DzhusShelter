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

        LogHabitEntryResponse? body;
        try
        {
            body = await response.Content.ReadFromJsonAsync<LogHabitEntryResponse>(ResponseJsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            // A 2xx status with an empty, truncated, or shape-mismatched body (proxy hiccup, API
            // bug, misconfigured upstream) should degrade to Failed, not crash the caller.
            return LogEntryOutcome.Failed;
        }

        return body is { AlreadyLogged: true } ? LogEntryOutcome.AlreadyLogged : LogEntryOutcome.Logged;
    }
}
