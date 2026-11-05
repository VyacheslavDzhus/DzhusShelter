using DzhusShelter.Api.Application.Birthdays;
using DzhusShelter.Api.Application.Birthdays.Commands;
using DzhusShelter.Api.Application.Birthdays.Dtos;
using DzhusShelter.Api.Application.Birthdays.Queries;
using DzhusShelter.Api.Domain.Birthdays;
using FluentAssertions;
using FluentValidation;
using NSubstitute;

namespace DzhusShelter.Api.Tests.Application.Birthdays;

public class BirthdayNotificationHandlersTests
{
    private readonly IBirthdayRepository _birthdays = Substitute.For<IBirthdayRepository>();
    private readonly IBirthdayNotificationRepository _notifications = Substitute.For<IBirthdayNotificationRepository>();

    private static Birthday B(DateOnly today, string name, int day, int month, int? year) =>
        Birthday.Create(name, day, month, year, today, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).Value;

    private async Task<IReadOnlyList<PendingBirthdayNotificationDto>> Pending(StubBirthdayClock clock, params Birthday[] people)
    {
        _birthdays.GetAllAsync(Arg.Any<CancellationToken>()).Returns(people.ToList());
        var handler = new GetPendingBirthdayNotificationsQueryHandler(_birthdays, _notifications, clock);
        var result = await handler.Handle(new GetPendingBirthdayNotificationsQuery(), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    [Fact]
    public async Task Pending_BeforeNotifyTime_IsEmpty()
    {
        var today = new DateOnly(2026, 11, 12);
        var clock = new StubBirthdayClock(today, isNotifyTimeReached: false);

        var pending = await Pending(clock, B(today, "Саша", 12, 11, 2001));

        pending.Should().BeEmpty();
    }

    [Fact]
    public async Task Pending_AfterNotifyTime_ReturnsTodayAndTomorrow()
    {
        var today = new DateOnly(2026, 11, 12);
        var clock = new StubBirthdayClock(today);

        var pending = await Pending(clock, B(today, "Оля", 13, 11, null), B(today, "Саша", 12, 11, 2001));

        pending.Should().HaveCount(2);
        pending[0].Kind.Should().Be(BirthdayNotificationKind.Today);
        pending[0].ForDate.Should().Be(today);
        pending[0].People.Should().Equal(new BirthdayPersonDto("Саша", 25));
        pending[1].Kind.Should().Be(BirthdayNotificationKind.Tomorrow);
        pending[1].ForDate.Should().Be(today);
        pending[1].People.Should().Equal(new BirthdayPersonDto("Оля", null));
    }

    [Fact]
    public async Task Pending_AlreadySentToday_ExcludesThatKind()
    {
        var today = new DateOnly(2026, 11, 12);
        var clock = new StubBirthdayClock(today);
        _notifications.ExistsAsync(BirthdayNotificationKind.Today, today, Arg.Any<CancellationToken>()).Returns(true);

        var pending = await Pending(clock, B(today, "Саша", 12, 11, 2001), B(today, "Оля", 13, 11, null));

        pending.Should().ContainSingle().Which.Kind.Should().Be(BirthdayNotificationKind.Tomorrow);
    }

    [Fact]
    public async Task Pending_NobodyDue_IsEmpty()
    {
        var today = new DateOnly(2026, 11, 12);

        var pending = await Pending(new StubBirthdayClock(today), B(today, "Саша", 20, 3, 2001));

        pending.Should().BeEmpty();
    }

    [Fact]
    public async Task Pending_OnDec31_JanFirstBirthdayIsTomorrowWithNewYearsAge()
    {
        var today = new DateOnly(2026, 12, 31);

        var pending = await Pending(new StubBirthdayClock(today), B(today, "Нова", 1, 1, 2000));

        var item = pending.Should().ContainSingle().Subject;
        item.Kind.Should().Be(BirthdayNotificationKind.Tomorrow);
        item.ForDate.Should().Be(today);
        item.People.Should().Equal(new BirthdayPersonDto("Нова", 27));
    }

    [Fact]
    public async Task Pending_Feb28NonLeap_IncludesFeb29Person()
    {
        var today = new DateOnly(2027, 2, 28);

        var pending = await Pending(new StubBirthdayClock(today), B(today, "Лео", 29, 2, 2000));

        var item = pending.Should().ContainSingle().Subject;
        item.Kind.Should().Be(BirthdayNotificationKind.Today);
        item.People.Should().Equal(new BirthdayPersonDto("Лео", 27));
    }

    [Fact]
    public async Task Pending_SeveralSameDay_SortedByName()
    {
        var today = new DateOnly(2026, 11, 12);

        var pending = await Pending(new StubBirthdayClock(today),
            B(today, "Яна", 12, 11, null), B(today, "Андрій", 12, 11, null), B(today, "Максим", 12, 11, null));

        pending.Should().ContainSingle().Which.People.Select(p => p.Name)
            .Should().Equal("Андрій", "Максим", "Яна");
    }

    [Fact]
    public async Task MarkSent_New_AddsRowWithClockUtcNow()
    {
        var today = new DateOnly(2026, 11, 12);
        var clock = new StubBirthdayClock(today) { UtcNow = new DateTimeOffset(2026, 11, 12, 7, 5, 0, TimeSpan.Zero) };
        _notifications.TryAddAsync(Arg.Any<BirthdayNotification>(), Arg.Any<CancellationToken>()).Returns(true);
        var handler = new MarkBirthdayNotificationSentCommandHandler(_notifications, new MarkBirthdayNotificationSentCommandValidator(), clock);

        var result = await handler.Handle(new MarkBirthdayNotificationSentCommand(BirthdayNotificationKind.Today, today), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _notifications.Received(1).TryAddAsync(
            Arg.Is<BirthdayNotification>(n => n.Kind == BirthdayNotificationKind.Today
                && n.ForDate == today && n.SentAt == clock.UtcNow),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkSent_Duplicate_StillSuccess()
    {
        var today = new DateOnly(2026, 11, 12);
        _notifications.TryAddAsync(Arg.Any<BirthdayNotification>(), Arg.Any<CancellationToken>()).Returns(false);
        var handler = new MarkBirthdayNotificationSentCommandHandler(_notifications, new MarkBirthdayNotificationSentCommandValidator(), new StubBirthdayClock(today));

        var result = await handler.Handle(new MarkBirthdayNotificationSentCommand(BirthdayNotificationKind.Tomorrow, today), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task MarkSent_InvalidKind_FailsWithValidationError()
    {
        var today = new DateOnly(2026, 11, 12);
        var handler = new MarkBirthdayNotificationSentCommandHandler(_notifications, new MarkBirthdayNotificationSentCommandValidator(), new StubBirthdayClock(today));

        var result = await handler.Handle(new MarkBirthdayNotificationSentCommand((BirthdayNotificationKind)99, today), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Birthdays.Validation");
        await _notifications.DidNotReceiveWithAnyArgs().TryAddAsync(default!, default);
    }
}
