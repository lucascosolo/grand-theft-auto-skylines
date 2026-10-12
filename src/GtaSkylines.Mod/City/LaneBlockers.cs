using System.Collections.Generic;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Mod.City
{
    /// <summary>One F-VEH-8 lane block: the Game car's pose and the invisible CS1 vehicle standing in for it.</summary>
    public struct LaneBlocker
    {
        /// <summary>The Game car's tag.</summary>
        public int Tag;
        /// <summary>Ground pivot, world metres.</summary>
        public Vec3 Pivot;
        /// <summary>Facing, degrees.</summary>
        public float YawDeg;
        /// <summary>Game vehicle model id.</summary>
        public int Model;
        /// <summary>Road segment id.</summary>
        public int Segment;
        /// <summary>The CS1 vehicle, 0 while none exists.</summary>
        public ushort Vehicle;
        /// <summary>No CS1 vehicle could be kept: only traffic pressure is added (reroute, no queue).</summary>
        public bool TrafficOnly;
    }

    /// <summary>
    /// The lane blocks the Mod keeps in CS1, by Game car tag (docs/contracts/C2-abandoned-cars.md). Thread-safe (main and
    /// simulation threads); allocates only when it grows. Vehicle 0 is never a blocker.
    /// </summary>
    public sealed class LaneBlockers
    {
        private readonly object _sync = new object();
        private readonly List<LaneBlocker> _items = new List<LaneBlocker>();

        /// <summary>Number of records.</summary>
        public int Count { get { lock (_sync) return _items.Count; } }

        /// <summary>Adds <paramref name="blocker"/>, replacing any record with the same tag.</summary>
        public void Add(LaneBlocker blocker)
        {
            lock (_sync)
            {
                int i = IndexOf(blocker.Tag);
                if (i >= 0) _items[i] = blocker;
                else _items.Add(blocker);
            }
        }

        /// <summary>Removes the record of <paramref name="tag"/>; false when there is none.</summary>
        public bool Remove(int tag, out LaneBlocker removed)
        {
            lock (_sync)
            {
                int i = IndexOf(tag);
                removed = i >= 0 ? _items[i] : default(LaneBlocker);
                if (i < 0) return false;
                _items.RemoveAt(i);
                return true;
            }
        }

        /// <summary>True when <paramref name="vehicle"/> (≠ 0) is the CS1 vehicle of some record.</summary>
        public bool IsBlocker(ushort vehicle)
        {
            if (vehicle == 0) return false;
            lock (_sync)
            {
                for (int i = 0; i < _items.Count; i++) if (_items[i].Vehicle == vehicle) return true;
                return false;
            }
        }

        /// <summary>Sets the CS1 vehicle and traffic-only flag of <paramref name="tag"/>'s record; no-op when there is none.</summary>
        public void SetVehicle(int tag, ushort vehicle, bool trafficOnly)
        {
            lock (_sync)
            {
                int i = IndexOf(tag);
                if (i < 0) return;
                LaneBlocker b = _items[i];
                b.Vehicle = vehicle;
                b.TrafficOnly = trafficOnly;
                _items[i] = b;
            }
        }

        /// <summary>Clears <paramref name="into"/> and copies every record into it.</summary>
        public void CopyTo(List<LaneBlocker> into)
        {
            into.Clear();
            lock (_sync) into.AddRange(_items);
        }

        /// <summary>Clears <paramref name="vehicles"/>, lists every non-zero CS1 vehicle in it and sets each record's vehicle to 0 (records stay).</summary>
        public void DetachAll(List<ushort> vehicles)
        {
            vehicles.Clear();
            lock (_sync)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    LaneBlocker b = _items[i];
                    if (b.Vehicle == 0) continue;
                    vehicles.Add(b.Vehicle);
                    b.Vehicle = 0;
                    _items[i] = b;
                }
            }
        }

        /// <summary>Clears <paramref name="vehicles"/>, lists every non-zero CS1 vehicle in it and forgets all records.</summary>
        public void Clear(List<ushort> vehicles)
        {
            DetachAll(vehicles);
            lock (_sync) _items.Clear();
        }

        private int IndexOf(int tag)
        {
            for (int i = 0; i < _items.Count; i++) if (_items[i].Tag == tag) return i;
            return -1;
        }
    }
}
