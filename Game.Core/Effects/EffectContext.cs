using Game.Core.Models;
using Game.Core.Random;

namespace Game.Core.Effects
{
    public class EffectContext
    {
        public Combatant User { get; }
        public Combatant Target { get; }
        public GameState State { get; }
        public IRandom Random { get; }

        public int PendingDamage { get; set; }

        public EffectContext(Combatant user, Combatant target, GameState state, IRandom random)
        {
            User = user;
            Target = target;
            State = state;
            Random = random;
        }
    }
}
