using DzhusShelter.Api.Application.Abstractions;
using DzhusShelter.Api.Application.Auth.Commands;
using DzhusShelter.Api.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DzhusShelter.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ICommandHandler<LoginCommand, Result<string>> _loginHandler;

    public AuthController(ICommandHandler<LoginCommand, Result<string>> loginHandler)
    {
        _loginHandler = loginHandler;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken cancellationToken)
    {
        var result = await _loginHandler.Handle(command, cancellationToken);
        return result.IsSuccess
            ? Ok(new { token = result.Value })
            : Unauthorized(new { error = "Invalid credentials." });
    }
}
