using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Relict_TelegramBot_Stride.MenuButtons;
using Relict_TelegramBot_Stride.Models;
using System.Collections.Concurrent;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Relict_TelegramBot_Stride.BotControllers
{
    public interface IBotService
    {
        TelegramBotClient Client { get; }
        Task HandleUpdate(Update update, CancellationToken ct);
        Task SendStart(long chatId, CancellationToken ct);
    }

    public class BotService : IBotService
    {
        public TelegramBotClient Client { get; }
        private readonly AlertApi _api;
        private readonly IMemoryCache _cache;
        private readonly ConcurrentDictionary<long, int> _positions = new();

        public BotService(IConfiguration cfg, AlertApi api, IMemoryCache cache)
        {
            Client = new TelegramBotClient(cfg["Telegram:BotToken"]!);
            _api = api;
            _cache = cache;
        }

        private static readonly Dictionary<string, string> GenderUa = new()
        {
            ["Male"] = "Чоловіча",
            ["Female"] = "Жіноча",
            ["Unknown"] = "Невідома"
        };
        private static readonly Dictionary<string, string> SkinUa = new()
        {
            ["Light"] = "Світлий",
            ["Medium"] = "Середній",
            ["Dark"] = "Темний",
            ["Unknown"] = "Невідомий"
        };

        public async Task HandleUpdate(Update update, CancellationToken ct)
        {
            switch (update)
            {
                case { Message: { Text: "/start" } m }:
                    {
                        var chatId = m.Chat.Id;

                        if (_cache.TryGetValue($"menu_{chatId}", out int oldMenu))
                            await SafeDelete(Client, chatId, oldMenu, ct);

                        var menu = await Client.SendMessage(
                            chatId,
                            "Головне меню:",
                            replyMarkup: InlineMenus.MainMenu(),
                            cancellationToken: ct);

                        _cache.Set($"menu_{chatId}", menu.MessageId);
                        return;
                    }

                case { CallbackQuery: { } cb }:
                    await HandleCallback(cb, ct);
                    break;
            }
        }

        public Task SendStart(long chatId, CancellationToken ct) =>
            Client.SendMessage(chatId, "Головне меню:", replyMarkup: InlineMenus.MainMenu(), cancellationToken: ct);


        private static async Task SafeDelete(ITelegramBotClient bot, long chatId, int msgId, CancellationToken ct)
        {
            try
            {
                await bot.DeleteMessage(chatId, msgId, ct);
            }

            catch (ApiRequestException e) when (e.ErrorCode is 400 or 404)
            { }
        }

        private async Task HandleCallback(CallbackQuery cb, CancellationToken ct)
        {
            var chatId = cb.Message!.Chat.Id;

            if (cb.Data == "menu")
            {
                if (_cache.TryGetValue($"album_{chatId}", out List<int>? album))
                    foreach (var id in album)
                        await SafeDelete(Client, chatId, id, ct);

                if (_cache.TryGetValue($"text_{chatId}", out int txt))
                    await SafeDelete(Client, chatId, txt, ct);

                if (_cache.TryGetValue($"menu_{chatId}", out int oldMenu))
                    await SafeDelete(Client, chatId, oldMenu, ct);

                var menuMsg = await Client.SendMessage(
                    chatId,
                    "Головне меню:",
                    replyMarkup: InlineMenus.MainMenu(),
                    cancellationToken: ct);

                _cache.Set($"menu_{chatId}", menuMsg.MessageId);
                _cache.Remove($"album_{chatId}");
                _cache.Remove($"text_{chatId}");
                _cache.Remove($"alerts_{chatId}");
                _positions.TryRemove(chatId, out _);

                await Client.AnswerCallbackQuery(cb.Id, cancellationToken: ct);
                return;
            }

            if (cb.Data == "menu_active")
            {
                if (_cache.TryGetValue($"menu_{chatId}", out int oldMenu))
                    await SafeDelete(Client, chatId, oldMenu, ct);
                _cache.Remove($"menu_{chatId}");

                var list = (await _api.GetAllAsync(ct))
                           .Where(a => a.AlertStatusId == 1)
                           .ToList();

                _cache.Set($"alerts_{chatId}", list, TimeSpan.FromMinutes(5));
                _positions[chatId] = 0;
            }

            if (!_cache.TryGetValue($"alerts_{chatId}", out List<AlertResponse>? alerts) || alerts.Count == 0)
            {
                await Client.AnswerCallbackQuery(cb.Id, "Немає активних алертів", cancellationToken: ct);
                return;
            }

            var pos = _positions.GetOrAdd(chatId, 0);
            pos = cb.Data switch
            {
                "next" => (pos + 1) % alerts.Count,
                "prev" => (pos - 1 + alerts.Count) % alerts.Count,
                _ => pos
            };
            _positions[chatId] = pos;

            if (_cache.TryGetValue($"album_{chatId}", out List<int>? oldAlbum))
                foreach (var id in oldAlbum)
                    await SafeDelete(Client, chatId, id, ct);

            if (_cache.TryGetValue($"text_{chatId}", out int oldTxt))
                await SafeDelete(Client, chatId, oldTxt, ct);

            await ShowAlert(chatId, cb.Message!.MessageId, alerts[pos], ct);
            await Client.AnswerCallbackQuery(cb.Id, cancellationToken: ct);
        }



        private static InputFile Base64ToInput(string? b64, string fileName)
        {
            if (string.IsNullOrWhiteSpace(b64))
                b64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR4nGNgYAAAAAMAASsJTYQAAAAASUVORK5CYII=";

            byte[] bytes = Convert.FromBase64String(b64);
            return InputFile.FromStream(new MemoryStream(bytes), fileName);
        }

        private async Task ShowAlert(long chatId, int _, AlertResponse a, CancellationToken ct)
        {
            var caption = BuildCaption(a);

            var albumMsg = await Client.SendMediaGroup(
                chatId,
                new[]
                {
            new InputMediaPhoto(Base64ToInput(a.Victim?.VictimPhoto,   "victim.jpg")),
            new InputMediaPhoto(Base64ToInput(a.Abductor?.AbductorPhoto,"abductor.jpg"))
                },
                cancellationToken: ct);

            var txt = await Client.SendMessage(
                chatId,
                caption,
                parseMode: ParseMode.Markdown,
                replyMarkup: InlineMenus.Nav(),
                cancellationToken: ct);

            _cache.Set($"album_{chatId}", albumMsg.Select(m => m.MessageId).ToList());
            _cache.Set($"text_{chatId}", txt.MessageId);
        }







        private string BuildCaption(AlertResponse a)
        {
            static string Map(Dictionary<string, string> dict, string? key, string @default = "Невідомо") =>
                key is not null && dict.TryGetValue(key, out var val) ? val : @default;

            var sb = new StringBuilder();

            sb.AppendLine($"*Алерт #{a.AlertId}* `{a.AlertStatus}`");
            sb.AppendLine($"📅 {a.CrimeDate?.Date ?? "??.??.????"}  ⏰ {a.CrimeDate?.Time ?? "--:--"}");
            sb.AppendLine($"📍 {a.CrimeDistrict}, {a.CrimeLocation}");
            sb.AppendLine();

            if (a.Victim is { } v)
            {
                sb.AppendLine("*👧 Дитина*");
                sb.AppendLine($"• Ім’я: {v.VictimName ?? "Невідомо"}");
                sb.AppendLine($"• Вік: {v.VictimAge}");
                sb.AppendLine($"• Стать: {Map(GenderUa, v.VictimGender)}");
                sb.AppendLine($"• Колір шкіри: {Map(SkinUa, v.VictimSkinColor)}");
                sb.AppendLine($"• Волосся: {v.VictimHair ?? "Невідомо"}");
                sb.AppendLine($"• Одяг: {v.VictimClothing ?? "Невідомо"}");
                sb.AppendLine($"• Особливі прикмети: {v.VictimDistinctiveFeatures ?? "—"}");
                sb.AppendLine();
            }

            if (a.Abductor is { } ab)
            {
                sb.AppendLine("*👤 Викрадач*");
                sb.AppendLine($"• Ім’я: {ab.AbductorName ?? "Невідомо"}");
                sb.AppendLine($"• Вік: {ab.AbductorAge}");
                sb.AppendLine($"• Стать: {Map(GenderUa, ab.AbductorGender)}");
                sb.AppendLine($"• Колір шкіри: {Map(SkinUa, ab.AbductorSkinColor)}");
                sb.AppendLine($"• Волосся: {ab.AbductorHair ?? "Невідомо"}");
                sb.AppendLine($"• Одяг: {ab.AbductorClothing ?? "Невідомо"}");
                sb.AppendLine($"• Особливі прикмети: {ab.AbductorDistinctiveFeatures ?? "—"}");
                sb.AppendLine($"• Транспорт: {ab.AbductorVehicle ?? "Невідомо"}");
            }

            return sb.ToString();
        }




    }
}
