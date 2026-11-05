using DzhusShelter.Api.Application.Abstractions;
using DzhusShelter.Api.Domain.Birthdays;
using DzhusShelter.Api.Domain.Common;
using FluentValidation;

namespace DzhusShelter.Api.Application.Birthdays.Commands;

public sealed class MarkBirthdayNotificationSentCommandHandler : ICommandHandler<MarkBirthdayNotificationSentCommand, Result>
{
    private readonly IBirthdayNotificationRepository _repository;
    private readonly IValidator<MarkBirthdayNotificationSentCommand> _validator;
    private readonly IBirthdayClock _clock;

    public MarkBirthdayNotificationSentCommandHandler(
        IBirthdayNotificationRepository repository,
        IValidator<MarkBirthdayNotificationSentCommand> validator,
        IBirthdayClock clock)
    {
        _repository = repository;
        _validator = validator;
        _clock = clock;
    }

    public async Task<Result> Handle(MarkBirthdayNotificationSentCommand command, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Failure(new Error("Birthdays.Validation", validationResult.Errors[0].ErrorMessage));

        var notification = BirthdayNotification.Create(command.Kind, command.ForDate, _clock.UtcNow);

        // Idempotent: a duplicate (kind, date) means the reminder is already recorded - still success.
        await _repository.TryAddAsync(notification, cancellationToken);
        return Result.Success();
    }
}
