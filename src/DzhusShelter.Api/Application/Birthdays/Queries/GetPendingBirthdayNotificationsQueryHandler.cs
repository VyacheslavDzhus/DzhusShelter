using DzhusShelter.Api.Application.Abstractions;
using DzhusShelter.Api.Application.Birthdays.Dtos;
using DzhusShelter.Api.Domain.Birthdays;
using DzhusShelter.Api.Domain.Common;

namespace DzhusShelter.Api.Application.Birthdays.Queries;

public sealed class GetPendingBirthdayNotificationsQueryHandler
    : IQueryHandler<GetPendingBirthdayNotificationsQuery, Result<IReadOnlyList<PendingBirthdayNotificationDto>>>
{
    private readonly IBirthdayRepository _birthdays;
    private readonly IBirthdayNotificationRepository _notifications;
    private readonly IBirthdayClock _clock;

    public GetPendingBirthdayNotificationsQueryHandler(
        IBirthdayRepository birthdays,
        IBirthdayNotificationRepository notifications,
        IBirthdayClock clock)
    {
        _birthdays = birthdays;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<PendingBirthdayNotificationDto>>> Handle(
        GetPendingBirthdayNotificationsQuery query, CancellationToken cancellationToken)
    {
        var pending = new List<PendingBirthdayNotificationDto>();
        if (!_clock.IsNotifyTimeReached)
            return Result.Success<IReadOnlyList<PendingBirthdayNotificationDto>>(pending);

        var today = _clock.Today;
        var birthdays = await _birthdays.GetAllAsync(cancellationToken);

        foreach (var (kind, occurrence) in new[]
                 {
                     (BirthdayNotificationKind.Today, today),
                     (BirthdayNotificationKind.Tomorrow, today.AddDays(1)),
                 })
        {
            var people = birthdays
                .Where(b => b.NextOccurrence(today) == occurrence)
                .OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
                .Select(b => new BirthdayPersonDto(b.Name, b.AgeOn(occurrence)))
                .ToList();

            if (people.Count == 0 || await _notifications.ExistsAsync(kind, today, cancellationToken))
                continue;

            pending.Add(new PendingBirthdayNotificationDto(kind, today, people));
        }

        return Result.Success<IReadOnlyList<PendingBirthdayNotificationDto>>(pending);
    }
}
