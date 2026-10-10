using GtaSkylines.Mod.Hud;
using Xunit;

namespace GtaSkylines.Mod.Tests.Hud
{
    public class HudLayoutTests
    {
        private const float Tol = 0.01f;

        private static void AssertRect(float x, float y, float w, float h, HudRect r)
        {
            Assert.Equal(x, r.X, Tol);
            Assert.Equal(y, r.Y, Tol);
            Assert.Equal(w, r.W, Tol);
            Assert.Equal(h, r.H, Tol);
        }

        [Fact]
        public void Rect_contains_is_half_open()
        {
            var r = new HudRect(10, 20, 30, 40);
            Assert.True(r.Contains(10, 20));
            Assert.False(r.Contains(40, 30));
            Assert.False(r.Contains(20, 60));
        }

        [Fact]
        public void Full_hd_scale_one_matches_the_reference_layout()
        {
            var l = HudLayout.Compute(1920f, 1080f, 1f, false);
            Assert.Equal(1f, l.Scale, Tol);
            Assert.Equal(57.6f, l.SafeX, Tol);
            Assert.Equal(32.4f, l.SafeY, Tol);
            Assert.Equal(9f, l.BarHeight, Tol);
            Assert.Equal(4f, l.BarGap, Tol);

            float mmH = 0.18f * 1080f;
            float mmW = 1.55f * mmH;
            float bottom = 1080f - 32.4f - 4f - 9f;
            AssertRect(57.6f, bottom - mmH, mmW, mmH, l.Minimap);

            AssertRect(57.6f, bottom + 4f, 0.55f * mmW - 2f, 9f, l.HealthBar);
            float armW = 0.45f * mmW - 2f;
            AssertRect(57.6f + mmW - armW, bottom + 4f, armW, 9f, l.ArmourBar);

            float mmTop = bottom - mmH;
            AssertRect(57.6f, mmTop - 4f - 4.5f, mmW, 4.5f, l.BreathBar);
            Assert.Equal(57.6f, l.Notifications.X, Tol);
            Assert.Equal(mmTop - 4f - 4.5f - 4f, l.Notifications.Y, Tol);
            Assert.Equal(mmW, l.Notifications.W, Tol);
            Assert.Equal(0f, l.Notifications.H, Tol);
        }

        [Fact]
        public void Full_hd_anchors()
        {
            var l = HudLayout.Compute(1920f, 1080f, 1f, false);
            Assert.Equal(1920f - 57.6f, l.TopRightX, Tol);
            Assert.Equal(32.4f, l.TopRightY, Tol);
            Assert.Equal(57.6f, l.HelpX, Tol);
            Assert.Equal(32.4f, l.HelpY, Tol);
            Assert.Equal(0.4f * 1920f, l.HelpMaxW, Tol);
            Assert.Equal(1080f - 32.4f - 40f, l.BottomCentreY, Tol);
            Assert.Equal(1920f - 57.6f, l.BottomRightX, Tol);
            Assert.Equal(1080f - 32.4f, l.BottomRightY, Tol);
            Assert.Equal(1920f, l.ScreenW, Tol);
            Assert.Equal(1080f, l.ScreenH, Tol);
        }

        [Fact]
        public void Ultra_hd_doubles_the_scale()
        {
            var l = HudLayout.Compute(3840f, 2160f, 1f, false);
            Assert.Equal(2f, l.Scale, Tol);
            Assert.Equal(115.2f, l.SafeX, Tol);
            Assert.Equal(64.8f, l.SafeY, Tol);
            Assert.Equal(18f, l.BarHeight, Tol);
            Assert.Equal(8f, l.BarGap, Tol);
            Assert.Equal(0.18f * 2160f, l.Minimap.H, Tol);
            Assert.Equal(115.2f, l.Minimap.X, Tol);
            Assert.Equal(2160f - 64.8f - 8f - 18f, l.Minimap.Bottom, Tol);
            Assert.Equal(2160f - 64.8f - 80f, l.BottomCentreY, Tol);
        }

        [Fact]
        public void Hud_scale_scales_elements_but_not_margins()
        {
            var l = HudLayout.Compute(1920f, 1080f, 2f, false);
            Assert.Equal(2f, l.Scale, Tol);
            Assert.Equal(57.6f, l.SafeX, Tol);
            Assert.Equal(32.4f, l.SafeY, Tol);
            Assert.Equal(0.18f * 1080f * 2f, l.Minimap.H, Tol);
            Assert.Equal(18f, l.BarHeight, Tol);
        }

        [Theory]
        [InlineData(3f, 2f)]
        [InlineData(100f, 2f)]
        [InlineData(0.1f, 0.5f)]
        [InlineData(0f, 0.5f)]
        [InlineData(-4f, 0.5f)]
        [InlineData(float.NaN, 1f)]
        [InlineData(float.PositiveInfinity, 1f)]
        [InlineData(float.NegativeInfinity, 1f)]
        public void Hud_scale_is_clamped_and_non_finite_is_one(float given, float effective)
        {
            var a = HudLayout.Compute(1920f, 1080f, given, false);
            var b = HudLayout.Compute(1920f, 1080f, effective, false);
            Assert.Equal(b.Scale, a.Scale, Tol);
            Assert.Equal(b.Minimap.H, a.Minimap.H, Tol);
            Assert.Equal(b.BarHeight, a.BarHeight, Tol);
            Assert.Equal(effective, a.Scale, Tol);
        }

        [Fact]
        public void Big_map_doubles_the_minimap_only()
        {
            var n = HudLayout.Compute(1920f, 1080f, 1f, false);
            var b = HudLayout.Compute(1920f, 1080f, 1f, true);
            Assert.Equal(n.Minimap.H * 2f, b.Minimap.H, Tol);
            Assert.Equal(1.55f * b.Minimap.H, b.Minimap.W, Tol);
            Assert.Equal(n.Minimap.Bottom, b.Minimap.Bottom, Tol);
            Assert.Equal(n.Minimap.X, b.Minimap.X, Tol);
            Assert.Equal(n.Scale, b.Scale, Tol);
            Assert.Equal(n.BarHeight, b.BarHeight, Tol);
            Assert.Equal(b.Minimap.W, b.BreathBar.W, Tol);
            Assert.Equal(b.Minimap.Y - 4f, b.BreathBar.Bottom, Tol);
        }
    }
}
