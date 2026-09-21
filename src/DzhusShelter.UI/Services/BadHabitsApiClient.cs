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
