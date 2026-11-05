using DzhusShelter.Api.Application.Birthdays;

namespace DzhusShelter.Api.Tests.Application.Birthdays;

internal sealed class StubBirthdayClock(DateOnly today, bool isNotifyTimeReached = true) : IBirthdayClock
{
    public DateTimeOffset UtcNow { get; init; } = new(today.ToDateTime(new TimeOnly(10, 0)), TimeSpan.Zero);
    public DateOnly Today { get; } = today;
    public bool IsNotifyTimeReached { get; } = isNotifyTimeReached;
}
