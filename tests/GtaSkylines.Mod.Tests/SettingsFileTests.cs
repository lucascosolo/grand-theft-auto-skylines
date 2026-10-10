using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Settings;
using GtaSkylines.Mod.Persistence;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class SettingsFileTests
    {
        private static string Fresh()
        {
            string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        [Fact]
        public void FileName_is_settings_cfg()
        {
            Assert.Equal("settings.cfg", SettingsFile.FileName);
        }

        [Fact]
        public void Load_missing_file_gives_defaults()
        {
            string path = Path.Combine(Fresh(), SettingsFile.FileName);
            GameSettings s = SettingsFile.Load(path, m => { });
            Assert.Equal(new GameSettings().ToText(), s.ToText());
        }

        [Fact]
        public void Load_logs_each_problem_with_its_key()
        {
            string path = Path.Combine(Fresh(), SettingsFile.FileName);
            File.WriteAllText(path, "mouse_sensitivity = 2.5\nbogus_key = 1\nfoot_view = banana\ninvert_y = true\n");
            var log = new List<string>();
            GameSettings s = SettingsFile.Load(path, log.Add);
            Assert.Equal(2, log.Count);
            Assert.Contains(log, m => m.Contains("bogus_key"));
            Assert.Contains(log, m => m.Contains("foot_view"));
            Assert.Equal(2.5f, s.MouseSensitivity);
            Assert.True(s.InvertY);
            Assert.Equal(0, s.FootView);
        }

        [Fact]
        public void Load_clean_file_logs_nothing()
        {
            string path = Path.Combine(Fresh(), SettingsFile.FileName);
            File.WriteAllText(path, new GameSettings().ToText());
            var log = new List<string>();
            SettingsFile.Load(path, log.Add);
            Assert.Empty(log);
        }

        [Fact]
        public void Load_unreadable_path_gives_defaults_and_logs()
        {
            string dir = Fresh();
            var log = new List<string>();
            GameSettings s = SettingsFile.Load(dir, log.Add);
            Assert.Equal(new GameSettings().ToText(), s.ToText());
            Assert.NotEmpty(log);
        }

        [Fact]
        public void Load_result_is_clamped()
        {
            string path = Path.Combine(Fresh(), SettingsFile.FileName);
            File.WriteAllText(path, "simulation_rate = 99\nmouse_sensitivity = 50\n");
            GameSettings s = SettingsFile.Load(path, m => { });
            Assert.InRange(s.SimulationRate, 1, 3);
            Assert.InRange(s.MouseSensitivity, 0.1f, 5f);
        }

        [Fact]
        public void Save_creates_parent_directory_and_writes_ToText()
        {
            string path = Path.Combine(Fresh(), "a", "b", SettingsFile.FileName);
            var s = new GameSettings { InvertY = true, MouseSensitivity = 3f };
            Assert.True(SettingsFile.Save(path, s, m => { }));
            Assert.Equal(s.ToText(), File.ReadAllText(path, Encoding.UTF8));
        }

        [Fact]
        public void Save_then_Load_round_trips()
        {
            string path = Path.Combine(Fresh(), SettingsFile.FileName);
            var s = new GameSettings { SimulationRate = 3, Subtitles = false, HudScale = 1.5f };
            s.Bindings.Set(GameButton.Sprint, 0, "Mouse2");
            s.Bindings.SetMove(MoveKey.Forward, 1, "UpArrow");
            Assert.True(SettingsFile.Save(path, s, m => { }));
            Assert.Equal(s.ToText(), SettingsFile.Load(path, m => { }).ToText());
        }

        [Fact]
        public void Save_failure_returns_false_and_logs()
        {
            string dir = Fresh();
            var log = new List<string>();
            Assert.False(SettingsFile.Save(dir, new GameSettings(), log.Add));
            Assert.NotEmpty(log);
        }
    }
}
