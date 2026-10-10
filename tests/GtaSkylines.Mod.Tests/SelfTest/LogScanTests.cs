using System.Collections.Generic;
using GtaSkylines.Mod.SelfTest;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class LogScanTests
    {
        private const string Log =
            "12:00:00.001 INFO  starting\n" +
            "12:00:00.002 ERROR first failure\n" +
            "   at Foo.Bar() in x.cs:line 3\n" +
            "12:00:00.003 WARN  careful\n" +
            "12:00:00.004 INFO  mentions ERROR but is info\n" +
            "12:00:00.005 ERROR second failure\n";

        [Fact]
        public void CountErrors_counts_only_error_level_lines()
        {
            Assert.Equal(2, LogScan.CountErrors(Log));
        }

        [Fact]
        public void CountErrors_null_and_empty_are_zero()
        {
            Assert.Equal(0, LogScan.CountErrors(null));
            Assert.Equal(0, LogScan.CountErrors(""));
        }

        [Fact]
        public void CountErrors_ignores_stack_trace_continuations_and_untimestamped_lines()
        {
            Assert.Equal(0, LogScan.CountErrors("ERROR at the start without a timestamp\n   ERROR trace line\n"));
        }

        [Fact]
        public void CountErrors_requires_error_token_at_the_level_position()
        {
            Assert.Equal(0, LogScan.CountErrors("12:00:00.001 INFO  ERROR in the message\n12:00:00.002 WARN  ERROR too\n"));
            Assert.Equal(1, LogScan.CountErrors("12:00:00.001 ERROR x"));
        }

        [Fact]
        public void CountErrors_handles_crlf()
        {
            Assert.Equal(1, LogScan.CountErrors("12:00:00.001 INFO  a\r\n12:00:00.002 ERROR b\r\n"));
        }

        [Fact]
        public void ErrorLines_returns_the_lines_in_order()
        {
            List<string> lines = LogScan.ErrorLines(Log, 10);
            Assert.Equal(new[] { "12:00:00.002 ERROR first failure", "12:00:00.005 ERROR second failure" }, lines.ToArray());
        }

        [Fact]
        public void ErrorLines_respects_max()
        {
            List<string> lines = LogScan.ErrorLines(Log, 1);
            Assert.Single(lines);
            Assert.Contains("first failure", lines[0]);
        }

        [Fact]
        public void ErrorLines_trims_trailing_carriage_return()
        {
            List<string> lines = LogScan.ErrorLines("12:00:00.001 ERROR boom\r\n", 5);
            Assert.Equal("12:00:00.001 ERROR boom", lines[0]);
        }

        [Fact]
        public void ErrorLines_null_gives_empty_list()
        {
            Assert.Empty(LogScan.ErrorLines(null, 5));
        }

        [Fact]
        public void ErrorLines_max_zero_gives_empty_list()
        {
            Assert.Empty(LogScan.ErrorLines(Log, 0));
        }

        [Fact]
        public void Since_returns_text_appended_after_the_start_length()
        {
            Assert.Equal("def", LogScan.Since("abcdef", 3));
        }

        [Fact]
        public void Since_nonpositive_start_returns_everything()
        {
            Assert.Equal("abc", LogScan.Since("abc", 0));
            Assert.Equal("abc", LogScan.Since("abc", -5));
        }

        [Fact]
        public void Since_start_at_or_beyond_length_returns_empty()
        {
            Assert.Equal("", LogScan.Since("abc", 3));
            Assert.Equal("", LogScan.Since("abc", 99));
        }

        [Fact]
        public void Since_null_text_is_empty()
        {
            Assert.Equal("", LogScan.Since(null, 0));
            Assert.Equal("", LogScan.Since(null, 4));
        }

        [Fact]
        public void Since_feeds_error_counting_for_only_new_lines()
        {
            string first = "12:00:00.001 ERROR old\n";
            string all = first + "12:00:00.002 INFO  fine\n12:00:00.003 ERROR new\n";
            Assert.Equal(1, LogScan.CountErrors(LogScan.Since(all, first.Length)));
        }
    }
}
