using GtaSkylines.Game.Mathx;
using GtaSkylines.Mod.Rendering;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class InterpTests
    {
        [Fact]
        public void Position_is_linear()
        {
            var r = Interp.Position(new Vec3(0, 0, 0), new Vec3(10, 20, -30), 0.25f);
            Assert.Equal(2.5f, r.X, 4); Assert.Equal(5f, r.Y, 4); Assert.Equal(-7.5f, r.Z, 4);
        }

        [Theory]
        [InlineData(-1f, 0f)]
        [InlineData(2f, 10f)]
        public void Position_clamps_alpha(float alpha, float expectedX)
        {
            Assert.Equal(expectedX, Interp.Position(Vec3.Zero, new Vec3(10, 0, 0), alpha).X, 4);
        }

        [Theory]
        [InlineData(350f, 10f, 0.5f, 0f)]
        [InlineData(10f, 350f, 0.25f, 5f)]
        [InlineData(0f, 90f, 0.5f, 45f)]
        [InlineData(90f, 0f, 1f, 0f)]
        [InlineData(30f, 100f, 0f, 30f)]
        [InlineData(30f, 100f, -5f, 30f)]
        [InlineData(30f, 100f, 5f, 100f)]
        public void YawDeg_takes_shortest_arc(float prev, float cur, float alpha, float expected)
        {
            float r = Interp.YawDeg(prev, cur, alpha);
            Assert.InRange(r, 0f, 359.9999f);
            Assert.Equal(expected, r, 3);
        }

        [Fact]
        public void YawDeg_result_is_normalised_below_360()
        {
            float r = Interp.YawDeg(355f, 5f, 0.25f);
            Assert.InRange(r, 0f, 359.9999f);
            Assert.Equal(357.5f, r, 3);
        }
    }

    public class RenderFrameTests
    {
        [Fact]
        public void Clear_empties_lists_and_keeps_capacity()
        {
            var f = new RenderFrame();
            f.Peds.Add(default(PedDraw)); f.Cars.Add(default(CarDraw)); f.Markers.Add(default(MarkerDraw));
            f.Shots.Add(default(ShotDraw)); f.MuzzleFlashes.Add(default(FlashDraw));
            f.Impacts.Add(default(ImpactDraw)); f.Explosions.Add(default(ExplosionDraw));
            int[] caps = { f.Peds.Capacity, f.Cars.Capacity, f.Markers.Capacity, f.Shots.Capacity, f.MuzzleFlashes.Capacity, f.Impacts.Capacity, f.Explosions.Capacity };

            f.Clear();

            Assert.Empty(f.Peds); Assert.Empty(f.Cars); Assert.Empty(f.Markers); Assert.Empty(f.Shots);
            Assert.Empty(f.MuzzleFlashes); Assert.Empty(f.Impacts); Assert.Empty(f.Explosions);
            Assert.Equal(caps, new[] { f.Peds.Capacity, f.Cars.Capacity, f.Markers.Capacity, f.Shots.Capacity, f.MuzzleFlashes.Capacity, f.Impacts.Capacity, f.Explosions.Capacity });
        }
    }
}
