using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    }
}
