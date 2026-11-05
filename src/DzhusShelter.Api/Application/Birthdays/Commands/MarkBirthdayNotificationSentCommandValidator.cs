using FluentValidation;

namespace DzhusShelter.Api.Application.Birthdays.Commands;

public sealed class MarkBirthdayNotificationSentCommandValidator : AbstractValidator<MarkBirthdayNotificationSentCommand>
{
    public MarkBirthdayNotificationSentCommandValidator()
    {
        RuleFor(x => x.Kind).IsInEnum().WithMessage("Невідомий тип сповіщення.");
    }
}
