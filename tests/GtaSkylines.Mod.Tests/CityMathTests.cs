using GtaSkylines.Mod.City;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class CityMathTests
    {
        [Theory]
        [InlineData(1f, 13.8889f)]
        [InlineData(2f, 27.7778f)]
        [InlineData(0.5f, 6.9444f)]
        public void SpeedLimitMs_converts_cs1_units(float cs1, float expected)
        {
            Assert.Equal(expected, CityMath.SpeedLimitMs(cs1), 3);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        [InlineData(float.NegativeInfinity)]
        public void SpeedLimitMs_invalid_is_zero(float cs1)
        {
            Assert.Equal(0f, CityMath.SpeedLimitMs(cs1));
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(15, 13)]
        [InlineData(45, 20)]
        [InlineData(90, 30)]
        [InlineData(180, 65)]
        [InlineData(255, 95)]
        public void AgeYears_hits_anchors(int units, int years)
        {
            Assert.Equal(years, CityMath.AgeYears(units));
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(30, 17)]   // 16.5 rounds away from zero
        [InlineData(135, 48)]  // 47.5 rounds away from zero
        public void AgeYears_interpolates_and_rounds_half_away(int units, int years)
        {
            Assert.Equal(years, CityMath.AgeYears(units));
        }

        [Theory]
        [InlineData(-10, 0)]
        [InlineData(300, 95)]
        public void AgeYears_clamps_input(int units, int years)
        {
            Assert.Equal(years, CityMath.AgeYears(units));
        }

        [Fact]
        public void AgeYears_is_monotone()
        {
            int prev = CityMath.AgeYears(0);
            for (int u = 1; u <= 255; u++)
            {
                int cur = CityMath.AgeYears(u);
                Assert.True(cur >= prev, "decreased at " + u);
                prev = cur;
            }
        }

        [Theory]
        [InlineData("Child", 0)]
        [InlineData("Teen 0", 1)]
        [InlineData("Teen", 1)]
        [InlineData("Young", 2)]
        [InlineData("Adult", 3)]
        [InlineData("Senior", 4)]
        [InlineData(null, 3)]
        [InlineData("Whatever", 3)]
        public void AgeGroupOfPhase_maps_names(string name, int group)
        {
            Assert.Equal(group, CityMath.AgeGroupOfPhase(name));
        }

        [Theory]
        [InlineData(24f, 65536u)]
        [InlineData(6f, 16384u)]
        [InlineData(0.5f, 1365u)]
        [InlineData(0f, 0u)]
        [InlineData(-3f, 0u)]
        [InlineData(float.NaN, 0u)]
        [InlineData(float.PositiveInfinity, 0u)]
        [InlineData(720f, 1966080u)]
        [InlineData(100000f, 1966080u)]
        public void HoursToDayFrames_converts_and_clamps(float hours, uint frames)
        {
            Assert.Equal(frames, CityMath.HoursToDayFrames(hours));
        }

        [Theory]
        [InlineData(0f, 0f)]
        [InlineData(90f, 90f)]
        [InlineData(360f, 0f)]
        [InlineData(-90f, 270f)]
        [InlineData(725f, 5f)]
        [InlineData(-725f, 355f)]
        public void NormalizeYawDeg_wraps_into_range(float deg, float expected)
        {
            float r = CityMath.NormalizeYawDeg(deg);
            Assert.InRange(r, 0f, 359.9999f);
            Assert.Equal(expected, r, 3);
        }
    }
}
