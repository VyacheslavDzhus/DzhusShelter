using DzhusShelter.Api.Application.Abstractions;
using DzhusShelter.Api.Domain.BadHabits;
using DzhusShelter.Api.Domain.Common;
using FluentValidation;

namespace DzhusShelter.Api.Application.BadHabits;

public sealed class LogHabitEntryCommandHandler : ICommandHandler<LogHabitEntryCommand, Result<Guid>>
{
    private readonly IHabitEntryRepository _repository;
    private readonly IValidator<LogHabitEntryCommand> _validator;
    private readonly TimeProvider _timeProvider;

    public LogHabitEntryCommandHandler(
        IHabitEntryRepository repository,
        IValidator<LogHabitEntryCommand> validator,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _validator = validator;
        _timeProvider = timeProvider;
    }

    public async Task<Result<Guid>> Handle(LogHabitEntryCommand command, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Failure<Guid>(new Error("BadHabits.Validation", validationResult.Errors[0].ErrorMessage));

        var entryResult = HabitEntry.Create(command.HabitType, command.SubType, command.OccurredAt, command.Notes, _timeProvider);
        if (entryResult.IsFailure)
            return Result.Failure<Guid>(entryResult.Error);

        await _repository.AddAsync(entryResult.Value, cancellationToken);
        return Result.Success(entryResult.Value.Id);
    }
}
