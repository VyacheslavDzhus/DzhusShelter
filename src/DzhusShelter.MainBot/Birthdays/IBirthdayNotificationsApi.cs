namespace DzhusShelter.MainBot.Birthdays;

public interface IBirthdayNotificationsApi
{
    Task<IReadOnlyList<PendingBirthdayNotification>> GetPendingAsync(CancellationToken ct);

    Task MarkSentAsync(BirthdayNotificationKind kind, DateOnly forDate, CancellationToken ct);
}
