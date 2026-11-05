namespace DzhusShelter.Api.Application.Birthdays;

/// <summary>
/// The single place that decides what "today" and "notify time reached" mean for birthdays
/// (Kyiv local time, not UTC). Separate from TimeProvider so integration tests can move the
/// birthday clock without also moving the clock JWT issuing/validation runs on.
/// </summary>
public interface IBirthdayClock
{
    DateTimeOffset UtcNow { get; }
    DateOnly Today { get; }
    bool IsNotifyTimeReached { get; }
}
