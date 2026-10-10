using System;
using System.Collections.Generic;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Roads;
using Xunit;

namespace GtaSkylines.Game.Tests.Roads
{
    /// <summary>Kinematic bicycle: wheelbase 2.7 m, max steer 35 degrees, throttle 4 m/s^2, brake 8 m/s^2, no reversing.</summary>
    internal sealed class Bike
    {
        public double X, Z, YawDeg, V;

        public Bike(double x, double z, double yawDeg, double v = 0.0) { X = x; Z = z; YawDeg = yawDeg; V = v; }

        public Vec3 Pos { get { return new Vec3((float)X, 0f, (float)Z); } }

        public float Heading { get { return MathUtil.WrapDeg((float)YawDeg); } }

        public void Step(DriveCommand c, double dt)
        {
            double delta = c.Steer * 35.0 * Math.PI / 180.0;
            double yawRate = V / 2.7 * Math.Tan(delta);
            YawDeg += yawRate * dt * 180.0 / Math.PI;
            double yaw = YawDeg * Math.PI / 180.0;
            X += V * Math.Sin(yaw) * dt;
            Z += V * Math.Cos(yaw) * dt;
            V = Math.Max(0.0, V + (c.Throttle * 4.0 - c.Brake * 8.0) * dt);
        }
    }

    public class RouteFollowerTests
    {
        const double Dt = 0.05;

        struct Sample
        {
            public float T;
            public double X, Z, V;
            public DriveCommand Cmd;
        }

        static List<Sample> Run(RouteFollower f, Route route, Bike b, float seconds)
        {
            var list = new List<Sample>();
            int steps = (int)Math.Round(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                DriveCommand c = f.Update(route, b.Pos, b.Heading, (float)b.V);
                list.Add(new Sample { T = (float)(i * Dt), X = b.X, Z = b.Z, V = b.V, Cmd = c });
                b.Step(c, Dt);
            }
            return list;
        }

        static Route StraightRoute(bool left = false, int lanes = 1, float limit = 15f, float length = 600f)
        {
            RoadGraph g = Nets.Straight(length, lanes, lanes, limit).Build(left);
            return Check.Route(g, Vec3.Zero, new Vec3(0, 0, length));
        }

        static float MaxAbsError(List<Sample> s, float fromT, float toT, float laneX, float minRemaining = 0f)
        {
            float worst = 0f;
            foreach (Sample x in s)
            {
                if (x.T < fromT || x.T > toT || x.Cmd.Remaining < minRemaining) continue;
                worst = Math.Max(worst, (float)Math.Abs(x.X - laneX));
            }
            return worst;
        }

        // ---- lane following on a straight ----

        [Fact]
        public void Straight_ConvergesToTheRightHandLaneCentreAndHoldsSpeed()
        {
            Route r = StraightRoute();
            var f = new RouteFollower();
            List<Sample> s = Run(f, r, new Bike(0, 0, 0), 25f);
            Assert.True(s[0].Cmd.Steer > 0f, "starts left of the lane, steers right");
            Assert.True(MaxAbsError(s, 8f, 25f, 1.5f, 40f) < 1.0f);
            float peak = 0f;
            foreach (Sample x in s) if (x.T > 10f) peak = Math.Max(peak, (float)x.V);
            Assert.InRange(peak, 13f, 16.5f);
        }

        [Fact]
        public void Straight_StartingOnTheLaneCentre_StaysThere()
        {
            Route r = StraightRoute();
            List<Sample> s = Run(new RouteFollower(), r, new Bike(1.5, 0, 0), 20f);
            Assert.True(MaxAbsError(s, 0f, 20f, 1.5f, 40f) < 0.5f);
        }

        [Fact]
        public void LeftHandTraffic_FlipsTheLaneSide()
        {
            Route r = StraightRoute(left: true);
            Assert.True(r.LeftHandTraffic);
            var f = new RouteFollower();
            List<Sample> s = Run(f, r, new Bike(0, 0, 0), 25f);
            Assert.True(s[0].Cmd.Steer < 0f, "starts right of the lane, steers left");
            Assert.True(MaxAbsError(s, 8f, 25f, -1.5f, 40f) < 1.0f);
        }

        [Fact]
        public void MultiLaneRoad_UsesTheKerbSideLane()
        {
            Route r = StraightRoute(lanes: 2); // lane centre (2 - 0.5) * 3 = 4.5 m right of the centreline
            List<Sample> s = Run(new RouteFollower(), r, new Bike(4.5, 0, 0), 25f);
            Assert.True(MaxAbsError(s, 5f, 25f, 4.5f, 40f) < 1.0f);
        }

        [Fact]
        public void LateralOffset_ShiftsThePathToTheRight_WhateverTheTrafficSide()
        {
            var f = new RouteFollower { LateralOffset = -3f };
            List<Sample> s = Run(f, StraightRoute(), new Bike(1.5, 0, 0), 25f);
            Assert.True(MaxAbsError(s, 8f, 25f, -1.5f, 40f) < 1.0f, "right-hand traffic, pulling out to the left");

            var g = new RouteFollower { LateralOffset = 3f };
            s = Run(g, StraightRoute(left: true), new Bike(-1.5, 0, 0), 25f);
            Assert.True(MaxAbsError(s, 8f, 25f, 1.5f, 40f) < 1.0f, "left-hand traffic, positive offset is to the right");
        }

        [Fact]
        public void Steer_PointsTowardsTheLane()
        {
            Route r = StraightRoute();
            DriveCommand fromLeft = new RouteFollower().Update(r, new Vec3(-8, 0, 50), 0f, 8f);
            Assert.True(fromLeft.Steer > 0.05f);
            DriveCommand fromRight = new RouteFollower().Update(r, new Vec3(10, 0, 50), 0f, 8f);
            Assert.True(fromRight.Steer < -0.05f);
            Assert.InRange(fromLeft.Steer, -1f, 1f);
        }

        [Fact]
        public void Update_ReportsProgressAndRemaining()
        {
            Route r = StraightRoute();
            var f = new RouteFollower();
            DriveCommand c = f.Update(r, new Vec3(1.5f, 0, 60), 0f, 5f);
            Check.Near(60f, c.Progress, 1.5f);
            Check.Near(r.Length - c.Progress, c.Remaining, 0.01f);
            Check.Near(c.Progress, f.Progress, 0.01f);
            Assert.False(c.Arrived);
            f.Reset();
            Assert.Equal(0f, f.Progress);
        }

        // ---- speed control ----

        [Fact]
        public void Pedals_FollowTheSpeedError()
        {
            Route r = StraightRoute(limit: 15f);
            DriveCommand slow = new RouteFollower().Update(r, new Vec3(1.5f, 0, 100), 0f, 0f);
            Assert.True(slow.Throttle > 0.5f);
            Assert.Equal(0f, slow.Brake);
            Check.Near(15f, slow.TargetSpeed, 0.5f);

            DriveCommand fast = new RouteFollower().Update(r, new Vec3(1.5f, 0, 100), 0f, 30f);
            Assert.True(fast.Brake > 0.5f);
            Assert.Equal(0f, fast.Throttle);

            DriveCommand onTarget = new RouteFollower().Update(r, new Vec3(1.5f, 0, 100), 0f, 15.2f);
            Assert.Equal(0f, onTarget.Brake);
        }

        [Fact]
        public void SpeedFactorAndMaxSpeed_ScaleAndCapTheTarget()
        {
            Route r = StraightRoute(limit: 15f);
            Check.Near(7.5f, new RouteFollower { SpeedFactor = 0.5f }.Update(r, new Vec3(1.5f, 0, 100), 0f, 5f).TargetSpeed, 0.3f);
            Check.Near(22.5f, new RouteFollower { SpeedFactor = 1.5f }.Update(r, new Vec3(1.5f, 0, 100), 0f, 5f).TargetSpeed, 0.5f);
            Check.Near(6f, new RouteFollower { MaxSpeed = 6f }.Update(r, new Vec3(1.5f, 0, 100), 0f, 5f).TargetSpeed, 0.3f);
        }

        [Fact]
        public void Curve_TrackedWithinOneMetre_AndSlowedBeforeIt()
        {
            RoadGraph g = Nets.StraightThenRightCurve(20f).Build();
            Route r = Check.Route(g, Vec3.Zero, new Vec3(240, 0, 340));
            var f = new RouteFollower();
            List<Sample> s = Run(f, r, new Bike(1.5, 0, 0), 40f);

            float peakBefore = 0f, speedAtEntry = -1f;
            float worstStraight = 0f, worstArc = 0f, worstExit = 0f;
            const float arcLen = 62.83f;
            foreach (Sample x in s)
            {
                float p = x.Cmd.Progress;
                if (x.Z < 240 && x.X < 5) peakBefore = Math.Max(peakBefore, (float)x.V);
                if (speedAtEntry < 0f && p >= 300f) speedAtEntry = (float)x.V;
                if (p >= 60f && p <= 270f) worstStraight = Math.Max(worstStraight, (float)Math.Abs(x.X - 1.5));
                if (p >= 305f && p <= 300f + arcLen - 5f)
                {
                    double radial = Math.Sqrt((x.X - 40) * (x.X - 40) + (x.Z - 300) * (x.Z - 300));
                    worstArc = Math.Max(worstArc, (float)Math.Abs(radial - 38.5));
                }
                if (p >= 300f + arcLen + 30f && p <= r.Length - 30f) worstExit = Math.Max(worstExit, (float)Math.Abs(x.Z - 338.5));
            }
            Assert.True(peakBefore >= 17f, "reaches near the limit on the straight, peak " + peakBefore);
            Assert.True(speedAtEntry > 0f && speedAtEntry <= 14.5f, "slowed for the bend, entry speed " + speedAtEntry);
            Assert.True(worstStraight < 1.0f, "straight error " + worstStraight);
            Assert.True(worstArc < 1.0f, "arc error " + worstArc);
            Assert.True(worstExit < 1.0f, "exit error " + worstExit);
        }

        [Fact]
        public void Commands_StayWithinRanges_AndNeverThrottleWhileBraking()
        {
            RoadGraph g = Nets.StraightThenRightCurve(20f).Build();
            Route r = Check.Route(g, Vec3.Zero, new Vec3(240, 0, 340));
            foreach (Sample x in Run(new RouteFollower(), r, new Bike(-6, 0, 20), 40f))
            {
                Assert.InRange(x.Cmd.Steer, -1f, 1f);
                Assert.InRange(x.Cmd.Throttle, 0f, 1f);
                Assert.InRange(x.Cmd.Brake, 0f, 1f);
                Assert.True(x.Cmd.TargetSpeed >= 0f);
                Assert.True(x.Cmd.Remaining >= 0f);
                if (x.Cmd.Brake > 0f) Assert.Equal(0f, x.Cmd.Throttle);
            }
        }

        // ---- stopping and arrival ----

        [Fact]
        public void StopPoint_VehicleComesToRestWithinTwoMetres()
        {
            Route r = StraightRoute(limit: 10f);
            var f = new RouteFollower();
            f.SetStop(200f);
            Assert.True(f.HasStop);
            Check.Near(200f, f.StopDistance, 0.001f);
            List<Sample> s = Run(f, r, new Bike(1.5, 0, 0), 60f);
            Sample last = s[s.Count - 1];
            Assert.True(Math.Abs(last.Z - 200.0) <= 2.0, "stopped at z = " + last.Z);
            Assert.True(last.V < 0.3, "speed " + last.V);
            Assert.True(last.Cmd.AtStopPoint);
            Assert.False(last.Cmd.Arrived);
            Assert.Equal(0f, last.Cmd.Throttle);
            Assert.Equal(1f, last.Cmd.Brake);
        }

        [Fact]
        public void StopPoint_IsApproachedWithinTheLimit_AndSlowingBeforeIt()
        {
            Route r = StraightRoute(limit: 10f);
            var f = new RouteFollower();
            f.SetStop(200f);
            List<Sample> s = Run(f, r, new Bike(1.5, 0, 0), 60f);
            foreach (Sample x in s)
            {
                Assert.True(x.V <= 11.0, "speed " + x.V);
                if (x.Z > 195) Assert.True(x.V < 3.0, "still doing " + x.V + " m/s at z = " + x.Z);
            }
        }

        [Fact]
        public void StopPoint_ClearedByClearStopResetAndNewRouteRevision()
        {
            Route r = StraightRoute();
            var f = new RouteFollower();
            f.SetStop(100f);
            f.ClearStop();
            Assert.False(f.HasStop);

            f.SetStop(100f);
            f.Reset();
            Assert.False(f.HasStop);

            f.SetStop(100f);
            f.Update(r, new Vec3(1.5f, 0, 10), 0f, 5f);
            Assert.True(f.HasStop);
            RoadGraph g = Nets.Straight(600, 1, 1, 15f).Build();
            g.FindRoute(Vec3.Zero, new Vec3(0, 0, 600), r); // refill: new revision
            DriveCommand c = f.Update(r, new Vec3(1.5f, 0, 10), 0f, 5f);
            Assert.False(f.HasStop);
            Assert.False(c.AtStopPoint);
        }

        [Fact]
        public void StopPoint_PastIt_CountsAsAtStopPoint()
        {
            Route r = StraightRoute();
            var f = new RouteFollower();
            f.SetStop(100f);
            DriveCommand c = f.Update(r, new Vec3(1.5f, 0, 130), 0f, 5f);
            Assert.True(c.AtStopPoint);
            Assert.Equal(0f, c.Throttle);
            Assert.Equal(1f, c.Brake);
        }

        [Fact]
        public void Arrival_FlagsAndHoldsTheBrakeAtTheRouteEnd()
        {
            RoadGraph g = Nets.Straight(300, 1, 1, 10f).Build();
            Route r = Check.Route(g, Vec3.Zero, new Vec3(0, 0, 150));
            var f = new RouteFollower();
            List<Sample> s = Run(f, r, new Bike(1.5, 0, 0), 40f);
            Assert.False(s[0].Cmd.Arrived);
            Check.Near(150f, s[0].Cmd.Remaining, 2f);
            Sample last = s[s.Count - 1];
            Assert.True(last.Cmd.Arrived);
            Assert.True(last.Cmd.Remaining <= f.ArrivalDistance + 0.001f);
            Assert.Equal(0f, last.Cmd.Throttle);
            Assert.Equal(1f, last.Cmd.Brake);
            Assert.InRange(last.Z, 140.0, 156.0);
            Assert.True(last.V < 0.3);
            foreach (Sample x in s) Assert.False(x.Cmd.Arrived && x.Z < 140);
        }

        [Fact]
        public void NullOrTooShortRoute_BrakesAndDoesNotArrive()
        {
            var f = new RouteFollower();
            foreach (Route r in new Route[] { null, new Route() })
            {
                DriveCommand c = f.Update(r, Vec3.Zero, 0f, 5f);
                Assert.Equal(0f, c.Steer);
                Assert.Equal(0f, c.Throttle);
                Assert.Equal(1f, c.Brake);
                Assert.False(c.Arrived);
            }
        }

        [Fact]
        public void FarFromRoute_StillSteersTowardsIt_AndNeverAsksForReverse()
        {
            Route r = StraightRoute();
            DriveCommand c = new RouteFollower().Update(r, new Vec3(60, 0, 100), 0f, 3f);
            Assert.True(c.Steer < 0f, "route lies to the west");
            Assert.True(c.TargetSpeed >= 0f);
            Assert.True(c.Throttle >= 0f && c.Brake >= 0f);
        }
    }
}
