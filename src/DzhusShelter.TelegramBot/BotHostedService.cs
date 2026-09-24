using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DzhusShelter.TelegramBot;

public sealed class TelegramBotSettings
{
    public required long AllowedChatId { get; init; }
}

public sealed class BotHostedService : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly BadHabitsApiClient _apiClient;
    private readonly long _allowedChatId;
    private readonly ILogger<BotHostedService> _logger;

    public BotHostedService(
        ITelegramBotClient botClient,
        BadHabitsApiClient apiClient,
        TelegramBotSettings settings,
        ILogger<BotHostedService> logger)
    {
        _botClient = botClient;
        _apiClient = apiClient;
        _allowedChatId = settings.AllowedChatId;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _botClient.StartReceiving(HandleUpdateAsync, HandlePollingErrorAsync, cancellationToken: stoppingToken);
        _logger.LogInformation("Telegram bot started long polling.");
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is { Text: "/start" } message && message.Chat.Id == _allowedChatId)
        {
            await SendRootMenuAsync(message.Chat.Id, cancellationToken);
            return;
        }

        if (update.CallbackQuery is { Data: { } data, Message.Chat.Id: var chatId } callback && chatId == _allowedChatId)
        {
            await HandleCallbackAsync(callback.Id, chatId, data, cancellationToken);
        }
    }

    private async Task SendRootMenuAsync(long chatId, CancellationToken cancellationToken)
    {
        var keyboard = new InlineKeyboardMarkup(
            InlineKeyboardButton.WithCallbackData("🚫 Шкідливі звички", BadHabitsKeyboard.OpenMenuCallbackData));

        await _botClient.SendMessage(chatId, "Що фіксуємо?", replyMarkup: keyboard, cancellationToken: cancellationToken);
    }

    private async Task SendHabitTypeMenuAsync(long chatId, CancellationToken cancellationToken)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🚬 Куріння", BadHabitsKeyboard.HabitTypeCallbackData(HabitType.Smoking)),
                InlineKeyboardButton.WithCallbackData("🍺 Алкоголь", BadHabitsKeyboard.HabitTypeCallbackData(HabitType.Alcohol)),
            },
        });

        await _botClient.SendMessage(chatId, "Яка звичка?", replyMarkup: keyboard, cancellationToken: cancellationToken);
    }

    private async Task HandleCallbackAsync(string callbackId, long chatId, string data, CancellationToken cancellationToken)
    {
        if (data == BadHabitsKeyboard.OpenMenuCallbackData)
        {
            await _botClient.AnswerCallbackQuery(callbackId, cancellationToken: cancellationToken);
            await SendHabitTypeMenuAsync(chatId, cancellationToken);
            return;
        }

        var habitType = BadHabitsKeyboard.TryParseHabitType(data);
        if (habitType is not null)
        {
            var buttons = BadHabitsKeyboard.SubTypesFor(habitType.Value)
                .Select(subType => InlineKeyboardButton.WithCallbackData(
                    BadHabitsKeyboard.DisplayName(subType), BadHabitsKeyboard.SubTypeCallbackData(habitType.Value, subType)))
                .ToArray();
            var keyboard = new InlineKeyboardMarkup(buttons);

            await _botClient.AnswerCallbackQuery(callbackId, cancellationToken: cancellationToken);
            await _botClient.SendMessage(chatId, "Який саме?", replyMarkup: keyboard, cancellationToken: cancellationToken);
            return;
        }

        var subTypeSelection = BadHabitsKeyboard.TryParseSubType(data);
        if (subTypeSelection is not null)
        {
            var (parsedHabitType, subType) = subTypeSelection.Value;
            var outcome = await _apiClient.LogEntryAsync(parsedHabitType, subType, cancellationToken);
            var label = BadHabitsKeyboard.DisplayName(subType);

            var message = outcome switch
            {
                LogEntryOutcome.Logged => $"Записано: {label}",
                LogEntryOutcome.AlreadyLogged => $"Вже зафіксовано на сьогодні: {label}",
                _ => "Не вдалося записати — спробуй ще раз.",
            };

            await _botClient.AnswerCallbackQuery(callbackId, cancellationToken: cancellationToken);
            await _botClient.SendMessage(chatId, message, cancellationToken: cancellationToken);
        }
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Telegram polling error from {Source}", source);
        return Task.CompletedTask;
    }
}
