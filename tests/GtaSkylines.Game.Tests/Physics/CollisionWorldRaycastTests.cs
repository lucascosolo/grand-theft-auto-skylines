using System;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using Xunit;

namespace GtaSkylines.Game.Tests.PhysicsTests
{
    public class CollisionWorldRaycastTests
    {
        private static readonly Vec3 Down = new Vec3(0, -1, 0);
        private static readonly Vec3 Up = new Vec3(0, 1, 0);

        private static CollisionWorld Floor(SurfaceFlags f = SurfaceFlags.Terrain, float y = 0f)
        {
            return new MeshBuilder().Floor(-10, -10, 10, 10, y, f).ToWorld();
        }

        [Fact]
        public void HitsFloorWithPointNormalDistanceFlagsAndKind()
        {
            var w = Floor(SurfaceFlags.Road | SurfaceFlags.Bridge);
            PhysicsHit hit;
            Assert.True(w.Raycast(new Vec3(1, 5, 2), Down, 100f, QueryMask.All, out hit));
            T.Near(new Vec3(1, 0, 2), hit.Point);
            T.Near(Up, hit.Normal);
            T.Near(5f, hit.Distance);
            Assert.Equal(SurfaceFlags.Road | SurfaceFlags.Bridge, hit.Flags);
            Assert.Equal(HitKind.Static, hit.Kind);
            Assert.Equal(0, hit.Tag);
        }

        [Fact]
        public void NormalFacesTheRayForADoubleSidedTriangle()
        {
            var w = Floor();
            PhysicsHit hit;
            Assert.True(w.Raycast(new Vec3(0, -4, 0), Up, 100f, QueryMask.All, out hit));
            T.Near(Down, hit.Normal);
            T.Near(4f, hit.Distance);
        }

        [Fact]
        public void ReturnsTheNearestOfSeveralSurfaces()
        {
            var w = new MeshBuilder().Floor(-10, -10, 10, 10, 0).Floor(-10, -10, 10, 10, 2).Floor(-10, -10, 10, 10, 1).ToWorld();
            PhysicsHit hit;
            Assert.True(w.Raycast(new Vec3(0, 5, 0), Down, 100f, QueryMask.All, out hit));
            T.Near(3f, hit.Distance);
            T.Near(2f, hit.Point.Y);
        }

        [Fact]
        public void AngledRayHitsAtTheRightPointAndDistance()
        {
            var w = Floor();
            PhysicsHit hit;
            Assert.True(w.Raycast(new Vec3(0, 3, 0), new Vec3(4, -3, 0), 100f, QueryMask.All, out hit));
            T.Near(new Vec3(4, 0, 0), hit.Point);
            T.Near(5f, hit.Distance);
        }

        [Fact]
        public void DirectionIsNormalisedInternally()
        {
            var w = Floor();
            PhysicsHit hit;
            Assert.True(w.Raycast(new Vec3(0, 5, 0), new Vec3(0, -37, 0), 100f, QueryMask.All, out hit));
            T.Near(5f, hit.Distance);
        }

        [Fact]
        public void MissesPastTheEdgeAndAwayFromTheSurface()
        {
            var w = Floor();
            PhysicsHit hit;
            Assert.False(w.Raycast(new Vec3(10.5f, 5, 0), Down, 100f, QueryMask.All, out hit));
            Assert.False(w.Raycast(new Vec3(0, 5, 0), Up, 100f, QueryMask.All, out hit));
            Assert.False(w.Raycast(new Vec3(0, 5, 0), new Vec3(1, 0, 0), 100f, QueryMask.All, out hit));
            Assert.Equal(0f, hit.Distance);
            Assert.Equal(Vec3.Zero, hit.Normal);
        }

        [Fact]
        public void MaxDistanceLimitsTheRayAndExactlyMaxCounts()
        {
            var w = Floor();
            PhysicsHit hit;
            Assert.False(w.Raycast(new Vec3(0, 5, 0), Down, 4.9f, QueryMask.All, out hit));
            Assert.True(w.Raycast(new Vec3(0, 5, 0), Down, 5f, QueryMask.All, out hit));
            T.Near(5f, hit.Distance);
        }

        [Fact]
        public void ZeroDirectionAndInvalidMaxDistanceReturnFalseWithDefaultHit()
        {
            var w = Floor();
            PhysicsHit hit;
            Vec3 o = new Vec3(0, 0.5f, 0);
            Assert.False(w.Raycast(o, Vec3.Zero, 10f, QueryMask.All, out hit));
            Assert.Equal(0f, hit.Distance);
            Assert.False(w.Raycast(o, Down, 0f, QueryMask.All, out hit));
            Assert.False(w.Raycast(o, Down, -1f, QueryMask.All, out hit));
            Assert.False(w.Raycast(o, Down, float.NaN, QueryMask.All, out hit));
            Assert.False(w.Raycast(o, Down, float.PositiveInfinity, QueryMask.All, out hit));
            Assert.Equal(Vec3.Zero, hit.Point);
        }

        [Fact]
        public void RayStartingOnATriangleHitsItAtDistanceZero()
        {
            var w = Floor();
            PhysicsHit hit;
            Assert.True(w.Raycast(new Vec3(2, 0, 3), Down, 10f, QueryMask.All, out hit));
            T.Near(0f, hit.Distance);
            T.Near(new Vec3(2, 0, 3), hit.Point);
        }

        [Fact]
        public void StaticOnlyIgnoresDynamicShapes()
        {
            var w = Floor();
            w.AddBox(new Vec3(0, 2, 0), new Vec3(1, 1, 1), Quat.Identity, 42);
            PhysicsHit hit;
            Assert.True(w.Raycast(new Vec3(0, 10, 0), Down, 100f, QueryMask.All, out hit));
            Assert.Equal(HitKind.Box, hit.Kind);
            Assert.Equal(42, hit.Tag);
            T.Near(7f, hit.Distance);
            Assert.Equal(SurfaceFlags.None, hit.Flags);
            Assert.True(w.Raycast(new Vec3(0, 10, 0), Down, 100f, QueryMask.StaticOnly, out hit));
            Assert.Equal(HitKind.Static, hit.Kind);
            T.Near(10f, hit.Distance);
        }

        [Fact]
        public void DynamicOnlyIgnoresStaticTriangles()
        {
            var w = Floor();
            PhysicsHit hit;
            Assert.False(w.Raycast(new Vec3(0, 10, 0), Down, 100f, QueryMask.DynamicOnly, out hit));
            w.AddCapsule(new Vec3(0, 0, 0), 0.5f, 2f, 9);
            Assert.True(w.Raycast(new Vec3(0, 10, 0), Down, 100f, QueryMask.DynamicOnly, out hit));
            Assert.Equal(HitKind.Capsule, hit.Kind);
            Assert.Equal(9, hit.Tag);
            T.Near(8f, hit.Distance);
        }

        [Fact]
        public void ExcludedFlagsSkipStaticTrianglesThatShareAnyBit()
        {
            var w = new MeshBuilder()
                .Floor(-10, -10, 10, 10, 0, SurfaceFlags.Terrain)
                .Floor(-10, -10, 10, 10, 1, SurfaceFlags.Prop | SurfaceFlags.NoCamera)
                .ToWorld();
            PhysicsHit hit;
            Assert.True(w.Raycast(new Vec3(0, 5, 0), Down, 100f, QueryMask.All, out hit));
            T.Near(4f, hit.Distance);
            Assert.True(w.Raycast(new Vec3(0, 5, 0), Down, 100f, QueryMask.All.Excluding(SurfaceFlags.NoCamera), out hit));
            T.Near(5f, hit.Distance);
            Assert.Equal(SurfaceFlags.Terrain, hit.Flags);
            Assert.True(w.Raycast(new Vec3(0, 5, 0), Down, 100f, QueryMask.All.Excluding(SurfaceFlags.None), out hit));
            T.Near(4f, hit.Distance);
            Assert.False(w.Raycast(new Vec3(0, 5, 0), Down, 100f, QueryMask.All.Excluding(SurfaceFlags.NoCamera | SurfaceFlags.Terrain), out hit));
        }

        [Fact]
        public void ExcludedFlagsDoNotAffectDynamicShapes()
        {
            var w = new CollisionWorld();
            w.AddBox(new Vec3(0, 1, 0), new Vec3(1, 1, 1), Quat.Identity, 1);
            PhysicsHit hit;
            Assert.True(w.Raycast(new Vec3(0, 10, 0), Down, 100f, QueryMask.All.Excluding((SurfaceFlags)0xFFFF), out hit));
            Assert.Equal(HitKind.Box, hit.Kind);
        }

        [Fact]
        public void IgnoreTagSkipsOnlyDynamicShapesWithThatTag()
        {
            var w = Floor();
            w.AddBox(new Vec3(0, 4, 0), new Vec3(1, 1, 1), Quat.Identity, 1);
            w.AddBox(new Vec3(0, 2, 0), new Vec3(1, 1, 1), Quat.Identity, 2);
            PhysicsHit hit;
            Assert.True(w.Raycast(new Vec3(0, 10, 0), Down, 100f, QueryMask.All, out hit));
            Assert.Equal(1, hit.Tag);
            Assert.True(w.Raycast(new Vec3(0, 10, 0), Down, 100f, QueryMask.All.Ignoring(1), out hit));
            Assert.Equal(2, hit.Tag);
            Assert.True(w.Raycast(new Vec3(0, 10, 0), Down, 100f, QueryMask.All.Ignoring(1).Ignoring(2), out hit));
            Assert.Equal(HitKind.Box, hit.Kind); // Ignoring replaces the earlier tag, so tag 1 is hittable again.
            Assert.Equal(1, hit.Tag);
        }

        [Fact]
        public void IgnoreTagDoesNotHideStaticGeometryWithTagZero()
        {
            var w = Floor();
            PhysicsHit hit;
            Assert.True(w.Raycast(new Vec3(0, 5, 0), Down, 100f, QueryMask.All.Ignoring(0), out hit));
            Assert.Equal(HitKind.Static, hit.Kind);
        }

        [Fact]
        public void RayStartingInsideADynamicShapeHitsItAtZeroWithReversedDirection()
        {
            var w = new CollisionWorld();
            w.AddBox(new Vec3(0, 1, 0), new Vec3(2, 2, 2), Quat.Identity, 5);
            PhysicsHit hit;
            Vec3 dir = new Vec3(1, 0, 0);
            Assert.True(w.Raycast(new Vec3(0, 1, 0), dir, 50f, QueryMask.All, out hit));
            T.Near(0f, hit.Distance);
            T.Near(new Vec3(-1, 0, 0), hit.Normal);
            Assert.Equal(5, hit.Tag);
        }

        [Fact]
        public void RotatedBoxIsHitOnItsRotatedFace()
        {
            var w = new CollisionWorld();
            w.AddBox(new Vec3(0, 1, 0), new Vec3(1, 1, 1), Quat.Yaw(45f), 3);
            PhysicsHit hit;
            Assert.True(w.Raycast(new Vec3(-5, 1, 0.5f), new Vec3(1, 0, 0), 50f, QueryMask.All, out hit));
            float x = (0.7071068f * 0.5f - 1f) / 0.7071068f;
            T.Near(x + 5f, hit.Distance, 5e-3f);
            T.Near(new Vec3(-0.7071068f, 0, 0.7071068f), hit.Normal, 5e-3f);
            Assert.Equal(HitKind.Box, hit.Kind);
        }

        [Fact]
        public void AxisAlignedRayThroughARotatedBoxCornerGapMisses()
        {
            // A 45 degree box has its corner at x = +-1.414: a ray at x = -1.3 passing z = 1.3 is outside the box.
            var w = new CollisionWorld();
            w.AddBox(new Vec3(0, 1, 0), new Vec3(1, 1, 1), Quat.Yaw(45f), 3);
            PhysicsHit hit;
            Assert.False(w.Raycast(new Vec3(-5, 1, 1.3f), new Vec3(1, 0, 0), 50f, QueryMask.All, out hit));
            Assert.True(w.Raycast(new Vec3(-5, 1, 0.9f), new Vec3(1, 0, 0), 50f, QueryMask.All, out hit));
        }

        [Fact]
        public void TiltedBoxIsHitOnItsTiltedTop()
        {
            var w = new CollisionWorld();
            w.AddBox(new Vec3(0, 0, 0), new Vec3(2, 0.5f, 2), Quat.AngleAxis(30f, Vec3.Forward), 3);
            PhysicsHit hit;
            Assert.True(w.Raycast(new Vec3(0, 10, 0), Down, 50f, QueryMask.All, out hit));
            // Top face passes through (0, 0.5/cos30, 0).
            float top = 0.5f / (float)Math.Cos(30 * Math.PI / 180.0);
            T.Near(10f - top, hit.Distance, 5e-3f);
            Assert.True(hit.Normal.Y > 0.85f && hit.Normal.Y < 0.88f, "normal " + hit.Normal);
        }

        [Fact]
        public void DynamicCapsuleIsHitOnItsSide()
        {
            var w = new CollisionWorld();
            w.AddCapsule(new Vec3(0, 0, 0), 0.5f, 2f, 11);
            PhysicsHit hit;
            Assert.True(w.Raycast(new Vec3(-5, 1, 0), new Vec3(1, 0, 0), 50f, QueryMask.All, out hit));
            T.Near(4.5f, hit.Distance);
            T.Near(new Vec3(-1, 0, 0), hit.Normal);
            Assert.Equal(HitKind.Capsule, hit.Kind);
            Assert.Equal(11, hit.Tag);
            Assert.False(w.Raycast(new Vec3(-5, 2.6f, 0), new Vec3(1, 0, 0), 50f, QueryMask.All, out hit));
            Assert.True(w.Raycast(new Vec3(0, 5, 0), Down, 50f, QueryMask.All, out hit));
            T.Near(3f, hit.Distance);
        }

        [Fact]
        public void LongRayCrossesManyGridCellsAndHitsTheWallAtTheRightDistance()
        {
            var w = new MeshBuilder().Wall(123.5f, -20, 123.5f, 20, -5, 5).ToWorld();
            PhysicsHit hit;
            Assert.True(w.Raycast(new Vec3(-60.25f, 1, 3), new Vec3(1, 0, 0), 500f, QueryMask.All, out hit));
            T.Near(183.75f, hit.Distance, 1e-2f);
            T.Near(new Vec3(-1, 0, 0), hit.Normal);
        }

        [Fact]
        public void DiagonalRayThroughManyCellsHitsAFarTriangle()
        {
            var w = new MeshBuilder().Floor(95, 95, 105, 105, -2).ToWorld();
            PhysicsHit hit;
            Vec3 dir = new Vec3(1, -0.02f, 1).Normalized;
            Assert.True(w.Raycast(new Vec3(0, 0, 0), dir, 400f, QueryMask.All, out hit));
            T.Near(-2f, hit.Point.Y, 1e-2f);
            T.Near(100f, hit.Point.X, 1.5f);
        }

        [Fact]
        public void TriangleLargerThanManyCellsIsHitAnywhereInside()
        {
            var w = new MeshBuilder().Floor(-400, -400, 400, 400, 1).ToWorld();
            PhysicsHit hit;
            float[,] pts = { { 137.3f, -211.9f }, { -399f, 399f }, { 0.1f, 0.2f }, { 3.99f, -4.01f }, { -250f, -0.5f } };
            for (int i = 0; i < pts.GetLength(0); i++)
            {
                Assert.True(w.Raycast(new Vec3(pts[i, 0], 6, pts[i, 1]), Down, 20f, QueryMask.All, out hit), "point " + i);
                T.Near(5f, hit.Distance);
            }
            Assert.False(w.Raycast(new Vec3(401, 6, 0), Down, 20f, QueryMask.All, out hit));
        }

        [Fact]
        public void LongSliverTriangleIsHitOnlyInsideItsArea()
        {
            var w = new MeshBuilder().Tri(new Vec3(0, 0, 0), new Vec3(100, 0, 0), new Vec3(100, 0, 1)).ToWorld();
            PhysicsHit hit;
            Assert.True(w.Raycast(new Vec3(50, 5, 0.2f), Down, 10f, QueryMask.All, out hit));
            Assert.False(w.Raycast(new Vec3(50, 5, 0.8f), Down, 10f, QueryMask.All, out hit));
            Assert.True(w.Raycast(new Vec3(95, 5, 0.8f), Down, 10f, QueryMask.All, out hit));
        }

        [Fact]
        public void SurfacesOnCellBordersAndNegativeCoordinatesAreFound()
        {
            var w = new CollisionWorld();
            new MeshBuilder().Wall(4f, -10, 4f, 10, 0, 5).Apply(w, 1);
            new MeshBuilder().Wall(-4f, -10, -4f, 10, 0, 5).Apply(w, 2);
            new MeshBuilder().Wall(0f, -10, 0f, 10, 0, 5).Apply(w, 3);
            PhysicsHit hit;
            Assert.True(w.Raycast(new Vec3(10, 1, -3.999f), new Vec3(-1, 0, 0), 100f, QueryMask.All, out hit));
            T.Near(6f, hit.Distance);
            Assert.True(w.Raycast(new Vec3(-10, 1, 4f), new Vec3(1, 0, 0), 100f, QueryMask.All, out hit));
            T.Near(6f, hit.Distance);
            Assert.True(w.Raycast(new Vec3(2, 1, 0), new Vec3(-1, 0, 0), 100f, QueryMask.All, out hit));
            T.Near(2f, hit.Distance);
        }

        [Fact]
        public void RayAcrossSeveralRegionsReturnsTheNearestRegionsHit()
        {
            var w = new CollisionWorld();
            for (int i = 0; i < 4; i++)
            {
                new MeshBuilder().Wall(20f * (i + 1), -5, 20f * (i + 1), 5, 0, 3).Apply(w, 10 + i);
            }
            PhysicsHit hit;
            Assert.True(w.Raycast(new Vec3(0, 1, 0), new Vec3(1, 0, 0), 500f, QueryMask.All, out hit));
            T.Near(20f, hit.Distance);
            w.RemoveRegion(10);
            Assert.True(w.Raycast(new Vec3(0, 1, 0), new Vec3(1, 0, 0), 500f, QueryMask.All, out hit));
            T.Near(40f, hit.Distance);
            Assert.True(w.Raycast(new Vec3(100, 1, 0), new Vec3(-1, 0, 0), 500f, QueryMask.All, out hit));
            T.Near(20f, hit.Distance);
        }
    }
}
