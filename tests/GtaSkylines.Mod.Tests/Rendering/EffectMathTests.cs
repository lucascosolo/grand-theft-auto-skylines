using GtaSkylines.Mod.Rendering;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class EffectMathTests
    {
        [Fact]
        public void Lifetimes_are_published()
        {
            Assert.Equal(0.06f, EffectMath.TracerLifetime, 4);
            Assert.Equal(0.5f, EffectMath.ImpactLifetime, 4);
            Assert.Equal(0.4f, EffectMath.FlashLifetime, 4);
            Assert.Equal(4f, EffectMath.SmokeLifetime, 4);
            Assert.Equal(20f, EffectMath.WreckFireLifetime, 4);
        }

        [Theory]
        [InlineData(0f, 1f)]
        [InlineData(0.03f, 0.5f)]
        [InlineData(0.06f, 0f)]
        [InlineData(1f, 0f)]
        [InlineData(-0.01f, 0f)]
        public void TracerAlpha_fades_linearly(float age, float expected)
        {
            Assert.Equal(expected, EffectMath.TracerAlpha(age), 4);
        }

        [Theory]
        [InlineData(0f, 0.15f, 0.8f)]
        [InlineData(0.25f, 0.375f, 0.4f)]
        [InlineData(0.5f, 0f, 0f)]
        [InlineData(2f, 0f, 0f)]
        [InlineData(-0.1f, 0f, 0f)]
        public void Impact_curve(float age, float scale, float alpha)
        {
            float s, a;
            EffectMath.Impact(age, out s, out a);
            Assert.Equal(scale, s, 4); Assert.Equal(alpha, a, 4);
        }

        [Theory]
        [InlineData(0f, 2f, 1f)]
        [InlineData(0.2f, 5f, 0.5f)]
        [InlineData(0.4f, 0f, 0f)]
        [InlineData(-1f, 0f, 0f)]
        public void ExplosionFlash_curve(float age, float scale, float alpha)
        {
            float s, a;
            EffectMath.ExplosionFlash(age, out s, out a);
            Assert.Equal(scale, s, 4); Assert.Equal(alpha, a, 4);
        }

        [Theory]
        [InlineData(0f, 3f, 0.6f)]
        [InlineData(2f, 5.5f, 0.3f)]
        [InlineData(4f, 0f, 0f)]
        [InlineData(-1f, 0f, 0f)]
        public void ExplosionSmoke_curve(float age, float scale, float alpha)
        {
            float s, a;
            EffectMath.ExplosionSmoke(age, out s, out a);
            Assert.Equal(scale, s, 4); Assert.Equal(alpha, a, 4);
        }

        [Theory]
        [InlineData(0f, true)]
        [InlineData(19.99f, true)]
        [InlineData(20f, false)]
        [InlineData(-0.01f, false)]
        public void WreckBurning_window(float s, bool expected)
        {
            Assert.Equal(expected, EffectMath.WreckBurning(s));
        }
    }
}
