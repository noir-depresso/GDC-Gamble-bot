namespace Game.Core.Random
{
    public interface IRandom
    {
        int Next(int minInclusive, int maxExclusive);
        double NextDouble();
    }
}
