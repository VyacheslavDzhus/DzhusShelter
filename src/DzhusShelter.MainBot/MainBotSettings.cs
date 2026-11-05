namespace DzhusShelter.MainBot;

public sealed class MainBotSettings
{
    public long ChatId { get; init; }
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMinutes(5);
}
