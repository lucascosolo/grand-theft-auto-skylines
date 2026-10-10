namespace GtaSkylines.Game.Physics
{
    /// <summary>
    /// Which shapes a <see cref="CollisionWorld"/> query considers. <c>default(QueryMask)</c> (= <see cref="All"/>)
    /// considers every static triangle and every dynamic shape. Filters combine: a shape is considered only when no
    /// filter rejects it.
    /// </summary>
    public struct QueryMask
    {
        /// <summary>Ignore every static triangle.</summary>
        public bool SkipStatic;

        /// <summary>Ignore every dynamic box and capsule.</summary>
        public bool SkipDynamic;

        /// <summary>
        /// Static triangles whose flags share any bit with this value are ignored (e.g. <see cref="SurfaceFlags.NoCamera"/>
        /// for the camera). Has no effect on dynamic shapes. <see cref="SurfaceFlags.None"/> excludes nothing.
        /// </summary>
        public SurfaceFlags ExcludeFlags;

        /// <summary>When true, dynamic shapes whose tag equals <see cref="IgnoreTag"/> are ignored.</summary>
        public bool HasIgnoreTag;

        /// <summary>The dynamic tag to ignore when <see cref="HasIgnoreTag"/> is set (e.g. a vehicle's own box).</summary>
        public int IgnoreTag;

        /// <summary>Everything.</summary>
        public static readonly QueryMask All = default(QueryMask);

        /// <summary>Static triangles only.</summary>
        public static readonly QueryMask StaticOnly = new QueryMask { SkipDynamic = true };

        /// <summary>Dynamic boxes and capsules only.</summary>
        public static readonly QueryMask DynamicOnly = new QueryMask { SkipStatic = true };

        /// <summary>A copy that also ignores dynamic shapes with tag <paramref name="tag"/> (replaces any earlier ignored tag).</summary>
        public QueryMask Ignoring(int tag)
        {
            QueryMask m = this;
            m.HasIgnoreTag = true;
            m.IgnoreTag = tag;
            return m;
        }

        /// <summary>A copy whose <see cref="ExcludeFlags"/> also contains <paramref name="flags"/>.</summary>
        public QueryMask Excluding(SurfaceFlags flags)
        {
            QueryMask m = this;
            m.ExcludeFlags |= flags;
            return m;
        }
    }
}
