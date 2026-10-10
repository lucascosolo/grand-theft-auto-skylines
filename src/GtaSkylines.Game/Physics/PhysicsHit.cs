using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.Physics
{
    /// <summary>The nearest hit of a ray, sphere or capsule cast. World space, metres.</summary>
    public struct PhysicsHit
    {
        /// <summary>
        /// The contact point on the surface that was hit. For a cast that starts overlapping, the closest surface point to
        /// the shape (see <see cref="CollisionWorld.SphereCast"/>).
        /// </summary>
        public Vec3 Point;

        /// <summary>
        /// Unit surface normal at <see cref="Point"/>, facing the query: for a ray, <c>Dot(Normal, direction) &lt;= 0</c>
        /// (static triangles are double-sided); for a sphere or capsule, pointing from the surface toward the shape's
        /// centre/axis at the moment of contact.
        /// </summary>
        public Vec3 Normal;

        /// <summary>
        /// How far the ray origin (or the swept shape's reference point) travelled along the normalised direction before
        /// contact, in [0, maxDistance]. 0 when the query started overlapping.
        /// </summary>
        public float Distance;

        /// <summary>Surface flags of a static triangle; <see cref="SurfaceFlags.None"/> for dynamic shapes.</summary>
        public SurfaceFlags Flags;

        /// <summary>Tag of a dynamic shape; 0 for static triangles.</summary>
        public int Tag;

        /// <summary>What was hit.</summary>
        public HitKind Kind;
    }

    /// <summary>One penetration found by <see cref="CollisionWorld.OverlapSphere"/>. World space, metres.</summary>
    public struct Contact
    {
        /// <summary>Closest point on the shape's surface to the sphere centre.</summary>
        public Vec3 Point;

        /// <summary>
        /// Unit direction that pushes the sphere out: from <see cref="Point"/> toward the centre, or, when the centre lies
        /// on the surface or inside a dynamic shape, the outward direction of least penetration (the triangle's geometric
        /// normal <c>Cross(b − a, c − a)</c> normalised for a centre exactly on a triangle; +x for a centre exactly on a
        /// capsule's axis).
        /// </summary>
        public Vec3 Normal;

        /// <summary>
        /// Penetration in metres, always &gt; 0: moving the sphere by <c>Normal * Depth</c> resolves this contact alone.
        /// </summary>
        public float Depth;

        /// <summary>Surface flags of a static triangle; <see cref="SurfaceFlags.None"/> for dynamic shapes.</summary>
        public SurfaceFlags Flags;

        /// <summary>Tag of a dynamic shape; 0 for static triangles.</summary>
        public int Tag;

        /// <summary>What is penetrated.</summary>
        public HitKind Kind;
    }
}
