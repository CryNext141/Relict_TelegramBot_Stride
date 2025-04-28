using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace Relict_TelegramBot_Stride.BotControllers;

public class BotHostedService : BackgroundService
{
    private readonly IBotService _svc;

    public BotHostedService(IBotService svc) => _svc = svc;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {

        Task HandleUpdate(
            ITelegramBotClient _,
            Update update,
            CancellationToken token)
            => _svc.HandleUpdate(update, token);

        Task HandleError(
            ITelegramBotClient _,
            Exception ex,
            CancellationToken token)
        {
            Console.WriteLine(ex);         
            return Task.CompletedTask;
        }

        _svc.Client.StartReceiving(
            updateHandler: HandleUpdate,
            errorHandler: HandleError,
            receiverOptions: new ReceiverOptions(),
            cancellationToken: ct);

        Console.WriteLine("Bot started…");
        await Task.Delay(Timeout.Infinite, ct);
    }
}
