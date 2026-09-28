namespace DzhusShelter.UI.Services;

public sealed class AuthCookieService
{
    private const string CookieName = "dzhus_auth_token";

    public string? GetToken(HttpContext? httpContext)
    {
        if (httpContext is null)
            return null;
        return httpContext.Request.Cookies.TryGetValue(CookieName, out var token) ? token : null;
    }

    public void SetToken(HttpContext httpContext, string token)
    {
        httpContext.Response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            // Reflects whether *this* request arrived over HTTPS. Behind the Cloudflare
            // Tunnel, that's only accurate because Program.cs's ForwardedHeaders
            // middleware (added for the Api project in Task 1; add the same for this UI
            // project below) restores the original scheme — otherwise Kestrel only ever
            // sees plain HTTP from the tunnel and this would always evaluate to false.
            Secure = httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7),
        });
    }

    public void ClearToken(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Delete(CookieName);
    }
}
