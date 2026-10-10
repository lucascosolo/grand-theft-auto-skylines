using GtaSkylines.Mod.Lifecycle;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class SurfacePickTests
    {
        [Fact]
        public void Default_is_no_hit()
        {
            var p = new SurfacePick();
            Assert.Equal(DropSurface.None, p.Surface);
            Assert.False(p.Hit);
        }

        [Fact]
        public void Terrain_bias_is_one_hundred()
        {
            Assert.Equal(100f, SurfacePick.TerrainBias);
        }

        [Fact]
        public void Offer_none_changes_nothing()
        {
            var p = new SurfacePick();
            Assert.False(p.Offer(DropSurface.None, 1f));
            Assert.False(p.Hit);
            Assert.True(p.Offer(DropSurface.Road, 50f));
            Assert.False(p.Offer(DropSurface.None, 1f));
            Assert.Equal(DropSurface.Road, p.Surface);
            Assert.Equal(50f, p.Distance);
        }

        [Fact]
        public void First_offer_wins_and_keeps_raw_distance()
        {
            var p = new SurfacePick();
            Assert.True(p.Offer(DropSurface.Terrain, 50f));
            Assert.True(p.Hit);
            Assert.Equal(DropSurface.Terrain, p.Surface);
            Assert.Equal(50f, p.Distance);
        }

        [Fact]
        public void Road_beats_terrain_within_the_bias()
        {
            var p = new SurfacePick();
            p.Offer(DropSurface.Terrain, 50f);
            Assert.True(p.Offer(DropSurface.Road, 120f));
            Assert.Equal(DropSurface.Road, p.Surface);
            Assert.Equal(120f, p.Distance);
        }

        [Fact]
        public void Road_beyond_the_bias_loses_to_terrain()
        {
            var p = new SurfacePick();
            p.Offer(DropSurface.Terrain, 50f);
            Assert.False(p.Offer(DropSurface.Road, 160f));
            Assert.Equal(DropSurface.Terrain, p.Surface);
            Assert.Equal(50f, p.Distance);
        }

        [Fact]
        public void Terrain_offered_after_road_is_biased_too()
        {
            var p = new SurfacePick();
            p.Offer(DropSurface.Road, 120f);
            Assert.False(p.Offer(DropSurface.Terrain, 50f));
            Assert.Equal(DropSurface.Road, p.Surface);
            Assert.Equal(120f, p.Distance);
        }

        [Fact]
        public void Nearer_building_beats_road()
        {
            var p = new SurfacePick();
            p.Offer(DropSurface.Building, 30f);
            Assert.False(p.Offer(DropSurface.Road, 40f));
            Assert.Equal(DropSurface.Building, p.Surface);
        }

        [Fact]
        public void Tie_keeps_the_earlier_pick()
        {
            var p = new SurfacePick();
            p.Offer(DropSurface.Road, 40f);
            Assert.False(p.Offer(DropSurface.Building, 40f));
            Assert.Equal(DropSurface.Road, p.Surface);
        }

        [Fact]
        public void Biased_tie_keeps_the_earlier_terrain_pick()
        {
            var p = new SurfacePick();
            p.Offer(DropSurface.Terrain, 50f);
            Assert.False(p.Offer(DropSurface.Road, 150f));
            Assert.Equal(DropSurface.Terrain, p.Surface);
        }
    }
}
