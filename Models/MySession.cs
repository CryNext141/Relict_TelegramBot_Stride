using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Relict_TelegramBot_Stride.Models
{
    public class MySession
    {
        public int CurrentPage { get; set; }
        public HashSet<int> Selected { get; } = new();
    }
}
