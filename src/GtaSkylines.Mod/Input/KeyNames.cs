using GtaSkylines.Game.Settings;

namespace GtaSkylines.Mod.Input
{
    /// <summary>How a bound key name is read.</summary>
    public enum KeySource
    {
        /// <summary>Unbound or not a readable name.</summary>
        None,
        /// <summary>A Unity <c>KeyCode</c> of the same name (keys and Mouse0-2).</summary>
        Key,
        /// <summary>Mouse wheel scrolled up this frame.</summary>
        WheelUp,
        /// <summary>Mouse wheel scrolled down this frame.</summary>
        WheelDown,
    }

    /// <summary>The key names a binding can hold (<see cref="KeyBindings.IsValidKeyName"/>). Unity-free.</summary>
    public static class KeyNames
    {
        /// <summary>Every bindable name, each once; the rebinding listener polls them in this order.</summary>
        public static readonly string[] All = Build();

        /// <summary>How <paramref name="name"/> is read; <see cref="KeySource.None"/> for "" or an invalid name.</summary>
        public static KeySource Classify(string name)
        {
            if (string.IsNullOrEmpty(name) || !KeyBindings.IsValidKeyName(name)) return KeySource.None;
            if (name == "WheelUp") return KeySource.WheelUp;
            if (name == "WheelDown") return KeySource.WheelDown;
            return KeySource.Key;
        }

        private static string[] Build()
        {
            var names = new System.Collections.Generic.List<string>();
            for (char c = 'A'; c <= 'Z'; c++) names.Add(c.ToString());
            for (int i = 0; i <= 9; i++) names.Add("Alpha" + i);
            for (int i = 0; i <= 9; i++) names.Add("Keypad" + i);
            for (int i = 1; i <= 12; i++) names.Add("F" + i);
            for (int i = 0; i <= 2; i++) names.Add("Mouse" + i);
            names.AddRange(new[]
            {
                "Space", "Tab", "Return", "Escape", "Backspace", "LeftShift", "RightShift", "LeftControl", "RightControl",
                "LeftAlt", "RightAlt", "UpArrow", "DownArrow", "LeftArrow", "RightArrow", "WheelUp", "WheelDown",
            });
            return names.ToArray();
        }
    }
}
