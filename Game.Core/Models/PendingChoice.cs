using System.Collections.Generic;

namespace Game.Core.Models
{
    // Represents an unresolved prompt that must be answered before normal flow continues.
    public class PendingChoice
    {
        public string ChoiceId { get; set; } = string.Empty;
        public string ChoiceType { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public string SourceCardId { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new();
    }
}
