using System;

namespace GtaSkylines.Mod.Menus
{
    /// <summary>Weapon wheel selection from a direction (screen space: x right, y down). Unity-free.</summary>
    public static class RadialMath
    {
        /// <summary>
        /// The segment under direction (dx, dy) of a wheel of <paramref name="segments"/> equal segments, segment 0
        /// centred on screen-up (dy &lt; 0), numbered clockwise; −1 when the direction's length is below
        /// <paramref name="deadZone"/> or segments &lt; 1. A direction exactly on a boundary belongs to the clockwise
        /// (higher-index, wrapping) segment.
        /// </summary>
        public static int Segment(float dx, float dy, int segments, float deadZone)
        {
            if (segments < 1 || Math.Sqrt((double)dx * dx + (double)dy * dy) < deadZone) return -1;
            double angle = Math.Atan2(dx, -dy) * 180.0 / Math.PI;
            if (angle < 0.0) angle += 360.0;
            double width = 360.0 / segments;
            return (int)Math.Floor((angle + width / 2.0 + 1e-7) / width) % segments;
        }

        /// <summary>Centre angle of a segment in degrees clockwise from screen-up: index × 360 / segments.</summary>
        public static float CentreDeg(int index, int segments)
        {
            return index * 360f / segments;
        }

        /// <summary>
        /// Adds a mouse delta to the accumulated stick (x, y) and limits its length to <paramref name="maxLength"/>
        /// (scaling it back onto the circle), so the selection follows the mouse direction like GTA's wheel.
        /// </summary>
        public static void Accumulate(ref float x, ref float y, float dx, float dy, float maxLength)
        {
            x += dx;
            y += dy;
            float len = (float)Math.Sqrt((double)x * x + (double)y * y);
            if (len <= maxLength) return;
            float k = maxLength / len;
            x *= k;
            y *= k;
        }
    }
}
