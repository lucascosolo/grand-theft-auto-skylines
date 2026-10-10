using System;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Mod.Rendering;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class MeshShapesTests
    {
        [Fact]
        public void Cylinder_is_an_open_tube()
        {
            var m = MeshShapes.Cylinder(12, 0.5f, 2f);
            MeshAsserts.Invariants(m);
            Assert.Equal(26, m.VertexCount);
            Assert.Equal(24, m.TriangleCount);
            for (int i = 0; i < m.VertexCount; i++)
            {
                var p = MeshAsserts.P(m, i);
                Assert.Equal(0.5f, (float)Math.Sqrt(p.X * p.X + p.Z * p.Z), 4);
                Assert.True(Math.Abs(p.Y) < 1e-4f || Math.Abs(p.Y - 2f) < 1e-4f);
            }
        }

        [Fact]
        public void Ring_is_a_flat_annulus_in_xy()
        {
            var m = MeshShapes.Ring(16, 0.3f, 0.6f);
            MeshAsserts.Invariants(m);
            Assert.Equal(34, m.VertexCount);
            Assert.Equal(32, m.TriangleCount);
            for (int i = 0; i < m.VertexCount; i++)
            {
                var p = MeshAsserts.P(m, i);
                Assert.Equal(0f, p.Z, 4);
                float d = (float)Math.Sqrt(p.X * p.X + p.Y * p.Y);
                Assert.True(Math.Abs(d - 0.3f) < 1e-4f || Math.Abs(d - 0.6f) < 1e-4f, "distance " + d);
            }
        }

        [Fact]
        public void Quad_is_centred_and_faces_minus_z()
        {
            var m = MeshShapes.Quad(2f, 4f);
            MeshAsserts.Invariants(m);
            Assert.Equal(4, m.VertexCount);
            Assert.Equal(2, m.TriangleCount);
            Vec3 min, max;
            MeshAsserts.Bounds(m, out min, out max);
            MeshAsserts.Near(new Vec3(-1, -2, 0), min);
            MeshAsserts.Near(new Vec3(1, 2, 0), max);
            for (int i = 0; i < 4; i++) MeshAsserts.Near(new Vec3(0, 0, -1), MeshAsserts.N(m, i));
            MeshAsserts.Winding(m);
        }

        [Fact]
        public void Arrow_points_plus_z_flat_on_the_ground()
        {
            var m = MeshShapes.Arrow(2f);
            MeshAsserts.Invariants(m);
            Assert.Equal(7, m.VertexCount);
            Assert.Equal(3, m.TriangleCount);
            int tip = 0;
            for (int i = 0; i < m.VertexCount; i++)
            {
                var p = MeshAsserts.P(m, i);
                Assert.Equal(0f, p.Y, 4);
                Assert.True(Math.Abs(p.X) <= 1.0001f && Math.Abs(p.Z) <= 1.0001f);
                MeshAsserts.Near(new Vec3(0, 1, 0), MeshAsserts.N(m, i));
                if (p.Z > MeshAsserts.P(m, tip).Z) tip = i;
            }
            var t = MeshAsserts.P(m, tip);
            Assert.Equal(0f, t.X, 4);
            Assert.Equal(1f, t.Z, 4);
            MeshAsserts.Winding(m);
        }

        [Fact]
        public void Box_has_24_vertices_12_triangles_and_exact_bounds()
        {
            var b = new MeshBuilder();
            b.AddBox(new Vec3(1, 2, 3), new Vec3(2, 4, 6));
            var m = b.ToMesh();
            MeshAsserts.Invariants(m);
            Assert.Equal(24, m.VertexCount);
            Assert.Equal(12, m.TriangleCount);
            Vec3 min, max;
            MeshAsserts.Bounds(m, out min, out max);
            MeshAsserts.Near(new Vec3(0, 0, 0), min);
            MeshAsserts.Near(new Vec3(2, 4, 6), max);
            MeshAsserts.Winding(m);
        }

        [Fact]
        public void Two_boxes_keep_indices_in_range()
        {
            var b = new MeshBuilder();
            b.AddBox(Vec3.Zero, Vec3.One);
            b.AddBox(new Vec3(5, 0, 0), Vec3.One);
            var m = b.ToMesh();
            MeshAsserts.Invariants(m);
            Assert.Equal(48, m.VertexCount);
            Assert.Equal(24, m.TriangleCount);
        }

        [Fact]
        public void CylinderZ_is_closed_centred_and_wound_outward()
        {
            var b = new MeshBuilder();
            b.AddCylinderZ(new Vec3(0, 1, 0), 0.5f, 2f, 16);
            var m = b.ToMesh();
            MeshAsserts.Invariants(m);
            Assert.True(m.TriangleCount >= 32 + 2 * 14);
            Vec3 min, max;
            MeshAsserts.Bounds(m, out min, out max);
            Assert.Equal(-1f, min.Z, 4); Assert.Equal(1f, max.Z, 4);
            Assert.Equal(-0.5f, min.X, 3); Assert.Equal(0.5f, max.X, 3);
            Assert.Equal(0.5f, min.Y, 3); Assert.Equal(1.5f, max.Y, 3);
            MeshAsserts.Winding(m);
        }

        [Fact]
        public void Clear_empties_the_builder()
        {
            var b = new MeshBuilder();
            b.AddBox(Vec3.Zero, Vec3.One);
            b.Clear();
            var m = b.ToMesh();
            Assert.Equal(0, m.VertexCount);
            Assert.Equal(0, m.TriangleCount);
            b.AddBox(Vec3.Zero, Vec3.One);
            Assert.Equal(24, b.ToMesh().VertexCount);
        }
    }
}
