namespace Game.Core.Random
{
    // Random abstraction keeps gameplay deterministic in tests and swappable in production.
    public interface IRandom
    {
        int Next(int minInclusive, int maxExclusive);
        double NextDouble();
    }
}
