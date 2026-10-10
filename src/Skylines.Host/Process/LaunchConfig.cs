using System;
using System.Collections.Generic;
using System.IO;

namespace Skylines.Host
{
    /// <summary>When the in-game self-test runs by itself (launch.cfg <c>selftest</c>).</summary>
    public enum SelfTestMode
    {
        /// <summary>Only on Ctrl+Shift+T (<c>off</c>, the default).</summary>
        Off,

        /// <summary>Once after each city load (<c>city_load</c>).</summary>
        CityLoad,
    }

    /// <summary>
    /// Parsed <c>launch.cfg</c> in the mod data folder: plain <c>key = value</c> lines, <c>#</c> starts a comment line.
    /// Keys: <c>selftest</c> (<c>off</c> default, <c>city_load</c>), <c>selftest_quit</c> (<c>false</c> default,
    /// <c>true</c>: quit the game without saving after the self-test report) and <c>autoload</c> (a save name, or
    /// <c>new:&lt;map&gt;</c> for a new game on a map, once per game start; empty = off). Bad lines become
    /// <see cref="Problems"/> and leave the default. Pure logic: no Unity.
    /// </summary>
    public sealed class LaunchConfig
    {
        /// <summary>File name inside the mod data folder.</summary>
        public const string FileName = "launch.cfg";

        /// <summary>When the self-test starts by itself.</summary>
        public SelfTestMode SelfTest = SelfTestMode.Off;

        /// <summary>Quit the game after the self-test writes its report (unattended runs).</summary>
        public bool SelfTestQuit;

        /// <summary>Save to load, or <c>new:&lt;map&gt;</c>, from the main menu once per game start; empty means off.</summary>
        public string Autoload = "";

        /// <summary>True when <see cref="Load"/> read a file.</summary>
        public bool Found;

        /// <summary>Problems found while reading or parsing.</summary>
        public readonly List<string> Problems = new List<string>();

        /// <summary>Reads and parses a file. A missing file gives the defaults; any other failure is a problem, never an exception.</summary>
        public static LaunchConfig Load(string path)
        {
            try
            {
                if (!File.Exists(path) && !Directory.Exists(path)) return new LaunchConfig();
                LaunchConfig c = Parse(File.ReadAllText(path));
                c.Found = true;
                return c;
            }
            catch (Exception e)
            {
                var c = new LaunchConfig();
                c.Problems.Add("cannot read " + path + ": " + e.Message);
                return c;
            }
        }

        /// <summary>Parses file contents (null is empty).</summary>
        public static LaunchConfig Parse(string text)
        {
            var c = new LaunchConfig();
            string[] lines = (text ?? "").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    c.Problems.Add("line " + (i + 1) + ": expected key = value");
                    continue;
                }
                c.Set(line.Substring(0, eq).Trim(), line.Substring(eq + 1).Trim(), i + 1);
            }
            return c;
        }

        private void Set(string key, string value, int lineNo)
        {
            string lower = value.ToLowerInvariant();
            switch (key)
            {
                case "selftest":
                    if (lower == "off") SelfTest = SelfTestMode.Off;
                    else if (lower == "city_load") SelfTest = SelfTestMode.CityLoad;
                    else Problems.Add("line " + lineNo + ": selftest must be off or city_load");
                    break;
                case "selftest_quit":
                    if (lower == "true") SelfTestQuit = true;
                    else if (lower == "false") SelfTestQuit = false;
                    else Problems.Add("line " + lineNo + ": selftest_quit must be true or false");
                    break;
                case "autoload":
                    if (AutoloadTarget.NewGameMap(value) == "") Problems.Add("line " + lineNo + ": autoload new: needs a map name");
                    else Autoload = value;
                    break;
                default:
                    Problems.Add("line " + lineNo + ": unknown key '" + key + "'");
                    break;
            }
        }
    }
}
