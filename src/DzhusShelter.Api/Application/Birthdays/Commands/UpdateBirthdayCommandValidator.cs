using DzhusShelter.Api.Domain.Birthdays;
using FluentValidation;

namespace DzhusShelter.Api.Application.Birthdays.Commands;

public sealed class UpdateBirthdayCommandValidator : AbstractValidator<UpdateBirthdayCommand>
{
    public UpdateBirthdayCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Вкажіть ім'я.")
            .MaximumLength(BirthdayErrors.MaxNameLength).WithMessage($"Ім'я не може бути довшим за {BirthdayErrors.MaxNameLength} символів.");
        RuleFor(x => x.Day).InclusiveBetween(1, 31).WithMessage("День має бути від 1 до 31.");
        RuleFor(x => x.Month).InclusiveBetween(1, 12).WithMessage("Місяць має бути від 1 до 12.");
        RuleFor(x => x.Year!.Value).InclusiveBetween(BirthdayErrors.MinYear, 9999)
            .When(x => x.Year.HasValue)
            .WithMessage($"Рік має бути від {BirthdayErrors.MinYear} до 9999.");
    }
}
