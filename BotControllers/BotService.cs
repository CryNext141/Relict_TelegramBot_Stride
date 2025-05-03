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
using Telegram.Bot.Types.ReplyMarkups;

namespace Relict_TelegramBot_Stride.BotControllers
{
    public interface IBotService
    {
        TelegramBotClient Client { get; }
        Task HandleUpdate(Update update, CancellationToken ct);
        Task SendAlertNotification(long chatId, int alertId, string text);
        Task SendStart(long chatId, CancellationToken ct);
    }

    public class BotService : IBotService
    {
        public TelegramBotClient Client { get; }
        private readonly AlertApi _api;
        private readonly NotificationsApi _notificationsApi;
        private readonly IMemoryCache _cache;
        private readonly ConcurrentDictionary<long, int> _positions = new();
        private readonly ConcurrentDictionary<long, ReportSession> _reports = new();
        private readonly ConcurrentDictionary<long, SubSession> _subs = new();
        private readonly ConcurrentDictionary<long, MySession> _my = new();

        public BotService(IConfiguration cfg, AlertApi api, NotificationsApi notificationsApi, IMemoryCache cache)
        {
            Client = new TelegramBotClient(cfg["Telegram:BotToken"]!);
            _api = api;
            _cache = cache;
            _notificationsApi = notificationsApi;
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

        private static readonly Dictionary<string, string> RegionUa = new()
        {
            ["Kyiv"] = "Київ",
            ["Kharkiv"] = "Харків",
            ["Odesa"] = "Одеса",
            ["Dnipro"] = "Дніпро",
            ["Donetsk"] = "Донецьк",
            ["Lviv"] = "Львів",
            ["Zaporizhzhia"] = "Запоріжжя",
            ["Kryvyi Rih"] = "Кривий Ріг",
            ["Mykolaiv"] = "Миколаїв",
            ["Mariupol"] = "Маріуполь",
            ["Luhansk"] = "Луганськ",
            ["Vinnytsia"] = "Вінниця",
            ["Sevastopol"] = "Севастополь",
            ["Simferopol"] = "Сімферополь",
            ["Kherson"] = "Херсон",
            ["Poltava"] = "Полтава",
            ["Chernihiv"] = "Чернігів",
            ["Cherkasy"] = "Черкаси",
            ["Zhytomyr"] = "Житомир",
            ["Sumy"] = "Суми",
            ["Khmelnytskyi"] = "Хмельницький",
            ["Chernivtsi"] = "Чернівці",
            ["Rivne"] = "Рівне",
            ["Ivano-Frankivsk"] = "Івано-Франківськ",
            ["Kropyvnytskyi"] = "Кропивницький",
            ["Kamianske"] = "Кам'янське",
            ["Lutsk"] = "Луцьк",
            ["Kremenchuk"] = "Кременчук",
            ["Bila Tserkva"] = "Біла Церква",
            ["Melitopol"] = "Мелітополь"
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

                case { Message: { } msg } when _reports.ContainsKey(msg.Chat.Id) &&
                                       (msg.Type == MessageType.Text || msg.Type == MessageType.Contact || msg.Type == MessageType.Location):
                    await HandleWizardMessage(msg, ct);
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

        private async Task AskPhone(long chatId, CancellationToken ct)
        {
            var kb = new ReplyKeyboardMarkup(
                new[]
                {
                    new[] { KeyboardButton.WithRequestContact("📞 Поділитись контактом") },
                    new[] { new KeyboardButton("⬅️ Назад"), new KeyboardButton("❌ Скасувати") }
                })
            { OneTimeKeyboard = true, ResizeKeyboard = true };

            var q = await Client.SendMessage(chatId,
             "Номер телефону (+380…):",
             replyMarkup: kb, cancellationToken: ct);

            if (_reports.TryGetValue(chatId, out var sess))
            {
                sess.MessageIdsToCleanup.Add(q.MessageId);
            }


        }

        private async Task AskLocation(long chatId, CancellationToken ct)
        {
            var kb = new ReplyKeyboardMarkup(
                new[]
                {
                        new[] { KeyboardButton.WithRequestLocation("📍 Поділитись геолокацією") },
                        new[] { new KeyboardButton("⬅️ Назад"), new KeyboardButton("❌ Скасувати") }
                })
            { OneTimeKeyboard = true, ResizeKeyboard = true };

            var q = await Client.SendMessage(chatId,
                "Введіть місце події або надішліть геолокацію:",
                replyMarkup: kb, cancellationToken: ct);

            if (_reports.TryGetValue(chatId, out var sess))
            {
                sess.MessageIdsToCleanup.Add(q.MessageId);
            }

        }

        private InlineKeyboardMarkup BuildRegionsKeyboard(IReadOnlyList<RegionDto> regions, SubSession s)
        {
            const int pageSize = 6;
            int start = s.CurrentPage * pageSize;
            var page = regions.Skip(start).Take(pageSize).ToList();

            var rows = new List<IEnumerable<InlineKeyboardButton>>();

            foreach (var r in page)
            {
                bool orig = s.Original.Contains(r.RegionId);
                bool add = s.Selected.Contains(r.RegionId);
                bool chosen = orig || add;

                string ua = RegionUa.TryGetValue(r.Name, out var t) ? t : r.Name;
                string mark = chosen ? "✅" : "☑️";

                rows.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData($"{mark} {ua}", $"reg_sel:{r.RegionId}")
                });
            }

            var nav = new List<InlineKeyboardButton>();
            if (start > 0) nav.Add(InlineKeyboardButton.WithCallbackData("◀️", "reg_prev"));
            if (start + pageSize < regions.Count) nav.Add(InlineKeyboardButton.WithCallbackData("▶️", "reg_next"));
            if (nav.Any()) rows.Add(nav);

            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Підписатися", "reg_sub"),
                InlineKeyboardButton.WithCallbackData("❌ Скасувати",   "reg_cancel")
            });

            return new(rows);
        }

        private InlineKeyboardMarkup BuildMyKeyboard(IReadOnlyList<RegionDto> regions, MySession s, IEnumerable<int> subscribed)
        {
            const int pageSize = 6;
            int start = s.CurrentPage * pageSize;
            var page = regions.Where(r => subscribed.Contains(r.RegionId))
                               .Skip(start).Take(pageSize).ToList();

            var rows = new List<InlineKeyboardButton[]>();

            foreach (var r in page)
            {
                bool chosen = s.Selected.Contains(r.RegionId);
                string ua = RegionUa.TryGetValue(r.Name, out var t) ? t : r.Name;
                rows.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData($"{(chosen ? "✅" : "☑️")} {ua}", $"my_sel:{r.RegionId}")
                });
            }

            if (rows.Count == 0)
                rows.Add(new[] { InlineKeyboardButton.WithCallbackData("— список порожній —", "noop") });

            var subsList = subscribed.ToList();
            if (subsList.Count > pageSize)
            {
                var nav = new List<InlineKeyboardButton>();
                if (start > 0) nav.Add(InlineKeyboardButton.WithCallbackData("◀️", "my_prev"));
                if (start + pageSize < subsList.Count) nav.Add(InlineKeyboardButton.WithCallbackData("▶️", "my_next"));
                if (nav.Any()) rows.Add(nav.ToArray());
            }

            return InlineMenus.MyNav(rows);
        }

        private async Task HandleWizardMessage(Message msg, CancellationToken ct)
        {
            var chatId = msg.Chat.Id;
            if (!_reports.TryGetValue(chatId, out var sess)) return;

            var txt = msg.Text?.Trim();

            if (msg.Text is "❌ Скасувати")
            {
                ReportSession? sessionToCancel = null;

                if (_reports.TryGetValue(chatId, out sessionToCancel))
                {
                    await CleanupKeyboardMessages(chatId, sessionToCancel.MessageIdsToCleanup, ct);
                }

                _reports.TryRemove(chatId, out _);
                await Client.SendMessage(chatId, "Заповнення скасоване.",
                             replyMarkup: new ReplyKeyboardRemove(),
                             cancellationToken: ct);


                string back = sessionToCancel?.FromPush == true
                      ? $"alert_back:{sessionToCancel.OriginMessageId}"
                      : "menu_active";

                await HandleCallback(new CallbackQuery { Message = msg, Data = back }, ct, false);

                return;
            }

            if (msg.Text is "⬅️ Назад")
            {
                await HandleCallback(new CallbackQuery { Message = msg, Data = "reg_prev" }, ct, false);
                return;
            }

            if (txt == "❌ Скасувати")
            {
                _subs.TryRemove(chatId, out _);
                await Client.SendMessage(chatId, "Підписку скасовано.", cancellationToken: ct);
                await HandleCallback(new CallbackQuery { Message = msg, Data = "menu" }, ct, false);
                return;
            }

            if (txt == "⬅️ Назад")
            {
                await HandleCallback(new CallbackQuery { Message = msg, Data = "rep_back" }, ct, false);
                return;
            }

            switch (sess.Step)
            {
                case ReportStep.AskName:
                    if (msg.Type != MessageType.Text || string.IsNullOrWhiteSpace(msg.Text))
                    {
                        await Client.SendMessage(chatId, "Ім'я не може бути порожнім. Будь ласка, введіть ваше ім’я:", replyMarkup: InlineMenus.BackCancel(), cancellationToken: ct);
                        return;
                    }
                    sess.CitizenName = msg.Text.Trim();
                    sess.History.Push(ReportStep.AskName);
                    sess.Step = ReportStep.AskPhone;
                    await AskPhone(chatId, ct);
                    break;

                case ReportStep.AskPhone:
                    string? phoneNumber = null;
                    if (msg.Contact is not null)
                    {
                        phoneNumber = msg.Contact.PhoneNumber;
                    }
                    else if (msg.Type == MessageType.Text && !string.IsNullOrWhiteSpace(msg.Text))
                    {
                        phoneNumber = msg.Text.Trim();
                    }

                    if (phoneNumber is null)
                    {
                        await Client.SendMessage(chatId, "Будь ласка, поділіться контактом або надішліть номер телефону.", cancellationToken: ct);
                        return;
                    }

                    sess.CitizenContactPhone = phoneNumber;
                    sess.History.Push(ReportStep.AskPhone);
                    sess.Step = ReportStep.AskDescription;

                    await Client.SendMessage(chatId,
                         "Номер телефону отримано.",
                         replyMarkup: new ReplyKeyboardRemove(),
                         cancellationToken: ct);

                    var q3 = await Client.SendMessage(chatId,
                    "Опишіть, що ви бачили:",
                    replyMarkup: InlineMenus.BackCancel(), cancellationToken: ct);
                    sess.MessageIdsToCleanup.Add(q3.MessageId);
                    break;

                case ReportStep.AskDescription:
                    sess.Description = msg.Text;
                    sess.History.Push(ReportStep.AskDescription);
                    sess.Step = ReportStep.AskLocation;
                    await AskLocation(chatId, ct);
                    break;

                case ReportStep.AskLocation:
                    if (msg.Location is not null)
                        sess.Location = $"{msg.Location.Latitude},{msg.Location.Longitude}";
                    else
                        sess.Location = msg.Text;

                    var now = DateTime.UtcNow;
                    var payload = new
                    {
                        citizenName = sess.IsAnonymous == true ? "" : sess.CitizenName,
                        citizenContactPhone = sess.IsAnonymous == true ? "" : sess.CitizenContactPhone,
                        location = sess.Location,
                        reportDate = new { date = now.ToString("dd.MM.yyyy"), time = now.ToString("HH\\:mm") },
                        description = sess.Description,
                        isAnonymous = sess.IsAnonymous ?? true
                    };

                    var ok = await _api.PostCitizenReportAsync(sess.AlertId, payload, ct);

                    await CleanupKeyboardMessages(chatId, sess.MessageIdsToCleanup, ct);
                    _reports.TryRemove(chatId, out _);

                    var conf = await Client.SendMessage(chatId,
                           ok ? "✅ Дякуємо! Інформацію надіслано."
                              : "⚠️ Не вдалося надіслати інформацію.",
                           replyMarkup: new ReplyKeyboardRemove(),
                           cancellationToken: ct);

                    _reports.TryRemove(chatId, out _);

                    await HandleCallback(
                        new CallbackQuery { Message = conf, Data = "menu", Id = Guid.NewGuid().ToString() },
                        ct, answer: false);

                    break;
            }
        }

        private async Task HandleCallback(CallbackQuery cb, CancellationToken ct, bool answer = true)
        {
            if (cb.Message is null)
            {
                await Client.AnswerCallbackQuery(cb.Id, "Error: No message context.", cancellationToken: ct);
                return;
            }

            var chatId = cb.Message!.Chat.Id;
            var messageId = cb.Message.MessageId;

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

            if (cb.Data == "sub")
            {
                await Client.EditMessageText(
                    chatId: chatId,
                    messageId: cb.Message!.MessageId,
                    text: "Підпишіться на сповіщення про нові алерти у вибраних містах.",
                    replyMarkup: InlineMenus.SubMenu(),
                    cancellationToken: ct);

                await Client.AnswerCallbackQuery(cb.Id, cancellationToken: ct);
                return;
            }

            if (cb.Data == "reg_page:0")
            {
                var regions = await _notificationsApi.GetRegionsAsync(ct);
                var subscribed = await _notificationsApi.GetUserRegionsAsync(chatId, ct);

                var ss = _subs.GetOrAdd(chatId, _ => new SubSession());
                ss.CurrentPage = 0;
                ss.Selected.Clear();
                ss.Original.Clear();
                foreach (var id in subscribed) ss.Original.Add(id);

                await Client.EditMessageText(
                    chatId, cb.Message!.MessageId,
                    "Оберіть місто (підписки позначено ✅):",
                    replyMarkup: BuildRegionsKeyboard(regions, ss),
                    cancellationToken: ct);

                await Client.AnswerCallbackQuery(cb.Id, cancellationToken: ct);
                return;
            }

            if (cb.Data.StartsWith("reg_sel:", StringComparison.Ordinal))
            {
                var id = int.Parse(cb.Data.Split(':')[1]);
                if (!_subs.TryGetValue(chatId, out var ss)) return;

                if (ss.Original.Contains(id))
                {
                    await Client.AnswerCallbackQuery(cb.Id, "Ви вже підписані на це місто", showAlert: true, cancellationToken: ct);
                    return;
                }

                if (!ss.Selected.Add(id)) ss.Selected.Remove(id);

                var regions = await _notificationsApi.GetRegionsAsync(ct);
                await Client.EditMessageReplyMarkup(
                    chatId, cb.Message!.MessageId,
                    replyMarkup: BuildRegionsKeyboard(regions, ss),
                    cancellationToken: ct);

                await Client.AnswerCallbackQuery(cb.Id, cancellationToken: ct);
                return;
            }

            if (cb.Data is "reg_prev" or "reg_next")
            {
                if (!_subs.TryGetValue(chatId, out var ss)) return;
                var regions = await _notificationsApi.GetRegionsAsync(ct);

                ss.CurrentPage += cb.Data == "reg_prev" ? -1 : 1;
                if (ss.CurrentPage < 0) ss.CurrentPage = 0;
                if (ss.CurrentPage * 6 >= regions.Count) ss.CurrentPage--;

                await Client.EditMessageReplyMarkup(
                    chatId, cb.Message!.MessageId,
                    replyMarkup: BuildRegionsKeyboard(regions, ss),
                    cancellationToken: ct);

                await Client.AnswerCallbackQuery(cb.Id, cancellationToken: ct);
                return;
            }

            if (cb.Data == "reg_cancel")
            {
                _subs.TryRemove(chatId, out _);
                await Client.SendMessage(chatId, "Підписку скасовано.", cancellationToken: ct);
                await HandleCallback(new CallbackQuery { Message = cb.Message, Data = "menu" }, ct, false);
                return;
            }

            if (cb.Data == "reg_sub")
            {
                if (!_subs.TryGetValue(chatId, out var ss) || ss.Selected.Count == 0)
                {
                    await Client.AnswerCallbackQuery(cb.Id, "Оберіть нові міста для підписки", true, cancellationToken: ct);
                    return;
                }

                var newList = ss.Original.Union(ss.Selected).ToList();
                var ok = await _notificationsApi.PostSubscriptionAsync(
                    new SubscribePayload(chatId, newList), ct);

                await Client.EditMessageText(chatId, cb.Message!.MessageId,
                    ok ? "✅ Підписку оновлено." : "⚠️ Не вдалося зберегти підписку.",
                    replyMarkup: InlineMenus.MainMenu(), cancellationToken: ct);

                _subs.TryRemove(chatId, out _);
                await Client.AnswerCallbackQuery(cb.Id, cancellationToken: ct);
                return;
            }

            if (cb.Data == "my_page:0")
            {
                var regions = await _notificationsApi.GetRegionsAsync(ct);
                var subscribed = await _notificationsApi.GetUserRegionsAsync(chatId, ct);

                if (subscribed.Count == 0)
                {
                    await Client.EditMessageText(chatId, cb.Message!.MessageId,
                        "Ви ще не підписані на жодне місто.",
                        replyMarkup: InlineMenus.SubMenu(), cancellationToken: ct);
                    await Client.AnswerCallbackQuery(cb.Id, cancellationToken: ct);
                    return;
                }

                var ms = _my.GetOrAdd(chatId, _ => new MySession());
                ms.CurrentPage = 0; ms.Selected.Clear();

                await Client.EditMessageText(chatId, cb.Message!.MessageId,
                    "Ваші підписки (виберіть, щоб відписатись):",
                    replyMarkup: BuildMyKeyboard(regions, ms, subscribed),
                    cancellationToken: ct);

                await Client.AnswerCallbackQuery(cb.Id, cancellationToken: ct);
                return;
            }

            if (cb.Data.StartsWith("my_sel:", StringComparison.Ordinal))
            {
                if (!_my.TryGetValue(chatId, out var ms)) return;
                int id = int.Parse(cb.Data.Split(':')[1]);

                if (!ms.Selected.Add(id)) ms.Selected.Remove(id);

                var regions = await _notificationsApi.GetRegionsAsync(ct);
                var subscribed = await _notificationsApi.GetUserRegionsAsync(chatId, ct);

                await Client.EditMessageReplyMarkup(
                    chatId, cb.Message!.MessageId,
                    replyMarkup: BuildMyKeyboard(regions, ms, subscribed),
                    cancellationToken: ct);

                await Client.AnswerCallbackQuery(cb.Id, cancellationToken: ct);
                return;
            }

            if (cb.Data is "my_prev" or "my_next")
            {
                if (!_my.TryGetValue(chatId, out var ms)) return;

                var subs = await _notificationsApi.GetUserRegionsAsync(chatId, ct);
                if (subs.Count <= 6) { await Client.AnswerCallbackQuery(cb.Id); return; }

                ms.CurrentPage += cb.Data == "my_prev" ? -1 : 1;
                if (ms.CurrentPage < 0) ms.CurrentPage = 0;
                if (ms.CurrentPage * 6 >= subs.Count) ms.CurrentPage--;

                var regions = await _notificationsApi.GetRegionsAsync(ct);
                await Client.EditMessageReplyMarkup(
                    chatId, cb.Message!.MessageId,
                    replyMarkup: BuildMyKeyboard(regions, ms, subs),
                    cancellationToken: ct);

                await Client.AnswerCallbackQuery(cb.Id, cancellationToken: ct);
                return;
            }

            if (cb.Data == "my_cancel")
            {
                _my.TryRemove(chatId, out _);
                await Client.EditMessageText(chatId, cb.Message!.MessageId,
                    "Підписку не змінено.",
                    replyMarkup: InlineMenus.SubMenu(), cancellationToken: ct);

                await Client.AnswerCallbackQuery(cb.Id, cancellationToken: ct);
                return;
            }

            if (cb.Data == "my_unsub")
            {
                if (!_my.TryGetValue(chatId, out var ms) || ms.Selected.Count == 0)
                {
                    await Client.AnswerCallbackQuery(cb.Id, "Виберіть місто для відписки", true, cancellationToken: ct);
                    return;
                }

                var ok = await _notificationsApi.DeleteUserRegionsAsync(chatId, ms.Selected, ct);

                await Client.EditMessageText(chatId, cb.Message!.MessageId,
                    ok ? "🚫 Вибрані міста відписано." : "⚠️ Не вдалося змінити підписку.",
                    replyMarkup: InlineMenus.MainMenu(), cancellationToken: ct);

                _my.TryRemove(chatId, out _);
                await Client.AnswerCallbackQuery(cb.Id, cancellationToken: ct);
                return;
            }


            if (cb.Data!.StartsWith("alert_detail:", StringComparison.Ordinal))
            {
                int alertId = int.Parse(cb.Data.Split(':')[1]);

                var alert = await _api.GetAlertByIdAsync(alertId, ct);
                if (alert is null)
                {
                    await Client.AnswerCallbackQuery(cb.Id, "Алерт не знайдено", true);
                    return;
                }

                int originId = cb.Message!.MessageId;

                await ShowSingleAlert(chatId, alert, originId, ct);

                await Client.AnswerCallbackQuery(cb.Id, cancellationToken: ct);
                return;
            }

            if (cb.Data.StartsWith("alert_back:", StringComparison.Ordinal))
            {
                int originId = int.Parse(cb.Data.Split(':')[1]);

               

                if (_cache.TryGetValue($"album_{chatId}", out List<int>? alb))
                    foreach (var id in alb)
                        if (id != originId)
                            await SafeDelete(Client, chatId, id, ct);

                if (_cache.TryGetValue($"text_{chatId}", out int detTxt))
                    if (detTxt != originId)
                        await SafeDelete(Client, chatId, detTxt, ct);

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
                alerts = (await _api.GetAllAsync(ct))
                         .Where(a => a.AlertStatusId == 1)
                         .ToList();

                if (alerts.Count == 0)
                {
                    await Client.AnswerCallbackQuery(cb.Id, "Немає активних алертів", cancellationToken: ct);
                    return;
                }

                _cache.Set($"alerts_{chatId}", alerts, TimeSpan.FromMinutes(5));
                _positions[chatId] = 0;
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

            if (cb.Data.StartsWith("report:", StringComparison.Ordinal))
            {
                if (_reports.ContainsKey(chatId))
                {
                    if (answer && !string.IsNullOrEmpty(cb.Id)) await SafeAnswerCallbackQuery(cb.Id, ct, "Ви вже заповнюєте репорт. Завершіть або скасуйте поточний.", showAlert: true);
                    return;
                }

                _reports.TryRemove(chatId, out _);

                var parts = cb.Data.Split(':');       
                int alertId = int.Parse(parts[1]);
                int origin = int.Parse(parts[2]);
                bool fromPush = parts.Length > 3 && parts[3] == "p";

                var sess = new ReportSession
                {
                    AlertId = alertId,
                    OriginMessageId = origin,
                    FromPush = fromPush,
                    MessageIdsToCleanup = new()
                };
                _reports[chatId] = sess;

                var questionMsg = await Client.SendMessage(
                    chatId,
                    "Бажаєте залишитись анонімним?",
                    replyMarkup: InlineMenus.AnonChoice(),
                    cancellationToken: ct);

                sess.MessageIdsToCleanup.Add(questionMsg.MessageId);

                await Client.AnswerCallbackQuery(cb.Id, cancellationToken: ct);
                return;
            }

            if (cb.Data.StartsWith("rep_anon:", StringComparison.Ordinal))
            {
                if (!_reports.TryGetValue(chatId, out var sess)) return;

                await SafeDelete(Client, chatId, cb.Message!.MessageId, ct);

                sess.IsAnonymous = cb.Data.EndsWith("yes");
                sess.History.Push(ReportStep.ChooseAnon);

                Message? sentMessage = null;

                if (sess.IsAnonymous == true)
                {
                    sess.Step = ReportStep.AskDescription;
                    sentMessage = await Client.SendMessage(chatId,
                    "Опишіть, що ви бачили:",
                    replyMarkup: InlineMenus.BackCancel(), cancellationToken: ct);

                    sess.MessageIdsToCleanup.Add(sentMessage.MessageId);

                }
                else
                {
                    sess.Step = ReportStep.AskName;
                    sentMessage = await Client.SendMessage(chatId,
                    "Ваше ім’я:",
                    replyMarkup: InlineMenus.BackCancel(), cancellationToken: ct);
                    sess.MessageIdsToCleanup.Add(sentMessage.MessageId);


                }

                if (sentMessage != null)
                {
                    sess.MessageIdsToCleanup.Add(sentMessage.MessageId);
                }

                await Client.AnswerCallbackQuery(cb.Id, cancellationToken: ct);
                return;
            }

            if (cb.Data == "rep_cancel")
            {
                if (_reports.TryRemove(chatId, out var toCancel))
                    await CleanupKeyboardMessages(chatId, toCancel.MessageIdsToCleanup, ct);

                await SafeDelete(Client, chatId, cb.Message!.MessageId, ct);

                await Client.SendMessage(chatId, "Заповнення скасоване.",
                    replyMarkup: new ReplyKeyboardRemove(), cancellationToken: ct);

                int origin = toCancel?.OriginMessageId ?? -1;

                string back = toCancel?.FromPush == true
                      ? $"alert_back:{toCancel.OriginMessageId}"   
                      : "menu_active";

                await HandleCallback(
                    new CallbackQuery { Message = cb.Message, Data = back },
                    ct, answer: false);

                return;
            }

            if (cb.Data == "rep_back")
            {
                if (!_reports.TryGetValue(chatId, out var sess) || sess.History.Count == 0)
                    return;

                sess.Step = sess.History.Pop();

                switch (sess.Step)
                {
                    case ReportStep.ChooseAnon:
                        var q0 = await Client.SendMessage(
                            chatId, "Бажаєте залишитись анонімним?",
                            replyMarkup: InlineMenus.AnonChoice(), cancellationToken: ct);
                        sess.MessageIdsToCleanup.Add(q0.MessageId);
                        break;
                    case ReportStep.AskName:
                        var q1 = await Client.SendMessage(chatId,
                        "Ваше ім’я:",
                        replyMarkup: InlineMenus.BackCancel(), cancellationToken: ct);
                        sess.MessageIdsToCleanup.Add(q1.MessageId);
                        break;
                    case ReportStep.AskPhone:
                        await AskPhone(chatId, ct);
                        break;
                    case ReportStep.AskDescription:
                        var q3 = await Client.SendMessage(chatId,
                        "Опишіть, що ви бачили:",
                        replyMarkup: InlineMenus.BackCancel(), cancellationToken: ct);
                        sess.MessageIdsToCleanup.Add(q3.MessageId);
                        break;
                    case ReportStep.AskLocation:
                        await AskLocation(chatId, ct);
                        break;
                }



                await Client.AnswerCallbackQuery(cb.Id, cancellationToken: ct);
                return;
            }

            await ShowAlert(chatId, cb.Message!.MessageId, alerts[pos], ct, alerts.Count);
            if (answer && !string.IsNullOrEmpty(cb.Id))
                await Client.AnswerCallbackQuery(cb.Id, cancellationToken: ct);
        }


        private async Task ShowSingleAlert(long chatId, AlertResponse a, int originId, CancellationToken ct)
        {
            var album = await Client.SendMediaGroup(
                chatId,
                new[]
                {
            new InputMediaPhoto(Base64ToInput(a.Victim?.VictimPhoto,    "victim.jpg")),
            new InputMediaPhoto(Base64ToInput(a.Abductor?.AbductorPhoto,"abductor.jpg"))
                },
                cancellationToken: ct);

            var txt = await Client.SendMessage(
                chatId,
                BuildCaption(a),
                parseMode: ParseMode.Markdown,
                replyMarkup: InlineMenus.DetailNav(a.AlertId, originId),
                cancellationToken: ct);

            _cache.Set($"album_{chatId}", album.Select(m => m.MessageId).ToList());
            _cache.Set($"text_{chatId}", txt.MessageId);
        }


        private static InputFile Base64ToInput(string? b64, string fileName)
        {
            if (string.IsNullOrWhiteSpace(b64))
                b64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR4nGNgYAAAAAMAASsJTYQAAAAASUVORK5CYII=";

            byte[] bytes = Convert.FromBase64String(b64);
            return InputFile.FromStream(new MemoryStream(bytes), fileName);
        }

        private async Task ShowAlert(long chatId, int _, AlertResponse a, CancellationToken ct, int totalAlerts)
        {
            var caption = BuildCaption(a);

            var albumMsg = await Client.SendMediaGroup(
                chatId,
                new[]
                {
                    new InputMediaPhoto(Base64ToInput(a.Victim?.VictimPhoto,"victim.jpg")),
                    new InputMediaPhoto(Base64ToInput(a.Abductor?.AbductorPhoto,"abductor.jpg"))
                },
                cancellationToken: ct);

            var txt = await Client.SendMessage(chatId, caption,
                parseMode: ParseMode.Markdown,
                 cancellationToken: ct);

            await Client.EditMessageReplyMarkup(chatId, txt.MessageId,
                replyMarkup: InlineMenus.NavWithReport(a.AlertId, txt.MessageId, totalAlerts), cancellationToken: ct);

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

        public async Task SendAlertNotification(long chatId, int alertId, string text)
        {
            await Client.SendMessage(
                chatId,
                text,
                replyMarkup: new InlineKeyboardMarkup(
                    InlineKeyboardButton.WithCallbackData("ℹ️ Детальніше", $"alert_detail:{alertId}")),
                parseMode: ParseMode.Markdown
                );
        }

        private async Task CleanupKeyboardMessages(long chatId, IEnumerable<int> messageIds, CancellationToken ct)
        {
            if (messageIds == null) return;

            foreach (var messageId in messageIds)
            {
                try
                {
                    await Client.EditMessageReplyMarkup(chatId, messageId, replyMarkup: null, cancellationToken: ct);
                }
                catch (ApiRequestException ex) when (ex.ErrorCode == 400 &&
                                                     (ex.Message.Contains("message is not modified") || ex.Message.Contains("message can't be edited") || ex.Message.Contains("message to edit not found")))
                {
                }
                catch (Exception ex)
                {


                    Console.WriteLine($"[Cleanup Error] Chat: {chatId}, Msg: {messageId}. Failed to edit reply markup: {ex.Message}");
                }
            }
        }


        private async Task SafeAnswerCallbackQuery(string callbackQueryId, CancellationToken ct, string? text = null, bool showAlert = false)
        {
            if (string.IsNullOrEmpty(callbackQueryId)) return;
            try
            {
                await Client.AnswerCallbackQuery(callbackQueryId, text, showAlert, cancellationToken: ct);
            }
            catch (ApiRequestException ex) when (ex.ErrorCode == 400 && ex.Message.Contains("query is too old"))
            {
                Console.WriteLine($"[SafeAnswerCallback] Query {callbackQueryId} is too old.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SafeAnswerCallback] Error answering query {callbackQueryId}: {ex.Message}");
            }
        }
    }
}
