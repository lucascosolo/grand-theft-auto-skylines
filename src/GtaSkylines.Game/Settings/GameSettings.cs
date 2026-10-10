using System.Collections.Generic;

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
        /// <summary>Voice and dialogue volume 0..1, default 1. Key <c>volume_voices</c>.</summary>
        public float VoiceVolume = 1f;
        /// <summary>Key bindings, never null. File lines <c>bind.&lt;GameButton name&gt; = Primary, Secondary</c> (e.g. <c>bind.Pause = Escape, P</c>) and <c>move.&lt;MoveKey name&gt; = Primary, Secondary</c>; an omitted or empty part is unbound.</summary>
        public KeyBindings Bindings = new KeyBindings();

        /// <summary>Creates settings equal to the defaults.</summary>
        public GameSettings() { }

        /// <summary>Forces every numeric field into its range (NaN becomes the default); enum-like ints are clamped too.</summary>
        public void Clamp() { throw new System.NotImplementedException(); }

        /// <summary>
        /// Parses <c>key = value</c> lines. Blank lines and lines starting with '#' are ignored; whitespace around key and value is
        /// trimmed; numbers use the invariant culture with '.' as the decimal separator; bools are true/false (case-insensitive).
        /// An unknown key, a line without '=', a value that does not parse, a number outside its range or an invalid key name
        /// adds a <see cref="SettingsProblem"/> and leaves that setting at its default; the other lines still apply. Later lines
        /// override earlier ones. A null or empty text gives the defaults and no problems. Never throws.
        /// </summary>
        public static SettingsParseResult Parse(string text) { throw new System.NotImplementedException(); }

        /// <summary>
        /// Serialises every setting and every binding (all <c>bind.*</c> and <c>move.*</c> lines, even defaults), one per line, '\n'
        /// line endings, numbers invariant, in the field order above, so that <c>Parse(ToText())</c> reproduces the settings exactly
        /// with no problems.
        /// </summary>
        public string ToText() { throw new System.NotImplementedException(); }
    }
}
