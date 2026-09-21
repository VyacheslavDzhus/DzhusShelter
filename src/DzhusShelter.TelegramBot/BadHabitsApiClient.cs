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
