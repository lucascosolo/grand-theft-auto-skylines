using System;
using System.Collections.Generic;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Vehicles;
using Xunit;

namespace GtaSkylines.Game.Tests.Vehicles
{
    /// <summary>Sleeping of resting vehicle bodies: when they sleep, what wakes them, and what a sleeper still does.</summary>
    public class VehicleSleepTests
    {
        private static VehicleControls Parked()
        {
            return new VehicleControls { Throttle = 1f, Brake = 1f, Handbrake = true };
        }

        private static Sim Settled(CollisionWorld world, Vec3 pos)
        {
            var s = new Sim(V.Car(), world, pos);
            for (int i = 0; i < 300 && !s.Body.IsSleeping; i++) s.Step(Parked());
            Assert.True(s.Body.IsSleeping, "parked car never fell asleep");
            return s;
        }

        private static CollisionWorld Flat() { return new Terrain().Flat(2000f).ToWorld(); }

        /// <summary>One frame of a two-car scene, as VehicleSystem does it: step both, then resolve the pair.</summary>
        private static void Frame(Sim a, int tagA, VehicleControls ca, Sim b, int tagB, VehicleControls cb)
        {
            a.Body.Step(V.Dt, ca, a.World, tagA);
            b.Body.Step(V.Dt, cb, b.World, tagB);
            VehicleImpact ia, ib;
            VehicleBody.ResolveCollision(a.Body, tagA, b.Body, tagB, out ia, out ib);
        }

        [Fact]
        public void NewBody_IsAwake()
        {
            var s = new Sim(V.Car(), Flat());
            Assert.False(s.Body.IsSleeping);
            Assert.Equal(0L, s.Body.IntegratedSteps);
        }

        [Fact]
        public void TenParkedCars_AllSleep_AndStopIntegratingAfterSettling()
        {
            CollisionWorld world = Flat();
            var cars = new List<Sim>();
            for (int i = 0; i < 10; i++) cars.Add(new Sim(V.Car(), world, new Vec3(i * 10f, 0.77f, 0f)));
            for (int n = 0; n < 300; n++) foreach (Sim c in cars) c.Step(Parked());
            long[] mid = new long[10];
            for (int i = 0; i < 10; i++) mid[i] = cars[i].Body.IntegratedSteps;
            for (int n = 0; n < 300; n++) foreach (Sim c in cars) c.Step(Parked());
            for (int i = 0; i < 10; i++)
            {
                Assert.True(cars[i].Body.IsSleeping, "car " + i + " is awake");
                Assert.Equal(mid[i], cars[i].Body.IntegratedSteps);
                Assert.Equal(0, cars[i].Body.LastSubsteps);
            }
        }

        [Fact]
        public void SleepingBody_KeepsItsPose_AndReturnsTheDefaultResult()
        {
            Sim s = Settled(Flat(), new Vec3(0f, 0.77f, 0f));
            Vec3 pos = s.Body.Position;
            Quat rot = s.Body.Rotation;
            VehicleStepResult r = s.Step(Parked());
            Assert.Equal(default(VehicleStepResult), r);
            Assert.Equal(pos, s.Body.Position);
            Assert.Equal(rot, s.Body.Rotation);
            Assert.Equal(Vec3.Zero, s.Body.Velocity);
        }

        [Fact]
        public void MovingBody_DoesNotSleep()
        {
            var s = new Sim(V.Car(), Flat());
            s.Run(s.Hold(10f), 3f);
            Assert.False(s.Body.IsSleeping);
        }

        [Fact]
        public void IdleControlsWithoutParkingBrake_AlsoSleep_ButDrivenControlsDoNot()
        {
            var s = new Sim(V.Car(), Flat(), new Vec3(0f, 0.77f, 0f));
            for (int i = 0; i < 300; i++) s.Step(new VehicleControls());
            Assert.True(s.Body.IsSleeping);
            s.Step(new VehicleControls { Throttle = 1f });
            Assert.False(s.Body.IsSleeping);
        }

        [Fact]
        public void Wake_Teleport_SetVelocity_Recover_ApplyDamage_AllWakeASleeper()
        {
            Sim s = Settled(Flat(), new Vec3(0f, 0.77f, 0f));
            s.Body.Wake();
            Assert.False(s.Body.IsSleeping);

            s.Body.Teleport(new Vec3(0f, 0.77f, 0f), Quat.Identity);
            Assert.False(s.Body.IsSleeping);
            for (int i = 0; i < 300 && !s.Body.IsSleeping; i++) s.Step(Parked());
            Assert.True(s.Body.IsSleeping);

            s.Body.SetVelocity(new Vec3(0f, 0f, 3f), Vec3.Zero);
            Assert.False(s.Body.IsSleeping);
            for (int i = 0; i < 600 && !s.Body.IsSleeping; i++) s.Step(Parked());
            Assert.True(s.Body.IsSleeping);

            s.Body.ApplyDamage(10f);
            Assert.False(s.Body.IsSleeping);
            for (int i = 0; i < 600 && !s.Body.IsSleeping; i++) s.Step(Parked());
            Assert.True(s.Body.IsSleeping);

            for (int i = 0; i < 600 && !s.Body.IsSleeping; i++) s.Step(Parked());
            Assert.True(s.Body.IsSleeping);

            s.Body.Recover();
            Assert.False(s.Body.IsSleeping);
        }

        [Fact]
        public void NonIdleControls_WakeASleeper_AndTheCarDrives()
        {
            Sim s = Settled(Flat(), new Vec3(0f, 0.77f, 0f));
            float z0 = s.Body.Position.Z;
            s.Run(new VehicleControls { Throttle = 1f }, 2f);
            Assert.False(s.Body.IsSleeping);
            Assert.True(s.Body.Position.Z - z0 > 1f);
        }

        [Fact]
        public void SleepingCar_HitByAMovingCar_WakesAndMoves()
        {
            CollisionWorld world = Flat();
            Sim sleeper = Settled(world, new Vec3(0f, 0.77f, 0f));
            var mover = new Sim(V.Car(), world, new Vec3(0f, 0.77f, -25f));
            Vec3 start = sleeper.Body.Position;
            for (int i = 0; i < 600; i++)
            {
                Frame(sleeper, 1, Parked(), mover, 2, mover.Hold(8f));
            }
            Assert.True((sleeper.Body.Position - start).Length > 0.2f, "sleeper did not move");
        }

        [Fact]
        public void SleepingCar_StillBlocksAMovingCar()
        {
            CollisionWorld world = Flat();
            Sim sleeper = Settled(world, new Vec3(0f, 0.77f, 0f));
            var mover = new Sim(V.Car(), world, new Vec3(0f, 0.77f, -25f));
            for (int i = 0; i < 900; i++)
            {
                Frame(sleeper, 1, Parked(), mover, 2, mover.Hold(8f));
                float sleeperRear = sleeper.Body.Position.Z - 2.2f;
                Assert.True(V.MaxCornerZ(mover.Body) <= sleeperRear + 0.5f, "mover passed through the sleeper at step " + i);
                Assert.True(mover.Body.Position.Z < sleeper.Body.Position.Z, "mover is past the sleeper's centre at step " + i);
            }
        }

        [Fact]
        public void TwoSleepers_NeverResolveAgainstEachOther_AndStayAsleep()
        {
            CollisionWorld world = Flat();
            Sim a = Settled(world, new Vec3(0f, 0.77f, 0f));
            Sim b = Settled(world, new Vec3(0f, 0.77f, 4.3f)); // boxes slightly overlapping
            Vec3 pa = a.Body.Position, pb = b.Body.Position;
            VehicleImpact ia, ib;
            for (int i = 0; i < 60; i++)
            {
                a.Step(Parked());
                b.Step(Parked());
                VehicleBody.ResolveCollision(a.Body, 1, b.Body, 2, out ia, out ib);
            }
            Assert.True(a.Body.IsSleeping);
            Assert.True(b.Body.IsSleeping);
            Assert.Equal(pa, a.Body.Position);
            Assert.Equal(pb, b.Body.Position);
        }

        [Fact]
        public void DynamicBox_AddedOverlappingASleeper_WakesIt()
        {
            CollisionWorld world = Flat();
            Sim s = Settled(world, new Vec3(0f, 0.77f, 0f));
            s.Step(Parked());
            Assert.True(s.Body.IsSleeping);
            world.AddBox(new Vec3(0.5f, 0.75f, 1f), new Vec3(0.5f, 0.5f, 0.5f), Quat.Identity, 9);
            s.Step(Parked());
            Assert.False(s.Body.IsSleeping);
        }

        [Fact]
        public void DynamicShapeFarAway_DoesNotWakeASleeper()
        {
            CollisionWorld world = Flat();
            Sim s = Settled(world, new Vec3(0f, 0.77f, 0f));
            world.AddBox(new Vec3(100f, 0.75f, 100f), new Vec3(0.5f, 0.5f, 0.5f), Quat.Identity, 9);
            world.AddCapsule(new Vec3(-80f, 0f, 30f), 0.3f, 1.8f, 10);
            s.Step(Parked());
            Assert.True(s.Body.IsSleeping);
        }

        [Fact]
        public void DynamicCapsule_OverlappingASleeper_WakesIt()
        {
            CollisionWorld world = Flat();
            Sim s = Settled(world, new Vec3(0f, 0.77f, 0f));
            world.AddCapsule(new Vec3(1.2f, 0f, 0f), 0.3f, 1.8f, 10);
            s.Step(Parked());
            Assert.False(s.Body.IsSleeping);
        }

        [Fact]
        public void StaticRegion_ReplacedRemovedOrCleared_WakesASleeper()
        {
            for (int mode = 0; mode < 3; mode++)
            {
                CollisionWorld world = Flat();
                Sim s = Settled(world, new Vec3(0f, 0.77f, 0f));
                if (mode == 0) new Terrain().Flat(2000f).Apply(world, 1);
                else if (mode == 1) world.RemoveRegion(1);
                else world.Clear();
                s.Step(Parked());
                Assert.False(s.Body.IsSleeping, "mode " + mode);
                if (mode == 0)
                {
                    for (int i = 0; i < 300 && !s.Body.IsSleeping; i++) s.Step(Parked());
                    Assert.True(s.Body.IsSleeping, "should settle again on the replaced ground");
                }
            }
        }

        [Fact]
        public void CandidateQueries_PerStep_AreAtMostOnePerSubstep()
        {
            var s = new Sim(V.Car(), Flat());
            s.World.AddBox(new Vec3(4f, 0.75f, 3f), new Vec3(0.9f, 0.75f, 2.2f), Quat.Identity, 9);
            s.World.AddCapsule(new Vec3(-3f, 0f, 2f), 0.3f, 1.8f, 10);
            for (int i = 0; i < 300; i++)
            {
                long before = s.World.CandidateQueryCount;
                s.Step(new VehicleControls { Throttle = 0.3f, Steer = 0.5f });
                long delta = s.World.CandidateQueryCount - before;
                Assert.True(delta >= 1 && delta <= s.Body.LastSubsteps, "step " + i + ": " + delta + " queries for " + s.Body.LastSubsteps + " substeps");
            }
        }

        [Fact]
        public void TenSleepers_AddNoQueries_ToADrivenCarsFrame()
        {
            CollisionWorld world = Flat();
            var sleepers = new List<Sim>();
            for (int i = 0; i < 10; i++) sleepers.Add(new Sim(V.Car(), world, new Vec3(-100f - i * 8f, 0.77f, 50f)));
            for (int n = 0; n < 300; n++) foreach (Sim c in sleepers) c.Step(Parked());
            foreach (Sim c in sleepers) Assert.True(c.Body.IsSleeping);
            var driven = new Sim(V.Car(), world, new Vec3(0f, 0.77f, 0f));
            for (int n = 0; n < 200; n++)
            {
                long before = world.CandidateQueryCount;
                foreach (Sim c in sleepers) c.Step(Parked());
                driven.Step(new VehicleControls { Throttle = 0.3f, Steer = 0.5f });
                long delta = world.CandidateQueryCount - before;
                Assert.True(delta <= driven.Body.LastSubsteps, "frame " + n + ": " + delta + " queries, driven substeps " + driven.Body.LastSubsteps);
            }
        }

        [Fact]
        public void TenSleepingPlusOneDriven_AllocatesNothingAfterWarmUp()
        {
            CollisionWorld world = Flat();
            world.AddBox(new Vec3(30f, 0.75f, 30f), new Vec3(0.9f, 0.75f, 2.2f), Quat.Identity, 9);
            var sleepers = new List<Sim>();
            for (int i = 0; i < 10; i++) sleepers.Add(new Sim(V.Car(), world, new Vec3(-100f - i * 8f, 0.77f, 50f)));
            var driven = new Sim(V.Car(), world, new Vec3(0f, 0.77f, 0f));
            VehicleImpact ia, ib;
            Action frame = () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    sleepers[i].Step(Parked());
                    VehicleBody.ResolveCollision(sleepers[i].Body, 100 + i, driven.Body, 1, out ia, out ib);
                }
                driven.Step(new VehicleControls { Throttle = 0.3f, Steer = 0.5f });
            };
            for (int i = 0; i < 300; i++) frame();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 600; i++) frame();
            Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
            foreach (Sim c in sleepers) Assert.True(c.Body.IsSleeping);
        }
    }
}
