using System;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Tests.Fakes;
using Xunit;

namespace GtaSkylines.Game.Tests.PhysicsTests
{
    public class CharacterMotorSpecialTests
    {
        // ---------------- ledge climb ----------------

        /// <summary>Floor plus a box whose front face is at z = 0.7 (0.35 m from the capsule's front surface at the origin).</summary>
        private static Sim LedgeScene(float top, float frontZ = 0.7f, MeshBuilder extra = null)
        {
            var m = T.BigFloor().Box(new Vec3(-5, 0, frontZ), new Vec3(5, top, 10));
            if (extra != null) m = extra;
            return new Sim(m.ToWorld(), Vec3.Zero).Settle();
        }

        [Fact]
        public void ClimbsOntoAOnePointFiveMetreLedgeWithHeadroom()
        {
            var s = LedgeScene(1.5f);
            MotorStepResult r = s.Step(Vec3.Zero, 0f, true);
            Assert.True(r.ClimbStarted);
            Assert.Equal(MotorState.Climbing, s.Motor.State);
            int startedAt = s.Steps;
            float lastProgress = s.Motor.ClimbProgress;
            int finishedAt = -1;
            for (int i = 0; i < 120 && finishedAt < 0; i++)
            {
                // Input is ignored while climbing.
                MotorStepResult step = s.Step(new Vec3(0, 0, -5), 180f, true);
                Assert.False(step.Jumped);
                if (step.ClimbFinished) finishedAt = s.Steps;
                else
                {
                    Assert.Equal(MotorState.Climbing, s.Motor.State);
                    Assert.True(s.Motor.ClimbProgress >= lastProgress, "progress went backwards");
                    Assert.InRange(s.Motor.ClimbProgress, 0f, 1f);
                    lastProgress = s.Motor.ClimbProgress;
                }
            }
            Assert.True(finishedAt > 0, "climb never finished");
            Assert.InRange(finishedAt - startedAt, 38, 46); // ClimbSeconds = 0.7 s
            Assert.Equal(MotorState.Grounded, s.Motor.State);
            T.Near(1.5f, s.Motor.Position.Y, 0.05f);
            Assert.True(s.Motor.Position.Z >= 0.7f + 0.35f - 0.02f, "z " + s.Motor.Position.Z);
            Assert.Equal(0f, s.Motor.ClimbProgress);
            Assert.False(s.Overlapping());
            s.Run(30, Vec3.Zero);
            T.Near(1.5f, s.Motor.Position.Y, 0.05f);
        }

        [Fact]
        public void LedgeClimbIsRefusedWithoutHeadroom()
        {
            // A slab 1.9 to 2.1 m up over the player: the capsule fits under it but cannot be lifted to the ledge.
            var m = T.BigFloor()
                .Box(new Vec3(-5, 0, 0.7f), new Vec3(5, 1.5f, 10))
                .Box(new Vec3(-5, 1.9f, -5), new Vec3(5, 2.1f, 0.7f));
            var s = LedgeScene(1.5f, 0.7f, m);
            for (int i = 0; i < 120; i++) s.Step(Vec3.Zero, 0f, i == 0);
            Assert.False(s.AnyClimbStarted);
            Assert.False(s.AnyClimbFinished);
            Assert.True(s.MaxY < 1.2f, "max y " + s.MaxY);
            T.Near(0f, s.Motor.Position.Y, 0.05f);
        }

        [Fact]
        public void LedgeClimbIsRefusedForATwoAndAHalfMetreLedge()
        {
            var s = LedgeScene(2.5f);
            for (int i = 0; i < 120; i++) s.Step(Vec3.Zero, 0f, i == 0);
            Assert.False(s.AnyClimbStarted);
            Assert.True(s.AnyJumped);
            Assert.True(s.MaxY < 1.5f, "max y " + s.MaxY);
        }

        [Fact]
        public void LedgeBelowTheMinimumHeightIsJumpedNotClimbed()
        {
            var s = LedgeScene(0.5f);
            MotorStepResult r = s.Step(Vec3.Zero, 0f, true);
            Assert.False(r.ClimbStarted);
            Assert.True(r.Jumped);
        }

        [Fact]
        public void LedgeBeyondReachOrBehindTheFacingIsNotClimbed()
        {
            var far = LedgeScene(1.5f, 1.4f);
            Assert.False(far.Step(Vec3.Zero, 0f, true).ClimbStarted);

            var behind = LedgeScene(1.5f);
            Assert.False(behind.Step(Vec3.Zero, 180f, true).ClimbStarted);
        }

        [Fact]
        public void TeleportCancelsAClimb()
        {
            var s = LedgeScene(1.5f);
            s.Step(Vec3.Zero, 0f, true);
            s.Run(10, Vec3.Zero);
            Assert.Equal(MotorState.Climbing, s.Motor.State);
            Assert.True(s.Motor.ClimbProgress > 0f);
            s.Motor.Teleport(new Vec3(-20, 0, -20));
            Assert.Equal(MotorState.Airborne, s.Motor.State);
            Assert.Equal(0f, s.Motor.ClimbProgress);
        }

        // ---------------- swimming ----------------

        private static Sim Pool(Vec3 feet)
        {
            var city = new FakeCity { Ground = -5f, WaterLevel = 0f };
            return new Sim(T.BigFloor(300, -5f).ToWorld(), feet, city);
        }

        [Fact]
        public void FeetOnePointTwoMetresBelowTheWaterSurfaceMeansSwimmingAndFloating()
        {
            var s = Pool(new Vec3(0, -1.3f, 0));
            MotorStepResult r = s.Step(Vec3.Zero);
            Assert.True(r.EnteredWater);
            Assert.Equal(MotorState.Swimming, s.Motor.State);
            s.Run(180, Vec3.Zero);
            Assert.Equal(MotorState.Swimming, s.Motor.State);
            T.Near(-1.2f, s.Motor.Position.Y, 0.1f);
            T.Near(0f, s.Motor.Velocity.Y, 0.2f);
            Assert.False(s.AnyLanded);
        }

        [Fact]
        public void SwimmerBelowTheFloatDepthIsBroughtUpToTheSurface()
        {
            var s = Pool(new Vec3(0, -3.5f, 0));
            s.Run(240, Vec3.Zero);
            Assert.Equal(MotorState.Swimming, s.Motor.State);
            T.Near(-1.2f, s.Motor.Position.Y, 0.1f);
        }

        [Fact]
        public void FallingIntoWaterEntersItWithoutALandingEvent()
        {
            var s = Pool(new Vec3(0, 3f, 0));
            s.Run(300, Vec3.Zero);
            Assert.True(s.AnyEntered);
            Assert.False(s.AnyLanded);
            Assert.Equal(MotorState.Swimming, s.Motor.State);
            T.Near(-1.2f, s.Motor.Position.Y, 0.15f);
        }

        [Fact]
        public void SwimSpeedIsCapped()
        {
            var s = Pool(new Vec3(0, -1.2f, 0));
            s.Run(120, new Vec3(5, 0, 0));
            Assert.InRange(s.Motor.Velocity.LengthXZ, 1.0f, 1.55f);
            Assert.InRange(s.Motor.Position.X, 1.5f, 3.2f);
        }

        [Fact]
        public void BreathDrainsOneSecondPerSecondAndDrowningStartsAfterTwentySeconds()
        {
            var s = Pool(new Vec3(0, -1.3f, 0));
            s.Run(600, Vec3.Zero);
            T.Near(10f, s.Motor.Breath, 0.3f);
            Assert.False(s.Motor.IsDrowning);
            s.Run(570, Vec3.Zero); // 19.5 s
            Assert.True(s.Motor.Breath > 0.2f && s.Motor.Breath < 0.8f, "breath " + s.Motor.Breath);
            Assert.False(s.Motor.IsDrowning);
            s.Run(90, Vec3.Zero); // 21 s
            Assert.Equal(0f, s.Motor.Breath);
            Assert.True(s.Motor.IsDrowning);
            s.Run(60, Vec3.Zero);
            Assert.Equal(0f, s.Motor.Breath);
            Assert.True(s.Motor.IsDrowning);
        }

        [Fact]
        public void BreathRefillsAfterLeavingTheWater()
        {
            var s = Pool(new Vec3(0, -1.3f, 0));
            s.Run(300, Vec3.Zero);
            T.Near(15f, s.Motor.Breath, 0.3f);
            s.City.WaterLevel = null;
            s.Step(Vec3.Zero);
            Assert.True(s.AnyLeft);
            Assert.NotEqual(MotorState.Swimming, s.Motor.State);
            s.Run(60, Vec3.Zero);
            T.Near(19f, s.Motor.Breath, 0.3f); // +4 per second
            s.Run(120, Vec3.Zero);
            Assert.Equal(20f, s.Motor.Breath);
            Assert.False(s.Motor.IsDrowning);
        }

        [Fact]
        public void TeleportRefillsBreath()
        {
            var s = Pool(new Vec3(0, -1.3f, 0));
            s.Run(300, Vec3.Zero);
            Assert.True(s.Motor.Breath < 16f);
            s.Motor.Teleport(new Vec3(0, -1.3f, 0));
            Assert.Equal(20f, s.Motor.Breath);
        }

        [Fact]
        public void WadingInShallowWaterIsNotSwimming()
        {
            // Water 0.5 m deep over a floor at -0.5: depth at the feet is 0.5 m, below SwimDepth.
            var city = new FakeCity { Ground = -0.5f, WaterLevel = 0f };
            var s = new Sim(T.BigFloor(300, -0.5f).ToWorld(), new Vec3(0, -0.5f, 0), city).Settle();
            s.Run(60, new Vec3(2, 0, 0));
            Assert.Equal(MotorState.Grounded, s.Motor.State);
            Assert.False(s.AnyEntered);
            T.Near(-0.5f, s.Motor.Position.Y, 0.02f);
        }

        [Fact]
        public void NullCityMeansNoWater()
        {
            var w = T.BigFloor(300, -5f).ToWorld();
            var m = new CharacterMotor(null);
            m.Teleport(new Vec3(0, -1.3f, 0));
            for (int i = 0; i < 60; i++) m.Step(T.Dt, new MotorInput(), w, null);
            Assert.NotEqual(MotorState.Swimming, m.State);
            Assert.Equal(20f, m.Breath);
        }

        // ---------------- dynamic shapes ----------------

        [Fact]
        public void OverlappingDynamicBoxPushesTheCapsuleOut()
        {
            var s = new Sim(T.BigFloor().ToWorld(), Vec3.Zero).Settle();
            s.World.AddBox(new Vec3(0.5f, 0.9f, 0), new Vec3(0.5f, 0.9f, 0.5f), Quat.Identity, 1);
            s.Step(Vec3.Zero);
            Assert.False(s.Overlapping());
            Assert.True(s.Motor.Position.X < -0.3f, "x " + s.Motor.Position.X);
            T.Near(0f, s.Motor.Position.Y, 0.05f);
        }

        [Fact]
        public void RotatedBoxPushesAlongItsLeastPenetrationAxis()
        {
            var s = new Sim(T.BigFloor().ToWorld(), Vec3.Zero).Settle();
            s.World.AddBox(new Vec3(1.0f, 0.9f, 0), new Vec3(1, 0.9f, 1), Quat.Yaw(45f), 1);
            s.Step(Vec3.Zero);
            Assert.False(s.Overlapping());
            Assert.True(s.Motor.Position.X < 0f, "x " + s.Motor.Position.X);
        }

        [Fact]
        public void BoxDrivenIntoThePlayerNeverEndsUpInsideIt()
        {
            var s = new Sim(T.BigFloor().ToWorld(), Vec3.Zero).Settle();
            for (int i = 1; i <= 120; i++)
            {
                float cx = 3f - 2f * (i * T.Dt);
                s.World.ClearDynamic();
                s.World.AddBox(new Vec3(cx, 0.9f, 0), new Vec3(0.5f, 0.9f, 0.5f), Quat.Identity, 1);
                s.Step(Vec3.Zero);
                Assert.False(s.Overlapping(), "overlap at frame " + i);
                Assert.True(s.Motor.Position.IsFinite);
            }
            float boxCentre = 3f - 2f * 2f;
            Assert.True(s.Motor.Position.X <= boxCentre - 0.5f - 0.35f + 0.02f, "x " + s.Motor.Position.X);
            Assert.True(s.Motor.Position.X >= boxCentre - 2f, "pushed unreasonably far: " + s.Motor.Position.X);
            T.Near(0f, s.Motor.Position.Y, 0.05f);
        }

        [Fact]
        public void OverlappingDynamicCapsuleIsPushedHorizontally()
        {
            var s = new Sim(T.BigFloor().ToWorld(), Vec3.Zero).Settle();
            s.World.AddCapsule(new Vec3(0.3f, 0, 0), 0.35f, 1.8f, 2);
            s.Step(Vec3.Zero);
            Assert.False(s.Overlapping());
            Assert.True(s.Motor.Position.X < 0f, "x " + s.Motor.Position.X);
            T.Near(0f, s.Motor.Position.Y, 0.02f);
        }

        [Fact]
        public void MaskIgnoringTheOwnTagLeavesTheMotorAlone()
        {
            var s = new Sim(T.BigFloor().ToWorld(), Vec3.Zero).Settle();
            s.Motor.Mask = QueryMask.All.Ignoring(7);
            s.World.AddCapsule(Vec3.Zero, 0.35f, 1.8f, 7);
            s.Run(10, Vec3.Zero);
            T.Near(Vec3.Zero, s.Motor.Position, 0.01f);
            Assert.Equal(QueryMask.All.Ignoring(7), s.Motor.Mask);
        }

        [Fact]
        public void WalkingIntoADynamicBoxIsBlockedByIt()
        {
            var s = new Sim(T.BigFloor().ToWorld(), Vec3.Zero).Settle();
            s.World.AddBox(new Vec3(3, 1, 0), new Vec3(1, 1, 1), Quat.Identity, 1);
            s.Run(150, new Vec3(3, 0, 0));
            Assert.InRange(s.Motor.Position.X, 1.55f, 1.66f);
            Assert.False(s.Overlapping());
        }

        [Fact]
        public void StaticOnlyMaskLetsTheMotorWalkThroughDynamicShapes()
        {
            var s = new Sim(T.BigFloor().ToWorld(), Vec3.Zero).Settle();
            s.Motor.Mask = QueryMask.StaticOnly;
            s.World.AddBox(new Vec3(3, 1, 0), new Vec3(1, 1, 1), Quat.Identity, 1);
            s.Run(150, new Vec3(3, 0, 0));
            Assert.True(s.Motor.Position.X > 4f, "x " + s.Motor.Position.X);
        }
    }
}
