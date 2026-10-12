using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.SessionTests
{
    public class TwoWheelerCrashTests
    {
        private const int BikeModel = 50;
        private const float WallZ = 12f;

        /// <summary>Player seated on a two-wheeler at the origin heading +z at the given speed, wall across the road ahead.</summary>
        private static Sim Scene(float speed, out Car bike)
        {
            var sim = new Sim(city: c => c.AddVehicleModel(new VehicleModel
            {
                Id = BikeModel, Name = "Bike", Kind = VehicleKind.Car, TwoWheeler = true,
                HalfExtents = new Vec3(0.35f, 0.6f, 0.9f)
            }));
            new Vehicles.Terrain().WallAcross(-50f, 50f, WallZ, 0f, 10f).Apply(sim.S.World.Collision, 7002);
            bike = sim.W.SpawnCar(BikeModel, Vec3.Zero, 0f, CarOwner.Abandoned, 0x112233);
            Assert.NotNull(bike);
            sim.W.PutInCar(sim.Player, bike, 0);
            bike.Body.SetVelocity(new Vec3(0f, 0f, speed), Vec3.Zero);
            return sim;
        }

        [Fact]
        public void CrashingAt12_ThrowsThePlayerOff_HurtsThem_AndLeavesTheBikeFallen()
        {
            Car bike;
            Sim sim = Scene(12f, out bike);
            float hp = sim.Player.Health;
            sim.Run(2.0, In.Forward);
            Assert.True(bike.Body.Fallen, "bike should be fallen");
            Assert.False(sim.Player.Has(PedFlags.InVehicle), "player should be ejected");
            Assert.True(sim.Player.Health < hp, "health " + sim.Player.Health + " of " + hp);
            Assert.True(sim.Player.Has(PedFlags.KnockedDown), "player should be knocked down");
            Assert.True(sim.Count(GameEventType.PlayerExitedCar) >= 1);
            Assert.True(bike.Body.Upright < 0.5f, "bike upright " + bike.Body.Upright);
        }

        [Fact]
        public void RollingIntoTheWallAt3_KeepsThePlayerOnTheBike_Unhurt()
        {
            Car bike;
            Sim sim = Scene(3f, out bike);
            // Start 3 m from the wall with no throttle so the bike meets it at about 3 m/s.
            bike.Body.Teleport(new Vec3(0f, bike.Body.Position.Y, WallZ - 3f), Quat.Identity);
            bike.Body.SetVelocity(new Vec3(0f, 0f, 3f), Vec3.Zero);
            float hp = sim.Player.Health;
            sim.Run(2.0, In.Idle);
            Assert.False(bike.Body.Fallen);
            Assert.True(sim.Player.Has(PedFlags.InVehicle));
            Assert.Equal(hp, sim.Player.Health);
        }
    }
}
