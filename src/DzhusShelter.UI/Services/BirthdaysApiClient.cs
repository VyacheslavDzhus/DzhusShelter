using System.Text.Json;
using System.Text.Json.Serialization;

namespace DzhusShelter.UI.Services;

public sealed record BirthdayDto(
    Guid Id, string Name, int Day, int Month, int? Year, DateOnly NextOccurrence, int DaysUntil, int? TurningAge);

public sealed record BirthdayInput(string Name, int Day, int Month, int? Year);

public sealed class BirthdaysApiClient
{
    public const string GenericError = "Не вдалося зберегти. Перевір дані.";
    public const string ConnectionError = "Немає зв'язку з сервером. Спробуй ще раз.";
    public const string ServerError = "Сервер не зміг обробити запит. Спробуй пізніше.";
    public const string SessionExpiredError = "Сесія завершилась — увійди знову.";

    // Failures that mean "the call did not complete" (network, timeout, unreadable body) rather
    // than "the Api rejected the input"; pages catch exactly these to keep the circuit alive.
    public static bool IsTransportFailure(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException or JsonException;

    // Same options as BadHabitsApiClient: without case-insensitivity and the string-enum
    // converter, deserialization silently yields default values instead of throwing.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _httpClient;

    public BirthdaysApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<BirthdayDto>> GetAllAsync(CancellationToken ct)
    {
        var items = await _httpClient.GetFromJsonAsync<List<BirthdayDto>>("/api/birthdays", JsonOptions, ct);
        return items ?? [];
    }

    // The mutating calls return null on success, otherwise the error text to show the user.

    public async Task<string?> CreateAsync(BirthdayInput input, CancellationToken ct)
    {
        using var response = await _httpClient.PostAsJsonAsync("/api/birthdays", input, JsonOptions, ct);
        return await ToErrorAsync(response, ct);
    }

    public async Task<string?> UpdateAsync(Guid id, BirthdayInput input, CancellationToken ct)
    {
        using var response = await _httpClient.PutAsJsonAsync($"/api/birthdays/{id}", input, JsonOptions, ct);
        return await ToErrorAsync(response, ct);
    }

    public async Task<string?> DeleteAsync(Guid id, CancellationToken ct)
    {
        using var response = await _httpClient.DeleteAsync($"/api/birthdays/{id}", ct);
        return await ToErrorAsync(response, ct);
    }

    private static async Task<string?> ToErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return null;

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return SessionExpiredError;

        // Api failures are { "error": "..." }; malformed bodies come back as ProblemDetails without it.
        try
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
            if (body.ValueKind == JsonValueKind.Object
                && body.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(error.GetString()))
            {
                return error.GetString();
            }
        }
        catch (JsonException)
        {
        }
        catch (NotSupportedException)
        {
        }

        // "Check your data" only makes sense for a validation rejection (400), not for 403/404/5xx.
        return response.StatusCode == System.Net.HttpStatusCode.BadRequest ? GenericError : ServerError;
    }
}
