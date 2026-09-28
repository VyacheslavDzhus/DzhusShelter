using System.Net.Http.Headers;

namespace DzhusShelter.UI.Services;

public sealed class AuthHeaderHandler : DelegatingHandler
{
    private readonly AuthCookieService _cookieService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthHeaderHandler(AuthCookieService cookieService, IHttpContextAccessor httpContextAccessor)
    {
        _cookieService = cookieService;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Microsoft's own docs caution against relying on IHttpContextAccessor.HttpContext
        // inside a live Blazor Server interactive circuit (e.g. on pages using
        // @rendermode InteractiveServer) — officially it's not guaranteed non-null there.
        // Empirically verified working as of 2026-09-28 (a real login, then real
        // month-navigation clicks on /bad-habits — an InteractiveServer page — both
        // correctly attached the bearer token; confirmed via server logs showing 200,
        // not 401, on the resulting Api calls). If interactive-page auth ever starts
        // silently failing after this comment was written, this undocumented reliance
        // is the first place to look — the documented, fully-supported fix at that
        // point is a CircuitServicesAccessor-based per-circuit token store instead of
        // this accessor.
        var token = _cookieService.GetToken(_httpContextAccessor.HttpContext);
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return base.SendAsync(request, cancellationToken);
    }
}
