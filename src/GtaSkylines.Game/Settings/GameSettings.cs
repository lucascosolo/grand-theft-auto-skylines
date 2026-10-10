using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GtaSkylines.Game.Input;

namespace GtaSkylines.Game.Settings
{
    /// <summary>Police difficulty (DESIGN F-SET).</summary>
    public enum PoliceDifficulty
    {
        /// <summary>Fewer units, slower recognition.</summary>
        Easy = 0,
        /// <summary>Default.</summary>
        Normal = 1,
        /// <summary>More units, faster recognition.</summary>
        Hard = 2,
    }

    /// <summary>A problem found while parsing a settings file. Parsing never throws.</summary>
    public struct SettingsProblem
    {
        /// <summary>1-based line number.</summary>
        public int Line;
        /// <summary>The key text on that line (trimmed); "" if the line had no key.</summary>
        public string Key;
        /// <summary>What was wrong: "unknown key", "bad value", "out of range", "missing '='" or "invalid key name".</summary>
        public string Message;
    }

    /// <summary>Result of <see cref="GameSettings.Parse"/>.</summary>
    public sealed class SettingsParseResult
    {
        /// <summary>The settings: defaults overlaid with every valid line. Never null.</summary>
        public GameSettings Settings { get; private set; }
        /// <summary>Problems in file order; empty when the file was clean.</summary>
        public List<SettingsProblem> Problems { get; private set; }

        /// <summary>Creates a result.</summary>
        public SettingsParseResult(GameSettings settings, List<SettingsProblem> problems)
        {
            Settings = settings;
            Problems = problems;
        }
    }

    /// <summary>
    /// Global settings (DESIGN F-SET, F-SAVE-2), not per city. Every field has a default and a range; the file loader and
    /// <see cref="Clamp"/> keep values inside it.
    /// </summary>
    public sealed class GameSettings
    {
        /// <summary>Mouse sensitivity multiplier range 0.1..5, default 1. File key <c>mouse_sensitivity</c>.</summary>
        public float MouseSensitivity = 1f;
        /// <summary>Invert vertical look, default false. File key <c>invert_y</c> (true/false).</summary>
        public bool InvertY = false;
        /// <summary>Vertical field of view in degrees, 50..100, default 70. File key <c>field_of_view</c>.</summary>
        public float FieldOfViewDeg = 70f;
        /// <summary>Default on-foot camera view 0..3 (0 first person, 1 near, 2 mid, 3 far), default 0. Key <c>foot_view</c>.</summary>
        public int FootView = 0;
        /// <summary>Default in-vehicle camera view 0..3, default 1. Key <c>vehicle_view</c>.</summary>
        public int VehicleView = 1;
        /// <summary>HUD scale 0.5..2, default 1. Key <c>hud_scale</c>.</summary>
        public float HudScale = 1f;
        /// <summary>Minimap shown, default true. Key <c>minimap</c>.</summary>
        public bool MinimapEnabled = true;
        /// <summary>Minimap fixed north-up instead of rotating with the camera, default false. Key <c>minimap_north_up</c>.</summary>
        public bool MinimapNorthUp = false;
        /// <summary>Subtitles shown, default true. Key <c>subtitles</c>.</summary>
        public bool Subtitles = true;
        /// <summary>CS1 simulation speed while playing, integer 1..3, default 1. Key <c>simulation_rate</c>.</summary>
        public int SimulationRate = 1;
        /// <summary>Police difficulty, default Normal. Key <c>police_difficulty</c> (easy/normal/hard, case-insensitive).</summary>
        public PoliceDifficulty Police = PoliceDifficulty.Normal;
        /// <summary>Master volume 0..1, default 1. Key <c>volume_master</c>.</summary>
        public float MasterVolume = 1f;
        /// <summary>Sound effects volume 0..1, default 1. Key <c>volume_effects</c>.</summary>
        public float EffectsVolume = 1f;
        /// <summary>Music and radio volume 0..1, default 0.6. Key <c>volume_music</c>.</summary>
        public float MusicVolume = 0.6f;
        /// <summary>Vehicle sounds (engines, sirens, horns, crashes) volume 0..1, default 1. Key <c>volume_vehicles</c>.</summary>
        public float VehiclesVolume = 1f;
        /// <summary>Menu and HUD sounds volume 0..1, default 1. Key <c>volume_ui</c>.</summary>
        public float UiVolume = 1f;
        /// <summary>Key bindings, never null. File lines <c>bind.&lt;GameButton name&gt; = Primary, Secondary</c> (e.g. <c>bind.Pause = Escape, P</c>) and <c>move.&lt;MoveKey name&gt; = Primary, Secondary</c>; an omitted or empty part is unbound.</summary>
        public KeyBindings Bindings = new KeyBindings();

        /// <summary>Creates settings equal to the defaults.</summary>
        public GameSettings() { }

        /// <summary>Forces every numeric field into its range (NaN becomes the default); enum-like ints are clamped too.</summary>
        public void Clamp()
        {
            MouseSensitivity = ClampF(MouseSensitivity, 0.1f, 5f, 1f);
            FieldOfViewDeg = ClampF(FieldOfViewDeg, 50f, 100f, 70f);
            FootView = ClampI(FootView, 0, 3);
            VehicleView = ClampI(VehicleView, 0, 3);
            HudScale = ClampF(HudScale, 0.5f, 2f, 1f);
            SimulationRate = ClampI(SimulationRate, 1, 3);
            Police = (PoliceDifficulty)ClampI((int)Police, 0, 2);
            MasterVolume = ClampF(MasterVolume, 0f, 1f, 1f);
            EffectsVolume = ClampF(EffectsVolume, 0f, 1f, 1f);
            MusicVolume = ClampF(MusicVolume, 0f, 1f, 0.6f);
            VehiclesVolume = ClampF(VehiclesVolume, 0f, 1f, 1f);
            UiVolume = ClampF(UiVolume, 0f, 1f, 1f);
        }

        private static float ClampF(float v, float lo, float hi, float def)
        {
            if (float.IsNaN(v)) return def;
            return v < lo ? lo : (v > hi ? hi : v);
        }

        private static int ClampI(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }

        private const string BadValue = "bad value";
        private const string OutOfRange = "out of range";

        private static string ParseFloat(string v, float lo, float hi, ref float target)
        {
            float f;
            if (!float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out f) || float.IsNaN(f) || float.IsInfinity(f)) return BadValue;
            if (f < lo || f > hi) return OutOfRange;
            target = f;
            return null;
        }

        private static string ParseInt(string v, int lo, int hi, ref int target)
        {
            int i;
            if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out i)) return BadValue;
            if (i < lo || i > hi) return OutOfRange;
            target = i;
            return null;
        }

        private static string ParseBool(string v, ref bool target)
        {
            if (string.Equals(v, "true", StringComparison.OrdinalIgnoreCase)) target = true;
            else if (string.Equals(v, "false", StringComparison.OrdinalIgnoreCase)) target = false;
            else return BadValue;
            return null;
        }

        // Returns null on success, otherwise the problem message.
        private string Apply(string key, string value)
        {
            switch (key)
            {
                case "mouse_sensitivity": return ParseFloat(value, 0.1f, 5f, ref MouseSensitivity);
                case "invert_y": return ParseBool(value, ref InvertY);
                case "field_of_view": return ParseFloat(value, 50f, 100f, ref FieldOfViewDeg);
                case "foot_view": return ParseInt(value, 0, 3, ref FootView);
                case "vehicle_view": return ParseInt(value, 0, 3, ref VehicleView);
                case "hud_scale": return ParseFloat(value, 0.5f, 2f, ref HudScale);
                case "minimap": return ParseBool(value, ref MinimapEnabled);
                case "minimap_north_up": return ParseBool(value, ref MinimapNorthUp);
                case "subtitles": return ParseBool(value, ref Subtitles);
                case "simulation_rate": return ParseInt(value, 1, 3, ref SimulationRate);
                case "police_difficulty":
                    string[] names = { "easy", "normal", "hard" };
                    for (int i = 0; i < names.Length; i++)
                        if (string.Equals(value, names[i], StringComparison.OrdinalIgnoreCase)) { Police = (PoliceDifficulty)i; return null; }
                    return BadValue;
                case "volume_master": return ParseFloat(value, 0f, 1f, ref MasterVolume);
                case "volume_effects": return ParseFloat(value, 0f, 1f, ref EffectsVolume);
                case "volume_music": return ParseFloat(value, 0f, 1f, ref MusicVolume);
                // Retired (no voiced dialogue, DESIGN out of scope): accepted from old files, ignored, never written.
                case "volume_voices": return null;
                case "volume_vehicles": return ParseFloat(value, 0f, 1f, ref VehiclesVolume);
                case "volume_ui": return ParseFloat(value, 0f, 1f, ref UiVolume);
            }
            if (key.StartsWith("bind.", StringComparison.Ordinal))
            {
                int b = IndexOfName(typeof(GameButton), key.Substring(5));
                return b < 0 ? "unknown key" : ApplyBinding(value, false, b);
            }
            if (key.StartsWith("move.", StringComparison.Ordinal))
            {
                int m = IndexOfName(typeof(MoveKey), key.Substring(5));
                return m < 0 ? "unknown key" : ApplyBinding(value, true, m);
            }
            return "unknown key";
        }

        private static int IndexOfName(Type enumType, string name)
        {
            string[] names = Enum.GetNames(enumType);
            for (int i = 0; i < names.Length; i++)
                if (names[i] == name) return Convert.ToInt32(Enum.Parse(enumType, name, false));
            return -1;
        }

        private string ApplyBinding(string value, bool move, int index)
        {
            string[] parts = value.Split(',');
            if (parts.Length > 2) return BadValue;
            string primary = parts[0].Trim();
            string secondary = parts.Length > 1 ? parts[1].Trim() : "";
            if ((primary != "" && !KeyBindings.IsValidKeyName(primary)) || (secondary != "" && !KeyBindings.IsValidKeyName(secondary)))
                return "invalid key name";
            if (move)
            {
                Bindings.SetMove((MoveKey)index, 0, primary);
                Bindings.SetMove((MoveKey)index, 1, secondary);
            }
            else
            {
                Bindings.Set((GameButton)index, 0, primary);
                Bindings.Set((GameButton)index, 1, secondary);
            }
            return null;
        }

        /// <summary>
        /// Parses <c>key = value</c> lines. Blank lines and lines starting with '#' are ignored; whitespace around key and value is
        /// trimmed; numbers use the invariant culture with '.' as the decimal separator; bools are true/false (case-insensitive).
        /// An unknown key, a line without '=', a value that does not parse, a number outside its range or an invalid key name
        /// adds a <see cref="SettingsProblem"/> and leaves that setting at its default; the other lines still apply. Later lines
        /// override earlier ones. A null or empty text gives the defaults and no problems. Never throws.
        /// </summary>
        public static SettingsParseResult Parse(string text)
        {
            var settings = new GameSettings();
            var problems = new List<SettingsProblem>();
            if (!string.IsNullOrEmpty(text))
            {
                string[] lines = text.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    int eq = line.IndexOf('=');
                    SettingsProblem p;
                    p.Line = i + 1;
                    if (eq < 0)
                    {
                        p.Key = line;
                        p.Message = "missing '='";
                        problems.Add(p);
                        continue;
                    }
                    p.Key = line.Substring(0, eq).Trim();
                    string message = settings.Apply(p.Key, line.Substring(eq + 1).Trim());
                    if (message == null) continue;
                    p.Message = message;
                    problems.Add(p);
                }
            }
            return new SettingsParseResult(settings, problems);
        }

        /// <summary>
        /// Serialises every setting and every binding (all <c>bind.*</c> and <c>move.*</c> lines, even defaults), one per line, '\n'
        /// line endings, numbers invariant, in the field order above, so that <c>Parse(ToText())</c> reproduces the settings exactly
        /// with no problems.
        /// </summary>
        public string ToText()
        {
            var sb = new StringBuilder();
            AppendLine(sb, "mouse_sensitivity", F(MouseSensitivity));
            AppendLine(sb, "invert_y", B(InvertY));
            AppendLine(sb, "field_of_view", F(FieldOfViewDeg));
            AppendLine(sb, "foot_view", FootView.ToString(CultureInfo.InvariantCulture));
            AppendLine(sb, "vehicle_view", VehicleView.ToString(CultureInfo.InvariantCulture));
            AppendLine(sb, "hud_scale", F(HudScale));
            AppendLine(sb, "minimap", B(MinimapEnabled));
            AppendLine(sb, "minimap_north_up", B(MinimapNorthUp));
            AppendLine(sb, "subtitles", B(Subtitles));
            AppendLine(sb, "simulation_rate", SimulationRate.ToString(CultureInfo.InvariantCulture));
            AppendLine(sb, "police_difficulty", Police.ToString().ToLowerInvariant());
            AppendLine(sb, "volume_master", F(MasterVolume));
            AppendLine(sb, "volume_effects", F(EffectsVolume));
            AppendLine(sb, "volume_music", F(MusicVolume));
            AppendLine(sb, "volume_vehicles", F(VehiclesVolume));
            AppendLine(sb, "volume_ui", F(UiVolume));
            foreach (GameButton g in Enum.GetValues(typeof(GameButton)))
                AppendLine(sb, "bind." + g, Bindings.Get(g, 0) + ", " + Bindings.Get(g, 1));
            foreach (MoveKey m in Enum.GetValues(typeof(MoveKey)))
                AppendLine(sb, "move." + m, Bindings.GetMove(m, 0) + ", " + Bindings.GetMove(m, 1));
            return sb.ToString();
        }

        private static string F(float v) { return v.ToString("R", CultureInfo.InvariantCulture); }

        private static string B(bool v) { return v ? "true" : "false"; }

        private static void AppendLine(StringBuilder sb, string key, string value)
        {
            sb.Append(key).Append(" = ").Append(value).Append('\n');
        }
    }
}
