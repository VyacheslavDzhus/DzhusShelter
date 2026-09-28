using DzhusShelter.Api.Application.Abstractions;
using DzhusShelter.Api.Application.Auth;
using DzhusShelter.Api.Domain.Common;
using FluentValidation;

namespace DzhusShelter.Api.Application.Auth.Commands;

public sealed class LoginCommandHandler : ICommandHandler<LoginCommand, Result<string>>
{
    private static readonly Error InvalidCredentials = new("Auth.InvalidCredentials", "Invalid credentials.");

    private readonly IConfiguration _configuration;
    private readonly IValidator<LoginCommand> _validator;
    private readonly IJwtTokenGenerator _tokenGenerator;

    public LoginCommandHandler(IConfiguration configuration, IValidator<LoginCommand> validator, IJwtTokenGenerator tokenGenerator)
    {
        _configuration = configuration;
        _validator = validator;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<Result<string>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Failure<string>(InvalidCredentials);

        var passwordHash = _configuration["Auth:AdminPasswordHash"];
        if (!PasswordMatches(command.Password, passwordHash))
            return Result.Failure<string>(InvalidCredentials);

        return Result.Success(_tokenGenerator.GenerateToken());
    }

    private static bool PasswordMatches(string password, string? passwordHash)
    {
        if (string.IsNullOrEmpty(passwordHash))
            return false;

        try
        {
            // BCrypt.Verify throws (e.g. SaltParseException) instead of returning false when
            // passwordHash isn't a well-formed bcrypt hash — a misconfigured Auth:AdminPasswordHash
            // (typo, leftover placeholder, wrong format) must fail the same way as a wrong
            // password, not surface as a distinguishable 500 that leaks "this is a config bug".
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
