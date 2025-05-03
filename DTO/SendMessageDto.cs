using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Relict_TelegramBot_Stride.DTO
{
    public class SendMessageDto
    {
        public long ChatId { get; set; }
        public int AlertId { get; set; }
        public string Text { get; set; }
    }
}
