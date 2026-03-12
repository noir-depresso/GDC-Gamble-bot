namespace Game.Core.Random
{
    public class DefaultRandom : IRandom
    {
        private readonly System.Random _random;

        public DefaultRandom()
        {
            _random = new System.Random();
        }

        public DefaultRandom(int seed)
        {
            _random = new System.Random(seed);
        }

        public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);

        public double NextDouble() => _random.NextDouble();
    }
}
