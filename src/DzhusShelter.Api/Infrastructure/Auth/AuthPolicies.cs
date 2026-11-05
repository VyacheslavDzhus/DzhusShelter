namespace DzhusShelter.Api.Infrastructure.Auth;

/// <summary>
/// Per-client access (docs/specs/birthdays.md, "Access per client"). An endpoint-level
/// [Authorize(Policy = ...)] replaces the FallbackPolicy for that endpoint, so these policies
/// scope individual actions without touching endpoints that rely on the fallback.
/// </summary>
public static class AuthPolicies
{
    /// <summary>JWT (the Blazor UI) only — no API key, whatever its client.</summary>
    public const string UiOnly = "UiOnly";

    /// <summary>The main bot's API key only.</summary>
    public const string MainBotNotifications = "MainBotNotifications";

    public const string ClientNameClaim = "client_name";

    /// <summary>Send-only client: the FallbackPolicy rejects it everywhere except its own endpoints.</summary>
    public const string MainBotClient = "main-bot";
}
