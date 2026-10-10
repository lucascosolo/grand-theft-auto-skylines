using System;
using System.Globalization;
using System.Text;

namespace GtaSkylines.Mod.Hud
{
    /// <summary>HUD and menu number formatting (invariant culture, ASCII only so every font has the glyphs). Unity-free.</summary>
    public static class HudFormat
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static string Group(long amount)
        {
            string digits = amount.ToString(Inv);
            if (amount < 0) digits = digits.Substring(1);
            var sb = new StringBuilder();
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0) sb.Append(',');
                sb.Append(digits[i]);
            }
            return sb.ToString();
        }

        /// <summary>"$1,234,567"; negatives "-$1,234"; 0 → "$0". long.MinValue must not throw.</summary>
        public static string Money(long amount)
        {
            return amount < 0 ? "-$" + Group(amount) : "$" + Group(amount);
        }

        /// <summary>Cash delta line: "+$500" for positive and zero, "-$200" for negative.</summary>
        public static string Delta(long delta)
        {
            return delta < 0 ? "-$" + Group(delta) : "+$" + Group(delta);
        }

        /// <summary>Ammo readout "12 / 96" (magazine / reserve); negatives are shown as 0.</summary>
        public static string Ammo(int magazine, int reserve)
        {
            return Math.Max(0, magazine).ToString(Inv) + " / " + Math.Max(0, reserve).ToString(Inv);
        }

        /// <summary>Speedometer text "87 km/h"; negatives are shown as 0.</summary>
        public static string Speed(int kmh)
        {
            return Math.Max(0, kmh).ToString(Inv) + " km/h";
        }

        /// <summary>Play time "12h 05m" from seconds (negatives as 0, minutes zero-padded to two digits).</summary>
        public static string PlayTime(double seconds)
        {
            long minutes = seconds > 0 ? (long)(seconds / 60.0) : 0;
            return (minutes / 60).ToString(Inv) + "h " + (minutes % 60).ToString("00", Inv) + "m";
        }
    }

    /// <summary>
    /// Formats an int once per distinct value: <see cref="Get"/> returns the same string instance while the value is
    /// unchanged, so a steady frame allocates nothing. Unity-free.
    /// </summary>
    public sealed class IntTextCache
    {
        private readonly Func<int, string> _format;
        private bool _has;
        private int _value;
        private string _text;

        /// <summary>Creates a cache around <paramref name="format"/> (called only when the value changes).</summary>
        public IntTextCache(Func<int, string> format)
        {
            _format = format;
        }

        /// <summary>Formatted text of <paramref name="value"/>; the first call always formats.</summary>
        public string Get(int value)
        {
            if (!_has || value != _value)
            {
                _text = _format(value);
                _value = value;
                _has = true;
            }
            return _text;
        }
    }

    /// <summary>Same as <see cref="IntTextCache"/> for a pair of ints (ammo magazine / reserve). Unity-free.</summary>
    public sealed class PairTextCache
    {
        private readonly Func<int, int, string> _format;
        private bool _has;
        private int _a;
        private int _b;
        private string _text;

        /// <summary>Creates a cache around <paramref name="format"/>.</summary>
        public PairTextCache(Func<int, int, string> format)
        {
            _format = format;
        }

        /// <summary>Formatted text of the pair; the same instance while both values are unchanged.</summary>
        public string Get(int a, int b)
        {
            if (!_has || a != _a || b != _b)
            {
                _text = _format(a, b);
                _a = a;
                _b = b;
                _has = true;
            }
            return _text;
        }
    }
}
