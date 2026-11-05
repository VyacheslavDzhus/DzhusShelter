using DzhusShelter.Api.Application.Abstractions;
using DzhusShelter.Api.Domain.Birthdays;
using DzhusShelter.Api.Domain.Common;
using FluentValidation;

namespace DzhusShelter.Api.Application.Birthdays.Commands;

public sealed class CreateBirthdayCommandHandler : ICommandHandler<CreateBirthdayCommand, Result<Guid>>
{
    private readonly IBirthdayRepository _repository;
    private readonly IValidator<CreateBirthdayCommand> _validator;
    private readonly IBirthdayClock _clock;

    public CreateBirthdayCommandHandler(
        IBirthdayRepository repository,
        IValidator<CreateBirthdayCommand> validator,
        IBirthdayClock clock)
    {
        _repository = repository;
        _validator = validator;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(CreateBirthdayCommand command, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Failure<Guid>(new Error("Birthdays.Validation", validationResult.Errors[0].ErrorMessage));

        var birthdayResult = Birthday.Create(command.Name, command.Day, command.Month, command.Year, _clock.Today, _clock.UtcNow);
        if (birthdayResult.IsFailure)
            return Result.Failure<Guid>(birthdayResult.Error);

        await _repository.AddAsync(birthdayResult.Value, cancellationToken);
        return Result.Success(birthdayResult.Value.Id);
    }
}
