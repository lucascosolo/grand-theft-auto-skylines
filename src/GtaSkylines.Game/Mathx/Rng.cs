namespace GtaSkylines.Game.Mathx
{
    /// <summary>
    /// Small deterministic random generator (xorshift32 with a splitmix seed), so generated interiors, dialogue
    /// choices and AI decisions repeat exactly for the same seed. Never use <c>System.Random</c> in game rules.
    /// </summary>
    public sealed class Rng
    {
        private uint _state;

        /// <summary>Creates a generator; any seed (including 0) is valid.</summary>
        public Rng(uint seed)
        {
            _state = Mix(seed);
            if (_state == 0) _state = 0x9E3779B9u;
        }

        /// <summary>Combines values into a seed (order matters).</summary>
        public static uint Seed(uint a, uint b)
        {
            return Mix(a ^ (Mix(b) + 0x9E3779B9u + (a << 6) + (a >> 2)));
        }

        /// <summary>Combines values into a seed (order matters).</summary>
        public static uint Seed(uint a, uint b, uint c)
        {
            return Seed(Seed(a, b), c);
        }

        private static uint Mix(uint x)
        {
            x += 0x9E3779B9u;
            x = (x ^ (x >> 16)) * 0x85EBCA6Bu;
            x = (x ^ (x >> 13)) * 0xC2B2AE35u;
            return x ^ (x >> 16);
        }

        /// <summary>Next raw 32-bit value.</summary>
        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>Uniform in [0, 1).</summary>
        public float NextFloat()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        /// <summary>Uniform in [min, max).</summary>
        public float Range(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }

        /// <summary>Uniform integer in [min, maxExclusive); min when the range is empty.</summary>
        public int Range(int min, int maxExclusive)
        {
            if (maxExclusive <= min) return min;
            return min + (int)(NextUInt() % (uint)(maxExclusive - min));
        }

        /// <summary>True with probability <paramref name="p"/>.</summary>
        public bool Chance(float p)
        {
            return NextFloat() < p;
        }

        /// <summary>A uniformly chosen element; default for an empty array.</summary>
        public T Pick<T>(T[] items)
        {
            if (items == null || items.Length == 0) return default(T);
            return items[Range(0, items.Length)];
        }
    }
}
