using System.Collections.Generic;

namespace GtaSkylines.Mod.City
{
    /// <summary>
    /// What the Game has taken from CS1, so the snapshot does not report it again: instances and vehicles from the
    /// moment their take is queued until the simulation thread has released them (after that CS1 may reuse the id for
    /// someone else), and citizens for as long as the Game holds them as ped actors (until returned or killed).
    /// Thread-safe (main and simulation threads); allocates only when a set grows.
    /// </summary>
    public sealed class TakenIds
    {
        private readonly object _sync = new object();
        private readonly HashSet<ushort> _instances = new HashSet<ushort>();
        private readonly HashSet<ushort> _vehicles = new HashSet<ushort>();
        private readonly HashSet<ushort> _parked = new HashSet<ushort>();
        private readonly HashSet<uint> _citizens = new HashSet<uint>();

        /// <summary>True while a take of this citizen instance is pending.</summary>
        public bool Instance(ushort id) { lock (_sync) return _instances.Contains(id); }

        /// <summary>True while the Game holds this citizen.</summary>
        public bool Citizen(uint id) { lock (_sync) return _citizens.Contains(id); }

        /// <summary>True while a take of this vehicle (parked or moving) is pending.</summary>
        public bool Vehicle(ushort id, bool parked) { lock (_sync) return parked ? _parked.Contains(id) : _vehicles.Contains(id); }

        internal void SetInstance(ushort id, bool taken) { lock (_sync) { if (taken) _instances.Add(id); else _instances.Remove(id); } }

        internal void SetCitizen(uint id, bool held) { lock (_sync) { if (held) _citizens.Add(id); else _citizens.Remove(id); } }

        internal void SetVehicle(ushort id, bool parked, bool taken)
        {
            lock (_sync)
            {
                HashSet<ushort> set = parked ? _parked : _vehicles;
                if (taken) set.Add(id);
                else set.Remove(id);
            }
        }

        /// <summary>Forgets everything (city unload, play mode end).</summary>
        public void Clear()
        {
            lock (_sync)
            {
                _instances.Clear();
                _vehicles.Clear();
                _parked.Clear();
                _citizens.Clear();
            }
        }
    }
}
