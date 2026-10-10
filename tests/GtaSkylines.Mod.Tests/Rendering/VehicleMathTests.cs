using GtaSkylines.Game.Mathx;
using GtaSkylines.Mod.Rendering;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class VehicleMathTests
    {
        [Fact]
        public void Origin_and_centre_round_trip_with_yaw_and_pitch()
        {
            var rot = Quat.Euler(15f, 70f, 0f);
            var offset = new Vec3(0.2f, 0.9f, 1.4f);
            var origin = new Vec3(10f, 2f, -5f);
            var centre = VehicleMath.BoundsCentreFromOrigin(origin, rot, offset);
            MeshAsserts.Near(origin + rot * offset, centre);
            MeshAsserts.Near(origin, VehicleMath.OriginFromBoundsCentre(centre, rot, offset));
        }

        [Fact]
        public void Origin_from_centre_unrotated_subtracts_offset()
        {
            MeshAsserts.Near(new Vec3(1, 1, 1), VehicleMath.OriginFromBoundsCentre(new Vec3(2, 3, 4), Quat.Identity, new Vec3(1, 2, 3)));
        }

        [Fact]
        public void Equal_compressions_only_heave()
        {
            MeshAsserts.Near(new Vec3(0, -0.1f, 0), VehicleMath.SwayFromWheels(0.1f, 0.1f, 0.1f, 0.1f, 1.6f, 2.6f));
        }

        [Fact]
        public void Left_side_compressed_gives_negative_roll()
        {
            var s = VehicleMath.SwayFromWheels(0.2f, 0f, 0.2f, 0f, 2f, 3f);
            Assert.Equal(-(float)System.Math.Atan(0.2 / 2.0), s.X, 4);
            Assert.Equal(-0.1f, s.Y, 4);
            Assert.Equal(0f, s.Z, 4);
        }

        [Fact]
        public void Front_compressed_gives_positive_pitch()
        {
            var s = VehicleMath.SwayFromWheels(0.3f, 0.3f, 0f, 0f, 2f, 3f);
            Assert.Equal(0f, s.X, 4);
            Assert.Equal(-0.15f, s.Y, 4);
            Assert.Equal((float)System.Math.Atan(0.3 / 3.0), s.Z, 4);
        }

        [Fact]
        public void Tiny_track_and_wheelbase_are_floored_at_one_centimetre()
        {
            var s = VehicleMath.SwayFromWheels(0.1f, 0f, 0.1f, 0f, 0f, -3f);
            Assert.Equal(-(float)System.Math.Atan(0.1 / 0.01), s.X, 4);
            var t = VehicleMath.SwayFromWheels(0.1f, 0.1f, 0f, 0f, 1f, 0f);
            Assert.Equal((float)System.Math.Atan(0.1 / 0.01), t.Z, 4);
        }

        [Theory]
        [InlineData(false, false, 0, false, 0f, 0f, 0f, 0f)]
        [InlineData(true, false, 0, false, 5f, 0.5f, 0f, 0f)]
        [InlineData(true, true, 0, false, 5f, 5f, 0f, 0f)]
        [InlineData(false, true, 0, false, 0f, 5f, 0f, 0f)]
        [InlineData(false, false, -1, true, 0f, 0f, 5f, 0f)]
        [InlineData(false, false, -1, false, 0f, 0f, 0f, 0f)]
        [InlineData(false, false, 1, true, 0f, 0f, 0f, 5f)]
        [InlineData(false, false, 1, false, 0f, 0f, 0f, 0f)]
        [InlineData(false, false, 0, true, 0f, 0f, 0f, 0f)]
        public void LightState_packs_lights(bool head, bool brake, int blinker, bool phase, float x, float y, float z, float w)
        {
            float ox, oy, oz, ow;
            VehicleMath.LightState(head, brake, blinker, phase, out ox, out oy, out oz, out ow);
            Assert.Equal(x, ox, 4); Assert.Equal(y, oy, 4); Assert.Equal(z, oz, 4); Assert.Equal(w, ow, 4);
        }

        [Theory]
        [InlineData(0xFFFFFF, 0x262626)]
        [InlineData(0x000000, 0x000000)]
        [InlineData(0xFF0000, 0x260000)]
        [InlineData(0x00C800, 0x001E00)]
        [InlineData(0x000064, 0x00000F)]
        public void ExplodedColour_darkens_each_channel(int rgb, int expected)
        {
            Assert.Equal(expected, VehicleMath.ExplodedColour(rgb));
        }

        [Fact]
        public void WheelTravel_accumulates_and_reverses()
        {
            Assert.Equal(12f, VehicleMath.WheelTravel(10f, 10f, 0.2f), 3);
            Assert.Equal(8f, VehicleMath.WheelTravel(10f, -10f, 0.2f), 3);
        }

        [Fact]
        public void WheelTravel_wraps_into_zero_to_thousand()
        {
            Assert.Equal(5f, VehicleMath.WheelTravel(995f, 10f, 1f), 3);
            Assert.Equal(995f, VehicleMath.WheelTravel(5f, -10f, 1f), 3);
            Assert.InRange(VehicleMath.WheelTravel(0f, 25000f, 1f), 0f, 999.999f);
            Assert.InRange(VehicleMath.WheelTravel(0f, -25000f, 1f), 0f, 999.999f);
        }
    }
}
