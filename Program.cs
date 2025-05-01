using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Relict_TelegramBot_Stride.BotControllers;
using Relict_TelegramBot_Stride.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemoryCache();

builder.Services.AddHttpClient<AlertApi>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"]!);
    c.DefaultRequestHeaders.Add("X-API-KEY", builder.Configuration["Api:ApiKey"]);
});

builder.Services.AddSingleton<IBotService, BotService>();
builder.Services.AddHostedService<BotHostedService>();

builder.Services.AddControllers();

var app = builder.Build();


app.MapControllers();

await app.RunAsync();
