namespace DzhusShelter.MainBot;

public sealed record MainBotConfig(string BotToken, string ApiBaseUrl, string ApiKey, MainBotSettings Settings)
{
    /// <summary>Reads and validates required configuration; throws <see cref="InvalidOperationException"/> if anything is missing.</summary>
    public static MainBotConfig Read(IConfiguration configuration)
    {
        var botToken = Required(configuration, "Telegram:BotToken");
        var apiBaseUrl = Required(configuration, "Api:BaseUrl");
        var apiKey = Required(configuration, "Api:ApiKey");

        // A private chat ID is a positive user ID, so null, 0 and negatives all mean "not configured".
        var chatId = configuration.GetValue<long?>("Telegram:ChatId");
        if (chatId is null or <= 0)
            throw new InvalidOperationException("Telegram:ChatId is not configured.");

        var pollInterval = configuration.GetValue<TimeSpan?>("MainBot:PollInterval") ?? TimeSpan.FromMinutes(5);
        if (pollInterval <= TimeSpan.Zero)
            throw new InvalidOperationException("MainBot:PollInterval must be positive.");

        return new MainBotConfig(botToken, apiBaseUrl, apiKey,
            new MainBotSettings { ChatId = chatId.Value, PollInterval = pollInterval });
    }

    private static string Required(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{key} is not configured.")
            : value;
    }
}
