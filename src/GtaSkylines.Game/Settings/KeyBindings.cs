using System.Collections.Generic;
using GtaSkylines.Game.Input;

namespace GtaSkylines.Game.Settings
{
    /// <summary>Movement keys, which are axes rather than <see cref="GameButton"/>s.</summary>
    public enum MoveKey
    {
        /// <summary>Forward / throttle. Default W.</summary>
        Forward = 0,
        /// <summary>Back / brake / reverse. Default S.</summary>
        Back = 1,
        /// <summary>Strafe or steer left. Default A.</summary>
        Left = 2,
        /// <summary>Strafe or steer right. Default D.</summary>
        Right = 3,
    }

    /// <summary>
    /// Physical keys per logical button: a primary and a secondary slot. Keys are Unity <c>KeyCode</c> names
    /// ("W", "LeftShift", "LeftControl", "Space", "Tab", "UpArrow", "Escape", "Return", "Backspace", "Alpha1", "F5", ...),
    /// mouse buttons "Mouse0".."Mouse2" and the wheel "WheelUp"/"WheelDown". An empty string means unbound.
    /// </summary>
    public sealed class KeyBindings
    {
        /// <summary>Primary slot index.</summary>
        public const int Primary = 0;
        /// <summary>Secondary slot index.</summary>
        public const int Secondary = 1;

        /// <summary>
        /// Default table: Sprint LeftShift/-; Jump Space/-; Sneak LeftControl/-; EnterExit F/-; Interact E/-; Attack Mouse0/-;
        /// Aim Mouse1/-; Reload R/-; WeaponWheel Tab/-; NextWeapon WheelUp/-; PrevWeapon WheelDown/-; HandsUp X/-;
        /// CameraView V/-; LookBehind C/-; Headlights H/-; Phone UpArrow/-; InteractionMenu M/-; Pause Escape/P; BigMap Z/-;
        /// MenuAccept Return/-; MenuBack Backspace/-; MenuUp UpArrow/W; MenuDown DownArrow/S; MenuLeft LeftArrow/A; MenuRight RightArrow/D.
        /// Index = (int)<see cref="GameButton"/>, [button, slot].
        /// </summary>
        public static readonly string[,] DefaultButtons =
        {
            { "LeftShift", "" },    // Sprint
            { "Space", "" },        // Jump
            { "LeftControl", "" },  // Sneak
            { "F", "" },            // EnterExit
            { "E", "" },            // Interact
            { "Mouse0", "" },       // Attack
            { "Mouse1", "" },       // Aim
            { "R", "" },            // Reload
            { "Tab", "" },          // WeaponWheel
            { "WheelUp", "" },      // NextWeapon
            { "WheelDown", "" },    // PrevWeapon
            { "X", "" },            // HandsUp
            { "V", "" },            // CameraView
            { "C", "" },            // LookBehind
            { "H", "" },            // Headlights
            { "UpArrow", "" },      // Phone
            { "M", "" },            // InteractionMenu
            { "Escape", "P" },      // Pause
            { "Z", "" },            // BigMap
            { "Return", "" },       // MenuAccept
            { "Backspace", "" },    // MenuBack
            { "UpArrow", "W" },     // MenuUp
            { "DownArrow", "S" },   // MenuDown
            { "LeftArrow", "A" },   // MenuLeft
            { "RightArrow", "D" },  // MenuRight
        };

        /// <summary>Default movement keys, [(int)MoveKey]: W, S, A, D (primary only; secondary is unbound).</summary>
        public static readonly string[] DefaultMove = { "W", "S", "A", "D" };

        /// <summary>Creates bindings equal to the defaults.</summary>
        public KeyBindings() { ResetToDefaults(); }

        private static readonly int ButtonCount = DefaultButtons.GetLength(0);
        private static readonly int MoveCount = DefaultMove.Length;
        private static readonly Dictionary<string, bool> ValidNames = BuildValidNames();

        private readonly string[,] buttons = new string[DefaultButtons.GetLength(0), 2];
        private readonly string[,] moves = new string[DefaultMove.Length, 2];

        private static Dictionary<string, bool> BuildValidNames()
        {
            var d = new Dictionary<string, bool>();
            for (char c = 'A'; c <= 'Z'; c++) d[c.ToString()] = true;
            for (int i = 0; i <= 9; i++) { d["Alpha" + i] = true; d["Keypad" + i] = true; }
            for (int i = 1; i <= 12; i++) d["F" + i] = true;
            for (int i = 0; i <= 2; i++) d["Mouse" + i] = true;
            string[] others =
            {
                "Space", "Tab", "Return", "Escape", "Backspace", "LeftShift", "RightShift", "LeftControl", "RightControl",
                "LeftAlt", "RightAlt", "UpArrow", "DownArrow", "LeftArrow", "RightArrow", "WheelUp", "WheelDown",
            };
            for (int i = 0; i < others.Length; i++) d[others[i]] = true;
            return d;
        }

        /// <summary>The key name in a slot (0 primary, 1 secondary); "" if unbound or the slot is out of range.</summary>
        public string Get(GameButton button, int slot)
        {
            return slot < 0 || slot > 1 ? "" : buttons[(int)button, slot];
        }

        /// <summary>The movement key name in a slot; "" if unbound or the slot is out of range.</summary>
        public string GetMove(MoveKey key, int slot)
        {
            return slot < 0 || slot > 1 ? "" : moves[(int)key, slot];
        }

        /// <summary>
        /// Binds a slot. Returns false and changes nothing if <paramref name="keyName"/> is not "" and not a valid name
        /// (<see cref="IsValidKeyName"/>) or the slot is outside 0..1. "" unbinds the slot. Binding a key already used by
        /// another action is allowed (see <see cref="FindConflicts"/>); a button may have the same key in both slots.
        /// </summary>
        public bool Set(GameButton button, int slot, string keyName)
        {
            if (slot < 0 || slot > 1 || !(keyName == "" || IsValidKeyName(keyName))) return false;
            buttons[(int)button, slot] = keyName;
            return true;
        }

        /// <summary>Same as <see cref="Set"/> for a movement key.</summary>
        public bool SetMove(MoveKey key, int slot, string keyName)
        {
            if (slot < 0 || slot > 1 || !(keyName == "" || IsValidKeyName(keyName))) return false;
            moves[(int)key, slot] = keyName;
            return true;
        }

        /// <summary>Restores every binding to the defaults.</summary>
        public void ResetToDefaults()
        {
            for (int b = 0; b < ButtonCount; b++) Reset((GameButton)b);
            for (int m = 0; m < MoveCount; m++)
            {
                moves[m, 0] = DefaultMove[m];
                moves[m, 1] = "";
            }
        }

        /// <summary>Restores one button's two slots to the defaults.</summary>
        public void Reset(GameButton button)
        {
            int b = (int)button;
            buttons[b, 0] = DefaultButtons[b, 0];
            buttons[b, 1] = DefaultButtons[b, 1];
        }

        /// <summary>Buttons (any slot) currently bound to the key name, in button order (new list; movement keys not included).</summary>
        public List<GameButton> FindConflicts(string keyName)
        {
            var list = new List<GameButton>();
            if (string.IsNullOrEmpty(keyName)) return list;
            for (int b = 0; b < ButtonCount; b++)
                if (buttons[b, 0] == keyName || buttons[b, 1] == keyName) list.Add((GameButton)b);
            return list;
        }

        /// <summary>
        /// True for names the Mod can read: A-Z, Alpha0-Alpha9, Keypad0-Keypad9, F1-F12, Space, Tab, Return, Escape, Backspace,
        /// LeftShift, RightShift, LeftControl, RightControl, LeftAlt, RightAlt, UpArrow, DownArrow, LeftArrow, RightArrow,
        /// Mouse0-Mouse2, WheelUp, WheelDown. Case-sensitive; "" is not valid here.
        /// </summary>
        public static bool IsValidKeyName(string keyName)
        {
            return keyName != null && ValidNames.ContainsKey(keyName);
        }

        /// <summary>True if every slot of every button and movement key equals the defaults.</summary>
        public bool IsDefault
        {
            get
            {
                for (int b = 0; b < ButtonCount; b++)
                    for (int s = 0; s < 2; s++)
                        if (buttons[b, s] != DefaultButtons[b, s]) return false;
                for (int m = 0; m < MoveCount; m++)
                    if (moves[m, 0] != DefaultMove[m] || moves[m, 1] != "") return false;
                return true;
            }
        }
    }
}
