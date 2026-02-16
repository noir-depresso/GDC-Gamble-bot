using Game.Core.Models;

namespace Game.Core.Effects.Implementations
{
    // Enchantment: next attack deals +50% damage (consumed by DealDamageEffect)
    public class EnchantmentEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            ctx.State.AddStacks(EffectIds.ENCHANT_NEXT, 1, durationTurns: 1);
            return "Enchantment: your next attack deals +50% damage.";
        }
    }

    // Discredit: if attacked this turn reflect 50%; if not attacked, take 10 at end of turn
    public class DiscreditEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            ctx.State.AddStacks(EffectIds.DISCREDIT_READY, 1, durationTurns: 1);
            return "Discredit armed: if you are attacked this turn, reflect 50%. Otherwise take 10 damage at turn end.";
        }
    }

    // Loan Shark: gain money now; in 2 turns repay with interest or lose HP; 5% catastrophe
    public class LoanSharkEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            // Receive now: +100% BI
            int gain = (int)System.MathF.Round(ctx.State.BasicIncome * 1.0f);
            ctx.State.Money += gain;

            // Debt marker lasts 2 turns; stacks = number of loans
            ctx.State.AddStacks(EffectIds.LOAN_SHARK_DEBT, 1, durationTurns: 2);

            return $"Loan Shark: gained +{gain} money now. Repayment due in 2 turns.";
        }
    }

    // Stocks & Bonds: just adds a permanent stack; payout is handled at round end in EffectRunner
    public class StocksAndBondsStackEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            ctx.State.AddStacks(EffectIds.STOCKS_BONDS, 1, durationTurns: -1);
            return "Stocks & Bonds: +1 stack (end of round: random -20%..+20% money per stack).";
        }
    }
}