using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Relict_TelegramBot_Stride.Models
{
    public enum ReportStep
    {
        ChooseAnon,
        AskName,
        AskPhone,
        AskDescription,
        AskLocation,
        Done
    }
    public class ReportSession
    {
        public int AlertId { get; init; }
        public int? OriginMessageId { get; set; }

        public bool? IsAnonymous { get; set; }
        public string? CitizenName { get; set; }
        public string? CitizenContactPhone { get; set; }
        public string? Description { get; set; }
        public string? Location { get; set; }

        public ReportStep Step { get; set; } = ReportStep.ChooseAnon;

        public Stack<ReportStep> History { get; } = new();
    }
}
