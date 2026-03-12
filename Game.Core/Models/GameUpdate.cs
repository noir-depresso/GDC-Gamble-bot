using System.Collections.Generic;

namespace Game.Core.Models
{
    public class GameUpdate
    {
        public List<string> Messages { get; } = new();
        public List<string> Errors { get; } = new();

        public bool StateChanged { get; set; }
        public bool SessionChanged { get; set; }

        public bool Success => Errors.Count == 0;
    }
}
