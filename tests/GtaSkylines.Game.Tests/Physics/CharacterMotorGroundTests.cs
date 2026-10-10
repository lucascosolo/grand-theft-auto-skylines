using System;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Tests.Fakes;
using Xunit;

namespace GtaSkylines.Game.Tests.PhysicsTests
{
    public class CharacterMotorGroundTests
    {
        private static Sim OnFloor(float y = 0f, FakeCity city = null)
        {
            return new Sim(T.BigFloor().ToWorld(), new Vec3(0, y, 0), city).Settle();
        }

        // ---------------- construction and basic API ----------------

        [Fact]
        public void NewMotorIsAtTheOriginAirborneAtRestWithFullBreath()
        {
            var m = new CharacterMotor(null);
            Assert.Equal(Vec3.Zero, m.Position);
            Assert.Equal(Vec3.Zero, m.Velocity);
            Assert.Equal(MotorState.Airborne, m.State);
            Assert.False(m.OnGround);
            Assert.Equal(Vec3.Up, m.GroundNormal);
            Assert.Equal(SurfaceFlags.None, m.GroundFlags);
            Assert.Equal(0f, m.ClimbProgress);
            Assert.Equal(20f, m.Breath);
            Assert.False(m.IsDrowning);
            Assert.Equal(QueryMask.All, m.Mask);
            Assert.Equal(0.35f, m.Settings.Radius);
            Assert.Equal(1.8f, m.Settings.Height);
        }

        [Fact]
        public void GivenSettingsInstanceIsKept()
        {
            var s = new MotorSettings { Radius = 0.5f };
            Assert.Same(s, new CharacterMotor(s).Settings);
        }

        [Fact]
        public void StepRejectsNullWorld()
        {
            var m = new CharacterMotor(null);
            Assert.Throws<ArgumentNullException>(() => m.Step(T.Dt, new MotorInput(), null, null));
        }

        [Fact]
        public void NonPositiveOrNonFiniteDtChangesNothing()
        {
            var w = T.BigFloor().ToWorld();
            var m = new CharacterMotor(null);
            m.Teleport(new Vec3(0, 5, 0));
            foreach (float dt in new[] { 0f, -0.016f, float.NaN, float.PositiveInfinity })
            {
                MotorStepResult r = m.Step(dt, new MotorInput { DesiredVelocity = new Vec3(3, 0, 0) }, w, null);
                Assert.Equal(default(MotorStepResult), r);
                Assert.Equal(new Vec3(0, 5, 0), m.Position);
                Assert.Equal(Vec3.Zero, m.Velocity);
                Assert.Equal(MotorState.Airborne, m.State);
            }
        }

        // ---------------- walking ----------------

        [Fact]
        public void WalkingReachesTheDesiredSpeedAndStaysAtFloorHeight()
        {
            var s = OnFloor();
            Assert.Equal(MotorState.Grounded, s.Motor.State);
            Assert.True(s.Motor.OnGround);
            T.Near(Vec3.Up, s.Motor.GroundNormal, 1e-3f);
            s.Run(60, new Vec3(3, 0, 0));
            T.Near(3f, s.Motor.Velocity.X, 0.05f);
            T.Near(0f, s.Motor.Velocity.Y, 0.05f);
            Assert.InRange(s.Motor.Position.X, 2.7f, 3.0f);
            T.Near(0f, s.Motor.Position.Z, 0.02f);
            Assert.InRange(s.MinY, -0.02f, 0.02f);
            Assert.InRange(s.MaxY, -0.02f, 0.02f);
            Assert.Equal(0, s.NotGroundedSteps);
            Assert.False(s.AnyLanded);
            Assert.False(s.AnyJumped);
        }

        [Fact]
        public void WalkingStopsWhenInputReleased()
        {
            var s = OnFloor();
            s.Run(30, new Vec3(0, 0, 4));
            s.Run(30, Vec3.Zero);
            Assert.True(s.Motor.Velocity.LengthXZ < 0.05f, "speed " + s.Motor.Velocity.LengthXZ);
            float z = s.Motor.Position.Z;
            s.Run(30, Vec3.Zero);
            T.Near(z, s.Motor.Position.Z, 0.01f);
        }

        [Fact]
        public void VerticalComponentOfDesiredVelocityIsIgnored()
        {
            var s = OnFloor();
            s.Run(60, new Vec3(3, 50, 0));
            Assert.InRange(s.MaxY, -0.02f, 0.02f);
            Assert.False(s.AnyJumped);
        }

        [Fact]
        public void GroundFlagsAndNormalComeFromTheSurfaceUnderTheFeet()
        {
            var w = new MeshBuilder().Floor(-20, -20, 20, 20, 0, SurfaceFlags.Road | SurfaceFlags.Bridge).ToWorld();
            var s = new Sim(w, Vec3.Zero).Settle();
            Assert.Equal(SurfaceFlags.Road | SurfaceFlags.Bridge, s.Motor.GroundFlags);
            T.Near(Vec3.Up, s.Motor.GroundNormal, 1e-3f);
        }

        [Fact]
        public void FallingOntoTheFloorLandsOnceWithTheFallSpeed()
        {
            var s = new Sim(T.BigFloor().ToWorld(), new Vec3(0, 2, 0));
            s.Run(120, Vec3.Zero);
            Assert.True(s.AnyLanded);
            Assert.Equal(MotorState.Grounded, s.Motor.State);
            float expected = (float)Math.Sqrt(2 * 9.81 * 2.0);
            T.Near(expected, s.FirstImpact, expected * 0.1f);
            T.Near(0f, s.Motor.Position.Y, 0.02f);
        }

        [Fact]
        public void TeleportResetsVelocityStateClimbAndBreath()
        {
            var s = OnFloor();
            s.Run(30, new Vec3(0, 0, 4));
            s.Motor.Velocity = new Vec3(1, 2, 3);
            s.Motor.Teleport(new Vec3(10, 7, -4));
            Assert.Equal(new Vec3(10, 7, -4), s.Motor.Position);
            Assert.Equal(Vec3.Zero, s.Motor.Velocity);
            Assert.Equal(MotorState.Airborne, s.Motor.State);
            Assert.Equal(0f, s.Motor.ClimbProgress);
            Assert.Equal(20f, s.Motor.Breath);
            Assert.False(s.Motor.IsDrowning);
        }

        [Fact]
        public void TeleportOntoTheFloorSettlesWithNegligibleImpact()
        {
            var s = OnFloor();
            s.Motor.Teleport(new Vec3(5, 0, 5));
            s.Run(5, Vec3.Zero);
            Assert.Equal(MotorState.Grounded, s.Motor.State);
            T.Near(0f, s.Motor.Position.Y, 0.02f);
            if (s.AnyLanded) Assert.True(s.FirstImpact < 1f, "impact " + s.FirstImpact);
        }

        [Fact]
        public void SettingAnUpwardVelocityOnTheGroundMakesTheMotorAirborne()
        {
            var s = OnFloor();
            s.Motor.Velocity = new Vec3(0, 4, 0);
            s.Step(Vec3.Zero);
            Assert.Equal(MotorState.Airborne, s.Motor.State);
            Assert.True(s.Motor.Position.Y > 0.02f);
        }

        // ---------------- walls and corners ----------------

        [Fact]
        public void WalkingIntoAWallStopsAtTheCapsuleRadius()
        {
            var w = T.BigFloor().Wall(5, -30, 5, 30, 0, 10).ToWorld();
            var s = new Sim(w, new Vec3(0, 0, 0)).Settle();
            s.Run(180, new Vec3(3, 0, 0));
            Assert.InRange(s.Motor.Position.X, 4.60f, 4.66f);
            Assert.True(s.Motor.Velocity.LengthXZ < 0.1f);
            Assert.False(s.Overlapping());
        }

        [Fact]
        public void SlidesAlongAWallAtFortyFiveDegrees()
        {
            // Wall along x = z. Walking +z from the x > z side deflects into the wall's tangent (1, 0, 1).
            var w = T.BigFloor().Wall(-50, -50, 50, 50, 0, 10).ToWorld();
            var s = new Sim(w, new Vec3(2, 0, -1)).Settle();
            s.Run(180, new Vec3(0, 0, 4));
            Vec3 p = s.Motor.Position;
            float distanceToWall = (p.X - p.Z) / (float)Math.Sqrt(2);
            Assert.InRange(distanceToWall, 0.33f, 0.45f);
            Assert.True(p.Z > 5f, "advanced along the wall: " + p);
            Assert.True(p.X > 5f, "advanced along the wall: " + p);
            float alongSpeed = s.Motor.Velocity.LengthXZ;
            Assert.InRange(alongSpeed, 2.3f, 3.0f);
            Assert.False(s.Overlapping());
        }

        [Fact]
        public void StopsInAConcaveCornerWithoutPenetrating()
        {
            var w = T.BigFloor()
                .Box(new Vec3(5, 0, -50), new Vec3(5.5f, 10, 50))
                .Box(new Vec3(-50, 0, 5), new Vec3(50, 10, 5.5f))
                .ToWorld();
            var s = new Sim(w, Vec3.Zero).Settle();
            for (int i = 0; i < 240; i++)
            {
                s.Step(new Vec3(3, 0, 3));
                Assert.False(s.Overlapping(), "overlapping at step " + i);
            }
            Assert.InRange(s.Motor.Position.X, 4.5f, 4.66f);
            Assert.InRange(s.Motor.Position.Z, 4.5f, 4.66f);
            Assert.True(s.Motor.Velocity.LengthXZ < 0.1f);
        }

        // ---------------- steps ----------------

        [Theory]
        [InlineData(0.4f, true)]
        [InlineData(0.5f, false)]
        public void StepUpIsAllowedAtFortyCentimetresAndRefusedAtFifty(float height, bool allowed)
        {
            var w = T.BigFloor().Box(new Vec3(-5, 0, 2), new Vec3(5, height, 10)).ToWorld();
            var s = new Sim(w, Vec3.Zero).Settle();
            s.Run(150, new Vec3(0, 0, 3));
            if (allowed)
            {
                T.Near(height, s.Motor.Position.Y, 0.02f);
                Assert.True(s.Motor.Position.Z > 4f, "z " + s.Motor.Position.Z);
                Assert.Equal(MotorState.Grounded, s.Motor.State);
            }
            else
            {
                T.Near(0f, s.Motor.Position.Y, 0.02f);
                Assert.InRange(s.Motor.Position.Z, 1.60f, 1.66f);
            }
        }

        // ---------------- slopes ----------------

        [Fact]
        public void WalksUpAFortyDegreeSlopeStayingGrounded()
        {
            var w = T.BigFloor().Ramp(-20, 20, 0, 40, 40f).ToWorld();
            var s = new Sim(w, new Vec3(0, 0, -5)).Settle();
            s.Run(240, new Vec3(0, 0, 3));
            float z = s.Motor.Position.Z;
            Assert.True(z > 4f, "z " + z);
            T.Near(z * (float)Math.Tan(40 * Math.PI / 180), s.Motor.Position.Y, 0.15f);
            Assert.Equal(0, s.NotGroundedSteps);
            Assert.Equal(MotorState.Grounded, s.Motor.State);
            Assert.True(s.Motor.GroundNormal.Y > 0.7f && s.Motor.GroundNormal.Y < 0.8f, "normal " + s.Motor.GroundNormal);
        }

        [Fact]
        public void WalksDownAFortyDegreeSlopeWithoutBouncing()
        {
            float tan = (float)Math.Tan(40 * Math.PI / 180);
            float top = 20f * tan;
            var m = new MeshBuilder()
                .Floor(-20, -60, 20, 0, top)
                .Quad(new Vec3(-20, top, 0), new Vec3(20, top, 0), new Vec3(20, 0, 20), new Vec3(-20, 0, 20))
                .Floor(-20, 20, 20, 60, 0);
            var s = new Sim(m.ToWorld(), new Vec3(0, top, -5)).Settle();
            s.Run(240, new Vec3(0, 0, 3));
            float z = s.Motor.Position.Z;
            Assert.True(z > 4f, "z " + z);
            T.Near(top - z * tan, s.Motor.Position.Y, 0.15f);
            Assert.Equal(0, s.NotGroundedSteps);
            Assert.False(s.AnyLanded);
        }

        [Fact]
        public void SlidesDownASixtyDegreeSlope()
        {
            var w = T.BigFloor().Ramp(-20, 20, 0, 40, 60f).ToWorld();
            float tan = (float)Math.Tan(60 * Math.PI / 180);
            var s = new Sim(w, new Vec3(0, 10 * tan + 0.02f, 10));
            s.Run(60, Vec3.Zero);
            Assert.Equal(MotorState.Sliding, s.Motor.State);
            Assert.True(s.Motor.OnGround);
            Assert.True(s.Motor.Position.Z < 9.3f, "z " + s.Motor.Position.Z);
            Assert.True(s.Motor.Velocity.Z < -0.5f, "velocity " + s.Motor.Velocity);
            // A capsule resting on a 60 degree slope has its feet r * (1 / cos 60 - 1) = 0.35 m above the surface (plus skin).
            T.Near(s.Motor.Position.Z * tan, s.Motor.Position.Y, 0.45f);
            Assert.True(s.Motor.GroundNormal.Y < 0.55f, "normal " + s.Motor.GroundNormal);
        }

        [Fact]
        public void CannotWalkUpASixtyDegreeSlope()
        {
            var w = T.BigFloor().Ramp(-20, 20, 0, 40, 60f).ToWorld();
            var s = new Sim(w, new Vec3(0, 0, -5)).Settle();
            s.Run(240, new Vec3(0, 0, 3));
            Assert.True(s.Motor.Position.Y < 0.5f, "climbed to " + s.Motor.Position);
            Assert.True(s.Motor.Position.Z < 0.2f, "z " + s.Motor.Position.Z);
        }

        // ---------------- jumping and falling ----------------

        [Fact]
        public void JumpApexIsAboutOnePointOneMetres()
        {
            var s = OnFloor();
            MotorStepResult r = s.Step(Vec3.Zero, 0f, true);
            Assert.True(r.Jumped);
            Assert.Equal(MotorState.Airborne, s.Motor.State);
            s.Run(100, Vec3.Zero);
            Assert.InRange(s.MaxY, 1.0f, 1.2f);
            Assert.True(s.AnyLanded);
            Assert.Equal(MotorState.Grounded, s.Motor.State);
            T.Near(0f, s.Motor.Position.Y, 0.02f);
            T.Near(s.Motor.Settings.JumpSpeed, s.FirstImpact, 0.5f);
        }

        [Fact]
        public void JumpNeedsToBeGroundedAndIsOneShotPerPress()
        {
            var s = OnFloor();
            s.Step(Vec3.Zero, 0f, true);
            s.Run(5, Vec3.Zero);
            MotorStepResult r = s.Step(Vec3.Zero, 0f, true); // pressed again mid-air: nothing happens
            Assert.False(r.Jumped);
            Assert.False(r.ClimbStarted);
            Assert.True(s.Motor.Velocity.Y < 6f);
            s.Run(100, Vec3.Zero);
            Assert.InRange(s.MaxY, 1.0f, 1.2f);
        }

        [Fact]
        public void AirControlIsThirtyPercentOfGroundAcceleration()
        {
            var s = new Sim(T.BigFloor().ToWorld(), new Vec3(0, 50, 0));
            s.Run(30, new Vec3(10, 0, 0));
            // 0.5 s at 0.3 * 40 = 12 m/s^2 would reach 6 m/s, so the speed is capped by the 10 m/s wish only after 0.83 s.
            T.Near(6f, s.Motor.Velocity.X, 0.4f);
        }

        [Fact]
        public void WalkingOffATallLedgeFallsAndLandsWithTheEnergyOfAThreeMetreDrop()
        {
            var w = T.BigFloor().Box(new Vec3(-50, 0, -50), new Vec3(50, 3, 0)).ToWorld();
            var s = new Sim(w, new Vec3(0, 3, -3)).Settle();
            T.Near(3f, s.Motor.Position.Y, 0.02f);
            bool leftGround = false;
            for (int i = 0; i < 240; i++)
            {
                s.Step(new Vec3(0, 0, 3));
                if (!s.Motor.OnGround) leftGround = true;
            }
            Assert.True(leftGround);
            Assert.True(s.AnyLanded);
            float expected = (float)Math.Sqrt(2 * 9.81 * 3.0);
            T.Near(expected, s.FirstImpact, expected * 0.1f);
            T.Near(0f, s.Motor.Position.Y, 0.02f);
            Assert.Equal(MotorState.Grounded, s.Motor.State);
            Assert.True(s.Motor.Position.Z > 0f);
        }

        [Fact]
        public void FallingSpeedIsCappedAtTerminalVelocityAndLandsOnAThinFloorWithoutTunnelling()
        {
            var w = new MeshBuilder().Floor(-20, -20, 20, 20, 0).ToWorld();
            var s = new Sim(w, new Vec3(0, 300, 0));
            for (int i = 0; i < 900 && !s.AnyLanded; i++)
            {
                s.Step(Vec3.Zero);
                Assert.True(s.Motor.Velocity.Y >= -55.01f, "velocity " + s.Motor.Velocity.Y);
                Assert.True(s.Motor.Position.Y >= -0.02f, "fell through at step " + i + ": " + s.Motor.Position);
            }
            Assert.True(s.AnyLanded);
            T.Near(55f, s.FirstImpact, 1f);
            T.Near(0f, s.Motor.Position.Y, 0.02f);
            Assert.Equal(MotorState.Grounded, s.Motor.State);
        }

        [Fact]
        public void RunningAtFiftyFiveMetresPerSecondDoesNotTunnelThroughAThinWall()
        {
            var w = T.BigFloor().Wall(-10, 20, 10, 20, 0, 5).ToWorld();
            var s = new Sim(w, Vec3.Zero).Settle();
            float maxZ = 0f;
            for (int i = 0; i < 90; i++)
            {
                s.Motor.Velocity = new Vec3(0, 0, 55);
                s.Step(new Vec3(0, 0, 55));
                if (s.Motor.Position.Z > maxZ) maxZ = s.Motor.Position.Z;
            }
            Assert.True(maxZ <= 20f - 0.35f + 0.02f, "reached z " + maxZ);
            Assert.True(maxZ >= 19f, "never reached the wall: " + maxZ);
        }

        // ---------------- robustness ----------------

        [Fact]
        public void TenThousandRandomStepsStayFiniteAndAboveTheFloor()
        {
            var m = T.BigFloor()
                .Ramp(-5, 5, 20, 10, 30f)
                .Box(new Vec3(-10, 0, -30), new Vec3(-8, 2, -20))
                .Box(new Vec3(8, 0, 10), new Vec3(12, 0.4f, 14))
                .Wall(-40, 40, 40, 40, 0, 6);
            var s = new Sim(m.ToWorld(), new Vec3(0, 0, 0));
            s.World.AddBox(new Vec3(3, 1, 3), new Vec3(1, 1, 1), Quat.Yaw(30f), 1);
            var rng = new Random(1234);
            for (int i = 0; i < 10000; i++)
            {
                var desired = new Vec3((float)(rng.NextDouble() * 16 - 8), 0, (float)(rng.NextDouble() * 16 - 8));
                if (rng.Next(100) == 0) s.Motor.Velocity = new Vec3((float)(rng.NextDouble() * 40 - 20), (float)(rng.NextDouble() * 20), (float)(rng.NextDouble() * 40 - 20));
                s.Step(desired, (float)(rng.NextDouble() * 360), rng.Next(30) == 0);
                Assert.True(s.Motor.Position.IsFinite, "position at step " + i + ": " + s.Motor.Position);
                Assert.True(s.Motor.Velocity.IsFinite, "velocity at step " + i + ": " + s.Motor.Velocity);
                Assert.True(s.Motor.Position.Y > -0.5f, "below the floor at step " + i + ": " + s.Motor.Position);
                Assert.True(s.Motor.Breath >= 0f && s.Motor.Breath <= 20f);
            }
        }
    }
}
