using System;
using System.Collections.Generic;
using GtaSkylines.Mod.Hud;
using Xunit;

namespace GtaSkylines.Mod.Tests.Hud
{
    public class MapRasterTests
    {
        private const uint Red = 0xFF0000FF;

        private static int CountSet(MapRaster r)
        {
            int n = 0;
            for (int j = 0; j < r.Pixels; j++)
                for (int i = 0; i < r.Pixels; i++)
                    if (r.Get(i, j) != 0) n++;
            return n;
        }

        private static MapRaster Unit(int pixels)
        {
            return new MapRaster(pixels, 0f, 0f, 1f);
        }

        [Fact]
        public void Constructor_allocates_a_zeroed_square_buffer()
        {
            var r = new MapRaster(8, 100f, 200f, 2f);
            Assert.Equal(8, r.Pixels);
            Assert.Equal(100f, r.MinX);
            Assert.Equal(200f, r.MinZ);
            Assert.Equal(2f, r.MetresPerPixel);
            Assert.Equal(16f, r.Size, 0.001f);
            Assert.Equal(8 * 8 * 4, r.Rgba.Length);
            Assert.All(r.Rgba, b => Assert.Equal(0, b));
        }

        [Theory]
        [InlineData(0, 1f)]
        [InlineData(-3, 1f)]
        [InlineData(4, 0f)]
        [InlineData(4, -1f)]
        [InlineData(4, float.NaN)]
        public void Constructor_rejects_invalid_arguments(int pixels, float mpp)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MapRaster(pixels, 0f, 0f, mpp));
        }

        [Fact]
        public void ForArea_makes_a_centred_square_with_margin()
        {
            var r = MapRaster.ForArea(0f, 0f, 100f, 50f, 10f, 2f, 1000);
            Assert.Equal(60, r.Pixels); // side 120 m at 2 m/px
            Assert.Equal(2f, r.MetresPerPixel, 0.001f);
            Assert.Equal(-10f, r.MinX, 0.001f);
            Assert.Equal(-35f, r.MinZ, 0.001f);
            Assert.Equal(50f, r.MinX + r.Size / 2f, 0.001f);
            Assert.Equal(25f, r.MinZ + r.Size / 2f, 0.001f);
        }

        [Fact]
        public void ForArea_caps_the_pixel_count_by_coarsening()
        {
            var r = MapRaster.ForArea(0f, 0f, 100f, 50f, 10f, 2f, 30);
            Assert.Equal(30, r.Pixels);
            Assert.Equal(4f, r.MetresPerPixel, 0.001f);
            Assert.Equal(-10f, r.MinX, 0.001f);
            Assert.Equal(-35f, r.MinZ, 0.001f);
        }

        [Fact]
        public void ForArea_rounds_the_pixel_count_up()
        {
            var r = MapRaster.ForArea(0f, 0f, 100f, 50f, 10f, 7f, 1000);
            Assert.Equal(18, r.Pixels); // ceil(120 / 7)
        }

        [Fact]
        public void ForArea_has_at_least_one_pixel()
        {
            var r = MapRaster.ForArea(5f, 5f, 5f, 5f, 0f, 1f, 100);
            Assert.Equal(1, r.Pixels);
        }

        [Fact]
        public void Clear_writes_rgba_byte_order()
        {
            var r = Unit(3);
            r.Clear(0x11223344);
            for (int k = 0; k < 9; k++)
            {
                Assert.Equal(0x11, r.Rgba[k * 4]);
                Assert.Equal(0x22, r.Rgba[k * 4 + 1]);
                Assert.Equal(0x33, r.Rgba[k * 4 + 2]);
                Assert.Equal(0x44, r.Rgba[k * 4 + 3]);
            }
            Assert.Equal(0x11223344u, r.Get(2, 2));
        }

        [Fact]
        public void Set_and_get_round_trip_with_row_zero_at_the_south()
        {
            var r = Unit(4);
            r.Set(2, 3, 0xAABBCCDD);
            Assert.Equal(0xAABBCCDDu, r.Get(2, 3));
            int idx = (3 * 4 + 2) * 4;
            Assert.Equal(0xAA, r.Rgba[idx]);
            Assert.Equal(0xBB, r.Rgba[idx + 1]);
            Assert.Equal(0xCC, r.Rgba[idx + 2]);
            Assert.Equal(0xDD, r.Rgba[idx + 3]);
            r.Set(0, 0, 0x01020304);
            Assert.Equal(0x01, r.Rgba[0]);
            Assert.Equal(0x04, r.Rgba[3]);
        }

        [Fact]
        public void Outside_pixels_read_zero_and_writes_are_ignored()
        {
            var r = Unit(4);
            r.Set(-1, 0, Red);
            r.Set(4, 0, Red);
            r.Set(0, -1, Red);
            r.Set(0, 4, Red);
            Assert.Equal(0, CountSet(r));
            r.Clear(Red);
            Assert.Equal(0u, r.Get(-1, 0));
            Assert.Equal(0u, r.Get(4, 0));
            Assert.Equal(0u, r.Get(0, -1));
            Assert.Equal(0u, r.Get(0, 4));
        }

        [Fact]
        public void ToPixel_floors_world_coordinates()
        {
            var r = new MapRaster(10, 100f, 200f, 2f);
            int i, j;
            r.ToPixel(100f, 200f, out i, out j);
            Assert.Equal((0, 0), (i, j));
            r.ToPixel(101.9f, 203.9f, out i, out j);
            Assert.Equal((0, 1), (i, j));
            r.ToPixel(99.9f, 199f, out i, out j);
            Assert.Equal((-1, -1), (i, j));
            r.ToPixel(120f, 220f, out i, out j);
            Assert.Equal((10, 10), (i, j));
        }

        [Fact]
        public void FillCellsWhere_tests_the_cell_centre()
        {
            var r = Unit(10);
            var seen = new List<(float, float)>();
            r.FillCellsWhere((x, z) => { seen.Add((x, z)); return x < 5f; }, 5, Red);
            Assert.Equal(4, seen.Count);
            Assert.Contains((2.5f, 2.5f), seen);
            Assert.Contains((7.5f, 2.5f), seen);
            Assert.Contains((2.5f, 7.5f), seen);
            Assert.Contains((7.5f, 7.5f), seen);
            for (int j = 0; j < 10; j++)
                for (int i = 0; i < 10; i++)
                    Assert.Equal(i < 5 ? Red : 0u, r.Get(i, j));
        }

        [Fact]
        public void FillCellsWhere_partial_last_cell_uses_the_full_square_centre()
        {
            var r = Unit(10);
            // Cells of 4: [0,4) [4,8) [8,10 partial]; full-square centres 2, 6, 10.
            r.FillCellsWhere((x, z) => x > 9.5f, 4, Red);
            for (int j = 0; j < 10; j++)
                for (int i = 0; i < 10; i++)
                    Assert.Equal(i >= 8 ? Red : 0u, r.Get(i, j));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-3)]
        public void FillCellsWhere_cell_size_below_one_counts_as_one(int cell)
        {
            var r = Unit(2);
            var seen = new List<(float, float)>();
            r.FillCellsWhere((x, z) => { seen.Add((x, z)); return true; }, cell, Red);
            Assert.Equal(4, seen.Count);
            Assert.Contains((0.5f, 0.5f), seen);
            Assert.Contains((1.5f, 1.5f), seen);
            Assert.Equal(4, CountSet(r));
        }

        [Fact]
        public void FillRect_axis_aligned_includes_edges()
        {
            var r = Unit(20);
            r.FillRect(10f, 10f, 0f, 4f, 10f, Red);
            Assert.Equal(40, CountSet(r)); // x centres 8.5..11.5, z centres 5.5..14.5
            Assert.Equal(Red, r.Get(8, 5));
            Assert.Equal(Red, r.Get(11, 14));
            Assert.Equal(0u, r.Get(7, 10));
            Assert.Equal(0u, r.Get(10, 4));
            Assert.Equal(0u, r.Get(10, 15));

            var e = Unit(20);
            e.FillRect(10f, 10f, 0f, 3f, 3f, Red); // edges at 8.5 and 11.5 sit exactly on pixel centres
            Assert.Equal(16, CountSet(e));
        }

        [Fact]
        public void FillRect_rotated_ninety_degrees_swaps_the_axes()
        {
            var r = Unit(20);
            r.FillRect(10f, 10f, 90f, 4f, 10f, Red);
            Assert.Equal(40, CountSet(r)); // length now runs along x
            Assert.Equal(Red, r.Get(5, 8));
            Assert.Equal(Red, r.Get(14, 11));
            Assert.Equal(0u, r.Get(8, 5));
            Assert.Equal(0u, r.Get(10, 14));
        }

        [Fact]
        public void FillRect_smaller_than_a_pixel_still_fills_the_centre_pixel()
        {
            var r = Unit(20);
            r.FillRect(10.3f, 10.7f, 0f, 0.1f, 0.1f, Red);
            Assert.Equal(1, CountSet(r));
            Assert.Equal(Red, r.Get(10, 10));
        }

        [Fact]
        public void DrawLine_fills_a_capsule_with_round_caps()
        {
            var r = Unit(20);
            r.DrawLine(2f, 10f, 18f, 10f, 4f, Red);
            for (int j = 8; j <= 11; j++) Assert.Equal(Red, r.Get(10, j));
            Assert.Equal(0u, r.Get(10, 7));
            Assert.Equal(0u, r.Get(10, 12));
            Assert.Equal(Red, r.Get(0, 9));   // centre (0.5, 9.5) is 1.58 m from the end point
            Assert.Equal(0u, r.Get(0, 11));   // centre (0.5, 11.5) is 2.12 m away: a round cap, not a square one
            Assert.Equal(0u, r.Get(0, 7));
        }

        [Fact]
        public void DrawLine_thinner_than_a_pixel_sets_every_pixel_it_passes_through()
        {
            var r = Unit(10);
            r.DrawLine(0.5f, 2.5f, 9.5f, 2.5f, 0.1f, Red);
            for (int i = 0; i < 10; i++) Assert.Equal(Red, r.Get(i, 2));
            Assert.Equal(0u, r.Get(5, 1));
            Assert.Equal(0u, r.Get(5, 3));

            var d = Unit(10);
            d.DrawLine(0.5f, 0.5f, 5.5f, 5.5f, 0.1f, Red);
            for (int k = 0; k <= 5; k++) Assert.Equal(Red, d.Get(k, k));
        }

        [Fact]
        public void Paint_calls_clip_to_the_buffer_without_throwing()
        {
            var r = Unit(10);
            r.DrawLine(-100f, -100f, 500f, 500f, 3f, Red);
            Assert.Equal(Red, r.Get(5, 5));

            var far = Unit(10);
            far.DrawLine(1000f, 1000f, 2000f, 2000f, 3f, Red);
            far.FillRect(-500f, -500f, 45f, 20f, 20f, Red);
            far.FillRect(5f, 5f, 30f, 10000f, 10000f, 0x01020304);
            far.DrawBezier(-50f, -50f, 100f, -50f, 100f, 100f, 300f, 300f, 2f, 5f, Red);
            far.FillCellsWhere((x, z) => true, 3, Red);
            Assert.Equal(100, CountSet(far));
        }

        [Fact]
        public void DrawBezier_with_collinear_controls_matches_a_straight_line()
        {
            var bez = Unit(20);
            bez.DrawBezier(2f, 10f, 7f, 10f, 12f, 10f, 18f, 10f, 2.4f, 3f, Red);
            var line = Unit(20);
            line.DrawLine(2f, 10f, 18f, 10f, 2.4f, Red);
            Assert.Equal(line.Rgba, bez.Rgba);
            Assert.True(CountSet(bez) > 0);
        }

        [Fact]
        public void DrawBezier_degenerate_curve_does_not_throw()
        {
            var r = Unit(10);
            r.DrawBezier(5f, 5f, 5f, 5f, 5f, 5f, 5f, 5f, 2f, 1f, Red);
            Assert.Equal(Red, r.Get(5, 5));
        }

        [Theory]
        [InlineData(1, 1, 3f, false, 6f)]
        [InlineData(2, 2, 3f, false, 12f)]
        [InlineData(1, 0, 3f, false, 6f)]
        [InlineData(2, 2, 4f, false, 16f)]
        [InlineData(3, 3, 0f, false, 18f)]
        [InlineData(3, 3, -2f, false, 18f)]
        [InlineData(3, 3, float.NaN, false, 18f)]
        [InlineData(3, 3, float.PositiveInfinity, false, 18f)]
        [InlineData(4, 4, 5f, true, 3f)]
        [InlineData(0, 0, 3f, true, 3f)]
        public void RoadWidth_follows_the_lane_rules(int fwd, int bwd, float lane, bool ped, float expected)
        {
            Assert.Equal(expected, MapStyle.RoadWidth(fwd, bwd, lane, ped), 0.001f);
        }
    }
}
