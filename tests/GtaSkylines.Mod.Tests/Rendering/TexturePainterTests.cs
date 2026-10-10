using System;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Mod.Rendering;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class TexturePainterTests
    {
        const int Base = 0x804020;
        const int Accent = 0x20E0C0;

        static TextureRecipe Recipe(TexturePattern p)
        {
            return new TextureRecipe { Pattern = p, BaseRgb = Base, AccentRgb = Accent, RepeatMetres = 1f };
        }

        static int Ch(int rgb, int c) { return (rgb >> (16 - 8 * c)) & 0xFF; }

        static int Px(byte[] t, int x, int y, int c) { return t[(y * 256 + x) * 4 + c]; }

        public static System.Collections.Generic.IEnumerable<object[]> AllPatterns()
        {
            foreach (TexturePattern p in Enum.GetValues(typeof(TexturePattern))) yield return new object[] { p };
        }

        // Colour the pattern puts at a pixel before noise (WindowSky and LightPanel have none).
        static int Expected(TexturePattern p, int x, int y)
        {
            switch (p)
            {
                case TexturePattern.Checker: return (((x / 32) + (y / 32)) % 2 == 0) ? Base : Accent;
                case TexturePattern.Tiles: return (x % 64 < 3 || y % 64 < 3) ? Accent : Base;
                case TexturePattern.Planks: return (y % 32 < 2 || ((x + ((y / 32) % 2) * 64) % 128) < 2) ? Accent : Base;
                case TexturePattern.Bricks: return (y % 32 < 3 || ((x + ((y / 32) % 2) * 32) % 64) < 3) ? Accent : Base;
                case TexturePattern.Stripes: return (x % 32 < 16) ? Base : Accent;
                case TexturePattern.LightPanel: return (x < 8 || x >= 248 || y < 8 || y >= 248) ? Accent : Base;
                default: return Base;
            }
        }

        [Fact]
        public void Paint_returns_rgba_256_square()
        {
            Assert.Equal(256, TexturePainter.Size);
            Assert.Equal(262144, TexturePainter.Paint(Recipe(TexturePattern.Plain)).Length);
        }

        [Fact]
        public void Paint_into_wrong_size_throws()
        {
            Assert.Throws<ArgumentException>(() => TexturePainter.Paint(Recipe(TexturePattern.Plain), new byte[10]));
            Assert.Throws<ArgumentException>(() => TexturePainter.Paint(Recipe(TexturePattern.Plain), new byte[262145]));
        }

        [Fact]
        public void Paint_into_matches_allocating_overload()
        {
            var r = Recipe(TexturePattern.Bricks);
            var into = new byte[262144];
            TexturePainter.Paint(r, into);
            Assert.Equal(TexturePainter.Paint(r), into);
        }

        [Theory]
        [MemberData(nameof(AllPatterns))]
        public void Every_pattern_is_opaque_and_deterministic(TexturePattern p)
        {
            var a = TexturePainter.Paint(Recipe(p));
            var b = TexturePainter.Paint(Recipe(p));
            Assert.Equal(a, b);
            for (int i = 3; i < a.Length; i += 4) Assert.Equal(255, a[i]);
        }

        [Theory]
        [InlineData(TexturePattern.Plain)]
        [InlineData(TexturePattern.Carpet)]
        [InlineData(TexturePattern.Concrete)]
        [InlineData(TexturePattern.Brushed)]
        [InlineData(TexturePattern.Checker)]
        [InlineData(TexturePattern.Tiles)]
        [InlineData(TexturePattern.Planks)]
        [InlineData(TexturePattern.Bricks)]
        [InlineData(TexturePattern.Stripes)]
        [InlineData(TexturePattern.LightPanel)]
        public void Structure_matches_pattern_within_noise(TexturePattern p)
        {
            var t = TexturePainter.Paint(Recipe(p));
            int tol = p == TexturePattern.LightPanel ? 0 : 12;
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                {
                    int e = Expected(p, x, y);
                    for (int c = 0; c < 3; c++)
                        Assert.True(Math.Abs(Px(t, x, y, c) - Ch(e, c)) <= tol, p + " pixel (" + x + "," + y + ") channel " + c);
                }
        }

        [Theory]
        [InlineData(TexturePattern.Plain)]
        [InlineData(TexturePattern.Carpet)]
        [InlineData(TexturePattern.Concrete)]
        [InlineData(TexturePattern.Brushed)]
        public void Noise_only_patterns_average_to_base(TexturePattern p)
        {
            byte r, g, b;
            TexturePainter.AverageRgb(TexturePainter.Paint(Recipe(p)), out r, out g, out b);
            Assert.InRange(r, Ch(Base, 0) - 8, Ch(Base, 0) + 8);
            Assert.InRange(g, Ch(Base, 1) - 8, Ch(Base, 1) + 8);
            Assert.InRange(b, Ch(Base, 2) - 8, Ch(Base, 2) + 8);
        }

        [Theory]
        [InlineData(TexturePattern.Plain)]
        [InlineData(TexturePattern.Carpet)]
        [InlineData(TexturePattern.Concrete)]
        [InlineData(TexturePattern.Brushed)]
        public void Noisy_patterns_tile_without_a_seam(TexturePattern p)
        {
            var t = TexturePainter.Paint(Recipe(p));
            for (int i = 0; i < 256; i++)
                for (int c = 0; c < 3; c++)
                {
                    Assert.True(Math.Abs(Px(t, 255, i, c) - Px(t, 0, i, c)) <= 24, "column seam row " + i);
                    Assert.True(Math.Abs(Px(t, i, 255, c) - Px(t, i, 0, c)) <= 24, "row seam column " + i);
                }
        }

        [Fact]
        public void WindowSky_is_a_vertical_gradient_from_accent_to_base()
        {
            var t = TexturePainter.Paint(Recipe(TexturePattern.WindowSky));
            for (int c = 0; c < 3; c++)
            {
                Assert.Equal(Ch(Accent, c), Px(t, 0, 0, c));
                Assert.Equal(Ch(Base, c), Px(t, 0, 255, c));
            }
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x += 17)
                    for (int c = 0; c < 3; c++)
                    {
                        double e = Ch(Accent, c) + (Ch(Base, c) - Ch(Accent, c)) * (y / 255.0);
                        Assert.True(Math.Abs(Px(t, x, y, c) - e) <= 1.0, "row " + y);
                        Assert.Equal(Px(t, 0, y, c), Px(t, x, y, c));
                    }
        }

        [Fact]
        public void AverageRgb_is_the_mean_of_all_pixels()
        {
            var buf = new byte[262144];
            for (int i = 0; i < buf.Length; i += 4)
            {
                bool even = (i / 4) % 2 == 0;
                buf[i] = (byte)(even ? 10 : 30); buf[i + 1] = (byte)(even ? 0 : 100); buf[i + 2] = 200; buf[i + 3] = 255;
            }
            byte r, g, b;
            TexturePainter.AverageRgb(buf, out r, out g, out b);
            Assert.InRange(r, 19, 21);
            Assert.InRange(g, 49, 51);
            Assert.Equal(200, b);
        }
    }
}
