namespace DzhusShelter.MainBot.Telegram;

public interface INotificationSender
{
    Task SendAsync(string text, CancellationToken ct);
}
