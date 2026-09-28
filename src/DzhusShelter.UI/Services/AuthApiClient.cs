using System.Net.Http.Json;

namespace DzhusShelter.UI.Services;

public sealed class AuthApiClient
{
    private readonly HttpClient _httpClient;

    public AuthApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private sealed record LoginRequest(string Password);
    private sealed record LoginResponse(string Token);

    public async Task<string?> LoginAsync(string password, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/auth/login", new LoginRequest(password), cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: cancellationToken);
        return body?.Token;
    }
}
