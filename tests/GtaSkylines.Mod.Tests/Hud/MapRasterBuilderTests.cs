using System.Collections.Generic;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Roads;
using GtaSkylines.Game.Tests.Fakes;
using GtaSkylines.Mod.Hud;
using Xunit;

namespace GtaSkylines.Mod.Tests.Hud
{
    public class MapRasterBuilderTests
    {
        private const float Mpp = 8f;
        private const int Max = 256;

        private static RoadSegment Seg(int id, int a, int b, Vec3 pa, Vec3 pb)
        {
            return new RoadSegment
            {
                Id = id, StartNode = a, EndNode = b,
                A = pa, B = pa + (pb - pa) / 3f, C = pa + (pb - pa) * (2f / 3f), D = pb,
                ForwardLanes = 1, BackwardLanes = 1, LaneWidth = 3f, SpeedLimit = 10f,
            };
        }

        private static RoadGraph Graph(float length = 200f)
        {
            var g = new RoadGraph();
            Rebuild(g, length);
            return g;
        }

        private static void Rebuild(RoadGraph g, float length)
        {
            var a = new Vec3(0, 0, 0);
            var b = new Vec3(0, 0, length);
            var nodes = new List<RoadNode> { new RoadNode(1, a), new RoadNode(2, b) };
            var segs = new List<RoadSegment> { Seg(1, 1, 2, a, b) };
            g.Rebuild(nodes, segs, false);
        }

        private static MapRaster Drain(MapRasterBuilder b, int budget, out int ticks)
        {
            ticks = 0;
            while (ticks < 5000000)
            {
                ticks++;
                MapRaster r = b.Tick(budget);
                if (r != null) return r;
            }
            return null;
        }

        [Fact]
        public void No_roads_or_empty_graph_builds_nothing()
        {
            var city = new FakeCity();
            var none = new MapRasterBuilder(null, city, Mpp, Max);
            Assert.Null(none.Tick(1000));
            Assert.False(none.Building);
            var empty = new MapRasterBuilder(new RoadGraph(), city, Mpp, Max);
            Assert.Null(empty.Tick(1000));
            Assert.False(empty.Building);
            Assert.Equal(-1, empty.BuiltVersion);
        }

        [Fact]
        public void Large_budget_delivers_once_then_idles_until_the_graph_changes()
        {
            RoadGraph g = Graph();
            var b = new MapRasterBuilder(g, new FakeCity(), Mpp, Max);
            Assert.Equal(-1, b.BuiltVersion);
            MapRaster r = b.Tick(int.MaxValue);
            Assert.NotNull(r);
            Assert.Equal(g.Version, b.BuiltVersion);
            Assert.False(b.Building);
            Assert.Null(b.Tick(int.MaxValue));
            Assert.False(b.Building);

            Rebuild(g, 300f);
            Assert.NotNull(b.Tick(int.MaxValue));
            Assert.Equal(g.Version, b.BuiltVersion);
        }

        [Fact]
        public void Small_budget_spreads_the_build_over_several_ticks()
        {
            RoadGraph g = Graph();
            var b = new MapRasterBuilder(g, new FakeCity(), Mpp, Max);
            Assert.Null(b.Tick(1));
            Assert.True(b.Building);
            Assert.Equal(-1, b.BuiltVersion);
            int ticks;
            Assert.NotNull(Drain(b, 1, out ticks));
            Assert.False(b.Building);
            Assert.Equal(g.Version, b.BuiltVersion);
            Assert.Null(b.Tick(1));
        }

        [Fact]
        public void Budget_below_one_counts_as_one()
        {
            var b = new MapRasterBuilder(Graph(), new FakeCity(), Mpp, Max);
            int ticks;
            Assert.NotNull(Drain(b, 0, out ticks));
            Assert.True(ticks > 1);
        }

        [Fact]
        public void Raster_is_identical_whatever_the_budget()
        {
            var city = new FakeCity();
            city.WaterLevel = 5f;
            MapRaster fast = new MapRasterBuilder(Graph(), city, Mpp, Max).Tick(int.MaxValue);
            int ticks;
            MapRaster slow = Drain(new MapRasterBuilder(Graph(), city, Mpp, Max), 1, out ticks);
            Assert.NotNull(fast);
            Assert.NotNull(slow);
            Assert.True(ticks > 1);
            Assert.Equal(fast.Pixels, slow.Pixels);
            Assert.Equal(fast.MinX, slow.MinX);
            Assert.Equal(fast.MinZ, slow.MinZ);
            Assert.Equal(fast.Rgba, slow.Rgba);
        }

        [Fact]
        public void Road_midpoint_is_road_coloured_and_far_corner_is_water_when_flooded()
        {
            var city = new FakeCity();
            city.Ground = 0f;
            city.WaterLevel = 5f;
            MapRaster r = new MapRasterBuilder(Graph(), city, Mpp, Max).Tick(int.MaxValue);
            int i, j;
            r.ToPixel(0f, 100f, out i, out j);
            Assert.Equal(MapStyle.Road, r.Get(i, j));
            Assert.Equal(MapStyle.Water, r.Get(0, 0));
        }

        [Fact]
        public void Graph_change_mid_build_restarts_and_never_delivers_the_stale_raster()
        {
            RoadGraph g = Graph();
            var b = new MapRasterBuilder(g, new FakeCity(), Mpp, Max);
            Assert.Null(b.Tick(1));
            Assert.True(b.Building);
            Rebuild(g, 400f);
            int ticks;
            MapRaster r = Drain(b, 1, out ticks);
            Assert.NotNull(r);
            Assert.Equal(g.Version, b.BuiltVersion);
            Assert.False(b.Building);
            MapRaster expected = new MapRasterBuilder(g, new FakeCity(), Mpp, Max).Tick(int.MaxValue);
            Assert.Equal(expected.Rgba, r.Rgba);
            Assert.Null(b.Tick(int.MaxValue));
        }
    }
}
