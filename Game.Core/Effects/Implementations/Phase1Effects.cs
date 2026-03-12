namespace Game.Core.Effects.Implementations
{
    public class EnchantmentEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            // duration 2 so it remains through the next player turn after end-of-turn ticking
            ctx.State.AddStacks(EffectIds.ENCHANT_NEXT_TURN, 1, durationTurns: 2);
            return "Enchantment: +30% damage next turn.";
        }
    }

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
