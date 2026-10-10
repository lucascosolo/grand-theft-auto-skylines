using System;
using System.Collections.Generic;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Roads;
using Xunit;

namespace GtaSkylines.Game.Tests.Roads
{
    public class PredictTests
    {
        [Fact]
        public void Predict_StraightRoad_IsSpeedTimesTime()
        {
            RoadGraph g = Nets.Straight(400).Build();
            RoadPrediction p = g.Predict(new Vec3(0, 0, 50), 0f, 10f, 3f);
            Assert.True(p.OnRoad);
            Assert.Equal(1, p.SegmentId);
            Check.Near(new Vec3(0, 0, 80), p.Position, 0.5f);
            Check.NearAngle(0f, p.HeadingDeg, 0.5f);
            Check.Near(30f, p.DistanceTravelled, 0.2f);
        }

        [Fact]
        public void Predict_SnapsToTheCentrelineFromAnOffsetPosition()
        {
            RoadGraph g = Nets.Straight(400).Build();
            RoadPrediction p = g.Predict(new Vec3(5, 0, 50), 0f, 10f, 3f);
            Check.Near(new Vec3(0, 0, 80), p.Position, 0.5f);
        }

        [Fact]
        public void Predict_HeadingPicksTheDirectionAlongTheSegment_IgnoringLaneDirection()
        {
            RoadGraph g = Nets.Straight(400, 1, 0).Build(); // lanes forward only
            RoadPrediction p = g.Predict(new Vec3(0, 0, 200), 180f, 10f, 5f);
            Check.Near(new Vec3(0, 0, 150), p.Position, 0.5f);
            Check.NearAngle(180f, p.HeadingDeg, 0.5f);
        }

        [Fact]
        public void Predict_DeadEnd_StopsAtTheNodeWithShortDistance()
        {
            RoadGraph g = Nets.Straight(200).Build();
            RoadPrediction p = g.Predict(new Vec3(0, 0, 150), 0f, 10f, 10f);
            Assert.True(p.OnRoad);
            Check.Near(new Vec3(0, 0, 200), p.Position, 0.5f);
            Check.Near(50f, p.DistanceTravelled, 0.5f);
        }

        [Fact]
        public void Predict_OffRoad_DeadReckonsOnTheGroundPlane()
        {
            RoadGraph g = Nets.Straight(200).Build();
            RoadPrediction p = g.Predict(new Vec3(500, 7, 500), 90f, 10f, 5f);
            Assert.False(p.OnRoad);
            Assert.Equal(-1, p.SegmentId);
            Check.Near(new Vec3(550, 7, 500), p.Position, 0.1f);
            Check.NearAngle(90f, p.HeadingDeg, 0.5f);
            Check.Near(50f, p.DistanceTravelled, 0.1f);
            Assert.False(new RoadGraph().Predict(Vec3.Zero, 0f, 1f, 1f).OnRoad);
        }

        [Fact]
        public void Predict_ZeroSpeedOrTime_ReturnsTheSnappedPoint()
        {
            RoadGraph g = Nets.Straight(200).Build();
            RoadPrediction p = g.Predict(new Vec3(4, 0, 50), 0f, 0f, 5f);
            Assert.True(p.OnRoad);
            Check.Near(new Vec3(0, 0, 50), p.Position, 0.2f);
            Assert.Equal(0f, p.DistanceTravelled);
            p = g.Predict(new Vec3(4, 0, 50), 0f, 10f, -1f);
            Assert.Equal(0f, p.DistanceTravelled);
            RoadPrediction off = g.Predict(new Vec3(500, 0, 50), 0f, 0f, 5f);
            Assert.False(off.OnRoad);
            Check.Near(new Vec3(500, 0, 50), off.Position, 0.01f);
        }

        [Fact]
        public void Predict_ThroughTJunction_ContinuesStraightOn()
        {
            RoadGraph g = Nets.TJunction().Build();
            RoadPrediction p = g.Predict(new Vec3(0, 0, -100), 0f, 10f, 15f);
            Assert.Equal(2, p.SegmentId);
            Check.Near(new Vec3(0, 0, 50), p.Position, 0.6f);
            Check.NearAngle(0f, p.HeadingDeg, 1f);
            Check.Near(150f, p.DistanceTravelled, 0.6f);
        }

        [Fact]
        public void Predict_ThroughYJunction_PicksTheMostAlignedContinuation()
        {
            // On the 60-degree branch 100 m from the junction, heading towards it (240 degrees): the
            // continuation closest to 240 is the road south (180), not the one north (0).
            RoadGraph g = Nets.YJunction().Build();
            var start = new Vec3(86.6025f, 0, 50f);
            RoadPrediction p = g.Predict(start, 240f, 10f, 15f);
            Assert.True(p.OnRoad);
            Assert.Equal(1, p.SegmentId);
            Check.Near(new Vec3(0, 0, -50), p.Position, 0.8f);
            Check.NearAngle(180f, p.HeadingDeg, 1f);
            Check.Near(150f, p.DistanceTravelled, 0.8f);
        }

        [Fact]
        public void Predict_AlongACurve_TravelsArcLength()
        {
            RoadGraph g = Nets.QuarterArc(100f).Build();
            RoadPrediction p = g.Predict(Vec3.Zero, 0f, 10f, 5f); // 50 m = 0.5 rad of the radius-100 arc
            double a = 0.5;
            var expected = new Vec3(100f - 100f * (float)Math.Cos(a), 0, 100f * (float)Math.Sin(a));
            Check.Near(expected, p.Position, 0.8f);
            Check.NearAngle((float)(a * 180.0 / Math.PI), p.HeadingDeg, 1.5f);
            Check.Near(50f, p.DistanceTravelled, 0.5f);
        }

        [Fact]
        public void Predict_DistanceIsConsistentAcrossSpeedsAndTimes()
        {
            RoadGraph g = Nets.Grid(4, 100f, 10f).Build();
            foreach (float speed in new[] { 5f, 12f, 20f })
                foreach (float t in new[] { 1f, 4f })
                {
                    RoadPrediction p = g.Predict(new Vec3(50, 0, 0), 90f, speed, t);
                    Assert.True(p.OnRoad);
                    Check.Near(speed * t, p.DistanceTravelled, 0.6f);
                    Assert.True(g.TryNearestPoint(p.Position, 0.6f, out _), "prediction is on a centreline");
                }
        }

        // ---- junctions ahead ----

        [Fact]
        public void JunctionsAhead_NullResults_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => Nets.CrossStreets().Build().FindJunctionsAhead(Vec3.Zero, 0f, 100f, null));
        }

        [Fact]
        public void JunctionsAhead_ReturnsJunctionsWithinDistanceInOrder()
        {
            RoadGraph g = Nets.CrossStreets().Build();
            var res = new List<JunctionAhead> { default(JunctionAhead) };
            int n = g.FindJunctionsAhead(new Vec3(10, 0, 0), 90f, 250f, res);
            Assert.Equal(2, n);
            Assert.Equal(2, res.Count);
            Assert.Equal(2, res[0].NodeId);
            Assert.Equal(3, res[1].NodeId);
            Check.Near(90f, res[0].Distance, 0.6f);
            Check.Near(190f, res[1].Distance, 0.6f);
            Check.Near(new Vec3(100, 0, 0), res[0].Position, 0.2f);
            Assert.Equal(4, res[0].Degree);
            Assert.Equal(1, res[0].ArrivingSegmentId);
            Assert.Equal(2, res[1].ArrivingSegmentId);
        }

        [Fact]
        public void JunctionsAhead_LimitsByDistance_AndSkipsDeadEnds()
        {
            RoadGraph g = Nets.CrossStreets().Build();
            var res = new List<JunctionAhead>();
            Assert.Equal(1, g.FindJunctionsAhead(new Vec3(10, 0, 0), 90f, 150f, res));
            Assert.Equal(0, g.FindJunctionsAhead(new Vec3(10, 0, 0), 90f, 50f, res));
            Assert.Empty(res);
            // Node 4 at x = 300 is a dead end (degree 1), not a junction.
            Assert.Equal(2, g.FindJunctionsAhead(new Vec3(10, 0, 0), 90f, 5000f, res));
            // Heading west runs into the dead end at node 1 without meeting a junction.
            Assert.Equal(0, g.FindJunctionsAhead(new Vec3(10, 0, 0), 270f, 500f, res));
        }

        [Fact]
        public void JunctionsAhead_ExcludesAJunctionAtTheSnappedStart()
        {
            RoadGraph g = Nets.CrossStreets().Build();
            var res = new List<JunctionAhead>();
            Assert.Equal(1, g.FindJunctionsAhead(new Vec3(100, 0, 0), 90f, 150f, res));
            Assert.Equal(3, res[0].NodeId);
            Check.Near(100f, res[0].Distance, 0.6f);
        }

        [Fact]
        public void JunctionsAhead_OffRoadOrEmpty_NoResults()
        {
            var res = new List<JunctionAhead> { default(JunctionAhead) };
            Assert.Equal(0, Nets.CrossStreets().Build().FindJunctionsAhead(new Vec3(150, 0, 500), 90f, 500f, res));
            Assert.Empty(res);
            Assert.Equal(0, new RoadGraph().FindJunctionsAhead(Vec3.Zero, 0f, 500f, res));
        }

        [Fact]
        public void JunctionsAhead_FollowsTheSamePathAsPredict_ThroughTurnsOnTheHighwayRamp()
        {
            // Along the highway the ramp merge at node 2 is a three-way junction 500 m ahead.
            RoadGraph g = Nets.HighwayWithRamp().Build();
            var res = new List<JunctionAhead>();
            Assert.Equal(1, g.FindJunctionsAhead(new Vec3(0, 0, 100), 0f, 600f, res));
            Assert.Equal(2, res[0].NodeId);
            Assert.Equal(3, res[0].Degree);
            Check.Near(400f, res[0].Distance, 1f);
            Assert.Equal(1, res[0].ArrivingSegmentId);
        }

        [Fact]
        public void JunctionsAhead_StopsAfterRevisitingANode()
        {
            // A one-way loop with a spur to make a junction: walking round a closed loop must terminate.
            Net net = Nets.OneWayLoop().Node(5, -100, 100).Road(5, 2, 5);
            var res = new List<JunctionAhead>();
            int n = net.Build().FindJunctionsAhead(new Vec3(0, 0, 50), 0f, 100000f, res);
            Assert.True(n >= 1 && n <= 4, "junction count " + n);
        }
    }
}
