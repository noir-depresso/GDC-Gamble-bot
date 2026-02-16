using Game.Core.Models;

namespace Game.Core.Effects
{
    public class EffectContext
    {
        public Combatant User { get; }
        public Combatant Target { get; }
        public GameState State { get; }

        public int PendingDamage { get; set; }

        public EffectContext(Combatant user, Combatant target, GameState state)
        {
            User = user;
            Target = target;
            State = state;
        }
    }
}