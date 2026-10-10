using GtaSkylines.Mod.Menus;
using Xunit;

namespace GtaSkylines.Mod.Tests.Menus
{
    public class MapViewStateTests
    {
        private const float Tol = 0.01f;
        private const float W = 800f, H = 600f;

        private static MapViewState Make()
        {
            return new MapViewState { CentreX = 1000f, CentreZ = 500f, MetresPerPixel = 4f };
        }

        [Fact]
        public void Screen_centre_is_the_view_centre()
        {
            var s = Make();
            float x, z;
            s.ScreenToWorld(W, H, 400f, 300f, out x, out z);
            Assert.Equal(1000f, x, Tol);
            Assert.Equal(500f, z, Tol);
        }

        [Fact]
        public void Screen_right_is_east_and_screen_up_is_north()
        {
            var s = Make();
            float x, z;
            s.ScreenToWorld(W, H, 500f, 300f, out x, out z);
            Assert.Equal(1400f, x, Tol);
            Assert.Equal(500f, z, Tol);
            s.ScreenToWorld(W, H, 400f, 200f, out x, out z);
            Assert.Equal(1000f, x, Tol);
            Assert.Equal(900f, z, Tol);
        }

        [Fact]
        public void World_screen_round_trip()
        {
            var s = Make();
            float x, z, sx, sy;
            s.ScreenToWorld(W, H, 123f, 456f, out x, out z);
            s.WorldToScreen(W, H, x, z, out sx, out sy);
            Assert.Equal(123f, sx, Tol);
            Assert.Equal(456f, sy, Tol);
        }

        [Fact]
        public void Drag_moves_the_point_under_the_cursor_with_it()
        {
            var s = Make();
            float x, z, sx, sy;
            s.ScreenToWorld(W, H, 250f, 180f, out x, out z);
            s.Drag(30f, -20f);
            s.WorldToScreen(W, H, x, z, out sx, out sy);
            Assert.Equal(280f, sx, Tol);
            Assert.Equal(160f, sy, Tol);
        }

        [Fact]
        public void Drag_right_moves_the_centre_west()
        {
            var s = Make();
            s.Drag(10f, 0f);
            Assert.Equal(960f, s.CentreX, Tol);
            Assert.Equal(500f, s.CentreZ, Tol);
        }

        [Fact]
        public void PanKeys_scales_with_zoom_and_time()
        {
            var s = Make();
            s.PanKeys(1f, 0f, W, H, 0.5f);
            Assert.Equal(2600f, s.CentreX, Tol);
            Assert.Equal(500f, s.CentreZ, Tol);
            s.PanKeys(0f, 1f, W, H, 0.5f);
            Assert.Equal(1700f, s.CentreZ, Tol);
        }

        [Theory]
        [InlineData(0.5f, 2f)]
        [InlineData(2f, 8f)]
        [InlineData(0.001f, 0.5f)]
        [InlineData(1000f, 32f)]
        public void Zoom_scales_and_clamps_while_keeping_the_anchor_fixed(float factor, float expectedMpp)
        {
            var s = Make();
            float x0, z0, x1, z1;
            s.ScreenToWorld(W, H, 600f, 100f, out x0, out z0);
            s.Zoom(factor, W, H, 600f, 100f);
            Assert.Equal(expectedMpp, s.MetresPerPixel, Tol);
            s.ScreenToWorld(W, H, 600f, 100f, out x1, out z1);
            Assert.Equal(x0, x1, Tol);
            Assert.Equal(z0, z1, Tol);
        }

        [Fact]
        public void Zoom_limits_are_the_documented_constants()
        {
            Assert.Equal(0.5f, MapViewState.MinMetresPerPixel);
            Assert.Equal(32f, MapViewState.MaxMetresPerPixel);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-2f)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        public void Zoom_ignores_invalid_factors(float factor)
        {
            var s = Make();
            s.Zoom(factor, W, H, 600f, 100f);
            Assert.Equal(4f, s.MetresPerPixel, Tol);
            Assert.Equal(1000f, s.CentreX, Tol);
            Assert.Equal(500f, s.CentreZ, Tol);
        }

        [Fact]
        public void ClampCentre_keeps_the_centre_inside_the_box()
        {
            var s = Make();
            s.CentreX = -50f;
            s.CentreZ = 9000f;
            s.ClampCentre(0f, 0f, 2000f, 1000f);
            Assert.Equal(0f, s.CentreX, Tol);
            Assert.Equal(1000f, s.CentreZ, Tol);

            s.CentreX = 700f;
            s.CentreZ = 300f;
            s.ClampCentre(0f, 0f, 2000f, 1000f);
            Assert.Equal(700f, s.CentreX, Tol);
            Assert.Equal(300f, s.CentreZ, Tol);

            s.CentreX = 5000f;
            s.CentreZ = -5f;
            s.ClampCentre(0f, 0f, 2000f, 1000f);
            Assert.Equal(2000f, s.CentreX, Tol);
            Assert.Equal(0f, s.CentreZ, Tol);
        }
    }
}
