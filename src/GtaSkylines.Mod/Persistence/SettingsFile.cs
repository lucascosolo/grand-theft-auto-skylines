using System;
using System.IO;
using System.Text;
using GtaSkylines.Game.Settings;

namespace GtaSkylines.Mod.Persistence
{
    /// <summary>The global settings file (DESIGN F-SAVE-2): <see cref="GameSettings"/> text. Never throws. Unity-free.</summary>
    public static class SettingsFile
    {
        /// <summary>File name inside the mod's data folder.</summary>
        public const string FileName = "settings.cfg";

        /// <summary>Reads <paramref name="path"/>; defaults when it is missing or unreadable. Every problem is logged.</summary>
        public static GameSettings Load(string path, Action<string> log)
        {
            GameSettings settings;
            try
            {
                if (!File.Exists(path) && !Directory.Exists(path)) return new GameSettings();
                SettingsParseResult parsed = GameSettings.Parse(File.ReadAllText(path, Encoding.UTF8));
                foreach (SettingsProblem p in parsed.Problems)
                    log("settings line " + p.Line + " '" + p.Key + "': " + p.Message + "; default kept");
                settings = parsed.Settings;
            }
            catch (Exception e)
            {
                log("settings file " + path + " unreadable (" + e.Message + "); using defaults");
                settings = new GameSettings();
            }
            settings.Clamp();
            return settings;
        }

        /// <summary>Writes <paramref name="settings"/> to <paramref name="path"/>, creating its folder. False (logged) on failure.</summary>
        public static bool Save(string path, GameSettings settings, Action<string> log)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, settings.ToText(), new UTF8Encoding(false));
                return true;
            }
            catch (Exception e)
            {
                log("settings file " + path + " not written: " + e.Message);
                return false;
            }
        }
    }
}
