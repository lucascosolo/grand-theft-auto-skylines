using System;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Vehicles;
using Xunit;

namespace GtaSkylines.Game.Tests.Vehicles
{
    public class VehicleAllocationTests
    {
        private static long Measure(Sim s, int steps, Func<Sim, VehicleControls> controls)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < steps; i++)
            {
                s.Step(controls(s));
            }
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        [Fact]
        public void Step_OnOpenGround_AllocatesNothingAfterWarmUp()
        {
            var s = new Sim(V.Car(), new Terrain().Flat(2000f).ToWorld());
            Func<Sim, VehicleControls> c = sim => sim.Hold(15f, 0.3f);
            Measure(s, 300, c);
            Assert.Equal(0L, Measure(s, 600, c));
        }

        [Fact]
        public void Step_WithDynamicShapesNearby_AllocatesNothingAfterWarmUp()
        {
            var s = new Sim(V.Car(), new Terrain().Flat(2000f).ToWorld());
            s.World.AddBox(new Vec3(4f, 0.75f, 3f), new Vec3(0.9f, 0.75f, 2.2f), Quat.Identity, 9);
            s.World.AddCapsule(new Vec3(-3f, 0f, 2f), 0.3f, 1.8f, 10);
            Func<Sim, VehicleControls> c = sim => new VehicleControls { Throttle = 0.1f, Steer = 1f };
            Measure(s, 300, c);
            Assert.Equal(0L, Measure(s, 600, c));
        }

        [Fact]
        public void Step_ScrapingAWall_AllocatesNothingAfterWarmUp()
        {
            var w = new Terrain().Flat(2000f).ToWorld(1);
            new Terrain().WallAcross(-100f, 100f, 40f, 0f, 12f).Apply(w, 2);
            var s = new Sim(V.Car(), w, new Vec3(0f, 0.77f, 30f), Quat.Yaw(20f));
            Func<Sim, VehicleControls> c = sim => new VehicleControls { Throttle = 1f };
            Measure(s, 300, c);
            Assert.Equal(0L, Measure(s, 600, c));
        }

        [Fact]
        public void Step_WithAnyDt_AllocatesNothing_IncludingSubSteppedLongSteps()
        {
            var s = new Sim(V.Car(), new Terrain().Flat(2000f).ToWorld());
            for (int i = 0; i < 300; i++) s.Body.Step(0.1f, new VehicleControls { Throttle = 1f }, s.World, V.Tag);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 300; i++) s.Body.Step(0.1f, new VehicleControls { Throttle = 1f }, s.World, V.Tag);
            Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        [Fact]
        public void ResolveCollision_AllocatesNothingAfterWarmUp()
        {
            var world = new Terrain().Flat(2000f).ToWorld();
            var a = new Sim(V.Car(), world, new Vec3(0f, 0.77f, 0f), Quat.Identity);
            var b = new Sim(V.Car(), world, new Vec3(0f, 0.77f, 3.5f), Quat.Identity);
            VehicleImpact ia, ib;
            for (int i = 0; i < 100; i++)
            {
                b.Body.Teleport(new Vec3(0f, 0.77f, 3.5f), Quat.Identity);
                VehicleBody.ResolveCollision(a.Body, 1, b.Body, 2, out ia, out ib);
            }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 300; i++)
            {
                b.Body.Teleport(new Vec3(0f, 0.77f, 3.5f), Quat.Identity);
                VehicleBody.ResolveCollision(a.Body, 1, b.Body, 2, out ia, out ib);
            }
            Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        }
    }
}
