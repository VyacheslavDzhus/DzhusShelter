using DzhusShelter.Api.Application.Birthdays;
using Microsoft.Extensions.Options;

namespace DzhusShelter.Api.Infrastructure.Birthdays;

public sealed class KyivBirthdayClock : IBirthdayClock
{
    // Pre-2022 tzdata only knows the old spelling.
    private const string LegacyKyivId = "Europe/Kiev";

    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _zone;
    private readonly TimeOnly _notifyAt;

    public KyivBirthdayClock(TimeProvider timeProvider, IOptions<BirthdaysOptions> options)
    {
        _timeProvider = timeProvider;
        _notifyAt = options.Value.NotifyAt;
        _zone = ResolveZone(options.Value.TimeZone);
    }

    public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

    public DateOnly Today => DateOnly.FromDateTime(LocalNow.DateTime);

    public bool IsNotifyTimeReached => TimeOnly.FromDateTime(LocalNow.DateTime) >= _notifyAt;

    private DateTimeOffset LocalNow => TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), _zone);

    private static TimeZoneInfo ResolveZone(string id)
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone))
            return zone;
        if (id == "Europe/Kyiv" && TimeZoneInfo.TryFindSystemTimeZoneById(LegacyKyivId, out zone))
            return zone;

        // Fail fast: silently falling back to UTC would shift "today" by 2–3 hours and send
        // reminders on the wrong day around midnight — worse than not starting at all.
        throw new InvalidOperationException(
            $"Time zone '{id}' (or '{LegacyKyivId}') is not available on this machine — install tzdata in the container.");
    }
}
