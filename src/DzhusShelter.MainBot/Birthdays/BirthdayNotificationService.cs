using DzhusShelter.MainBot.Telegram;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DzhusShelter.MainBot.Birthdays;

public sealed class BirthdayNotificationService(
    IBirthdayNotificationsApi api,
    INotificationSender sender,
    MainBotSettings settings,
    ILogger<BirthdayNotificationService> logger) : BackgroundService
{
    // Sent successfully but not yet acknowledged. Prevents re-sending while the ack keeps failing.
    // In-memory on purpose: a restart clears it (spec accepts one resend in the crash window).
    // Only touched from the single polling loop.
    private readonly HashSet<(BirthdayNotificationKind Kind, DateOnly ForDate)> _awaitingAck = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await RunOnceAsync(stoppingToken);

            using var timer = new PeriodicTimer(settings.PollInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await RunOnceAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
    }

    /// <summary>
    /// One poll. Failures are isolated per notification and logged; only cancellation escapes.
    /// Order is send -> mark (at-least-once): never mark before a successful send.
    /// If the send succeeded but the ack failed, later polls retry only the ack (never resend).
    /// </summary>
    public async Task RunOnceAsync(CancellationToken ct)
    {
        IReadOnlyList<PendingBirthdayNotification> pending;
        try
        {
            pending = await api.GetPendingAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not fetch pending birthday notifications; will retry on the next poll.");
            return;
        }

        foreach (var notification in pending)
        {
            if (notification.People.Count == 0)
            {
                logger.LogWarning("Skipping {Kind} notification for {ForDate}: no people.", notification.Kind, notification.ForDate);
                continue;
            }

            // Entries from a previous day can never match again; drop them so the set can't grow.
            _awaitingAck.RemoveWhere(e => e.ForDate < notification.ForDate);
            var key = (notification.Kind, notification.ForDate);

            try
            {
                if (!_awaitingAck.Contains(key))
                {
                    await sender.SendAsync(BirthdayMessageFormatter.Format(notification), ct);
                    _awaitingAck.Add(key);
                }

                await api.MarkSentAsync(notification.Kind, notification.ForDate, ct);
                _awaitingAck.Remove(key);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Failed to deliver {Kind} notification for {ForDate}; will retry on the next poll.",
                    notification.Kind, notification.ForDate);
            }
        }
    }
}
