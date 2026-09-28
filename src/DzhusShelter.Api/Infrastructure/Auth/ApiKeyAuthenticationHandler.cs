using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace DzhusShelter.Api.Infrastructure.Auth;

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    private const string HeaderName = "X-Api-Key";

    private readonly IConfiguration _configuration;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var provided) || string.IsNullOrEmpty(provided))
            return Task.FromResult(AuthenticateResult.NoResult());

        var providedKey = provided.ToString();
        var botKey = _configuration["Auth:ApiKeyBot"];
        var shortcutsKey = _configuration["Auth:ApiKeyShortcuts"];

        // Plain string comparison, not constant-time: this is a personal-use API key over
        // HTTPS, not a defense against nation-state timing attacks — proportionate to the
        // threat model, not an oversight.
        string? clientName = !string.IsNullOrEmpty(botKey) && providedKey == botKey ? "bot"
            : !string.IsNullOrEmpty(shortcutsKey) && providedKey == shortcutsKey ? "shortcuts"
            : null;

        if (clientName is null)
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));

        var identity = new ClaimsIdentity([new Claim("client_name", clientName)], SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
