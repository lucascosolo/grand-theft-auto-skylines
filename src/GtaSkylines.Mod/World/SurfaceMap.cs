using GtaSkylines.Game.Physics;

namespace GtaSkylines.Mod.World
{
    /// <summary>
    /// Skylines.Host geometry flags (terrain 1, <c>NetGeometry.RoadSurfaceFlag</c> 2, <c>BridgeDeckFlag</c> 4,
    /// <c>BuildingGeometry.BuildingFlag</c> 8, <c>NetGeometry.RailingFlag</c> 16, <c>TunnelFlag</c> 32,
    /// <c>ObstacleGeometry.VegetationFlag</c> 64, <c>PropFlag</c> 128, <c>AreaBoundary.BoundaryFlag</c> 256) to the
    /// Game's <see cref="SurfaceFlags"/>. Pavements are emitted as road surface by the Host, so they map to Road. Pure.
    /// </summary>
    public static class SurfaceMap
    {
        /// <summary>The Game flags for one triangle.</summary>
        public static SurfaceFlags ToSurface(ushort hostFlags)
        {
            SurfaceFlags f = SurfaceFlags.None;
            if ((hostFlags & 1) != 0) f |= SurfaceFlags.Terrain;
            if ((hostFlags & 2) != 0) f |= SurfaceFlags.Road;
            if ((hostFlags & 4) != 0) f |= SurfaceFlags.Road | SurfaceFlags.Bridge;
            if ((hostFlags & 8) != 0) f |= SurfaceFlags.Building;
            if ((hostFlags & 16) != 0) f |= SurfaceFlags.Bridge;
            if ((hostFlags & 32) != 0) f |= SurfaceFlags.Road;
            if ((hostFlags & 64) != 0) f |= SurfaceFlags.Tree | SurfaceFlags.NoCamera;
            if ((hostFlags & 128) != 0) f |= SurfaceFlags.Prop | SurfaceFlags.NoCamera;
            if ((hostFlags & 256) != 0) f |= SurfaceFlags.NoCamera;
            return f;
        }
    }
}
