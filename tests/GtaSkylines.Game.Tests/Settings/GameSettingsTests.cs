using System.Collections.Generic;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Settings;
using Xunit;

namespace GtaSkylines.Game.Tests.Settings
{
    public class GameSettingsTests
    {
        private static void AssertSame(GameSettings a, GameSettings b)
        {
            Assert.Equal(a.MouseSensitivity, b.MouseSensitivity);
            Assert.Equal(a.InvertY, b.InvertY);
            Assert.Equal(a.FieldOfViewDeg, b.FieldOfViewDeg);
            Assert.Equal(a.FootView, b.FootView);
            Assert.Equal(a.VehicleView, b.VehicleView);
            Assert.Equal(a.HudScale, b.HudScale);
            Assert.Equal(a.MinimapEnabled, b.MinimapEnabled);
            Assert.Equal(a.Subtitles, b.Subtitles);
            Assert.Equal(a.SimulationRate, b.SimulationRate);
            Assert.Equal(a.Police, b.Police);
            Assert.Equal(a.MasterVolume, b.MasterVolume);
            Assert.Equal(a.EffectsVolume, b.EffectsVolume);
            Assert.Equal(a.MusicVolume, b.MusicVolume);
            foreach (GameButton g in System.Enum.GetValues(typeof(GameButton)))
                for (int s = 0; s < 2; s++)
                    Assert.Equal(a.Bindings.Get(g, s), b.Bindings.Get(g, s));
            foreach (MoveKey m in System.Enum.GetValues(typeof(MoveKey)))
                for (int s = 0; s < 2; s++)
                    Assert.Equal(a.Bindings.GetMove(m, s), b.Bindings.GetMove(m, s));
        }

        [Fact]
        public void Defaults_match_design()
        {
            var s = new GameSettings();
            Assert.Equal(1f, s.MouseSensitivity);
            Assert.False(s.InvertY);
            Assert.Equal(70f, s.FieldOfViewDeg);
            Assert.Equal(0, s.FootView);
            Assert.Equal(1, s.VehicleView);
            Assert.Equal(1f, s.HudScale);
            Assert.True(s.MinimapEnabled);
            Assert.True(s.Subtitles);
            Assert.Equal(1, s.SimulationRate);
            Assert.Equal(PoliceDifficulty.Normal, s.Police);
            Assert.Equal(1f, s.MasterVolume);
            Assert.Equal(1f, s.EffectsVolume);
            Assert.Equal(0.6f, s.MusicVolume);
            Assert.NotNull(s.Bindings);
            Assert.True(s.Bindings.IsDefault);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Parse_null_or_empty_gives_defaults_and_no_problems(string text)
        {
            var r = GameSettings.Parse(text);
            Assert.NotNull(r.Settings);
            Assert.Empty(r.Problems);
            AssertSame(new GameSettings(), r.Settings);
        }

        [Fact]
        public void Parse_reads_every_documented_key()
        {
            var r = GameSettings.Parse(string.Join("\n", new[]
            {
                "mouse_sensitivity = 2.5", "invert_y = true", "field_of_view = 90", "foot_view = 2", "vehicle_view = 3",
                "hud_scale = 1.5", "minimap = false", "subtitles = false", "simulation_rate = 3", "police_difficulty = hard",
                "volume_master = 0.5", "volume_effects = 0.25", "volume_music = 0", "volume_voices = 0.75",
            }));
            Assert.Empty(r.Problems);
            var s = r.Settings;
            Assert.Equal(2.5f, s.MouseSensitivity);
            Assert.True(s.InvertY);
            Assert.Equal(90f, s.FieldOfViewDeg);
            Assert.Equal(2, s.FootView);
            Assert.Equal(3, s.VehicleView);
            Assert.Equal(1.5f, s.HudScale);
            Assert.False(s.MinimapEnabled);
            Assert.False(s.Subtitles);
            Assert.Equal(3, s.SimulationRate);
            Assert.Equal(PoliceDifficulty.Hard, s.Police);
            Assert.Equal(0.5f, s.MasterVolume);
            Assert.Equal(0.25f, s.EffectsVolume);
            Assert.Equal(0f, s.MusicVolume);
        }

        [Fact]
        public void Parse_ignores_blank_lines_and_comments_trims_and_is_case_insensitive_for_bools_and_enums()
        {
            var r = GameSettings.Parse("# comment\n\n   \n  invert_y   =   TRUE  \npolice_difficulty = EaSy\r\nhud_scale=0.5\n");
            Assert.Empty(r.Problems);
            Assert.True(r.Settings.InvertY);
            Assert.Equal(PoliceDifficulty.Easy, r.Settings.Police);
            Assert.Equal(0.5f, r.Settings.HudScale);
        }

        [Fact]
        public void Later_lines_override_earlier_ones()
        {
            var r = GameSettings.Parse("field_of_view = 60\nfield_of_view = 80");
            Assert.Empty(r.Problems);
            Assert.Equal(80f, r.Settings.FieldOfViewDeg);
        }

        [Fact]
        public void Numbers_use_invariant_culture_with_a_dot()
        {
            var old = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                var r = GameSettings.Parse("mouse_sensitivity = 2.5\nhud_scale = 1,5");
                Assert.Equal(2.5f, r.Settings.MouseSensitivity);
                Assert.Single(r.Problems);
                Assert.Equal(2, r.Problems[0].Line);
                Assert.Equal(1f, r.Settings.HudScale);
            }
            finally { System.Threading.Thread.CurrentThread.CurrentCulture = old; }
        }

        [Fact]
        public void Unknown_key_is_a_problem_and_other_lines_still_apply()
        {
            var r = GameSettings.Parse("fov_extra = 3\nfield_of_view = 80");
            Assert.Single(r.Problems);
            Assert.Equal(1, r.Problems[0].Line);
            Assert.Equal("fov_extra", r.Problems[0].Key);
            Assert.Equal("unknown key", r.Problems[0].Message);
            Assert.Equal(80f, r.Settings.FieldOfViewDeg);
        }

        [Fact]
        public void Line_without_equals_is_a_problem()
        {
            var r = GameSettings.Parse("invert_y true\nsubtitles = false");
            Assert.Single(r.Problems);
            Assert.Equal(1, r.Problems[0].Line);
            Assert.Equal("missing '='", r.Problems[0].Message);
            Assert.False(r.Settings.Subtitles);
            Assert.False(r.Settings.InvertY);
        }

        [Theory]
        [InlineData("mouse_sensitivity = fast")]
        [InlineData("invert_y = maybe")]
        [InlineData("simulation_rate = 1.5")]
        [InlineData("foot_view = x")]
        [InlineData("police_difficulty = brutal")]
        [InlineData("field_of_view = ")]
        public void Unparseable_value_is_a_bad_value_problem_leaving_the_default(string line)
        {
            var r = GameSettings.Parse(line);
            Assert.Single(r.Problems);
            Assert.Equal(1, r.Problems[0].Line);
            Assert.Equal("bad value", r.Problems[0].Message);
            AssertSame(new GameSettings(), r.Settings);
        }

        [Theory]
        [InlineData("mouse_sensitivity = 0.05")]
        [InlineData("mouse_sensitivity = 5.5")]
        [InlineData("field_of_view = 49")]
        [InlineData("field_of_view = 101")]
        [InlineData("foot_view = 4")]
        [InlineData("foot_view = -1")]
        [InlineData("vehicle_view = 9")]
        [InlineData("hud_scale = 0.4")]
        [InlineData("hud_scale = 2.1")]
        [InlineData("simulation_rate = 0")]
        [InlineData("simulation_rate = 4")]
        [InlineData("volume_master = 1.1")]
        [InlineData("volume_effects = -0.1")]
        [InlineData("volume_music = 2")]
        public void Out_of_range_value_is_reported_and_the_default_kept(string line)
        {
            var r = GameSettings.Parse(line);
            Assert.Single(r.Problems);
            Assert.Equal("out of range", r.Problems[0].Message);
            AssertSame(new GameSettings(), r.Settings);
        }

        [Theory]
        [InlineData("mouse_sensitivity = 0.1", 0.1f)]
        [InlineData("mouse_sensitivity = 5", 5f)]
        public void Range_bounds_are_inclusive(string line, float expected)
        {
            var r = GameSettings.Parse(line);
            Assert.Empty(r.Problems);
            Assert.Equal(expected, r.Settings.MouseSensitivity);
        }

        [Fact]
        public void Problems_are_listed_in_file_order_with_line_numbers()
        {
            var r = GameSettings.Parse("bogus = 1\n# c\nfield_of_view = 500\nno equals\nhud_scale = 1.25");
            Assert.Equal(3, r.Problems.Count);
            Assert.Equal(1, r.Problems[0].Line);
            Assert.Equal(3, r.Problems[1].Line);
            Assert.Equal(4, r.Problems[2].Line);
            Assert.Equal(1.25f, r.Settings.HudScale);
        }

        [Fact]
        public void Garbage_input_never_throws()
        {
            var r = GameSettings.Parse("=\n= =\n\0\u0001 = ÿ\nbind. = \nbind.Pause\nmove.Nope = W\n" + new string('x', 100000) + "\n==1");
            Assert.NotNull(r.Settings);
            Assert.NotNull(r.Problems);
            Assert.NotEmpty(r.Problems);
        }

        [Fact]
        public void Parse_reads_bindings_with_primary_and_secondary()
        {
            var r = GameSettings.Parse("bind.Pause = Escape, P\nbind.Jump = Q\nbind.Sprint = , RightShift\nmove.Forward = UpArrow, W");
            Assert.Empty(r.Problems);
            var b = r.Settings.Bindings;
            Assert.Equal("Escape", b.Get(GameButton.Pause, 0));
            Assert.Equal("P", b.Get(GameButton.Pause, 1));
            Assert.Equal("Q", b.Get(GameButton.Jump, 0));
            Assert.Equal("", b.Get(GameButton.Jump, 1));
            Assert.Equal("", b.Get(GameButton.Sprint, 0));
            Assert.Equal("RightShift", b.Get(GameButton.Sprint, 1));
            Assert.Equal("UpArrow", b.GetMove(MoveKey.Forward, 0));
            Assert.Equal("W", b.GetMove(MoveKey.Forward, 1));
        }

        [Fact]
        public void Bad_binding_lines_are_problems_and_leave_the_default()
        {
            var r = GameSettings.Parse("bind.Jump = NotAKey\nbind.Teleport = Q\nmove.Sideways = Q\nbind.Reload = R, Q, Z");
            Assert.True(r.Problems.Count >= 3);
            Assert.Equal(1, r.Problems[0].Line);
            Assert.Equal(2, r.Problems[1].Line);
            Assert.Equal(3, r.Problems[2].Line);
            Assert.Equal("Space", r.Settings.Bindings.Get(GameButton.Jump, 0));
        }

        [Fact]
        public void ToText_of_defaults_round_trips_with_no_problems_and_uses_lf()
        {
            var s = new GameSettings();
            string text = s.ToText();
            Assert.DoesNotContain("\r", text);
            Assert.Contains("mouse_sensitivity = 1", text);
            Assert.Contains("bind.Pause = Escape, P", text);
            Assert.Contains("move.Forward = W", text);
            var r = GameSettings.Parse(text);
            Assert.Empty(r.Problems);
            AssertSame(s, r.Settings);
        }

        [Fact]
        public void ToText_lists_every_setting_and_every_binding()
        {
            string text = new GameSettings().ToText();
            string[] keys = { "mouse_sensitivity", "invert_y", "field_of_view", "foot_view", "vehicle_view", "hud_scale", "minimap", "subtitles", "simulation_rate", "police_difficulty", "volume_master", "volume_effects", "volume_music" };
            foreach (var k in keys) Assert.Contains("\n" + k + " = ", "\n" + text);
            foreach (GameButton g in System.Enum.GetValues(typeof(GameButton))) Assert.Contains("bind." + g + " = ", text);
            foreach (MoveKey m in System.Enum.GetValues(typeof(MoveKey))) Assert.Contains("move." + m + " = ", text);
        }

        [Fact]
        public void ToText_does_not_write_the_removed_volume_voices_key()
        {
            Assert.DoesNotContain("volume_voices", new GameSettings().ToText());
        }

        [Theory]
        [InlineData("volume_voices = loud")]
        [InlineData("volume_voices = -1")]
        public void Legacy_volume_voices_key_is_accepted_with_no_problem_whatever_the_value(string line)
        {
            var r = GameSettings.Parse(line);
            Assert.Empty(r.Problems);
            AssertSame(new GameSettings(), r.Settings);
        }

        [Fact]
        public void Legacy_volume_voices_line_does_not_stop_other_keys_being_read()
        {
            var r = GameSettings.Parse("volume_master = 0.5\nvolume_voices = loud\nhud_scale = 1.5\nvolume_music = 0.2");
            Assert.Empty(r.Problems);
            Assert.Equal(0.5f, r.Settings.MasterVolume);
            Assert.Equal(1.5f, r.Settings.HudScale);
            Assert.Equal(0.2f, r.Settings.MusicVolume);
        }

        [Fact]
        public void Non_default_settings_round_trip_exactly()
        {
            var s = new GameSettings
            {
                MouseSensitivity = 3.25f, InvertY = true, FieldOfViewDeg = 93.5f, FootView = 3, VehicleView = 0,
                HudScale = 1.75f, MinimapEnabled = false, Subtitles = false, SimulationRate = 2,
                Police = PoliceDifficulty.Easy, MasterVolume = 0.35f, EffectsVolume = 0.1f, MusicVolume = 0f,
            };
            s.Bindings.Set(GameButton.Jump, 0, "Alpha1");
            s.Bindings.Set(GameButton.Jump, 1, "Keypad5");
            s.Bindings.Set(GameButton.Pause, 0, "");
            s.Bindings.Set(GameButton.Attack, 1, "Mouse2");
            s.Bindings.SetMove(MoveKey.Forward, 0, "UpArrow");
            s.Bindings.SetMove(MoveKey.Right, 1, "RightArrow");
            var r = GameSettings.Parse(s.ToText());
            Assert.Empty(r.Problems);
            AssertSame(s, r.Settings);
        }

        [Fact]
        public void Clamp_forces_values_into_range_and_nan_to_default()
        {
            var s = new GameSettings
            {
                MouseSensitivity = 50f, FieldOfViewDeg = 10f, FootView = 9, VehicleView = -2, HudScale = 0f,
                SimulationRate = 7, MasterVolume = 3f, EffectsVolume = -1f, MusicVolume = float.NaN,
            };
            s.Clamp();
            Assert.Equal(5f, s.MouseSensitivity);
            Assert.Equal(50f, s.FieldOfViewDeg);
            Assert.Equal(3, s.FootView);
            Assert.Equal(0, s.VehicleView);
            Assert.Equal(0.5f, s.HudScale);
            Assert.Equal(3, s.SimulationRate);
            Assert.Equal(1f, s.MasterVolume);
            Assert.Equal(0f, s.EffectsVolume);
            Assert.Equal(0.6f, s.MusicVolume);
        }

        [Fact]
        public void Clamp_replaces_nan_everywhere_with_defaults_and_keeps_valid_values()
        {
            var s = new GameSettings { MouseSensitivity = float.NaN, FieldOfViewDeg = float.NaN, HudScale = float.NaN, MasterVolume = 0.2f };
            s.Clamp();
            Assert.Equal(1f, s.MouseSensitivity);
            Assert.Equal(70f, s.FieldOfViewDeg);
            Assert.Equal(1f, s.HudScale);
            Assert.Equal(0.2f, s.MasterVolume);
        }
    }
}
