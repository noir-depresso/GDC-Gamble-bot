using Game.Core.Random;

namespace Game.Core.Tests;

internal sealed class PredictableRandom : IRandom
{
    public int Next(int minInclusive, int maxExclusive)
    {
        return minInclusive;
    }

    public double NextDouble()
    {
        return 0.5;
    }
}
