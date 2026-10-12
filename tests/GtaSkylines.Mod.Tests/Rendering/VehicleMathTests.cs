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

        private static CarDraw Draw(float fl, float fr, float rl, float rr)
        {
            return new CarDraw
            {
                PrevCentre = new Vec3(0f, 1f, 0f), Centre = new Vec3(10f, 1.5f, -4f),
                PrevRotation = Quat.Euler(0f, 10f, 0f), Rotation = Quat.Euler(15f, 70f, 5f),
                CompressionFL = fl, CompressionFR = fr, CompressionRL = rl, CompressionRR = rr,
            };
        }

        [Theory]
        [InlineData(0f, 0f, 0f, 0f)]
        [InlineData(0.1f, 0.1f, 0.1f, 0.1f)]
        [InlineData(0.3f, 0f, 0.2f, 0f)]
        [InlineData(2f, 2f, 2f, 2f)]
        public void RenderPose_sway_is_always_zero(float fl, float fr, float rl, float rr)
        {
            Vec3 origin, sway; Quat rot;
            VehicleMath.RenderPose(Draw(fl, fr, rl, rr), 0.5f, new Vec3(0.2f, 0.9f, 1.4f), out origin, out rot, out sway);
            Assert.Equal(Vec3.Zero, sway);
        }

        [Fact]
        public void RenderPose_at_alpha_one_is_the_body_pose()
        {
            CarDraw c = Draw(0.3f, 0f, 0.2f, 0f);
            var offset = new Vec3(0.2f, 0.9f, 1.4f);
            Vec3 origin, sway; Quat rot;
            VehicleMath.RenderPose(c, 1f, offset, out origin, out rot, out sway);
            MeshAsserts.Near(VehicleMath.OriginFromBoundsCentre(c.Centre, c.Rotation, offset), origin);
            MeshAsserts.Near(c.Rotation * Vec3.Forward, rot * Vec3.Forward);
        }

        [Fact]
        public void RenderPose_at_mid_alpha_interpolates_pose_then_derives_origin()
        {
            CarDraw c = Draw(0f, 0f, 0f, 0f);
            var offset = new Vec3(0.2f, 0.9f, 1.4f);
            Vec3 origin, sway; Quat rot;
            VehicleMath.RenderPose(c, 0.4f, offset, out origin, out rot, out sway);
            Quat wantRot = Quat.Slerp(c.PrevRotation, c.Rotation, 0.4f);
            Vec3 wantCentre = Interp.Position(c.PrevCentre, c.Centre, 0.4f);
            MeshAsserts.Near(VehicleMath.OriginFromBoundsCentre(wantCentre, wantRot, offset), origin);
            MeshAsserts.Near(wantRot * Vec3.Forward, rot * Vec3.Forward);
        }

        [Fact]
        public void RenderPose_clamps_alpha()
        {
            CarDraw c = Draw(0f, 0f, 0f, 0f);
            Vec3 o0, o1, oLow, oHigh, s; Quat r;
            VehicleMath.RenderPose(c, 0f, Vec3.Zero, out o0, out r, out s);
            VehicleMath.RenderPose(c, 1f, Vec3.Zero, out o1, out r, out s);
            VehicleMath.RenderPose(c, -2f, Vec3.Zero, out oLow, out r, out s);
            VehicleMath.RenderPose(c, 5f, Vec3.Zero, out oHigh, out r, out s);
            MeshAsserts.Near(o0, oLow);
            MeshAsserts.Near(o1, oHigh);
        }

        [Theory]
        [InlineData(false, false, CarDamage.Intact, CarDrawFlags.None)]
        [InlineData(false, true, CarDamage.Intact, CarDrawFlags.None)]
        [InlineData(false, true, CarDamage.Smoking, CarDrawFlags.None)]
        [InlineData(false, true, CarDamage.Burning, CarDrawFlags.None)]
        [InlineData(false, true, CarDamage.Exploded, CarDrawFlags.None)]
        [InlineData(false, false, CarDamage.Exploded, CarDrawFlags.None)]
        [InlineData(true, false, CarDamage.Intact, CarDrawFlags.Created | CarDrawFlags.Spawned)]
        [InlineData(true, false, CarDamage.Exploded, CarDrawFlags.Created | CarDrawFlags.Spawned)]
        [InlineData(true, true, CarDamage.Intact, CarDrawFlags.Created | CarDrawFlags.Spawned | CarDrawFlags.Emergency2)]
        [InlineData(true, true, CarDamage.Smoking, CarDrawFlags.Created | CarDrawFlags.Spawned | CarDrawFlags.Emergency2)]
        [InlineData(true, true, CarDamage.Burning, CarDrawFlags.Created | CarDrawFlags.Spawned | CarDrawFlags.Emergency2)]
        [InlineData(true, true, CarDamage.Exploded, CarDrawFlags.Created | CarDrawFlags.Spawned)]
        public void DrawFlags_follow_occupancy_siren_and_damage(bool occupied, bool siren, CarDamage damage, CarDrawFlags expected)
        {
            Assert.Equal(expected, VehicleMath.DrawFlags(occupied, siren, damage));
        }

        [Fact]
        public void CarDrawFlags_values_are_the_cs1_bits()
        {
            Assert.Equal(0, (int)CarDrawFlags.None);
            Assert.Equal(1, (int)CarDrawFlags.Created);
            Assert.Equal(2, (int)CarDrawFlags.Spawned);
            Assert.Equal(4, (int)CarDrawFlags.Emergency2);
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
