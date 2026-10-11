using System.Collections.Generic;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Session;

namespace GtaSkylines.Mod.City
{
    /// <summary>What <see cref="DepartureTracker"/> reads from the city (CS1 in the Mod, a fake in tests).</summary>
    public interface IDepartureCity
    {
        /// <summary>True while parked vehicle <paramref name="parked"/> is created, not deleted and owned by <paramref name="owner"/>.</summary>
        bool ParkedStillThere(ushort parked, uint owner);
        /// <summary>The citizen's current vehicle (<c>Citizen.m_vehicle</c>), 0 for none.</summary>
        ushort VehicleOf(uint citizen);
        /// <summary>False when the vehicle is 0, not created or deleted; else whether it is spawned, its pivot and model id.</summary>
        bool TryVehicle(ushort vehicle, out bool spawned, out Vec3 pivot, out int model);
        /// <summary>True when the player's camera could see <paramref name="point"/>.</summary>
        bool PlayerSees(Vec3 point);
    }

    /// <summary>Why a departing ghost ended.</summary>
    public enum GhostEnd
    {
        /// <summary>Not ended (a start event).</summary>
        None,
        /// <summary>CS1 spawned the moving car at the parked spot and draws it there.</summary>
        InPlace,
        /// <summary>The moving car no longer exists (CS1 gave up, or the Game took it).</summary>
        Released,
        /// <summary>CS1 spawned the car elsewhere and the ghost is now far and unseen.</summary>
        OutOfSight,
    }

    /// <summary>A parked car whose owner drove off, still drawn and solid at its spot (F-VEH-7).</summary>
    public struct DepartureGhost
    {
        /// <summary>CS1's moving vehicle that replaced the parked car (the id the Game takes it by).</summary>
        public ushort Vehicle;
        /// <summary>The released parked vehicle id (for logs).</summary>
        public ushort Parked;
        /// <summary>Owner citizen, the moving car's driver.</summary>
        public uint Owner;
        /// <summary>Parked pivot (CS1 vehicle origin).</summary>
        public Vec3 Pivot;
        /// <summary>Model bounds centre.</summary>
        public Vec3 Centre;
        /// <summary>Parked orientation.</summary>
        public Quat Rotation;
        /// <summary>Vehicle model id.</summary>
        public int Model;
        /// <summary>Parked colour, 0xRRGGBB.</summary>
        public int ColourRgb;
        /// <summary>CS1 spawned the moving car more than <see cref="DepartureTracker.InPlaceMetres"/> away: draw only, not takeable.</summary>
        public bool Elsewhere;
    }

    /// <summary>A ghost started or ended during the last <see cref="DepartureTracker.Update"/>.</summary>
    public struct GhostEvent
    {
        /// <summary>The ghost as it was.</summary>
        public DepartureGhost Ghost;
        /// <summary>True for a start (then <see cref="End"/> is None).</summary>
        public bool Started;
        /// <summary>Why it ended.</summary>
        public GhostEnd End;
    }

    /// <summary>
    /// Remembers the parked cars near the player and turns one into a <see cref="DepartureGhost"/> when CS1 releases it
    /// because its owner's moving car replaced it and that car is not drawn at the spot (unspawned, or spawned
    /// elsewhere). Pure; steady state allocates nothing.
    /// </summary>
    public sealed class DepartureTracker
    {
        /// <summary>A moving car spawned this close to the parked pivot is CS1 drawing it in place.</summary>
        public const float InPlaceMetres = 2f;
        /// <summary>An Elsewhere ghost ends only beyond this distance and out of sight (the Game's demotion sight rule).</summary>
        public const float UnseenDistance = PlayerRules.DemoteUnseenDistance;

        private struct Remembered
        {
            public uint Owner;
            public Vec3 Pivot, Centre;
            public Quat Rotation;
            public int Model, ColourRgb, Frame;
        }

        private readonly Dictionary<ushort, Remembered> _parked = new Dictionary<ushort, Remembered>();
        private readonly List<ushort> _stale = new List<ushort>();
        private readonly List<DepartureGhost> _ghosts = new List<DepartureGhost>();
        private readonly List<GhostEvent> _events = new List<GhostEvent>();
        private int _frame;

        /// <summary>Live ghosts.</summary>
        public IList<DepartureGhost> Ghosts { get { return _ghosts; } }

        /// <summary>Starts and ends of the last <see cref="Update"/>, in order.</summary>
        public IList<GhostEvent> Events { get { return _events; } }

        /// <summary>Starts a frame: parked cars not seen again before <see cref="Update"/> are gone from range.</summary>
        public void BeginFrame() { _frame++; }

        /// <summary>A parked car in range this frame.</summary>
        public void SeeParked(ushort parked, uint owner, Vec3 pivot, Vec3 centre, Quat rotation, int model, int colourRgb)
        {
            _parked[parked] = new Remembered
            {
                Owner = owner, Pivot = pivot, Centre = centre, Rotation = rotation, Model = model, ColourRgb = colourRgb, Frame = _frame,
            };
        }

        /// <summary>Forgets unseen parked cars (starting ghosts for departures), then ends ghosts as described on the type's members.</summary>
        public void Update(IDepartureCity city, Vec3 player)
        {
            _events.Clear();
            foreach (KeyValuePair<ushort, Remembered> kv in _parked) if (kv.Value.Frame != _frame) _stale.Add(kv.Key);
            for (int i = 0; i < _stale.Count; i++)
            {
                ushort id = _stale[i];
                Remembered r = _parked[id];
                _parked.Remove(id);
                if (r.Owner != 0 && !city.ParkedStillThere(id, r.Owner)) Depart(city, id, r);
            }
            _stale.Clear();
            for (int i = _ghosts.Count - 1; i >= 0; i--)
            {
                DepartureGhost g = _ghosts[i];
                GhostEnd end = Check(city, player, ref g);
                if (end == GhostEnd.None)
                {
                    _ghosts[i] = g;
                    continue;
                }
                _ghosts.RemoveAt(i);
                _events.Add(new GhostEvent { Ghost = g, End = end });
            }
        }

        /// <summary>Forgets everything.</summary>
        public void Clear()
        {
            _parked.Clear();
            _stale.Clear();
            _ghosts.Clear();
            _events.Clear();
        }

        private void Depart(IDepartureCity city, ushort parked, Remembered r)
        {
            ushort v = city.VehicleOf(r.Owner);
            if (v == 0 || HasGhost(v)) return;
            bool spawned;
            Vec3 pivot;
            int model;
            if (!city.TryVehicle(v, out spawned, out pivot, out model)) return;
            bool elsewhere = spawned && model == r.Model && Vec3.Distance(pivot, r.Pivot) > InPlaceMetres;
            if (spawned && !elsewhere) return;
            var g = new DepartureGhost
            {
                Vehicle = v, Parked = parked, Owner = r.Owner, Pivot = r.Pivot, Centre = r.Centre, Rotation = r.Rotation,
                Model = r.Model, ColourRgb = r.ColourRgb, Elsewhere = elsewhere,
            };
            _ghosts.Add(g);
            _events.Add(new GhostEvent { Ghost = g, Started = true });
        }

        private static GhostEnd Check(IDepartureCity city, Vec3 player, ref DepartureGhost g)
        {
            if (!g.Elsewhere)
            {
                bool spawned;
                Vec3 pivot;
                int model;
                if (!city.TryVehicle(g.Vehicle, out spawned, out pivot, out model)) return GhostEnd.Released;
                if (!spawned) return GhostEnd.None;
                if (Vec3.Distance(pivot, g.Pivot) <= InPlaceMetres) return GhostEnd.InPlace;
                g.Elsewhere = true;
            }
            return Vec3.DistanceXZ(g.Centre, player) > UnseenDistance && !city.PlayerSees(g.Centre) ? GhostEnd.OutOfSight : GhostEnd.None;
        }

        private bool HasGhost(ushort vehicle)
        {
            for (int i = 0; i < _ghosts.Count; i++) if (_ghosts[i].Vehicle == vehicle) return true;
            return false;
        }
    }
}
