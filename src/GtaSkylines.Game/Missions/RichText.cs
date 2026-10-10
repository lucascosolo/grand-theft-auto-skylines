using System;
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// Writes this text as the objective of <paramref name="status"/>: Objective = <see cref="Text"/> (null when the
        /// text is empty), ObjectiveSpans cleared and refilled. Allocates nothing beyond list growth.
        /// </summary>
        public void WriteObjective(MissionStatus status)
        {
            throw new NotImplementedException();
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

        /// <summary>Sets a value (null clears it). Returns this for chaining.</summary>
        public TextVars Set(string key, string value)
        {
            throw new NotImplementedException();
        }

        /// <summary>The value of a key, or null.</summary>
        public string Get(string key)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets a place's Name, Street and District: into <see cref="Place"/>/<see cref="Street"/>/<see cref="District"/>
        /// when <paramref name="second"/> is false, else <see cref="Place2"/>/<see cref="Street2"/>/<see cref="District2"/>.
        /// </summary>
        public TextVars SetPlace(CampaignPlace place, bool second)
        {
            throw new NotImplementedException();
        }

        /// <summary>The template with every known <c>{key}</c> replaced. Null → empty string.</summary>
        public string Fill(string template)
        {
            throw new NotImplementedException();
        }

        /// <summary><see cref="RichText.Parse"/>(<see cref="Fill"/>(template)).</summary>
        public RichText Rich(string template)
        {
            throw new NotImplementedException();
        }
    }
}
