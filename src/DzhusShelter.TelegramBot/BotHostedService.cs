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
    private const string StartLoggingButtonText = "▶️ Почати фіксування";

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
        if (update.Message is { Text: "/start" or StartLoggingButtonText } message && message.Chat.Id == _allowedChatId)
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
        // Sent as a *reply* keyboard (not inline) so it stays pinned below the text input across
        // the whole conversation, not just attached to this one message — the point is to give
        // the user a persistent "start" button so they never have to type /start again.
        var persistentKeyboard = new ReplyKeyboardMarkup(new KeyboardButton(StartLoggingButtonText))
        {
            ResizeKeyboard = true,
            IsPersistent = true,
        };
        await _botClient.SendMessage(
            chatId,
            $"Кнопка \"{StartLoggingButtonText}\" тепер завжди під рукою — більше не треба писати /start.",
            replyMarkup: persistentKeyboard,
            cancellationToken: cancellationToken);

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
                    BadHabitsKeyboard.DisplayNameWithEmoji(subType), BadHabitsKeyboard.SubTypeCallbackData(habitType.Value, subType)))
                .ToArray();
            // Chunk into rows of 3 so long Cyrillic labels (e.g. all 7 alcohol subtypes) don't
            // get crammed into a single unreadable row on a phone screen.
            var keyboard = new InlineKeyboardMarkup(buttons.Chunk(3));

            await _botClient.AnswerCallbackQuery(callbackId, cancellationToken: cancellationToken);
            await _botClient.SendMessage(chatId, "Який саме?", replyMarkup: keyboard, cancellationToken: cancellationToken);
            return;
        }

        var subTypeSelection = BadHabitsKeyboard.TryParseSubType(data);
        if (subTypeSelection is not null)
        {
            var (parsedHabitType, subType) = subTypeSelection.Value;
            var outcome = await _apiClient.LogEntryAsync(parsedHabitType, subType, cancellationToken);
            var label = BadHabitsKeyboard.DisplayNameWithEmoji(subType);

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
