using System;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Mod.Rendering;
using Xunit;

namespace GtaSkylines.Mod.Tests.Rendering
{
    public class HeliMathTests
    {
        [Fact]
        public void Beam_straight_down_points_the_cone_axis_down_with_the_half_angle_radius()
        {
            Quat rot;
            float radius, length;
            Assert.True(HeliMath.Beam(new Vec3(10, 40, 20), new Vec3(10, 0, 20), out rot, out radius, out length));
            Assert.Equal(40f, length, 3);
            Vec3 axis = rot * Vec3.Forward;
            Assert.Equal(-1f, axis.Y, 3);
            Assert.Equal(40f * (float)Math.Tan(HeliMath.BeamHalfAngleDeg * MathUtil.Deg2Rad), radius, 3);
        }

        [Fact]
        public void Beam_to_an_offset_target_points_at_it()
        {
            Quat rot;
            float radius, length;
            Vec3 from = new Vec3(0, 40, 0), to = new Vec3(30, 0, 0);
            Assert.True(HeliMath.Beam(from, to, out rot, out radius, out length));
            Vec3 tip = from + rot * Vec3.Forward * length;
            Assert.True(Vec3.Distance(tip, to) < 0.01f);
        }

        [Fact]
        public void Beam_rejects_a_zero_length_or_non_finite_beam()
        {
            Quat rot;
            float radius, length;
            Assert.False(HeliMath.Beam(Vec3.Zero, new Vec3(0, -0.5f, 0), out rot, out radius, out length));
            Assert.False(HeliMath.Beam(Vec3.Zero, new Vec3(float.NaN, 0, 0), out rot, out radius, out length));
        }

        [Fact]
        public void Tilt_follows_forward_and_right_speed_and_is_clamped()
        {
            float lean, nod;
            HeliMath.Tilt(new Vec3(0, 0, 10), 0f, out lean, out nod);
            Assert.True(nod > 0f);
            Assert.Equal(0f, lean, 4);
            HeliMath.Tilt(new Vec3(10, 0, 0), 0f, out lean, out nod);
            Assert.True(lean > 0f);
            HeliMath.Tilt(new Vec3(10, 0, 0), 90f, out lean, out nod);
            Assert.True(nod > 0f);
            Assert.Equal(0f, lean, 4);
            HeliMath.Tilt(new Vec3(0, 0, 1000), 0f, out lean, out nod);
            Assert.Equal(HeliMath.MaxTilt, nod);
        }

        [Fact]
        public void Cone_is_an_apex_at_the_origin_opening_to_unit_radius_at_z_one()
        {
            SimpleMesh m = MeshShapes.Cone(16);
            MeshAsserts.Invariants(m);
            Assert.Equal(16, m.TriangleCount);
            for (int i = 0; i < m.VertexCount; i++)
            {
                Vec3 p = MeshAsserts.P(m, i);
                bool apex = Math.Abs(p.X) + Math.Abs(p.Y) + Math.Abs(p.Z) < 1e-4f;
                bool rim = Math.Abs(p.Z - 1f) < 1e-4f && Math.Abs(Math.Sqrt(p.X * p.X + p.Y * p.Y) - 1.0) < 1e-4;
                Assert.True(apex || rim);
            }
        }
    }
}
