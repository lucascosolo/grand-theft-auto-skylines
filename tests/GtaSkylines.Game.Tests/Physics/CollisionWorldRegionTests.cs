using System;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using Skylines.Core.Geometry;
using Xunit;

namespace GtaSkylines.Game.Tests.PhysicsTests
{
    public class CollisionWorldRegionTests
    {
        private static readonly Vec3 Down = new Vec3(0, -1, 0);

        [Fact]
        public void NewWorldIsEmptyAndHitsNothing()
        {
            var w = new CollisionWorld();
            PhysicsHit hit;
            Assert.Equal(0, w.RegionCount);
            Assert.Equal(0, w.TriangleCount);
            Assert.Equal(0, w.DynamicCount);
            Assert.False(w.Raycast(new Vec3(0, 5, 0), Down, 100f, QueryMask.All, out hit));
            Assert.Equal(4f, CollisionWorld.CellSize);
        }

        [Fact]
        public void SetRegionCountsRegionsAndTriangles()
        {
            var w = new CollisionWorld();
            new MeshBuilder().Floor(-10, -10, 10, 10, 0).Apply(w, 1);
            new MeshBuilder().Box(new Vec3(0, 0, 0), new Vec3(1, 1, 1)).Apply(w, 2);
            Assert.Equal(2, w.RegionCount);
            Assert.Equal(2 + 12, w.TriangleCount);
            Assert.True(w.HasRegion(1));
            Assert.True(w.HasRegion(2));
            Assert.False(w.HasRegion(3));
        }

        [Fact]
        public void ReplacingARegionKeepsCountAndRemovesOldTriangles()
        {
            var w = new CollisionWorld();
            new MeshBuilder().Floor(-10, -10, 10, 10, 0).Apply(w, 7);
            new MeshBuilder().Floor(50, 50, 60, 60, 3).Apply(w, 7);
            PhysicsHit hit;
            Assert.Equal(1, w.RegionCount);
            Assert.Equal(2, w.TriangleCount);
            Assert.False(w.Raycast(new Vec3(0, 5, 0), Down, 100f, QueryMask.All, out hit));
            Assert.True(w.Raycast(new Vec3(55, 5, 55), Down, 100f, QueryMask.All, out hit));
            T.Near(2f, hit.Distance);
        }

        [Fact]
        public void ReplacingWithFewerTrianglesShrinksTheCount()
        {
            var w = new CollisionWorld();
            new MeshBuilder().Box(Vec3.Zero, Vec3.One).Apply(w, 1);
            new MeshBuilder().Floor(0, 0, 1, 1, 0).Apply(w, 1);
            Assert.Equal(2, w.TriangleCount);
        }

        [Fact]
        public void RemoveRegionReportsWhetherItExisted()
        {
            var w = new CollisionWorld();
            new MeshBuilder().Floor(-10, -10, 10, 10, 0).Apply(w, 1);
            PhysicsHit hit;
            Assert.False(w.RemoveRegion(99));
            Assert.True(w.RemoveRegion(1));
            Assert.False(w.RemoveRegion(1));
            Assert.Equal(0, w.RegionCount);
            Assert.Equal(0, w.TriangleCount);
            Assert.False(w.HasRegion(1));
            Assert.False(w.Raycast(new Vec3(0, 5, 0), Down, 100f, QueryMask.All, out hit));
        }

        [Fact]
        public void OverlappingRegionsAreAUnionAndRemovingOneKeepsTheOther()
        {
            var w = new CollisionWorld();
            new MeshBuilder().Floor(-10, -10, 10, 10, 0).Apply(w, 1);
            new MeshBuilder().Floor(-10, -10, 10, 10, 2).Apply(w, 2);
            PhysicsHit hit;
            Assert.True(w.Raycast(new Vec3(0, 5, 0), Down, 100f, QueryMask.All, out hit));
            T.Near(3f, hit.Distance);
            w.RemoveRegion(2);
            Assert.True(w.Raycast(new Vec3(0, 5, 0), Down, 100f, QueryMask.All, out hit));
            T.Near(5f, hit.Distance);
        }

        [Fact]
        public void EmptyRegionStillCounts()
        {
            var w = new CollisionWorld();
            w.SetRegion(5, new float[0], new ushort[0], 0);
            Assert.Equal(1, w.RegionCount);
            Assert.Equal(0, w.TriangleCount);
            Assert.True(w.HasRegion(5));
        }

        [Fact]
        public void OnlyTheFirstTriangleCountEntriesAreRead()
        {
            var w = new CollisionWorld();
            float[] pos =
            {
                -10, 0, -10, 10, 0, -10, 10, 0, 10, -10, 0, -10, 10, 0, 10, -10, 0, 10,
                -10, 5, -10, 10, 5, -10, 10, 5, 10, -10, 5, -10, 10, 5, 10, -10, 5, 10,
            };
            ushort[] flags = { 1, 1, 1, 1 };
            w.SetRegion(3, pos, flags, 2);
            PhysicsHit hit;
            Assert.Equal(2, w.TriangleCount);
            Assert.True(w.Raycast(new Vec3(0, 10, 0), Down, 100f, QueryMask.All, out hit));
            T.Near(10f, hit.Distance);
        }

        [Fact]
        public void SetRegionCopiesTheArrays()
        {
            var w = new CollisionWorld();
            float[] pos = { -10, 0, -10, 10, 0, -10, 0, 0, 10 };
            ushort[] flags = { (ushort)SurfaceFlags.Road };
            w.SetRegion(1, pos, flags, 1);
            Array.Clear(pos, 0, pos.Length);
            flags[0] = 0;
            PhysicsHit hit;
            Assert.True(w.Raycast(new Vec3(0, 3, 0), Down, 10f, QueryMask.All, out hit));
            Assert.Equal(SurfaceFlags.Road, hit.Flags);
        }

        [Fact]
        public void SetRegionFromTriangleBuffer()
        {
            var w = new CollisionWorld();
            var buf = new TriangleBuffer();
            buf.Add(-10, 0, -10, 10, 0, -10, 0, 0, 10, (ushort)SurfaceFlags.Pavement);
            w.SetRegion(1, buf);
            PhysicsHit hit;
            Assert.Equal(1, w.TriangleCount);
            Assert.True(w.Raycast(new Vec3(0, 3, 0), Down, 10f, QueryMask.All, out hit));
            Assert.Equal(SurfaceFlags.Pavement, hit.Flags);
        }

        [Fact]
        public void InvalidSetRegionArgumentsThrow()
        {
            var w = new CollisionWorld();
            Assert.Throws<ArgumentNullException>(() => w.SetRegion(1, null, new ushort[1], 1));
            Assert.Throws<ArgumentNullException>(() => w.SetRegion(1, new float[9], null, 1));
            Assert.Throws<ArgumentNullException>(() => w.SetRegion(1, (TriangleBuffer)null));
            Assert.Throws<ArgumentOutOfRangeException>(() => w.SetRegion(1, new float[9], new ushort[1], -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => w.SetRegion(1, new float[8], new ushort[1], 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => w.SetRegion(1, new float[9], new ushort[1], 2));
        }

        [Fact]
        public void DegenerateAndNonFiniteTrianglesAreCountedButNeverHit()
        {
            var w = new CollisionWorld();
            var m = new MeshBuilder();
            m.Tri(new Vec3(0, 0, 0), new Vec3(0, 0, 0), new Vec3(0, 0, 0));
            m.Tri(new Vec3(-5, 0, 0), new Vec3(0, 0, 0), new Vec3(5, 0, 0));
            m.Tri(new Vec3(float.NaN, 0, 0), new Vec3(1, 0, 0), new Vec3(0, 0, 1));
            m.Apply(w, 1);
            PhysicsHit hit;
            Assert.Equal(3, w.TriangleCount);
            Assert.False(w.Raycast(new Vec3(0, 3, 0), Down, 10f, QueryMask.All, out hit));
            Assert.False(w.SphereCast(new Vec3(0, 3, 0), 0.5f, Down, 10f, QueryMask.All, out hit));
        }

        [Fact]
        public void ClearRemovesStaticAndDynamic()
        {
            var w = new CollisionWorld();
            new MeshBuilder().Floor(-10, -10, 10, 10, 0).Apply(w, 1);
            w.AddBox(new Vec3(0, 1, 0), new Vec3(1, 1, 1), Quat.Identity, 1);
            w.Clear();
            Assert.Equal(0, w.RegionCount);
            Assert.Equal(0, w.TriangleCount);
            Assert.Equal(0, w.DynamicCount);
        }

        [Fact]
        public void ClearDynamicKeepsStaticGeometry()
        {
            var w = new CollisionWorld();
            new MeshBuilder().Floor(-10, -10, 10, 10, 0).Apply(w, 1);
            w.AddBox(new Vec3(0, 1, 0), new Vec3(1, 1, 1), Quat.Identity, 1);
            w.AddCapsule(new Vec3(5, 0, 5), 0.35f, 1.8f, 2);
            Assert.Equal(2, w.DynamicCount);
            PhysicsHit hit;
            Assert.True(w.Raycast(new Vec3(0, 10, 0), Down, 100f, QueryMask.All, out hit));
            Assert.Equal(HitKind.Box, hit.Kind);
            w.ClearDynamic();
            Assert.Equal(0, w.DynamicCount);
            Assert.Equal(1, w.RegionCount);
            Assert.True(w.Raycast(new Vec3(0, 10, 0), Down, 100f, QueryMask.All, out hit));
            Assert.Equal(HitKind.Static, hit.Kind);
            T.Near(10f, hit.Distance);
        }

        [Fact]
        public void InvalidDynamicShapesThrow()
        {
            var w = new CollisionWorld();
            Assert.Throws<ArgumentOutOfRangeException>(() => w.AddBox(Vec3.Zero, new Vec3(0, 1, 1), Quat.Identity, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => w.AddBox(Vec3.Zero, new Vec3(1, -1, 1), Quat.Identity, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => w.AddBox(Vec3.Zero, new Vec3(1, 1, float.NaN), Quat.Identity, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => w.AddCapsule(Vec3.Zero, 0f, 1.8f, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => w.AddCapsule(Vec3.Zero, 0.5f, 0.9f, 1));
            Assert.Equal(0, w.DynamicCount);
        }

        [Fact]
        public void RegionsFarApartAreEachFoundAndRemovedIndependently()
        {
            var w = new CollisionWorld();
            for (int i = 0; i < 5; i++)
            {
                float o = i * 1000f - 2000f;
                new MeshBuilder().Floor(o - 8, -8, o + 8, 8, i).Apply(w, 100 + i);
            }
            PhysicsHit hit;
            for (int i = 0; i < 5; i++)
            {
                float o = i * 1000f - 2000f;
                Assert.True(w.Raycast(new Vec3(o + 1, 10, 1), Down, 50f, QueryMask.All, out hit));
                T.Near(10f - i, hit.Distance);
            }
            w.RemoveRegion(102);
            Assert.False(w.Raycast(new Vec3(1, 10, 1), Down, 50f, QueryMask.All, out hit));
            Assert.True(w.Raycast(new Vec3(1001, 10, 1), Down, 50f, QueryMask.All, out hit));
        }
    }
}
