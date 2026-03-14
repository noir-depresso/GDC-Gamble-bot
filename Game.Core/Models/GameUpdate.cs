using System.Collections.Generic;

namespace Game.Core.Models
{
    // Engine response envelope used by session/service layers to build player-facing output.
    public class GameUpdate
    {
        public List<string> Messages { get; } = new();
        public List<string> Errors { get; } = new();

        public bool StateChanged { get; set; }
        public bool SessionChanged { get; set; }

        // Success is inferred from whether any errors were recorded.
        public bool Success => Errors.Count == 0;
    }
}
