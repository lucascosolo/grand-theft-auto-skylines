namespace GtaSkylines.Mod.Lifecycle
{
    /// <summary>What an entry raycast hit.</summary>
    internal enum DropSurface
    {
        /// <summary>Nothing.</summary>
        None = 0,
        /// <summary>The terrain.</summary>
        Terrain = 1,
        /// <summary>A road, path or bridge (NetManager).</summary>
        Road = 2,
        /// <summary>A building (roof or wall).</summary>
        Building = 3,
    }

    /// <summary>
    /// Picks the walkable surface nearest along the cursor ray the way CS1's <c>ToolBase.RayCast</c> does (ToolBase.cs:562-600):
    /// terrain counts <see cref="TerrainBias"/> farther so a road or roof at nearly the same spot wins.
    /// </summary>
    internal struct SurfacePick
    {
        /// <summary>Metres added to a terrain hit (ToolBase.cs:564).</summary>
        public const float TerrainBias = 100f;

        /// <summary>The current pick.</summary>
        public DropSurface Surface;

        /// <summary>Raw distance of the current pick from the ray origin.</summary>
        public float Distance;

        /// <summary>True once something was picked.</summary>
        public bool Hit { get { return Surface != DropSurface.None; } }

        /// <summary>True when this hit becomes the pick (strictly nearer by the biased distance).</summary>
        public bool Offer(DropSurface surface, float distance)
        {
            if (surface == DropSurface.None) return false;
            if (Hit && Effective(surface, distance) >= Effective(Surface, Distance)) return false;
            Surface = surface;
            Distance = distance;
            return true;
        }

        private static float Effective(DropSurface s, float d) { return s == DropSurface.Terrain ? d + TerrainBias : d; }
    }
}
