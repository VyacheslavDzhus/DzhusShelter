using DzhusShelter.TelegramBot;
using Telegram.Bot;

var builder = Host.CreateApplicationBuilder(args);

var botToken = builder.Configuration["Telegram:BotToken"]
    ?? throw new InvalidOperationException("Telegram:BotToken is not configured.");
var allowedChatId = builder.Configuration.GetValue<long?>("Telegram:AllowedChatId")
    ?? throw new InvalidOperationException("Telegram:AllowedChatId is not configured.");
var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException("Api:BaseUrl is not configured.");

builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(botToken));
builder.Services.AddHttpClient<BadHabitsApiClient>(client => client.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddSingleton(new TelegramBotSettings { AllowedChatId = allowedChatId });
builder.Services.AddHostedService<BotHostedService>();

var host = builder.Build();
host.Run();
