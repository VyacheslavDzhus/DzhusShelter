using DzhusShelter.MainBot.Birthdays;
using DzhusShelter.MainBot.Telegram;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace DzhusShelter.MainBot.Tests;

public class BirthdayNotificationServiceTests
{
    private static readonly DateOnly Date = new(2026, 11, 12);
    private static readonly PendingBirthdayNotification Today =
        new(BirthdayNotificationKind.Today, Date, [new BirthdayPerson("Саша Пупкін", 25)]);
    private static readonly PendingBirthdayNotification Tomorrow =
        new(BirthdayNotificationKind.Tomorrow, Date, [new BirthdayPerson("Оля Коваль", null)]);

    private readonly IBirthdayNotificationsApi _api = Substitute.For<IBirthdayNotificationsApi>();
    private readonly INotificationSender _sender = Substitute.For<INotificationSender>();

    private BirthdayNotificationService Service() =>
        new(_api, _sender, new MainBotSettings(), NullLogger<BirthdayNotificationService>.Instance);

    private void Pending(params PendingBirthdayNotification[] items) =>
        _api.GetPendingAsync(Arg.Any<CancellationToken>()).Returns(items);

    [Fact]
    public async Task RunOnce_SendsEachPendingThenMarksIt()
    {
        Pending(Today, Tomorrow);

        await Service().RunOnceAsync(CancellationToken.None);

        Received.InOrder(() =>
        {
            _sender.SendAsync(BirthdayMessageFormatter.Format(Today), Arg.Any<CancellationToken>());
            _api.MarkSentAsync(BirthdayNotificationKind.Today, Today.ForDate, Arg.Any<CancellationToken>());
            _sender.SendAsync(BirthdayMessageFormatter.Format(Tomorrow), Arg.Any<CancellationToken>());
            _api.MarkSentAsync(BirthdayNotificationKind.Tomorrow, Tomorrow.ForDate, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task RunOnce_FirstSendFails_SecondStillSentAndOnlySecondMarked()
    {
        Pending(Today, Tomorrow);
        _sender.SendAsync(BirthdayMessageFormatter.Format(Today), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("telegram down"));

        var act = () => Service().RunOnceAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        await _sender.Received(1).SendAsync(BirthdayMessageFormatter.Format(Tomorrow), Arg.Any<CancellationToken>());
        await _api.DidNotReceive().MarkSentAsync(BirthdayNotificationKind.Today, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
        await _api.Received(1).MarkSentAsync(BirthdayNotificationKind.Tomorrow, Tomorrow.ForDate, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnce_MarkFails_DoesNotThrowOutOfService()
    {
        Pending(Today, Tomorrow);
        _api.MarkSentAsync(BirthdayNotificationKind.Today, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("api down"));

        var act = () => Service().RunOnceAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        await _api.Received(1).MarkSentAsync(BirthdayNotificationKind.Tomorrow, Tomorrow.ForDate, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnce_ApiUnreachable_NothingSent()
    {
        _api.GetPendingAsync(Arg.Any<CancellationToken>()).Throws(new HttpRequestException("unreachable"));

        var act = () => Service().RunOnceAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        await _sender.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnce_NotificationWithNoPeople_IsSkipped()
    {
        Pending(new PendingBirthdayNotification(BirthdayNotificationKind.Today, Date, []), Tomorrow);

        await Service().RunOnceAsync(CancellationToken.None);

        await _sender.Received(1).SendAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _api.DidNotReceive().MarkSentAsync(BirthdayNotificationKind.Today, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
        await _api.Received(1).MarkSentAsync(BirthdayNotificationKind.Tomorrow, Tomorrow.ForDate, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnce_Cancelled_PropagatesCancellation()
    {
        Pending(Today);
        using var cts = new CancellationTokenSource();
        _sender.SendAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => { cts.Cancel(); return Task.FromCanceled(cts.Token); });

        var act = () => Service().RunOnceAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        await _api.DidNotReceive().MarkSentAsync(Arg.Any<BirthdayNotificationKind>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnce_AckFailedEarlier_DoesNotResendAndRetriesAck()
    {
        Pending(Today);
        _api.MarkSentAsync(BirthdayNotificationKind.Today, Today.ForDate, Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("api down"));
        var service = Service();

        await service.RunOnceAsync(CancellationToken.None);
        await service.RunOnceAsync(CancellationToken.None);

        await _sender.Received(1).SendAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _api.Received(2).MarkSentAsync(BirthdayNotificationKind.Today, Today.ForDate, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnce_AckRetrySucceeds_ThenSameItemAppearingAgainIsTreatedAsNewAndSent()
    {
        Pending(Today);
        _api.MarkSentAsync(BirthdayNotificationKind.Today, Today.ForDate, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new HttpRequestException("api down")), Task.CompletedTask, Task.CompletedTask);
        var service = Service();

        await service.RunOnceAsync(CancellationToken.None); // send ok, ack fails
        await service.RunOnceAsync(CancellationToken.None); // ack retried OK, entry forgotten
        await service.RunOnceAsync(CancellationToken.None); // same item again -> new -> sent

        await _sender.Received(2).SendAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnce_OldAwaitingAckEntries_AreDropped()
    {
        var yesterday = new PendingBirthdayNotification(BirthdayNotificationKind.Today, Date.AddDays(-1), [new BirthdayPerson("Іра Мельник", 30)]);
        _api.MarkSentAsync(BirthdayNotificationKind.Today, yesterday.ForDate, Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("api down"));
        var service = Service();

        Pending(yesterday);
        await service.RunOnceAsync(CancellationToken.None); // sent, ack failed, remembered
        Pending(Today);
        await service.RunOnceAsync(CancellationToken.None); // new day: old entry dropped, today sent
        Pending(yesterday);
        await service.RunOnceAsync(CancellationToken.None); // yesterday entry was dropped -> sent again

        await _sender.Received(2).SendAsync(BirthdayMessageFormatter.Format(yesterday), Arg.Any<CancellationToken>());
        await _sender.Received(1).SendAsync(BirthdayMessageFormatter.Format(Today), Arg.Any<CancellationToken>());
    }
}
