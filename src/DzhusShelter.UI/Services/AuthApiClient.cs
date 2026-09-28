using System.Net;
using System.Net.Http.Json;

namespace DzhusShelter.UI.Services;

public enum LoginOutcome
{
    Success,
    InvalidCredentials,
    TooManyAttempts,
}

public sealed record LoginResult(LoginOutcome Outcome, string? Token);

public sealed class AuthApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthApiClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
    }

    private sealed record LoginRequest(string Password);
    private sealed record LoginResponseBody(string Token);

    public async Task<LoginResult> LoginAsync(string password, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest(password)),
        };

        // The Api's login rate limiter partitions by client IP — without this, every
        // dashboard login looks like it comes from this UI container's own Docker IP,
        // sharing one bucket across every real visitor. Program.cs's own
        // ForwardedHeaders middleware already restored the true browser IP into
        // HttpContext for THIS (UI-side) request; forward it onward so the Api's
        // ForwardedHeaders middleware can do the same on its end.
        var clientIp = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrEmpty(clientIp))
            request.Headers.Add("X-Forwarded-For", clientIp);

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            return new LoginResult(LoginOutcome.TooManyAttempts, null);

        if (!response.IsSuccessStatusCode)
            return new LoginResult(LoginOutcome.InvalidCredentials, null);

        var body = await response.Content.ReadFromJsonAsync<LoginResponseBody>(cancellationToken: cancellationToken);
        return new LoginResult(LoginOutcome.Success, body?.Token);
    }
}
