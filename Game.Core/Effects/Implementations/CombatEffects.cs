using System;

namespace Game.Core.Effects.Implementations
{
    /// <summary>
    /// Basic direct-damage effect used by straightforward attack cards.
    /// It applies temporary damage buffs before the state's global scaling multipliers.
    /// </summary>
    public class DealDamageEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;
        private readonly int _amount;

        public DealDamageEffect(int amount) => _amount = amount;

        public string Apply(EffectContext ctx)
        {
            if (ctx.Target == null) return "No target to damage.";

            int dmg = _amount;
            float mult = 1f;

            // Buff-style statuses stack before difficulty/encounter scaling so card text stays intuitive.
            int enchant = ctx.State.GetStacks(EffectIds.ENCHANT_NEXT_TURN);
            if (enchant > 0) mult *= 1f + (0.30f * enchant);

            if (ctx.State.GetStacks(EffectIds.RELEASE_FILES) > 0) mult *= 2f;
            if (ctx.State.GetStacks(EffectIds.PERSUASION_BUFF) > 0) mult *= 1.25f;

            dmg = (int)MathF.Round(dmg * mult);
            if (dmg < 0) dmg = 0;

            dmg = ctx.State.ScalePlayerOutgoingDamage(dmg);
            ctx.Target.TakeDamage(dmg);

            // Wane and Wax tracks the final damage that landed, not the raw card base value.
            if (ctx.State.GetStacks(EffectIds.WANE_WAX_NEXT) > 0)
                ctx.State.AddStacks(EffectIds.WANE_WAX_DAMAGE_TRACK, dmg, durationTurns: 2);

            return $"Dealt {dmg} damage to enemy.";
        }
    }

    /// <summary>
    /// Fixed-value healing effect for simple recovery cards.
    /// </summary>
    public class HealEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;
        private readonly int _amount;

        public HealEffect(int amount) => _amount = amount;

        public string Apply(EffectContext ctx)
        {
            ctx.User.Heal(_amount);
            return $"Healed {_amount} HP.";
        }
    }

    /// <summary>
    /// Tradeoff heal that restores a percentage of max HP while permanently shrinking the max-health cap.
    /// </summary>
    public class HealPercentAndReduceMaxHealthEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;
        private readonly float _healPct;
        private readonly float _maxHealthLossPct;

        public HealPercentAndReduceMaxHealthEffect(float healPct, float maxHealthLossPct)
        {
            _healPct = healPct;
            _maxHealthLossPct = maxHealthLossPct;
        }

        public string Apply(EffectContext ctx)
        {
            int heal = (int)MathF.Round(ctx.User.MaxHealth * _healPct);
            int maxLoss = (int)MathF.Round(ctx.User.MaxHealth * _maxHealthLossPct);
            if (maxLoss < 1) maxLoss = 1;

            ctx.User.MaxHealth = Math.Max(1, ctx.User.MaxHealth - maxLoss);
            if (ctx.User.CurrentHealth > ctx.User.MaxHealth)
                ctx.User.CurrentHealth = ctx.User.MaxHealth;

            ctx.User.Heal(heal);
            return $"Prosthesis: healed {heal}. Permanent max health -{maxLoss}.";
        }
    }

    /// <summary>
    /// Scaling damage card whose output increases each time the card is played in a run.
    /// </summary>
    public class SocialPressureEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            ctx.State.AddStacks(EffectIds.SOCIAL_PRESSURE, 1, durationTurns: -1);
            int stacks = ctx.State.GetStacks(EffectIds.SOCIAL_PRESSURE);
            int dmg = 10 + (10 * stacks);

            if (ctx.Target == null)
                return $"Social Pressure stacks: {stacks}. (No target to hit.)";

            dmg = ctx.State.ScalePlayerOutgoingDamage(dmg);
            ctx.Target.TakeDamage(dmg);
            return $"Social Pressure stacks: {stacks}. Dealt {dmg} damage.";
        }
    }

    /// <summary>
    /// Simple variance card that either spikes or low-rolls from a 50/50 coin flip.
    /// </summary>
    public class CoinFlipDamageEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;
        private readonly int _baseDamage;

        public CoinFlipDamageEffect(int baseDamage)
        {
            _baseDamage = baseDamage;
        }

        public string Apply(EffectContext ctx)
        {
            if (ctx.Target == null) return "No target to damage.";

            bool heads = ctx.Random.NextDouble() >= 0.5;
            float mult = heads ? 2f : 0.5f;
            int dmg = (int)MathF.Round(_baseDamage * mult);
            dmg = ctx.State.ScalePlayerOutgoingDamage(dmg);
            ctx.Target.TakeDamage(dmg);

            return heads
                ? $"Coin Flip: heads. Dealt {dmg} damage (double)."
                : $"Coin Flip: tails. Dealt {dmg} damage (half).";
        }
    }

    /// <summary>
    /// Defensive setup card that sacrifices economy for one safer incoming hit window.
    /// </summary>
    public class HedgingEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            ctx.State.AddStacks(EffectIds.HEDGING_SHIELD, 1, durationTurns: 1);
            ctx.State.AddStacks(EffectIds.HEDGING_INCOME_PENALTY, 1, durationTurns: 1);
            return "Hedging: next incoming damage reduced by 75%, next income reduced by 50%.";
        }
    }

    /// <summary>
    /// Delayed sustain effect that pays you back for the damage you deal next turn.
    /// </summary>
    public class WaneAndWaxEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            ctx.State.RemoveStatus(EffectIds.WANE_WAX_DAMAGE_TRACK);
            ctx.State.AddStacks(EffectIds.WANE_WAX_NEXT, 1, durationTurns: 2);
            return "Wane and Wax: next turn, heal 50% of damage dealt.";
        }
    }

    /// <summary>
    /// Reflection effect that turns part of an incoming hit back onto the enemy.
    /// </summary>
    public class FirewallEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            ctx.State.AddStacks(EffectIds.FIREWALL_READY, 1, durationTurns: 1);
            return "Firewall: reflect 25% of incoming damage this turn.";
        }
    }

    /// <summary>
    /// Directly reduces enemy attack, making future enemy turns less punishing.
    /// </summary>
    public class GuardDownEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            int newAttack = (int)MathF.Round(ctx.Target.Attack * 0.7f);
            if (newAttack < 0) newAttack = 0;
            int delta = ctx.Target.Attack - newAttack;
            ctx.Target.Attack = newAttack;
            return $"Guard Down: enemy attack reduced by {delta} ({newAttack} now).";
        }
    }

    /// <summary>
    /// Conditional burst setup that only turns on when the player is in the danger zone.
    /// </summary>
    public class ReleaseFilesEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            if (ctx.User.CurrentHealth <= (int)MathF.Round(ctx.User.MaxHealth * 0.2f))
            {
                ctx.State.AddStacks(EffectIds.RELEASE_FILES, 1, durationTurns: 2);
                return "Release the Files: low HP condition met. Damage doubled next turn.";
            }

            return "Release the Files: HP condition not met (<=20% required).";
        }
    }

    /// <summary>
    /// Emergency heal plus a temporary immunity flag for the next incoming hit window.
    /// </summary>
    public class SutureKitEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            ctx.User.Heal(25);
            ctx.State.AddStacks(EffectIds.SUTURE_IMMUNE, 1, durationTurns: 1);
            return "Suture Kit: healed 25 and gained 1 turn immunity.";
        }
    }

    /// <summary>
    /// Sets up retaliation if hit, or a consolation heal if the enemy never triggers it.
    /// </summary>
    public class TraumaTeamEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            ctx.State.AddStacks(EffectIds.TRAUMA_TEAM_READY, 1, durationTurns: 1);
            return "Trauma Team armed: if attacked next turn, retaliate for 50; else heal 15.";
        }
    }

    /// <summary>
    /// Placeholder EMP protection hook used by the simplified ruleset.
    /// </summary>
    public class EmpEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            ctx.State.AddStacks(EffectIds.EMP_IMMUNE, 1, durationTurns: 2);
            return "EMP: card effects against you are suppressed for 2 turns.";
        }
    }

    /// <summary>
    /// Damage card that schedules itself to return, effectively acting as repeatable pressure.
    /// </summary>
    public class HiredGunEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            int dmg = ctx.State.ScalePlayerOutgoingDamage(20);
            if (ctx.Target != null)
                ctx.Target.TakeDamage(dmg);

            ctx.State.AddStacks(EffectIds.HIRED_GUN_RETURN, 1, durationTurns: 1);
            return $"Hired Gun: dealt {dmg} damage and will return to hand.";
        }
    }

    /// <summary>
    /// Delayed card-draw setup with a small random delay so planning around it is imperfect.
    /// </summary>
    public class RaanEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            int delay = ctx.Random.Next(2, 4);
            ctx.State.AddStacks(EffectIds.RAAN_DRAW, delay, durationTurns: delay);
            return $"Raan: extra draw in {delay} turns.";
        }
    }

    /// <summary>
    /// Random event card with one of three outcomes: self-harm, self-heal, or enemy damage.
    /// </summary>
    public class RouletteEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            int roll = ctx.Random.Next(0, 3);
            if (roll == 0)
            {
                ctx.User.TakeDamage(20);
                return "Roulette: self-damage 20.";
            }

            if (roll == 1)
            {
                ctx.User.Heal(20);
                return "Roulette: healed 20.";
            }

            int dmg = ctx.State.ScalePlayerOutgoingDamage(20);
            if (ctx.Target != null)
                ctx.Target.TakeDamage(dmg);
            return $"Roulette: dealt {dmg} damage to enemy.";
        }
    }

    /// <summary>
    /// Marks the hand to be rebuilt later in the turn/round pipeline instead of reshuffling immediately.
    /// </summary>
    public class ChaosEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            ctx.State.AddStacks("CHAOS_RESHUFFLE_HAND", 1, 1);
            return "Chaos: your hand will be reshuffled.";
        }
    }

    /// <summary>
    /// Schedules a delayed generated item reward for future turns.
    /// </summary>
    public class SpiderAndroidsEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            ctx.State.AddStacks("SPIDER_BUILD", 1, 2);
            return "Spider Androids deployed: building an item over 2 turns.";
        }
    }
}
