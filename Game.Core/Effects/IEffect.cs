namespace Game.Core.Effects
{
    public interface IEffect
    {
        EffectTrigger Trigger { get; }
        string Apply(EffectContext ctx);
    }
}