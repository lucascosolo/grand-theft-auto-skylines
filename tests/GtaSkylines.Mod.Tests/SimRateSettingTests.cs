using GtaSkylines.Mod.Lifecycle;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class SimRateSettingTests
    {
        [Theory]
        [InlineData(1, 0.5f)]
        [InlineData(2, 0.75f)]
        [InlineData(3, 1f)]
        public void Multiplier_maps_settings(int setting, float expected)
        {
            Assert.Equal(expected, SimRateSetting.Multiplier(setting));
        }

        [Theory]
        [InlineData(0, 0.5f)]
        [InlineData(-7, 0.5f)]
        [InlineData(4, 1f)]
        [InlineData(100, 1f)]
        public void Multiplier_clamps_out_of_range(int setting, float expected)
        {
            Assert.Equal(expected, SimRateSetting.Multiplier(setting));
        }
    }
}
