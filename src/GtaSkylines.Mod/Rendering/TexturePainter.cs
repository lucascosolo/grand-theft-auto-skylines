using System;
using GtaSkylines.Game.Interiors;

namespace GtaSkylines.Mod.Rendering
{
    /// <summary>
    /// Paints a <see cref="TextureRecipe"/> into 256 × 256 RGBA32 bytes (row 0 at the bottom, as
    /// <c>Texture2D.LoadRawTextureData</c> expects). Every pattern tiles: structure repeats on divisors of 256 and the
    /// noise is at most ±12 per channel, so seams never differ by more than the noise. Deterministic.
    /// </summary>
    public static class TexturePainter
    {
        /// <summary>Width and height in pixels.</summary>
        public const int Size = 256;
        private const int Bytes = Size * Size * 4;

        /// <summary>A new painted buffer.</summary>
        public static byte[] Paint(TextureRecipe r)
        {
            var b = new byte[Bytes];
            Paint(r, b);
            return b;
        }

        /// <summary>Paints into <paramref name="into"/> (length 256 × 256 × 4).</summary>
        public static void Paint(TextureRecipe r, byte[] into)
        {
            if (into == null || into.Length != Bytes) throw new ArgumentException("buffer must be " + Bytes + " bytes", "into");
            int seed = r.GetHashCode();
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    bool accent;
                    int noise;
                    Pixel(r.Pattern, x, y, seed, out accent, out noise);
                    int rgb = accent ? r.AccentRgb : r.BaseRgb;
                    int i = (y * Size + x) * 4;
                    if (r.Pattern == TexturePattern.WindowSky)
                    {
                        float t = y / 255f;
                        into[i] = Mix(r.AccentRgb >> 16, r.BaseRgb >> 16, t);
                        into[i + 1] = Mix(r.AccentRgb >> 8, r.BaseRgb >> 8, t);
                        into[i + 2] = Mix(r.AccentRgb, r.BaseRgb, t);
                    }
                    else
                    {
                        into[i] = Channel(rgb >> 16, noise);
                        into[i + 1] = Channel(rgb >> 8, noise);
                        into[i + 2] = Channel(rgb, noise);
                    }
                    into[i + 3] = 255;
                }
            }
        }

        /// <summary>Mean colour of an RGBA buffer (the unlit fallback for self-lit recipes).</summary>
        public static void AverageRgb(byte[] rgba, out byte r, out byte g, out byte b)
        {
            long sr = 0, sg = 0, sb = 0;
            int n = rgba.Length / 4;
            for (int i = 0; i < n * 4; i += 4)
            {
                sr += rgba[i];
                sg += rgba[i + 1];
                sb += rgba[i + 2];
            }
            if (n == 0) n = 1;
            r = (byte)(sr / n);
            g = (byte)(sg / n);
            b = (byte)(sb / n);
        }

        private static void Pixel(TexturePattern p, int x, int y, int seed, out bool accent, out int noise)
        {
            accent = false;
            noise = 0;
            switch (p)
            {
                case TexturePattern.Plain:
                    noise = (int)(Hash(x, y, seed) * 4f);
                    break;
                case TexturePattern.Carpet:
                    noise = (int)(Hash(x, y, seed) * 10f);
                    break;
                case TexturePattern.Concrete:
                    noise = (int)(Blotch(x, y, seed) * 8f + Hash(x, y, seed) * 4f);
                    break;
                case TexturePattern.Brushed:
                    noise = (int)(Hash(0, y, seed) * 8f + Hash(x, y, seed) * 3f);
                    break;
                case TexturePattern.Checker:
                    accent = ((x / 32 + y / 32) & 1) == 1;
                    noise = (int)(Hash(x, y, seed) * 3f);
                    break;
                case TexturePattern.Tiles:
                    accent = x % 64 < 3 || y % 64 < 3;
                    noise = (int)(Hash(x, y, seed) * 5f);
                    break;
                case TexturePattern.Planks:
                    accent = y % 32 < 2 || (x + ((y / 32) % 2) * 64) % 128 < 2;
                    noise = (int)(Hash(0, y, seed) * 6f + Hash(x, y, seed) * 4f);
                    break;
                case TexturePattern.Bricks:
                    accent = y % 32 < 3 || (x + ((y / 32) % 2) * 32) % 64 < 3;
                    noise = (int)(Hash(x / 64, y / 32, seed) * 6f + Hash(x, y, seed) * 4f);
                    break;
                case TexturePattern.Stripes:
                    accent = x % 32 >= 16;
                    noise = (int)(Hash(x, y, seed) * 3f);
                    break;
                case TexturePattern.LightPanel:
                    accent = x < 8 || x >= Size - 8 || y < 8 || y >= Size - 8;
                    break;
            }
        }

        // Value noise on an 8 × 8 lattice of 32 px cells, wrapping at 256 so the texture tiles.
        private static float Blotch(int x, int y, int seed)
        {
            int cx = x / 32, cy = y / 32;
            float fx = (x % 32) / 32f, fy = (y % 32) / 32f;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(cx, cy, seed + 7), b = Hash((cx + 1) & 7, cy, seed + 7);
            float c = Hash(cx, (cy + 1) & 7, seed + 7), d = Hash((cx + 1) & 7, (cy + 1) & 7, seed + 7);
            return (a + (b - a) * fx) * (1f - fy) + (c + (d - c) * fx) * fy;
        }

        // Integer hash to [-1, 1].
        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
                h = (h ^ (h >> 13)) * 1103515245u;
                h ^= h >> 16;
                return (h & 0xFFFF) / 32767.5f - 1f;
            }
        }

        private static byte Channel(int c, int noise)
        {
            int v = (c & 0xFF) + noise;
            return (byte)(v < 0 ? 0 : v > 255 ? 255 : v);
        }

        private static byte Mix(int a, int b, float t)
        {
            a &= 0xFF;
            b &= 0xFF;
            return (byte)Math.Round(a + (b - a) * t);
        }
    }
}
