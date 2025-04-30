using Telegram.Bot.Types.ReplyMarkups;

namespace Relict_TelegramBot_Stride.MenuButtons
{
    public static class InlineMenus
    {
        public static InlineKeyboardMarkup MainMenu() =>
            new(InlineKeyboardButton.WithCallbackData("🟢 Активні алерти", "menu_active"));

        public static InlineKeyboardMarkup Nav() =>
            new(new[]
            {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("◀️", "prev"),
                InlineKeyboardButton.WithCallbackData("🏠", "menu"),
                InlineKeyboardButton.WithCallbackData("▶️", "next")
            }
            });

        public static InlineKeyboardMarkup NavWithReport(int alertId, int totalAlerts)
        {
            if (totalAlerts > 1)
            {
                return new InlineKeyboardMarkup(new[]
                {
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("◀️", "prev"),
                        InlineKeyboardButton.WithCallbackData("🏠", "menu"),
                        InlineKeyboardButton.WithCallbackData("▶️", "next")
                    },
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("📢 Повідомити інформацію", $"report:{alertId}")
                    }
                });
            }
            else
            {
                return new InlineKeyboardMarkup(new[]
                {
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("🏠", "menu"),
                        InlineKeyboardButton.WithCallbackData("📢 Повідомити інформацію", $"report:{alertId}")
                    }
                });
            }
        }

        public static InlineKeyboardMarkup ReportStart(int alertId) =>
            new(new[]
            {
                InlineKeyboardButton.WithCallbackData("📢 Повідомити інформацію", $"report:{alertId}")
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
