using GtaSkylines.Mod.Hud;
using Xunit;

namespace GtaSkylines.Mod.Tests.Hud
{
    public class HudAnimTests
    {
        private const float Tol = 0.001f;

        [Theory]
        [InlineData(0f, 1f)]
        [InlineData(0.25f, 0.5f)]
        [InlineData(0.5f, 0f)]
        [InlineData(0.75f, 0.5f)]
        [InlineData(1f, 1f)]
        public void StarPulse_is_cosine_over_one_second(float t, float expected)
        {
            Assert.Equal(expected, HudAnim.StarPulse(t), Tol);
        }

        [Theory]
        [InlineData(0f, 0f)]
        [InlineData(0.2f, 0.5f)]
        [InlineData(0.4f, 1f)]
        [InlineData(1.5f, 1f)]
        [InlineData(2.8f, 0.5f)]
        [InlineData(3f, 0f)]
        [InlineData(-1f, 0f)]
        [InlineData(5f, 0f)]
        public void Window_ramps_in_holds_and_ramps_out(float age, float expected)
        {
            Assert.Equal(expected, HudAnim.Window(age, 3f, 0.4f), Tol);
        }

        [Fact]
        public void Window_nan_age_is_zero()
        {
            Assert.Equal(0f, HudAnim.Window(float.NaN, 3f, 0.4f), Tol);
        }

        [Theory]
        [InlineData(0.2f, 0.25f)]
        [InlineData(0.5f, 0.625f)]
        [InlineData(0.8f, 0.25f)]
        public void Window_overlapping_ramps_take_the_smaller(float age, float expected)
        {
            Assert.Equal(expected, HudAnim.Window(age, 1f, 0.8f), Tol);
        }

        [Theory]
        [InlineData(0f, 1f)]
        [InlineData(0.5f, 1f)]
        [InlineData(0.99f, 1f)]
        [InlineData(1f, 0f)]
        [InlineData(-0.5f, 0f)]
        public void Window_without_fade_is_one_inside_the_window(float age, float expected)
        {
            Assert.Equal(expected, HudAnim.Window(age, 1f, 0f), Tol);
            Assert.Equal(expected, HudAnim.Window(age, 1f, -2f), Tol);
        }

        [Theory]
        [InlineData(-0.1f, 0f)]
        [InlineData(0f, 0f)]
        [InlineData(0.05f, 0.5f)]
        [InlineData(0.1f, 1f)]
        [InlineData(0.5f, 1f)]
        [InlineData(0.85f, 1f)]
        [InlineData(0.925f, 0.5f)]
        [InlineData(1f, 0f)]
        [InlineData(1.2f, 0f)]
        public void BigScreenAlpha_follows_the_piecewise_curve(float progress, float expected)
        {
            Assert.Equal(expected, HudAnim.BigScreenAlpha(progress), Tol);
        }

        [Theory]
        [InlineData(-1f, 1.4f)]
        [InlineData(0f, 1.4f)]
        [InlineData(0.06f, 1.1f)]
        [InlineData(0.12f, 1f)]
        [InlineData(0.5f, 1f)]
        [InlineData(1f, 1f)]
        [InlineData(2f, 1f)]
        public void BigScreenTitleScale_slams_down_with_quadratic_ease_out(float progress, float expected)
        {
            Assert.Equal(expected, HudAnim.BigScreenTitleScale(progress), Tol);
        }

        [Theory]
        [InlineData(0f, 10f, 5f, 1f, 5f)]
        [InlineData(0f, 10f, 5f, 3f, 10f)]
        [InlineData(10f, 0f, 4f, 1f, 6f)]
        [InlineData(10f, 0f, 4f, 5f, 0f)]
        [InlineData(7f, 7f, 4f, 1f, 7f)]
        public void Approach_never_overshoots(float cur, float target, float rate, float dt, float expected)
        {
            Assert.Equal(expected, HudAnim.Approach(cur, target, rate, dt), Tol);
        }
    }
}
