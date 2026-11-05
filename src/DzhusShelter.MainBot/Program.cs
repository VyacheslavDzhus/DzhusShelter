using DzhusShelter.MainBot;
using DzhusShelter.MainBot.Birthdays;
using DzhusShelter.MainBot.Telegram;
using global::Telegram.Bot;

// Note: Host.CreateApplicationBuilder loads user secrets only when DOTNET_ENVIRONMENT=Development.
var builder = Host.CreateApplicationBuilder(args);

var config = MainBotConfig.Read(builder.Configuration);

builder.Services.AddSingleton(config.Settings);
builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(config.BotToken));
builder.Services.AddSingleton<INotificationSender, TelegramNotificationSender>();
builder.Services.AddHttpClient<IBirthdayNotificationsApi, BirthdayNotificationsApiClient>(client =>
{
    client.BaseAddress = new Uri(config.ApiBaseUrl);
    client.DefaultRequestHeaders.Add("X-Api-Key", config.ApiKey);
});
builder.Services.AddHostedService<BirthdayNotificationService>();

builder.Build().Run();
