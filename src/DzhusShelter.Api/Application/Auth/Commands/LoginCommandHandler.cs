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
        if (string.IsNullOrEmpty(passwordHash) || !BCrypt.Net.BCrypt.Verify(command.Password, passwordHash))
            return Result.Failure<string>(InvalidCredentials);

        return Result.Success(_tokenGenerator.GenerateToken());
    }
}
