using System;
using System.Collections.Generic;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.SessionTests
{
    /// <summary>Parked cars in a running session fall asleep, cost nothing, and wake when the player takes them.</summary>
    public class VehicleSleepSessionTests
    {
        private static List<Car> SpawnParked(Sim sim, int n, float x, float z0)
        {
            var cars = new List<Car>();
            for (int i = 0; i < n; i++) cars.Add(sim.W.SpawnCar(0, new Vec3(x, 0f, z0 + i * 20f), 0f, CarOwner.Abandoned, 0x808080));
            return cars;
        }

        [Fact]
        public void TenParkedCars_AllFallAsleep_AndStopIntegrating()
        {
            var sim = new Sim();
            List<Car> cars = SpawnParked(sim, 10, 60f, -100f);
            sim.Run(5.0, In.Idle);
            long[] mid = new long[10];
            for (int i = 0; i < 10; i++) mid[i] = cars[i].Body.IntegratedSteps;
            sim.Run(5.0, In.Idle);
            for (int i = 0; i < 10; i++)
            {
                Assert.True(cars[i].Body.IsSleeping, "car " + i + " is awake");
                Assert.Equal(mid[i], cars[i].Body.IntegratedSteps);
            }
        }

        [Fact]
        public void SleepingCar_ThePlayerEnters_WakesAndRespondsToThrottle()
        {
            var sim = new Sim();
            Car car = sim.W.SpawnCar(0, new Vec3(30f, 0f, 0f), 0f, CarOwner.Abandoned, 0x112233);
            sim.Run(3.0, In.Idle);
            Assert.True(car.Body.IsSleeping, "precondition: the abandoned car sleeps");
            sim.W.PutInCar(sim.Player, car, 0);
            Assert.False(car.Body.IsSleeping);
            Vec3 start = car.Position;
            sim.Run(2.0, In.Forward);
            Assert.True((car.Position - start).Length > 1f, "car did not move under throttle: " + (car.Position - start).Length);
        }

        private static long DrivenAllocation(int parked)
        {
            var sim = new Sim();
            List<Car> cars = SpawnParked(sim, parked, -300f, -180f);
            Car mine = sim.W.SpawnCar(0, Vec3.Zero, 0f, CarOwner.Abandoned, 0x112233);
            sim.W.PutInCar(sim.Player, mine, 0);
            InputFrame drive = In.Move(0.4f, 0.6f);
            for (int i = 0; i < 400; i++) sim.S.Tick(Sim.Dt, drive, sim.Snap);
            foreach (Car c in cars) Assert.True(c.Body.IsSleeping);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 600; i++) sim.S.Tick(Sim.Dt, drive, sim.Snap);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        [Fact]
        public void TenSleepingCars_AddNoAllocation_ToADrivenCarsSteadyState()
        {
            long baseline = DrivenAllocation(0);
            long withSleepers = DrivenAllocation(10);
            Assert.True(withSleepers <= baseline, "10 sleepers allocated " + withSleepers + " bytes vs " + baseline + " with none");
        }
    }
}
