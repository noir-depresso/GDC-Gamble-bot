using System.Collections.Generic;

namespace Game.Core.Models
{
    public class PendingChoice
    {
        public string ChoiceId { get; set; } = string.Empty;
        public string ChoiceType { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public string SourceCardId { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new();
    }
}
