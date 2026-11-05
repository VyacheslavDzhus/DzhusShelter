namespace DzhusShelter.Api.Application.Birthdays;

public sealed class BirthdaysOptions
{
    public const string SectionName = "Birthdays";

    /// <summary>IANA time zone all "today/tomorrow/notify time" logic runs in.</summary>
    public string TimeZone { get; set; } = "Europe/Kyiv";

    /// <summary>Local time from which today's reminders become pending.</summary>
    public TimeOnly NotifyAt { get; set; } = new(9, 0);
}
