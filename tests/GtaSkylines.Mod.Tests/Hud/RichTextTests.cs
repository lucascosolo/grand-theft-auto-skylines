using System;
using System.Collections.Generic;
using GtaSkylines.Game.World;
using GtaSkylines.Mod.Hud;
using Xunit;

namespace GtaSkylines.Mod.Tests.Hud
{
    public class RichTextTests
    {
        private static float Measure(string s) { return 10f * s.Length; }

        private static int Layout(string text, IList<TextSpan> spans, float maxW, List<TextRun> runs, List<float> widths, float pad = 3f)
        {
            return RichText.Layout(text, spans, maxW, Measure, pad, runs, widths);
        }

        private static TextSpan Span(int start, int length, HighlightKind kind)
        {
            return new TextSpan { Start = start, Length = length, Kind = kind };
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Empty_text_has_no_lines_and_clears_outputs(string text)
        {
            var runs = new List<TextRun> { new TextRun { Text = "old" } };
            var widths = new List<float> { 99f };
            Assert.Equal(0, Layout(text, null, 100f, runs, widths));
            Assert.Empty(runs);
            Assert.Empty(widths);
        }

        [Fact]
        public void Words_sit_on_one_line_separated_by_one_space()
        {
            var runs = new List<TextRun>();
            var widths = new List<float>();
            Assert.Equal(1, Layout("hello world", null, 0f, runs, widths));
            Assert.Equal(2, runs.Count);
            Assert.Equal("hello", runs[0].Text);
            Assert.Equal(0f, runs[0].X, 0.01f);
            Assert.Equal(50f, runs[0].Width, 0.01f);
            Assert.Equal(RunStyle.Plain, runs[0].Style);
            Assert.Equal("world", runs[1].Text);
            Assert.Equal(60f, runs[1].X, 0.01f);
            Assert.Equal(110f, widths[0], 0.01f);
        }

        [Fact]
        public void Repeated_spaces_collapse_to_one()
        {
            var runs = new List<TextRun>();
            Layout("a   b", null, 0f, runs, new List<float>());
            Assert.Equal(2, runs.Count);
            Assert.Equal(20f, runs[1].X, 0.01f);
        }

        [Fact]
        public void Wraps_when_a_word_would_exceed_max_width()
        {
            var runs = new List<TextRun>();
            var widths = new List<float>();
            // "aaa bbb" is exactly 70 wide (fits); adding " ccc" would be 110.
            Assert.Equal(2, Layout("aaa bbb ccc", null, 70f, runs, widths));
            Assert.Equal(3, runs.Count);
            Assert.Equal(0, runs[1].Line);
            Assert.Equal(1, runs[2].Line);
            Assert.Equal(0f, runs[2].X, 0.01f);
            Assert.Equal(70f, widths[0], 0.01f);
            Assert.Equal(30f, widths[1], 0.01f);
        }

        [Fact]
        public void Oversized_word_sits_alone_on_its_line()
        {
            var runs = new List<TextRun>();
            var widths = new List<float>();
            Assert.Equal(2, Layout("abcdefghij kk", null, 50f, runs, widths));
            Assert.Equal("abcdefghij", runs[0].Text);
            Assert.Equal(0, runs[0].Line);
            Assert.Equal(100f, widths[0], 0.01f);
            Assert.Equal("kk", runs[1].Text);
            Assert.Equal(1, runs[1].Line);
            Assert.Equal(20f, widths[1], 0.01f);
        }

        [Fact]
        public void Single_oversized_word_is_one_line_and_never_split()
        {
            var runs = new List<TextRun>();
            Assert.Equal(1, Layout("abcdefghij", null, 50f, runs, new List<float>()));
            Assert.Single(runs);
        }

        [Fact]
        public void Newline_forces_a_break_and_empty_lines_count()
        {
            var runs = new List<TextRun>();
            var widths = new List<float>();
            Assert.Equal(3, Layout("a\n\nb", null, 0f, runs, widths));
            Assert.Equal(3, widths.Count);
            Assert.Equal(10f, widths[0], 0.01f);
            Assert.Equal(0f, widths[1], 0.01f);
            Assert.Equal(10f, widths[2], 0.01f);
            Assert.Equal(2, runs.Count);
            Assert.Equal(2, runs[1].Line);
        }

        [Fact]
        public void Key_token_is_its_own_word_with_padding()
        {
            var runs = new List<TextRun>();
            var widths = new List<float>();
            Assert.Equal(1, Layout("Press ~F~ now", null, 0f, runs, widths, 3f));
            Assert.Equal(3, runs.Count);
            Assert.Equal(RunStyle.Key, runs[1].Style);
            Assert.Equal("F", runs[1].Text);
            Assert.Equal(16f, runs[1].Width, 0.01f);
            Assert.Equal(60f, runs[1].X, 0.01f);
            Assert.Equal("now", runs[2].Text);
            Assert.Equal(86f, runs[2].X, 0.01f);
            Assert.Equal(116f, widths[0], 0.01f);
        }

        [Fact]
        public void Key_token_adjacent_to_characters_joins_the_word_without_gaps()
        {
            var runs = new List<TextRun>();
            Layout("(~F~)", null, 0f, runs, new List<float>(), 3f);
            Assert.Equal(3, runs.Count);
            Assert.Equal("(", runs[0].Text);
            Assert.Equal(RunStyle.Key, runs[1].Style);
            Assert.Equal(10f, runs[1].X, 0.01f);
            Assert.Equal(")", runs[2].Text);
            Assert.Equal(26f, runs[2].X, 0.01f);
        }

        [Fact]
        public void Key_width_counts_towards_wrapping()
        {
            var runs = new List<TextRun>();
            // "aa" = 20, key = 10 + 2*2 = 14; 20 + 10 + 14 = 44 > 40.
            Assert.Equal(2, Layout("aa ~F~", null, 40f, runs, new List<float>(), 2f));
            Assert.Equal(1, runs[1].Line);
        }

        [Fact]
        public void Unmatched_tilde_is_literal()
        {
            var runs = new List<TextRun>();
            Layout("a ~b", null, 0f, runs, new List<float>());
            Assert.Equal(2, runs.Count);
            Assert.Equal("~b", runs[1].Text);
            Assert.Equal(RunStyle.Plain, runs[1].Style);
            Assert.Equal(20f, runs[1].Width, 0.01f);
        }

        [Fact]
        public void Tildes_in_different_words_do_not_pair()
        {
            var runs = new List<TextRun>();
            Layout("~F G~", null, 0f, runs, new List<float>());
            Assert.Equal(2, runs.Count);
            Assert.Equal("~F", runs[0].Text);
            Assert.Equal("G~", runs[1].Text);
            Assert.All(runs, r => Assert.Equal(RunStyle.Plain, r.Style));
        }

        [Theory]
        [InlineData(HighlightKind.Place, RunStyle.Place)]
        [InlineData(HighlightKind.Friendly, RunStyle.Friendly)]
        [InlineData(HighlightKind.Target, RunStyle.Target)]
        [InlineData(HighlightKind.Goal, RunStyle.Goal)]
        public void Span_styles_the_covered_word(HighlightKind kind, RunStyle style)
        {
            var runs = new List<TextRun>();
            Layout("go to Bank now", new[] { Span(6, 4, kind) }, 0f, runs, new List<float>());
            Assert.Equal(4, runs.Count);
            Assert.Equal("Bank", runs[2].Text);
            Assert.Equal(style, runs[2].Style);
            Assert.Equal(RunStyle.Plain, runs[1].Style);
            Assert.Equal(RunStyle.Plain, runs[3].Style);
        }

        [Fact]
        public void Span_inside_a_word_splits_it_into_adjacent_runs()
        {
            var runs = new List<TextRun>();
            Layout("xABy", new[] { Span(1, 2, HighlightKind.Friendly) }, 0f, runs, new List<float>());
            Assert.Equal(3, runs.Count);
            Assert.Equal("x", runs[0].Text);
            Assert.Equal("AB", runs[1].Text);
            Assert.Equal(RunStyle.Friendly, runs[1].Style);
            Assert.Equal("y", runs[2].Text);
            Assert.Equal(runs[0].X + runs[0].Width, runs[1].X, 0.01f);
            Assert.Equal(runs[1].X + runs[1].Width, runs[2].X, 0.01f);
            Assert.Equal(20f, runs[1].Width, 0.01f);
        }

        [Fact]
        public void First_matching_span_wins()
        {
            var runs = new List<TextRun>();
            var spans = new[] { Span(0, 3, HighlightKind.Target), Span(1, 1, HighlightKind.Goal) };
            Layout("abc", spans, 0f, runs, new List<float>());
            Assert.Single(runs);
            Assert.Equal(RunStyle.Target, runs[0].Style);
        }

        [Fact]
        public void Spans_out_of_range_are_clipped()
        {
            var runs = new List<TextRun>();
            Layout("abcdef", new[] { Span(-2, 4, HighlightKind.Place) }, 0f, runs, new List<float>());
            Assert.Equal(2, runs.Count);
            Assert.Equal("ab", runs[0].Text);
            Assert.Equal(RunStyle.Place, runs[0].Style);
            Assert.Equal("cdef", runs[1].Text);

            runs.Clear();
            Layout("abcdef", new[] { Span(4, 100, HighlightKind.Place) }, 0f, runs, new List<float>());
            Assert.Equal(2, runs.Count);
            Assert.Equal("ef", runs[1].Text);
            Assert.Equal(RunStyle.Place, runs[1].Style);

            runs.Clear();
            Layout("abcdef", new[] { Span(100, 5, HighlightKind.Place) }, 0f, runs, new List<float>());
            Assert.Single(runs);
            Assert.Equal(RunStyle.Plain, runs[0].Style);
        }

        [Fact]
        public void Cache_relays_out_only_when_inputs_change()
        {
            int calls = 0;
            Func<string, float> measure = s => { calls++; return 10f * s.Length; };
            var cache = new RichTextCache();
            string text = "hello big world";

            Assert.True(cache.Update(text, null, 100f, measure, 3f, 0));
            int afterFirst = calls;
            Assert.True(afterFirst > 0);
            Assert.True(cache.Lines >= 1);
            Assert.NotEmpty(cache.Runs);
            Assert.Equal(cache.Lines, cache.LineWidths.Count);

            Assert.False(cache.Update(text, null, 100f, measure, 3f, 0));
            Assert.Equal(afterFirst, calls);

            Assert.False(cache.Update(text, null, 100.3f, measure, 3f, 0));
            Assert.Equal(afterFirst, calls);

            Assert.True(cache.Update(text, null, 101f, measure, 3f, 0));
            Assert.True(calls > afterFirst);
        }

        [Fact]
        public void Cache_relays_out_for_a_new_text_instance_or_generation()
        {
            int calls = 0;
            Func<string, float> measure = s => { calls++; return 10f * s.Length; };
            var cache = new RichTextCache();
            string text = "hello world";
            cache.Update(text, null, 0f, measure, 3f, 0);

            string copy = new string(text.ToCharArray());
            Assert.NotSame(text, copy);
            Assert.True(cache.Update(copy, null, 0f, measure, 3f, 0));
            Assert.False(cache.Update(copy, null, 0f, measure, 3f, 0));
            Assert.True(cache.Update(copy, null, 0f, measure, 3f, 1));
            Assert.False(cache.Update(copy, null, 0f, measure, 3f, 1));
        }

        [Fact]
        public void Cache_reports_max_line_width_and_clears_on_null()
        {
            var cache = new RichTextCache();
            cache.Update("aaaa b", null, 50f, Measure, 3f, 0);
            Assert.Equal(2, cache.Lines);
            Assert.Equal(40f, cache.MaxLineWidth, 0.01f);

            cache.Update(null, null, 50f, Measure, 3f, 0);
            Assert.Equal(0, cache.Lines);
            Assert.Empty(cache.Runs);
            Assert.Equal(0f, cache.MaxLineWidth, 0.01f);
        }
    }
}
