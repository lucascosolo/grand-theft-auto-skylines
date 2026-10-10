using System;
using System.IO;
using ColossalFramework.IO;
using GtaSkylines.Game.Settings;
using GtaSkylines.Mod.Persistence;
using Skylines.Host;

namespace GtaSkylines.Mod
{
    /// <summary>Process-wide mod state: log, global settings, the loaded city's profile record.</summary>
    internal static class ModRuntime
    {
        public const string Name = "GtaSkylines";

        private static readonly object s_sync = new object();
        private static HostLog s_log;
        private static GameSettings s_settings;
        private static ProfileStore s_profiles;

        /// <summary>The mod log (<c>ModLogs/GtaSkylines.log</c> under CS1's user data folder).</summary>
        public static HostLog Log
        {
            get
            {
                lock (s_sync)
                {
                    if (s_log == null) s_log = new HostLog(Name);
                    return s_log;
                }
            }
        }

        /// <summary><c>&lt;DataLocation.localApplicationData&gt;/GtaSkylines</c>; never the game install.</summary>
        public static string DataFolder { get { return Path.Combine(DataLocation.localApplicationData, Name); } }

        public static string SettingsPath { get { return Path.Combine(DataFolder, SettingsFile.FileName); } }

        /// <summary>Global settings, loaded on first use. Main thread.</summary>
        public static GameSettings Settings
        {
            get
            {
                if (s_settings == null)
                {
                    s_settings = SettingsFile.Load(SettingsPath, Log.Warn);
                    Log.Info("settings loaded from " + SettingsPath);
                }
                return s_settings;
            }
        }

        /// <summary>Bumped on every settings change; readers rebuild derived state when it moves.</summary>
        public static int SettingsVersion { get; private set; }

        /// <summary>Records a settings change and writes the file.</summary>
        public static void SettingsChanged()
        {
            SettingsVersion++;
            SettingsFile.Save(SettingsPath, Settings, Log.Warn);
        }

        /// <summary>The loaded city's profile record (loading, saving and main threads).</summary>
        public static ProfileStore Profiles
        {
            get
            {
                lock (s_sync)
                {
                    if (s_profiles == null) s_profiles = new ProfileStore(m => Log.Warn(m));
                    return s_profiles;
                }
            }
        }

        /// <summary>Runs <paramref name="step"/>, logging any exception as <paramref name="what"/>.</summary>
        public static void Guard(string what, Action step)
        {
            try { step(); }
            catch (Exception e) { Log.Error(what, e); }
        }
    }
}
