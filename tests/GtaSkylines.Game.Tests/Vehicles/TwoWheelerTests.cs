using System;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Vehicles;
using Xunit;

namespace GtaSkylines.Game.Tests.Vehicles
{
    public class TwoWheelerTests
    {
        private static VehicleModel Bike()
        {
            return new VehicleModel { Id = 200, Name = "Test bike", Kind = VehicleKind.Car, HalfExtents = new Vec3(0.35f, 0.6f, 0.9f), TwoWheeler = true };
        }

        private static Sim BikeOnGround(CollisionWorld world = null)
        {
            return new Sim(Bike(), world ?? new Terrain().Flat(2000f).ToWorld(1));
        }

        private static VehicleControls SpeedHold(Sim s, float target, float steer)
        {
            return new VehicleControls { Steer = steer, Throttle = s.Body.Speed < target ? 1f : 0f };
        }

        private static void Circle(Sim s, float steer, int steps, out float minUpright, out float meanLastLean)
        {
            s.Body.SetVelocity(new Vec3(0f, 0f, 8f), Vec3.Zero);
            minUpright = 1f;
            double sum = 0;
            int n = 0;
            for (int i = 0; i < steps; i++)
            {
                s.Step(SpeedHold(s, 8f, steer));
                minUpright = Math.Min(minUpright, s.Body.Upright);
                Assert.False(s.Body.Fallen, "fell at step " + i);
                if (i >= steps - 300)
                {
                    sum += s.Body.LeanDeg;
                    n++;
                }
            }
            meanLastLean = (float)(sum / n);
        }

        [Fact]
        public void RidingARightCircle_StaysUpright_AndLeansRight()
        {
            Sim s = BikeOnGround();
            float minUp, lean;
            Circle(s, 0.6f, 600, out minUp, out lean);
            Assert.True(minUp >= 0.6f, "min upright " + minUp);
            Assert.True(lean > 5f, "mean lean " + lean);
        }

        [Fact]
        public void RidingALeftCircle_StaysUpright_AndLeansLeft()
        {
            Sim s = BikeOnGround();
            float minUp, lean;
            Circle(s, -0.6f, 600, out minUp, out lean);
            Assert.True(minUp >= 0.6f, "min upright " + minUp);
            Assert.True(lean < -5f, "mean lean " + lean);
        }

        [Fact]
        public void AtRestWithNoInput_StaysUprightOnItsTwoWheels()
        {
            Sim s = BikeOnGround();
            for (int i = 0; i < 300; i++)
            {
                s.Step(VehicleControls.None);
                if (i >= 15)
                {
                    Assert.True(s.Body.Upright >= 0.99f, "upright " + s.Body.Upright + " at step " + i);
                }
                Assert.False(s.Body.Fallen);
            }
        }

        private static float MeanAbsLean(float speed)
        {
            Sim s = BikeOnGround();
            s.Body.SetVelocity(new Vec3(0f, 0f, speed), Vec3.Zero);
            double sum = 0;
            for (int i = 0; i < 360; i++)
            {
                s.Step(SpeedHold(s, speed, 0.3f));
                if (i >= 240)
                {
                    sum += Math.Abs(s.Body.LeanDeg);
                }
            }
            return (float)(sum / 120.0);
        }

        [Fact]
        public void LeanGrowsWithSpeed_ForTheSameSteer()
        {
            float slow = MeanAbsLean(4f);
            float fast = MeanAbsLean(7f);
            Assert.True(fast >= slow + 3f, "lean at 7 m/s " + fast + " vs at 4 m/s " + slow);
        }

        private static Sim BikeAtWall(float speed)
        {
            var w = new Terrain().Flat(2000f).ToWorld(1);
            new Terrain().WallAcross(-50f, 50f, 6f, 0f, 10f).Apply(w, 2);
            Sim s = BikeOnGround(w);
            s.Body.SetVelocity(new Vec3(0f, 0f, speed), Vec3.Zero);
            return s;
        }

        [Fact]
        public void HittingAWallAt12_Falls_ReportsCrashSpeed_AndEndsLyingOnItsSide()
        {
            Sim s = BikeAtWall(12f);
            bool fell = false;
            for (int i = 0; i < 360; i++)
            {
                s.Step(new VehicleControls { Throttle = 1f });
                if (i < 120 && s.Body.Fallen)
                {
                    fell = true;
                }
            }
            Assert.True(fell, "never fell within 120 steps");
            Assert.True(s.Body.Fallen);
            Assert.True(s.Body.CrashSpeed >= 9f, "crash speed " + s.Body.CrashSpeed);
            Assert.True(s.Body.Upright < 0.5f, "upright at end " + s.Body.Upright);
        }

        [Fact]
        public void HittingAWallAt3_DoesNotFall()
        {
            // No throttle: the bike must meet the wall at about 3 m/s, not faster.
            Sim s = BikeAtWall(3f);
            for (int i = 0; i < 360; i++)
            {
                s.Step(VehicleControls.None);
                Assert.False(s.Body.Fallen, "fell at step " + i);
            }
            Assert.True(s.Body.Upright >= 0.9f, "upright " + s.Body.Upright);
        }

        [Fact]
        public void Tuning_TwoWheeler_IsSingleTrack_WithMotorcycleMass()
        {
            VehicleTuning t = VehicleTuning.FromModel(Bike());
            Assert.True(t.SingleTrack);
            Assert.Equal(0f, t.TrackHalfWidth);
            Assert.Equal(0f, t.AntiRollStiffness);
            Assert.InRange(t.Mass, 150f, 400f);
        }

        [Fact]
        public void Tuning_FourWheeler_IsNotSingleTrack_AndKeepsItsTrackAndMass()
        {
            VehicleModel m = V.Car();
            VehicleTuning t = VehicleTuning.FromModel(m);
            Assert.False(t.SingleTrack);
            V.Near(0.85f * m.HalfExtents.X, t.TrackHalfWidth, 1e-4f);
            V.Near(1400f, t.Mass, 70f);
        }

        private static void DriveCar(Sim s)
        {
            s.Body.SetVelocity(new Vec3(0f, 0f, 8f), Vec3.Zero);
            for (int i = 0; i < 600; i++)
            {
                s.Step(SpeedHold(s, 8f, 0.6f));
            }
        }

        [Fact]
        public void FourWheelCar_IgnoresTheSingleTrackFields()
        {
            var a = new Sim(V.Car(), new Terrain().Flat(2000f).ToWorld(1));
            var b = new Sim(V.Car(), new Terrain().Flat(2000f).ToWorld(1));
            b.Tuning.MaxLeanDeg = 10f;
            b.Tuning.LeanMinSpeed = 0.1f;
            b.Tuning.LeanStiffness *= 3f;
            b.Tuning.LeanDamping *= 3f;
            b.Tuning.CrashImpactSpeed = 1f;
            b.Tuning.CrashLeanDeg = 5f;
            DriveCar(a);
            DriveCar(b);
            Assert.False(a.Body.Fallen);
            Assert.False(b.Body.Fallen);
            Assert.Equal(a.Body.Position, b.Body.Position);
            Assert.Equal(a.Body.Rotation, b.Body.Rotation);
        }
    }
}
