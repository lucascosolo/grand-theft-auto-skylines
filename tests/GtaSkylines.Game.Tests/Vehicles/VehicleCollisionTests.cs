using System;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Vehicles;
using Xunit;

namespace GtaSkylines.Game.Tests.Vehicles
{
    public class VehicleCollisionTests
    {
        private const float WallZ = 150f;

        private static CollisionWorld WorldWithWall(float wallZ = WallZ)
        {
            var w = new Terrain().Flat(2000f).ToWorld(1);
            new Terrain().WallAcross(-100f, 100f, wallZ, 0f, 12f).Apply(w, 2);
            return w;
        }

        /// <summary>Drives at the target speed until the first step that reports an impact; returns that step's result.</summary>
        private static VehicleStepResult DriveIntoUntilImpact(Sim s, float speed, float steer = 0f)
        {
            for (int i = 0; i < 60 * 40; i++)
            {
                VehicleStepResult r = s.Step(s.Hold(speed, steer));
                if (r.ImpactCount > 0)
                {
                    return r;
                }
            }
            Assert.Fail("never hit anything; z=" + s.Body.Position.Z);
            return default(VehicleStepResult);
        }

        [Fact]
        public void HeadOnWallHit_At20_ReportsAbout20_CostsHealth_StopsAndStaysOutOfTheWall()
        {
            var s = new Sim(V.Car(), WorldWithWall());
            s.Body.Teleport(new Vec3(0f, 0.77f, 20f), Quat.Identity);
            VehicleStepResult r = DriveIntoUntilImpact(s, 20f);
            VehicleImpact hit = r.GetImpact(0);
            Assert.Equal(ImpactKind.Static, hit.Kind);
            Assert.Equal(-1, hit.OtherTag);
            V.Near(20f, hit.RelativeNormalSpeed, 3f);
            Assert.True(hit.Normal.Z < -0.9f, "normal points from the wall into the car: " + hit.Normal);
            V.Near(WallZ, hit.Point.Z, 0.15f);
            Assert.True(hit.Damage > 15f, "damage " + hit.Damage);
            V.Near(100f - hit.Damage, s.Body.Health, 1f);
            Assert.True(s.Body.Health < 85f);

            for (int i = 0; i < 60 * 3; i++)
            {
                s.Step(VehicleControls.None);
                Assert.True(V.MaxCornerZ(s.Body) <= WallZ + VehicleBody.PenetrationTolerance + 0.01f, "inside the wall by " + (V.MaxCornerZ(s.Body) - WallZ));
            }
            Assert.True(Math.Abs(s.Body.Speed) < 1.5f, "speed after crash " + s.Body.Speed);
            Assert.True(s.Body.Upright > 0.9f);
        }

        [Fact]
        public void Pushing_IntoAWallWithFullThrottle_NeverPenetratesPastTolerance()
        {
            var s = new Sim(V.Car(), WorldWithWall(60f));
            s.Body.Teleport(new Vec3(0f, 0.77f, 30f), Quat.Identity);
            for (int i = 0; i < 60 * 8; i++)
            {
                s.Step(new VehicleControls { Throttle = 1f });
                Assert.True(V.MaxCornerZ(s.Body) <= 60f + VehicleBody.PenetrationTolerance + 0.01f, "inside the wall at step " + i);
            }
        }

        [Fact]
        public void Impact_DamageFollowsQuadraticFormula_ScaledByTuning()
        {
            var s = new Sim(V.Car(), WorldWithWall());
            s.Body.Teleport(new Vec3(0f, 0.77f, 20f), Quat.Identity);
            VehicleImpact hit = DriveIntoUntilImpact(s, 14f).GetImpact(0);
            float expected = s.Tuning.DamageScale * (float)Math.Pow(hit.RelativeNormalSpeed - s.Tuning.DamageSpeedThreshold, 2);
            V.Near(expected, hit.Damage, Math.Max(0.5f, expected * 0.05f));
            V.Near(100f - hit.Damage, s.Body.Health, 0.5f);
        }

        [Fact]
        public void SlowImpact_BelowDamageThreshold_IsReportedButFree()
        {
            var s = new Sim(V.Car(), WorldWithWall(60f));
            s.Body.Teleport(new Vec3(0f, 0.77f, 40f), Quat.Identity);
            VehicleImpact hit = DriveIntoUntilImpact(s, 3.5f).GetImpact(0);
            Assert.True(hit.RelativeNormalSpeed >= s.Tuning.ImpactReportSpeed);
            Assert.True(hit.RelativeNormalSpeed < 4f);
            Assert.Equal(0f, hit.Damage, 1e-4);
            Assert.Equal(100f, s.Body.Health, 1e-3);
        }

        [Fact]
        public void Glancing45DegreeWallHit_KeepsMostOfTheTangentialSpeed()
        {
            var s = new Sim(V.Car(), WorldWithWall(), new Vec3(-60f, 0.77f, 20f), Quat.Yaw(45f));
            float tangentialBefore = 0f;
            VehicleStepResult r = default(VehicleStepResult);
            for (int i = 0; i < 60 * 40; i++)
            {
                tangentialBefore = s.Body.Velocity.X;
                r = s.Step(s.Hold(20f));
                if (r.ImpactCount > 0) break;
            }
            Assert.True(r.ImpactCount > 0, "never hit the wall");
            Assert.True(tangentialBefore > 10f, "tangential speed before: " + tangentialBefore);
            s.Run(new VehicleControls { Throttle = 0.3f }, 0.5f);
            Assert.True(s.Body.Velocity.X > 0.5f * tangentialBefore, "tangential " + tangentialBefore + " -> " + s.Body.Velocity.X);
            Assert.True(s.Body.Velocity.Z < 2f, "normal speed after " + s.Body.Velocity.Z);
            Assert.True(V.MaxCornerZ(s.Body) <= WallZ + VehicleBody.PenetrationTolerance + 0.01f);
        }

        [Fact]
        public void At50MetresPerSecond_NeverTunnelsThrough_A0p1mWall()
        {
            var s = new Sim(V.Car(45f), new Terrain().Flat(3000f).ToWorld());
            // M7 caps top speed at 50 m/s (VehicleTuning.MaxTopSpeed), so the car is driven up and then set to 50 m/s.
            s.DriveTo(45f, 60f);
            s.Body.SetVelocity(s.Body.Rotation * new Vec3(0f, 0f, 50f), Vec3.Zero);
            float wallZ = s.Body.Position.Z + 30f;
            new Terrain().Slab(-60f, 60f, wallZ, 0.1f, 0f, 6f).Apply(s.World, 2);
            float maxImpact = 0f;
            for (int i = 0; i < 60 * 3; i++)
            {
                VehicleStepResult r = s.Step(new VehicleControls { Throttle = 1f });
                if (r.ImpactCount > 0) maxImpact = Math.Max(maxImpact, r.GetImpact(0).RelativeNormalSpeed);
                Assert.True(V.Finite(s.Body.Position));
                Assert.True(s.Body.Position.Z < wallZ, "centre passed the wall at step " + i + " z=" + s.Body.Position.Z);
            }
            Assert.True(maxImpact > 30f, "impact " + maxImpact);
        }

        [Fact]
        public void BottomSliceOfTheBox_IgnoresLowObstacles()
        {
            // A 0.1 m kerb-high slab is below BodyClearance (0.2 m): body collision must not see it.
            var w = new Terrain().Flat(2000f).ToWorld(1);
            new Terrain().Slab(-30f, 30f, 60f, 0.5f, 0f, 0.1f).Apply(w, 2);
            var s = new Sim(V.Car(), w, new Vec3(0f, 0.77f, 30f), Quat.Identity);
            for (int i = 0; i < 60 * 8; i++)
            {
                VehicleStepResult r = s.Step(s.Hold(8f));
                if (r.ImpactCount > 0)
                {
                    Assert.True(r.GetImpact(0).RelativeNormalSpeed < 2f, "kerb produced a body impact of " + r.GetImpact(0).RelativeNormalSpeed);
                }
            }
            Assert.True(s.Body.Position.Z > 75f, "should have driven over the kerb, z=" + s.Body.Position.Z);
            Assert.True(s.Body.Health > 99f);
        }

        [Fact]
        public void PedestrianCapsule_At15_ReportsImpactWithItsTag_AndBarelySlowsTheCar()
        {
            var s = new Sim(V.Car(), new Terrain().Flat(2000f).ToWorld(), new Vec3(0f, 0.77f, 0f), Quat.Identity);
            const int pedTag = 77;
            VehicleStepResult hitStep = default(VehicleStepResult);
            float speedBefore = 0f;
            bool hit = false;
            for (int i = 0; i < 60 * 30 && !hit; i++)
            {
                s.World.ClearDynamic();
                s.World.AddCapsule(new Vec3(0f, 0f, 90f), 0.3f, 1.8f, pedTag);
                speedBefore = s.Body.Speed;
                hitStep = s.Step(s.Hold(15f));
                hit = hitStep.ImpactCount > 0;
            }
            Assert.True(hit, "never touched the pedestrian");
            VehicleImpact imp = hitStep.GetImpact(0);
            Assert.Equal(ImpactKind.Capsule, imp.Kind);
            Assert.Equal(pedTag, imp.OtherTag);
            V.Near(15f, imp.RelativeNormalSpeed, 2.5f);
            float ratio = 2f * s.Tuning.PedestrianMass / (s.Tuning.Mass + s.Tuning.PedestrianMass);
            float expected = s.Tuning.DamageScale * (float)Math.Pow(imp.RelativeNormalSpeed - s.Tuning.DamageSpeedThreshold, 2) * ratio;
            V.Near(expected, imp.Damage, Math.Max(0.3f, expected * 0.06f));
            Assert.True(s.Body.Speed > speedBefore - 2f, "speed " + speedBefore + " -> " + s.Body.Speed);
            s.World.ClearDynamic();
            s.Run(s.Hold(15f), 0.5f);
            Assert.True(s.Body.Speed > 11f, "car was stopped by a pedestrian: " + s.Body.Speed);
            Assert.True(s.Body.Health > 90f);
        }

        [Fact]
        public void DynamicBox_ActsAsAnImmovableObstacle_WithItsTag()
        {
            var s = new Sim(V.Car(), new Terrain().Flat(2000f).ToWorld(), new Vec3(0f, 0.77f, 0f), Quat.Identity);
            const int boxTag = 5;
            VehicleStepResult first = default(VehicleStepResult);
            bool hit = false;
            for (int i = 0; i < 60 * 30; i++)
            {
                s.World.ClearDynamic();
                s.World.AddBox(new Vec3(0f, 0.75f, 60f), new Vec3(0.9f, 0.75f, 2.2f), Quat.Identity, boxTag);
                VehicleStepResult r = s.Step(s.Hold(10f));
                if (!hit && r.ImpactCount > 0) { first = r; hit = true; }
                if (hit) { Assert.True(V.MaxCornerZ(s.Body) <= 57.8f + VehicleBody.PenetrationTolerance + 0.02f); }
                if (hit && i > 60 * 20) break;
            }
            Assert.True(hit);
            Assert.Equal(ImpactKind.Box, first.GetImpact(0).Kind);
            Assert.Equal(boxTag, first.GetImpact(0).OtherTag);
            Assert.True(Math.Abs(s.Body.Speed) < 2f);
        }

        [Fact]
        public void OwnTaggedBox_IsIgnoredByEveryQuery()
        {
            var s = new Sim(V.Car(), new Terrain().Flat(2000f).ToWorld());
            for (int i = 0; i < 60 * 4; i++)
            {
                s.World.ClearDynamic();
                s.World.AddBox(s.Body.Position, s.Tuning.HalfExtents, s.Body.Rotation, V.Tag);
                VehicleStepResult r = s.Step(new VehicleControls { Throttle = 1f });
                Assert.Equal(0, r.ImpactCount);
            }
            Assert.Equal(4, s.Body.GroundedWheelCount);
            Assert.True(s.Body.Speed > 5f);
        }

        [Fact]
        public void ImpactsAreOrderedByDescendingSpeed_AndAtMostOnePerOther()
        {
            var s = new Sim(V.Car(), new Terrain().Flat(2000f).ToWorld(), new Vec3(0f, 0.77f, 0f), Quat.Identity);
            VehicleStepResult r = default(VehicleStepResult);
            for (int i = 0; i < 60 * 30; i++)
            {
                s.World.ClearDynamic();
                s.World.AddBox(new Vec3(-0.5f, 0.75f, 60f), new Vec3(0.5f, 0.75f, 1f), Quat.Identity, 11);
                s.World.AddBox(new Vec3(0.5f, 0.75f, 60f), new Vec3(0.5f, 0.75f, 1f), Quat.Identity, 12);
                r = s.Step(s.Hold(12f));
                if (r.ImpactCount > 0) break;
            }
            Assert.InRange(r.ImpactCount, 1, VehicleStepResult.MaxImpacts);
            for (int i = 1; i < r.ImpactCount; i++)
            {
                Assert.True(r.GetImpact(i - 1).RelativeNormalSpeed >= r.GetImpact(i).RelativeNormalSpeed);
                Assert.NotEqual(r.GetImpact(i - 1).OtherTag, r.GetImpact(i).OtherTag);
            }
        }

        // ---- pairwise ----

        private static void StepPair(Sim a, Sim b, VehicleControls ca, VehicleControls cb)
        {
            a.Step(ca);
            b.Step(cb);
        }

        [Fact]
        public void TwoEqualCars_HeadOnAt10Each_EndWithSmallRoughlyOppositeVelocities_MomentumConserved()
        {
            var world = new Terrain().Flat(2000f).ToWorld();
            var a = new Sim(V.Car(), world, new Vec3(0f, 0.77f, -40f), Quat.Identity);
            var b = new Sim(V.Car(), world, new Vec3(0f, 0.77f, 40f), Quat.Yaw(180f));
            VehicleImpact ia = default(VehicleImpact), ib = default(VehicleImpact);
            Vec3 pa = Vec3.Zero, pb = Vec3.Zero;
            bool collided = false;
            for (int i = 0; i < 60 * 30 && !collided; i++)
            {
                StepPair(a, b, a.Hold(10f), b.Hold(10f));
                pa = a.Body.Velocity * a.Tuning.Mass;
                pb = b.Body.Velocity * b.Tuning.Mass;
                collided = VehicleBody.ResolveCollision(a.Body, 1, b.Body, 2, out ia, out ib);
            }
            Assert.True(collided, "boxes never overlapped");
            float before = (pa + pb).Length;
            Vec3 after = a.Body.Velocity * a.Tuning.Mass + b.Body.Velocity * b.Tuning.Mass;
            float scale = a.Tuning.Mass * 10f;
            Assert.True((after - (pa + pb)).Length < 0.1f * scale, "momentum changed by " + (after - (pa + pb)).Length + " (before " + before + ")");
            Assert.True(a.Body.Velocity.Z < 1f && b.Body.Velocity.Z > -1f, "a=" + a.Body.Velocity + " b=" + b.Body.Velocity);
            Assert.True(a.Body.Velocity.Length < 3f && b.Body.Velocity.Length < 3f);
            Assert.Equal(ImpactKind.Vehicle, ia.Kind);
            Assert.Equal(2, ia.OtherTag);
            Assert.Equal(1, ib.OtherTag);
            V.Near(20f, ia.RelativeNormalSpeed, 4f);
            V.Near(ia.RelativeNormalSpeed, ib.RelativeNormalSpeed, 0.5f);
            Assert.True(ia.Normal.Z < -0.8f && ib.Normal.Z > 0.8f, "normals point from the other into this body");
            Assert.True(ia.Damage > 0f && ib.Damage > 0f);
            V.Near(ia.Damage, ib.Damage, Math.Max(0.5f, ia.Damage * 0.05f));
            V.Near(100f - ia.Damage, a.Body.Health, 0.5f);
        }

        [Fact]
        public void ResolveCollision_SeparatesTheBoxes()
        {
            var world = new Terrain().Flat(2000f).ToWorld();
            var a = new Sim(V.Car(), world, new Vec3(0f, 0.77f, 0f), Quat.Identity);
            var b = new Sim(V.Car(), world, new Vec3(0.3f, 0.77f, 3.5f), Quat.Identity);
            VehicleImpact ia, ib;
            Assert.True(VehicleBody.ResolveCollision(a.Body, 1, b.Body, 2, out ia, out ib));
            Assert.False(VehicleBody.ResolveCollision(a.Body, 1, b.Body, 2, out ia, out ib), "no longer overlapping after resolution");
        }

        [Fact]
        public void ResolveCollision_FalseWhenApart_OrSameBody_AndImpactsAreDefault()
        {
            var world = new Terrain().Flat(2000f).ToWorld();
            var a = new Sim(V.Car(), world, new Vec3(0f, 0.77f, 0f), Quat.Identity);
            var b = new Sim(V.Car(), world, new Vec3(0f, 0.77f, 30f), Quat.Identity);
            VehicleImpact ia, ib;
            Assert.False(VehicleBody.ResolveCollision(a.Body, 1, b.Body, 2, out ia, out ib));
            Assert.Equal(0f, ia.RelativeNormalSpeed, 1e-6);
            Assert.Equal(0f, ib.Damage, 1e-6);
            Assert.False(VehicleBody.ResolveCollision(a.Body, 1, a.Body, 1, out ia, out ib));
            Assert.Throws<ArgumentNullException>(() => VehicleBody.ResolveCollision(null, 1, b.Body, 2, out ia, out ib));
            Assert.Throws<ArgumentNullException>(() => VehicleBody.ResolveCollision(a.Body, 1, null, 2, out ia, out ib));
        }

        [Fact]
        public void HeavyTruck_HittingAStandingCar_PushesItAndBarelySlows()
        {
            var world = new Terrain().Flat(2000f).ToWorld();
            var truck = new Sim(V.Truck(), world, new Vec3(0f, 1.72f, -60f), Quat.Identity);
            var car = new Sim(V.Car(), world, new Vec3(0f, 0.77f, 0f), Quat.Identity);
            VehicleImpact it = default(VehicleImpact), ic = default(VehicleImpact);
            bool collided = false;
            float truckBefore = 0f;
            for (int i = 0; i < 60 * 40 && !collided; i++)
            {
                truck.Step(truck.Hold(10f));
                car.Step(VehicleControls.None);
                truckBefore = truck.Body.Speed;
                collided = VehicleBody.ResolveCollision(truck.Body, 1, car.Body, 2, out it, out ic);
            }
            Assert.True(collided, "truck never reached the car");
            Assert.True(car.Body.Velocity.Z > 0.7f * truckBefore, "car pushed to " + car.Body.Velocity.Z + " from truck speed " + truckBefore);
            Assert.True(truck.Body.Velocity.Z > 0.7f * truckBefore, "truck kept " + truck.Body.Velocity.Z);
            Assert.True(car.Body.Velocity.Z >= truck.Body.Velocity.Z - 0.01f, "car is not left behind inside the truck");
            Assert.True(ic.Damage > it.Damage, "the car (lighter) is hurt more: " + ic.Damage + " vs " + it.Damage);
            Assert.Equal(1, ic.OtherTag);
            Assert.Equal(2, it.OtherTag);
        }
    }
}
