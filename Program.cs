using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Relict_TelegramBot_Stride.BotControllers;
using Relict_TelegramBot_Stride.Models;
using Telegram.Bot;



var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemoryCache();

builder.Services.AddHttpClient<AlertApi>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"]!);
    c.DefaultRequestHeaders.Add("X-API-KEY", builder.Configuration["Api:ApiKey"]);
});

builder.Services.AddHttpClient<NotificationsApi>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Api:KisenBaseUrl"]!);
});

builder.Services.AddSingleton<IBotService, BotService>();
builder.Services.AddHostedService<BotHostedService>();

builder.Services.AddControllers();

var Client = new TelegramBotClient(builder.Configuration["Telegram:BotToken"]!);


await Client.SetMyCommands(new[]
{
    new Telegram.Bot.Types.BotCommand { Command = "exit", Description = "Повернутись в головне меню" },
    new Telegram.Bot.Types.BotCommand { Command = "restart", Description = "Перезапустити бота" }
});

var app = builder.Build();

app.MapControllers();

await app.RunAsync();
