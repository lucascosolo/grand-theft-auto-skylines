using System;
using System.Collections.Generic;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Settings;

namespace GtaSkylines.Mod.Options
{
    /// <summary>
    /// Text for the key rebinding buttons, with conflicts. Menu buttons (MenuAccept..MenuRight) only conflict with each
    /// other; every other button and the movement keys form the gameplay context. Unity-free.
    /// </summary>
    public static class BindingText
    {
        private static readonly int ButtonCount = Enum.GetValues(typeof(GameButton)).Length;
        private static readonly int MoveCount = Enum.GetValues(typeof(MoveKey)).Length;

        /// <summary>The enum name of a button or movement key.</summary>
        public static string ActionName(bool move, int index)
        {
            return move ? ((MoveKey)index).ToString() : ((GameButton)index).ToString();
        }

        /// <summary>Other actions in the same context bound to this slot's key (buttons, then movement keys).</summary>
        public static List<string> Conflicts(KeyBindings kb, bool move, int index, int slot)
        {
            var list = new List<string>();
            string key = Key(kb, move, index, slot);
            if (key == "") return list;
            bool menu = IsMenu(move, index);
            for (int b = 0; b < ButtonCount; b++)
                if ((move || b != index) && IsMenu(false, b) == menu && Bound(kb, false, b, key)) list.Add(ActionName(false, b));
            for (int m = 0; m < MoveCount; m++)
                if ((!move || m != index) && !menu && Bound(kb, true, m, key)) list.Add(ActionName(true, m));
            return list;
        }

        /// <summary>"(none)", the key name, or "Key (also A, B)".</summary>
        public static string Describe(KeyBindings kb, bool move, int index, int slot)
        {
            string key = Key(kb, move, index, slot);
            if (key == "") return "(none)";
            List<string> c = Conflicts(kb, move, index, slot);
            return c.Count == 0 ? key : key + " (also " + string.Join(", ", c.ToArray()) + ")";
        }

        private static bool IsMenu(bool move, int index)
        {
            return !move && index >= (int)GameButton.MenuAccept && index <= (int)GameButton.MenuRight;
        }

        private static bool Bound(KeyBindings kb, bool move, int index, string key)
        {
            return Key(kb, move, index, 0) == key || Key(kb, move, index, 1) == key;
        }

        private static string Key(KeyBindings kb, bool move, int index, int slot)
        {
            return (move ? kb.GetMove((MoveKey)index, slot) : kb.Get((GameButton)index, slot)) ?? "";
        }
    }
}
