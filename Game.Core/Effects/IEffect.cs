namespace Game.Core.Effects
{
    // Common contract implemented by all card effects and trigger effects.
    public interface IEffect
    {
        EffectTrigger Trigger { get; }
        string Apply(EffectContext ctx);
    }
}
