using System.Collections.Generic;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;
using Xunit;

namespace GtaSkylines.Game.Tests.Interiors
{
    /// <summary>InteriorNavGrid on hand-made grids.</summary>
    public class InteriorNavGridTests
    {
        private static InteriorNavGrid OpenGrid(float size = 10f)
        {
            var g = new InteriorNavGrid(new Rect2(0f, 0f, size, size));
            FillRect(g, 0f, 0f, size, size, true);
            return g;
        }

        private static void FillRect(InteriorNavGrid g, float x0, float z0, float x1, float z1, bool free)
        {
            for (float x = x0 + 0.125f; x < x1; x += 0.25f)
                for (float z = z0 + 0.125f; z < z1; z += 0.25f)
                    g.Set(x, z, free);
        }

        [Fact]
        public void NewGridCoversTheRectangleAndIsAllBlocked()
        {
            var g = new InteriorNavGrid(new Rect2(-2f, 1f, 3.1f, 5f));
            Assert.Equal(-2f, g.OriginX);
            Assert.Equal(1f, g.OriginZ);
            Assert.InRange(g.CellsX * InteriorNavGrid.CellSize, 5.1f, 5.1f + InteriorNavGrid.CellSize);
            Assert.Equal(16, g.CellsZ);
            Assert.Equal(0, g.FreeCount());
            Assert.False(g.IsFree(0f, 2f));
        }

        [Fact]
        public void SetAndIsFreeWorkPerCell()
        {
            var g = new InteriorNavGrid(new Rect2(-2f, 1f, 2f, 5f));
            g.Set(0.1f, 2.1f, true);
            Assert.True(g.IsFree(0.1f, 2.1f));
            Assert.True(g.IsFree(0.24f, 2.24f), "same 0.25 m cell");
            Assert.False(g.IsFree(0.26f, 2.1f));
            Assert.False(g.IsFree(0.1f, 2.26f));
            Assert.Equal(1, g.FreeCount());
            g.Set(0.2f, 2.2f, true);
            Assert.Equal(1, g.FreeCount());
            g.Set(0.1f, 2.1f, false);
            Assert.False(g.IsFree(0.1f, 2.1f));
            Assert.Equal(0, g.FreeCount());
        }

        [Fact]
        public void OutsideTheGridIsNotFreeAndSetIsANoOp()
        {
            var g = new InteriorNavGrid(new Rect2(0f, 0f, 2f, 2f));
            g.Set(-1f, 1f, true);
            g.Set(1f, 5f, true);
            g.Set(99f, 99f, true);
            Assert.Equal(0, g.FreeCount());
            Assert.False(g.IsFree(-1f, 1f));
            Assert.False(g.IsFree(5f, 5f));
        }

        [Fact]
        public void FreeCountCountsEveryFreeCell()
        {
            var g = OpenGrid(4f);
            Assert.Equal(16 * 16, g.FreeCount());
            FillRect(g, 0f, 0f, 1f, 4f, false);
            Assert.Equal(12 * 16, g.FreeCount());
        }

        [Fact]
        public void NearestFreeReturnsTheClosestFreeCellCentre()
        {
            var g = new InteriorNavGrid(new Rect2(0f, 0f, 10f, 10f));
            g.Set(1.1f, 1.1f, true);
            g.Set(7.6f, 1.1f, true);
            Vec3 free;
            Assert.True(g.NearestFree(new Vec3(1.1f, 0f, 1.1f), 0.1f, out free));
            Assert.Equal(1.125f, free.X, 3);
            Assert.Equal(1.125f, free.Z, 3);
            Assert.True(g.NearestFree(new Vec3(6.5f, 0f, 1.0f), 5f, out free));
            Assert.Equal(7.625f, free.X, 3);
            Assert.Equal(1.125f, free.Z, 3);
            Assert.True(g.NearestFree(new Vec3(1.9f, 0f, 1.1f), 5f, out free));
            Assert.Equal(1.125f, free.X, 3);
        }

        [Fact]
        public void NearestFreeFailsBeyondTheMaximumDistance()
        {
            var g = new InteriorNavGrid(new Rect2(0f, 0f, 10f, 10f));
            g.Set(1.1f, 1.1f, true);
            Vec3 free;
            Assert.False(g.NearestFree(new Vec3(5f, 0f, 5f), 2f, out free));
            Assert.False(new InteriorNavGrid(new Rect2(0f, 0f, 10f, 10f)).NearestFree(new Vec3(5f, 0f, 5f), 100f, out free));
        }

        [Fact]
        public void ConnectedFollowsFreeCells()
        {
            var g = OpenGrid();
            Assert.True(g.Connected(new Vec3(1f, 0f, 1f), new Vec3(9f, 0f, 9f)));
            Assert.True(g.Connected(new Vec3(1f, 0f, 1f), new Vec3(1f, 0f, 1f)));
            FillRect(g, 5f, 0f, 5.5f, 10f, false);
            Assert.False(g.Connected(new Vec3(1f, 0f, 1f), new Vec3(9f, 0f, 9f)));
            FillRect(g, 5f, 4f, 5.5f, 5f, true);
            Assert.True(g.Connected(new Vec3(1f, 0f, 1f), new Vec3(9f, 0f, 9f)));
        }

        [Fact]
        public void ConnectedDoesNotCutCornersBetweenTwoBlockedCells()
        {
            var g = new InteriorNavGrid(new Rect2(0f, 0f, 2f, 2f));
            g.Set(0.1f, 0.1f, true);
            g.Set(0.35f, 0.35f, true);
            Assert.False(g.Connected(new Vec3(0.1f, 0f, 0.1f), new Vec3(0.35f, 0f, 0.35f)));
            g.Set(0.35f, 0.1f, true);
            Assert.True(g.Connected(new Vec3(0.1f, 0f, 0.1f), new Vec3(0.35f, 0f, 0.35f)));
        }

        [Fact]
        public void ConnectedIsFalseOutsideTheGrid()
        {
            var g = OpenGrid();
            Assert.False(g.Connected(new Vec3(1f, 0f, 1f), new Vec3(50f, 0f, 50f)));
        }

        [Fact]
        public void FindPathAcrossOpenFloorIsAShortStraightRun()
        {
            var g = OpenGrid();
            var path = new List<Vec3>();
            Assert.True(g.FindPath(new Vec3(1.1f, 0f, 1.1f), new Vec3(8.9f, 0f, 8.9f), path));
            Assert.InRange(path.Count, 2, 3);
            AssertNear(path[0], 1.125f, 1.125f);
            AssertNear(path[path.Count - 1], 8.875f, 8.875f);
            foreach (Vec3 p in path) Assert.Equal(0f, p.Y);
        }

        [Fact]
        public void FindPathGoesThroughTheGapAndStaysOnFreeCells()
        {
            var g = OpenGrid();
            FillRect(g, 4.5f, 0f, 5.5f, 10f, false);
            FillRect(g, 4.5f, 7.5f, 5.5f, 8.5f, true);
            var path = new List<Vec3>();
            Assert.True(g.FindPath(new Vec3(1f, 0f, 1f), new Vec3(9f, 0f, 1f), path));
            Assert.True(path.Count >= 3);
            bool throughGap = false;
            for (int i = 0; i + 1 < path.Count; i++)
            {
                Vec3 a = path[i], b = path[i + 1];
                Assert.True(g.IsFree(a.X, a.Z));
                float len = Vec3.DistanceXZ(a, b);
                for (float s = 0f; s <= len; s += 0.1f)
                {
                    Vec3 p = Vec3.Lerp(a, b, len < 1e-4f ? 0f : s / len);
                    Assert.True(g.IsFree(p.X, p.Z), "segment leaves free cells at " + p);
                    if (p.X > 4.6f && p.X < 5.4f && p.Z > 7.5f && p.Z < 8.5f) throughGap = true;
                }
            }
            Assert.True(throughGap);
            Assert.True(g.IsFree(path[path.Count - 1].X, path[path.Count - 1].Z));
        }

        [Fact]
        public void FindPathSnapsEndpointsWithinOneMetre()
        {
            var g = new InteriorNavGrid(new Rect2(0f, 0f, 10f, 10f));
            FillRect(g, 3f, 3f, 8f, 8f, true);
            var path = new List<Vec3>();
            Assert.True(g.FindPath(new Vec3(2.5f, 0f, 5f), new Vec3(8.6f, 0f, 5f), path));
            Assert.True(g.IsFree(path[0].X, path[0].Z));
            Assert.True(g.IsFree(path[path.Count - 1].X, path[path.Count - 1].Z));
            Assert.True(Vec3.DistanceXZ(path[0], new Vec3(2.5f, 0f, 5f)) <= 1f);
            Assert.True(Vec3.DistanceXZ(path[path.Count - 1], new Vec3(8.6f, 0f, 5f)) <= 1f);
        }

        [Fact]
        public void FindPathFailsWhenAnEndpointHasNoFreeCellWithinOneMetre()
        {
            var g = new InteriorNavGrid(new Rect2(0f, 0f, 10f, 10f));
            FillRect(g, 3f, 3f, 8f, 8f, true);
            var path = new List<Vec3> { new Vec3(1f, 2f, 3f) };
            Assert.False(g.FindPath(new Vec3(0.5f, 0f, 5f), new Vec3(5f, 0f, 5f), path));
            Assert.Empty(path);
        }

        [Fact]
        public void FindPathFailsBetweenSeparateRegionsAndClearsTheList()
        {
            var g = OpenGrid();
            FillRect(g, 5f, 0f, 5.5f, 10f, false);
            var path = new List<Vec3> { new Vec3(9f, 9f, 9f), new Vec3(8f, 8f, 8f) };
            Assert.False(g.FindPath(new Vec3(1f, 0f, 1f), new Vec3(9f, 0f, 9f), path));
            Assert.Empty(path);
        }

        [Fact]
        public void FindPathClearsTheListOnSuccessAndHandlesStartEqualsEnd()
        {
            var g = OpenGrid();
            var path = new List<Vec3> { new Vec3(99f, 99f, 99f) };
            Assert.True(g.FindPath(new Vec3(2f, 0f, 2f), new Vec3(2f, 0f, 2f), path));
            Assert.NotEmpty(path);
            foreach (Vec3 p in path) Assert.True(p.X < 50f);
            Assert.True(Vec3.DistanceXZ(path[path.Count - 1], new Vec3(2f, 0f, 2f)) <= 0.2f);
        }

        private static void AssertNear(Vec3 p, float x, float z)
        {
            Assert.True(Vec3.DistanceXZ(p, new Vec3(x, 0f, z)) <= 0.2f, "expected near (" + x + "," + z + ") got " + p);
        }
    }
}
