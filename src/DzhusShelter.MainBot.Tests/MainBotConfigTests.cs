using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace DzhusShelter.MainBot.Tests;

public class MainBotConfigTests
{
    private static IConfiguration Config(Action<Dictionary<string, string?>>? tweak = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Telegram:BotToken"] = "token",
            ["Telegram:ChatId"] = "123456",
            ["Api:BaseUrl"] = "http://api",
            ["Api:ApiKey"] = "key",
        };
        tweak?.Invoke(values);
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void Read_ValidConfig_ReturnsValuesAndDefaultPollInterval()
    {
        var c = MainBotConfig.Read(Config());

        c.Settings.ChatId.Should().Be(123456);
        c.Settings.PollInterval.Should().Be(TimeSpan.FromMinutes(5));
        c.ApiKey.Should().Be("key");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-5")]
    public void Read_MissingZeroOrNegativeChatId_Throws(string? chatId)
    {
        var act = () => MainBotConfig.Read(Config(v => v["Telegram:ChatId"] = chatId));

        act.Should().Throw<InvalidOperationException>().WithMessage("Telegram:ChatId is not configured.");
    }

    [Theory]
    [InlineData("Telegram:BotToken")]
    [InlineData("Api:ApiKey")]
    [InlineData("Api:BaseUrl")]
    public void Read_EmptyOrWhitespaceRequiredValue_Throws(string key)
    {
        var act = () => MainBotConfig.Read(Config(v => v[key] = "  "));

        act.Should().Throw<InvalidOperationException>().WithMessage($"{key} is not configured.");
    }
}
