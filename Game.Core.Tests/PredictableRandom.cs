using Game.Core.Random;

namespace Game.Core.Tests;

/// <summary>
/// Test helper that removes randomness by always returning the lowest valid value.
/// Use it when a test only cares that the engine is deterministic, not about any specific roll distribution.
/// </summary>
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
