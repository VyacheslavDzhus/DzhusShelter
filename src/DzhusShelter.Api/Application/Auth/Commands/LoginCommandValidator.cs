using FluentValidation;

namespace DzhusShelter.Api.Application.Auth.Commands;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Password).NotEmpty();
    }
}
