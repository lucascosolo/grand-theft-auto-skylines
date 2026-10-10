using System;

namespace GtaSkylines.Mod.Hud
{
    /// <summary>Icons of the HUD atlas (white shapes on transparent, tinted when drawn).</summary>
    public enum Icon
    {
        /// <summary>Solid white (rectangles, bars).</summary>
        White = 0,
        /// <summary>Filled disc.</summary>
        Disc,
        /// <summary>Ring.</summary>
        Ring,
        /// <summary>Player arrow pointing up.</summary>
        Arrow,
        /// <summary>Filled five-point star.</summary>
        Star,
        /// <summary>Star outline.</summary>
        StarOutline,
        /// <summary>Rounded square.</summary>
        Square,
        /// <summary>House (safehouse).</summary>
        House,
        /// <summary>Diamond (waypoint).</summary>
        Diamond,
        /// <summary>Downward chevron (objective marker).</summary>
        Chevron,
        /// <summary>Soft round glow.</summary>
        Glow,
        /// <summary>Rounded rectangle outline (key caps, phone frame).</summary>
        Frame,
        /// <summary>Shopping bag (shops).</summary>
        Bag,
        /// <summary>Speech bubble (texts).</summary>
        Bubble,
        /// <summary>Bird-ish chirp (Chirper).</summary>
        Chirp,
        /// <summary>Radial vignette: transparent centre, opaque edges.</summary>
        Vignette,
    }

    /// <summary>
    /// Paints the HUD's icon atlas into RGBA32 bytes: 4 × 4 cells of <see cref="Cell"/> pixels, alpha from 4 × 4
    /// supersampled coverage, colour white. Row 0 is the bottom (Unity texture order). Unity-free.
    /// </summary>
    public static class IconAtlas
    {
        /// <summary>Cell size in pixels.</summary>
        public const int Cell = 64;
        /// <summary>Cells per side.</summary>
        public const int Cells = 4;
        /// <summary>Atlas side in pixels.</summary>
        public const int Size = Cell * Cells;

        /// <summary>Normalised UV rectangle (x, y, w, h) of an icon, inset half a pixel against bleeding.</summary>
        public static void Uv(Icon icon, out float x, out float y, out float w, out float h)
        {
            int i = (int)icon;
            float inset = 0.5f / Size;
            x = (i % Cells) / (float)Cells + inset;
            y = (Cells - 1 - i / Cells) / (float)Cells + inset;
            w = 1f / Cells - 2f * inset;
            h = w;
        }

        /// <summary>The atlas bytes, Size² × 4.</summary>
        public static byte[] Paint()
        {
            var px = new byte[Size * Size * 4];
            for (int icon = 0; icon < Cells * Cells; icon++)
            {
                int ox = (icon % Cells) * Cell;
                int oy = (Cells - 1 - icon / Cells) * Cell;
                for (int j = 0; j < Cell; j++)
                    for (int i = 0; i < Cell; i++)
                    {
                        int hits = 0;
                        for (int sy = 0; sy < 4; sy++)
                            for (int sx = 0; sx < 4; sx++)
                            {
                                // u right, v up, both in [-1, 1]
                                float u = ((i + (sx + 0.5f) / 4f) / Cell) * 2f - 1f;
                                float v = ((j + (sy + 0.5f) / 4f) / Cell) * 2f - 1f;
                                if (Inside((Icon)icon, u, v)) hits++;
                            }
                        float a = (Icon)icon == Icon.Glow ? Glow(i, j) : (Icon)icon == Icon.Vignette ? Vignette(i, j) : hits / 16f;
                        int k = ((oy + j) * Size + ox + i) * 4;
                        px[k] = px[k + 1] = px[k + 2] = 255;
                        px[k + 3] = (byte)(a * 255f + 0.5f);
                    }
            }
            return px;
        }

        private static float Glow(int i, int j)
        {
            float r = Radius(i, j);
            return r >= 1f ? 0f : (1f - r) * (1f - r);
        }

        private static float Vignette(int i, int j)
        {
            float r = Radius(i, j) / 1.2f;
            return r < 0.45f ? 0f : Math.Min(1f, (r - 0.45f) / 0.55f);
        }

        private static float Radius(int i, int j)
        {
            float u = (i + 0.5f) / Cell * 2f - 1f, v = (j + 0.5f) / Cell * 2f - 1f;
            return (float)Math.Sqrt(u * u + v * v);
        }

        private static bool Inside(Icon icon, float u, float v)
        {
            float r = (float)Math.Sqrt(u * u + v * v);
            switch (icon)
            {
                case Icon.White: return true;
                case Icon.Disc: return r <= 0.9f;
                case Icon.Ring: return r <= 0.9f && r >= 0.62f;
                case Icon.Arrow: return Tri(u, v, 0f, 0.92f, 0.72f, -0.82f, -0.72f, -0.82f) && !Tri(u, v, 0f, -0.38f, 0.75f, -0.95f, -0.75f, -0.95f);
                case Icon.Star: return Star(u, v, 0.92f, 0.38f);
                case Icon.StarOutline: return Star(u, v, 0.92f, 0.38f) && !Star(u, v * 1.02f + 0.04f, 0.66f, 0.27f);
                case Icon.Square: return RoundRect(u, v, 0.75f, 0.75f, 0.2f);
                case Icon.House:
                    return (Math.Abs(u) <= 0.62f && v >= -0.8f && v <= 0.15f && !(Math.Abs(u) <= 0.18f && v <= -0.25f))
                        || Tri(u, v, 0f, 0.88f, 0.9f, 0.1f, -0.9f, 0.1f);
                case Icon.Diamond: return Math.Abs(u) + Math.Abs(v) <= 0.9f;
                case Icon.Chevron: return Tri(u, v, -0.85f, 0.6f, 0.85f, 0.6f, 0f, -0.7f) && !Tri(u, v, -0.45f, 0.62f, 0.45f, 0.62f, 0f, -0.05f);
                case Icon.Frame: return RoundRect(u, v, 0.95f, 0.95f, 0.3f) && !RoundRect(u, v, 0.8f, 0.8f, 0.18f);
                case Icon.Bag:
                    float hd = (float)Math.Sqrt(u * u + (v - 0.4f) * (v - 0.4f));
                    return RoundRect(u, v + 0.2f, 0.7f, 0.6f, 0.12f) || (hd <= 0.38f && hd >= 0.24f && v >= 0.4f);
                case Icon.Bubble: return RoundRect(u, v - 0.12f, 0.88f, 0.62f, 0.3f) || Tri(u, v, -0.5f, -0.3f, -0.1f, -0.3f, -0.6f, -0.85f);
                case Icon.Chirp:
                    return (u * u / 0.5f + (v + 0.1f) * (v + 0.1f) / 0.35f <= 1f)
                        || (u - 0.45f) * (u - 0.45f) + (v - 0.35f) * (v - 0.35f) <= 0.09f
                        || Tri(u, v, 0.7f, 0.42f, 0.95f, 0.35f, 0.7f, 0.25f);
                default: return false;
            }
        }

        private static bool RoundRect(float u, float v, float hw, float hh, float rad)
        {
            float dx = Math.Abs(u) - (hw - rad), dy = Math.Abs(v) - (hh - rad);
            if (dx <= 0f && Math.Abs(v) <= hh) return true;
            if (dy <= 0f && Math.Abs(u) <= hw) return true;
            return dx * dx + dy * dy <= rad * rad;
        }

        private static bool Tri(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d1 = (px - bx) * (ay - by) - (ax - bx) * (py - by);
            float d2 = (px - cx) * (by - cy) - (bx - cx) * (py - cy);
            float d3 = (px - ax) * (cy - ay) - (cx - ax) * (py - ay);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        private static bool Star(float u, float v, float outer, float inner)
        {
            // Point-in-polygon on the 10-vertex star, first point at the top.
            bool inside = false;
            for (int k = 0, m = 9; k < 10; m = k++)
            {
                float ak = (float)(Math.PI / 2 + k * Math.PI / 5), am = (float)(Math.PI / 2 + m * Math.PI / 5);
                float rk = k % 2 == 0 ? outer : inner, rm = m % 2 == 0 ? outer : inner;
                float xk = rk * (float)Math.Cos(ak), yk = rk * (float)Math.Sin(ak) - 0.06f;
                float xm = rm * (float)Math.Cos(am), ym = rm * (float)Math.Sin(am) - 0.06f;
                if ((yk > v) != (ym > v) && u < (xm - xk) * (v - yk) / (ym - yk) + xk) inside = !inside;
            }
            return inside;
        }
    }
}
