using DzhusShelter.Api.Domain.Birthdays;

namespace DzhusShelter.Api.Application.Birthdays;

public interface IBirthdayNotificationRepository
{
    Task<bool> ExistsAsync(BirthdayNotificationKind kind, DateOnly forDate, CancellationToken cancellationToken);

    /// <summary>Returns false (never throws) when a row for the same (kind, forDate) already exists.</summary>
    Task<bool> TryAddAsync(BirthdayNotification notification, CancellationToken cancellationToken);
}
