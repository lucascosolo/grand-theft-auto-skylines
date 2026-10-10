using System;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Settings;
using GtaSkylines.Mod.Hud;
using UnityEngine;

namespace GtaSkylines.Mod.Menus
{
    /// <summary>A menu navigation key.</summary>
    public enum MenuKey
    {
        /// <summary>Nothing.</summary>
        None,
        /// <summary>MenuUp.</summary>
        Up,
        /// <summary>MenuDown.</summary>
        Down,
        /// <summary>MenuLeft.</summary>
        Left,
        /// <summary>MenuRight.</summary>
        Right,
        /// <summary>MenuAccept.</summary>
        Accept,
        /// <summary>MenuBack, Pause, or Escape.</summary>
        Back,
    }

    /// <summary>Reads menu keys from IMGUI key events through the player's bindings (re-parsed only when a binding changes).</summary>
    public sealed class MenuKeys
    {
        private static readonly GameButton[] Buttons =
        {
            GameButton.MenuUp, GameButton.MenuDown, GameButton.MenuLeft, GameButton.MenuRight, GameButton.MenuAccept, GameButton.MenuBack, GameButton.Pause,
        };
        private static readonly MenuKey[] Keys = { MenuKey.Up, MenuKey.Down, MenuKey.Left, MenuKey.Right, MenuKey.Accept, MenuKey.Back, MenuKey.Back };
        private readonly string[] _names = new string[Buttons.Length * 2];
        private readonly KeyCode[] _codes = new KeyCode[Buttons.Length * 2];
        private readonly string[] _moveNames = new string[8];
        private readonly KeyCode[] _moveCodes = new KeyCode[8];

        /// <summary>The menu key of this IMGUI event (KeyDown only), or None.</summary>
        public MenuKey Read(Event e, KeyBindings kb)
        {
            return Read(e, kb, false);
        }

        /// <summary>
        /// The menu key of this IMGUI event (KeyDown only), or None. With <paramref name="whileMoving"/> (the phone: the
        /// player keeps walking or driving), a key that is also a movement key is ignored, so W/A/S/D move the player
        /// and only the arrows navigate, as in GTA.
        /// </summary>
        public MenuKey Read(Event e, KeyBindings kb, bool whileMoving)
        {
            if (e.type != EventType.KeyDown || e.keyCode == KeyCode.None) return MenuKey.None;
            Refresh(kb);
            if (e.keyCode == KeyCode.Escape) return MenuKey.Back;
            if (whileMoving && IsMoveKey(e.keyCode, kb)) return MenuKey.None;
            for (int i = 0; i < _codes.Length; i++)
                if (_codes[i] == e.keyCode) return Keys[i / 2];
            return MenuKey.None;
        }

        /// <summary>True while a key bound to <paramref name="key"/> is held (map panning).</summary>
        public bool Held(MenuKey key, KeyBindings kb)
        {
            Refresh(kb);
            for (int i = 0; i < _codes.Length; i++)
                if (Keys[i / 2] == key && _codes[i] != KeyCode.None && UnityEngine.Input.GetKey(_codes[i])) return true;
            return false;
        }

        private bool IsMoveKey(KeyCode code, KeyBindings kb)
        {
            for (int m = 0; m < 4; m++)
                for (int slot = 0; slot < 2; slot++)
                {
                    int k = m * 2 + slot;
                    string n = kb.GetMove((MoveKey)m, slot);
                    if (!ReferenceEquals(n, _moveNames[k]))
                    {
                        _moveNames[k] = n;
                        _moveCodes[k] = Parse(n);
                    }
                    if (_moveCodes[k] == code) return true;
                }
            return false;
        }

        private void Refresh(KeyBindings kb)
        {
            for (int b = 0; b < Buttons.Length; b++)
                for (int slot = 0; slot < 2; slot++)
                {
                    string n = kb.Get(Buttons[b], slot);
                    int k = b * 2 + slot;
                    if (ReferenceEquals(n, _names[k])) continue;
                    _names[k] = n;
                    _codes[k] = Parse(n);
                }
        }

        /// <summary>Unity KeyCode of a binding name, None for unbound, wheel or unknown names.</summary>
        public static KeyCode Parse(string name)
        {
            if (string.IsNullOrEmpty(name) || name.StartsWith("Wheel", StringComparison.Ordinal)) return KeyCode.None;
            try
            {
                return (KeyCode)Enum.Parse(typeof(KeyCode), name, false);
            }
            catch (ArgumentException)
            {
                return KeyCode.None;
            }
        }
    }

    /// <summary>Rows a GTA list panel shows.</summary>
    public interface IMenuRows
    {
        /// <summary>Left text of a row.</summary>
        string Label(int row);
        /// <summary>Right text of a row (price, value), or null.</summary>
        string Value(int row);
        /// <summary>Colour of the value text when the row is not selected.</summary>
        Color ValueColour(int row);
    }

    /// <summary>
    /// The GTA V interaction-menu look: header banner, black sub-header with the row counter, rows (selected row white with
    /// black text), and a description box. Width 432 px at 1080p, top-left at the safe margin.
    /// </summary>
    public static class MenuPanel
    {
        /// <summary>Panel width at 1080p.</summary>
        public const float Width = 432f;
        /// <summary>Row height at 1080p.</summary>
        public const float RowHeight = 38f;

        /// <summary>
        /// Draws the panel (no banner when <paramref name="header"/> is null) and handles mouse hover/click on rows. Returns the row clicked this event (accept), or −1.
        /// <paramref name="counter"/> is the cached "3/12" text (or null).
        /// </summary>
        public static int Draw(HudStyle st, float x, float y, string header, string subHeader, string counter, MenuList list, IMenuRows rows,
            RichTextCache description, string descriptionText, int generation, IUiSounds sounds)
        {
            float w = st.Px(Width), rowH = st.Px(RowHeight), pad = st.Px(10f);
            float bannerH = st.Px(96f);
            if (header != null)
            {
                st.Fill(new Rect(x, y, w, bannerH), HudStyle.Banner);
                st.Title(new Rect(x, y, w, bannerH), header, st.Px(header.Length > 16 ? 34f : 46f), TextAnchor.MiddleCenter, Color.white, 1f);
                y += bannerH;
            }
            st.Fill(new Rect(x, y, w, rowH), Color.black);
            st.Plain(new Rect(x + pad, y, w, rowH), subHeader, 17f, TextAnchor.MiddleLeft, HudStyle.Blue, 1f);
            st.Plain(new Rect(x, y, w - pad, rowH), counter, 17f, TextAnchor.MiddleRight, Color.white, 1f);
            y += rowH;

            int clicked = -1;
            Event e = Event.current;
            int last = list.VisibleRows > 0 ? Math.Min(list.Count, list.First + list.VisibleRows) : list.Count;
            for (int i = list.First; i < last; i++)
            {
                var r = new Rect(x, y, w, rowH);
                bool sel = i == list.Selected, on = list.IsEnabled(i);
                st.Fill(r, sel ? new Color(0.94f, 0.94f, 0.94f, 0.95f) : new Color(0f, 0f, 0f, 0.55f));
                Color tc = !on ? new Color(0.5f, 0.5f, 0.5f, 1f) : sel ? Color.black : Color.white;
                st.Plain(new Rect(r.x + pad, r.y, w, rowH), rows.Label(i), 19f, TextAnchor.MiddleLeft, tc, 1f);
                string v = rows.Value(i);
                if (v != null) st.Plain(new Rect(r.x, r.y, w - pad, rowH), v, 19f, TextAnchor.MiddleRight, sel && on ? Color.black : on ? rows.ValueColour(i) : tc, 1f);
                if (r.Contains(e.mousePosition))
                {
                    if (e.type == EventType.MouseMove && !sel && list.Select(i)) sounds.Navigate();
                    if (e.type == EventType.MouseDown && e.button == 0)
                    {
                        list.Select(i);
                        clicked = i;
                        e.Use();
                    }
                }
                y += rowH;
            }
            if (e.type == EventType.ScrollWheel && new Rect(x, y - rowH * (last - list.First), w, rowH * (last - list.First)).Contains(e.mousePosition))
            {
                if (list.Scroll(e.delta.y > 0 ? 1 : -1)) sounds.Navigate();
                e.Use();
            }

            if (!string.IsNullOrEmpty(descriptionText))
            {
                y += st.Px(4f);
                float lineH = st.Px(24f);
                st.MeasureSize = st.Px(17f);
                description.Update(descriptionText, null, w - 2 * pad, st.Measurer, st.Px(5f), generation);
                st.Fill(new Rect(x, y, w, st.Px(3f)), Color.black);
                st.Fill(new Rect(x, y + st.Px(3f), w, description.Lines * lineH + 2 * pad), new Color(0f, 0f, 0f, 0.6f));
                st.Runs(description, x + pad, y + st.Px(3f) + pad, lineH, 17f, 1f, false, null);
            }
            return clicked;
        }

        /// <summary>Applies a navigation key to a list (Up/Down) and plays the move sound; true when handled.</summary>
        public static bool Navigate(MenuKey key, MenuList list, IUiSounds sounds)
        {
            if (key == MenuKey.Up) { if (list.Up()) sounds.Navigate(); return true; }
            if (key == MenuKey.Down) { if (list.Down()) sounds.Navigate(); return true; }
            return false;
        }

        /// <summary>Draws GL quads in screen pixels (y down) with the vertex-coloured material; call Begin/End around vertex calls.</summary>
        public static bool BeginScreenGl(HudStyle st)
        {
            Material m = st.Colored;
            if (m == null || Event.current.type != EventType.Repaint) return false;
            GL.PushMatrix();
            m.SetPass(0);
            GL.LoadPixelMatrix(0f, Screen.width, Screen.height, 0f);
            GL.Begin(GL.QUADS);
            return true;
        }

        /// <summary>Ends <see cref="BeginScreenGl"/>.</summary>
        public static void EndScreenGl()
        {
            GL.End();
            GL.PopMatrix();
        }
    }

    /// <summary>"3/12" style counters cached by value.</summary>
    public sealed class CounterText
    {
        private int _a = -1, _b = -1;
        private string _text;

        /// <summary>"a/b" (1-based a), same instance while unchanged.</summary>
        public string Get(int selected, int count)
        {
            if (selected != _a || count != _b)
            {
                _a = selected;
                _b = count;
                _text = (selected + 1) + "/" + count;
            }
            return _text;
        }
    }
}
