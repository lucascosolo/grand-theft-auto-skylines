using System;
using System.Collections.Generic;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Roads;
using Xunit;

namespace GtaSkylines.Game.Tests.Roads
{
    public class RoadGraphTests
    {
        [Fact]
        public void NewGraph_HasVersionZeroAndNoContent()
        {
            var g = new RoadGraph();
            Assert.Equal(0, g.Version);
            Assert.Equal(0, g.NodeCount);
            Assert.Equal(0, g.SegmentCount);
        }

        [Fact]
        public void Rebuild_IncrementsVersionAndReplacesContent()
        {
            Net net = Nets.Straight();
            var g = new RoadGraph();
            Assert.Equal(1, g.Rebuild(net.Nodes, net.Segments, false));
            Assert.Equal(1, g.Version);
            Assert.Equal(2, g.NodeCount);
            Assert.Equal(1, g.SegmentCount);

            Net grid = Nets.Grid();
            Assert.Equal(12, g.Rebuild(grid.Nodes, grid.Segments, true));
            Assert.Equal(2, g.Version);
            Assert.Equal(9, g.NodeCount);
            Assert.True(g.LeftHandTraffic);
            Assert.False(g.TryGetSegment(1, out _));
        }

        [Fact]
        public void Rebuild_NullListsCountAsEmpty()
        {
            var g = new RoadGraph();
            Assert.Equal(0, g.Rebuild(null, null, false));
            Assert.Equal(1, g.Version);
            Assert.Equal(0, g.NodeCount);
        }

        [Fact]
        public void Rebuild_SkipsInvalidNodesAndSegments()
        {
            var nodes = new List<RoadNode>
            {
                new RoadNode(1, new Vec3(0, 0, 0)), new RoadNode(2, new Vec3(0, 0, 100)),
                new RoadNode(-5, new Vec3(9, 0, 9)),
            };
            RoadSegment ok = Nets.Straight(100).Segments[0];
            RoadSegment negativeId = ok; negativeId.Id = -1;
            RoadSegment missingNode = ok; missingNode.Id = 7; missingNode.EndNode = 99;
            RoadSegment nan = ok; nan.Id = 8; nan.B = new Vec3(float.NaN, 0, 0);
            var g = new RoadGraph();
            int accepted = g.Rebuild(nodes, new List<RoadSegment> { ok, negativeId, missingNode, nan }, false);
            Assert.Equal(1, accepted);
            Assert.Equal(2, g.NodeCount);
            Assert.Equal(1, g.SegmentCount);
            Assert.False(g.TryGetNode(-5, out _));
        }

        [Fact]
        public void Rebuild_LaterDuplicateIdReplacesEarlier()
        {
            var nodes = new List<RoadNode> { new RoadNode(1, new Vec3(0, 0, 0)), new RoadNode(1, new Vec3(5, 0, 5)), new RoadNode(2, new Vec3(0, 0, 100)) };
            RoadSegment a = Nets.Straight(100).Segments[0]; a.SpeedLimit = 11f;
            RoadSegment b = a; b.SpeedLimit = 22f;
            var g = new RoadGraph();
            g.Rebuild(nodes, new List<RoadSegment> { a, b }, false);
            Assert.Equal(2, g.NodeCount);
            Assert.Equal(1, g.SegmentCount);
            Assert.True(g.TryGetNode(1, out RoadNode n));
            Assert.Equal(5f, n.Position.X);
            Assert.True(g.TryGetSegment(1, out RoadSegment s));
            Assert.Equal(22f, s.SpeedLimit);
        }

        [Fact]
        public void TryGet_ReturnsSegmentAsPassedAndFalseWhenAbsent()
        {
            Net net = Nets.Straight();
            RoadSegment seg = net.Segments[0]; seg.Name = "Main St"; seg.LaneWidth = 0f; seg.SpeedLimit = 0f;
            net.Segments[0] = seg;
            RoadGraph g = net.Build();
            Assert.True(g.TryGetSegment(1, out RoadSegment got));
            Assert.Equal("Main St", got.Name);
            Assert.Equal(0f, got.LaneWidth);
            Assert.False(g.TryGetSegment(42, out _));
            Assert.False(g.TryGetNode(42, out _));
        }

        [Fact]
        public void SegmentLength_IsArcLength()
        {
            Check.Near(200f, Nets.Straight(200).Build().SegmentLength(1), 0.2f);
            Check.Near((float)(Math.PI * 100.0 / 2.0), Nets.QuarterArc(100).Build().SegmentLength(1), 0.5f);
            Assert.Equal(0f, Nets.Straight().Build().SegmentLength(99));
        }

        [Fact]
        public void NodeDegree_CountsDrivableSegmentsOnly()
        {
            Net net = Nets.TJunction().Node(5, 100, 100).Road(9, 5, 4, 0, 0).Node(6, -100, 0).Road(10, 2, 6, 1, 1, 10f, RoadFlags.PedestrianOnly);
            RoadGraph g = net.Build();
            Assert.Equal(3, g.NodeDegree(2));
            Assert.Equal(1, g.NodeDegree(1));
            Assert.Equal(1, g.NodeDegree(4));
            Assert.Equal(0, g.NodeDegree(5));
            Assert.Equal(0, g.NodeDegree(6));
            Assert.Equal(0, g.NodeDegree(77));
            // SegmentCount counts non-drivable segments too.
            Assert.Equal(5, g.SegmentCount);
        }

        [Fact]
        public void IsDrivable_RequiresLanesAndNotPedestrianOnly()
        {
            var seg = new RoadSegment { ForwardLanes = 1, BackwardLanes = 0 };
            Assert.True(seg.IsDrivable);
            seg.ForwardLanes = 0; seg.BackwardLanes = 2;
            Assert.True(seg.IsDrivable);
            seg.BackwardLanes = 0;
            Assert.False(seg.IsDrivable);
            seg.ForwardLanes = 2; seg.Flags = RoadFlags.PedestrianOnly;
            Assert.False(seg.IsDrivable);
        }

        // ---- nearest point ----

        [Fact]
        public void Nearest_OnStraightSegment_RightSideIsPositiveOffset()
        {
            RoadGraph g = Nets.Straight(200).Build();
            Assert.True(g.TryNearestPoint(new Vec3(4, 0, 50), 100f, out RoadPoint p));
            Assert.Equal(1, p.SegmentId);
            Check.Near(0.25f, p.T, 0.01f, "t");
            Check.Near(new Vec3(0, 0, 50), p.Position, 0.1f);
            Check.Near(4f, p.Distance, 0.05f, "distance");
            Check.Near(4f, p.LateralOffset, 0.05f, "lateral");
            Check.NearAngle(0f, p.HeadingDeg, 0.5f);
            Check.Near(50f, p.DistanceAlong, 0.2f, "along");

            Assert.True(g.TryNearestPoint(new Vec3(-4, 0, 50), 100f, out p));
            Check.Near(-4f, p.LateralOffset, 0.05f, "lateral left");
            Check.Near(4f, p.Distance, 0.05f);
        }

        [Fact]
        public void Nearest_LateralOffsetFollowsSegmentDirection_NotNodeOrder()
        {
            // Segment drawn from z = 200 down to z = 0: forward is south, so +x is to the left.
            var net = new Net().Node(1, 0, 200).Node(2, 0, 0).Road(1, 1, 2);
            RoadGraph g = net.Build();
            Assert.True(g.TryNearestPoint(new Vec3(4, 0, 50), 100f, out RoadPoint p));
            Check.Near(-4f, p.LateralOffset, 0.05f);
            Assert.True(Math.Abs(Math.Abs(p.HeadingDeg) - 180f) < 0.5f, "heading " + p.HeadingDeg);
            Check.Near(150f, p.DistanceAlong, 0.2f);
        }

        [Fact]
        public void Nearest_IsIndependentOfTrafficSide()
        {
            Assert.True(Nets.Straight().Build(true).TryNearestPoint(new Vec3(4, 0, 50), 100f, out RoadPoint p));
            Check.Near(4f, p.LateralOffset, 0.05f);
        }

        [Fact]
        public void Nearest_UsesThreeDimensionalDistance_AndClampsToSegmentEnds()
        {
            RoadGraph g = Nets.Straight(200).Build();
            Assert.True(g.TryNearestPoint(new Vec3(0, 3, 50), 100f, out RoadPoint p));
            Check.Near(3f, p.Distance, 0.05f);
            Check.Near(0f, p.LateralOffset, 0.05f);
            Assert.True(g.TryNearestPoint(new Vec3(0, 0, -10), 100f, out p));
            Check.Near(0f, p.T, 0.001f);
            Check.Near(10f, p.Distance, 0.05f);
            Assert.True(g.TryNearestPoint(new Vec3(0, 0, 230), 100f, out p));
            Check.Near(1f, p.T, 0.001f);
        }

        [Fact]
        public void Nearest_BeyondMaxDistance_ReturnsFalse()
        {
            RoadGraph g = Nets.Straight().Build();
            Assert.False(g.TryNearestPoint(new Vec3(50, 0, 50), 20f, out RoadPoint p));
            Assert.Equal(0f, p.Distance);
            Assert.Equal(Vec3.Zero, p.Position);
            Assert.True(g.TryNearestPoint(new Vec3(50, 0, 50), 60f, out _));
            Assert.False(new RoadGraph().TryNearestPoint(Vec3.Zero, 1000f, out _));
        }

        [Fact]
        public void Nearest_PicksTheNearerOfTwoRoads_AndIgnoresUndrivableOnes()
        {
            Net net = Nets.Straight(200).Node(3, 20, 0).Node(4, 20, 200).Road(2, 3, 4)
                .Node(5, 6, 0).Node(6, 6, 200).Road(3, 5, 6, 1, 1, 10f, RoadFlags.PedestrianOnly);
            RoadGraph g = net.Build();
            Assert.True(g.TryNearestPoint(new Vec3(14, 0, 100), 50f, out RoadPoint p));
            Assert.Equal(2, p.SegmentId);
            Assert.True(g.TryNearestPoint(new Vec3(5, 0, 100), 50f, out p));
            Assert.Equal(1, p.SegmentId); // the footpath at x = 6 is closer but not drivable
        }

        [Fact]
        public void Nearest_OnCurvedSegment_FollowsTheArc()
        {
            RoadGraph g = Nets.QuarterArc(100).Build();
            float s = (float)Math.Sqrt(0.5);
            // Mid-arc of the circle centred (100,0,0) is (100 - 100s, 0, 100s); heading there is 45 degrees.
            var outside = new Vec3(100f - 110f * s, 0, 110f * s);
            Assert.True(g.TryNearestPoint(outside, 50f, out RoadPoint p));
            Check.Near(0.5f, p.T, 0.02f, "t");
            Check.Near(new Vec3(100f - 100f * s, 0, 100f * s), p.Position, 0.6f);
            Check.Near(10f, p.Distance, 0.6f);
            Check.Near(-10f, p.LateralOffset, 0.6f, "outside of a right-hand bend is on the left");
            Check.NearAngle(45f, p.HeadingDeg, 1.5f);
            Check.Near((float)(Math.PI * 100.0 / 4.0), p.DistanceAlong, 0.8f);

            var inside = new Vec3(100f - 90f * s, 0, 90f * s);
            Assert.True(g.TryNearestPoint(inside, 50f, out p));
            Check.Near(10f, p.LateralOffset, 0.6f);
        }

        [Fact]
        public void Nearest_UsesControlPointsNotNodePositions()
        {
            // Chord from (0,0,0) to (100,0,100) passes through (50,0,50); the arc bulges away from it.
            RoadGraph g = Nets.QuarterArc(100).Build();
            Assert.True(g.TryNearestPoint(new Vec3(50, 0, 50), 100f, out RoadPoint p));
            Assert.True(p.Distance > 15f, "distance " + p.Distance);
        }
    }
}
