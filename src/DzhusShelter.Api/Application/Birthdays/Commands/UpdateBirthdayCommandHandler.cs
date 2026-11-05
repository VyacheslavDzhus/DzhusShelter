using DzhusShelter.Api.Application.Abstractions;
using DzhusShelter.Api.Domain.Birthdays;
using DzhusShelter.Api.Domain.Common;
using FluentValidation;

namespace DzhusShelter.Api.Application.Birthdays.Commands;

public sealed class UpdateBirthdayCommandHandler : ICommandHandler<UpdateBirthdayCommand, Result>
{
    private readonly IBirthdayRepository _repository;
    private readonly IValidator<UpdateBirthdayCommand> _validator;
    private readonly IBirthdayClock _clock;

    public UpdateBirthdayCommandHandler(
        IBirthdayRepository repository,
        IValidator<UpdateBirthdayCommand> validator,
        IBirthdayClock clock)
    {
        _repository = repository;
        _validator = validator;
        _clock = clock;
    }

    public async Task<Result> Handle(UpdateBirthdayCommand command, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Failure(new Error("Birthdays.Validation", validationResult.Errors[0].ErrorMessage));

        var birthday = await _repository.GetByIdAsync(command.Id, cancellationToken);
        if (birthday is null)
            return Result.Failure(BirthdayErrors.NotFound(command.Id));

        var updateResult = birthday.Update(command.Name, command.Day, command.Month, command.Year, _clock.Today);
        if (updateResult.IsFailure)
            return updateResult;

        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
