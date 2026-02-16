using Game.Core.Models;

namespace Game.Core.Effects.Implementations
{
    /// <summary>
    /// Deal a fixed amount of damage to the target.
    /// Also applies "next attack" modifiers like Enchantment.
    /// </summary>
    public class DealDamageEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        private readonly int _amount;

        public DealDamageEffect(int amount)
        {
            _amount = amount;
        }

        public string Apply(EffectContext ctx)
        {
            if (ctx.Target == null)
                return "No target to damage.";

            int dmg = _amount;

            // Enchantment: next attack deals +50% damage, then consume it.
            if (ctx.State.GetStacks(EffectIds.ENCHANT_NEXT) > 0)
            {
                dmg = (int)System.MathF.Round(dmg * 1.5f);
                ctx.State.RemoveStatus(EffectIds.ENCHANT_NEXT);
                return ApplyAndLog(ctx, dmg, "Enchantment consumed (+50% damage).");
            }

            return ApplyAndLog(ctx, dmg, null);
        }

        private static string ApplyAndLog(EffectContext ctx, int dmg, string? extra)
        {
            ctx.Target!.TakeDamage(dmg);

            if (!string.IsNullOrWhiteSpace(extra))
                return $"Dealt {dmg} damage to enemy. {extra}";

            return $"Dealt {dmg} damage to enemy.";
        }
    }

    /// <summary>
    /// Heal the user (player) by a fixed amount.
    /// </summary>
    public class HealEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        private readonly int _amount;

        public HealEffect(int amount)
        {
            _amount = amount;
        }

        public string Apply(EffectContext ctx)
        {
            ctx.User.Heal(_amount);
            return $"Healed {_amount} HP.";
        }
    }

    /// <summary>
    /// Social Pressure:
    /// - Gain 1 Social Pressure stack (permanent)
    /// - Deal (10 + 5 * stacks) damage after adding the stack
    /// </summary>
    public class SocialPressureEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            ctx.State.AddStacks(EffectIds.SOCIAL_PRESSURE, 1, durationTurns: -1);
            int stacks = ctx.State.GetStacks(EffectIds.SOCIAL_PRESSURE);

            int dmg = 10 + 5 * stacks;

            if (ctx.Target == null)
                return $"Social Pressure stacks: {stacks}. (No target to hit.)";

            // Apply same "next attack" modifier logic as DealDamageEffect
            if (ctx.State.GetStacks(EffectIds.ENCHANT_NEXT) > 0)
            {
                dmg = (int)System.MathF.Round(dmg * 1.5f);
                ctx.State.RemoveStatus(EffectIds.ENCHANT_NEXT);
                ctx.Target.TakeDamage(dmg);
                return $"Social Pressure stacks: {stacks}. Dealt {dmg} damage. Enchantment consumed (+50% damage).";
            }

            ctx.Target.TakeDamage(dmg);
            return $"Social Pressure stacks: {stacks}. Dealt {dmg} damage.";
        }
    }
}