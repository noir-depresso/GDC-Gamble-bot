using Game.Core.Models;

namespace Game.Core.Effects.Implementations
{
    /// <summary>
    /// Generic: add stacks for any status id.
    /// durationTurns: -1 = permanent
    /// </summary>
    public class AddStacksEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        private readonly string _id;
        private readonly int _addStacks;
        private readonly int _durationTurns;

        public AddStacksEffect(string id, int addStacks, int durationTurns)
        {
            _id = id;
            _addStacks = addStacks;
            _durationTurns = durationTurns;
        }

        public string Apply(EffectContext ctx)
        {
            ctx.State.AddStacks(_id, _addStacks, _durationTurns);
            return $"{_id}: +{_addStacks} stack(s).";
        }
    }

    /// <summary>
    /// Buy Low: +1 stack (max 2). Used by cost formula in GameEngine.FinalCost().
    /// </summary>
    public class BuyLowStackEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            int stacks = ctx.State.GetStacks(EffectIds.BUY_LOW);
            if (stacks >= 2)
                return "Buy Low is already at max stacks (2).";

            ctx.State.AddStacks(EffectIds.BUY_LOW, 1, durationTurns: -1);
            return "Buy Low: +1 stack (max 2).";
        }
    }

    /// <summary>
    /// Sell High: +1 stack (max 2). Payout happens in EffectRunner at OnRoundEnd.
    /// </summary>
    public class SellHighStackEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            int stacks = ctx.State.GetStacks(EffectIds.SELL_HIGH);
            if (stacks >= 2)
                return "Sell High is already at max stacks (2).";

            ctx.State.AddStacks(EffectIds.SELL_HIGH, 1, durationTurns: -1);
            return "Sell High: +1 stack (max 2).";
        }
    }
}