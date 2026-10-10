using System;
using System.Collections.Generic;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Roads;
using Xunit;

namespace GtaSkylines.Game.Tests.Roads
{
    public class RouteTests
    {
        /// <summary>Two 100 m segments along +z, limits 10 and 20 m/s: route 0 -> 200 has ETA 10 + 5 = 15 s.</summary>
        static Route TwoSpeedRoute()
        {
            var net = new Net().Node(1, 0, 0).Node(2, 0, 100).Node(3, 0, 200).Road(1, 1, 2, 1, 1, 10f).Road(2, 2, 3, 1, 1, 20f);
            return Check.Route(net.Build(), new Vec3(0, 0, 0), new Vec3(0, 0, 200));
        }

        [Fact]
        public void NewRoute_IsEmpty()
        {
            var r = new Route();
            Assert.Equal(RouteStatus.None, r.Status);
            Assert.Equal(0, r.Count);
            Assert.Equal(0, r.SegmentCount);
            Assert.Equal(0f, r.Length);
            Assert.Equal(0f, r.EtaSeconds);
            Assert.Equal(Vec3.Zero, r.Start);
            Assert.Equal(Vec3.Zero, r.End);
            Assert.Equal(0f, r.EtaFrom(10f));
            Assert.Equal(default(RoutePoint).Distance, r.Sample(10f).Distance);
        }

        [Fact]
        public void Indexers_ThrowOutOfRange()
        {
            var r = new Route();
            Assert.Throws<ArgumentOutOfRangeException>(() => r[0]);
            Assert.Throws<ArgumentOutOfRangeException>(() => r.GetSegmentId(0));
            Route two = TwoSpeedRoute();
            Assert.Throws<ArgumentOutOfRangeException>(() => two[-1]);
            Assert.Throws<ArgumentOutOfRangeException>(() => two[two.Count]);
            Assert.Throws<ArgumentOutOfRangeException>(() => two.GetSegmentId(two.SegmentCount));
        }

        [Fact]
        public void Points_HaveCumulativeDistanceFromZeroToLength()
        {
            Route r = TwoSpeedRoute();
            Assert.True(r.Count >= 2);
            Assert.Equal(0f, r[0].Distance);
            Assert.Equal(r.Length, r[r.Count - 1].Distance);
            Check.Near(200f, r.Length, 1f);
            for (int i = 1; i < r.Count; i++)
            {
                float step = Vec3.Distance(r[i - 1].Position, r[i].Position);
                Check.Near(r[i - 1].Distance + step, r[i].Distance, 0.01f, "distance at " + i);
                Assert.True(step <= RoadDefaults.RouteSampleSpacing + 0.05f);
            }
        }

        [Fact]
        public void Eta_IsSumOfLengthOverLimit()
        {
            Route r = TwoSpeedRoute();
            Check.Near(15f, r.EtaSeconds, 0.2f);
        }

        [Fact]
        public void EtaFrom_CoversTheRemainderAndClamps()
        {
            Route r = TwoSpeedRoute();
            Check.Near(15f, r.EtaFrom(0f), 0.2f);
            Check.Near(10f, r.EtaFrom(50f), 0.2f);
            Check.Near(5f, r.EtaFrom(100f), 0.2f);
            Check.Near(2.5f, r.EtaFrom(150f), 0.2f);
            Check.Near(15f, r.EtaFrom(-30f), 0.2f);
            Check.Near(0f, r.EtaFrom(1e6f), 0.01f);
        }

        [Fact]
        public void Sample_InterpolatesAndClamps()
        {
            Route r = TwoSpeedRoute();
            RoutePoint a = r.Sample(50f);
            Check.Near(new Vec3(0, 0, 50), a.Position, 0.3f);
            Check.NearAngle(0f, a.HeadingDeg, 0.5f);
            Check.Near(50f, a.Distance, 0.01f);
            Check.Near(10f, a.SpeedLimit, 0.01f);
            Assert.Equal(1, a.SegmentId);

            RoutePoint b = r.Sample(150f);
            Check.Near(new Vec3(0, 0, 150), b.Position, 0.3f);
            Check.Near(20f, b.SpeedLimit, 0.01f);
            Assert.Equal(2, b.SegmentId);

            Check.Near(Vec3.Zero, r.Sample(-10f).Position, 0.01f);
            Assert.Equal(0f, r.Sample(-10f).Distance);
            Check.Near(r.End, r.Sample(1e6f).Position, 0.01f);
            Check.Near(r.Length, r.Sample(1e6f).Distance, 0.01f);
        }

        [Fact]
        public void Sample_OnACurve_HeadingIsThePiecesHeading()
        {
            RoadGraph g = Nets.QuarterArc(100f).Build();
            Route r = Check.Route(g, Vec3.Zero, new Vec3(100, 0, 100));
            RoutePoint mid = r.Sample(r.Length * 0.5f);
            Check.NearAngle(45f, mid.HeadingDeg, 3f);
        }

        [Fact]
        public void Project_FindsDistanceAlongAndDistanceToRoute()
        {
            Route r = TwoSpeedRoute();
            float along = r.Project(new Vec3(3, 0, 40), out float off);
            Check.Near(40f, along, 0.5f);
            Check.Near(3f, off, 0.1f);

            along = r.Project(new Vec3(0, 4, 500), out off);
            Check.Near(r.Length, along, 0.01f);
            Check.Near((float)Math.Sqrt(300.0 * 300.0 + 16.0), off, 0.1f);
        }

        [Fact]
        public void Project_Windowed_IgnoresPartsOutsideTheWindow()
        {
            Route r = TwoSpeedRoute();
            float along = r.Project(new Vec3(0, 0, 40), 60f, 120f, out float off);
            Check.Near(60f, along, 0.5f);
            Check.Near(20f, off, 0.5f);
        }

        [Fact]
        public void Project_OnACrossingRoute_WindowPicksTheRightPass()
        {
            // A loop that returns to its start: positions near the origin match both ends; the window decides.
            RoadGraph g = Nets.OneWayLoop().Build();
            Route r = Check.Route(g, new Vec3(0, 0, 60), new Vec3(0, 0, 40));
            float first = r.Project(new Vec3(0, 0, 50), 0f, 20f, out _);
            float second = r.Project(new Vec3(0, 0, 50), r.Length - 30f, r.Length, out _);
            Check.Near(0f, first, 10.5f);
            Assert.True(second > r.Length - 30f - 0.01f);
        }

        [Fact]
        public void Project_EmptyRoute_ReturnsZeroAndInfinity()
        {
            float along = new Route().Project(new Vec3(1, 2, 3), out float off);
            Assert.Equal(0f, along);
            Assert.True(float.IsPositiveInfinity(off));
        }

        [Fact]
        public void PointsAhead_SpacedFromDistance_AndCapped()
        {
            Route r = Check.Route(Nets.Straight(100).Build(), Vec3.Zero, new Vec3(0, 0, 100));
            var pts = new List<Vec3> { new Vec3(9, 9, 9) };
            Assert.Equal(3, r.PointsAhead(0f, 30f, 10, pts));
            Assert.Equal(3, pts.Count);
            Check.Near(new Vec3(0, 0, 30), pts[0], 0.3f);
            Check.Near(new Vec3(0, 0, 60), pts[1], 0.3f);
            Check.Near(new Vec3(0, 0, 90), pts[2], 0.3f);

            Assert.Equal(2, r.PointsAhead(0f, 30f, 2, pts));
            Assert.Equal(2, pts.Count);

            Assert.Equal(2, r.PointsAhead(50f, 20f, 10, pts));
            Check.Near(new Vec3(0, 0, 70), pts[0], 0.3f);
            Check.Near(new Vec3(0, 0, 90), pts[1], 0.3f);
        }

        [Fact]
        public void PointsAhead_DegenerateInputs()
        {
            Route r = Check.Route(Nets.Straight(100).Build(), Vec3.Zero, new Vec3(0, 0, 100));
            var pts = new List<Vec3>();
            Assert.Equal(0, r.PointsAhead(0f, 0f, 5, pts));
            Assert.Equal(0, r.PointsAhead(0f, -3f, 5, pts));
            Assert.Equal(0, r.PointsAhead(95f, 10f, 5, pts));
            Assert.Equal(0, new Route().PointsAhead(0f, 10f, 5, pts));
            Assert.Throws<ArgumentNullException>(() => r.PointsAhead(0f, 10f, 5, null));
        }

        [Fact]
        public void Clear_EmptiesTheRouteAndBumpsRevision()
        {
            Route r = TwoSpeedRoute();
            int rev = r.Revision;
            r.Clear();
            Assert.Equal(RouteStatus.None, r.Status);
            Assert.Equal(0, r.Count);
            Assert.Equal(0f, r.Length);
            Assert.True(r.Revision > rev);
        }

        [Fact]
        public void Refill_ReusesTheSameRouteObject()
        {
            RoadGraph g = Nets.Grid().Build();
            var r = new Route();
            g.FindRoute(new Vec3(0, 0, 0), new Vec3(200, 0, 200), r);
            int rev = r.Revision;
            g.FindRoute(new Vec3(0, 0, 0), new Vec3(100, 0, 0), r);
            Assert.True(r.Revision > rev);
            Check.Near(100f, r.Length, 1f);
            Assert.Equal(1, r.SegmentCount);
        }
    }
}
