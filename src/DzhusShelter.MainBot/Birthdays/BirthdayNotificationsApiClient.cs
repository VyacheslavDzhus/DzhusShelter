using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DzhusShelter.MainBot.Birthdays;

public sealed class BirthdayNotificationsApiClient(HttpClient http) : IBirthdayNotificationsApi
{
    // Web defaults = camelCase + case-insensitive reading; the enum converter reads/writes "Today" instead of 1.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<IReadOnlyList<PendingBirthdayNotification>> GetPendingAsync(CancellationToken ct)
    {
        using var response = await http.GetAsync("api/birthdays/notifications/pending", ct);
        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<List<PendingBirthdayNotification>>(Json, ct);
        return items ?? [];
    }

    public async Task MarkSentAsync(BirthdayNotificationKind kind, DateOnly forDate, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync("api/birthdays/notifications/sent", new { kind, forDate }, Json, ct);
        response.EnsureSuccessStatusCode();
    }
}
