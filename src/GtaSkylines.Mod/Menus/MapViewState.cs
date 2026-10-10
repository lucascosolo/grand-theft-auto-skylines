using System;

namespace GtaSkylines.Mod.Menus
{
    /// <summary>
    /// Pan and zoom of the pause-menu map (north up). Screen pixels have y down; the view's centre (CentreX, CentreZ) sits
    /// at the screen rectangle's centre; world z grows upward on screen. Unity-free.
    /// </summary>
    public sealed class MapViewState
    {
        /// <summary>Closest zoom, metres per pixel.</summary>
        public const float MinMetresPerPixel = 0.5f;
        /// <summary>Farthest zoom, metres per pixel.</summary>
        public const float MaxMetresPerPixel = 32f;

        /// <summary>World x at the screen centre.</summary>
        public float CentreX;
        /// <summary>World z at the screen centre.</summary>
        public float CentreZ;
        /// <summary>Zoom, metres per pixel, within [Min, Max].</summary>
        public float MetresPerPixel = 4f;

        /// <summary>World point under screen pixel (sx, sy) of a <paramref name="w"/> × <paramref name="h"/> map rectangle (pixel coordinates relative to its top-left).</summary>
        public void ScreenToWorld(float w, float h, float sx, float sy, out float x, out float z)
        {
            x = CentreX + (sx - w / 2f) * MetresPerPixel;
            z = CentreZ - (sy - h / 2f) * MetresPerPixel;
        }

        /// <summary>Inverse of <see cref="ScreenToWorld"/>.</summary>
        public void WorldToScreen(float w, float h, float x, float z, out float sx, out float sy)
        {
            sx = w / 2f + (x - CentreX) / MetresPerPixel;
            sy = h / 2f - (z - CentreZ) / MetresPerPixel;
        }

        /// <summary>Moves the map with a mouse drag of (dxPixels, dyPixels): the world point under the cursor follows it.</summary>
        public void Drag(float dxPixels, float dyPixels)
        {
            CentreX -= dxPixels * MetresPerPixel;
            CentreZ += dyPixels * MetresPerPixel;
        }

        /// <summary>Pans by keyboard: (right, up) in screen fractions per second × dt, scaled by the zoom: Centre moves (right·w, up·h)·mpp·dt metres.</summary>
        public void PanKeys(float right, float up, float w, float h, float dt)
        {
            CentreX += right * w * MetresPerPixel * dt;
            CentreZ += up * h * MetresPerPixel * dt;
        }

        /// <summary>
        /// Multiplies MetresPerPixel by <paramref name="factor"/> (clamped to [Min, Max]) keeping the world point under
        /// screen pixel (sx, sy) fixed. factor ≤ 0 or not finite is ignored.
        /// </summary>
        public void Zoom(float factor, float w, float h, float sx, float sy)
        {
            if (!(factor > 0f) || float.IsInfinity(factor)) return;
            float x, z;
            ScreenToWorld(w, h, sx, sy, out x, out z);
            MetresPerPixel = Math.Max(MinMetresPerPixel, Math.Min(MaxMetresPerPixel, MetresPerPixel * factor));
            CentreX = x - (sx - w / 2f) * MetresPerPixel;
            CentreZ = z + (sy - h / 2f) * MetresPerPixel;
        }

        /// <summary>Keeps the centre inside [minX, maxX] × [minZ, maxZ].</summary>
        public void ClampCentre(float minX, float minZ, float maxX, float maxZ)
        {
            CentreX = Math.Max(minX, Math.Min(maxX, CentreX));
            CentreZ = Math.Max(minZ, Math.Min(maxZ, CentreZ));
        }
    }
}
