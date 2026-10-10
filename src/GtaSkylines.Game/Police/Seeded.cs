using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.Police
{
    /// <summary>Allocation-free seeded numbers: <c>Rng.Seed(seed, a, b)</c> mapped to a range (same inputs, same value).</summary>
    internal static class Seeded
    {
        /// <summary>Uniform in [0, 1) from the mixed seed of the three values.</summary>
        public static float Unit(uint seed, uint a, uint b)
        {
            return (Rng.Seed(seed, a, b) >> 8) * (1f / 16777216f);
        }

        /// <summary>Uniform in [min, max) from the mixed seed of the three values.</summary>
        public static float Range(uint seed, uint a, uint b, float min, float max)
        {
            return min + (max - min) * Unit(seed, a, b);
        }
    }
}
