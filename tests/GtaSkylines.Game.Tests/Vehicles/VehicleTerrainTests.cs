using System;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Vehicles;
using Xunit;

namespace GtaSkylines.Game.Tests.Vehicles
{
    public class VehicleTerrainTests
    {
        [Fact]
        public void Climbs15DegreeRamp_AndRestsOnTheTop()
        {
            float topY;
            var t = new Terrain().Flat(300f).Ramp(-20f, 20f, 10f, 30f, 15f, 0f, out topY);
            t.Floor(-20f, 40f, 20f, 200f, topY);
            var s = new Sim(V.Car(), t.ToWorld());
            for (int i = 0; i < 60 * 14; i++)
            {
                s.Step(new VehicleControls { Throttle = 0.7f });
                Assert.True(V.Finite(s.Body.Position));
            }
            Assert.True(s.Body.Position.Z > 45f, "z " + s.Body.Position.Z);
            V.Near(topY + s.Tuning.HalfExtents.Y, s.Body.Position.Y, 0.25f);
            Assert.True(s.Body.Upright > 0.95f);
            Assert.True(s.Body.Health > 90f, "ramp seam must not hurt: " + s.Body.Health);
        }

        [Fact]
        public void HalfwayUpTheRamp_CarIsPitchedWithTheSlope()
        {
            float topY;
            var t = new Terrain().Flat(300f).Ramp(-20f, 20f, 10f, 60f, 15f, 0f, out topY);
            var s = new Sim(V.Car(), t.ToWorld());
            while (s.Body.Position.Z < 40f && s.Time < 20f)
            {
                s.Step(new VehicleControls { Throttle = 1f });
            }
            Assert.True(s.Body.Position.Z >= 40f, "never got up the ramp");
            float pitchDeg = (float)(Math.Asin(Math.Max(-1f, Math.Min(1f, s.Body.Rotation.Forward.Y))) * 180.0 / Math.PI);
            V.Near(15f, pitchDeg, 4f);
        }

        [Fact]
        public void RestsOnTenDegreeSlope_WithNoInputAndWithHandbrake()
        {
            float topY;
            float angle = 10f;
            float rad = angle * (float)Math.PI / 180f;
            var t = new Terrain().Ramp(-50f, 50f, -100f, 200f, angle, 0f, out topY);
            var forward = new Vec3(0f, (float)Math.Sin(rad), (float)Math.Cos(rad));
            var normal = new Vec3(0f, (float)Math.Cos(rad), -(float)Math.Sin(rad));
            Quat rot = Quat.LookRotation(forward, normal);
            float groundYAtZ0 = 100f * (float)Math.Tan(rad);
            Vec3 start = new Vec3(0f, groundYAtZ0, 0f) + normal * 0.77f;
            foreach (bool handbrake in new[] { false, true })
            {
                var s = new Sim(V.Car(), t.ToWorld(), start, rot);
                s.Run(new VehicleControls { Handbrake = handbrake }, 1f);
                Vec3 settled = s.Body.Position;
                s.Run(new VehicleControls { Handbrake = handbrake }, 3f);
                Assert.True(Vec3.Distance(settled, s.Body.Position) < 0.3f, "crept " + Vec3.Distance(settled, s.Body.Position) + " m (handbrake=" + handbrake + ")");
                Assert.True(s.Body.Velocity.Length < 0.1f);
                Assert.Equal(4, s.Body.GroundedWheelCount);
            }
        }

        [Fact]
        public void DrivingOffA2mLedge_LandsUprightWithLandedFlagAfterLeftGround()
        {
            var t = new Terrain().Flat(300f, 0f).Floor(-30f, -100f, 30f, 20f, 2f);
            t.WallAcross(-30f, 30f, 20f, 0f, 2f);
            var s = new Sim(V.Car(), t.ToWorld(), new Vec3(0f, 2f + 0.77f, 0f), Quat.Identity);
            int leftAt = -1, landedAt = -1;
            for (int i = 0; i < 60 * 12; i++)
            {
                VehicleStepResult r = s.Step(s.Hold(14f));
                if (r.LeftGround && leftAt < 0) leftAt = i;
                if (r.Landed && landedAt < 0) landedAt = i;
            }
            Assert.True(leftAt >= 0, "LeftGround never set");
            Assert.True(landedAt > leftAt, "Landed (" + landedAt + ") must follow LeftGround (" + leftAt + ")");
            Assert.True(s.Body.Upright > 0.95f, "upright " + s.Body.Upright);
            Assert.Equal(4, s.Body.GroundedWheelCount);
            V.Near(s.Tuning.HalfExtents.Y, s.Body.Position.Y, 0.15f);
            Assert.True(s.Body.Position.Z > 20f);
        }

        [Fact]
        public void KerbHop_ShorterThanAQuarterSecond_DoesNotReportLanded()
        {
            // A 0.05 m step keeps the wheels airborne for a few frames at most.
            var t = new Terrain().Flat(300f, 0f).Floor(-30f, 15f, 30f, 200f, -0.05f);
            var s = new Sim(V.Car(), t.ToWorld());
            for (int i = 0; i < 60 * 8; i++)
            {
                VehicleStepResult r = s.Step(s.Hold(12f));
                Assert.False(r.Landed, "Landed on step " + i);
            }
        }

        [Fact]
        public void Airborne_TracksAirborneSecondsAndFallsUnderGravity()
        {
            var s = new Sim(V.Car(), new Terrain().Flat(300f, -1000f).ToWorld(), new Vec3(0f, 0f, 0f), Quat.Identity);
            s.Run(VehicleControls.None, 0.5f);
            Assert.Equal(0, s.Body.GroundedWheelCount);
            V.Near(0.5f, s.Body.AirborneSeconds, 0.03f);
            V.Near(-9.81f * 0.5f, s.Body.Velocity.Y, 0.3f);
            V.Near(-0.5f * 9.81f * 0.25f, s.Body.Position.Y, 0.15f);
        }

        [Fact]
        public void AirControl_SteerYawsALittle_ThrottlePitchesNoseDown_BrakeNoseUp()
        {
            var world = new Terrain().Flat(300f, -1000f).ToWorld();
            var yaw = new Sim(V.Car(), world, new Vec3(0f, 0f, 0f), Quat.Identity);
            yaw.Run(new VehicleControls { Steer = 1f }, 0.5f);
            Assert.True(yaw.Body.AngularVelocity.Y > 0.2f && yaw.Body.AngularVelocity.Y < 2f, "yaw rate " + yaw.Body.AngularVelocity.Y);

            var down = new Sim(V.Car(), world, new Vec3(0f, 0f, 0f), Quat.Identity);
            down.Run(new VehicleControls { Throttle = 1f }, 0.5f);
            Assert.True(down.Body.Rotation.Forward.Y < -0.03f, "forward.y " + down.Body.Rotation.Forward.Y);

            var up = new Sim(V.Car(), world, new Vec3(0f, 0f, 0f), Quat.Identity);
            up.Run(new VehicleControls { Brake = 1f }, 0.5f);
            Assert.True(up.Body.Rotation.Forward.Y > 0.03f, "forward.y " + up.Body.Rotation.Forward.Y);
        }

        [Fact]
        public void TenThousandStepsOnBumpyTerrain_StayFiniteAndBounded()
        {
            var t = new Terrain().Flat(400f, -2f).Bumpy(120f);
            var s = new Sim(V.Car(), t.ToWorld(), new Vec3(0f, 1f, 0f), Quat.Identity);
            float maxY = 0f;
            for (int i = 0; i < 10000; i++)
            {
                s.Step(s.Hold(10f, 0.3f));
                Assert.True(V.Finite(s.Body.Position) && V.Finite(s.Body.Velocity) && V.Finite(s.Body.AngularVelocity) && V.Finite(s.Body.Rotation), "non-finite state at step " + i);
                maxY = Math.Max(maxY, s.Body.Position.Y);
            }
            Assert.True(maxY < 6f, "launched to " + maxY);
            Assert.True(s.Body.Velocity.Length < 30f);
            Quat q = s.Body.Rotation;
            V.Near(1f, (float)Math.Sqrt(q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W), 1e-3f);
        }
    }
}
