using Game.Core.Models;

namespace Game.Core.Effects.Implementations
{
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

    public class LoanSharkEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            int gain = ctx.State.BasicIncome;
            ctx.State.Money += gain;
            ctx.State.AddStacks(EffectIds.LOAN_SHARK_DEBT, 1, durationTurns: 2);
            return $"Loan Shark: gained +{gain} money now. Repayment due in 2 turns.";
        }
    }

    public class StocksAndBondsStackEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            ctx.State.AddStacks(EffectIds.STOCKS_BONDS, 1, durationTurns: -1);
            return "Stocks & Bonds: +1 stack (end of round: random -10%..+20% money per stack).";
        }
    }

    public class CryptoEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            ctx.State.AddStacks(EffectIds.CRYPTO_NEXT, 1, durationTurns: 2);
            return "Crypto: next round gains +50% of this round earnings.";
        }
    }

    public class RealEstateEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            int increase = ctx.Random.Next(5, 26);
            ctx.State.BasicIncome += increase;
            return $"Real Estate: Basic Income increased by {increase}.";
        }
    }
}
