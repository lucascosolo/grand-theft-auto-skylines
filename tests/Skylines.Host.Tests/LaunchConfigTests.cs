using System;
using System.IO;
using Skylines.Host;
using Xunit;

namespace Skylines.Host.Tests
{
    public class LaunchConfigTests
    {
        private static string Fresh()
        {
            string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        [Fact]
        public void FileName_is_launch_cfg()
        {
            Assert.Equal("launch.cfg", LaunchConfig.FileName);
        }

        [Fact]
        public void Defaults()
        {
            LaunchConfig c = LaunchConfig.Parse("");
            Assert.Equal(SelfTestMode.Off, c.SelfTest);
            Assert.False(c.SelfTestQuit);
            Assert.Equal("", c.Autoload);
            Assert.False(c.Found);
            Assert.Empty(c.Problems);
        }

        [Fact]
        public void Parse_null_is_empty()
        {
            LaunchConfig c = LaunchConfig.Parse(null);
            Assert.Empty(c.Problems);
            Assert.Equal(SelfTestMode.Off, c.SelfTest);
        }

        [Fact]
        public void Parses_all_three_keys_with_crlf_comments_and_blanks()
        {
            LaunchConfig c = LaunchConfig.Parse("# comment\r\n\r\nselftest = city_load\r\nselftest_quit = true\r\nautoload = My Save\r\n");
            Assert.Empty(c.Problems);
            Assert.Equal(SelfTestMode.CityLoad, c.SelfTest);
            Assert.True(c.SelfTestQuit);
            Assert.Equal("My Save", c.Autoload);
        }

        [Theory]
        [InlineData("off", SelfTestMode.Off)]
        [InlineData("city_load", SelfTestMode.CityLoad)]
        [InlineData("CITY_LOAD", SelfTestMode.CityLoad)]
        [InlineData("  Off  ", SelfTestMode.Off)]
        public void Parses_selftest(string value, SelfTestMode expected)
        {
            LaunchConfig c = LaunchConfig.Parse("selftest = " + value + "\n");
            Assert.Empty(c.Problems);
            Assert.Equal(expected, c.SelfTest);
        }

        [Fact]
        public void Bad_selftest_is_one_problem_and_stays_off()
        {
            LaunchConfig c = LaunchConfig.Parse("selftest = sometimes\n");
            Assert.Single(c.Problems);
            Assert.Contains("selftest", c.Problems[0]);
            Assert.Equal(SelfTestMode.Off, c.SelfTest);
        }

        [Theory]
        [InlineData("TRUE", true)]
        [InlineData("true", true)]
        [InlineData("False", false)]
        public void Selftest_quit_is_case_insensitive(string v, bool expected)
        {
            LaunchConfig c = LaunchConfig.Parse("selftest_quit = " + v + "\n");
            Assert.Empty(c.Problems);
            Assert.Equal(expected, c.SelfTestQuit);
        }

        [Fact]
        public void Bad_selftest_quit_is_one_problem_and_stays_default()
        {
            LaunchConfig c = LaunchConfig.Parse("selftest_quit = maybe\n");
            Assert.Single(c.Problems);
            Assert.Contains("selftest_quit", c.Problems[0]);
            Assert.False(c.SelfTestQuit);
        }

        [Fact]
        public void Autoload_save_name_is_trimmed_and_keeps_inner_equals()
        {
            LaunchConfig c = LaunchConfig.Parse("autoload =   My City #2 = Big  Save  \n");
            Assert.Empty(c.Problems);
            Assert.Equal("My City #2 = Big  Save", c.Autoload);
        }

        [Fact]
        public void Autoload_new_map_is_kept_as_written()
        {
            LaunchConfig c = LaunchConfig.Parse("autoload = new:Green Plains\n");
            Assert.Empty(c.Problems);
            Assert.Equal("new:Green Plains", c.Autoload);
        }

        [Theory]
        [InlineData("new:")]
        [InlineData("new:   ")]
        public void Autoload_new_with_blank_map_is_a_problem(string value)
        {
            LaunchConfig c = LaunchConfig.Parse("autoload = " + value + "\n");
            Assert.Single(c.Problems);
            Assert.Contains("autoload", c.Problems[0]);
            Assert.Equal("", c.Autoload);
        }

        [Fact]
        public void Autoload_empty_value_is_allowed()
        {
            LaunchConfig c = LaunchConfig.Parse("autoload =\n");
            Assert.Empty(c.Problems);
            Assert.Equal("", c.Autoload);
        }

        [Fact]
        public void Later_duplicate_keys_win()
        {
            LaunchConfig c = LaunchConfig.Parse("autoload = a\nautoload = b\nselftest_quit = true\nselftest_quit = false\n");
            Assert.Equal("b", c.Autoload);
            Assert.False(c.SelfTestQuit);
            Assert.Empty(c.Problems);
        }

        [Fact]
        public void Unknown_key_reports_line_and_key_and_is_ignored()
        {
            LaunchConfig c = LaunchConfig.Parse("autoload = a\nbogus = 1\n");
            Assert.Single(c.Problems);
            Assert.Contains("line 2", c.Problems[0]);
            Assert.Contains("unknown key 'bogus'", c.Problems[0]);
            Assert.Equal("a", c.Autoload);
        }

        [Fact]
        public void Keys_are_case_sensitive()
        {
            LaunchConfig c = LaunchConfig.Parse("SelfTest = city_load\n");
            Assert.Single(c.Problems);
            Assert.Contains("unknown key 'SelfTest'", c.Problems[0]);
            Assert.Equal(SelfTestMode.Off, c.SelfTest);
        }

        [Fact]
        public void Minecraft_era_command_key_is_now_unknown()
        {
            LaunchConfig c = LaunchConfig.Parse("command = x\n");
            Assert.Single(c.Problems);
            Assert.Contains("unknown key 'command'", c.Problems[0]);
        }

        [Theory]
        [InlineData("nonsense")]
        [InlineData("= value")]
        public void Line_without_key_value_shape_is_a_problem_with_line_number(string bad)
        {
            LaunchConfig c = LaunchConfig.Parse("# c\n\n" + bad + "\n");
            Assert.Single(c.Problems);
            Assert.Contains("line 3", c.Problems[0]);
            Assert.Contains("expected key = value", c.Problems[0]);
        }

        [Fact]
        public void Indented_comment_and_whitespace_lines_are_skipped()
        {
            LaunchConfig c = LaunchConfig.Parse("   # note\n   \n\t\nautoload = z\n");
            Assert.Empty(c.Problems);
            Assert.Equal("z", c.Autoload);
        }

        [Fact]
        public void Load_missing_file_gives_defaults_without_a_problem()
        {
            LaunchConfig c = LaunchConfig.Load(Path.Combine(Fresh(), LaunchConfig.FileName));
            Assert.False(c.Found);
            Assert.Empty(c.Problems);
            Assert.Equal("", c.Autoload);
        }

        [Fact]
        public void Load_reads_a_file_and_sets_Found()
        {
            string path = Path.Combine(Fresh(), LaunchConfig.FileName);
            File.WriteAllText(path, "selftest = city_load\nautoload = new:Map\n");
            LaunchConfig c = LaunchConfig.Load(path);
            Assert.True(c.Found);
            Assert.Empty(c.Problems);
            Assert.Equal(SelfTestMode.CityLoad, c.SelfTest);
            Assert.Equal("new:Map", c.Autoload);
        }

        [Fact]
        public void Load_unreadable_path_gives_one_cannot_read_problem_and_does_not_throw()
        {
            // A directory where the file should be: reading it fails, but it is not "missing".
            string path = Path.Combine(Fresh(), LaunchConfig.FileName);
            Directory.CreateDirectory(path);
            LaunchConfig c = LaunchConfig.Load(path);
            Assert.Single(c.Problems);
            Assert.Contains("cannot read", c.Problems[0]);
            Assert.Equal(SelfTestMode.Off, c.SelfTest);
        }
    }
}
