using System;
using System.Collections.Generic;
using GtaSkylines.Game.World;

namespace GtaSkylines.Mod.Hud
{
    /// <summary>How a run of text is drawn.</summary>
    public enum RunStyle
    {
        /// <summary>White text.</summary>
        Plain = 0,
        /// <summary>Highlight: places (yellow), <see cref="HighlightKind.Place"/>.</summary>
        Place = 1,
        /// <summary>Highlight: friendly (blue), <see cref="HighlightKind.Friendly"/>.</summary>
        Friendly = 2,
        /// <summary>Highlight: target (red), <see cref="HighlightKind.Target"/>.</summary>
        Target = 3,
        /// <summary>Highlight: goal (green), <see cref="HighlightKind.Goal"/>.</summary>
        Goal = 4,
        /// <summary>A key name drawn inside a key cap (from <c>~Key~</c>).</summary>
        Key = 5,
    }

    /// <summary>One positioned piece of laid-out text.</summary>
    public struct TextRun
    {
        /// <summary>Text to draw (for keys: the key name without tildes).</summary>
        public string Text;
        /// <summary>Style.</summary>
        public RunStyle Style;
        /// <summary>Line index, 0-based.</summary>
        public int Line;
        /// <summary>X offset from the line's left edge.</summary>
        public float X;
        /// <summary>Width (keys include 2 × key padding).</summary>
        public float Width;
    }

    /// <summary>
    /// Word-wraps HUD text with highlight spans and key tokens, measuring through an injected function (the Unity layer
    /// passes <c>GUIStyle.CalcSize</c>). Allocates; call only when the text changes (see <see cref="RichTextCache"/>).
    /// Unity-free.
    /// </summary>
    public static class RichText
    {
        private static float BuildWord(string text, int start, int end, IList<TextSpan> spans, Func<string, float> measure,
            float keyPadding, int line, List<TextRun> word)
        {
            word.Clear();
            float total = 0f;
            int i = start;
            int segStart = start;
            while (i <= end)
            {
                int close = -1;
                if (i < end && text[i] == '~')
                {
                    close = text.IndexOf('~', i + 1, end - i - 1);
                    if (close == i + 1) close = -1;
                }
                if (close < 0 && i < end) { i++; continue; }
                total += AddStyled(text, segStart, i, spans, measure, word);
                if (close < 0) break;
                string name = text.Substring(i + 1, close - i - 1);
                float w = measure(name) + 2f * keyPadding;
                word.Add(new TextRun { Text = name, Style = RunStyle.Key, Line = line, Width = w });
                total += w;
                i = close + 1;
                segStart = i;
            }
            return total;
        }

        private static float AddStyled(string text, int start, int end, IList<TextSpan> spans, Func<string, float> measure, List<TextRun> word)
        {
            float total = 0f;
            int a = start;
            while (a < end)
            {
                RunStyle st = StyleAt(a, spans);
                int b = a + 1;
                while (b < end && StyleAt(b, spans) == st) b++;
                string piece = text.Substring(a, b - a);
                float w = measure(piece);
                word.Add(new TextRun { Text = piece, Style = st, Width = w });
                total += w;
                a = b;
            }
            return total;
        }

        private static RunStyle StyleAt(int index, IList<TextSpan> spans)
        {
            if (spans == null) return RunStyle.Plain;
            for (int k = 0; k < spans.Count; k++)
            {
                TextSpan sp = spans[k];
                if (index < sp.Start || index >= sp.Start + sp.Length) continue;
                switch (sp.Kind)
                {
                    case HighlightKind.Place: return RunStyle.Place;
                    case HighlightKind.Friendly: return RunStyle.Friendly;
                    case HighlightKind.Target: return RunStyle.Target;
                    case HighlightKind.Goal: return RunStyle.Goal;
                }
            }
            return RunStyle.Plain;
        }

        /// <summary>
        /// Lays out <paramref name="text"/> into <paramref name="runs"/> and <paramref name="lineWidths"/> (both cleared
        /// first) and returns the line count (0 for null or empty/whitespace-only text).
        /// <para>
        /// Words are separated by one or more spaces; '\n' forces a line break (an empty line between two '\n' still
        /// counts as a line with width 0). Inside a line, consecutive words are separated by exactly one space of width
        /// measure(" "); no run is produced for spaces. A word goes to the next line when placing it (with its leading
        /// space) would make the line wider than <paramref name="maxWidth"/>; a word wider than maxWidth on its own still
        /// sits alone on its line (it is never split). maxWidth ≤ 0 means no wrapping.
        /// </para>
        /// <para>
        /// "~Name~" (no spaces inside, Name non-empty) is a key token: one run with Style Key, Text "Name", Width
        /// measure(Name) + 2·keyPadding; it is a word of its own when separated by spaces, or part of the surrounding word
        /// when adjacent to other characters. A '~' without a matching closing '~' in the same word is literal text.
        /// </para>
        /// <para>
        /// <paramref name="spans"/> (may be null) index characters of <paramref name="text"/>; a character inside a span
        /// takes that span's style (HighlightKind Place/Friendly/Target/Goal → RunStyle Place/Friendly/Target/Goal), the
        /// first matching span winning; spans out of range are clipped. A word whose characters change style is split into
        /// adjacent runs with no gap (X of the next = X + Width of the previous). Run widths are measure(run text).
        /// </para>
        /// <para>lineWidths[i] = right edge of the last run on line i (0 for an empty line).</para>
        /// </summary>
        public static int Layout(string text, IList<TextSpan> spans, float maxWidth, Func<string, float> measure, float keyPadding,
            List<TextRun> runs, List<float> lineWidths)
        {
            runs.Clear();
            lineWidths.Clear();
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0) return 0;

            float spaceW = measure(" ");
            var word = new List<TextRun>();
            int line = 0;
            float x = 0f;
            bool lineHasWord = false;
            int pos = 0;
            while (true)
            {
                int nl = text.IndexOf('\n', pos);
                int end = nl < 0 ? text.Length : nl;
                int p = pos;
                while (p < end)
                {
                    if (text[p] == ' ') { p++; continue; }
                    int ws = p;
                    while (p < end && text[p] != ' ') p++;
                    float wordW = BuildWord(text, ws, p, spans, measure, keyPadding, line, word);
                    float gap = lineHasWord ? spaceW : 0f;
                    if (lineHasWord && maxWidth > 0f && x + gap + wordW > maxWidth)
                    {
                        lineWidths.Add(x);
                        line++;
                        x = 0f;
                        gap = 0f;
                    }
                    x += gap;
                    for (int k = 0; k < word.Count; k++)
                    {
                        TextRun r = word[k];
                        r.Line = line;
                        r.X = x;
                        runs.Add(r);
                        x += r.Width;
                    }
                    lineHasWord = true;
                }
                lineWidths.Add(x);
                if (nl < 0) break;
                line++;
                x = 0f;
                lineHasWord = false;
                pos = nl + 1;
            }
            return lineWidths.Count;
        }
    }

    /// <summary>
    /// Keeps the last layout and recomputes it only when the text instance, the width (beyond 0.5 px) or the measurer's
    /// generation changes. Unity-free.
    /// </summary>
    public sealed class RichTextCache
    {
        private bool _valid;
        private string _text;
        private float _maxWidth;
        private int _generation;

        /// <summary>Runs of the last layout.</summary>
        public readonly List<TextRun> Runs = new List<TextRun>();
        /// <summary>Line widths of the last layout.</summary>
        public readonly List<float> LineWidths = new List<float>();

        /// <summary>Lines of the last layout.</summary>
        public int Lines { get; private set; }

        /// <summary>Widest line of the last layout (0 when none).</summary>
        public float MaxLineWidth { get; private set; }

        /// <summary>
        /// Ensures the layout matches <paramref name="text"/> (compared by reference), <paramref name="maxWidth"/> and
        /// <paramref name="generation"/> (bump it when the font or font size changes). Returns true when it re-laid out.
        /// A null text clears the layout (Lines 0).
        /// </summary>
        public bool Update(string text, IList<TextSpan> spans, float maxWidth, Func<string, float> measure, float keyPadding, int generation)
        {
            if (_valid && ReferenceEquals(text, _text) && Math.Abs(maxWidth - _maxWidth) <= 0.5f && generation == _generation) return false;
            _valid = true;
            _text = text;
            _maxWidth = maxWidth;
            _generation = generation;
            Lines = RichText.Layout(text, spans, maxWidth, measure, keyPadding, Runs, LineWidths);
            MaxLineWidth = 0f;
            for (int i = 0; i < LineWidths.Count; i++)
                if (LineWidths[i] > MaxLineWidth) MaxLineWidth = LineWidths[i];
            return true;
        }
    }
}
