using System;
using System.Collections.Generic;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Roads;
using Xunit;

namespace GtaSkylines.Game.Tests.Roads
{
    public class RoutingTests
    {
        [Fact]
        public void FindRoute_NullRoute_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => Nets.Straight().Build().FindRoute(Vec3.Zero, Vec3.One, null));
        }

        [Fact]
        public void FindRoute_EmptyGraph_NoRoadNearStart()
        {
            var r = new Route();
            Assert.Equal(RouteStatus.NoRoadNearStart, new RoadGraph().FindRoute(Vec3.Zero, new Vec3(10, 0, 10), r));
            Assert.Equal(RouteStatus.NoRoadNearStart, r.Status);
            Assert.Equal(0, r.Count);
        }

        [Fact]
        public void FindRoute_StartTooFarFromRoad_NoRoadNearStart()
        {
            RoadGraph g = Nets.Straight().Build();
            var r = new Route();
            Assert.Equal(RouteStatus.NoRoadNearStart, g.FindRoute(new Vec3(RoadDefaults.RouteSnapDistance + 100f, 0, 100), new Vec3(0, 0, 150), r));
            Assert.Equal(0, r.Count);
            Assert.Equal(0f, r.Length);
        }

        [Fact]
        public void FindRoute_TargetTooFarFromRoad_NoRoadNearTarget()
        {
            RoadGraph g = Nets.Straight().Build();
            var r = new Route();
            Assert.Equal(RouteStatus.NoRoadNearTarget, g.FindRoute(new Vec3(0, 0, 50), new Vec3(0, 0, 100 + 2000), r));
            Assert.Equal(RouteStatus.NoRoadNearTarget, r.Status);
            Assert.Equal(0, r.Count);
        }

        [Fact]
        public void FindRoute_PedestrianOnlyRoadsAreNotRouted()
        {
            var net = new Net().Node(1, 0, 0).Node(2, 0, 200).Road(1, 1, 2, 1, 1, 10f, RoadFlags.PedestrianOnly);
            Assert.Equal(RouteStatus.NoRoadNearStart, net.Build().FindRoute(new Vec3(0, 0, 50), new Vec3(0, 0, 150), new Route()));
        }

        [Fact]
        public void FindRoute_DisconnectedNetworks_Unreachable()
        {
            Net net = Nets.Straight().Node(3, 500, 0).Node(4, 500, 200).Road(2, 3, 4);
            var r = new Route();
            int rev = r.Revision;
            Assert.Equal(RouteStatus.Unreachable, net.Build().FindRoute(new Vec3(0, 0, 50), new Vec3(500, 0, 50), r));
            Assert.Equal(RouteStatus.Unreachable, r.Status);
            Assert.Equal(0, r.Count);
            Assert.True(r.Revision > rev);
        }

        [Fact]
        public void FindRoute_ClearsPreviousRouteOnFailure()
        {
            RoadGraph g = Nets.Straight().Build();
            var r = Check.Route(g, new Vec3(0, 0, 10), new Vec3(0, 0, 100));
            int rev = r.Revision;
            Assert.Equal(RouteStatus.NoRoadNearTarget, g.FindRoute(new Vec3(0, 0, 10), new Vec3(9000, 0, 0), r));
            Assert.Equal(0, r.Count);
            Assert.True(r.Revision > rev);
        }

        [Fact]
        public void FindRoute_StartAndEndMidSegment_AreSnappedAndExact()
        {
            RoadGraph g = Nets.Straight(200).Build();
            Route r = Check.Route(g, new Vec3(0, 0, 50), new Vec3(0, 0, 150));
            Assert.Equal(RouteStatus.Ok, r.Status);
            Check.Near(100f, r.Length, 0.5f);
            Check.Near(new Vec3(0, 0, 50), r.Start, 0.2f);
            Check.Near(new Vec3(0, 0, 150), r.End, 0.2f);
            Assert.Equal(new Vec3(0, 0, 50), r.RequestedStart);
            Assert.Equal(new Vec3(0, 0, 150), r.RequestedTarget);
            Assert.Equal(new List<int> { 1 }, Check.SegmentIds(r));
        }

        [Fact]
        public void FindRoute_OffRoadEnds_RouteStartsAndEndsOnTheCentreline()
        {
            RoadGraph g = Nets.Straight(200).Build();
            Route r = Check.Route(g, new Vec3(30, 0, 50), new Vec3(-40, 0, 150));
            Check.Near(new Vec3(0, 0, 50), r.Start, 0.2f);
            Check.Near(new Vec3(0, 0, 150), r.End, 0.2f);
            Assert.Equal(new Vec3(30, 0, 50), r.RequestedStart);
            Assert.Equal(new Vec3(-40, 0, 150), r.RequestedTarget);
        }

        [Fact]
        public void FindRoute_SameSegment_GoingBackwardUsesBackwardLanes()
        {
            RoadGraph g = Nets.Straight(200).Build();
            Route r = Check.Route(g, new Vec3(0, 0, 150), new Vec3(0, 0, 50));
            Check.Near(100f, r.Length, 0.5f);
            Check.NearAngle(180f, r[0].HeadingDeg, 1f);
            Assert.Equal(new List<int> { 1 }, Check.SegmentIds(r));
        }

        [Fact]
        public void FindRoute_SameSegmentAndSamePoint_StillOkWithTwoPoints()
        {
            RoadGraph g = Nets.Straight(200).Build();
            var r = new Route();
            Assert.Equal(RouteStatus.Ok, g.FindRoute(new Vec3(0, 0, 50), new Vec3(0, 0, 50), r));
            Assert.True(r.Count >= 2);
            Check.Near(0f, r.Length, 0.2f);
        }

        [Fact]
        public void FindRoute_OneWaySegment_ForwardOkBackwardUnreachable()
        {
            RoadGraph g = Nets.Straight(200, 1, 0).Build();
            Assert.Equal(RouteStatus.Ok, g.FindRoute(new Vec3(0, 0, 50), new Vec3(0, 0, 150), new Route()));
            var r = new Route();
            Assert.Equal(RouteStatus.Unreachable, g.FindRoute(new Vec3(0, 0, 150), new Vec3(0, 0, 50), r));
            Assert.Equal(0, r.Count);
        }

        [Fact]
        public void FindRoute_BackwardOnlySegment_IgnoresTheOneWayFlagAndReadsLaneCounts()
        {
            // Drawn start -> end along +z but only the backward lanes exist, and the OneWay flag is absent.
            RoadGraph g = Nets.Straight(200, 0, 1).Build();
            Assert.Equal(RouteStatus.Ok, g.FindRoute(new Vec3(0, 0, 150), new Vec3(0, 0, 50), new Route()));
            Assert.Equal(RouteStatus.Unreachable, g.FindRoute(new Vec3(0, 0, 50), new Vec3(0, 0, 150), new Route()));
        }

        [Fact]
        public void FindRoute_OneWayFlagWithTwoWayLanes_StillRoutesBothWays()
        {
            Net net = Nets.Straight(200, 1, 1);
            RoadSegment s = net.Segments[0]; s.Flags = RoadFlags.OneWay; net.Segments[0] = s;
            RoadGraph g = net.Build();
            Assert.Equal(RouteStatus.Ok, g.FindRoute(new Vec3(0, 0, 150), new Vec3(0, 0, 50), new Route()));
        }

        [Fact]
        public void FindRoute_OneWayLoop_TargetBehindGoesAllTheWayRound()
        {
            RoadGraph g = Nets.OneWayLoop().Build();
            Route ahead = Check.Route(g, new Vec3(0, 0, 60), new Vec3(0, 0, 80));
            Check.Near(20f, ahead.Length, 1f);

            Route behind = Check.Route(g, new Vec3(0, 0, 60), new Vec3(0, 0, 40));
            Check.Near(380f, behind.Length, 3f);
            Assert.Equal(new List<int> { 1, 2, 3, 4, 1 }, Check.SegmentIds(behind));
            Check.NearAngle(0f, behind[0].HeadingDeg, 1f);
        }

        [Fact]
        public void FindRoute_OneWayLoop_NeverTravelsAgainstTheLoop()
        {
            RoadGraph g = Nets.OneWayLoop().Build();
            Route r = Check.Route(g, new Vec3(100, 0, 50), new Vec3(0, 0, 50)); // east side to west side
            Assert.Equal(new List<int> { 3, 4, 1 }, Check.SegmentIds(r));
            Check.Near(200f, r.Length, 3f);
        }

        [Fact]
        public void FindRoute_Grid_ShortestPathAndSegmentList()
        {
            RoadGraph g = Nets.Grid(3, 100f, 10f).Build();
            Route r = Check.Route(g, new Vec3(0, 0, 0), new Vec3(200, 0, 200));
            Check.Near(400f, r.Length, 2f);
            Check.Near(40f, r.EtaSeconds, 0.5f);
            Assert.Equal(4, r.SegmentCount);
            Check.Near(Vec3.Zero, r.Start, 0.5f);
            Check.Near(new Vec3(200, 0, 200), r.End, 0.5f);
            // Consecutive segments in the list are distinct and each is a real segment.
            List<int> ids = Check.SegmentIds(r);
            for (int i = 0; i < ids.Count; i++)
            {
                Assert.True(g.TryGetSegment(ids[i], out _));
                if (i > 0) Assert.NotEqual(ids[i - 1], ids[i]);
            }
        }

        [Fact]
        public void FindRoute_Grid_MidSegmentStartAndTarget()
        {
            RoadGraph g = Nets.Grid(3, 100f, 10f).Build();
            Route r = Check.Route(g, new Vec3(50, 0, 0), new Vec3(100, 0, 50));
            Check.Near(100f, r.Length, 1.5f);
            Check.Near(new Vec3(50, 0, 0), r.Start, 0.5f);
            Check.Near(new Vec3(100, 0, 50), r.End, 0.5f);
        }

        [Fact]
        public void FindRoute_NoUTurnAtNodes_DeadEndTargetBehindStartNeedsNoReversal()
        {
            // Start mid-segment may go either way: from the middle of the T's main road to its east branch end.
            RoadGraph g = Nets.TJunction().Build();
            Route r = Check.Route(g, new Vec3(0, 0, -100), new Vec3(200, 0, 0));
            Check.Near(300f, r.Length, 2f);
            Assert.Equal(new List<int> { 1, 3 }, Check.SegmentIds(r));
        }

        [Fact]
        public void FindRoute_FasterDetourBeatsSlowDirectStreet()
        {
            RoadGraph g = Nets.HighwayDetour(30f).Build();
            Route r = Check.Route(g, new Vec3(0, 0, 0), new Vec3(1000, 0, 0));
            Check.Near(1600f, r.Length, 6f);
            Check.Near(1600f / 30f, r.EtaSeconds, 0.5f);
            Assert.Contains(2, Check.SegmentIds(r));
            Assert.Contains(3, Check.SegmentIds(r));
            Assert.Contains(4, Check.SegmentIds(r));
        }

        [Fact]
        public void FindRoute_SlowDetourLosesToTheDirectStreet()
        {
            // Direct: 1000 m at 5 m/s = 200 s. Detour: 1600 m at 6 m/s = 267 s.
            RoadGraph g = Nets.HighwayDetour(6f).Build();
            Route r = Check.Route(g, new Vec3(0, 0, 0), new Vec3(1000, 0, 0));
            Check.Near(1000f, r.Length, 6f);
            Check.Near(200f, r.EtaSeconds, 1f);
            Assert.Contains(1, Check.SegmentIds(r));
        }

        [Fact]
        public void FindRoute_HighwayOnRamp_RampIsOneWay()
        {
            RoadGraph g = Nets.HighwayWithRamp().Build();
            Route up = Check.Route(g, new Vec3(150, 0, 350), new Vec3(0, 0, 1000));
            List<int> ids = Check.SegmentIds(up);
            Assert.Equal(new List<int> { 3, 2 }, ids);

            var back = new Route();
            Assert.Equal(RouteStatus.Unreachable, g.FindRoute(new Vec3(0, 0, 1000), new Vec3(150, 0, 350), back));

            Route along = Check.Route(g, new Vec3(0, 0, 0), new Vec3(0, 0, 1000));
            Assert.DoesNotContain(3, Check.SegmentIds(along));
            Check.Near(1000f, along.Length, 3f);
            Check.Near(1000f / 30f, along.EtaSeconds, 0.5f);
        }

        [Fact]
        public void FindRoute_Curve_IsSubdividedAndFollowsTheArc()
        {
            RoadGraph g = Nets.QuarterArc(100f).Build();
            Route r = Check.Route(g, new Vec3(0, 0, 0), new Vec3(100, 0, 100));
            Check.Near((float)(Math.PI * 50.0), r.Length, 1f);
            Check.NearAngle(0f, r[0].HeadingDeg, 2f);
            Check.NearAngle(90f, r[r.Count - 1].HeadingDeg, 2f);
            for (int i = 0; i < r.Count; i++)
            {
                Check.Near(100f, Vec3.Distance(new Vec3(100, 0, 0), r[i].Position), 0.5f, "radius at " + i);
                if (i > 0)
                {
                    Assert.True(Vec3.Distance(r[i - 1].Position, r[i].Position) <= RoadDefaults.RouteSampleSpacing + 0.05f);
                    Assert.True(MathUtil.DeltaAngleDeg(r[i - 1].HeadingDeg, r[i].HeadingDeg) >= -0.01f, "heading turns right monotonically");
                }
            }
        }

        [Fact]
        public void FindRoute_PointsCarryDirectionalSegmentData()
        {
            var net = new Net().Node(1, 0, 0).Node(2, 0, 200).Road(1, 1, 2, 3, 1, 0f, RoadFlags.None, 0f);
            RoadGraph g = net.Build();
            Route fwd = Check.Route(g, new Vec3(0, 0, 20), new Vec3(0, 0, 180));
            Assert.Equal(3, fwd[0].Lanes);
            Check.Near(RoadDefaults.SpeedLimit, fwd[0].SpeedLimit, 0.01f);
            Check.Near(RoadDefaults.LaneWidth, fwd[0].LaneWidth, 0.01f);
            Assert.Equal(1, fwd[0].SegmentId);
            Route back = Check.Route(g, new Vec3(0, 0, 180), new Vec3(0, 0, 20));
            Assert.Equal(1, back[0].Lanes);
        }

        [Fact]
        public void FindRoute_JunctionPointsCarryNodeIds_NodesAppearOnce()
        {
            RoadGraph g = Nets.Grid(3, 100f, 10f).Build();
            Route r = Check.Route(g, new Vec3(0, 0, 0), new Vec3(200, 0, 100));
            int junctions = 0;
            for (int i = 0; i < r.Count; i++)
            {
                if (i > 0) Assert.True(Vec3.Distance(r[i - 1].Position, r[i].Position) > 0.001f, "duplicate point at " + i);
                if (r[i].JunctionNodeId >= 0)
                {
                    junctions++;
                    Assert.True(g.NodeDegree(r[i].JunctionNodeId) >= RoadDefaults.JunctionMinDegree);
                }
            }
            Assert.True(junctions >= 1);
        }

        [Fact]
        public void FindRoute_RevisionAndGraphVersionAreStamped()
        {
            RoadGraph g = Nets.Straight().Build(true);
            var r = new Route();
            int rev = r.Revision;
            g.FindRoute(new Vec3(0, 0, 10), new Vec3(0, 0, 100), r);
            Assert.True(r.Revision > rev);
            Assert.Equal(g.Version, r.GraphVersion);
            Assert.True(r.LeftHandTraffic);
        }

        // ---- wander ----

        [Fact]
        public void Wander_NullArguments_Throw()
        {
            RoadGraph g = Nets.Grid().Build();
            Assert.Throws<ArgumentNullException>(() => g.FindWanderRoute(Vec3.Zero, 0f, 100f, null, new Route()));
            Assert.Throws<ArgumentNullException>(() => g.FindWanderRoute(Vec3.Zero, 0f, 100f, new Rng(1), null));
        }

        [Fact]
        public void Wander_NoRoad_NoRoadNearStart()
        {
            var r = new Route();
            Assert.Equal(RouteStatus.NoRoadNearStart, new RoadGraph().FindWanderRoute(Vec3.Zero, 0f, 100f, new Rng(1), r));
            Assert.Equal(RouteStatus.NoRoadNearStart, Nets.Straight().Build().FindWanderRoute(new Vec3(5000, 0, 0), 0f, 100f, new Rng(1), r));
            Assert.Equal(0, r.Count);
        }

        [Theory]
        [InlineData(1u)]
        [InlineData(7u)]
        [InlineData(1234u)]
        public void Wander_StaysOnDrivableRoadsAndIsLongEnough(uint seed)
        {
            Net net = Nets.Grid(3, 100f, 10f).Node(50, 400, 0).Road(300, 3, 50, 1, 1, 10f, RoadFlags.PedestrianOnly);
            RoadGraph g = net.Build();
            var r = new Route();
            Assert.Equal(RouteStatus.Ok, g.FindWanderRoute(new Vec3(100, 0, 0), 90f, 700f, new Rng(seed), r));
            Assert.True(r.Length >= 700f);
            Assert.True(r.Length < 700f + 100f + 5f, "ends on the segment where length reaches minLength; was " + r.Length);
            for (int i = 0; i < r.Count; i++)
            {
                Assert.True(g.TryNearestPoint(r[i].Position, 0.5f, out RoadPoint p), "point " + i + " off road");
                Assert.NotEqual(300, p.SegmentId);
            }
            List<int> ids = Check.SegmentIds(r);
            for (int i = 1; i < ids.Count; i++) Assert.NotEqual(ids[i - 1], ids[i]);
        }

        [Fact]
        public void Wander_SameSeedSameRoute_DifferentSeedsEventuallyDiffer()
        {
            RoadGraph g = Nets.Grid(4, 100f, 10f).Build();
            List<int> Run(uint seed)
            {
                var r = new Route();
                g.FindWanderRoute(new Vec3(150, 0, 0), float.NaN, 800f, new Rng(seed), r);
                Assert.Equal(RouteStatus.Ok, r.Status);
                return Check.SegmentIds(r);
            }
            Assert.Equal(Run(5), Run(5));
            bool differ = false;
            for (uint s = 6; s < 16; s++) differ |= !Run(5).SequenceEqualTo(Run(s));
            Assert.True(differ);
        }

        [Fact]
        public void Wander_FirstDirectionFollowsHeading()
        {
            RoadGraph g = Nets.Straight(400).Build();
            var north = new Route();
            g.FindWanderRoute(new Vec3(0, 0, 200), 0f, 50f, new Rng(1), north);
            Assert.True(north.End.Z > north.Start.Z);
            var south = new Route();
            g.FindWanderRoute(new Vec3(0, 0, 200), 180f, 50f, new Rng(1), south);
            Assert.True(south.End.Z < south.Start.Z);
        }

        [Fact]
        public void Wander_OnOneWayLoop_FollowsTheLoop()
        {
            RoadGraph g = Nets.OneWayLoop().Build();
            var r = new Route();
            Assert.Equal(RouteStatus.Ok, g.FindWanderRoute(new Vec3(0, 0, 50), 180f, 350f, new Rng(3), r));
            Assert.Equal(new List<int> { 1, 2, 3, 4 }, Check.SegmentIds(r).GetRange(0, 4));
            Check.NearAngle(0f, r[0].HeadingDeg, 1f);
        }

        [Fact]
        public void Wander_TurnsBackAtADeadEnd()
        {
            RoadGraph g = Nets.Straight(200).Build();
            var r = new Route();
            Assert.Equal(RouteStatus.Ok, g.FindWanderRoute(new Vec3(0, 0, 100), 0f, 500f, new Rng(1), r));
            Assert.True(r.Length >= 500f);
        }

        [Fact]
        public void Wander_EndsEarlyAtADeadEndOfAOneWayRoad()
        {
            RoadGraph g = Nets.Straight(200, 1, 0).Build();
            var r = new Route();
            Assert.Equal(RouteStatus.Ok, g.FindWanderRoute(new Vec3(0, 0, 100), 0f, 500f, new Rng(1), r));
            Check.Near(100f, r.Length, 1f);
        }
    }

    internal static class ListExt
    {
        public static bool SequenceEqualTo(this List<int> a, List<int> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
