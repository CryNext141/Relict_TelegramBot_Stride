using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Relict_TelegramBot_Stride.BotControllers;
using Relict_TelegramBot_Stride.Models;
using System.Net.Http.Headers;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) =>
    {
        services.AddMemoryCache();

        services.AddHttpClient<AlertApi>(c =>
        {
            c.BaseAddress = new Uri(ctx.Configuration["Api:BaseUrl"]!);
        c.DefaultRequestHeaders.Add("X-API-KEY", ctx.Configuration["Api:ApiKey"]);
        });

        services.AddSingleton<IBotService, BotService>();
        services.AddHostedService<BotHostedService>();
    })
    .Build();

await host.RunAsync();
