namespace Game.Core.Effects.Implementations
{
    /// <summary>
    /// Delayed next-turn damage buff.
    /// Duration is intentionally 2 so the status survives the end-of-turn tick and is still present next turn.
    /// </summary>
    public class EnchantmentEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            ctx.State.AddStacks(EffectIds.ENCHANT_NEXT_TURN, 1, durationTurns: 2);
            return "Enchantment: +30% damage next turn.";
        }
    }

    /// <summary>
    /// Arms a one-turn reactive defense that either pays off when attacked or backfires at turn end.
    /// </summary>
    public class DiscreditEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            ctx.State.AddStacks(EffectIds.DISCREDIT_READY, 1, durationTurns: 1);
            return "Discredit armed: if attacked this turn, block and reflect 50%. Else take 40% self-damage.";
        }
    }
}
