using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DzhusShelter.Api.Application.Auth;
using Microsoft.IdentityModel.Tokens;

namespace DzhusShelter.Api.Infrastructure.Auth;

public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _timeProvider;

    public JwtTokenGenerator(IConfiguration configuration, TimeProvider timeProvider)
    {
        _configuration = configuration;
        _timeProvider = timeProvider;
    }

    public string GenerateToken()
    {
        var signingKey = _configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var now = _timeProvider.GetUtcNow();
        var token = new JwtSecurityToken(
            claims: [new Claim(ClaimTypes.NameIdentifier, "admin")],
            notBefore: now.UtcDateTime,
            expires: now.AddDays(7).UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
