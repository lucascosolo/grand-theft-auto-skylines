using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Missions;
using GtaSkylines.Game.Phone;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Session;
using GtaSkylines.Game.World;
using GtaSkylines.Mod.Hud;
using GtaSkylines.Mod.Menus;

namespace GtaSkylines.Mod.Play
{
    /// <summary>What only the Mod can do for a menu command (CS1 save, leaving play, cursor and pause).</summary>
    public interface IPlayHost
    {
        /// <summary>Close the pause menu and resume play.</summary>
        void Resume();
        /// <summary>Settings were edited in place: apply and persist them.</summary>
        void ApplySettings();
        /// <summary>Trigger CS1's own save.</summary>
        void SaveGame();
        /// <summary>Leave play mode.</summary>
        void ReturnToCityBuilder();
    }

    /// <summary>
    /// <see cref="IMenuCommands"/> over a <see cref="GameParts"/> (pure; Unity-only work goes to <see cref="IPlayHost"/>).
    /// Every command is a no-op while the session is not running.
    /// </summary>
    public sealed class GameMenuCommands : IMenuCommands
    {
        /// <summary>Most outfits the wardrobe offers (same cap as the safehouse wardrobe point).</summary>
        public const int MaxOutfits = 24;
        /// <summary>Reason shown when the player ends a shift from the phone.</summary>
        public const string ShiftEnded = "Shift ended.";

        private readonly GameParts _g;
        private readonly IPlayHost _host;

        /// <summary>Commands for <paramref name="game"/>.</summary>
        public GameMenuCommands(GameParts game, IPlayHost host)
        {
            _g = game;
            _host = host;
        }

        private GameWorld W { get { return _g.Session.World; } }
        private bool Live { get { return _g.Session.Running; } }

        /// <inheritdoc />
        public void Resume() { _host.Resume(); }

        /// <inheritdoc />
        public void ChooseMenuOption(int option) { if (Live) _g.Session.ChooseMenuOption(option); }

        /// <inheritdoc />
        public void CloseMenu() { _g.Session.CloseMenu(); }

        /// <inheritdoc />
        public long PriceOf(ShopItem item) { return Shop.PriceOf(item, W.Profile); }

        /// <summary>Waypoint at (x, ground height, z).</summary>
        public void SetWaypoint(float x, float z) { _g.Session.SetWaypoint(new Vec3(x, W.City.GroundHeight(x, z), z)); }

        /// <inheritdoc />
        public void ClearWaypoint() { _g.Session.ClearWaypoint(); }

        /// <inheritdoc />
        public bool TryGetWaypoint(out float x, out float z)
        {
            x = W.Waypoint.X;
            z = W.Waypoint.Z;
            return W.HasWaypoint;
        }

        /// <inheritdoc />
        public void ApplySettings() { _host.ApplySettings(); }

        /// <inheritdoc />
        public void SaveGame() { _host.SaveGame(); }

        /// <inheritdoc />
        public void ReturnToCityBuilder() { _host.ReturnToCityBuilder(); }

        /// <summary>Slot = weapon id; owned weapons only. The combat system loads the magazine on the next step.</summary>
        public void SelectWeapon(int slot)
        {
            Ped p = W.Player;
            if (p == null || slot < 0 || slot >= WeaponInfo.Count || !W.Profile.OwnsWeapon((WeaponId)slot)) return;
            p.CurrentWeapon = slot;
        }

        /// <summary>
        /// Contacts row → call that contact (mission offer or "no work"); Messages row → marked read; Jobs row (index =
        /// <see cref="SideJobKind"/>) → ends that job when it runs, else tries to start it; Gps rows 0-3 →
        /// <see cref="GpsShortcut"/> (Shop, Garage, Hospital, Safehouse), row 4 → clear the waypoint. Bad indices are ignored.
        /// </summary>
        public void PhoneAction(PhoneApp app, int index)
        {
            if (!Live || index < 0) return;
            MissionRunner r = _g.Missions;
            PhoneService phone = r.Phone;
            switch (app)
            {
                case PhoneApp.Contacts:
                    if (index < phone.ContactList.Count) phone.Call(W, r.Campaign, phone.ContactList[index].Id);
                    break;
                case PhoneApp.Messages:
                    if (index < phone.Messages.Count) phone.Messages[index].Read = true;
                    break;
                case PhoneApp.Jobs:
                    if (index >= SideJobTable.KindCount) return;
                    var kind = (SideJobKind)index;
                    if (r.ActiveJob != null && r.ActiveJob.Kind == kind) r.EndSideJob(W, ShiftEnded);
                    else r.TryStartSideJob(W, kind);
                    break;
                case PhoneApp.Gps:
                    if (index < 4) phone.GpsTo(W, r.Places, r.Campaign, (GpsShortcut)index);
                    else if (index == 4) _g.Session.ClearWaypoint();
                    break;
            }
        }

        /// <summary>
        /// None clears the waypoint; Safehouse and Hospital use the phone's GPS shortcuts; Ammunation (gun store), Clothes
        /// (clothes shop), Food (restaurant) and PoliceStation route to the nearest such place (commercial buildings for
        /// the shops, police buildings for the station). Nothing found → waypoint unchanged.
        /// </summary>
        public void QuickGps(QuickGpsTarget target)
        {
            if (!Live) return;
            MissionRunner r = _g.Missions;
            switch (target)
            {
                case QuickGpsTarget.None: _g.Session.ClearWaypoint(); return;
                case QuickGpsTarget.Safehouse: r.Phone.GpsTo(W, r.Places, r.Campaign, GpsShortcut.Safehouse); return;
                case QuickGpsTarget.Hospital: r.Phone.GpsTo(W, r.Places, r.Campaign, GpsShortcut.Hospital); return;
                case QuickGpsTarget.Ammunation: Route(ServiceKind.Commercial, InteriorArchetype.GunStore); return;
                case QuickGpsTarget.Clothes: Route(ServiceKind.Commercial, InteriorArchetype.ClothesShop); return;
                case QuickGpsTarget.Food: Route(ServiceKind.Commercial, InteriorArchetype.Restaurant); return;
                case QuickGpsTarget.PoliceStation: Route(ServiceKind.Police, InteriorArchetype.PoliceStation); return;
            }
        }

        private void Route(ServiceKind service, InteriorArchetype archetype)
        {
            if (_g.Missions.Places == null || W.Player == null) return;
            CampaignPlace p = _g.Missions.Places.Nearest(service, archetype, W.Player.Position, 0f);
            if (p != null) _g.Session.SetWaypoint(p.Door);
        }

        /// <summary>Opens the wardrobe menu (outfits 0 .. min(<see cref="MaxOutfits"/>, model count) − 1) when inside an owned building.</summary>
        public void OpenWardrobe()
        {
            GameWorld w = W;
            if (!Live || !w.Interior.IsInside || w.Interior.Building == null || !w.Profile.OwnsProperty(w.Interior.Building.Id)) return;
            MenuRequest m = w.Menu;
            m.Close();
            m.Kind = MenuKind.Wardrobe;
            m.Title = "Wardrobe";
            m.Building = w.Interior.Building.Id;
            int n = System.Math.Min(MaxOutfits, w.City.CitizenModelCount);
            for (int i = 0; i < n; i++) m.Outfits.Add(i);
            m.Revision++;
        }

        /// <summary>Abandons the running mission and starts it again from its first step.</summary>
        public void RetryMission()
        {
            MissionRunner r = _g.Missions;
            if (!Live || r.State != RunnerState.Mission || r.ActiveMission == null) return;
            string id = r.ActiveMission.Id;
            r.AbortMission(W);
            r.TryStartMission(W, id);
        }

        /// <inheritdoc />
        public void AbortMission()
        {
            if (Live) _g.Missions.AbortMission(W);
        }
    }
}
