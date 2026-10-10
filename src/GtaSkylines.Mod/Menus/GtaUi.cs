using System;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Hud;
using GtaSkylines.Game.Roads;
using GtaSkylines.Game.Settings;
using GtaSkylines.Game.World;
using GtaSkylines.Mod.Hud;
using Skylines.Host;
using UnityEngine;

namespace GtaSkylines.Mod.Menus
{
    /// <summary>
    /// The UI root the session owns: the HUD, the city map, the pause menu, weapon wheel, phone, interaction menu and the
    /// Game's MenuRequest menus. The wiring calls <see cref="Tick"/> from Update, <see cref="OnGui"/> from OnGUI and the
    /// open/close methods from its input; <see cref="WantsCursor"/> tells it when to free the cursor (SetMenuOpen).
    /// </summary>
    public sealed class GtaUi : IDisposable
    {
        private const int MaxLoggedErrors = 5;
        private readonly HostLog _log;
        private readonly GameSettings _settings;
        private readonly IMenuCommands _cmds;
        private readonly IUiSounds _sounds;
        private readonly MenuKeys _keys = new MenuKeys();
        private readonly PauseMenu _pause = new PauseMenu();
        private readonly ShopMenu _shop = new ShopMenu();
        private readonly InteractionMenu _interaction = new InteractionMenu();
        private readonly PhoneMenu _phone = new PhoneMenu();
        private readonly WeaponWheel _wheel = new WeaponWheel();
        private HudStyle _style;
        private int _errors;
        private int _openedFrame = -1;

        /// <summary>Creates the UI. Call on the main thread after the city loaded.</summary>
        public GtaUi(HostLog log, GameSettings settings, IMenuCommands commands, IUiSounds sounds, RoadGraph roads, ICity city)
        {
            _log = log;
            _settings = settings;
            _cmds = commands;
            _sounds = sounds;
            Map = new CityMapTexture(log, roads, city);
        }

        /// <summary>Phone contents (filled by the wiring).</summary>
        public readonly PhoneView PhoneView = new PhoneView();
        /// <summary>Weapon wheel contents (filled by the wiring).</summary>
        public readonly WeaponWheelView WheelView = new WeaponWheelView();
        /// <summary>Interaction menu state (filled by the wiring).</summary>
        public readonly InteractionView InteractionView = new InteractionView();
        /// <summary>BRIEF/STATS contents (filled by the wiring).</summary>
        public readonly PauseView PauseView = new PauseView();

        /// <summary>The city map texture.</summary>
        public CityMapTexture Map { get; private set; }
        /// <summary>The HUD renderer (null until the first OnGui).</summary>
        public HudRenderer Hud { get; private set; }
        /// <summary>Pause menu open.</summary>
        public bool PauseOpen { get; private set; }
        /// <summary>Phone out.</summary>
        public bool PhoneOpen { get; private set; }
        /// <summary>Weapon wheel held.</summary>
        public bool WheelOpen { get; private set; }
        /// <summary>Interaction menu open.</summary>
        public bool InteractionOpen { get; private set; }
        /// <summary>A menu that needs the mouse cursor is open (pause, interaction, or a MenuRequest).</summary>
        public bool WantsCursor { get; private set; }
        /// <summary>True while the pause menu waits for a key to bind: the wiring must ignore game keys.</summary>
        public bool Rebinding { get { return PauseOpen && _pause.Rebinding; } }

        /// <summary>Per frame from Update: rebuilds the map when the road graph changed.</summary>
        public void Tick()
        {
            try
            {
                Map.Tick();
            }
            catch (Exception e)
            {
                if (_errors++ < MaxLoggedErrors) _log.Error("hud: map", e);
            }
        }

        /// <summary>Opens the pause menu on the map.</summary>
        public void OpenPause(Vector3 player)
        {
            CloseOverlays();
            PauseOpen = true;
            _openedFrame = Time.frameCount;
            _pause.Open(_settings, player.x, player.z);
            _sounds.Open();
        }

        /// <summary>Closes the pause menu (the wiring resumes the game).</summary>
        public void ClosePause()
        {
            PauseOpen = false;
        }

        /// <summary>Phone in or out.</summary>
        public void TogglePhone()
        {
            if (PauseOpen) return;
            PhoneOpen = !PhoneOpen;
            if (PhoneOpen)
            {
                _phone.Open();
                _openedFrame = Time.frameCount;
                _sounds.Open();
            }
            else _sounds.Back();
        }

        /// <summary>Interaction menu in or out.</summary>
        public void ToggleInteraction()
        {
            if (PauseOpen) return;
            InteractionOpen = !InteractionOpen;
            if (InteractionOpen)
            {
                PhoneOpen = false;
                _interaction.Open();
                _openedFrame = Time.frameCount;
                _sounds.Open();
            }
        }

        /// <summary>Starts holding the weapon wheel.</summary>
        public void OpenWheel()
        {
            if (PauseOpen || WheelOpen) return;
            WheelOpen = true;
            _wheel.Open(WheelView);
            _sounds.Open();
        }

        /// <summary>Feeds the mouse delta (pixels, y down) while the wheel is held.</summary>
        public void FeedWheel(float dx, float dy)
        {
            if (WheelOpen) _wheel.Feed(dx, dy, WheelView, _sounds);
        }

        /// <summary>Releases the wheel; equips the hovered weapon when <paramref name="commit"/>.</summary>
        public void CloseWheel(bool commit)
        {
            if (!WheelOpen) return;
            WheelOpen = false;
            int slot = _wheel.Commit(WheelView);
            if (commit && slot >= 0 && slot != WheelView.Equipped)
            {
                _cmds.SelectWeapon(slot);
                _sounds.Select();
            }
        }

        /// <summary>Z: enlarged minimap.</summary>
        public void ToggleBigMap()
        {
            if (Hud != null) Hud.BigMap = !Hud.BigMap;
        }

        /// <summary>Draws everything for this OnGUI event and handles menu input.</summary>
        public void OnGui(HudModel hud, MenuRequest menu)
        {
            try
            {
                Gui(hud, menu);
            }
            catch (Exception e)
            {
                GUI.color = Color.white;
                if (_errors++ < MaxLoggedErrors) _log.Error("hud: OnGUI", e);
            }
        }

        private void Gui(HudModel hud, MenuRequest menu)
        {
            if (_style == null)
            {
                _style = new HudStyle(_log);
                Hud = new HudRenderer(_style);
            }
            Event e = Event.current;
            bool menuOpen = menu != null && menu.IsOpen;
            bool listening = PauseOpen || menuOpen || InteractionOpen || PhoneOpen;
            MenuKey key = !listening || Time.frameCount == _openedFrame ? MenuKey.None : _keys.Read(e, _settings.Bindings);
            if (key != MenuKey.None) e.Use();

            float wx, wz;
            bool hasWaypoint = _cmds.TryGetWaypoint(out wx, out wz);
            Hud.NorthUp = _settings.MinimapNorthUp;
            Hud.Draw(hud, _settings, Map, PauseOpen, hasWaypoint, wx, wz);
            int gen = _style.Px(20f);

            if (PauseOpen)
            {
                if (!_pause.Draw(_style, _settings, PauseView, hud, Map, key, _cmds, _sounds, gen)) PauseOpen = false;
            }
            else if (menuOpen)
            {
                _shop.Draw(_style, menu, hud.Cash, key, _cmds, _sounds, gen);
            }
            else
            {
                _shop.Draw(_style, null, 0, MenuKey.None, _cmds, _sounds, gen);
                if (InteractionOpen && !_interaction.Draw(_style, InteractionView, key, _cmds, _sounds, gen)) InteractionOpen = false;
                else if (PhoneOpen && !InteractionOpen && !_phone.Draw(_style, PhoneView, key, _cmds, _sounds)) PhoneOpen = false;
            }
            if (WheelOpen && !PauseOpen) _wheel.Draw(_style, WheelView);
            WantsCursor = PauseOpen || InteractionOpen || menuOpen;
        }

        private void CloseOverlays()
        {
            PhoneOpen = false;
            InteractionOpen = false;
            WheelOpen = false;
        }

        /// <summary>Frees textures and render targets.</summary>
        public void Dispose()
        {
            if (Hud != null) Hud.Dispose();
            if (_style != null) _style.Dispose();
            Map.Dispose();
        }
    }
}
