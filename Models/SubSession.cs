using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Relict_TelegramBot_Stride.Models
{
    public class SubSession
    {
        public int CurrentPage { get; set; }
        public HashSet<int> Selected { get; } = new();
        public HashSet<int> Original { get; } = new();
    }
}
