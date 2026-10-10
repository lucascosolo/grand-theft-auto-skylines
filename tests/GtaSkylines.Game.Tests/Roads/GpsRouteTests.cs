using System;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Roads;
using Xunit;

namespace GtaSkylines.Game.Tests.Roads
{
    public class GpsRouteTests
    {
        static RoadGraph LongRoad() { return Nets.Straight(2000, 1, 1, 15f).Build(); }

        static GpsRoute Started(RoadGraph g, Vec3 player, Vec3 target)
        {
            var gps = new GpsRoute(g);
            gps.SetTarget(target);
            Assert.True(gps.Tick(0.1f, player));
            return gps;
        }

        [Fact]
        public void Constructor_NullGraph_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new GpsRoute(null));
        }

        [Fact]
        public void NewGps_HasNoTargetAndTickDoesNothing()
        {
            var gps = new GpsRoute(LongRoad());
            Assert.False(gps.HasTarget);
            Assert.Equal(RouteStatus.None, gps.Status);
            Assert.False(gps.Tick(1f, Vec3.Zero));
            Assert.Equal(0, gps.RecomputeCount);
            Assert.Equal(0, gps.Route.Count);
        }

        [Fact]
        public void NewTarget_IsComputedImmediatelyOnTheNextTick()
        {
            var gps = new GpsRoute(LongRoad());
            gps.SetTarget(new Vec3(0, 0, 1000));
            Assert.True(gps.HasTarget);
            Assert.Equal(new Vec3(0, 0, 1000), gps.Target);
            Assert.True(gps.Tick(0.016f, new Vec3(0, 0, 100)));
            Assert.Equal(RouteStatus.Ok, gps.Status);
            Assert.Equal(1, gps.RecomputeCount);
            Check.Near(900f, gps.Route.Length, 3f);
            Check.Near(0f, gps.Progress, 1f);
            Check.Near(900f, gps.Remaining, 3f);
            Check.Near(0f, gps.OffRouteDistance, 0.5f);
        }

        [Fact]
        public void AFreshTarget_RecomputesImmediatelyEvenRightAfterAnotherRecompute()
        {
            GpsRoute gps = Started(LongRoad(), new Vec3(0, 0, 100), new Vec3(0, 0, 1000));
            gps.ClearTarget();
            Assert.False(gps.HasTarget);
            Assert.Equal(RouteStatus.None, gps.Status);
            Assert.Equal(0, gps.Route.Count);
            gps.SetTarget(new Vec3(0, 0, 1500));
            Assert.True(gps.Tick(0.1f, new Vec3(0, 0, 100)));
            Assert.Equal(2, gps.RecomputeCount);
        }

        [Fact]
        public void FollowingTheRoute_UpdatesProgressWithoutRecomputing()
        {
            GpsRoute gps = Started(LongRoad(), new Vec3(0, 0, 100), new Vec3(0, 0, 1000));
            for (int i = 1; i <= 100; i++)
                Assert.False(gps.Tick(0.5f, new Vec3(1.5f, 0, 100 + i * 5)));
            Assert.Equal(1, gps.RecomputeCount);
            Check.Near(500f, gps.Progress, 2f);
            Check.Near(400f, gps.Remaining, 3f);
            Check.Near(1.5f, gps.OffRouteDistance, 0.2f);
        }

        [Fact]
        public void StrayingMoreThan25m_RecomputesButAtMostEveryThreeSeconds()
        {
            GpsRoute gps = Started(LongRoad(), new Vec3(0, 0, 100), new Vec3(0, 0, 1000));
            var stray = new Vec3(30, 0, 500);
            Assert.False(gps.Tick(1.2f, stray));
            Assert.False(gps.Tick(1.2f, stray));
            Assert.Equal(1, gps.RecomputeCount);
            Check.Near(30f, gps.OffRouteDistance, 0.5f);
            Assert.True(gps.Tick(1.2f, stray), "3.6 s since the last recompute");
            Assert.Equal(2, gps.RecomputeCount);
            Assert.Equal(RouteStatus.Ok, gps.Status);
        }

        [Fact]
        public void StrayingForLong_NeverRecomputesMoreOftenThanOnceEveryThreeSeconds()
        {
            GpsRoute gps = Started(LongRoad(), new Vec3(0, 0, 100), new Vec3(0, 0, 1000));
            var stray = new Vec3(30, 0, 500); // still 30 m from the road after every recompute
            for (int i = 0; i < 20; i++) gps.Tick(0.5f, stray); // 10 s
            Assert.InRange(gps.RecomputeCount, 3, 4);
        }

        [Fact]
        public void StrayingLessThan25m_DoesNotRecompute()
        {
            GpsRoute gps = Started(LongRoad(), new Vec3(0, 0, 100), new Vec3(0, 0, 1000));
            for (int i = 0; i < 20; i++) Assert.False(gps.Tick(1f, new Vec3(20, 0, 300)));
            Assert.Equal(1, gps.RecomputeCount);
        }

        [Fact]
        public void TargetDriftingBeyond25m_RecomputesRateLimited_SmallDriftDoesNot()
        {
            GpsRoute gps = Started(LongRoad(), new Vec3(0, 0, 100), new Vec3(0, 0, 1000));
            gps.SetTarget(new Vec3(0, 0, 1010));
            for (int i = 0; i < 10; i++) Assert.False(gps.Tick(1f, new Vec3(0, 0, 100)));
            Assert.Equal(1, gps.RecomputeCount);

            gps = Started(LongRoad(), new Vec3(0, 0, 100), new Vec3(0, 0, 1000));
            gps.SetTarget(new Vec3(0, 0, 1200));
            Assert.False(gps.Tick(1f, new Vec3(0, 0, 100)));
            Assert.False(gps.Tick(1f, new Vec3(0, 0, 100)));
            Assert.True(gps.Tick(1.5f, new Vec3(0, 0, 100)));
            Assert.Equal(2, gps.RecomputeCount);
            Check.Near(1100f, gps.Route.Length, 3f);
        }

        [Fact]
        public void GraphVersionChange_RecomputesImmediately()
        {
            RoadGraph g = LongRoad();
            GpsRoute gps = Started(g, new Vec3(0, 0, 100), new Vec3(0, 0, 1000));
            Net same = Nets.Straight(2000, 1, 1, 15f);
            g.Rebuild(same.Nodes, same.Segments, false);
            Assert.True(gps.Tick(0.05f, new Vec3(0, 0, 100)));
            Assert.Equal(2, gps.RecomputeCount);
            Assert.Equal(g.Version, gps.Route.GraphVersion);
            Assert.False(gps.Tick(0.05f, new Vec3(0, 0, 100)));
        }

        [Fact]
        public void FailedComputation_IsRetriedEveryThreeSeconds()
        {
            var gps = new GpsRoute(LongRoad());
            gps.SetTarget(new Vec3(9000, 0, 9000));
            Assert.True(gps.Tick(0.1f, new Vec3(0, 0, 100)));
            Assert.Equal(RouteStatus.NoRoadNearTarget, gps.Status);
            Assert.Equal(0, gps.Route.Count);
            Assert.Equal(0f, gps.Remaining);
            Assert.True(float.IsPositiveInfinity(gps.OffRouteDistance));
            Assert.False(gps.Tick(1f, new Vec3(0, 0, 100)));
            Assert.False(gps.Tick(1f, new Vec3(0, 0, 100)));
            Assert.True(gps.Tick(1.1f, new Vec3(0, 0, 100)));
            Assert.Equal(2, gps.RecomputeCount);
        }

        [Fact]
        public void ClearTarget_EmptiesTheRoute()
        {
            GpsRoute gps = Started(LongRoad(), new Vec3(0, 0, 100), new Vec3(0, 0, 1000));
            gps.ClearTarget();
            Assert.Equal(RouteStatus.None, gps.Status);
            Assert.Equal(0, gps.Route.Count);
            Assert.False(gps.Tick(5f, new Vec3(0, 0, 100)));
        }
    }
}
