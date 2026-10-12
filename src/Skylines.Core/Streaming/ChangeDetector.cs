using System;

namespace Skylines.Core.Streaming
{
    /// <summary>Remembers one signature per id and reports when it changes. Allocates only in the constructor.</summary>
    public sealed class ChangeDetector
    {
        private readonly ulong[] _last;
        private readonly bool[] _seen;

        /// <summary>Tracks ids 0 to <paramref name="capacity"/> - 1.</summary>
        public ChangeDetector(int capacity)
        {
            _last = new ulong[capacity];
            _seen = new bool[capacity];
        }

        /// <summary>Records <paramref name="signature"/>; true when it differs from the one recorded before (never on the first).</summary>
        public bool Observe(int id, ulong signature)
        {
            bool changed = _seen[id] && _last[id] != signature;
            _seen[id] = true;
            _last[id] = signature;
            return changed;
        }

        /// <summary>Forgets every id.</summary>
        public void Reset()
        {
            Array.Clear(_seen, 0, _seen.Length);
        }
    }
}
