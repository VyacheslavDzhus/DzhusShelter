using FluentValidation;

namespace DzhusShelter.Api.Application.BadHabits.Commands;

public sealed class LogHabitEntryCommandValidator : AbstractValidator<LogHabitEntryCommand>
{
    public LogHabitEntryCommandValidator()
    {
        RuleFor(x => x.HabitType).IsInEnum();
        RuleFor(x => x.SubType).IsInEnum();
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
