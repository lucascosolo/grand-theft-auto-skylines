using System.Collections.Generic;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Mod.City;
using Xunit;

namespace GtaSkylines.Mod.Tests.City
{
    public class DepartureTrackerTests
    {
        private sealed class FakeDepartureCity : IDepartureCity
        {
            public readonly HashSet<ushort> ParkedAlive = new HashSet<ushort>();
            public readonly Dictionary<uint, ushort> Vehicles = new Dictionary<uint, ushort>();
            public readonly Dictionary<ushort, KeyValuePair<Vec3, int>> Spawned = new Dictionary<ushort, KeyValuePair<Vec3, int>>();
            public readonly HashSet<ushort> Unspawned = new HashSet<ushort>();
            public bool Sees;

            public bool ParkedStillThere(ushort parked, uint owner) { return ParkedAlive.Contains(parked); }
            public ushort VehicleOf(uint citizen) { ushort v; return Vehicles.TryGetValue(citizen, out v) ? v : (ushort)0; }

            public bool TryVehicle(ushort vehicle, out bool spawned, out Vec3 pivot, out int model)
            {
                spawned = false; pivot = default(Vec3); model = 0;
                KeyValuePair<Vec3, int> s;
                if (Spawned.TryGetValue(vehicle, out s)) { spawned = true; pivot = s.Key; model = s.Value; return true; }
                return Unspawned.Contains(vehicle);
            }

            public bool PlayerSees(Vec3 point) { return Sees; }
        }

        private const ushort Parked = 10;
        private const ushort Moving = 77;
        private const uint Owner = 5;
        private const int Model = 3;
        private static readonly Vec3 Pivot = new Vec3(100, 0, 100);
        private static readonly Vec3 Centre = new Vec3(100, 0.8f, 102);
        private static readonly Quat Rot = new Quat(0, 0.5f, 0, 0.866f);
        private static readonly Vec3 Player = new Vec3(100, 0, 100);

        private static DepartureTracker Remember(uint owner = Owner)
        {
            var t = new DepartureTracker();
            t.BeginFrame();
            t.SeeParked(Parked, owner, Pivot, Centre, Rot, Model, 0xABCDEF);
            t.Update(new FakeDepartureCity(), Player);
            return t;
        }

        private static void Vanish(DepartureTracker t, FakeDepartureCity c, Vec3? player = null)
        {
            t.BeginFrame();
            t.Update(c, player ?? Player);
        }

        private static FakeDepartureCity Departed()
        {
            var c = new FakeDepartureCity();
            c.Vehicles[Owner] = Moving;
            return c;
        }

        [Fact]
        public void Vanished_car_with_unspawned_vehicle_starts_ghost_with_remembered_pose_and_moving_id()
        {
            DepartureTracker t = Remember();
            FakeDepartureCity c = Departed();
            c.Unspawned.Add(Moving);
            Vanish(t, c);
            DepartureGhost g = Assert.Single(t.Ghosts);
            Assert.Equal(Moving, g.Vehicle);
            Assert.Equal(Parked, g.Parked);
            Assert.Equal(Owner, g.Owner);
            Assert.Equal(Pivot, g.Pivot);
            Assert.Equal(Centre, g.Centre);
            Assert.Equal(Rot, g.Rotation);
            Assert.Equal(Model, g.Model);
            Assert.Equal(0xABCDEF, g.ColourRgb);
            Assert.False(g.Elsewhere);
        }

        [Fact]
        public void Ghost_stays_while_vehicle_is_unspawned()
        {
            DepartureTracker t = Remember();
            FakeDepartureCity c = Departed();
            c.Unspawned.Add(Moving);
            Vanish(t, c);
            Vanish(t, c);
            Vanish(t, c);
            Assert.Single(t.Ghosts);
            Assert.Empty(t.Events);
        }

        [Fact]
        public void Ghost_ends_InPlace_when_vehicle_spawns_within_two_metres()
        {
            DepartureTracker t = Remember();
            FakeDepartureCity c = Departed();
            c.Unspawned.Add(Moving);
            Vanish(t, c);
            c.Unspawned.Clear();
            c.Spawned[Moving] = new KeyValuePair<Vec3, int>(Pivot + new Vec3(1.5f, 0, 0), Model);
            Vanish(t, c);
            Assert.Empty(t.Ghosts);
            GhostEvent e = Assert.Single(t.Events);
            Assert.False(e.Started);
            Assert.Equal(GhostEnd.InPlace, e.End);
            Assert.Equal(Moving, e.Ghost.Vehicle);
        }

        [Fact]
        public void Ghost_ends_Released_when_vehicle_is_gone()
        {
            DepartureTracker t = Remember();
            FakeDepartureCity c = Departed();
            c.Unspawned.Add(Moving);
            Vanish(t, c);
            c.Unspawned.Clear();
            Vanish(t, c);
            Assert.Empty(t.Ghosts);
            Assert.Equal(GhostEnd.Released, Assert.Single(t.Events).End);
        }

        [Fact]
        public void Vehicle_spawned_far_makes_ghost_Elsewhere_which_stays_while_near_or_seen()
        {
            DepartureTracker t = Remember();
            FakeDepartureCity c = Departed();
            c.Unspawned.Add(Moving);
            Vanish(t, c);
            c.Unspawned.Clear();
            c.Spawned[Moving] = new KeyValuePair<Vec3, int>(Pivot + new Vec3(10, 0, 0), Model);
            Vanish(t, c);
            Assert.True(Assert.Single(t.Ghosts).Elsewhere);
            Assert.Empty(t.Events);

            Vanish(t, c, new Vec3(100, 0, 100 + 59f));          // near, unseen
            Assert.Single(t.Ghosts);
            c.Sees = true;
            Vanish(t, c, new Vec3(100, 0, 100 + 200f));         // far, seen
            Assert.Single(t.Ghosts);
        }

        [Fact]
        public void Elsewhere_ghost_ends_OutOfSight_when_far_and_unseen_even_if_vehicle_is_gone()
        {
            DepartureTracker t = Remember();
            FakeDepartureCity c = Departed();
            c.Unspawned.Add(Moving);
            Vanish(t, c);
            c.Unspawned.Clear();
            c.Spawned[Moving] = new KeyValuePair<Vec3, int>(Pivot + new Vec3(10, 0, 0), Model);
            Vanish(t, c);
            c.Spawned.Clear();                                    // vehicle vanished; Elsewhere ignores that
            Vanish(t, c);
            Assert.Single(t.Ghosts);
            Vanish(t, c, new Vec3(100, 0, 100 + 70f));
            Assert.Empty(t.Ghosts);
            GhostEvent e = Assert.Single(t.Events);
            Assert.Equal(GhostEnd.OutOfSight, e.End);
            Assert.False(e.Started);
        }

        [Fact]
        public void Vehicle_already_spawned_in_place_at_first_observation_makes_no_ghost()
        {
            DepartureTracker t = Remember();
            FakeDepartureCity c = Departed();
            c.Spawned[Moving] = new KeyValuePair<Vec3, int>(Pivot + new Vec3(0.5f, 0, 0), Model);
            Vanish(t, c);
            Assert.Empty(t.Ghosts);
            Assert.Empty(t.Events);
        }

        [Fact]
        public void Vehicle_already_spawned_far_with_same_model_makes_Elsewhere_ghost()
        {
            DepartureTracker t = Remember();
            FakeDepartureCity c = Departed();
            c.Spawned[Moving] = new KeyValuePair<Vec3, int>(Pivot + new Vec3(30, 0, 0), Model);
            Vanish(t, c);
            Assert.True(Assert.Single(t.Ghosts).Elsewhere);
            Assert.True(Assert.Single(t.Events).Started);
        }

        [Fact]
        public void Vehicle_already_spawned_far_with_different_model_makes_no_ghost()
        {
            DepartureTracker t = Remember();
            FakeDepartureCity c = Departed();
            c.Spawned[Moving] = new KeyValuePair<Vec3, int>(Pivot + new Vec3(30, 0, 0), Model + 1);
            Vanish(t, c);
            Assert.Empty(t.Ghosts);
        }

        [Fact]
        public void Parked_car_that_still_exists_leaves_range_without_ghost()
        {
            DepartureTracker t = Remember();
            FakeDepartureCity c = Departed();
            c.Unspawned.Add(Moving);
            c.ParkedAlive.Add(Parked);
            Vanish(t, c);
            Assert.Empty(t.Ghosts);
        }

        [Fact]
        public void Owner_zero_makes_no_ghost()
        {
            DepartureTracker t = Remember(0);
            FakeDepartureCity c = Departed();
            c.Vehicles[0] = Moving;
            c.Unspawned.Add(Moving);
            Vanish(t, c);
            Assert.Empty(t.Ghosts);
        }

        [Fact]
        public void Owner_without_vehicle_or_unknown_vehicle_makes_no_ghost()
        {
            DepartureTracker t = Remember();
            Vanish(t, new FakeDepartureCity());                  // VehicleOf = 0
            Assert.Empty(t.Ghosts);

            t = Remember();
            Vanish(t, Departed());                               // TryVehicle false
            Assert.Empty(t.Ghosts);
        }

        [Fact]
        public void Car_seen_this_frame_is_never_a_departure()
        {
            DepartureTracker t = Remember();
            FakeDepartureCity c = Departed();
            c.Unspawned.Add(Moving);
            t.BeginFrame();
            t.SeeParked(Parked, Owner, Pivot, Centre, Rot, Model, 0xABCDEF);
            t.Update(c, Player);
            Assert.Empty(t.Ghosts);
        }

        [Fact]
        public void No_duplicate_ghost_for_the_same_vehicle()
        {
            var t = new DepartureTracker();
            t.BeginFrame();
            t.SeeParked(Parked, Owner, Pivot, Centre, Rot, Model, 1);
            t.SeeParked(11, Owner, Pivot, Centre, Rot, Model, 1);
            t.Update(new FakeDepartureCity(), Player);
            FakeDepartureCity c = Departed();
            c.Unspawned.Add(Moving);
            Vanish(t, c);
            Assert.Single(t.Ghosts);
            Assert.Single(t.Events);
        }

        [Fact]
        public void Clear_forgets_remembered_cars_ghosts_and_events()
        {
            DepartureTracker t = Remember();
            FakeDepartureCity c = Departed();
            c.Unspawned.Add(Moving);
            Vanish(t, c);
            Assert.Single(t.Ghosts);
            t.Clear();
            Assert.Empty(t.Ghosts);
            Assert.Empty(t.Events);

            t = Remember();
            t.Clear();
            Vanish(t, c);
            Assert.Empty(t.Ghosts);
        }

        [Fact]
        public void Events_report_starts_then_ends_with_reasons_and_reset_each_update()
        {
            DepartureTracker t = Remember();
            FakeDepartureCity c = Departed();
            c.Unspawned.Add(Moving);
            Vanish(t, c);
            GhostEvent started = Assert.Single(t.Events);
            Assert.True(started.Started);
            Assert.Equal(GhostEnd.None, started.End);
            Assert.Equal(Moving, started.Ghost.Vehicle);

            Vanish(t, c);
            Assert.Empty(t.Events);

            c.Unspawned.Clear();
            Vanish(t, c);
            Assert.Equal(GhostEnd.Released, Assert.Single(t.Events).End);
        }

        [Fact]
        public void Ghost_started_and_ended_in_the_same_update_reports_both_events_in_order()
        {
            DepartureTracker t = Remember();
            FakeDepartureCity c = Departed();
            c.Spawned[Moving] = new KeyValuePair<Vec3, int>(Pivot + new Vec3(30, 0, 0), Model);
            Vanish(t, c, new Vec3(100, 0, 100 + 500f));           // far from the ghost, unseen
            Assert.Empty(t.Ghosts);
            Assert.Equal(2, t.Events.Count);
            Assert.True(t.Events[0].Started);
            Assert.Equal(GhostEnd.OutOfSight, t.Events[1].End);
        }
    }
}
