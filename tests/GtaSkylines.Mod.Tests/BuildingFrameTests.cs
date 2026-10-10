using System;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Mod.City;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class BuildingFrameTests
    {
        private static void AssertVec(Vec3 expected, Vec3 actual)
        {
            Assert.Equal(expected.X, actual.X, 3);
            Assert.Equal(expected.Y, actual.Y, 3);
            Assert.Equal(expected.Z, actual.Z, 3);
        }

        private static Vec3 Forward(float yawRad) { return new Vec3((float)Math.Sin(yawRad), 0f, (float)Math.Cos(yawRad)); }
        private static Vec3 Right(float yawRad) { return new Vec3((float)Math.Cos(yawRad), 0f, -(float)Math.Sin(yawRad)); }

        [Fact]
        public void ToWorld_angle_zero_has_local_z_pointing_back()
        {
            AssertVec(new Vec3(11f, 7f, 15f), BuildingFrame.ToWorld(new Vec3(10f, 5f, 20f), 0f, new Vec3(1f, 2f, 5f)));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1.0)]
        [InlineData(-2.5)]
        [InlineData(1.5707963267948966)]
        public void ToWorld_matches_cs1_calculate_position(double a)
        {
            float ang = (float)a;
            var o = new Vec3(100f, 3f, -40f);
            var l = new Vec3(2f, 1.5f, -7f);
            float xAx = (float)Math.Cos(ang), xAz = (float)Math.Sin(ang);
            float zAx = (float)Math.Sin(ang), zAz = -(float)Math.Cos(ang);
            var expected = new Vec3(o.X + xAx * l.X + zAx * l.Z, o.Y + l.Y, o.Z + xAz * l.X + zAz * l.Z);
            AssertVec(expected, BuildingFrame.ToWorld(o, ang, l));
        }

        [Theory]
        [InlineData(0.0, 0f)]
        [InlineData(1.5707963267948966, 270f)]
        [InlineData(-1.5707963267948966, 90f)]
        [InlineData(3.141592653589793, 180f)]
        public void YawDeg_is_negated_angle_normalized(double a, float expected)
        {
            Assert.Equal(expected, BuildingFrame.YawDeg((float)a), 3);
        }

        [Theory]
        [InlineData(4, 4, true, 0f)]
        [InlineData(6, 4, true, 8f)]
        [InlineData(6, 4, false, -8f)]
        [InlineData(3, 4, true, -4f)]
        [InlineData(3, 4, false, 4f)]
        public void LocalMeshOffset_scales_cells_and_yard(int len, int infoLen, bool expand, float expected)
        {
            Assert.Equal(expected, BuildingFrame.LocalMeshOffset(len, infoLen, expand), 3);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1.5707963267948966)]
        [InlineData(1.0)]
        [InlineData(-2.5)]
        public void DoorWorld_puts_plus_z_toward_building_front(double a)
        {
            float ang = (float)a;
            var o = new Vec3(50f, 2f, -30f);
            var p = new Vec3(1.5f, 0.5f, 6f);
            float mesh = 4f;
            float yaw = BuildingFrame.YawDeg(ang) * (float)Math.PI / 180f;
            Vec3 f = Forward(yaw), r = Right(yaw);
            float k = p.Z - mesh;
            var expected = new Vec3(o.X + r.X * p.X + f.X * k, o.Y + p.Y, o.Z + r.Z * p.X + f.Z * k);
            AssertVec(expected, BuildingFrame.DoorWorld(o, ang, mesh, p));
            AssertVec(BuildingFrame.ToWorld(o, ang, new Vec3(p.X, p.Y, mesh - p.Z)), BuildingFrame.DoorWorld(o, ang, mesh, p));
        }

        [Fact]
        public void DoorWorld_angle_zero_front_is_plus_z()
        {
            AssertVec(new Vec3(0f, 0f, 8f), BuildingFrame.DoorWorld(Vec3.Zero, 0f, 0f, new Vec3(0f, 0f, 8f)));
        }

        [Fact]
        public void FrontCenter_angle_zero_is_plus_z()
        {
            AssertVec(new Vec3(10f, 5f, 28f), BuildingFrame.FrontCenter(new Vec3(10f, 5f, 20f), 0f, 2));
        }

        [Theory]
        [InlineData(1.0)]
        [InlineData(-2.5)]
        [InlineData(1.5707963267948966)]
        public void FrontCenter_moves_along_forward_by_cells_times_four(double a)
        {
            float ang = (float)a;
            var o = new Vec3(10f, 5f, 20f);
            float yaw = BuildingFrame.YawDeg(ang) * (float)Math.PI / 180f;
            Vec3 f = Forward(yaw);
            AssertVec(new Vec3(o.X + f.X * 12f, 5f, o.Z + f.Z * 12f), BuildingFrame.FrontCenter(o, ang, 3));
        }
    }
}
