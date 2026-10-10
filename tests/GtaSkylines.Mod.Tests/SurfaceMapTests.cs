using GtaSkylines.Game.Physics;
using GtaSkylines.Mod.World;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class SurfaceMapTests
    {
        [Theory]
        [InlineData((ushort)0, SurfaceFlags.None)]
        [InlineData((ushort)1, SurfaceFlags.Terrain)]
        [InlineData((ushort)2, SurfaceFlags.Road)]
        [InlineData((ushort)4, SurfaceFlags.Road | SurfaceFlags.Bridge)]
        [InlineData((ushort)8, SurfaceFlags.Building)]
        [InlineData((ushort)16, SurfaceFlags.Bridge)]
        [InlineData((ushort)32, SurfaceFlags.Road)]
        [InlineData((ushort)64, SurfaceFlags.Tree | SurfaceFlags.NoCamera)]
        [InlineData((ushort)128, SurfaceFlags.Prop | SurfaceFlags.NoCamera)]
        [InlineData((ushort)256, SurfaceFlags.NoCamera)]
        public void ToSurface_single_bits(ushort host, SurfaceFlags expected)
        {
            Assert.Equal(expected, SurfaceMap.ToSurface(host));
        }

        [Fact]
        public void ToSurface_ors_combined_bits()
        {
            Assert.Equal(SurfaceFlags.Terrain | SurfaceFlags.Building, SurfaceMap.ToSurface(1 | 8));
            Assert.Equal(SurfaceFlags.Road | SurfaceFlags.Tree | SurfaceFlags.NoCamera, SurfaceMap.ToSurface(2 | 64));
        }

        [Fact]
        public void ToSurface_ignores_unknown_bits()
        {
            Assert.Equal(SurfaceFlags.None, SurfaceMap.ToSurface(512));
            Assert.Equal(SurfaceFlags.Terrain, SurfaceMap.ToSurface(1 | 1024 | 4096));
        }
    }
}
