using DzhusShelter.MainBot;
using DzhusShelter.MainBot.Birthdays;
using DzhusShelter.MainBot.Telegram;
using global::Telegram.Bot;

// Note: Host.CreateApplicationBuilder loads user secrets only when DOTNET_ENVIRONMENT=Development.
var builder = Host.CreateApplicationBuilder(args);

var botToken = builder.Configuration["Telegram:BotToken"];
if (string.IsNullOrWhiteSpace(botToken))
    throw new InvalidOperationException("Telegram:BotToken is not configured.");
var chatId = builder.Configuration.GetValue<long?>("Telegram:ChatId")
    ?? throw new InvalidOperationException("Telegram:ChatId is not configured.");
var apiBaseUrl = builder.Configuration["Api:BaseUrl"];
if (string.IsNullOrWhiteSpace(apiBaseUrl))
    throw new InvalidOperationException("Api:BaseUrl is not configured.");
var apiKey = builder.Configuration["Api:ApiKey"];
if (string.IsNullOrWhiteSpace(apiKey))
    throw new InvalidOperationException("Api:ApiKey is not configured.");
var pollInterval = builder.Configuration.GetValue<TimeSpan?>("MainBot:PollInterval") ?? TimeSpan.FromMinutes(5);
if (pollInterval <= TimeSpan.Zero)
    throw new InvalidOperationException("MainBot:PollInterval must be positive.");

builder.Services.AddSingleton(new MainBotSettings { ChatId = chatId, PollInterval = pollInterval });
builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(botToken));
builder.Services.AddSingleton<INotificationSender, TelegramNotificationSender>();
builder.Services.AddHttpClient<IBirthdayNotificationsApi, BirthdayNotificationsApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
});
builder.Services.AddHostedService<BirthdayNotificationService>();

builder.Build().Run();
