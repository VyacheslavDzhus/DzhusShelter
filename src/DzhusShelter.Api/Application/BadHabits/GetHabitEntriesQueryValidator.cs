using FluentValidation;

namespace DzhusShelter.Api.Application.BadHabits;

public sealed class GetHabitEntriesQueryValidator : AbstractValidator<GetHabitEntriesQuery>
{
    public GetHabitEntriesQueryValidator()
    {
        RuleFor(x => x.From).LessThanOrEqualTo(x => x.To).WithMessage("'From' must be before or equal to 'To'.");
    }
}
