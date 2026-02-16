using System.Text;
using Game.Core.Cards;

namespace Game.Core.Effects
{
    public class EffectRunner
    {
        public string RunOnPlay(CardDef card, EffectContext ctx)
        {
            var sb = new StringBuilder();
            foreach (var e in card.Effects)
            {
                if (e.Trigger != EffectTrigger.OnPlay) continue;
                var line = e.Apply(ctx);
                if (!string.IsNullOrWhiteSpace(line)) sb.AppendLine(line);
            }
            return sb.ToString().TrimEnd();
        }

        public string FireTrigger(EffectTrigger trigger, EffectContext ctx)
        {
            var sb = new StringBuilder();

            if (trigger == EffectTrigger.OnBeforeTakeDamage)
            {
                int hedge = ctx.State.GetStacks(EffectIds.HEDGING_SHIELD);
                if (hedge > 0)
                {
                    ctx.PendingDamage = (int)System.MathF.Round(ctx.PendingDamage * 0.5f);
                    sb.AppendLine("Hedging: incoming damage reduced by 50%.");
                }
            }

            if (trigger == EffectTrigger.OnTurnEnd)
            {
                // Discredit punishment if still armed and about to expire (meaning: you weren't attacked)
                var dis = ctx.State.GetStatus(EffectIds.DISCREDIT_READY);
                if (dis != null && dis.DurationTurns == 1)
                {
                    ctx.User.TakeDamage(10);
                    ctx.State.RemoveStatus(EffectIds.DISCREDIT_READY);
                    sb.AppendLine("Discredit: you were not attacked. Took 10 self-damage.");
                }

                // Loan shark repayment when about to expire
                var debt = ctx.State.GetStatus(EffectIds.LOAN_SHARK_DEBT);
                if (debt != null && debt.DurationTurns == 1)
                {
                    int stacks = debt.Stacks;

                    // repay = BI * 1.3 * stacks
                    int repay = (int)System.MathF.Round(ctx.State.BasicIncome * 1.3f * stacks);

                    if (ctx.State.Money >= repay)
                    {
                        ctx.State.Money -= repay;
                        sb.AppendLine($"Loan Shark: repaid {repay} money.");
                    }
                    else
                    {
                        int missing = repay - ctx.State.Money;
                        ctx.State.Money = 0;

                        // convert missing money to HP loss (h = 0.5)
                        int hpLoss = (int)System.MathF.Round(missing * 0.5f);
                        ctx.User.TakeDamage(hpLoss);

                        sb.AppendLine($"Loan Shark: couldn't pay. Lost {hpLoss} HP instead.");
                    }

                    // 5% catastrophe: lose 20% money + 10 HP
                    if (ctx.State.Rand01() <= 0.05f)
                    {
                        int moneyLoss = (int)System.MathF.Round(ctx.State.Money * 0.2f);
                        ctx.State.Money -= moneyLoss;
                        ctx.User.TakeDamage(10);
                        sb.AppendLine("Loan Shark catastrophe: lost 20% money and 10 HP.");
                    }
                }
            }

            if (trigger == EffectTrigger.OnDamageTaken)
            {
                int discredit = ctx.State.GetStacks(EffectIds.DISCREDIT_READY);
                if (discredit > 0 && ctx.Target != null)
                {
                    int reflect = (int)System.MathF.Round(ctx.PendingDamage * 0.5f);
                    ctx.Target.TakeDamage(reflect);

                    ctx.State.RemoveStatus(EffectIds.DISCREDIT_READY);
                    sb.AppendLine($"Discredit: reflected {reflect} damage back to attacker.");
                }
            }

            

            if (trigger == EffectTrigger.OnRoundEnd)
            {
                int sbStacks = ctx.State.GetStacks(EffectIds.STOCKS_BONDS);
                if (sbStacks > 0)
                {
                    // Apply per stack: money += round(money * rand[-0.2, +0.2])
                    for (int i = 0; i < sbStacks; i++)
                    {
                        float pct = ctx.State.RandRange(-0.2f, 0.2f);
                        int delta = (int)System.MathF.Round(ctx.State.Money * pct);
                        ctx.State.Money += delta;

                        string sign = delta >= 0 ? "+" : "";
                        sb.AppendLine($"Stocks & Bonds: {sign}{delta} money ({pct * 100:0.#}% drift).");
                    }
                }
                int bank = ctx.State.GetStacks(EffectIds.BANK_ACCOUNT);
                if (bank > 0)
                {
                    int gain = (int)System.MathF.Round(ctx.State.BasicIncome * 0.05f * bank);
                    ctx.State.Money += gain;
                    sb.AppendLine($"Bank Account: +{gain} money (5% BI × {bank}).");
                }

                int sell = ctx.State.GetStacks(EffectIds.SELL_HIGH);
                if (sell > 0)
                {
                    int gain = (int)System.MathF.Round(ctx.State.BasicIncome * 0.10f * sell);
                    ctx.State.Money += gain;
                    sb.AppendLine($"Sell High: +{gain} money (10% BI × {sell}).");
                }
            }

            return sb.ToString().TrimEnd();
        }
    }
}