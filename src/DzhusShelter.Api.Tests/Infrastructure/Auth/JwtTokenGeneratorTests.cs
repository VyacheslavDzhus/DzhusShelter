using System.IdentityModel.Tokens.Jwt;
using System.Text;
using DzhusShelter.Api.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Tokens;

namespace DzhusShelter.Api.Tests.Infrastructure.Auth;

public class JwtTokenGeneratorTests
{
    private const string SigningKey = "unit-test-signing-key-at-least-32-bytes!";

    // JwtSecurityTokenHandler.ValidateToken checks lifetime against the real system clock,
    // not the FakeTimeProvider injected into the generator (that only controls what the
    // token's claims are minted with). A hardcoded calendar date here would eventually fall
    // into the past relative to whenever the suite actually runs and make this test fail
    // with a spurious "token expired" — so anchor it to the real clock instead.
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static JwtTokenGenerator CreateGenerator()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = SigningKey })
            .Build();
        var timeProvider = new FakeTimeProvider(Now);
        return new JwtTokenGenerator(configuration, timeProvider);
    }

    [Fact]
    public void GenerateToken_ProducesATokenThatValidatesAgainstTheSameSigningKey()
    {
        var generator = CreateGenerator();

        var token = generator.GenerateToken();

        var handler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
        };

        var act = () => handler.ValidateToken(token, validationParameters, out _);
        act.Should().NotThrow();
    }

    [Fact]
    public void GenerateToken_ExpiresSevenDaysFromNow()
    {
        var generator = CreateGenerator();

        var token = generator.GenerateToken();

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.ValidTo.Should().BeCloseTo(Now.AddDays(7).UtcDateTime, TimeSpan.FromSeconds(5));
    }
}
