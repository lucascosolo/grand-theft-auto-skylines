using System;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Vehicles;
using Xunit;

namespace GtaSkylines.Game.Tests.Vehicles
{
    public class VehicleDrivingTests
    {
        private static Sim OnFlat(VehicleModel model = null)
        {
            return new Sim(model ?? V.Car(), new Terrain().Flat(1500f).ToWorld());
        }

        [Fact]
        public void NewBody_StartsAtOriginAtRestWithFullHealth()
        {
            var b = new VehicleBody(VehicleTuning.FromModel(V.Car()));
            Assert.Equal(Vec3.Zero, b.Position);
            Assert.Equal(Vec3.Zero, b.Velocity);
            Assert.Equal(Vec3.Zero, b.AngularVelocity);
            V.Near(1f, b.Rotation.W, 1e-4f);
            Assert.Equal(100f, b.Health, 1e-3);
            Assert.Equal(VehicleDamageState.Intact, b.DamageState);
            Assert.Equal(0f, b.FireSeconds, 1e-4);
            Assert.Equal(0, b.GroundedWheelCount);
            Assert.Equal(1, b.Gear);
            V.Near(0.2f, b.EngineRpm, 1e-3f);
        }

        [Fact]
        public void AtRest_SettlesWithoutDriftOrJitter()
        {
            Sim s = OnFlat();
            Vec3 start = s.Body.Position;
            s.Run(VehicleControls.None, 1f);
            float worst = 0f;
            for (int i = 0; i < 60; i++)
            {
                s.Step(VehicleControls.None);
                worst = Math.Max(worst, s.Body.Velocity.Length);
            }
            Assert.True(worst < 0.01f, "velocity " + worst + " m/s between 1 s and 2 s");
            Assert.True(Vec3.DistanceXZ(start, s.Body.Position) < 0.05f);
            V.Near(s.Tuning.HalfExtents.Y, s.Body.Position.Y, 0.05f);
            Assert.Equal(4, s.Body.GroundedWheelCount);
            Assert.True(s.Body.Upright > 0.999f);
            for (int w = 0; w < 4; w++)
            {
                WheelState ws = s.Body.GetWheel(w);
                Assert.True(ws.Contact);
                V.Near(0.097f, ws.Compression, 0.03f);
            }
        }

        [Fact]
        public void OnFlatGround_NoLeftGroundLandedOrImpactWhileDriving()
        {
            Sim s = OnFlat();
            for (int i = 0; i < 300; i++)
            {
                VehicleStepResult r = s.Step(new VehicleControls { Throttle = 1f });
                Assert.False(r.LeftGround);
                Assert.False(r.Landed);
                Assert.Equal(0, r.ImpactCount);
            }
        }

        [Fact]
        public void FullThrottle_ReachesNinetyPercentOfTopSpeedQuickly_AndNeverExceedsItBy2Percent()
        {
            Sim s = OnFlat(V.Car(20f));
            float top = s.Tuning.TopSpeed;
            float tReach = -1f, max = 0f;
            for (int i = 0; i < 60 * 40; i++)
            {
                s.Step(new VehicleControls { Throttle = 1f });
                max = Math.Max(max, s.Body.Speed);
                if (tReach < 0f && s.Body.Speed >= 0.9f * top)
                {
                    tReach = s.Time;
                }
            }
            Assert.True(tReach > 0f && tReach <= 12f, "reached 90% of top speed at " + tReach + " s");
            Assert.True(max <= top * 1.02f, "peak " + max + " vs top " + top);
            Assert.True(max >= 0.95f * top, "peak " + max + " never approached top " + top);
        }

        [Fact]
        public void FullThrottle_AcceleratesQuicklyFromStandstill()
        {
            Sim s = OnFlat(V.Car(30f));
            s.Run(new VehicleControls { Throttle = 1f }, 3f);
            Assert.True(s.Body.Speed > 9f, "speed after 3 s " + s.Body.Speed);
            Assert.True(s.Body.Speed < 18f);
            Assert.True(s.Body.Position.Z > 10f, "drove forward (+z)");
        }

        [Fact]
        public void Coasting_SlowsNaturally()
        {
            Sim s = OnFlat();
            s.DriveTo(20f);
            float before = s.Body.Speed;
            s.Run(VehicleControls.None, 3f);
            Assert.True(s.Body.Speed < before - 0.3f, "speed " + before + " -> " + s.Body.Speed);
            Assert.True(s.Body.Speed > 5f, "no instant stop");
        }

        [Fact]
        public void Braking_FromTwentyFiveStopsWithinFortyMetres()
        {
            Sim s = OnFlat();
            s.DriveTo(25f);
            float z0 = s.Body.Position.Z;
            int guard = 0;
            while (s.Body.Speed > 0.3f && guard++ < 60 * 10)
            {
                s.Step(new VehicleControls { Brake = 1f });
            }
            float dist = s.Body.Position.Z - z0;
            Assert.True(s.Body.Speed <= 0.3f, "still moving at " + s.Body.Speed);
            Assert.True(dist < 40f, "stopping distance " + dist);
            Assert.True(dist > 15f, "implausibly short stop " + dist);
            Assert.True(s.Body.Upright > 0.95f);
        }

        [Fact]
        public void Brake_OnStoppedCar_ReversesAndCapsAtReverseTopSpeed()
        {
            Sim s = OnFlat();
            s.Run(VehicleControls.None, 0.5f);
            float min = 0f;
            for (int i = 0; i < 60 * 12; i++)
            {
                s.Step(new VehicleControls { Brake = 1f });
                min = Math.Min(min, s.Body.Speed);
            }
            Assert.True(s.Body.Speed < -8f, "reverse speed " + s.Body.Speed);
            Assert.True(min >= -9f * 1.03f, "exceeded reverse top speed: " + min);
            Assert.True(s.Body.Position.Z < -20f, "moved backward");
            Assert.Equal(-1, s.Body.Gear);
        }

        [Fact]
        public void Brake_WhileRollingForward_BrakesInsteadOfReversing()
        {
            Sim s = OnFlat();
            s.DriveTo(15f);
            s.Run(new VehicleControls { Brake = 1f }, 0.5f);
            Assert.True(s.Body.Speed > 0f, "still rolling forward after half a second of braking");
            Assert.True(s.Body.Speed < 12f, "braking had an effect");
        }

        [Fact]
        public void ThrottleWhileRollingBackward_Brakes_InsteadOfDrivingForward()
        {
            Sim s = OnFlat();
            s.Run(new VehicleControls { Brake = 1f }, 3f);
            float rev = s.Body.Speed;
            Assert.True(rev < -4f);
            s.Run(new VehicleControls { Throttle = 1f }, 0.3f);
            Assert.True(s.Body.Speed > rev + 1.5f, "W while reversing brakes: " + rev + " -> " + s.Body.Speed);
            Assert.True(s.Body.Speed < 0f, "...but does not instantly drive forward");
        }

        [Fact]
        public void EqualThrottleAndBrake_OnStoppedCar_HoldsIt()
        {
            Sim s = OnFlat();
            s.Run(new VehicleControls { Throttle = 1f, Brake = 1f }, 3f);
            Assert.True(Math.Abs(s.Body.Speed) < 0.1f);
            Assert.True(s.Body.Position.Length < 0.5f);
        }

        [Fact]
        public void LargerOfThrottleAndBrake_DrivesAtThatStrength()
        {
            Sim fwd = OnFlat();
            fwd.Run(new VehicleControls { Throttle = 0.6f, Brake = 0.2f }, 2f);
            Assert.True(fwd.Body.Speed > 3f);
            Sim rev = OnFlat();
            rev.Run(new VehicleControls { Throttle = 0.2f, Brake = 0.6f }, 2f);
            Assert.True(rev.Body.Speed < -2f);
        }

        [Fact]
        public void SteerInput_IsRateLimitedInsideTheBody()
        {
            Sim s = OnFlat();
            s.Step(new VehicleControls { Steer = 1f });
            float rate = s.Tuning.SteerRate;
            V.Near(rate * V.Dt, s.Body.SmoothedSteer, 0.02f);
            s.Run(new VehicleControls { Steer = 1f }, 0.1f);
            Assert.True(s.Body.SmoothedSteer < 0.6f, "after 0.1 s: " + s.Body.SmoothedSteer);
            s.Run(new VehicleControls { Steer = 1f }, 0.5f);
            V.Near(1f, s.Body.SmoothedSteer, 1e-3f);
            s.Step(new VehicleControls { Steer = 0f });
            V.Near(1f - rate * V.Dt, s.Body.SmoothedSteer, 0.02f);
            s.Run(new VehicleControls { Steer = 0f }, 0.5f);
            V.Near(0f, s.Body.SmoothedSteer, 1e-3f);
        }

        [Fact]
        public void FrontWheelsSteerFullLockAtStandstill_RearWheelsDoNot()
        {
            Sim s = OnFlat();
            s.Run(new VehicleControls { Steer = 1f }, 1f);
            V.Near(35f, s.Body.GetWheel(0).SteerDeg, 1f);
            V.Near(35f, s.Body.GetWheel(1).SteerDeg, 1f);
            Assert.Equal(0f, s.Body.GetWheel(2).SteerDeg, 1e-3);
            Assert.Equal(0f, s.Body.GetWheel(3).SteerDeg, 1e-3);
            s.Run(new VehicleControls { Steer = -1f }, 1f);
            V.Near(-35f, s.Body.GetWheel(0).SteerDeg, 1f);
        }

        [Fact]
        public void SteerLock_FallsLinearlyWithSpeed()
        {
            Sim s = OnFlat(V.Car(20f));
            float top = s.Tuning.TopSpeed;
            s.DriveTo(0.5f * top);
            for (int i = 0; i < 40; i++)
            {
                s.Step(s.Hold(0.5f * top, 1f));
            }
            float frac = Math.Abs(s.Body.Speed) / top;
            float expected = 35f + (8f - 35f) * frac;
            V.Near(expected, s.Body.GetWheel(0).SteerDeg, 2.5f);
            Assert.True(s.Body.GetWheel(0).SteerDeg < 30f && s.Body.GetWheel(0).SteerDeg > 12f);
        }

        [Fact]
        public void SteadySteer_AtTenMetresPerSecond_FollowsACircleOfRoughlyConstantRadius()
        {
            Sim s = OnFlat();
            s.DriveTo(10f);
            for (int i = 0; i < 90; i++)
            {
                s.Step(s.Hold(10f, 0.5f));
            }
            float sum = 0f;
            var radii = new float[120];
            for (int i = 0; i < radii.Length; i++)
            {
                s.Step(s.Hold(10f, 0.5f));
                float yaw = Math.Abs(s.Body.AngularVelocity.Y);
                radii[i] = new Vec3(s.Body.Velocity.X, 0f, s.Body.Velocity.Z).Length / Math.Max(yaw, 1e-4f);
                sum += radii[i];
            }
            float mean = sum / radii.Length;
            Assert.True(mean > 4f && mean < 60f, "radius " + mean);
            for (int i = 0; i < radii.Length; i++)
            {
                Assert.True(Math.Abs(radii[i] - mean) < 0.25f * mean, "radius " + radii[i] + " vs mean " + mean + " at sample " + i);
            }
            Assert.True(s.Body.AngularVelocity.Y > 0f, "steer +1 yaws to the right (positive y)");
            Assert.True(s.Body.Upright > 0.95f);
        }

        [Fact]
        public void SteerLeft_YawsNegative_AndMirrorsRight()
        {
            Sim r = OnFlat(), l = OnFlat();
            r.DriveTo(10f);
            l.DriveTo(10f);
            for (int i = 0; i < 48; i++)
            {
                r.Step(r.Hold(10f, 1f));
                l.Step(l.Hold(10f, -1f));
            }
            Assert.True(r.Body.AngularVelocity.Y > 0.1f);
            Assert.True(l.Body.AngularVelocity.Y < -0.1f);
            Assert.True(r.Body.Position.X > 0.2f && l.Body.Position.X < -0.2f);
            V.Near(r.Body.Position.X, -l.Body.Position.X, 0.3f);
        }

        [Fact]
        public void Handbrake_WithSteerAtTwentyMetresPerSecond_YawsFasterAndSlides()
        {
            float yawNormal, yawHand, slideNormal, slideHand;
            Slide(false, out yawNormal, out slideNormal);
            Slide(true, out yawHand, out slideHand);
            Assert.True(yawHand > yawNormal, "yaw rate with handbrake " + yawHand + " vs without " + yawNormal);
            Assert.True(slideHand > 2f, "lateral speed " + slideHand);
        }

        private static void Slide(bool handbrake, out float maxYaw, out float maxLateral)
        {
            Sim s = OnFlat();
            s.DriveTo(20f);
            maxYaw = 0f;
            maxLateral = 0f;
            for (int i = 0; i < 90; i++)
            {
                s.Step(new VehicleControls { Steer = 0.6f, Handbrake = handbrake });
                maxYaw = Math.Max(maxYaw, Math.Abs(s.Body.AngularVelocity.Y));
                maxLateral = Math.Max(maxLateral, Math.Abs(Vec3.Dot(s.Body.Velocity, s.Body.Rotation.Right)));
            }
        }

        [Fact]
        public void Handbrake_Straight_LocksRearAndSlowsTheCar()
        {
            Sim s = OnFlat();
            s.DriveTo(20f);
            Sim t = OnFlat();
            t.DriveTo(20f);
            s.Run(new VehicleControls { Handbrake = true }, 1f);
            t.Run(VehicleControls.None, 1f);
            Assert.True(s.Body.Speed < t.Body.Speed - 3f, "handbrake " + s.Body.Speed + " vs coast " + t.Body.Speed);
            Assert.True(s.Body.GetWheel(2).SlipSpeed > 1f, "rear wheels skid");
        }

        [Fact]
        public void Gear_AndEngineRpm_FollowSpeedThroughFifths()
        {
            Sim s = OnFlat();
            float top = s.Tuning.TopSpeed;
            for (int i = 0; i < 60 * 12; i++)
            {
                s.Step(new VehicleControls { Throttle = 1f });
                float sp = Math.Max(s.Body.Speed, 0f);
                float fifths = 5f * sp / top;
                int gear = 1 + Math.Min(4, (int)Math.Floor(fifths));
                Assert.Equal(gear, s.Body.Gear);
                if (sp > 0.5f && fifths < 4.99f)
                {
                    float expected = 0.2f + 0.8f * (fifths - (float)Math.Floor(fifths));
                    V.Near(expected, s.Body.EngineRpm, 0.03f);
                }
                Assert.InRange(s.Body.EngineRpm, 0f, 1f);
            }
        }

        [Fact]
        public void EngineRpm_InReverse_FollowsReverseSpeed()
        {
            Sim s = OnFlat();
            s.Run(new VehicleControls { Brake = 1f }, 4f);
            Assert.Equal(-1, s.Body.Gear);
            float expected = 0.2f + 0.8f * Math.Min(1f, Math.Abs(s.Body.Speed) / s.Tuning.ReverseTopSpeed);
            V.Near(expected, s.Body.EngineRpm, 0.03f);
        }

        [Fact]
        public void WheelSpin_IncreasesWhenRollingForward_AndStaysWrapped()
        {
            Sim s = OnFlat();
            s.DriveTo(10f);
            float prev = s.Body.GetWheel(0).SpinDeg;
            int advances = 0;
            for (int i = 0; i < 120; i++)
            {
                s.Step(s.Hold(10f));
                float cur = s.Body.GetWheel(0).SpinDeg;
                Assert.InRange(cur, 0f, 359.999f);
                float delta = cur - prev;
                if (delta < -180f) delta += 360f;
                if (delta > 0f) advances++;
                prev = cur;
            }
            Assert.True(advances > 110, "spin advanced on " + advances + " of 120 steps");
        }
    }
}
