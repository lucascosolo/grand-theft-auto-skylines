using System;

namespace GtaSkylines.Game.Mathx
{
    /// <summary>
    /// 32-bit FNV-1a, the Game's one stable digest (same value on Mono and .NET, unlike <c>GetHashCode</c>). Add feeds
    /// little-endian bytes: an int 4, a long 8, a float the 8 bytes of its widened double.
    /// </summary>
    public struct Fnv1a
    {
        /// <summary>FNV-1a 32-bit offset basis.</summary>
        public const uint OffsetBasis = 2166136261u;
        /// <summary>FNV-1a 32-bit prime.</summary>
        public const uint Prime = 16777619u;

        private uint _h;
        private bool _started;

        /// <summary>The digest so far (<see cref="OffsetBasis"/> before anything was added).</summary>
        public uint Value { get { return _started ? _h : OffsetBasis; } }

        /// <summary>Adds the low 8 bits of <paramref name="b"/>.</summary>
        public void AddByte(uint b)
        {
            if (!_started) { _h = OffsetBasis; _started = true; }
            _h ^= b & 0xFFu;
            _h *= Prime;
        }

        /// <summary>Adds 4 bytes.</summary>
        public void Add(int v)
        {
            for (int k = 0; k < 4; k++) AddByte((uint)(v >> (8 * k)));
        }

        /// <summary>Adds 8 bytes.</summary>
        public void Add(long v)
        {
            for (int k = 0; k < 8; k++) AddByte((uint)(v >> (8 * k)));
        }

        /// <summary>Adds the 8 bytes of the value widened to double.</summary>
        public void Add(float f) { Add(BitConverter.DoubleToInt64Bits(f)); }

        /// <summary>Adds X, Y, Z as floats.</summary>
        public void Add(Vec3 v) { Add(v.X); Add(v.Y); Add(v.Z); }

        /// <summary>FNV-1a over a string's UTF-16 code units, one step per unit (the interior seed path: never change it).</summary>
        public static uint Of(string s)
        {
            uint h = OffsetBasis;
            if (s == null) return h;
            for (int i = 0; i < s.Length; i++)
            {
                h ^= s[i];
                h *= Prime;
            }
            return h;
        }
    }
}
