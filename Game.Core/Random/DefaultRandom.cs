namespace Game.Core.Random
{
    // Thin wrapper around System.Random that satisfies the engine's random contract.
    public class DefaultRandom : IRandom
    {
        private readonly System.Random _random;

        // Default constructor uses a time-based seed.
        public DefaultRandom()
        {
            _random = new System.Random();
        }

        // Seeded constructor is useful for deterministic simulations.
        public DefaultRandom(int seed)
        {
            _random = new System.Random(seed);
        }

        public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);

        public double NextDouble() => _random.NextDouble();
    }
}
