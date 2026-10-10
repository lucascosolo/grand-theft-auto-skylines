using System;

namespace GtaSkylines.Mod.Hud
{
    /// <summary>
    /// A rotated, zoomed view of the city around a centre, drawn into a W × H pixel rectangle (y down). World axes are
    /// CS1's x (east) and z (north); yaw follows <c>Vec3.YawDegOf</c>: 0 = +z, 90 = +x, clockwise seen from above. The
    /// view's "up" on screen is the yaw direction (north-up is yaw 0). The centre sits at pixel (W·AnchorX, H·AnchorY);
    /// pixels per metre = (H / 2) / Radius. Unity-free.
    /// </summary>
    public struct MinimapView
    {
        /// <summary>World x at the anchor.</summary>
        public float CentreX;
        /// <summary>World z at the anchor.</summary>
        public float CentreZ;
        /// <summary>Screen-up direction in yaw degrees.</summary>
        public float YawDeg;
        /// <summary>Metres from the anchor to H/2 pixels away (HudModel.MinimapRadius). Must be &gt; 0.</summary>
        public float Radius;
        /// <summary>Rectangle width, pixels.</summary>
        public float W;
        /// <summary>Rectangle height, pixels.</summary>
        public float H;
        /// <summary>Anchor x as a fraction of W (0.5 for the minimap).</summary>
        public float AnchorX;
        /// <summary>Anchor y as a fraction of H (GTA puts the player below centre: 0.62).</summary>
        public float AnchorY;
    }

    /// <summary>World ↔ minimap transforms, edge clamping and texture coordinates. Unity-free.</summary>
    public static class MinimapMath
    {
        /// <summary>The player's anchor height on the minimap (fraction of its height from the top).</summary>
        public const float PlayerAnchorY = 0.62f;

        /// <summary>
        /// World (x, z) to pixel (px, py) inside the view rectangle: with forward f = (sin yaw, cos yaw) and right
        /// r = (cos yaw, −sin yaw) in (x, z), d = (x − CentreX, z − CentreZ), ppm = (H/2)/Radius:
        /// px = W·AnchorX + (d·r)·ppm, py = H·AnchorY − (d·f)·ppm.
        /// </summary>
        public static void WorldToView(ref MinimapView v, float x, float z, out float px, out float py)
        {
            double yaw = v.YawDeg * Math.PI / 180.0;
            double s = Math.Sin(yaw), c = Math.Cos(yaw);
            double dx = x - v.CentreX, dz = z - v.CentreZ;
            double ppm = (v.H / 2.0) / v.Radius;
            px = (float)(v.W * v.AnchorX + (dx * c - dz * s) * ppm);
            py = (float)(v.H * v.AnchorY - (dx * s + dz * c) * ppm);
        }

        /// <summary>Inverse of <see cref="WorldToView"/>.</summary>
        public static void ViewToWorld(ref MinimapView v, float px, float py, out float x, out float z)
        {
            double yaw = v.YawDeg * Math.PI / 180.0;
            double s = Math.Sin(yaw), c = Math.Cos(yaw);
            double ppm = (v.H / 2.0) / v.Radius;
            double dr = (px - v.W * v.AnchorX) / ppm;
            double df = (v.H * v.AnchorY - py) / ppm;
            x = (float)(v.CentreX + dr * c + df * s);
            z = (float)(v.CentreZ - dr * s + df * c);
        }

        /// <summary>
        /// Clamps a blip at (px, py) to the view rectangle shrunk by <paramref name="inset"/> on every side. Inside (edges
        /// inclusive): unchanged, returns false. Outside: moves it along the straight line towards the anchor
        /// (W·AnchorX, H·AnchorY) to the first point on the shrunk rectangle's border, returns true.
        /// </summary>
        public static bool ClampToEdge(ref MinimapView v, float inset, ref float px, ref float py)
        {
            float minX = inset, maxX = v.W - inset, minY = inset, maxY = v.H - inset;
            if (px >= minX && px <= maxX && py >= minY && py <= maxY) return false;
            float ax = v.W * v.AnchorX, ay = v.H * v.AnchorY;
            float dx = px - ax, dy = py - ay;
            float t = float.MaxValue;
            if (dx > 0f) t = Math.Min(t, (maxX - ax) / dx);
            else if (dx < 0f) t = Math.Min(t, (minX - ax) / dx);
            if (dy > 0f) t = Math.Min(t, (maxY - ay) / dy);
            else if (dy < 0f) t = Math.Min(t, (minY - ay) / dy);
            t = Math.Max(0f, Math.Min(1f, t));
            px = ax + dx * t;
            py = ay + dy * t;
            return true;
        }

        /// <summary>
        /// Screen angle in degrees (clockwise from screen-up) of something facing world yaw <paramref name="worldYawDeg"/>
        /// in this view: worldYawDeg − YawDeg, normalised to [0, 360).
        /// </summary>
        public static float ScreenAngle(ref MinimapView v, float worldYawDeg)
        {
            double a = worldYawDeg - v.YawDeg;
            a -= 360.0 * Math.Floor(a / 360.0);
            float f = (float)a;
            return f >= 360f ? 0f : f;
        }

        /// <summary>
        /// World (x, z) to normalised texture coordinates of a map raster covering [minX, minX + size] × [minZ, minZ +
        /// size]: u = (x − minX)/size, v = (z − minZ)/size (texture v = 0 is the south edge). Not clamped.
        /// </summary>
        public static void WorldToTexture(float minX, float minZ, float size, float x, float z, out float u, out float v)
        {
            u = (x - minX) / size;
            v = (z - minZ) / size;
        }
    }
}
