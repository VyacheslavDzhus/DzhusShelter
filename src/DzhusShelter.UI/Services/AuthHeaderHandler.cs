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
        var token = _cookieService.GetToken(_httpContextAccessor.HttpContext);
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return base.SendAsync(request, cancellationToken);
    }
}
