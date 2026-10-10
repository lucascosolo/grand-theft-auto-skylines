using System.Threading;

namespace GtaSkylines.Mod.Lifecycle
{
    /// <summary>
    /// Simulation frames withheld by a slowed simulation and owed to CS1's day clock. <see cref="Accrue"/> runs on the
    /// main thread, <see cref="Take"/> on the simulation thread; the pending count is exchanged atomically. Unity-free.
    /// </summary>
    internal sealed class SimClockDebt
    {
        /// <summary>Pending frames worth one simulation-thread action (8 of 65536 day frames: invisible on the sun).</summary>
        public const int FlushFrames = 8;

        private int _pending;
        private uint _accounted;

        /// <summary>Frames owed and not yet taken.</summary>
        public int Pending { get { return Interlocked.CompareExchange(ref _pending, 0, 0);} }

        /// <summary>True once enough is owed to queue a simulation-thread action.</summary>
        public bool ShouldFlush { get { return Pending >= FlushFrames; } }

        /// <summary>Starts accounting from zero (a new slowed session); the pending debt is kept.</summary>
        public void Reset()
        {
            _accounted = 0;
        }

        /// <summary>
        /// <paramref name="ran"/> frames ran since the session started at <paramref name="rate"/>: adds what is newly owed
        /// (frames normal speed would have run beyond those) and returns the pending debt.
        /// </summary>
        public int Accrue(uint ran, float rate)
        {
            uint owed = (uint)(ran / (double)rate) - ran;
            if (owed > _accounted)
            {
                int extra = (int)(owed - _accounted);
                _accounted = owed;
                return Interlocked.Add(ref _pending, extra);
            }
            return Pending;
        }

        /// <summary>Returns the pending debt and clears it.</summary>
        public int Take()
        {
            return Interlocked.Exchange(ref _pending, 0);
        }
    }
}
