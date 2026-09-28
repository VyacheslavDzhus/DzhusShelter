using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;

namespace DzhusShelter.Api.IntegrationTests;

public class AuthEndpointsTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public AuthEndpointsTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private sealed record LoginResponse(string Token);

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { password = "wrong" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithCorrectPassword_ReturnsATokenThatAuthorizesProtectedEndpoints()
    {
        using var client = _factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new { password = ApiWebApplicationFactory.AdminPassword });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        loginBody!.Token.Should().NotBeNullOrWhiteSpace();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody.Token);
        var protectedResponse = await client.GetAsync(
            $"/api/bad-habits/entries?from={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"))}" +
            $"&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"))}");
        protectedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
