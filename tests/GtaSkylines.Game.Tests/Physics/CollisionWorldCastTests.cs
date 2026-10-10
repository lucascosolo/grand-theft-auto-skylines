using System;
using System.Collections.Generic;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using Xunit;

namespace GtaSkylines.Game.Tests.PhysicsTests
{
    public class CollisionWorldCastTests
    {
        private static readonly Vec3 Down = new Vec3(0, -1, 0);
        private static readonly Vec3 PlusX = new Vec3(1, 0, 0);

        private static CollisionWorld Floor(float y = 0f)
        {
            return new MeshBuilder().Floor(-30, -30, 30, 30, y).ToWorld();
        }

        private static CollisionWorld WallAtX5()
        {
            return new MeshBuilder().Wall(5, -30, 5, 30, -10, 10).ToWorld();
        }

        // ---------------- sphere cast ----------------

        [Fact]
        public void SphereCastLandsOnTheFloor()
        {
            var w = Floor();
            PhysicsHit hit;
            Assert.True(w.SphereCast(new Vec3(1, 3, 2), 0.5f, Down, 10f, QueryMask.All, out hit));
            T.Near(2.5f, hit.Distance);
            T.Near(new Vec3(1, 0, 2), hit.Point);
            T.Near(Vec3.Up, hit.Normal);
            Assert.Equal(HitKind.Static, hit.Kind);
        }

        [Fact]
        public void SphereCastHitsAWallWithNormalTowardTheCentre()
        {
            var w = WallAtX5();
            PhysicsHit hit;
            Assert.True(w.SphereCast(new Vec3(0, 1, 0), 0.5f, PlusX, 10f, QueryMask.All, out hit));
            T.Near(4.5f, hit.Distance);
            T.Near(new Vec3(5, 1, 0), hit.Point);
            T.Near(new Vec3(-1, 0, 0), hit.Normal);
        }

        [Fact]
        public void SphereCastMaxDistanceAndMissesAreRespected()
        {
            var w = WallAtX5();
            PhysicsHit hit;
            Assert.False(w.SphereCast(new Vec3(0, 1, 0), 0.5f, PlusX, 4.4f, QueryMask.All, out hit));
            Assert.False(w.SphereCast(new Vec3(0, 1, 0), 0.5f, new Vec3(-1, 0, 0), 100f, QueryMask.All, out hit));
            Assert.False(w.SphereCast(new Vec3(0, 1, 0), 0.5f, new Vec3(0, 0, 1), 100f, QueryMask.All, out hit));
            Assert.True(w.SphereCast(new Vec3(0, 1, 0), 0.5f, PlusX, 4.6f, QueryMask.All, out hit));
        }

        [Fact]
        public void SphereCastDirectionIsNormalised()
        {
            var w = WallAtX5();
            PhysicsHit hit;
            Assert.True(w.SphereCast(new Vec3(0, 1, 0), 0.5f, new Vec3(9, 0, 0), 10f, QueryMask.All, out hit));
            T.Near(4.5f, hit.Distance);
        }

        [Fact]
        public void SphereCastIntoACornerStopsAtBothWalls()
        {
            var w = new MeshBuilder().Wall(5, -30, 5, 30, -10, 10).Wall(-30, 5, 30, 5, -10, 10).ToWorld();
            PhysicsHit hit;
            Vec3 dir = new Vec3(1, 0, 1).Normalized;
            Assert.True(w.SphereCast(new Vec3(0, 1, 0), 0.5f, dir, 20f, QueryMask.All, out hit));
            T.Near(4.5f * (float)Math.Sqrt(2), hit.Distance, 5e-3f);
            Assert.True(Vec3.Dot(hit.Normal, new Vec3(-1, 0, 0)) > 0.99f || Vec3.Dot(hit.Normal, new Vec3(0, 0, -1)) > 0.99f, "normal " + hit.Normal);
        }

        [Fact]
        public void SphereCastHitsDynamicBoxAndCapsule()
        {
            var w = new CollisionWorld();
            w.AddBox(new Vec3(5, 1, 0), new Vec3(1, 1, 1), Quat.Identity, 4);
            PhysicsHit hit;
            Assert.True(w.SphereCast(new Vec3(0, 1, 0), 0.5f, PlusX, 20f, QueryMask.All, out hit));
            T.Near(3.5f, hit.Distance);
            Assert.Equal(HitKind.Box, hit.Kind);
            Assert.Equal(4, hit.Tag);
            T.Near(new Vec3(-1, 0, 0), hit.Normal);

            w.ClearDynamic();
            w.AddCapsule(new Vec3(5, 0, 0), 0.5f, 2f, 8);
            Assert.True(w.SphereCast(new Vec3(0, 1, 0), 0.5f, PlusX, 20f, QueryMask.All, out hit));
            T.Near(4f, hit.Distance);
            Assert.Equal(HitKind.Capsule, hit.Kind);
            Assert.Equal(8, hit.Tag);
            T.Near(new Vec3(-1, 0, 0), hit.Normal);
        }

        [Fact]
        public void SphereCastHonoursMasks()
        {
            var w = Floor();
            w.AddBox(new Vec3(0, 3, 0), new Vec3(1, 1, 1), Quat.Identity, 4);
            PhysicsHit hit;
            Assert.True(w.SphereCast(new Vec3(0, 10, 0), 0.5f, Down, 20f, QueryMask.StaticOnly, out hit));
            T.Near(9.5f, hit.Distance);
            Assert.True(w.SphereCast(new Vec3(0, 10, 0), 0.5f, Down, 20f, QueryMask.DynamicOnly, out hit));
            T.Near(6.5f, hit.Distance);
            Assert.True(w.SphereCast(new Vec3(0, 10, 0), 0.5f, Down, 20f, QueryMask.All.Ignoring(4), out hit));
            T.Near(9.5f, hit.Distance);
        }

        [Fact]
        public void SphereCastStartingPenetratingReturnsDistanceZeroWithDeepestContact()
        {
            var w = Floor();
            PhysicsHit hit;
            Assert.True(w.SphereCast(new Vec3(0, 0.3f, 0), 0.5f, PlusX, 10f, QueryMask.All, out hit));
            T.Near(0f, hit.Distance);
            T.Near(Vec3.Up, hit.Normal);
            T.Near(new Vec3(0, 0, 0), hit.Point);
        }

        [Fact]
        public void SphereCastZeroDirectionOrZeroDistanceIsAnOverlapTest()
        {
            var w = Floor();
            PhysicsHit hit;
            Assert.True(w.SphereCast(new Vec3(0, 0.3f, 0), 0.5f, Vec3.Zero, 5f, QueryMask.All, out hit));
            T.Near(0f, hit.Distance);
            Assert.True(w.SphereCast(new Vec3(0, 0.3f, 0), 0.5f, Down, 0f, QueryMask.All, out hit));
            T.Near(0f, hit.Distance);
            Assert.False(w.SphereCast(new Vec3(0, 2f, 0), 0.5f, Vec3.Zero, 5f, QueryMask.All, out hit));
            Assert.False(w.SphereCast(new Vec3(0, 2f, 0), 0.5f, Down, 0f, QueryMask.All, out hit));
        }

        [Fact]
        public void SphereCastTouchingAtStartIsOnlyAHitWhenMovingIntoTheSurface()
        {
            var w = Floor();
            PhysicsHit hit;
            Vec3 touching = new Vec3(0, 0.5f, 0);
            Assert.False(w.SphereCast(touching, 0.5f, PlusX, 3f, QueryMask.All, out hit));
            Assert.False(w.SphereCast(touching, 0.5f, Vec3.Up, 3f, QueryMask.All, out hit));
            Assert.False(w.SphereCast(touching, 0.5f, Vec3.Zero, 3f, QueryMask.All, out hit));
            Assert.True(w.SphereCast(touching, 0.5f, Down, 3f, QueryMask.All, out hit));
            T.Near(0f, hit.Distance, 2e-3f);
        }

        [Fact]
        public void SphereCastInvalidInputs()
        {
            var w = Floor();
            PhysicsHit hit;
            Assert.False(w.SphereCast(new Vec3(0, 3, 0), 0.5f, Down, -1f, QueryMask.All, out hit));
            Assert.False(w.SphereCast(new Vec3(0, 3, 0), 0.5f, Down, float.NaN, QueryMask.All, out hit));
            Assert.False(w.SphereCast(new Vec3(0, 3, 0), 0.5f, Down, float.PositiveInfinity, QueryMask.All, out hit));
            Assert.Throws<ArgumentOutOfRangeException>(() => { PhysicsHit h; w.SphereCast(Vec3.Zero, 0f, Down, 1f, QueryMask.All, out h); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { PhysicsHit h; w.SphereCast(Vec3.Zero, -1f, Down, 1f, QueryMask.All, out h); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { PhysicsHit h; w.SphereCast(Vec3.Zero, float.NaN, Down, 1f, QueryMask.All, out h); });
        }

        [Fact]
        public void SphereCastIsContinuousOverAThousandMetres()
        {
            var w = new MeshBuilder().Wall(500.5f, -5, 500.5f, 5, -5, 5).ToWorld();
            PhysicsHit hit;
            Assert.True(w.SphereCast(new Vec3(0, 1, 0), 0.25f, PlusX, 1000f, QueryMask.All, out hit));
            T.Near(500.25f, hit.Distance, 2e-2f);
        }

        [Fact]
        public void SphereCastSpansManyCellsAndRegions()
        {
            var w = new CollisionWorld();
            new MeshBuilder().Wall(30, -50, 30, 50, 0, 5).Apply(w, 1);
            new MeshBuilder().Wall(70, -50, 70, 50, 0, 5).Apply(w, 2);
            PhysicsHit hit;
            Assert.True(w.SphereCast(new Vec3(-20, 1, 0.3f), 0.5f, PlusX, 200f, QueryMask.All, out hit));
            T.Near(49.5f, hit.Distance, 1e-2f);
            Assert.True(w.SphereCast(new Vec3(100, 1, -0.3f), 0.5f, new Vec3(-1, 0, 0), 200f, QueryMask.All, out hit));
            T.Near(29.5f, hit.Distance, 1e-2f);
        }

        // ---------------- capsule cast ----------------

        [Fact]
        public void CapsuleCastHitsAWallOnTheCapsuleRadius()
        {
            var w = WallAtX5();
            PhysicsHit hit;
            Assert.True(w.CapsuleCast(new Vec3(0, 0, 0), 0.35f, 1.8f, PlusX, 10f, QueryMask.All, out hit));
            T.Near(4.65f, hit.Distance);
            T.Near(new Vec3(-1, 0, 0), hit.Normal);
            T.Near(5f, hit.Point.X);
            Assert.False(w.CapsuleCast(new Vec3(0, 0, 0), 0.35f, 1.8f, PlusX, 4.5f, QueryMask.All, out hit));
        }

        [Fact]
        public void CapsuleCastFeetLandOnTheFloor()
        {
            var w = Floor();
            PhysicsHit hit;
            Assert.True(w.CapsuleCast(new Vec3(2, 3, 1), 0.35f, 1.8f, Down, 10f, QueryMask.All, out hit));
            T.Near(3f, hit.Distance);
            T.Near(Vec3.Up, hit.Normal);
            T.Near(0f, hit.Point.Y);
        }

        [Fact]
        public void CapsuleCastHeadHitsACeiling()
        {
            var w = Floor(3f);
            PhysicsHit hit;
            Assert.True(w.CapsuleCast(new Vec3(0, 0, 0), 0.35f, 1.8f, Vec3.Up, 10f, QueryMask.All, out hit));
            T.Near(1.2f, hit.Distance);
            T.Near(Down, hit.Normal);
        }

        [Fact]
        public void CapsuleCastHitsTheLowerHemisphereOnALowObstacleEdge()
        {
            // A 0.2 m high step top edge at x = 5: the foot sphere (centre 0.35 up) clears it by 0.15 and touches the edge.
            var w = new MeshBuilder().Box(new Vec3(5, 0, -10), new Vec3(8, 0.2f, 10)).ToWorld();
            PhysicsHit hit;
            Assert.True(w.CapsuleCast(new Vec3(0, 0, 0), 0.35f, 1.8f, PlusX, 10f, QueryMask.All, out hit));
            float gap = (float)Math.Sqrt(0.35 * 0.35 - 0.15 * 0.15);
            T.Near(5f - gap, hit.Distance, 5e-3f);
            T.Near(new Vec3(-gap / 0.35f, 0.15f / 0.35f, 0), hit.Normal, 1e-2f);
        }

        [Fact]
        public void CapsuleCastPassesOverAnObstacleBelowTheCapsuleBottom()
        {
            var w = new MeshBuilder().Box(new Vec3(5, 0, -10), new Vec3(8, 0.2f, 10)).ToWorld();
            PhysicsHit hit;
            Assert.False(w.CapsuleCast(new Vec3(0, 0.2f, 0), 0.35f, 1.8f, PlusX, 10f, QueryMask.All, out hit));
        }

        [Fact]
        public void CapsuleCastHitsDynamicBoxAndCapsule()
        {
            var w = new CollisionWorld();
            w.AddBox(new Vec3(5, 1, 0), new Vec3(1, 1, 1), Quat.Identity, 6);
            PhysicsHit hit;
            Assert.True(w.CapsuleCast(Vec3.Zero, 0.35f, 1.8f, PlusX, 10f, QueryMask.All, out hit));
            T.Near(3.65f, hit.Distance);
            Assert.Equal(HitKind.Box, hit.Kind);
            Assert.Equal(6, hit.Tag);

            w.ClearDynamic();
            w.AddCapsule(new Vec3(5, 0, 0), 0.5f, 2f, 7);
            Assert.True(w.CapsuleCast(Vec3.Zero, 0.35f, 1.8f, PlusX, 10f, QueryMask.All, out hit));
            T.Near(4.15f, hit.Distance);
            Assert.Equal(HitKind.Capsule, hit.Kind);
            Assert.Equal(7, hit.Tag);
            T.Near(new Vec3(-1, 0, 0), hit.Normal);
        }

        [Fact]
        public void CapsuleCastHitsARotatedBox()
        {
            var w = new CollisionWorld();
            w.AddBox(new Vec3(5, 1, 0), new Vec3(1, 1, 1), Quat.Yaw(45f), 6);
            PhysicsHit hit;
            // The box's corner points at -x at x = 5 - 1.414; the capsule axis (z = 0) meets it head on.
            Assert.True(w.CapsuleCast(Vec3.Zero, 0.35f, 1.8f, PlusX, 10f, QueryMask.All, out hit));
            T.Near(5f - 1.4142136f - 0.35f, hit.Distance, 1e-2f);
        }

        [Fact]
        public void CapsuleCastHonoursMasks()
        {
            var w = WallAtX5();
            w.AddBox(new Vec3(3, 1, 0), new Vec3(0.5f, 1, 1), Quat.Identity, 2);
            PhysicsHit hit;
            Assert.True(w.CapsuleCast(Vec3.Zero, 0.35f, 1.8f, PlusX, 10f, QueryMask.All, out hit));
            T.Near(2.15f, hit.Distance);
            Assert.True(w.CapsuleCast(Vec3.Zero, 0.35f, 1.8f, PlusX, 10f, QueryMask.StaticOnly, out hit));
            T.Near(4.65f, hit.Distance);
            Assert.True(w.CapsuleCast(Vec3.Zero, 0.35f, 1.8f, PlusX, 10f, QueryMask.DynamicOnly, out hit));
            T.Near(2.15f, hit.Distance);
            Assert.True(w.CapsuleCast(Vec3.Zero, 0.35f, 1.8f, PlusX, 10f, QueryMask.All.Ignoring(2), out hit));
            T.Near(4.65f, hit.Distance);
            Assert.False(w.CapsuleCast(Vec3.Zero, 0.35f, 1.8f, PlusX, 10f, QueryMask.All.Ignoring(2).Excluding(SurfaceFlags.Building), out hit));
        }

        [Fact]
        public void CapsuleCastStartingPenetratingIsDistanceZero()
        {
            var w = Floor();
            PhysicsHit hit;
            Assert.True(w.CapsuleCast(new Vec3(0, -0.1f, 0), 0.35f, 1.8f, PlusX, 10f, QueryMask.All, out hit));
            T.Near(0f, hit.Distance);
            T.Near(Vec3.Up, hit.Normal);
        }

        [Fact]
        public void CapsuleCastZeroLengthIsAnOverlapTest()
        {
            var w = WallAtX5();
            PhysicsHit hit;
            Assert.True(w.CapsuleCast(new Vec3(4.8f, 0, 0), 0.35f, 1.8f, Vec3.Zero, 3f, QueryMask.All, out hit));
            T.Near(0f, hit.Distance);
            T.Near(new Vec3(-1, 0, 0), hit.Normal);
            Assert.True(w.CapsuleCast(new Vec3(4.8f, 0, 0), 0.35f, 1.8f, PlusX, 0f, QueryMask.All, out hit));
            Assert.False(w.CapsuleCast(new Vec3(0, 0, 0), 0.35f, 1.8f, Vec3.Zero, 3f, QueryMask.All, out hit));
            Assert.False(w.CapsuleCast(new Vec3(0, 0, 0), 0.35f, 1.8f, PlusX, 0f, QueryMask.All, out hit));
        }

        [Fact]
        public void CapsuleStandingOnTheFloorMovingAlongItDoesNotHit()
        {
            var w = Floor();
            PhysicsHit hit;
            Assert.False(w.CapsuleCast(Vec3.Zero, 0.35f, 1.8f, PlusX, 5f, QueryMask.All, out hit));
            Assert.False(w.CapsuleCast(Vec3.Zero, 0.35f, 1.8f, Vec3.Up, 5f, QueryMask.All, out hit));
            Assert.True(w.CapsuleCast(Vec3.Zero, 0.35f, 1.8f, Down, 5f, QueryMask.All, out hit));
            T.Near(0f, hit.Distance, 2e-3f);
        }

        [Fact]
        public void CapsuleCastInvalidInputs()
        {
            var w = Floor();
            PhysicsHit hit;
            Assert.False(w.CapsuleCast(new Vec3(0, 3, 0), 0.35f, 1.8f, Down, -1f, QueryMask.All, out hit));
            Assert.False(w.CapsuleCast(new Vec3(0, 3, 0), 0.35f, 1.8f, Down, float.NaN, QueryMask.All, out hit));
            Assert.Throws<ArgumentOutOfRangeException>(() => { PhysicsHit h; w.CapsuleCast(Vec3.Zero, 0f, 1.8f, Down, 1f, QueryMask.All, out h); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { PhysicsHit h; w.CapsuleCast(Vec3.Zero, 0.5f, 0.9f, Down, 1f, QueryMask.All, out h); });
        }

        [Fact]
        public void CapsuleCastSweepsAcrossManyCellsAndDoesNotTunnelThinGeometry()
        {
            var w = new CollisionWorld();
            new MeshBuilder().Wall(-20, -5, -20, 5, 0, 3).Apply(w, 1);
            new MeshBuilder().Wall(400, -5, 400, 5, 0, 3).Apply(w, 2);
            PhysicsHit hit;
            Assert.True(w.CapsuleCast(new Vec3(100, 0, 0), 0.35f, 1.8f, PlusX, 1000f, QueryMask.All, out hit));
            T.Near(299.65f, hit.Distance, 2e-2f);
            Assert.True(w.CapsuleCast(new Vec3(100, 0, 0), 0.35f, 1.8f, new Vec3(-1, 0, 0), 1000f, QueryMask.All, out hit));
            T.Near(119.65f, hit.Distance, 2e-2f);
        }

        // ---------------- overlap sphere ----------------

        private static CollisionWorld BigTriangle(SurfaceFlags f = SurfaceFlags.Road)
        {
            return new MeshBuilder().Tri(new Vec3(-20, 0, -20), new Vec3(20, 0, -20), new Vec3(0, 0, 20), f).ToWorld();
        }

        [Fact]
        public void OverlapSphereReportsTriangleContactWithNormalAndDepth()
        {
            var w = BigTriangle();
            var list = new List<Contact>();
            Assert.Equal(1, w.OverlapSphere(new Vec3(0, 0.3f, 0), 0.5f, QueryMask.All, list));
            Assert.Single(list);
            T.Near(0.2f, list[0].Depth);
            T.Near(Vec3.Up, list[0].Normal);
            T.Near(Vec3.Zero, list[0].Point);
            Assert.Equal(SurfaceFlags.Road, list[0].Flags);
            Assert.Equal(HitKind.Static, list[0].Kind);
        }

        [Fact]
        public void OverlapSphereTouchingOrAboveIsNoContact()
        {
            var w = BigTriangle();
            var list = new List<Contact>();
            Assert.Equal(0, w.OverlapSphere(new Vec3(0, 0.5f, 0), 0.5f, QueryMask.All, list));
            Assert.Equal(0, w.OverlapSphere(new Vec3(0, 2f, 0), 0.5f, QueryMask.All, list));
            Assert.Empty(list);
        }

        [Fact]
        public void OverlapSphereClearsTheListFirst()
        {
            var w = BigTriangle();
            var list = new List<Contact> { new Contact(), new Contact(), new Contact() };
            Assert.Equal(1, w.OverlapSphere(new Vec3(0, 0.3f, 0), 0.5f, QueryMask.All, list));
            Assert.Single(list);
            Assert.Equal(0, w.OverlapSphere(new Vec3(0, 9f, 0), 0.5f, QueryMask.All, list));
            Assert.Empty(list);
        }

        [Fact]
        public void OverlapSphereCentreOnATriangleUsesItsGeometricNormal()
        {
            // Cross(b - a, c - a) of this winding points down (0, -1, 0).
            var w = BigTriangle();
            var list = new List<Contact>();
            Assert.Equal(1, w.OverlapSphere(new Vec3(0, 0, 0), 0.5f, QueryMask.All, list));
            T.Near(new Vec3(0, -1, 0), list[0].Normal);
            T.Near(0.5f, list[0].Depth);
        }

        [Fact]
        public void OverlapSphereGivesOneContactPerAdjacentTriangle()
        {
            var w = Floor();
            var list = new List<Contact>();
            // The centre of a quad lies on the shared diagonal, so both triangles are penetrated.
            Assert.Equal(2, w.OverlapSphere(new Vec3(0, 0.3f, 0), 0.5f, QueryMask.All, list));
            foreach (var c in list) T.Near(0.2f, c.Depth);
        }

        [Fact]
        public void OverlapSphereBoxContactOutsideAndInside()
        {
            var w = new CollisionWorld();
            w.AddBox(new Vec3(0, 1, 0), new Vec3(1, 1, 1), Quat.Identity, 12);
            var list = new List<Contact>();
            Assert.Equal(1, w.OverlapSphere(new Vec3(1.3f, 1, 0), 0.5f, QueryMask.All, list));
            T.Near(0.2f, list[0].Depth);
            T.Near(new Vec3(1, 0, 0), list[0].Normal);
            T.Near(new Vec3(1, 1, 0), list[0].Point);
            Assert.Equal(HitKind.Box, list[0].Kind);
            Assert.Equal(12, list[0].Tag);
            Assert.Equal(SurfaceFlags.None, list[0].Flags);

            // Centre inside, nearest face +x at 0.1: push out along +x by 0.1 + radius.
            Assert.Equal(1, w.OverlapSphere(new Vec3(0.9f, 1, 0), 0.5f, QueryMask.All, list));
            T.Near(new Vec3(1, 0, 0), list[0].Normal);
            T.Near(0.6f, list[0].Depth);
        }

        [Fact]
        public void OverlapSphereRotatedBoxEdge()
        {
            var w = new CollisionWorld();
            w.AddBox(new Vec3(0, 1, 0), new Vec3(1, 1, 1), Quat.Yaw(45f), 12);
            var list = new List<Contact>();
            // Unrotated, the box ends at z = 1 and misses this sphere; rotated, its edge reaches z = 1.414.
            Assert.Equal(1, w.OverlapSphere(new Vec3(0, 1, 1.6f), 0.5f, QueryMask.All, list));
            T.Near(0.5f - (1.6f - 1.4142136f), list[0].Depth, 5e-3f);
            T.Near(new Vec3(0, 0, 1), list[0].Normal, 5e-3f);
        }

        [Fact]
        public void OverlapSphereCapsuleContactAndAxisCentre()
        {
            var w = new CollisionWorld();
            w.AddCapsule(new Vec3(0, 0, 0), 0.5f, 2f, 3);
            var list = new List<Contact>();
            Assert.Equal(1, w.OverlapSphere(new Vec3(0.8f, 1, 0), 0.5f, QueryMask.All, list));
            T.Near(0.2f, list[0].Depth);
            T.Near(new Vec3(1, 0, 0), list[0].Normal);
            T.Near(new Vec3(0.5f, 1, 0), list[0].Point);
            Assert.Equal(HitKind.Capsule, list[0].Kind);
            Assert.Equal(3, list[0].Tag);

            Assert.Equal(1, w.OverlapSphere(new Vec3(0, 1, 0), 0.5f, QueryMask.All, list));
            T.Near(new Vec3(1, 0, 0), list[0].Normal);
            T.Near(1.0f, list[0].Depth);
        }

        [Fact]
        public void OverlapSphereHonoursMasks()
        {
            var w = new MeshBuilder().Floor(-10, -10, 10, 10, 0, SurfaceFlags.NoCamera).ToWorld();
            w.AddBox(new Vec3(0, 1, 0), new Vec3(1, 1, 1), Quat.Identity, 5);
            var list = new List<Contact>();
            Vec3 c = new Vec3(0, 0.3f, 0);
            Assert.Equal(3, w.OverlapSphere(c, 0.5f, QueryMask.All, list));
            Assert.Equal(2, w.OverlapSphere(c, 0.5f, QueryMask.StaticOnly, list));
            Assert.Equal(1, w.OverlapSphere(c, 0.5f, QueryMask.DynamicOnly, list));
            Assert.Equal(1, w.OverlapSphere(c, 0.5f, QueryMask.All.Excluding(SurfaceFlags.NoCamera), list));
            Assert.Equal(2, w.OverlapSphere(c, 0.5f, QueryMask.All.Ignoring(5), list));
        }

        [Fact]
        public void OverlapSphereSpansManyCellsAndRegions()
        {
            var w = new CollisionWorld();
            long key = 0;
            for (int i = 0; i < 3; i++)
            {
                var m = new MeshBuilder();
                for (int j = 0; j < 3; j++)
                {
                    float cx = i * 10f, cz = j * 10f;
                    m.Tri(new Vec3(cx - 0.5f, 0, cz - 0.5f), new Vec3(cx + 0.5f, 0, cz - 0.5f), new Vec3(cx, 0, cz + 0.5f));
                }
                m.Apply(w, key++);
            }
            new MeshBuilder().Tri(new Vec3(39.5f, 0, 9.5f), new Vec3(40.5f, 0, 9.5f), new Vec3(40, 0, 10.5f)).Apply(w, 99);
            var list = new List<Contact>();
            Assert.Equal(9, w.OverlapSphere(new Vec3(10, 0.5f, 10), 15f, QueryMask.All, list));
            foreach (var c in list) Assert.True(c.Depth > 0f);
        }

        [Fact]
        public void OverlapSphereInvalidArguments()
        {
            var w = Floor();
            Assert.Throws<ArgumentNullException>(() => w.OverlapSphere(Vec3.Zero, 0.5f, QueryMask.All, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => w.OverlapSphere(Vec3.Zero, 0f, QueryMask.All, new List<Contact>()));
            Assert.Throws<ArgumentOutOfRangeException>(() => w.OverlapSphere(Vec3.Zero, float.NaN, QueryMask.All, new List<Contact>()));
        }
    }
}
