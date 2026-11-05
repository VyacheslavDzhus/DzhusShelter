using global::Telegram.Bot;

namespace DzhusShelter.MainBot.Telegram;

public sealed class TelegramNotificationSender(ITelegramBotClient bot, MainBotSettings settings) : INotificationSender
{
    public async Task SendAsync(string text, CancellationToken ct) =>
        await bot.SendMessage(settings.ChatId, text, cancellationToken: ct);
}
