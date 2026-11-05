namespace DzhusShelter.Api.Domain.Birthdays;

/// <summary>
/// Record that the <see cref="Kind"/> reminder due on <see cref="ForDate"/> (a Kyiv date) was sent.
/// A unique (Kind, ForDate) index makes "mark as sent" idempotent and keeps reminders once per day
/// across bot restarts.
/// </summary>
public sealed class BirthdayNotification
{
    private BirthdayNotification()
    {
        // EF Core materialization
    }

    private BirthdayNotification(Guid id, BirthdayNotificationKind kind, DateOnly forDate, DateTimeOffset sentAt)
    {
        Id = id;
        Kind = kind;
        ForDate = forDate;
        SentAt = sentAt;
    }

    public Guid Id { get; private set; }
    public BirthdayNotificationKind Kind { get; private set; }
    public DateOnly ForDate { get; private set; }
    public DateTimeOffset SentAt { get; private set; }

    public static BirthdayNotification Create(BirthdayNotificationKind kind, DateOnly forDate, DateTimeOffset sentAt) =>
        new(Guid.NewGuid(), kind, forDate, sentAt);
}
