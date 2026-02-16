namespace Game.Core.Models
{
    public class StatusInstance
    {
        public string Id { get; }
        public int Stacks { get; set; }

        // -1 = permanent, otherwise counts down each turn
        public int DurationTurns { get; set; }

        public StatusInstance(string id, int stacks, int durationTurns)
        {
            Id = id;
            Stacks = stacks;
            DurationTurns = durationTurns;
        }
    }
}