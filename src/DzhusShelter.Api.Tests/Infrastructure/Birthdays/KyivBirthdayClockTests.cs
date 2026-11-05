using DzhusShelter.Api.Application.Birthdays;
using DzhusShelter.Api.Infrastructure.Birthdays;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace DzhusShelter.Api.Tests.Infrastructure.Birthdays;

public class KyivBirthdayClockTests
{
    private static KyivBirthdayClock ClockAt(string utcIso) =>
        new(new FakeTimeProvider(DateTimeOffset.Parse(utcIso)), Options.Create(new BirthdaysOptions()));

    [Theory]
    [InlineData("2026-01-15T21:59:00Z", "2026-01-15")]
    [InlineData("2026-01-15T22:00:00Z", "2026-01-16")]
    public void Today_InWinter_UsesUtcPlus2(string utc, string expectedToday)
    {
        ClockAt(utc).Today.Should().Be(DateOnly.Parse(expectedToday));
    }

    [Fact]
    public void Today_AfterKyivMidnight_IsNextDayEvenThoughUtcIsStillPreviousDay()
    {
        ClockAt("2026-12-31T22:30:00Z").Today.Should().Be(new DateOnly(2027, 1, 1));
    }

    [Theory]
    [InlineData("2026-01-15T06:59:00Z", false)]
    [InlineData("2026-01-15T07:00:00Z", true)]
    public void IsNotifyTimeReached_Winter(string utc, bool expected)
    {
        ClockAt(utc).IsNotifyTimeReached.Should().Be(expected);
    }

    [Theory]
    [InlineData("2026-07-15T05:59:00Z", false)]
    [InlineData("2026-07-15T06:00:00Z", true)]
    public void IsNotifyTimeReached_Summer(string utc, bool expected)
    {
        ClockAt(utc).IsNotifyTimeReached.Should().Be(expected);
    }

    [Theory]
    [InlineData("2026-03-29T05:59:00Z", false)]
    [InlineData("2026-03-29T06:00:00Z", true)]
    public void IsNotifyTimeReached_OnDstSwitchDay(string utc, bool expected)
    {
        ClockAt(utc).IsNotifyTimeReached.Should().Be(expected);
    }

    [Fact]
    public void UtcNow_PassesThroughTimeProvider()
    {
        ClockAt("2026-11-05T10:00:00Z").UtcNow.Should().Be(new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void NotifyAt_IsConfigurable()
    {
        var clock = new KyivBirthdayClock(
            new FakeTimeProvider(DateTimeOffset.Parse("2026-01-15T05:30:00Z")),
            Options.Create(new BirthdaysOptions { NotifyAt = new TimeOnly(7, 30) }));

        clock.IsNotifyTimeReached.Should().BeTrue();
    }

    [Fact]
    public void UnknownTimeZone_FailsFastInsteadOfFallingBackToUtc()
    {
        var act = () => new KyivBirthdayClock(TimeProvider.System, Options.Create(new BirthdaysOptions { TimeZone = "Mars/Olympus_Mons" }));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Mars/Olympus_Mons*");
    }
}
