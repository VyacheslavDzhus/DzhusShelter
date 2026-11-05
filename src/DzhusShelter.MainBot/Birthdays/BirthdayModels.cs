namespace DzhusShelter.MainBot.Birthdays;

public enum BirthdayNotificationKind
{
    Today = 1,
    Tomorrow = 2,
}

public record BirthdayPerson(string Name, int? Age);

public record PendingBirthdayNotification(
    BirthdayNotificationKind Kind,
    DateOnly ForDate,
    IReadOnlyList<BirthdayPerson> People);
