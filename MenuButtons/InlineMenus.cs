using Telegram.Bot.Types.ReplyMarkups;

namespace Relict_TelegramBot_Stride.MenuButtons
{
    public static class InlineMenus
    {
        public static InlineKeyboardMarkup MainMenu() =>
            /*new(
                InlineKeyboardButton.WithCallbackData("🟢 Активні алерти", "menu_active"),
                InlineKeyboardButton.WithCallbackData("🔔 Підписка", "sub")
            );*/
       
            new(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🟢 Активні алерти", "menu_active"),
                    InlineKeyboardButton.WithCallbackData("🔔 Підписка", "sub")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("ℹ️ Що вміє цей бот?", "about_bot")
                }
            });



        public static InlineKeyboardMarkup SubMenu() =>
            new(new[]
            {
                InlineKeyboardButton.WithCallbackData("📍 Обрати місто", "reg_page:0"),
                InlineKeyboardButton.WithCallbackData("📑 Мої міста",    "my_page:0"),
                InlineKeyboardButton.WithCallbackData("⬅️ Назад",        "menu")
            });

        public static InlineKeyboardMarkup MyNav(IEnumerable<InlineKeyboardButton[]> rows) =>
            new(rows.Concat(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🚫 Відписатися", "my_unsub"),
                    InlineKeyboardButton.WithCallbackData("❌ Скасувати",   "my_cancel")
                }
    }));


        public static InlineKeyboardMarkup Nav() =>
            new(new[]
            {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("◀️", "prev"),
                InlineKeyboardButton.WithCallbackData("🏠 На головну", "menu"),
                InlineKeyboardButton.WithCallbackData("▶️", "next")
            }
            });

        public static InlineKeyboardMarkup NavWithReport(int alertId, int originId, int totalAlerts)
        {
            if (totalAlerts > 1)
            {
                return new InlineKeyboardMarkup(new[]
                {
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("◀️", "prev"),
                        InlineKeyboardButton.WithCallbackData("🏠 На головну", "menu"),
                        InlineKeyboardButton.WithCallbackData("▶️", "next")
                    },
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("📢 Повідомити інформацію", $"report:{alertId}:{originId}:a")
                    }
                });
            }
            else
            {
                return new InlineKeyboardMarkup(new[]
                {
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("🏠 На головну", "menu"),
                        InlineKeyboardButton.WithCallbackData("📢 Повідомити інформацію", $"report:{alertId}")
                    }
                });
            }
        }

        public static InlineKeyboardMarkup AboutNav() =>
            new(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⬅️ Назад", "menu"),
                    InlineKeyboardButton.WithCallbackData("❓ FAQ", "faq_page")
                }
            });

        public static InlineKeyboardMarkup FaqNav() =>
            new(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⬅️ Назад", "about_bot"),
                    InlineKeyboardButton.WithCallbackData("🏠 На головну", "menu")
                }
            });

        public static InlineKeyboardMarkup ReportStart(int alertId) =>
            new(new[]
            {
                InlineKeyboardButton.WithCallbackData("📢 Повідомити інформацію", $"report:{alertId}")
            });


        public static InlineKeyboardMarkup DetailNav(int alertId, int originId) =>
            new(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⬅️ Назад", $"alert_back:{originId}")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("📢 Повідомити інформацію", $"report:{alertId}:{originId}:p")
                }
            });

        public static InlineKeyboardMarkup AnonChoice() =>
            new(new[]
            {
                InlineKeyboardButton.WithCallbackData("👤 Ім’я/Контакт", "rep_anon:no"),
                InlineKeyboardButton.WithCallbackData("🔒 Анонімно",       "rep_anon:yes"),
                InlineKeyboardButton.WithCallbackData("❌ Скасувати", "rep_cancel")


            });

        public static InlineKeyboardMarkup BackCancel() =>
            new(new[]
            {
                InlineKeyboardButton.WithCallbackData("⬅️ Назад", "rep_back"),
                InlineKeyboardButton.WithCallbackData("❌ Скасувати", "rep_cancel")
            });

        public static InlineKeyboardMarkup CancelOnly() =>
            new(InlineKeyboardButton.WithCallbackData("❌ Скасувати", "rep_cancel"));
    }
}
