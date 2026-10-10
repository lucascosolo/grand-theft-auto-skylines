using GtaSkylines.Mod.Hud;
using Xunit;

namespace GtaSkylines.Mod.Tests.Hud
{
    public class MinimapMathTests
    {
        private const float Tol = 0.01f;

        // 200 x 100 view, 50 m radius => exactly 1 pixel per metre; anchor at (100, 62).
        private static MinimapView View(float yaw, float cx = 1000f, float cz = 2000f)
        {
            return new MinimapView
            {
                CentreX = cx, CentreZ = cz, YawDeg = yaw, Radius = 50f, W = 200f, H = 100f, AnchorX = 0.5f, AnchorY = 0.62f,
            };
        }

        [Fact]
        public void Centre_maps_to_the_anchor()
        {
            var v = View(37f);
            float px, py;
            MinimapMath.WorldToView(ref v, 1000f, 2000f, out px, out py);
            Assert.Equal(100f, px, Tol);
            Assert.Equal(62f, py, Tol);
        }

        [Fact]
        public void Yaw_zero_is_north_up()
        {
            var v = View(0f);
            float px, py;
            MinimapMath.WorldToView(ref v, 1000f, 2010f, out px, out py);
            Assert.Equal(100f, px, Tol);
            Assert.Equal(52f, py, Tol);
            MinimapMath.WorldToView(ref v, 1010f, 2000f, out px, out py);
            Assert.Equal(110f, px, Tol);
            Assert.Equal(62f, py, Tol);
        }

        [Fact]
        public void Yaw_ninety_puts_plus_x_screen_up()
        {
            var v = View(90f);
            float px, py;
            MinimapMath.WorldToView(ref v, 1010f, 2000f, out px, out py);
            Assert.Equal(100f, px, Tol);
            Assert.Equal(52f, py, Tol);
        }

        [Fact]
        public void Scale_follows_radius()
        {
            var v = View(0f);
            v.Radius = 25f; // 2 px per metre
            float px, py;
            MinimapMath.WorldToView(ref v, 1005f, 2000f, out px, out py);
            Assert.Equal(110f, px, Tol);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(33f)]
        [InlineData(90f)]
        [InlineData(180f)]
        [InlineData(271f)]
        public void World_view_round_trip(float yaw)
        {
            var v = View(yaw, 1234f, -567f);
            float px, py, x, z;
            MinimapMath.WorldToView(ref v, 1250f, -540f, out px, out py);
            MinimapMath.ViewToWorld(ref v, px, py, out x, out z);
            Assert.Equal(1250f, x, Tol);
            Assert.Equal(-540f, z, Tol);
        }

        [Fact]
        public void Clamp_leaves_inside_points_alone_including_the_edge()
        {
            var v = View(0f);
            float px = 190f, py = 50f;
            Assert.False(MinimapMath.ClampToEdge(ref v, 10f, ref px, ref py));
            Assert.Equal(190f, px, Tol);
            Assert.Equal(50f, py, Tol);
        }

        [Fact]
        public void Clamp_moves_outside_points_along_the_ray_to_the_anchor()
        {
            var v = View(0f);
            float px = 300f, py = 62f;
            Assert.True(MinimapMath.ClampToEdge(ref v, 10f, ref px, ref py));
            Assert.Equal(190f, px, Tol);
            Assert.Equal(62f, py, Tol);

            // Direction (200, 100) from the anchor hits the bottom border (y = 90) first.
            px = 300f;
            py = 162f;
            Assert.True(MinimapMath.ClampToEdge(ref v, 10f, ref px, ref py));
            Assert.Equal(156f, px, Tol);
            Assert.Equal(90f, py, Tol);

            px = 100f;
            py = -50f;
            Assert.True(MinimapMath.ClampToEdge(ref v, 10f, ref px, ref py));
            Assert.Equal(100f, px, Tol);
            Assert.Equal(10f, py, Tol);
        }

        [Theory]
        [InlineData(90f, 0f, 270f)]
        [InlineData(90f, 90f, 0f)]
        [InlineData(0f, 0f, 0f)]
        [InlineData(10f, 350f, 340f)]
        [InlineData(350f, 10f, 20f)]
        [InlineData(0f, 370f, 10f)]
        [InlineData(0f, -30f, 330f)]
        public void ScreenAngle_is_normalised(float viewYaw, float worldYaw, float expected)
        {
            var v = View(viewYaw);
            float a = MinimapMath.ScreenAngle(ref v, worldYaw);
            Assert.InRange(a, 0f, 359.9999f);
            Assert.Equal(expected, a, Tol);
        }

        [Fact]
        public void WorldToTexture_is_a_plain_unclamped_fraction()
        {
            float u, t;
            MinimapMath.WorldToTexture(-100f, 50f, 200f, 0f, 150f, out u, out t);
            Assert.Equal(0.5f, u, Tol);
            Assert.Equal(0.5f, t, Tol);
            MinimapMath.WorldToTexture(-100f, 50f, 200f, -200f, 450f, out u, out t);
            Assert.Equal(-0.5f, u, Tol);
            Assert.Equal(2f, t, Tol);
        }
    }
}
