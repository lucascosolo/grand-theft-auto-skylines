using System.Collections.Generic;
using GtaSkylines.Mod.Hud;
using UnityEngine;

namespace GtaSkylines.Mod.Menus
{
    /// <summary>
    /// The phone (Up arrow): a phone-shaped panel bottom-right with a 2 × 2 app grid and list apps. Keyboard only (the
    /// cursor stays locked while the phone is out, as in GTA): arrows move, Enter selects, Backspace goes back or closes.
    /// </summary>
    public sealed class PhoneMenu
    {
        private static readonly string[] AppNames = { "Contacts", "Messages", "Jobs", "GPS" };
        private static readonly Icon[] AppIcons = { Icon.Disc, Icon.Bubble, Icon.Square, Icon.Diamond };
        private static readonly Color[] AppColours =
        {
            HudStyle.Rgb(0x3FA34D), HudStyle.Rgb(0x2D6EB4), HudStyle.Rgb(0xD08B2C), HudStyle.Rgb(0x8E44AD),
        };
        private readonly MenuList _rows = new MenuList(6);
        private int _app;
        private bool _inApp;

        /// <summary>Back to the home screen.</summary>
        public void Open()
        {
            _app = 0;
            _inApp = false;
        }

        /// <summary>Draws the phone and handles a key; returns false when it closed.</summary>
        public bool Draw(HudStyle st, PhoneView view, MenuKey key, IMenuCommands cmds, IUiSounds sounds)
        {
            List<PhoneRow> rows = view.Rows((PhoneApp)_app);
            if (_inApp) _rows.SetCount(rows.Count);
            switch (key)
            {
                case MenuKey.Back:
                    sounds.Back();
                    if (!_inApp) return false;
                    _inApp = false;
                    break;
                case MenuKey.Accept:
                    if (!_inApp)
                    {
                        _inApp = true;
                        _rows.Reset();
                        _rows.SetCount(view.Rows((PhoneApp)_app).Count);
                        sounds.Select();
                    }
                    else if (_rows.Selected >= 0)
                    {
                        sounds.Select();
                        cmds.PhoneAction((PhoneApp)_app, _rows.Selected);
                    }
                    else sounds.Error();
                    break;
                case MenuKey.Up:
                case MenuKey.Down:
                case MenuKey.Left:
                case MenuKey.Right:
                    if (_inApp) MenuPanel.Navigate(key, _rows, sounds);
                    else
                    {
                        int d = key == MenuKey.Up || key == MenuKey.Down ? 2 : 1;
                        int next = (_app + (key == MenuKey.Up || key == MenuKey.Left ? 4 - d : d)) % 4;
                        if (next != _app) sounds.Navigate();
                        _app = next;
                    }
                    break;
            }

            float w = st.Px(300f), h = st.Px(560f), pad = st.Px(12f);
            var body = new Rect(Screen.width - Screen.width * 0.03f - w, Screen.height - Screen.height * 0.03f - h, w, h);
            st.Fill(new Rect(body.x - 2, body.y - 2, w + 4, h + 4), new Color(0.6f, 0.6f, 0.62f, 1f));
            st.Fill(body, HudStyle.Rgb(0x111214));
            var screen = new Rect(body.x + pad, body.y + pad * 3, w - 2 * pad, h - pad * 6);
            st.Fill(screen, HudStyle.Rgb(0x1E2328));
            float barH = st.Px(30f);
            st.Fill(new Rect(screen.x, screen.y, screen.width, barH), _inApp ? AppColours[_app] : Color.black);
            st.Plain(new Rect(screen.x + pad, screen.y, screen.width, barH), _inApp ? AppNames[_app] : "Home", 17f, TextAnchor.MiddleLeft, Color.white, 1f);
            st.Plain(new Rect(screen.x, screen.y, screen.width - pad, barH), view.Clock, 17f, TextAnchor.MiddleRight, Color.white, 1f);
            float top = screen.y + barH;

            if (!_inApp)
            {
                float cell = (screen.width - pad * 3) / 2f;
                for (int i = 0; i < 4; i++)
                {
                    var c = new Rect(screen.x + pad + (i % 2) * (cell + pad), top + pad + (i / 2) * (cell + pad), cell, cell);
                    if (i == _app) st.Fill(new Rect(c.x - 3, c.y - 3, c.width + 6, c.height + 6), Color.white);
                    st.Fill(c, AppColours[i]);
                    float ic = cell * 0.45f;
                    st.Icon(new Rect(c.x + (cell - ic) / 2, c.y + cell * 0.15f, ic, ic), AppIcons[i], Color.white);
                    st.Plain(new Rect(c.x, c.y + cell * 0.65f, cell, cell * 0.3f), AppNames[i], 16f, TextAnchor.MiddleCenter, Color.white, 1f);
                }
                return true;
            }

            float rowH = st.Px(58f);
            if (rows.Count == 0)
                st.Plain(new Rect(screen.x, top + pad, screen.width, rowH), "Nothing here.", 16f, TextAnchor.MiddleCenter, new Color(0.7f, 0.7f, 0.7f, 1f), 1f);
            int last = System.Math.Min(rows.Count, _rows.First + _rows.VisibleRows);
            for (int i = _rows.First; i < last; i++)
            {
                PhoneRow row = rows[i];
                var r = new Rect(screen.x, top + (i - _rows.First) * rowH, screen.width, rowH - 1);
                bool sel = i == _rows.Selected;
                st.Fill(r, sel ? Color.white : new Color(1f, 1f, 1f, 0.04f));
                Color tc = sel ? Color.black : Color.white;
                if (row.Highlight) st.Fill(new Rect(r.x, r.y, st.Px(4f), r.height), AppColours[_app]);
                st.Plain(new Rect(r.x + pad, r.y + st.Px(4f), r.width - pad, rowH * 0.5f), row.Title, 18f, TextAnchor.MiddleLeft, tc, 1f);
                st.Plain(new Rect(r.x + pad, r.y + rowH * 0.48f, r.width - pad, rowH * 0.45f), row.Detail, 14f, TextAnchor.MiddleLeft,
                    sel ? new Color(0.25f, 0.25f, 0.25f, 1f) : new Color(0.7f, 0.7f, 0.7f, 1f), 1f);
            }
            return true;
        }
    }

    /// <summary>
    /// The weapon wheel (hold Tab): radial segments drawn with GL, selection from the accumulated mouse direction, the
    /// hovered weapon's name and ammo in the centre. The session slows the game while it is open.
    /// </summary>
    public sealed class WeaponWheel
    {
        private float _x, _y;
        private int _hover = -1;

        /// <summary>Hovered slot, −1 when the stick is centred.</summary>
        public int Hover { get { return _hover; } }

        /// <summary>Resets the stick (opening).</summary>
        public void Open(WeaponWheelView view)
        {
            _x = _y = 0f;
            _hover = view.Equipped;
        }

        /// <summary>Feeds a mouse delta in pixels (screen x right, y down).</summary>
        public void Feed(float dx, float dy, WeaponWheelView view, IUiSounds sounds)
        {
            RadialMath.Accumulate(ref _x, ref _y, dx / 120f, dy / 120f, 1f);
            int seg = RadialMath.Segment(_x, _y, view.Slots.Count, 0.35f);
            if (seg >= 0 && seg != _hover && seg < view.Slots.Count && view.Slots[seg].Owned)
            {
                _hover = seg;
                sounds.Navigate();
            }
        }

        /// <summary>The slot to equip on release (owned hovered slot), or −1.</summary>
        public int Commit(WeaponWheelView view)
        {
            return _hover >= 0 && _hover < view.Slots.Count && view.Slots[_hover].Owned ? _hover : -1;
        }

        /// <summary>Draws the wheel.</summary>
        public void Draw(HudStyle st, WeaponWheelView view)
        {
            int n = view.Slots.Count;
            if (n == 0) return;
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f, R = st.Px(300f), r0 = st.Px(130f);
            float gapDeg = 1.2f, step = 360f / n;
            if (MenuPanel.BeginScreenGl(st))
            {
                for (int i = 0; i < n; i++)
                {
                    bool sel = i == _hover;
                    Color c = sel ? new Color(1f, 1f, 1f, 0.55f) : view.Slots[i].Owned ? new Color(0f, 0f, 0f, 0.6f) : new Color(0f, 0f, 0f, 0.35f);
                    float a0 = RadialMath.CentreDeg(i, n) - step / 2 + gapDeg, a1 = a0 + step - 2 * gapDeg;
                    const int k = 8;
                    for (int j = 0; j < k; j++)
                    {
                        float b0 = (a0 + (a1 - a0) * j / k) * Mathf.Deg2Rad, b1 = (a0 + (a1 - a0) * (j + 1) / k) * Mathf.Deg2Rad;
                        float s0 = Mathf.Sin(b0), c0 = -Mathf.Cos(b0), s1 = Mathf.Sin(b1), c1 = -Mathf.Cos(b1);
                        GL.Color(c);
                        GL.Vertex3(cx + s0 * r0, cy + c0 * r0, 0f);
                        GL.Vertex3(cx + s0 * R, cy + c0 * R, 0f);
                        GL.Vertex3(cx + s1 * R, cy + c1 * R, 0f);
                        GL.Vertex3(cx + s1 * r0, cy + c1 * r0, 0f);
                    }
                }
                MenuPanel.EndScreenGl();
            }
            float mid = (R + r0) * 0.5f, lw = st.Px(150f), lh = st.Px(24f);
            for (int i = 0; i < n; i++)
            {
                WheelSlot s = view.Slots[i];
                if (!s.Owned) continue;
                float a = RadialMath.CentreDeg(i, n) * Mathf.Deg2Rad;
                float x = cx + Mathf.Sin(a) * mid, y = cy - Mathf.Cos(a) * mid;
                Color tc = i == _hover ? Color.black : Color.white;
                st.Plain(new Rect(x - lw / 2, y - lh, lw, lh), s.Name, 17f, TextAnchor.MiddleCenter, tc, 1f);
                if (s.AmmoText != null) st.Plain(new Rect(x - lw / 2, y, lw, lh), s.AmmoText, 15f, TextAnchor.MiddleCenter, tc, 0.9f);
            }
            if (_hover >= 0 && _hover < n)
            {
                WheelSlot s = view.Slots[_hover];
                st.Text(new Rect(cx - r0, cy - st.Px(30f), 2 * r0, st.Px(32f)), s.Name, 26f, TextAnchor.MiddleCenter, Color.white, 1f);
                if (s.AmmoText != null) st.Text(new Rect(cx - r0, cy + st.Px(4f), 2 * r0, st.Px(28f)), s.AmmoText, 20f, TextAnchor.MiddleCenter, Color.white, 1f);
            }
        }
    }
}
