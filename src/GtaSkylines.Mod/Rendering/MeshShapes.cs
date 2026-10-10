using System;
using System.Collections.Generic;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Mod.Rendering
{
    /// <summary>Plain mesh arrays (positions, normals: 3 floats; uvs: 2 floats; 3 indices per triangle).</summary>
    public sealed class SimpleMesh
    {
        /// <summary>Vertex positions.</summary>
        public float[] Positions = new float[0];
        /// <summary>Vertex normals.</summary>
        public float[] Normals = new float[0];
        /// <summary>Texture coordinates.</summary>
        public float[] Uvs = new float[0];
        /// <summary>Triangle indices.</summary>
        public int[] Indices = new int[0];

        /// <summary>Number of vertices.</summary>
        public int VertexCount { get { return Positions.Length / 3; } }

        /// <summary>Number of triangles.</summary>
        public int TriangleCount { get { return Indices.Length / 3; } }
    }

    /// <summary>
    /// Accumulates triangles with Unity's front-face winding (<c>Cross(b − a, c − a)</c> along the normal of the first
    /// vertex; every triangle is oriented by that rule, so callers never reason about winding).
    /// </summary>
    public sealed class MeshBuilder
    {
        private readonly List<float> _p = new List<float>();
        private readonly List<float> _n = new List<float>();
        private readonly List<float> _uv = new List<float>();
        private readonly List<int> _i = new List<int>();

        /// <summary>Vertices so far.</summary>
        public int VertexCount { get { return _p.Count / 3; } }

        /// <summary>Adds a vertex and returns its index.</summary>
        public int Vertex(Vec3 p, Vec3 n, float u, float v)
        {
            _p.Add(p.X); _p.Add(p.Y); _p.Add(p.Z);
            _n.Add(n.X); _n.Add(n.Y); _n.Add(n.Z);
            _uv.Add(u); _uv.Add(v);
            return VertexCount - 1;
        }

        /// <summary>Adds a triangle facing the normal of vertex <paramref name="a"/>.</summary>
        public void Triangle(int a, int b, int c)
        {
            Vec3 pa = At(_p, a), pb = At(_p, b), pc = At(_p, c);
            if (Vec3.Dot(Vec3.Cross(pb - pa, pc - pa), At(_n, a)) < 0f)
            {
                int t = b; b = c; c = t;
            }
            _i.Add(a); _i.Add(b); _i.Add(c);
        }

        /// <summary>A flat quad centred on <paramref name="c"/> spanning ±<paramref name="u"/>, ±<paramref name="v"/>, facing <paramref name="n"/>.</summary>
        public void Quad(Vec3 c, Vec3 u, Vec3 v, Vec3 n)
        {
            int a = Vertex(c - u - v, n, 0f, 0f);
            int b = Vertex(c - u + v, n, 0f, 1f);
            int d = Vertex(c + u + v, n, 1f, 1f);
            int e = Vertex(c + u - v, n, 1f, 0f);
            Triangle(a, b, d);
            Triangle(a, d, e);
        }

        /// <summary>An axis-aligned box: 24 vertices, 12 triangles, outward normals.</summary>
        public void AddBox(Vec3 centre, Vec3 size)
        {
            var hx = new Vec3(size.X * 0.5f, 0f, 0f);
            var hy = new Vec3(0f, size.Y * 0.5f, 0f);
            var hz = new Vec3(0f, 0f, size.Z * 0.5f);
            Quad(centre + hx, hz, hy, Vec3.Right);
            Quad(centre - hx, hz, hy, -Vec3.Right);
            Quad(centre + hy, hx, hz, Vec3.Up);
            Quad(centre - hy, hx, hz, -Vec3.Up);
            Quad(centre + hz, hx, hy, Vec3.Forward);
            Quad(centre - hz, hx, hy, -Vec3.Forward);
        }

        /// <summary>A closed cylinder along +z centred on <paramref name="centre"/>.</summary>
        public void AddCylinderZ(Vec3 centre, float radius, float length, int segments)
        {
            segments = Math.Max(3, segments);
            float hz = length * 0.5f;
            int side = VertexCount;
            for (int i = 0; i <= segments; i++)
            {
                double a = 2.0 * Math.PI * i / segments;
                var n = new Vec3((float)Math.Cos(a), (float)Math.Sin(a), 0f);
                Vertex(centre + n * radius + new Vec3(0f, 0f, -hz), n, i / (float)segments, 0f);
                Vertex(centre + n * radius + new Vec3(0f, 0f, hz), n, i / (float)segments, 1f);
            }
            for (int i = 0; i < segments; i++)
            {
                int b0 = side + i * 2, t0 = b0 + 1, b1 = b0 + 2, t1 = b0 + 3;
                Triangle(b0, t0, t1);
                Triangle(b0, t1, b1);
            }
            for (int s = -1; s <= 1; s += 2)
            {
                var n = new Vec3(0f, 0f, s);
                int c = Vertex(centre + n * hz, n, 0.5f, 0.5f);
                int first = VertexCount;
                for (int i = 0; i < segments; i++)
                {
                    double a = 2.0 * Math.PI * i / segments;
                    var r = new Vec3((float)Math.Cos(a), (float)Math.Sin(a), 0f);
                    Vertex(centre + r * radius + n * hz, n, 0.5f + r.X * 0.5f, 0.5f + r.Y * 0.5f);
                }
                for (int i = 0; i < segments; i++) Triangle(c, first + i, first + (i + 1) % segments);
            }
        }

        /// <summary>The accumulated mesh (new arrays).</summary>
        public SimpleMesh ToMesh()
        {
            return new SimpleMesh { Positions = _p.ToArray(), Normals = _n.ToArray(), Uvs = _uv.ToArray(), Indices = _i.ToArray() };
        }

        /// <summary>Starts over.</summary>
        public void Clear()
        {
            _p.Clear(); _n.Clear(); _uv.Clear(); _i.Clear();
        }

        private static Vec3 At(List<float> a, int i)
        {
            return new Vec3(a[i * 3], a[i * 3 + 1], a[i * 3 + 2]);
        }
    }

    /// <summary>Marker and billboard meshes.</summary>
    public static class MeshShapes
    {
        /// <summary>Open tube around +y from y = 0 to <paramref name="height"/>, normals outward.</summary>
        public static SimpleMesh Cylinder(int segments, float radius, float height)
        {
            segments = Math.Max(3, segments);
            var b = new MeshBuilder();
            for (int i = 0; i <= segments; i++)
            {
                double a = 2.0 * Math.PI * i / segments;
                var n = new Vec3((float)Math.Sin(a), 0f, (float)Math.Cos(a));
                b.Vertex(n * radius, n, i / (float)segments, 0f);
                b.Vertex(n * radius + new Vec3(0f, height, 0f), n, i / (float)segments, 1f);
            }
            for (int i = 0; i < segments; i++)
            {
                int b0 = i * 2;
                b.Triangle(b0, b0 + 1, b0 + 3);
                b.Triangle(b0, b0 + 3, b0 + 2);
            }
            return b.ToMesh();
        }

        /// <summary>Open cone: apex at the origin, opening along +z to radius 1 at z = 1 (scale x, y by the radius and z by the length).</summary>
        public static SimpleMesh Cone(int segments)
        {
            segments = Math.Max(3, segments);
            var b = new MeshBuilder();
            for (int i = 0; i <= segments; i++)
            {
                double a = 2.0 * Math.PI * i / segments;
                var d = new Vec3((float)Math.Sin(a), (float)Math.Cos(a), 0f);
                b.Vertex(Vec3.Zero, d, i / (float)segments, 0f);
                b.Vertex(d + new Vec3(0f, 0f, 1f), d, i / (float)segments, 1f);
            }
            for (int i = 0; i < segments; i++) b.Triangle(i * 2, i * 2 + 1, i * 2 + 3);
            return b.ToMesh();
        }

        /// <summary>Flat annulus in the x-y plane (z = 0), facing −z.</summary>
        public static SimpleMesh Ring(int segments, float innerRadius, float outerRadius)
        {
            segments = Math.Max(3, segments);
            var b = new MeshBuilder();
            var n = new Vec3(0f, 0f, -1f);
            for (int i = 0; i <= segments; i++)
            {
                double a = 2.0 * Math.PI * i / segments;
                var d = new Vec3((float)Math.Cos(a), (float)Math.Sin(a), 0f);
                b.Vertex(d * innerRadius, n, i / (float)segments, 0f);
                b.Vertex(d * outerRadius, n, i / (float)segments, 1f);
            }
            for (int i = 0; i < segments; i++)
            {
                int b0 = i * 2;
                b.Triangle(b0, b0 + 1, b0 + 3);
                b.Triangle(b0, b0 + 3, b0 + 2);
            }
            return b.ToMesh();
        }

        /// <summary>Quad in the x-y plane centred on the origin, facing −z.</summary>
        public static SimpleMesh Quad(float width, float height)
        {
            var b = new MeshBuilder();
            b.Quad(Vec3.Zero, new Vec3(width * 0.5f, 0f, 0f), new Vec3(0f, height * 0.5f, 0f), new Vec3(0f, 0f, -1f));
            return b.ToMesh();
        }

        /// <summary>Flat arrow on the x-z plane pointing +z, facing up.</summary>
        public static SimpleMesh Arrow(float size)
        {
            var b = new MeshBuilder();
            float h = size * 0.5f, w = size / 8f;
            Vec3 n = Vec3.Up;
            int s0 = b.Vertex(new Vec3(-w, 0f, -h), n, 0f, 0f);
            int s1 = b.Vertex(new Vec3(-w, 0f, 0f), n, 0f, 0.5f);
            int s2 = b.Vertex(new Vec3(w, 0f, 0f), n, 1f, 0.5f);
            int s3 = b.Vertex(new Vec3(w, 0f, -h), n, 1f, 0f);
            int h0 = b.Vertex(new Vec3(-h, 0f, 0f), n, 0f, 0.5f);
            int h1 = b.Vertex(new Vec3(0f, 0f, h), n, 0.5f, 1f);
            int h2 = b.Vertex(new Vec3(h, 0f, 0f), n, 1f, 0.5f);
            b.Triangle(s0, s1, s2);
            b.Triangle(s0, s2, s3);
            b.Triangle(h0, h1, h2);
            return b.ToMesh();
        }
    }
}
