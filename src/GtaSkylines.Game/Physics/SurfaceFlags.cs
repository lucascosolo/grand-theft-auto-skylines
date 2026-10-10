using System;

namespace GtaSkylines.Game.Physics
{
    /// <summary>
    /// What a static triangle is. The Mod streams one value per triangle (the <c>ushort</c> flags of
    /// <c>Skylines.Core.Geometry.TriangleBuffer</c>), so the numeric values are part of the contract. A triangle may carry
    /// several flags (a bridge deck is <c>Road | Bridge</c>).
    /// </summary>
    [Flags]
    public enum SurfaceFlags : ushort
    {
        /// <summary>No flags.</summary>
        None = 0,

        /// <summary>CS1 terrain.</summary>
        Terrain = 1 << 0,

        /// <summary>Road surface vehicles drive on.</summary>
        Road = 1 << 1,

        /// <summary>Sidewalk / pedestrian paths.</summary>
        Pavement = 1 << 2,

        /// <summary>Building shell.</summary>
        Building = 1 << 3,

        /// <summary>Props (lamps, benches, signs).</summary>
        Prop = 1 << 4,

        /// <summary>Tree trunks.</summary>
        Tree = 1 << 5,

        /// <summary>Elevated road or path structure.</summary>
        Bridge = 1 << 6,

        /// <summary>Generated interior geometry.</summary>
        Interior = 1 << 7,

        /// <summary>The camera's spring arm passes through this triangle (foliage, small props).</summary>
        NoCamera = 1 << 8,
    }

    /// <summary>What a query hit.</summary>
    public enum HitKind
    {
        /// <summary>A static triangle; <see cref="PhysicsHit.Flags"/> is its surface flags, the tag is 0.</summary>
        Static = 0,

        /// <summary>A dynamic oriented box; the tag is the box's tag, the flags are <see cref="SurfaceFlags.None"/>.</summary>
        Box = 1,

        /// <summary>A dynamic vertical capsule; the tag is the capsule's tag, the flags are <see cref="SurfaceFlags.None"/>.</summary>
        Capsule = 2,
    }
}
