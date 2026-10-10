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
        public KeyBindings() { }

        /// <summary>The key name in a slot (0 primary, 1 secondary); "" if unbound or the slot is out of range.</summary>
        public string Get(GameButton button, int slot) { throw new System.NotImplementedException(); }

        /// <summary>The movement key name in a slot; "" if unbound or the slot is out of range.</summary>
        public string GetMove(MoveKey key, int slot) { throw new System.NotImplementedException(); }

        /// <summary>
        /// Binds a slot. Returns false and changes nothing if <paramref name="keyName"/> is not "" and not a valid name
        /// (<see cref="IsValidKeyName"/>) or the slot is outside 0..1. "" unbinds the slot. Binding a key already used by
        /// another action is allowed (see <see cref="FindConflicts"/>); a button may have the same key in both slots.
        /// </summary>
        public bool Set(GameButton button, int slot, string keyName) { throw new System.NotImplementedException(); }

        /// <summary>Same as <see cref="Set"/> for a movement key.</summary>
        public bool SetMove(MoveKey key, int slot, string keyName) { throw new System.NotImplementedException(); }

        /// <summary>Restores every binding to the defaults.</summary>
        public void ResetToDefaults() { throw new System.NotImplementedException(); }

        /// <summary>Restores one button's two slots to the defaults.</summary>
        public void Reset(GameButton button) { throw new System.NotImplementedException(); }

        /// <summary>Buttons (any slot) currently bound to the key name, in button order (new list; movement keys not included).</summary>
        public List<GameButton> FindConflicts(string keyName) { throw new System.NotImplementedException(); }

        /// <summary>
        /// True for names the Mod can read: A-Z, Alpha0-Alpha9, Keypad0-Keypad9, F1-F12, Space, Tab, Return, Escape, Backspace,
        /// LeftShift, RightShift, LeftControl, RightControl, LeftAlt, RightAlt, UpArrow, DownArrow, LeftArrow, RightArrow,
        /// Mouse0-Mouse2, WheelUp, WheelDown. Case-sensitive; "" is not valid here.
        /// </summary>
        public static bool IsValidKeyName(string keyName) { throw new System.NotImplementedException(); }

        /// <summary>True if every slot of every button and movement key equals the defaults.</summary>
        public bool IsDefault { get { throw new System.NotImplementedException(); } }
    }
}
