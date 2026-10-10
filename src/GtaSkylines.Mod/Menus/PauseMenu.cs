using System;
using System.Collections.Generic;
using GtaSkylines.Game.Hud;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Settings;
using GtaSkylines.Game.World;
using GtaSkylines.Mod.Hud;
using GtaSkylines.Mod.Input;
using GtaSkylines.Mod.Options;
using UnityEngine;
using UInput = UnityEngine.Input;

namespace GtaSkylines.Mod.Menus
{
    /// <summary>
    /// The GTA V pause menu (Esc/P): tab bar MAP, BRIEF, STATS, SETTINGS, CONTROLS, GAME. Keyboard (menu bindings) and
    /// mouse. Settings change live through <see cref="IMenuCommands.ApplySettings"/>; key rebinding behaves like the M1
    /// options page (next key, mouse button or wheel; Esc cancels; conflicts shown on the row).
    /// </summary>
    public sealed class PauseMenu : IMenuRows
    {
        private static readonly string[] TabNames = { "MAP", "BRIEF", "STATS", "SETTINGS", "CONTROLS", "GAME" };
        private static readonly string[] Views = { "First person", "Third person near", "Third person mid", "Third person far" };
        private static readonly string[] Rates = { "0.5x", "0.75x", "1x" };
        private static readonly string[] Police = { "Easy", "Normal", "Hard" };
        private static readonly string[] Settings =
        {
            "Mouse sensitivity", "Invert mouse Y", "Field of view", "Default view on foot", "Default view in vehicles", "HUD scale",
            "Minimap", "Minimap north up", "Subtitles", "Simulation speed while playing", "Police difficulty", "Master volume",
            "Effects volume", "Music and radio volume", "Voice volume", "Vehicle volume", "Menu and HUD volume",
        };
        private static readonly string[] GameRows = { "Resume", "Save game", "Return to City Builder" };
        private static readonly string[] ConfirmRows = { "Yes, return to City Builder", "No" };
        private static readonly int MoveCount = Enum.GetValues(typeof(MoveKey)).Length;
        private static readonly int ButtonCount = Enum.GetValues(typeof(GameButton)).Length;
        private static readonly BlipKind[] LegendKinds =
        {
            BlipKind.Waypoint, BlipKind.MissionStart, BlipKind.Target, BlipKind.Job, BlipKind.Shop, BlipKind.Safehouse, BlipKind.Cop,
        };
        private static readonly string[] LegendNames = { "Waypoint", "Mission", "Target", "Job", "Shop", "Safehouse", "Police" };

        private readonly PauseNav _nav = new PauseNav();
        private readonly MapViewState _map = new MapViewState();
        private readonly MenuList _settings = new MenuList(14);
        private readonly MenuList _game = new MenuList(0);
        private readonly MenuList _confirm = new MenuList(0);
        private readonly MenuList _controls = new MenuList(16);
        private readonly MenuList _stats = new MenuList(16);
        private readonly RichTextCache _desc = new RichTextCache();
        private readonly RichTextCache _brief = new RichTextCache();
        private readonly CounterText _counter = new CounterText();
        private readonly List<string> _values = new List<string>();
        private readonly List<string> _controlRows = new List<string>();
        private readonly List<string> _controlKeys = new List<string>();
        private KeyCode[] _codes;
        private string[] _codeNames;
        private int _rebindRow = -1;
        private int _rebindFrame;
        private string _rebindDesc;
        private int _rows; // which list the IMenuRows view shows: 0 settings, 1 game, 2 confirm, 3 controls, 4 stats
        private PauseView _view;
        private GameSettings _s;
        private float _lastTime;

        /// <summary>Opens on the map centred on the player.</summary>
        public void Open(GameSettings s, float playerX, float playerZ)
        {
            _nav.Open(PauseTab.Map);
            _nav.Accept();
            _map.CentreX = playerX;
            _map.CentreZ = playerZ;
            _s = s;
            _settings.Reset();
            _settings.SetCount(Settings.Length + (MoveCount + ButtonCount) * 2 + 1);
            _game.Reset();
            _game.SetCount(GameRows.Length);
            _values.Clear();
            for (int i = 0; i < _settings.Count; i++) _values.Add(null);
            BuildControls(s.Bindings);
            _lastTime = Time.realtimeSinceStartup;
        }

        /// <summary>True while a key rebind waits; the wiring must not treat the next key as a game key.</summary>
        public bool Rebinding { get { return _nav.Rebinding; } }

        /// <summary>Draws and handles input; returns false when the menu wants to close (resume).</summary>
        public bool Draw(HudStyle st, GameSettings s, PauseView view, HudModel hud, CityMapTexture cityMap, MenuKey key, IMenuCommands cmds,
            IUiSounds sounds, int generation)
        {
            _s = s;
            _view = view;
            float now = Time.realtimeSinceStartup, dt = Mathf.Clamp(now - _lastTime, 0f, 0.1f);
            if (Event.current.type == EventType.Repaint) _lastTime = now;
            if (_nav.Rebinding)
            {
                PollRebind(s, cmds, sounds);
                key = MenuKey.None;
            }

            if (key == MenuKey.Back)
            {
                sounds.Back();
                if (_nav.Back() == BackResult.Close)
                {
                    cmds.Resume();
                    return false;
                }
                key = MenuKey.None;
            }
            if (!_nav.InContent && !_nav.Confirming)
            {
                if (key == MenuKey.Left || key == MenuKey.Right)
                {
                    if (_nav.CycleTab(key == MenuKey.Right ? 1 : -1)) sounds.Tab();
                    if (_nav.Tab == PauseTab.Map) _nav.Accept();
                }
                else if (key == MenuKey.Accept && _nav.Accept()) sounds.Select();
                key = MenuKey.None;
            }

            float W = Screen.width, H = Screen.height, sx = W * 0.05f, top = H * 0.06f;
            st.Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.72f));
            st.Title(new Rect(sx, top - st.Px(64f), W, st.Px(60f)), view.CityName ?? "GTA: Skylines", st.Px(44f), TextAnchor.MiddleLeft, Color.white, 1f);
            st.Text(new Rect(0, top - st.Px(64f), W - sx, st.Px(60f)), view.CashText, 26f, TextAnchor.MiddleRight, Color.white, 1f);
            Tabs(st, sx, top, W - 2 * sx, sounds);
            var content = new Rect(sx, top + st.Px(52f), W - 2 * sx, H - top - st.Px(52f) - H * 0.08f);

            switch (_nav.Tab)
            {
                case PauseTab.Map: MapTab(st, content, hud, cityMap, key, cmds, sounds, dt); break;
                case PauseTab.Brief: BriefTab(st, content, view, generation); break;
                case PauseTab.Stats: _rows = 4; List(st, content, _stats, view.Stats.Count, key, sounds, generation, null); break;
                case PauseTab.Settings: SettingsTab(st, content, s, key, cmds, sounds, generation); break;
                case PauseTab.Controls: _rows = 3; List(st, content, _controls, _controlRows.Count, key, sounds, generation, null); break;
                default:
                    if (!GameTab(st, content, key, cmds, sounds, generation)) return false;
                    break;
            }
            Footer(st, W, H);
            return true;
        }

        private void Tabs(HudStyle st, float x, float y, float w, IUiSounds sounds)
        {
            float tw = w / PauseNav.TabCount, th = st.Px(44f);
            Event e = Event.current;
            for (int i = 0; i < PauseNav.TabCount; i++)
            {
                var r = new Rect(x + i * tw, y, tw - st.Px(4f), th);
                bool sel = (int)_nav.Tab == i;
                st.Fill(r, sel ? Color.white : new Color(0f, 0f, 0f, 0.6f));
                if (sel) st.Fill(new Rect(r.x, r.y, r.width, st.Px(4f)), _nav.InContent ? HudStyle.Banner : HudStyle.Mission);
                st.Plain(r, TabNames[i], 19f, TextAnchor.MiddleCenter, sel ? Color.black : Color.white, 1f);
                if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
                {
                    if (_nav.ClickTab((PauseTab)i)) sounds.Tab();
                    e.Use();
                }
            }
        }

        private void MapTab(HudStyle st, Rect r, HudModel hud, CityMapTexture cityMap, MenuKey key, IMenuCommands cmds, IUiSounds sounds, float dt)
        {
            Event e = Event.current;
            if (_nav.InContent)
            {
                float right = (_keys.Held(MenuKey.Right, _s.Bindings) ? 1f : 0f) - (_keys.Held(MenuKey.Left, _s.Bindings) ? 1f : 0f);
                float up = (_keys.Held(MenuKey.Up, _s.Bindings) ? 1f : 0f) - (_keys.Held(MenuKey.Down, _s.Bindings) ? 1f : 0f);
                if (e.type == EventType.Repaint && (right != 0f || up != 0f)) _map.PanKeys(right * 0.6f, up * 0.6f, r.width, r.height, dt);
                if (key == MenuKey.Accept) ToggleWaypoint(r.width * 0.5f, r.height * 0.5f, r, cmds, sounds);
                if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.PageUp || e.keyCode == KeyCode.PageDown))
                {
                    _map.Zoom(e.keyCode == KeyCode.PageUp ? 1f / 1.25f : 1.25f, r.width, r.height, r.width * 0.5f, r.height * 0.5f);
                    e.Use();
                }
            }
            if (r.Contains(e.mousePosition))
            {
                float mx = e.mousePosition.x - r.x, my = e.mousePosition.y - r.y;
                if (e.type == EventType.ScrollWheel)
                {
                    _map.Zoom(e.delta.y > 0 ? 1.2f : 1f / 1.2f, r.width, r.height, mx, my);
                    e.Use();
                }
                else if (e.type == EventType.MouseDrag && e.button == 0)
                {
                    _map.Drag(e.delta.x, e.delta.y);
                    e.Use();
                }
                else if (e.type == EventType.MouseDown && e.button == 1)
                {
                    ToggleWaypoint(mx, my, r, cmds, sounds);
                    e.Use();
                }
            }
            if (cityMap != null && cityMap.Ready)
                _map.ClampCentre(cityMap.MinX, cityMap.MinZ, cityMap.MinX + cityMap.Size, cityMap.MinZ + cityMap.Size);

            st.Fill(r, HudStyle.Rgb(0x3A3F44));
            if (cityMap != null && cityMap.Ready)
            {
                float x0, z0;
                _map.ScreenToWorld(r.width, r.height, 0f, r.height, out x0, out z0);
                float span = cityMap.Size;
                var uv = new Rect((x0 - cityMap.MinX) / span, (z0 - cityMap.MinZ) / span, r.width * _map.MetresPerPixel / span, r.height * _map.MetresPerPixel / span);
                GUI.DrawTextureWithTexCoords(r, cityMap.Texture, uv);
            }
            float icon = st.Px(20f);
            for (int i = 0; i < hud.Blips.Count; i++)
            {
                HudBlip b = hud.Blips[i];
                if (b.Kind == BlipKind.Player) continue;
                MapIcon(st, r, b.Kind, b.Position.X, b.Position.Z, icon, 0f);
            }
            float wx, wz;
            if (cmds.TryGetWaypoint(out wx, out wz)) MapIcon(st, r, BlipKind.Waypoint, wx, wz, icon, 0f);
            float yaw = 0f;
            for (int i = 0; i < hud.Blips.Count; i++)
                if (hud.Blips[i].Kind == BlipKind.Player) { yaw = hud.Blips[i].YawDeg; break; }
            MapIcon(st, r, BlipKind.Player, hud.PlayerPosition.X, hud.PlayerPosition.Z, st.Px(24f), yaw);
            if (_nav.InContent)
            {
                float c = st.Px(18f), t = st.Px(2f), cx = r.x + r.width * 0.5f, cy = r.y + r.height * 0.5f;
                st.Fill(new Rect(cx - c / 2, cy - t / 2, c, t), Color.white);
                st.Fill(new Rect(cx - t / 2, cy - c / 2, t, c), Color.white);
            }

            float lh = st.Px(26f), lw = st.Px(190f), pad = st.Px(10f);
            var legend = new Rect(r.x + pad, r.yMax - pad - lh * LegendKinds.Length - 2 * pad, lw, lh * LegendKinds.Length + 2 * pad);
            st.Fill(legend, new Color(0f, 0f, 0f, 0.7f));
            for (int i = 0; i < LegendKinds.Length; i++)
            {
                float y = legend.y + pad + i * lh;
                st.Icon(new Rect(legend.x + pad, y + lh * 0.15f, lh * 0.7f, lh * 0.7f), BlipStyle.Shape(LegendKinds[i]), BlipStyle.Colour(LegendKinds[i], 0f));
                st.Plain(new Rect(legend.x + pad + lh, y, lw, lh), LegendNames[i], 16f, TextAnchor.MiddleLeft, Color.white, 1f);
            }
        }

        private void MapIcon(HudStyle st, Rect r, BlipKind kind, float x, float z, float size, float yaw)
        {
            float sx, sy;
            _map.WorldToScreen(r.width, r.height, x, z, out sx, out sy);
            if (sx < 0 || sy < 0 || sx > r.width || sy > r.height) return;
            var rect = new Rect(r.x + sx - size / 2, r.y + sy - size / 2, size, size);
            if (kind == BlipKind.Player) st.IconRotated(rect, Icon.Arrow, Color.white, yaw);
            else st.Icon(rect, BlipStyle.Shape(kind), BlipStyle.Colour(kind, Time.realtimeSinceStartup));
        }

        private void ToggleWaypoint(float mx, float my, Rect r, IMenuCommands cmds, IUiSounds sounds)
        {
            float wx, wz, sx, sy;
            if (cmds.TryGetWaypoint(out wx, out wz))
            {
                _map.WorldToScreen(r.width, r.height, wx, wz, out sx, out sy);
                if ((sx - mx) * (sx - mx) + (sy - my) * (sy - my) < 400f)
                {
                    cmds.ClearWaypoint();
                    sounds.Back();
                    return;
                }
            }
            _map.ScreenToWorld(r.width, r.height, mx, my, out wx, out wz);
            cmds.SetWaypoint(wx, wz);
            sounds.Select();
        }

        private void BriefTab(HudStyle st, Rect r, PauseView view, int generation)
        {
            float pad = st.Px(16f), lh = st.Px(28f), w = r.width * 0.55f;
            st.Fill(new Rect(r.x, r.y, w, r.height), new Color(0f, 0f, 0f, 0.6f));
            float y = r.y + pad;
            st.Plain(new Rect(r.x + pad, y, w, lh), view.MissionTitle ?? "No active mission", 24f, TextAnchor.MiddleLeft, HudStyle.Mission, 1f);
            y += lh * 1.3f;
            if (view.Objective != null)
            {
                st.MeasureSize = st.Px(19f);
                _brief.Update(view.Objective, null, w - 2 * pad, st.Measurer, st.Px(5f), generation);
                st.Runs(_brief, r.x + pad, y, lh, 19f, 1f, false, null);
                y += _brief.Lines * lh + pad;
            }
            for (int i = 0; i < view.Log.Count && y < r.yMax - lh; i++, y += lh)
                st.Plain(new Rect(r.x + pad, y, w - 2 * pad, lh), view.Log[i], 17f, TextAnchor.MiddleLeft, new Color(0.8f, 0.8f, 0.8f, 1f), 1f);
            var jr = new Rect(r.x + w + pad, r.y, r.width - w - pad, r.height);
            st.Fill(jr, new Color(0f, 0f, 0f, 0.6f));
            st.Plain(new Rect(jr.x + pad, jr.y + pad, jr.width, lh), "SIDE JOBS", 20f, TextAnchor.MiddleLeft, HudStyle.Blue, 1f);
            for (int i = 0; i < view.Jobs.Count; i++)
            {
                float yy = jr.y + pad + lh * (i + 1.3f);
                st.Plain(new Rect(jr.x + pad, yy, jr.width - 2 * pad, lh), view.Jobs[i].Key, 18f, TextAnchor.MiddleLeft, Color.white, 1f);
                st.Plain(new Rect(jr.x, yy, jr.width - pad, lh), view.Jobs[i].Value, 18f, TextAnchor.MiddleRight, Color.white, 1f);
            }
        }

        private void List(HudStyle st, Rect r, MenuList list, int count, MenuKey key, IUiSounds sounds, int generation, string desc)
        {
            if (list.Count != count) list.SetCount(count);
            MenuPanel.Navigate(key, list, sounds);
            MenuPanel.Draw(st, r.x, r.y, null, TabNames[(int)_nav.Tab], _counter.Get(list.Selected, list.Count),
                list, this, _desc, desc, generation, sounds);
        }

        private void SettingsTab(HudStyle st, Rect r, GameSettings s, MenuKey key, IMenuCommands cmds, IUiSounds sounds, int generation)
        {
            _rows = 0;
            int sel = _settings.Selected;
            if (_nav.InContent && !_nav.Rebinding)
            {
                if (key == MenuKey.Left || key == MenuKey.Right) Adjust(s, sel, key == MenuKey.Right ? 1 : -1, cmds, sounds);
                else if (key == MenuKey.Accept) Accept(s, sel, cmds, sounds);
            }
            if (_nav.InContent) MenuPanel.Navigate(key, _settings, sounds);
            string desc = _nav.Rebinding ? _rebindDesc
                : sel < Settings.Length ? "~Left~ ~Right~ change the value. Changes apply at once." : "~Enter~ rebinds the key.";
            int clicked = MenuPanel.Draw(st, r.x, r.y, null, "SETTINGS", _counter.Get(_settings.Selected, _settings.Count),
                _settings, this, _desc, desc, generation, sounds);
            if (clicked >= 0 && !_nav.Rebinding)
            {
                if (!_nav.InContent) _nav.Accept();
                if (clicked < Settings.Length) Adjust(s, clicked, Event.current.mousePosition.x > r.x + st.Px(MenuPanel.Width) * 0.75f ? 1 : -1, cmds, sounds);
                else Accept(s, clicked, cmds, sounds);
            }
        }

        private void Adjust(GameSettings s, int row, int d, IMenuCommands cmds, IUiSounds sounds)
        {
            if (row < 0 || row >= Settings.Length) return;
            switch (row)
            {
                case 0: s.MouseSensitivity = Step(s.MouseSensitivity, d, 0.05f, 0.1f, 5f); break;
                case 1: s.InvertY = !s.InvertY; break;
                case 2: s.FieldOfViewDeg = Step(s.FieldOfViewDeg, d, 1f, 50f, 100f); break;
                case 3: s.FootView = (s.FootView + d + 4) % 4; break;
                case 4: s.VehicleView = (s.VehicleView + d + 4) % 4; break;
                case 5: s.HudScale = Step(s.HudScale, d, 0.05f, 0.5f, 2f); break;
                case 6: s.MinimapEnabled = !s.MinimapEnabled; break;
                case 7: s.MinimapNorthUp = !s.MinimapNorthUp; break;
                case 8: s.Subtitles = !s.Subtitles; break;
                case 9: s.SimulationRate = (s.SimulationRate - 1 + d + 3) % 3 + 1; break;
                case 10: s.Police = (PoliceDifficulty)(((int)s.Police + d + 3) % 3); break;
                case 11: s.MasterVolume = Step(s.MasterVolume, d, 0.05f, 0f, 1f); break;
                case 12: s.EffectsVolume = Step(s.EffectsVolume, d, 0.05f, 0f, 1f); break;
                case 13: s.MusicVolume = Step(s.MusicVolume, d, 0.05f, 0f, 1f); break;
                case 14: s.VoiceVolume = Step(s.VoiceVolume, d, 0.05f, 0f, 1f); break;
                case 15: s.VehiclesVolume = Step(s.VehiclesVolume, d, 0.05f, 0f, 1f); break;
                default: s.UiVolume = Step(s.UiVolume, d, 0.05f, 0f, 1f); break;
            }
            _values[row] = null;
            sounds.Navigate();
            cmds.ApplySettings();
        }

        private static float Step(float v, int d, float step, float lo, float hi)
        {
            return Mathf.Clamp(Mathf.Round((v + d * step) / step) * step, lo, hi);
        }

        private void Accept(GameSettings s, int row, IMenuCommands cmds, IUiSounds sounds)
        {
            if (row < Settings.Length) return;
            if (row == _settings.Count - 1)
            {
                s.Bindings.ResetToDefaults();
                BindingsChanged(s, cmds);
                sounds.Select();
                return;
            }
            if (!_nav.BeginRebind()) return;
            _rebindRow = row;
            _rebindFrame = Time.frameCount;
            _rows = 0;
            _rebindDesc = "Press a key or mouse button for " + Label(row) + ". ~Escape~ cancels.";
            _values[row] = "press a key...";
            sounds.Select();
        }

        private void PollRebind(GameSettings s, IMenuCommands cmds, IUiSounds sounds)
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown || e.type == EventType.MouseDown || e.type == EventType.ScrollWheel) e.Use();
            if (e.type != EventType.Repaint || Time.frameCount <= _rebindFrame) return;
            if (_codes == null) PrepareCodes();
            string name = null;
            bool cancel = UInput.GetKeyDown(KeyCode.Escape);
            if (!cancel)
            {
                float wheel = UInput.mouseScrollDelta.y;
                if (wheel > 0f) name = "WheelUp";
                else if (wheel < 0f) name = "WheelDown";
                else
                    for (int i = 0; i < _codes.Length; i++)
                        if (UInput.GetKeyDown(_codes[i])) { name = _codeNames[i]; break; }
                if (name == null) return;
            }
            _nav.EndRebind();
            int row = _rebindRow;
            _rebindRow = -1;
            if (name != null)
            {
                bool move; int index, slot;
                BindingOf(row, out move, out index, out slot);
                if (move) s.Bindings.SetMove((MoveKey)index, slot, name);
                else s.Bindings.Set((GameButton)index, slot, name);
                sounds.Select();
            }
            else sounds.Back();
            BindingsChanged(s, cmds);
        }

        private void BindingsChanged(GameSettings s, IMenuCommands cmds)
        {
            for (int i = Settings.Length; i < _values.Count; i++) _values[i] = null;
            BuildControls(s.Bindings);
            cmds.ApplySettings();
        }

        private void PrepareCodes()
        {
            var codes = new List<KeyCode>();
            var names = new List<string>();
            foreach (string n in KeyNames.All)
            {
                if (KeyNames.Classify(n) != KeySource.Key || n == "Escape") continue;
                KeyCode c = MenuKeys.Parse(n);
                if (c == KeyCode.None) continue;
                codes.Add(c);
                names.Add(n);
            }
            _codes = codes.ToArray();
            _codeNames = names.ToArray();
        }

        private static void BindingOf(int row, out bool move, out int index, out int slot)
        {
            int k = row - Settings.Length;
            slot = k % 2;
            k /= 2;
            move = k < MoveCount;
            index = move ? k : k - MoveCount;
        }

        private void BuildControls(KeyBindings kb)
        {
            _controlRows.Clear();
            _controlKeys.Clear();
            for (int m = 0; m < MoveCount; m++) Control(kb, true, m);
            for (int b = 0; b < ButtonCount; b++) Control(kb, false, b);
        }

        private void Control(KeyBindings kb, bool move, int index)
        {
            string a = move ? kb.GetMove((MoveKey)index, 0) : kb.Get((GameButton)index, 0);
            string b = move ? kb.GetMove((MoveKey)index, 1) : kb.Get((GameButton)index, 1);
            _controlRows.Add(BindingText.ActionName(move, index));
            _controlKeys.Add((string.IsNullOrEmpty(a) ? "-" : a) + (string.IsNullOrEmpty(b) ? "" : "  /  " + b));
        }

        private bool GameTab(HudStyle st, Rect r, MenuKey key, IMenuCommands cmds, IUiSounds sounds, int generation)
        {
            MenuList list = _nav.Confirming ? _confirm : _game;
            _rows = _nav.Confirming ? 2 : 1;
            if (_nav.Confirming && _confirm.Count != 2)
            {
                _confirm.SetCount(2);
                _confirm.Down();
            }
            MenuPanel.Navigate(key, list, sounds);
            int choose = key == MenuKey.Accept ? list.Selected : -1;
            string desc = _nav.Confirming ? "Return to the city builder? Progress is kept in this city's next save."
                : list.Selected == 1 ? "Saves the city and your progress (Cities: Skylines' own save)." : null;
            int clicked = MenuPanel.Draw(st, r.x, r.y, null, _nav.Confirming ? "CONFIRM" : "GAME", _counter.Get(list.Selected, list.Count),
                list, this, _desc, desc, generation, sounds);
            if (clicked >= 0)
            {
                if (!_nav.InContent) _nav.Accept();
                choose = clicked;
            }
            if (choose < 0) return true;
            sounds.Select();
            if (_nav.Confirming)
            {
                _nav.EndConfirm();
                _confirm.Reset();
                if (choose == 0) cmds.ReturnToCityBuilder();
                return choose != 0;
            }
            if (choose == 0)
            {
                cmds.Resume();
                return false;
            }
            if (choose == 1) cmds.SaveGame();
            else _nav.BeginConfirm();
            return true;
        }

        private readonly MenuKeys _keys = new MenuKeys();

        private void Footer(HudStyle st, float W, float H)
        {
            string hint = _nav.Tab == PauseTab.Map ? "Right click / Enter: waypoint    Wheel: zoom    Drag: pan    Esc: back"
                : _nav.InContent ? "Enter: select    Esc / Backspace: back" : "Left / Right: tabs    Enter: open    Esc: resume";
            st.Text(new Rect(0, H - H * 0.07f, W - W * 0.05f, H * 0.05f), hint, 17f, TextAnchor.MiddleRight, Color.white, 0.9f);
        }

        /// <inheritdoc/>
        public string Label(int row)
        {
            switch (_rows)
            {
                case 0:
                    if (row < Settings.Length) return Settings[row];
                    if (row == _settings.Count - 1) return "Reset all keys to defaults";
                    bool move; int index, slot;
                    BindingOf(row, out move, out index, out slot);
                    return slot == 0 ? BindingText.ActionName(move, index) : BindingText.ActionName(move, index) + " (2nd)";
                case 1: return GameRows[row];
                case 2: return ConfirmRows[row];
                case 3: return _controlRows[row];
                default: return _view.Stats[row].Key;
            }
        }

        /// <inheritdoc/>
        public string Value(int row)
        {
            switch (_rows)
            {
                case 0:
                    if (row == _settings.Count - 1) return null;
                    if (_values[row] == null) _values[row] = SettingValue(row);
                    return _values[row];
                case 3: return _controlKeys[row];
                case 4: return _view.Stats[row].Value;
                default: return null;
            }
        }

        /// <inheritdoc/>
        public Color ValueColour(int row)
        {
            return _rows == 0 && row >= Settings.Length && _values[row] != null && _values[row].IndexOf("(also", StringComparison.Ordinal) >= 0
                ? HudStyle.Mission : Color.white;
        }

        private string SettingValue(int row)
        {
            GameSettings s = _s;
            switch (row)
            {
                case 0: return "< " + s.MouseSensitivity.ToString("0.00") + " >";
                case 1: return s.InvertY ? "< On >" : "< Off >";
                case 2: return "< " + s.FieldOfViewDeg.ToString("0") + " >";
                case 3: return "< " + Views[Mathf.Clamp(s.FootView, 0, 3)] + " >";
                case 4: return "< " + Views[Mathf.Clamp(s.VehicleView, 0, 3)] + " >";
                case 5: return "< " + s.HudScale.ToString("0.00") + " >";
                case 6: return s.MinimapEnabled ? "< On >" : "< Off >";
                case 7: return s.MinimapNorthUp ? "< On >" : "< Off >";
                case 8: return s.Subtitles ? "< On >" : "< Off >";
                case 9: return "< " + Rates[Mathf.Clamp(s.SimulationRate - 1, 0, 2)] + " >";
                case 10: return "< " + Police[Mathf.Clamp((int)s.Police, 0, 2)] + " >";
                case 11: return "< " + Mathf.RoundToInt(s.MasterVolume * 100f) + "% >";
                case 12: return "< " + Mathf.RoundToInt(s.EffectsVolume * 100f) + "% >";
                case 13: return "< " + Mathf.RoundToInt(s.MusicVolume * 100f) + "% >";
                case 14: return "< " + Mathf.RoundToInt(s.VoiceVolume * 100f) + "% >";
                case 15: return "< " + Mathf.RoundToInt(s.VehiclesVolume * 100f) + "% >";
                case 16: return "< " + Mathf.RoundToInt(s.UiVolume * 100f) + "% >";
                default:
                    bool move; int index, slot;
                    BindingOf(row, out move, out index, out slot);
                    return BindingText.Describe(s.Bindings, move, index, slot);
            }
        }
    }
}
