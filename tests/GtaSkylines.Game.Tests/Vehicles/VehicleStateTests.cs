using System;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Vehicles;
using Xunit;

namespace GtaSkylines.Game.Tests.Vehicles
{
    public class VehicleStateTests
    {
        private static Sim OnFlat(Vec3? pos = null, Quat? rot = null)
        {
            return new Sim(V.Car(), new Terrain().Flat(1500f).ToWorld(), pos, rot);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-0.016f)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        [InlineData(float.NegativeInfinity)]
        public void Step_WithUnusableDt_ChangesNothingAndReturnsDefaultResult(float dt)
        {
            Sim s = OnFlat();
            s.Run(new VehicleControls { Throttle = 1f }, 1f);
            Vec3 p = s.Body.Position, v = s.Body.Velocity;
            Quat q = s.Body.Rotation;
            VehicleStepResult r = s.Body.Step(dt, new VehicleControls { Throttle = 1f }, s.World, V.Tag);
            Assert.Equal(0, r.ImpactCount);
            Assert.False(r.Exploded || r.LeftGround || r.Landed);
            Assert.Equal(p, s.Body.Position);
            Assert.Equal(v, s.Body.Velocity);
            Assert.Equal(q.W, s.Body.Rotation.W, 1e-6);
        }

        [Fact]
        public void Step_LongDt_IsClampedToMaxStepSeconds()
        {
            var s = new Sim(V.Car(), new Terrain().Flat(300f, -1000f).ToWorld(), new Vec3(0f, 100f, 0f), Quat.Identity);
            s.Body.Step(5f, VehicleControls.None, s.World, V.Tag);
            float expectedDrop = 0.5f * 9.81f * VehicleBody.MaxStepSeconds * VehicleBody.MaxStepSeconds;
            V.Near(100f - expectedDrop, s.Body.Position.Y, 0.03f);
            V.Near(-9.81f * VehicleBody.MaxStepSeconds, s.Body.Velocity.Y, 0.05f);
        }

        [Fact]
        public void Step_NullWorld_Throws()
        {
            var body = new VehicleBody(VehicleTuning.FromModel(V.Car()));
            Assert.Throws<ArgumentNullException>(() => body.Step(V.Dt, VehicleControls.None, null, 1));
        }

        [Fact]
        public void NonFiniteControls_CountAsZero()
        {
            Sim nan = OnFlat(), none = OnFlat();
            var bad = new VehicleControls { Throttle = float.NaN, Brake = float.PositiveInfinity, Steer = float.NaN };
            for (int i = 0; i < 120; i++)
            {
                nan.Step(bad);
                none.Step(VehicleControls.None);
            }
            Assert.True(V.Finite(nan.Body.Position) && V.Finite(nan.Body.Velocity));
            Assert.True(Vec3.Distance(nan.Body.Position, none.Body.Position) < 0.01f);
            Assert.True(nan.Body.Velocity.Length < 0.05f);
            V.Near(0f, nan.Body.SmoothedSteer, 1e-4f);
        }

        [Fact]
        public void OutOfRangeControls_AreClamped()
        {
            Sim wild = OnFlat(), full = OnFlat();
            for (int i = 0; i < 120; i++)
            {
                wild.Step(new VehicleControls { Throttle = 7f, Steer = -9f });
                full.Step(new VehicleControls { Throttle = 1f, Steer = -1f });
            }
            Assert.True(Vec3.Distance(wild.Body.Position, full.Body.Position) < 0.05f);
            V.Near(-1f, wild.Body.SmoothedSteer, 1e-3f);
            var neg = OnFlat();
            neg.Run(new VehicleControls { Throttle = -3f, Brake = -3f }, 1f);
            Assert.True(neg.Body.Velocity.Length < 0.05f, "negative pedals count as 0");
        }

        [Fact]
        public void Teleport_PlacesBody_AndClearsMotion_KeepingHealth()
        {
            var w = new Terrain().Flat(2000f).ToWorld(1);
            new Terrain().WallAcross(-100f, 100f, 100f, 0f, 12f).Apply(w, 2);
            var s = new Sim(V.Car(), w, new Vec3(0f, 0.77f, 40f), Quat.Identity);
            while (s.Time < 30f)
            {
                if (s.Step(s.Hold(15f, 0.2f)).ImpactCount > 0) break;
            }
            Assert.True(s.Body.Health < 100f, "need a damaged body for this test");
            float health = s.Body.Health;
            Quat target = Quat.Yaw(70f);
            s.Body.Teleport(new Vec3(5f, 20f, -30f), target);
            Assert.Equal(new Vec3(5f, 20f, -30f), s.Body.Position);
            Assert.True(Math.Abs(Quat.Dot(s.Body.Rotation, target)) > 0.9999f);
            Assert.Equal(Vec3.Zero, s.Body.Velocity);
            Assert.Equal(Vec3.Zero, s.Body.AngularVelocity);
            Assert.Equal(0f, s.Body.Speed, 1e-5);
            Assert.Equal(0f, s.Body.SmoothedSteer, 1e-5);
            Assert.Equal(0f, s.Body.AirborneSeconds, 1e-5);
            Assert.Equal(0f, s.Body.UpsideDownSeconds, 1e-5);
            Assert.Equal(0, s.Body.GroundedWheelCount);
            Assert.Equal(health, s.Body.Health, 1e-4);
        }

        [Fact]
        public void Teleport_NormalisesRotation_AndRejectsGarbage()
        {
            var body = new VehicleBody(VehicleTuning.FromModel(V.Car()));
            body.Teleport(Vec3.Zero, new Quat(0f, 0.2f, 0f, 0.2f));
            Quat q = body.Rotation;
            V.Near(1f, (float)Math.Sqrt(q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W), 1e-4f);
            Assert.Throws<ArgumentException>(() => body.Teleport(new Vec3(float.NaN, 0f, 0f), Quat.Identity));
            Assert.Throws<ArgumentException>(() => body.Teleport(new Vec3(0f, float.PositiveInfinity, 0f), Quat.Identity));
            Assert.Throws<ArgumentException>(() => body.Teleport(Vec3.Zero, new Quat(0f, 0f, 0f, 0f)));
            Assert.Throws<ArgumentException>(() => body.Teleport(Vec3.Zero, new Quat(float.NaN, 0f, 0f, 1f)));
        }

        [Fact]
        public void GetWheel_OutOfRangeThrows()
        {
            var body = new VehicleBody(VehicleTuning.FromModel(V.Car()));
            Assert.Throws<ArgumentOutOfRangeException>(() => body.GetWheel(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => body.GetWheel(4));
        }

        private static Sim OnRoof()
        {
            Quat roof = Quat.Yaw(30f) * Quat.AngleAxis(180f, Vec3.Forward);
            return OnFlat(new Vec3(0f, 1.0f, 0f), roof);
        }

        [Fact]
        public void UpsideDown_AtRest_BecomesRecoverableAfterThreeSeconds()
        {
            Sim s = OnRoof();
            s.Run(VehicleControls.None, 2f);
            Assert.True(s.Body.Upright < 0f, "should be on its roof: " + s.Body.Upright);
            Assert.False(s.Body.CanRecover);
            Assert.True(s.Body.UpsideDownSeconds < 2f);
            s.Run(VehicleControls.None, 3f);
            Assert.True(s.Body.CanRecover, "upside down seconds " + s.Body.UpsideDownSeconds);
            Assert.True(s.Body.UpsideDownSeconds >= 3f);
        }

        [Fact]
        public void UpsideDownCar_ExertsNoWheelForce_ThrottleDoesNotDriveIt()
        {
            Sim s = OnRoof();
            s.Run(VehicleControls.None, 2f);
            Vec3 p = s.Body.Position;
            s.Run(new VehicleControls { Throttle = 1f }, 2f);
            Assert.True(Vec3.Distance(p, s.Body.Position) < 1f);
        }

        [Fact]
        public void Recover_RightsTheCarInPlace_KeepingHeading_AndItDropsOntoItsWheels()
        {
            Sim s = OnRoof();
            s.Run(VehicleControls.None, 4f);
            Vec3 before = s.Body.Position;
            float yawBefore = s.Body.Rotation.YawDeg;
            s.Body.Recover();
            Assert.True(s.Body.Upright > 0.999f);
            V.Near(yawBefore, s.Body.Rotation.YawDeg, 1f);
            Assert.True(Vec3.DistanceXZ(before, s.Body.Position) < 0.01f);
            V.Near(before.Y + s.Tuning.HalfExtents.Y, s.Body.Position.Y, 0.01f);
            Assert.Equal(Vec3.Zero, s.Body.Velocity);
            Assert.Equal(Vec3.Zero, s.Body.AngularVelocity);
            Assert.Equal(0f, s.Body.UpsideDownSeconds, 1e-5);
            Assert.False(s.Body.CanRecover);
            s.Run(VehicleControls.None, 3f);
            Assert.Equal(4, s.Body.GroundedWheelCount);
            V.Near(s.Tuning.HalfExtents.Y, s.Body.Position.Y, 0.08f);
            Assert.True(s.Body.Upright > 0.99f);
        }

        [Fact]
        public void Recover_WorksOnASidewaysCarToo()
        {
            Quat side = Quat.AngleAxis(90f, Vec3.Forward);
            Sim s = OnFlat(new Vec3(0f, 2f, 0f), side);
            s.Body.Recover();
            Assert.True(s.Body.Upright > 0.999f);
        }

        [Fact]
        public void InAir_WithoutGround_AirborneSecondsCountsFromZeroAfterTeleport()
        {
            var s = new Sim(V.Car(), new Terrain().Flat(300f, -1000f).ToWorld(), new Vec3(0f, 0f, 0f), Quat.Identity);
            s.Run(VehicleControls.None, 0.3f);
            Assert.True(s.Body.AirborneSeconds > 0.25f);
            s.Body.Teleport(Vec3.Zero, Quat.Identity);
            Assert.Equal(0f, s.Body.AirborneSeconds, 1e-5);
        }
    }
}
