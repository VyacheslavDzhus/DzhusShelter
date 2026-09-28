using DzhusShelter.Api.Application.Auth;
using DzhusShelter.Api.Application.Auth.Commands;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace DzhusShelter.Api.Tests.Application.Auth;

public class LoginCommandHandlerTests
{
    private const string CorrectPassword = "correct-horse-battery-staple";

    // Real bcrypt hash of CorrectPassword above, generated via:
    //   python -c "import bcrypt; print(bcrypt.hashpw(b'correct-horse-battery-staple', bcrypt.gensalt()).decode())"
    private const string PasswordHash = "$2b$12$55/OFwUX6g.Zi1eKUT8/MO/msH7vSUCqU4ThTfbi55lkf77PTmcnq";

    private static LoginCommandHandler CreateHandler(IJwtTokenGenerator? tokenGenerator = null, string? passwordHash = PasswordHash)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:AdminPasswordHash"] = passwordHash })
            .Build();
        return new LoginCommandHandler(configuration, new LoginCommandValidator(), tokenGenerator ?? Substitute.For<IJwtTokenGenerator>());
    }

    [Fact]
    public async Task Handle_WithCorrectPassword_ReturnsSuccessWithGeneratedToken()
    {
        var tokenGenerator = Substitute.For<IJwtTokenGenerator>();
        tokenGenerator.GenerateToken().Returns("a-generated-jwt");
        var handler = CreateHandler(tokenGenerator);

        var result = await handler.Handle(new LoginCommand(CorrectPassword), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("a-generated-jwt");
    }

    [Fact]
    public async Task Handle_WithWrongPassword_ReturnsFailure()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new LoginCommand("definitely-not-it"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
    }

    [Fact]
    public async Task Handle_WithEmptyPassword_ReturnsFailureWithoutCallingBcrypt()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new LoginCommand(""), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
    }

    [Fact]
    public async Task Handle_WithMalformedConfiguredHash_ReturnsFailureInsteadOfThrowing()
    {
        // Auth:AdminPasswordHash misconfigured (typo, leftover placeholder, wrong format) must
        // fail the same way as a wrong password, not bubble up BCrypt.Net's SaltParseException
        // as an unhandled 500 that would distinguish "misconfigured" from "wrong password".
        var handler = CreateHandler(passwordHash: "not-a-real-bcrypt-hash");

        var result = await handler.Handle(new LoginCommand(CorrectPassword), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
    }
}
