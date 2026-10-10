using System;
using System.Collections.Generic;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;
using Xunit;
using static GtaSkylines.Game.Tests.Interiors.InteriorTestSupport;

namespace GtaSkylines.Game.Tests.Interiors
{
    /// <summary>Render meshes, texture recipes and collision triangles.</summary>
    public class InteriorGeometryTests
    {
        private const string Arch = nameof(InteriorTestSupport.AllArchetypes);

        [Theory, MemberData(Arch, MemberType = typeof(InteriorTestSupport))]
        public void RecipesAreFewDistinctAndReferencedInRange(InteriorArchetype a)
        {
            foreach (InteriorLayout l in Layouts(a))
            {
                Assert.InRange(l.Recipes.Count, 1, 32);
                for (int i = 0; i < l.Recipes.Count; i++)
                {
                    Assert.True(l.Recipes[i].RepeatMetres > 0f);
                    for (int j = i + 1; j < l.Recipes.Count; j++)
                        Assert.False(l.Recipes[i].Equals(l.Recipes[j]), "recipes " + i + " and " + j + " are equal");
                }
                Assert.NotEmpty(l.MeshParts);
                foreach (MeshPart m in l.MeshParts) Assert.InRange(m.Recipe, 0, l.Recipes.Count - 1);
            }
        }

        [Theory, MemberData(Arch, MemberType = typeof(InteriorTestSupport))]
        public void MeshPartsAreConsistent(InteriorArchetype a)
        {
            foreach (InteriorLayout l in Layouts(a))
                foreach (MeshPart m in l.MeshParts)
                {
                    int n = m.Positions.Length / 3;
                    Assert.True(n > 0 && n <= 65000, "vertex count " + n);
                    Assert.Equal(0, m.Positions.Length % 3);
                    Assert.Equal(n * 3, m.Normals.Length);
                    Assert.Equal(n * 2, m.Uvs.Length);
                    Assert.True(m.Indices.Length > 0 && m.Indices.Length % 3 == 0);
                    foreach (int idx in m.Indices) Assert.InRange(idx, 0, n - 1);
                    foreach (float f in m.Positions) Assert.True(!float.IsNaN(f) && !float.IsInfinity(f));
                    foreach (float f in m.Uvs) Assert.True(!float.IsNaN(f) && !float.IsInfinity(f));
                    for (int v = 0; v < n; v++)
                    {
                        double len = Math.Sqrt(m.Normals[v * 3] * (double)m.Normals[v * 3] + m.Normals[v * 3 + 1] * (double)m.Normals[v * 3 + 1] +
                                               m.Normals[v * 3 + 2] * (double)m.Normals[v * 3 + 2]);
                        Assert.True(Math.Abs(len - 1.0) <= 0.01, "normal length " + len);
                    }
                }
        }

        [Theory, MemberData(Arch, MemberType = typeof(InteriorTestSupport))]
        public void CollisionTrianglesAreWellFormed(InteriorArchetype a)
        {
            foreach (InteriorLayout l in Layouts(a))
            {
                float[] t = l.CollisionTriangles;
                Assert.NotNull(t);
                Assert.Equal(0, t.Length % 9);
                Assert.True(l.CollisionTriangleCount >= 12, "floor, ceiling and walls need at least 12 triangles");
                Assert.Equal(t.Length / 9, l.CollisionTriangleCount);
                foreach (float f in t) Assert.True(!float.IsNaN(f) && !float.IsInfinity(f));
                for (int k = 0; k < l.CollisionTriangleCount; k++)
                    Assert.True(TriNormal(t, k).Length > 1e-4f, "degenerate collision triangle " + k);
            }
        }

        [Theory, MemberData(Arch, MemberType = typeof(InteriorTestSupport))]
        public void HorizontalRaysFromFreeCellsHitCollisionWithinTheFootprintDiagonal(InteriorArchetype a)
        {
            InteriorLayout[] ls = Layouts(a);
            for (int i = 0; i < ls.Length; i += 5)
            {
                InteriorLayout l = ls[i];
                float diag = (float)Math.Sqrt(l.Width * l.Width + l.Depth * l.Depth) + 0.5f;
                List<Vec3> free = FreeCells(l.Nav);
                int stride = Math.Max(10, free.Count / 150);
                for (int c = 0; c < free.Count; c += stride)
                    for (int dir = 0; dir < 8; dir++)
                    {
                        Vec3 d = Vec3.FromYawDeg(dir * 45f);
                        int tri;
                        float hit = Raycast(l.CollisionTriangles, new Vec3(free[c].X, 1.0f, free[c].Z), d, diag, out tri);
                        Assert.True(hit > 0f, Tag(a, i) + " ray escaped from " + free[c] + " yaw " + dir * 45);
                        Vec3 p = new Vec3(free[c].X, 1.0f, free[c].Z) + d * hit;
                        Assert.True(p.X >= -l.Width / 2f - 0.3f && p.X <= l.Width / 2f + 0.3f && p.Z >= -0.3f && p.Z <= l.Depth + 0.3f, "hit outside footprint " + p);
                    }
            }
        }

        [Theory, MemberData(Arch, MemberType = typeof(InteriorTestSupport))]
        public void VerticalRaysFromFreeCellsHitFloorAndCeiling(InteriorArchetype a)
        {
            InteriorLayout[] ls = Layouts(a);
            for (int i = 0; i < ls.Length; i += 5)
            {
                InteriorLayout l = ls[i];
                List<Vec3> free = FreeCells(l.Nav);
                int stride = Math.Max(10, free.Count / 150);
                for (int c = 0; c < free.Count; c += stride)
                {
                    var o = new Vec3(free[c].X, 1.0f, free[c].Z);
                    int tri;
                    float down = Raycast(l.CollisionTriangles, o, new Vec3(0f, -1f, 0f), 10f, out tri);
                    Assert.True(down > 0f, Tag(a, i) + " no floor under " + free[c]);
                    Assert.True(Near(1.0f - down, 0f, 0.01f), Tag(a, i) + " floor at y=" + (1.0f - down));
                    Assert.True(TriNormal(l.CollisionTriangles, tri).Y > 0f, "floor normal points up");

                    float up = Raycast(l.CollisionTriangles, o, new Vec3(0f, 1f, 0f), 10f, out tri);
                    Assert.True(up > 0f, Tag(a, i) + " no ceiling over " + free[c]);
                    Assert.True(Near(1.0f + up, l.CeilingHeight, 0.01f), Tag(a, i) + " ceiling at y=" + (1.0f + up) + " expected " + l.CeilingHeight);
                    Assert.True(TriNormal(l.CollisionTriangles, tri).Y < 0f, "ceiling normal points down");
                }
            }
        }

        [Theory, MemberData(Arch, MemberType = typeof(InteriorTestSupport))]
        public void EntranceDoorIsClosedInTheCollisionGeometry(InteriorArchetype a)
        {
            foreach (InteriorLayout l in Layouts(a))
            {
                var o = new Vec3(l.EntranceSpawn.X, 1.0f, l.EntranceSpawn.Z);
                int tri;
                float hit = Raycast(l.CollisionTriangles, o, new Vec3(0f, 0f, -1f), 5f, out tri);
                Assert.True(hit > 0f, "street wall behind the entrance spawn must block");
                Assert.True(hit <= l.EntranceSpawn.Z + 0.1f);
                Assert.True(TriNormal(l.CollisionTriangles, tri).Z > 0f, "wall faces into the room");
            }
        }
    }
}
