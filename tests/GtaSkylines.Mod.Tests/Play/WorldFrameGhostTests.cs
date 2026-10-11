using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.World;
using GtaSkylines.Mod.City;
using GtaSkylines.Mod.Play;
using GtaSkylines.Mod.Rendering;
using Xunit;

namespace GtaSkylines.Mod.Tests.Play
{
    public class WorldFrameGhostTests
    {
        private sealed class GhostCity : IDepartureCity
        {
            public ushort Moving = 77;
            public bool Spawned;
            public Vec3 SpawnPivot;
            public int SpawnModel = 3;
            public bool ParkedStillThere(ushort parked, uint owner) { return false; }
            public ushort VehicleOf(uint citizen) { return Moving; }
            public bool TryVehicle(ushort vehicle, out bool spawned, out Vec3 pivot, out int model)
            {
                spawned = Spawned; pivot = SpawnPivot; model = SpawnModel; return true;
            }
            public bool PlayerSees(Vec3 point) { return true; }
        }

        private static readonly Vec3 Pivot = new Vec3(100, 0, 100);
        private static readonly Vec3 Centre = new Vec3(100, 0.8f, 102);
        private static readonly Quat Rot = new Quat(0, 0.5f, 0, 0.866f);

        private static DepartureTracker WithGhost(GhostCity city)
        {
            var t = new DepartureTracker();
            t.BeginFrame();
            t.SeeParked(10, 5, Pivot, Centre, Rot, 3, 0x445566);
            t.Update(city, Pivot);
            t.BeginFrame();
            t.Update(city, Pivot);
            return t;
        }

        private static RenderFrame Fill(WorldFrameBuilder b, PlayGame g)
        {
            var f = new RenderFrame();
            b.Fill(f, g.Parts, 0.1f);
            return f;
        }

        [Fact]
        public void Null_departures_changes_nothing()
        {
            var g = new PlayGame();
            g.SpawnCar(1, new Vec3(10, 0, 10));
            Assert.Single(Fill(new WorldFrameBuilder { Departures = null }, g).Cars);
        }

        [Fact]
        public void Ghost_is_appended_after_game_cars_with_stationary_defaults()
        {
            var g = new PlayGame();
            g.SpawnCar(1, new Vec3(10, 0, 10));
            DepartureTracker t = WithGhost(new GhostCity());
            Assert.Single(t.Ghosts);
            RenderFrame f = Fill(new WorldFrameBuilder { Departures = t }, g);
            Assert.Equal(2, f.Cars.Count);
            CarDraw d = f.Cars[1];
            Assert.Equal(3, d.ModelId);
            Assert.Equal(Centre, d.Centre);
            Assert.Equal(Centre, d.PrevCentre);
            Assert.Equal(Rot, d.Rotation);
            Assert.Equal(Rot, d.PrevRotation);
            Assert.Equal(0x445566, d.ColourRgb);
            Assert.Equal(Vec3.Zero, d.Velocity);
            Assert.False(d.Headlights);
            Assert.False(d.Brake);
            Assert.False(d.Siren);
            Assert.Equal(0f, d.CompressionFL);
            Assert.Equal(0f, d.CompressionFR);
            Assert.Equal(0f, d.CompressionRL);
            Assert.Equal(0f, d.CompressionRR);
            Assert.Equal(CarDamage.Intact, d.Damage);
        }

        [Fact]
        public void Ghost_is_skipped_when_its_vehicle_is_already_a_drawn_game_car()
        {
            var g = new PlayGame();
            Car car = g.SpawnCar(1, new Vec3(10, 0, 10));
            car.SourceCs1Vehicle = 77;
            car.SourceParked = false;
            DepartureTracker t = WithGhost(new GhostCity());
            Assert.Single(Fill(new WorldFrameBuilder { Departures = t }, g).Cars);
        }

        [Fact]
        public void Ghost_is_kept_when_the_matching_game_car_is_hidden_by_the_gate_or_was_parked()
        {
            var g = new PlayGame();
            Car car = g.SpawnCar(1, new Vec3(10, 0, 10));
            car.SourceCs1Vehicle = 77;
            DepartureTracker t = WithGhost(new GhostCity());
            var gate = new FakeGate { Cars = false };
            RenderFrame hidden = Fill(new WorldFrameBuilder { Departures = t, Gate = gate }, g);
            Assert.Single(hidden.Cars);
            Assert.Equal(Centre, hidden.Cars[0].Centre);

            car.SourceParked = true;
            Assert.Equal(2, Fill(new WorldFrameBuilder { Departures = t }, g).Cars.Count);
        }

        [Fact]
        public void Elsewhere_ghost_is_never_skipped_for_a_matching_game_car()
        {
            var g = new PlayGame();
            Car car = g.SpawnCar(1, new Vec3(10, 0, 10));
            car.SourceCs1Vehicle = 77;
            var city = new GhostCity { Spawned = true, SpawnPivot = Pivot + new Vec3(30, 0, 0) };
            DepartureTracker t = WithGhost(city);
            Assert.True(Assert.Single(t.Ghosts).Elsewhere);
            Assert.Equal(2, Fill(new WorldFrameBuilder { Departures = t }, g).Cars.Count);
        }
    }
}
