namespace Game.Core.Models
{
    // Generic status payload used by the effect system for buffs, delayed effects, and counters.
    public class StatusInstance
    {
        public string Id { get; set; } = string.Empty;
        public int Stacks { get; set; }

        // -1 = permanent, otherwise counts down each turn.
        public int DurationTurns { get; set; }

        // Parameterless constructor supports serialization.
        public StatusInstance()
        {
        }

        // Main constructor used by gameplay code when a new status is created.
        public StatusInstance(string id, int stacks, int durationTurns)
        {
            Id = id;
            Stacks = stacks;
            DurationTurns = durationTurns;
        }
    }
}
