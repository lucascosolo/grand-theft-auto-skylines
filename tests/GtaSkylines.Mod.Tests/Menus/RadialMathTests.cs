using GtaSkylines.Mod.Menus;
using Xunit;

namespace GtaSkylines.Mod.Tests.Menus
{
    public class RadialMathTests
    {
        [Theory]
        [InlineData(0f, -1f, 0)]
        [InlineData(1f, 0f, 2)]
        [InlineData(0f, 1f, 4)]
        [InlineData(-1f, 0f, 6)]
        [InlineData(1f, -1f, 1)]
        [InlineData(1f, 1f, 3)]
        [InlineData(-1f, 1f, 5)]
        [InlineData(-1f, -1f, 7)]
        public void Eight_segments_are_numbered_clockwise_from_up(float dx, float dy, int expected)
        {
            Assert.Equal(expected, RadialMath.Segment(dx, dy, 8, 0.1f));
        }

        [Theory]
        [InlineData(1f, -1f, 1)]
        [InlineData(1f, 1f, 2)]
        [InlineData(-1f, 1f, 3)]
        [InlineData(-1f, -1f, 0)]
        public void Boundary_belongs_to_the_clockwise_segment(float dx, float dy, int expected)
        {
            Assert.Equal(expected, RadialMath.Segment(dx, dy, 4, 0.1f));
        }

        [Fact]
        public void Dead_zone_gives_minus_one()
        {
            Assert.Equal(-1, RadialMath.Segment(0.05f, 0f, 8, 0.1f));
            Assert.Equal(-1, RadialMath.Segment(0f, 0f, 8, 0.5f));
        }

        [Fact]
        public void Length_equal_to_the_dead_zone_is_not_below_it()
        {
            Assert.Equal(0, RadialMath.Segment(0f, -5f, 8, 5f));
            Assert.Equal(2, RadialMath.Segment(5f, 0f, 8, 5f));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-2)]
        public void Fewer_than_one_segment_gives_minus_one(int segments)
        {
            Assert.Equal(-1, RadialMath.Segment(0f, -1f, segments, 0.1f));
        }

        [Fact]
        public void A_single_segment_covers_every_direction()
        {
            Assert.Equal(0, RadialMath.Segment(1f, 0f, 1, 0.1f));
            Assert.Equal(0, RadialMath.Segment(0f, 1f, 1, 0.1f));
        }

        [Theory]
        [InlineData(0, 8, 0f)]
        [InlineData(2, 8, 90f)]
        [InlineData(3, 4, 270f)]
        [InlineData(1, 3, 120f)]
        public void CentreDeg_is_index_times_step(int index, int segments, float expected)
        {
            Assert.Equal(expected, RadialMath.CentreDeg(index, segments), 0.001f);
        }

        [Fact]
        public void Accumulate_adds_below_the_limit()
        {
            float x = 0f, y = 0f;
            RadialMath.Accumulate(ref x, ref y, 3f, 4f, 10f);
            Assert.Equal(3f, x, 0.001f);
            Assert.Equal(4f, y, 0.001f);
            RadialMath.Accumulate(ref x, ref y, 3f, 4f, 10f);
            Assert.Equal(6f, x, 0.001f);
            Assert.Equal(8f, y, 0.001f);
        }

        [Fact]
        public void Accumulate_scales_back_onto_the_circle()
        {
            float x = 6f, y = 8f;
            RadialMath.Accumulate(ref x, ref y, 3f, 4f, 10f); // (9, 12) has length 15
            Assert.Equal(6f, x, 0.001f);
            Assert.Equal(8f, y, 0.001f);

            x = 0f;
            y = 0f;
            RadialMath.Accumulate(ref x, ref y, 30f, 40f, 5f);
            Assert.Equal(3f, x, 0.001f);
            Assert.Equal(4f, y, 0.001f);
        }
    }
}
