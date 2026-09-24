using DzhusShelter.Api.Application.Abstractions;
using DzhusShelter.Api.Application.BadHabits;
using DzhusShelter.Api.Domain.BadHabits;
using DzhusShelter.Api.Domain.Common;
using FluentValidation;

namespace DzhusShelter.Api.Application.BadHabits.Commands;

public sealed class LogHabitEntryCommandHandler : ICommandHandler<LogHabitEntryCommand, Result<LogHabitEntryResult>>
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

    public async Task<Result<LogHabitEntryResult>> Handle(LogHabitEntryCommand command, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Failure<LogHabitEntryResult>(new Error("BadHabits.Validation", validationResult.Errors[0].ErrorMessage));

        var entryResult = HabitEntry.Create(command.HabitType, command.SubType, command.OccurredAt, command.Notes, _timeProvider);
        if (entryResult.IsFailure)
            return Result.Failure<LogHabitEntryResult>(entryResult.Error);

        var dayStart = new DateTimeOffset(command.OccurredAt.UtcDateTime.Date, TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1).AddTicks(-1);
        var existingEntries = await _repository.GetAsync(dayStart, dayEnd, command.HabitType, command.SubType, cancellationToken);
        if (existingEntries.Count > 0)
            return Result.Success(new LogHabitEntryResult(existingEntries[0].Id, AlreadyLogged: true));

        await _repository.AddAsync(entryResult.Value, cancellationToken);
        return Result.Success(new LogHabitEntryResult(entryResult.Value.Id, AlreadyLogged: false));
    }
}
