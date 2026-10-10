using System;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Mod.Rendering;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    internal static class MeshAsserts
    {
        public static Vec3 P(SimpleMesh m, int i) { return new Vec3(m.Positions[i * 3], m.Positions[i * 3 + 1], m.Positions[i * 3 + 2]); }
        public static Vec3 N(SimpleMesh m, int i) { return new Vec3(m.Normals[i * 3], m.Normals[i * 3 + 1], m.Normals[i * 3 + 2]); }

        public static void Invariants(SimpleMesh m)
        {
            Assert.Equal(m.Positions.Length, m.Normals.Length);
            Assert.Equal(0, m.Positions.Length % 3);
            Assert.Equal(m.VertexCount * 2, m.Uvs.Length);
            Assert.Equal(0, m.Indices.Length % 3);
            Assert.Equal(m.Indices.Length / 3, m.TriangleCount);
            foreach (int i in m.Indices) Assert.InRange(i, 0, m.VertexCount - 1);
        }

        public static void Winding(SimpleMesh m)
        {
            for (int t = 0; t < m.TriangleCount; t++)
            {
                int a = m.Indices[t * 3], b = m.Indices[t * 3 + 1], c = m.Indices[t * 3 + 2];
                Vec3 pa = P(m, a);
                float d = Vec3.Dot(Vec3.Cross(P(m, b) - pa, P(m, c) - pa), N(m, a));
                Assert.True(d > 0f, "triangle " + t + " winds against its normal (dot=" + d + ")");
            }
        }

        public static void Bounds(SimpleMesh m, out Vec3 min, out Vec3 max)
        {
            min = P(m, 0); max = min;
            for (int i = 1; i < m.VertexCount; i++)
            {
                Vec3 p = P(m, i);
                min = new Vec3(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
                max = new Vec3(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
            }
        }

        public static void Near(Vec3 expected, Vec3 actual, float tol = 1e-4f)
        {
            Assert.InRange(actual.X, expected.X - tol, expected.X + tol);
            Assert.InRange(actual.Y, expected.Y - tol, expected.Y + tol);
            Assert.InRange(actual.Z, expected.Z - tol, expected.Z + tol);
        }
    }
}
