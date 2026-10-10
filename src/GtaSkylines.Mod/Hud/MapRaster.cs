using System;

namespace GtaSkylines.Mod.Hud
{
    /// <summary>
    /// The city map as an RGBA32 pixel buffer, painted once per city load (and when the road graph changes) and uploaded
    /// with <c>Texture2D.LoadRawTextureData</c>. Square: <see cref="Pixels"/> × <see cref="Pixels"/>, covering
    /// [MinX, MinX + Size] × [MinZ, MinZ + Size] world metres. Row 0 is the south edge (z = MinZ), so the buffer maps to a
    /// Unity texture with v = 0 at the bottom. Pixel (i, j) covers x ∈ [MinX + i·mpp, MinX + (i+1)·mpp), z likewise with
    /// j; its centre is at +0.5 pixel. Colours are 0xRRGGBBAA; byte order in <see cref="Rgba"/> is R, G, B, A at index
    /// (j·Pixels + i)·4. Every paint call clips to the buffer and never throws for geometry outside it. Unity-free.
    /// </summary>
    public sealed class MapRaster
    {
        /// <summary>Side in pixels.</summary>
        public readonly int Pixels;
        /// <summary>West edge, metres.</summary>
        public readonly float MinX;
        /// <summary>South edge, metres.</summary>
        public readonly float MinZ;
        /// <summary>Metres per pixel.</summary>
        public readonly float MetresPerPixel;
        /// <summary>RGBA32 bytes, Pixels² × 4.</summary>
        public readonly byte[] Rgba;

        private const double Eps = 1e-6;

        private static int ClampInt(double v)
        {
            return v < -1e9 ? -1000000000 : v > 1e9 ? 1000000000 : (int)v;
        }

        private static double Dist(float x0, float z0, float x1, float z1)
        {
            double dx = x1 - x0, dz = z1 - z0;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Creates a buffer (all bytes 0). pixels must be ≥ 1 and metresPerPixel &gt; 0.</summary>
        /// <exception cref="ArgumentOutOfRangeException">pixels &lt; 1 or metresPerPixel not &gt; 0.</exception>
        public MapRaster(int pixels, float minX, float minZ, float metresPerPixel)
        {
            if (pixels < 1) throw new ArgumentOutOfRangeException("pixels");
            if (!(metresPerPixel > 0f)) throw new ArgumentOutOfRangeException("metresPerPixel");
            Pixels = pixels;
            MinX = minX;
            MinZ = minZ;
            MetresPerPixel = metresPerPixel;
            Rgba = new byte[pixels * pixels * 4];
        }

        /// <summary>World side length covered, metres (Pixels × MetresPerPixel).</summary>
        public float Size { get { return Pixels * MetresPerPixel; } }

        /// <summary>
        /// A raster covering the box [minX, maxX] × [minZ, maxZ] grown to a square around its centre plus
        /// <paramref name="margin"/> metres on every side, at <paramref name="metresPerPixel"/>; when that would exceed
        /// <paramref name="maxPixels"/> the metres per pixel grow so the side is exactly maxPixels. Pixels =
        /// ceil(side / mpp) (at least 1). The square's centre equals the box centre.
        /// </summary>
        public static MapRaster ForArea(float minX, float minZ, float maxX, float maxZ, float margin, float metresPerPixel, int maxPixels)
        {
            float side = Math.Max(maxX - minX, maxZ - minZ) + 2f * margin;
            int pixels = Math.Max(1, (int)Math.Ceiling(side / metresPerPixel));
            if (pixels > maxPixels)
            {
                pixels = Math.Max(1, maxPixels);
                metresPerPixel = side / pixels;
            }
            float half = pixels * metresPerPixel / 2f;
            return new MapRaster(pixels, (minX + maxX) / 2f - half, (minZ + maxZ) / 2f - half, metresPerPixel);
        }

        /// <summary>Fills every pixel with <paramref name="rgba"/>.</summary>
        public void Clear(uint rgba)
        {
            byte r = (byte)(rgba >> 24), g = (byte)(rgba >> 16), b = (byte)(rgba >> 8), a = (byte)rgba;
            for (int k = 0; k < Rgba.Length; k += 4)
            {
                Rgba[k] = r;
                Rgba[k + 1] = g;
                Rgba[k + 2] = b;
                Rgba[k + 3] = a;
            }
        }

        /// <summary>The colour of pixel (i, j) as 0xRRGGBBAA; 0 outside the buffer.</summary>
        public uint Get(int i, int j)
        {
            if (i < 0 || j < 0 || i >= Pixels || j >= Pixels) return 0;
            int k = (j * Pixels + i) * 4;
            return ((uint)Rgba[k] << 24) | ((uint)Rgba[k + 1] << 16) | ((uint)Rgba[k + 2] << 8) | Rgba[k + 3];
        }

        /// <summary>Sets pixel (i, j); ignored outside the buffer.</summary>
        public void Set(int i, int j, uint rgba)
        {
            if (i < 0 || j < 0 || i >= Pixels || j >= Pixels) return;
            int k = (j * Pixels + i) * 4;
            Rgba[k] = (byte)(rgba >> 24);
            Rgba[k + 1] = (byte)(rgba >> 16);
            Rgba[k + 2] = (byte)(rgba >> 8);
            Rgba[k + 3] = (byte)rgba;
        }

        /// <summary>The pixel containing world (x, z) (floor); may be outside the buffer.</summary>
        public void ToPixel(float x, float z, out int i, out int j)
        {
            i = ClampInt(Math.Floor((x - MinX) / (double)MetresPerPixel));
            j = ClampInt(Math.Floor((z - MinZ) / (double)MetresPerPixel));
        }

        /// <summary>
        /// Fills, cell by cell, the square blocks of <paramref name="cellPixels"/> × <paramref name="cellPixels"/> pixels
        /// (the last row/column of cells may be partial) whose world centre (the centre of the block's full square) makes
        /// <paramref name="predicate"/>(x, z) true. cellPixels &lt; 1 counts as 1. Used for water.
        /// </summary>
        public void FillCellsWhere(Func<float, float, bool> predicate, int cellPixels, uint rgba)
        {
            int c = Math.Max(1, cellPixels);
            for (int cj = 0; cj < Pixels; cj += c)
            {
                for (int ci = 0; ci < Pixels; ci += c)
                {
                    if (!predicate(MinX + (ci + c * 0.5f) * MetresPerPixel, MinZ + (cj + c * 0.5f) * MetresPerPixel)) continue;
                    int iEnd = Math.Min(Pixels, ci + c), jEnd = Math.Min(Pixels, cj + c);
                    for (int j = cj; j < jEnd; j++)
                        for (int i = ci; i < iEnd; i++) Set(i, j, rgba);
                }
            }
        }

        /// <summary>
        /// Fills the pixels whose centres lie inside the rectangle centred at (cx, cz), <paramref name="width"/> metres
        /// across its local x and <paramref name="length"/> along its local z, rotated by <paramref name="yawDeg"/> (local
        /// +z points to yaw: (sin yaw, cos yaw)). Edges inclusive. A rectangle smaller than one pixel still fills the
        /// pixel containing its centre (so small buildings show). Buildings and parks.
        /// </summary>
        public void FillRect(float cx, float cz, float yawDeg, float width, float length, uint rgba)
        {
            double yaw = yawDeg * Math.PI / 180.0;
            double s = Math.Sin(yaw), c = Math.Cos(yaw);
            double hw = width / 2.0 + Eps, hl = length / 2.0 + Eps;
            double ex = Math.Abs(c) * hw + Math.Abs(s) * hl, ez = Math.Abs(s) * hw + Math.Abs(c) * hl;
            int i0, j0, i1, j1;
            ToPixel((float)(cx - ex), (float)(cz - ez), out i0, out j0);
            ToPixel((float)(cx + ex), (float)(cz + ez), out i1, out j1);
            bool any = false;
            for (int j = Math.Max(0, j0); j <= Math.Min(Pixels - 1, j1); j++)
            {
                double dz = MinZ + (j + 0.5) * MetresPerPixel - cz;
                for (int i = Math.Max(0, i0); i <= Math.Min(Pixels - 1, i1); i++)
                {
                    double dx = MinX + (i + 0.5) * MetresPerPixel - cx;
                    if (Math.Abs(dx * c - dz * s) > hw || Math.Abs(dx * s + dz * c) > hl) continue;
                    Set(i, j, rgba);
                    any = true;
                }
            }
            if (any) return;
            int ci, cj;
            ToPixel(cx, cz, out ci, out cj);
            Set(ci, cj, rgba);
        }

        /// <summary>
        /// Fills the capsule of the given total <paramref name="width"/> around segment (x0, z0)–(x1, z1): every pixel whose
        /// centre is within width/2 of the segment (inclusive). Width below one pixel is drawn one pixel wide (every pixel
        /// the segment passes through is set). Round caps make polylines join cleanly.
        /// </summary>
        public void DrawLine(float x0, float z0, float x1, float z1, float width, uint rgba)
        {
            if (width < MetresPerPixel)
            {
                double len = Math.Sqrt((double)(x1 - x0) * (x1 - x0) + (double)(z1 - z0) * (z1 - z0));
                int steps = (int)Math.Min(1e7, Math.Ceiling(len / (MetresPerPixel * 0.25)));
                for (int n = 0; n <= steps; n++)
                {
                    double t = steps == 0 ? 0.0 : (double)n / steps;
                    int pi, pj;
                    ToPixel((float)(x0 + (x1 - x0) * t), (float)(z0 + (z1 - z0) * t), out pi, out pj);
                    Set(pi, pj, rgba);
                }
                return;
            }
            double r = width / 2.0 + Eps;
            int i0, j0, i1, j1;
            ToPixel((float)(Math.Min(x0, x1) - r), (float)(Math.Min(z0, z1) - r), out i0, out j0);
            ToPixel((float)(Math.Max(x0, x1) + r), (float)(Math.Max(z0, z1) + r), out i1, out j1);
            double sx = x1 - x0, sz = z1 - z0, len2 = sx * sx + sz * sz;
            for (int j = Math.Max(0, j0); j <= Math.Min(Pixels - 1, j1); j++)
            {
                double pz = MinZ + (j + 0.5) * MetresPerPixel - z0;
                for (int i = Math.Max(0, i0); i <= Math.Min(Pixels - 1, i1); i++)
                {
                    double px = MinX + (i + 0.5) * MetresPerPixel - x0;
                    double t = len2 > 0.0 ? Math.Max(0.0, Math.Min(1.0, (px * sx + pz * sz) / len2)) : 0.0;
                    double ox = px - sx * t, oz = pz - sz * t;
                    if (ox * ox + oz * oz <= r * r) Set(i, j, rgba);
                }
            }
        }

        /// <summary>
        /// Draws the cubic Bézier a·u³ + 3b·u²t + 3c·ut² + d·t³ (x, z of each control point) as
        /// max(1, ceil(chord length / <paramref name="stepMetres"/>)) connected <see cref="DrawLine"/> pieces, where chord
        /// length = |a−b| + |b−c| + |c−d|.
        /// </summary>
        public void DrawBezier(float ax, float az, float bx, float bz, float cx, float cz, float dx, float dz, float width, float stepMetres, uint rgba)
        {
            double chord = Dist(ax, az, bx, bz) + Dist(bx, bz, cx, cz) + Dist(cx, cz, dx, dz);
            int n = Math.Max(1, (int)Math.Min(1e6, Math.Ceiling(chord / stepMetres)));
            float px = ax, pz = az;
            for (int k = 1; k <= n; k++)
            {
                float t = (float)k / n, u = 1f - t;
                float qx = u * u * u * ax + 3f * u * u * t * bx + 3f * u * t * t * cx + t * t * t * dx;
                float qz = u * u * u * az + 3f * u * u * t * bz + 3f * u * t * t * cz + t * t * t * dz;
                if (k == n) { qx = dx; qz = dz; }
                DrawLine(px, pz, qx, qz, width, rgba);
                px = qx;
                pz = qz;
            }
        }
    }

    /// <summary>Road drawing rules of the map. Unity-free.</summary>
    public static class MapStyle
    {
        /// <summary>Land.</summary>
        public const uint Land = 0x3A3F44FF;
        /// <summary>Water.</summary>
        public const uint Water = 0x3E6C8CFF;
        /// <summary>Parks.</summary>
        public const uint Park = 0x4F7A4AFF;
        /// <summary>Buildings.</summary>
        public const uint Building = 0x2B2F33FF;
        /// <summary>Ordinary roads.</summary>
        public const uint Road = 0xC9CDD1FF;
        /// <summary>Highways.</summary>
        public const uint Highway = 0xD8C27AFF;
        /// <summary>Footpaths.</summary>
        public const uint Path = 0x6E747AFF;

        /// <summary>
        /// Drawn width of a road, metres: (forwardLanes + backwardLanes) × laneWidth (laneWidth ≤ 0 or not finite → 3),
        /// at least 6 for drivable roads; pedestrian paths are 3 regardless of lanes.
        /// </summary>
        public static float RoadWidth(int forwardLanes, int backwardLanes, float laneWidth, bool pedestrianOnly)
        {
            if (pedestrianOnly) return 3f;
            if (!(laneWidth > 0f) || float.IsInfinity(laneWidth)) laneWidth = 3f;
            return Math.Max(6f, (forwardLanes + backwardLanes) * laneWidth);
        }
    }
}
