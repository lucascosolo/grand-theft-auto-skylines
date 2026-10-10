using System;
using System.Collections.Generic;
using System.Text;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Missions
{
    /// <summary>
    /// A display string with highlighted ranges, parsed once from GTA-style markup and then shown without allocating.
    /// <para>
    /// Markup: <c>~y~</c> starts a <see cref="HighlightKind.Place"/> span, <c>~b~</c> <see cref="HighlightKind.Friendly"/>,
    /// <c>~r~</c> <see cref="HighlightKind.Target"/>, <c>~g~</c> <see cref="HighlightKind.Goal"/>; <c>~s~</c> ends the open
    /// span. A new colour code closes the open span first. A span still open at the end of the string ends there. Empty
    /// spans are dropped. Unknown codes (<c>~x~</c>) and a lone <c>~</c> are kept as literal text.
    /// Example: <c>"Go to ~y~Vince's garage~s~."</c> → Text "Go to Vince's garage.", one Place span (6, 14).
    /// </para>
    /// </summary>
    public sealed class RichText
    {
        /// <summary>The empty text (no spans).</summary>
        public static readonly RichText Empty = new RichText(string.Empty, new TextSpan[0]);

        /// <summary>Creates a text from parts already parsed.</summary>
        /// <exception cref="ArgumentNullException">text or spans is null.</exception>
        public RichText(string text, TextSpan[] spans)
        {
            if (text == null) throw new ArgumentNullException("text");
            if (spans == null) throw new ArgumentNullException("spans");
            Text = text;
            Spans = spans;
        }

        /// <summary>The plain text, markup removed.</summary>
        public string Text { get; private set; }

        /// <summary>Highlighted ranges in ascending, non-overlapping order.</summary>
        public TextSpan[] Spans { get; private set; }

        /// <summary>Parses markup (see the class). Null parses as <see cref="Empty"/>.</summary>
        public static RichText Parse(string markup)
        {
            if (string.IsNullOrEmpty(markup)) return Empty;
            var text = new StringBuilder(markup.Length);
            var spans = new List<TextSpan>();
            int open = -1;
            HighlightKind kind = HighlightKind.Place;
            int i = 0;
            while (i < markup.Length)
            {
                char c = markup[i];
                if (c == '~' && i + 2 < markup.Length && markup[i + 2] == '~')
                {
                    char code = markup[i + 1];
                    HighlightKind k;
                    bool colour = TryCode(code, out k);
                    if (colour || code == 's')
                    {
                        Close(spans, open, text.Length, kind);
                        open = colour ? text.Length : -1;
                        kind = k;
                        i += 3;
                        continue;
                    }
                }
                text.Append(c);
                i++;
            }
            Close(spans, open, text.Length, kind);
            return new RichText(text.ToString(), spans.ToArray());
        }

        private static bool TryCode(char code, out HighlightKind kind)
        {
            switch (code)
            {
                case 'y': kind = HighlightKind.Place; return true;
                case 'b': kind = HighlightKind.Friendly; return true;
                case 'r': kind = HighlightKind.Target; return true;
                case 'g': kind = HighlightKind.Goal; return true;
                default: kind = HighlightKind.Place; return false;
            }
        }

        private static void Close(List<TextSpan> spans, int open, int end, HighlightKind kind)
        {
            if (open >= 0 && end > open) spans.Add(new TextSpan { Start = open, Length = end - open, Kind = kind });
        }

        /// <summary>
        /// Writes this text as the objective of <paramref name="status"/>: Objective = <see cref="Text"/> (null when the
        /// text is empty), ObjectiveSpans cleared and refilled. Allocates nothing beyond list growth.
        /// </summary>
        public void WriteObjective(MissionStatus status)
        {
            status.Objective = Text.Length == 0 ? null : Text;
            status.ObjectiveSpans.Clear();
            for (int i = 0; i < Spans.Length; i++) status.ObjectiveSpans.Add(Spans[i]);
        }

        /// <inheritdoc />
        public override string ToString() { return Text; }
    }

    /// <summary>
    /// Values substituted into authored templates. A placeholder is <c>{key}</c> (letters, digits and underscores);
    /// <see cref="Fill"/> replaces each known key with its value and leaves unknown placeholders as written, so a missing
    /// value is visible in tests. Keys used by the campaign are the constants on this class.
    /// </summary>
    public sealed class TextVars
    {
        /// <summary>The contact building's name ("Rusty Bolt Motors").</summary>
        public const string Contact = "contact";
        /// <summary>The contact building's street.</summary>
        public const string ContactStreet = "contact_street";
        /// <summary>The contact building's district.</summary>
        public const string ContactDistrict = "contact_district";
        /// <summary>The current destination's name.</summary>
        public const string Place = "place";
        /// <summary>The current destination's street.</summary>
        public const string Street = "street";
        /// <summary>The current destination's district.</summary>
        public const string District = "district";
        /// <summary>A second named place (pick-up, warehouse, hospital).</summary>
        public const string Place2 = "place2";
        /// <summary>The second place's street.</summary>
        public const string Street2 = "street2";
        /// <summary>The second place's district.</summary>
        public const string District2 = "district2";
        /// <summary>A person's name (patient, suspect).</summary>
        public const string Person = "person";
        /// <summary>A vehicle model name ("Sports Coupe").</summary>
        public const string Vehicle = "vehicle";
        /// <summary>The city's name.</summary>
        public const string City = "city";
        /// <summary>A number (seconds, dollars, count) already formatted.</summary>
        public const string Number = "number";

        private readonly Dictionary<string, string> _values = new Dictionary<string, string>();

        /// <summary>Sets a value (null clears it). Returns this for chaining.</summary>
        public TextVars Set(string key, string value)
        {
            if (value == null) _values.Remove(key); else _values[key] = value;
            return this;
        }

        /// <summary>The value of a key, or null.</summary>
        public string Get(string key)
        {
            string v;
            return key != null && _values.TryGetValue(key, out v) ? v : null;
        }

        /// <summary>
        /// Sets a place's Name, Street and District: into <see cref="Place"/>/<see cref="Street"/>/<see cref="District"/>
        /// when <paramref name="second"/> is false, else <see cref="Place2"/>/<see cref="Street2"/>/<see cref="District2"/>.
        /// </summary>
        public TextVars SetPlace(CampaignPlace place, bool second)
        {
            Set(second ? Place2 : Place, place.Name);
            Set(second ? Street2 : Street, place.Street);
            return Set(second ? District2 : District, place.District);
        }

        /// <summary>The template with every known <c>{key}</c> replaced. Null → empty string.</summary>
        public string Fill(string template)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;
            var sb = new StringBuilder(template.Length + 32);
            int i = 0;
            while (i < template.Length)
            {
                char c = template[i];
                if (c == '{')
                {
                    int end = template.IndexOf('}', i + 1);
                    if (end > i + 1)
                    {
                        string v = Get(template.Substring(i + 1, end - i - 1));
                        if (v != null)
                        {
                            sb.Append(v);
                            i = end + 1;
                            continue;
                        }
                    }
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        /// <summary><see cref="RichText.Parse"/>(<see cref="Fill"/>(template)).</summary>
        public RichText Rich(string template)
        {
            return RichText.Parse(Fill(template));
        }
    }
}
