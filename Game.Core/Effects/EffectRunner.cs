using System;
using System.Text;
using Game.Core.Cards;

namespace Game.Core.Effects
{
    /// <summary>
    /// Executes card effects and lifecycle triggers against a mutable combat state.
    /// All effect side effects are centralized here so the engine has one place to ask for trigger resolution.
    /// </summary>
    public class EffectRunner
    {
        /// <summary>
        /// Runs only OnPlay effects for a card in definition order and returns a combined combat log snippet.
        /// </summary>
        public string RunOnPlay(CardDef card, EffectContext ctx)
        {
            var sb = new StringBuilder();
            foreach (var e in card.Effects)
            {
                if (e.Trigger != EffectTrigger.OnPlay) continue;
                string line = e.Apply(ctx);
                if (!string.IsNullOrWhiteSpace(line)) sb.AppendLine(line);
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Fires non-play triggers that depend on engine timing, such as taking damage or ending a round.
        /// The engine decides when a trigger should happen; the runner handles the status math and log text.
        /// </summary>
        public string FireTrigger(EffectTrigger trigger, EffectContext ctx)
        {
            var sb = new StringBuilder();

            if (trigger == EffectTrigger.OnBeforeTakeDamage)
            {
                // Defensive statuses adjust pending damage before the hit is applied.
                if (ctx.State.GetStacks(EffectIds.SUTURE_IMMUNE) > 0)
                {
                    ctx.PendingDamage = 0;
                    sb.AppendLine("Suture immunity: prevented all incoming damage.");
                }

                int hedge = ctx.State.GetStacks(EffectIds.HEDGING_SHIELD);
                if (hedge > 0)
                {
                    ctx.PendingDamage = (int)MathF.Round(ctx.PendingDamage * 0.25f);
                    sb.AppendLine("Hedging: incoming damage reduced by 75%.");
                }
            }

            if (trigger == EffectTrigger.OnDamageTaken)
            {
                // Reactive statuses care about the damage that actually landed this turn.
                if (ctx.State.GetStacks(EffectIds.FIREWALL_READY) > 0 && ctx.Target != null)
                {
                    int reflect = (int)MathF.Round(ctx.PendingDamage * 0.25f);
                    reflect = ctx.State.ScalePlayerOutgoingDamage(reflect);
                    ctx.Target.TakeDamage(reflect);
                    sb.AppendLine($"Firewall: reflected {reflect} damage.");
                }

                if (ctx.State.GetStacks(EffectIds.DISCREDIT_READY) > 0 && ctx.Target != null)
                {
                    int reflected = (int)MathF.Round(ctx.PendingDamage * 0.5f);
                    reflected = ctx.State.ScalePlayerOutgoingDamage(reflected);
                    ctx.Target.TakeDamage(reflected);

                    // In this simplified model, blocking half the hit is represented by healing that half back.
                    int blocked = (int)MathF.Round(ctx.PendingDamage * 0.5f);
                    ctx.User.Heal(blocked);

                    ctx.State.RemoveStatus(EffectIds.DISCREDIT_READY);
                    sb.AppendLine($"Discredit: blocked ~{blocked} and reflected {reflected}.");
                }

                if (ctx.State.GetStacks(EffectIds.TRAUMA_TEAM_READY) > 0 && ctx.Target != null)
                {
                    int retaliation = ctx.State.ScalePlayerOutgoingDamage(50);
                    ctx.Target.TakeDamage(retaliation);
                    ctx.State.RemoveStatus(EffectIds.TRAUMA_TEAM_READY);
                    sb.AppendLine($"Trauma Team: retaliated for {retaliation}.");
                }
            }

            if (trigger == EffectTrigger.OnTurnEnd)
            {
                // These statuses resolve after both sides have had a chance to interact during the turn.
                var dis = ctx.State.GetStatus(EffectIds.DISCREDIT_READY);
                if (dis != null)
                {
                    int selfDamage = (int)MathF.Round(ctx.User.CurrentHealth * 0.4f);
                    ctx.User.TakeDamage(selfDamage);
                    ctx.State.RemoveStatus(EffectIds.DISCREDIT_READY);
                    sb.AppendLine($"Discredit failed: took {selfDamage} self-damage.");
                }

                var debt = ctx.State.GetStatus(EffectIds.LOAN_SHARK_DEBT);
                if (debt != null && debt.DurationTurns == 1)
                {
                    int repay = (int)MathF.Round(ctx.State.BasicIncome * 2.0f * debt.Stacks);
                    ctx.State.Money -= repay;
                    sb.AppendLine($"Loan Shark: repaid {repay} money.");

                    if (ctx.Random.NextDouble() <= 0.05)
                    {
                        int moneyLoss = (int)MathF.Round(Math.Abs(ctx.State.Money) * 0.2f);
                        ctx.State.Money -= moneyLoss;
                        int hpLoss = (int)MathF.Round(ctx.User.MaxHealth * 0.2f);
                        ctx.User.TakeDamage(hpLoss);
                        sb.AppendLine($"Loan Shark catastrophe: lost {moneyLoss} money and {hpLoss} HP.");
                    }
                }

                var trauma = ctx.State.GetStatus(EffectIds.TRAUMA_TEAM_READY);
                if (trauma != null && trauma.DurationTurns == 1)
                {
                    ctx.User.Heal(15);
                    ctx.State.RemoveStatus(EffectIds.TRAUMA_TEAM_READY);
                    sb.AppendLine("Trauma Team: not attacked, healed 15.");
                }

                var wane = ctx.State.GetStatus(EffectIds.WANE_WAX_NEXT);
                if (wane != null && wane.DurationTurns == 1)
                {
                    int tracked = Math.Max(0, ctx.State.GetStacks(EffectIds.WANE_WAX_DAMAGE_TRACK));
                    int heal = (int)MathF.Round(tracked * 0.5f);
                    if (heal > 0) ctx.User.Heal(heal);
                    ctx.State.RemoveStatus(EffectIds.WANE_WAX_DAMAGE_TRACK);
                    sb.AppendLine($"Wane and Wax: healed {heal} from tracked damage.");
                }

                var raan = ctx.State.GetStatus(EffectIds.RAAN_DRAW);
                if (raan != null && raan.DurationTurns == 1)
                {
                    ctx.State.PendingExtraDraws += 1;
                    sb.AppendLine("Raan: gained 1 extra draw.");
                }
            }

            if (trigger == EffectTrigger.OnRoundEnd)
            {
                // Round-end economy is intentionally computed as one pipeline so modifiers stack in a readable order.
                int roundGain = ctx.State.BasicIncome;

                if (ctx.State.GetStacks(EffectIds.HEDGING_INCOME_PENALTY) > 0)
                    roundGain = (int)MathF.Round(roundGain * 0.5f);

                int sell = Math.Min(2, ctx.State.GetStacks(EffectIds.SELL_HIGH));
                if (sell > 0)
                {
                    int bonus = (int)MathF.Round(roundGain * 0.25f * sell);
                    roundGain += bonus;
                    sb.AppendLine($"Sell High: +{bonus} money.");
                }

                int bank = ctx.State.GetStacks(EffectIds.BANK_ACCOUNT);
                if (bank > 0)
                {
                    int gain = (int)MathF.Round(ctx.State.BasicIncome * 0.05f * bank);
                    roundGain += gain;
                    sb.AppendLine($"Bank Account: +{gain} money.");
                }

                if (ctx.State.GetStacks(EffectIds.CRYPTO_NEXT) > 0)
                {
                    int cryptoBonus = (int)MathF.Round(ctx.State.LastRoundMoneyGain * 0.5f);
                    roundGain += cryptoBonus;
                    sb.AppendLine($"Crypto: +{cryptoBonus} money.");
                    ctx.State.RemoveStatus(EffectIds.CRYPTO_NEXT);
                }

                int sbStacks = ctx.State.GetStacks(EffectIds.STOCKS_BONDS);
                for (int i = 0; i < sbStacks; i++)
                {
                    float pct = -0.10f + (float)(ctx.Random.NextDouble() * 0.30f);
                    int delta = (int)MathF.Round(ctx.State.Money * pct);
                    roundGain += delta;
                    sb.AppendLine($"Stocks & Bonds: {(delta >= 0 ? "+" : "")}{delta} money ({pct * 100:0.#}%).");
                }

                int scaledRoundGain = ctx.State.ScaleRoundMoneyGain(roundGain);
                ctx.State.Money += scaledRoundGain;
                ctx.State.LastRoundMoneyGain = Math.Max(0, scaledRoundGain);
                if (scaledRoundGain != roundGain)
                    sb.AppendLine($"Difficulty/encounter scaling adjusted round gain from {roundGain} to {scaledRoundGain}.");
                sb.AppendLine($"Round money gain: +{scaledRoundGain}.");
            }

            return sb.ToString().TrimEnd();
        }
    }
}
