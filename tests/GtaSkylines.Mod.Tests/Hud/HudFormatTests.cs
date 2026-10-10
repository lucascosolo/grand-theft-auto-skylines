using System;
using GtaSkylines.Mod.Hud;
using Xunit;

namespace GtaSkylines.Mod.Tests.Hud
{
    public class HudFormatTests
    {
        [Theory]
        [InlineData(1234567L, "$1,234,567")]
        [InlineData(-1234L, "-$1,234")]
        [InlineData(0L, "$0")]
        [InlineData(999L, "$999")]
        [InlineData(1000L, "$1,000")]
        public void Money_groups_thousands(long amount, string expected)
        {
            Assert.Equal(expected, HudFormat.Money(amount));
        }

        [Fact]
        public void Money_min_value_does_not_throw()
        {
            var s = HudFormat.Money(long.MinValue);
            Assert.StartsWith("-$", s);
        }

        [Theory]
        [InlineData(500L, "+$500")]
        [InlineData(0L, "+$0")]
        [InlineData(-200L, "-$200")]
        public void Delta_has_explicit_sign(long delta, string expected)
        {
            Assert.Equal(expected, HudFormat.Delta(delta));
        }

        [Theory]
        [InlineData(12, 96, "12 / 96")]
        [InlineData(-1, 5, "0 / 5")]
        [InlineData(3, -9, "3 / 0")]
        public void Ammo_shows_magazine_over_reserve(int mag, int reserve, string expected)
        {
            Assert.Equal(expected, HudFormat.Ammo(mag, reserve));
        }

        [Theory]
        [InlineData(87, "87 km/h")]
        [InlineData(0, "0 km/h")]
        [InlineData(-5, "0 km/h")]
        public void Speed_clamps_negatives(int kmh, string expected)
        {
            Assert.Equal(expected, HudFormat.Speed(kmh));
        }

        [Theory]
        [InlineData(43500.0, "12h 05m")]
        [InlineData(3600.0, "1h 00m")]
        [InlineData(-10.0, "0h 00m")]
        [InlineData(0.0, "0h 00m")]
        public void PlayTime_pads_minutes(double seconds, string expected)
        {
            Assert.Equal(expected, HudFormat.PlayTime(seconds));
        }

        [Fact]
        public void IntTextCache_formats_once_per_distinct_value()
        {
            int calls = 0;
            var cache = new IntTextCache(v => { calls++; return "n" + v; });
            var a = cache.Get(5);
            Assert.Equal("n5", a);
            Assert.Same(a, cache.Get(5));
            Assert.Equal(1, calls);
            var b = cache.Get(6);
            Assert.Equal("n6", b);
            Assert.Equal(2, calls);
            Assert.Same(b, cache.Get(6));
            Assert.Equal(2, calls);
        }

        [Fact]
        public void IntTextCache_first_call_formats_even_for_zero()
        {
            int calls = 0;
            var cache = new IntTextCache(v => { calls++; return "n" + v; });
            Assert.Equal("n0", cache.Get(0));
            Assert.Equal(1, calls);
        }

        [Fact]
        public void PairTextCache_formats_once_until_either_value_changes()
        {
            int calls = 0;
            var cache = new PairTextCache((a, b) => { calls++; return a + "/" + b; });
            var s = cache.Get(1, 2);
            Assert.Equal("1/2", s);
            Assert.Same(s, cache.Get(1, 2));
            Assert.Equal(1, calls);
            Assert.Equal("1/3", cache.Get(1, 3));
            Assert.Equal(2, calls);
            Assert.Equal("2/3", cache.Get(2, 3));
            Assert.Equal(3, calls);
        }

        [Fact]
        public void PairTextCache_first_call_formats_even_for_zeros()
        {
            int calls = 0;
            var cache = new PairTextCache((a, b) => { calls++; return a + "/" + b; });
            Assert.Equal("0/0", cache.Get(0, 0));
            Assert.Equal(1, calls);
        }
    }
}
