using System;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Missions;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Session;
using GtaSkylines.Game.World;
using GtaSkylines.Mod.Hud;
using GtaSkylines.Mod.Menus;
using GtaSkylines.Mod.Play;
using Xunit;

namespace GtaSkylines.Mod.Tests.Play
{
    public class GameMenuCommandsTests
    {
        private static GameMenuCommands Make(PlayGame g, RecordingHost host = null)
        {
            return new GameMenuCommands(g.Parts, host ?? new RecordingHost());
        }

        // ---- host calls ----------------------------------------------------------------------------------------

        [Fact]
        public void Host_commands_are_forwarded_once_each()
        {
            var g = new PlayGame();
            var host = new RecordingHost();
            GameMenuCommands c = Make(g, host);
            c.Resume();
            c.ApplySettings();
            c.SaveGame();
            c.ReturnToCityBuilder();
            Assert.Equal(new[] { "Resume", "ApplySettings", "SaveGame", "ReturnToCityBuilder" }, host.Calls);
        }

        [Fact]
        public void PriceOf_is_the_shop_price_for_the_live_profile()
        {
            var g = new PlayGame();
            GameMenuCommands c = Make(g);
            foreach (ShopItem item in Shop.Items)
                Assert.Equal(Shop.PriceOf(item, g.Profile), c.PriceOf(item));
        }

        // ---- waypoint ------------------------------------------------------------------------------------------

        [Fact]
        public void SetWaypoint_uses_the_ground_height_and_TryGetWaypoint_reads_it_back()
        {
            var g = new PlayGame();
            g.City.Ground = 7f;
            GameMenuCommands c = Make(g);
            float x, z;
            Assert.False(c.TryGetWaypoint(out x, out z));
            c.SetWaypoint(120f, -45f);
            Assert.True(g.W.HasWaypoint);
            Assert.Equal(new Vec3(120f, 7f, -45f), g.W.Waypoint);
            Assert.True(c.TryGetWaypoint(out x, out z));
            Assert.Equal(120f, x);
            Assert.Equal(-45f, z);
        }

        [Fact]
        public void ClearWaypoint_removes_it()
        {
            var g = new PlayGame();
            GameMenuCommands c = Make(g);
            c.SetWaypoint(1f, 2f);
            c.ClearWaypoint();
            float x, z;
            Assert.False(c.TryGetWaypoint(out x, out z));
            Assert.False(g.W.HasWaypoint);
        }

        // ---- menu routing --------------------------------------------------------------------------------------

        [Fact]
        public void CloseMenu_closes_the_open_request()
        {
            var g = new PlayGame();
            g.W.Menu.Kind = MenuKind.Wardrobe;
            Make(g).CloseMenu();
            Assert.False(g.W.Menu.IsOpen);
        }

        // ---- weapons -------------------------------------------------------------------------------------------

        [Fact]
        public void SelectWeapon_equips_an_owned_weapon()
        {
            var g = new PlayGame();
            g.Profile.GiveWeapon(WeaponId.Smg);
            Make(g).SelectWeapon((int)WeaponId.Smg);
            Assert.Equal((int)WeaponId.Smg, g.Player.CurrentWeapon);
        }

        [Fact]
        public void SelectWeapon_ignores_unowned_and_out_of_range_slots()
        {
            var g = new PlayGame();
            g.Player.CurrentWeapon = (int)WeaponId.Fists;
            GameMenuCommands c = Make(g);
            c.SelectWeapon((int)WeaponId.Carbine);
            c.SelectWeapon(-1);
            c.SelectWeapon(WeaponInfo.Count);
            c.SelectWeapon(500);
            Assert.Equal((int)WeaponId.Fists, g.Player.CurrentWeapon);
        }

        // ---- phone ---------------------------------------------------------------------------------------------

        [Fact]
        public void PhoneAction_messages_marks_the_row_read()
        {
            var g = new PlayGame();
            g.Parts.Missions.Phone.ReceiveText(g.W, "Reyes", "Hello");
            Assert.False(g.Parts.Missions.Phone.Messages[0].Read);
            Make(g).PhoneAction(PhoneApp.Messages, 0);
            Assert.True(g.Parts.Missions.Phone.Messages[0].Read);
        }

        [Fact]
        public void PhoneAction_ignores_bad_indices()
        {
            var g = new PlayGame();
            g.Parts.Missions.Phone.ReceiveText(g.W, "Reyes", "Hello");
            GameMenuCommands c = Make(g);
            c.PhoneAction(PhoneApp.Messages, 5);
            c.PhoneAction(PhoneApp.Messages, -1);
            c.PhoneAction(PhoneApp.Contacts, 99);
            c.PhoneAction(PhoneApp.Jobs, SideJobTable.KindCount);
            c.PhoneAction(PhoneApp.Gps, 5);
            Assert.False(g.Parts.Missions.Phone.Messages[0].Read);
            Assert.Null(g.Parts.Missions.ActiveJob);
        }

        [Fact]
        public void PhoneAction_jobs_starts_then_ends_the_shift()
        {
            var g = new PlayGame();
            Car taxi = g.SpawnCar(4, new Vec3(10, 0, 10));
            g.W.PutInCar(g.Player, taxi, 0);
            g.Tick();
            GameMenuCommands c = Make(g);
            c.PhoneAction(PhoneApp.Jobs, (int)SideJobKind.Taxi);
            Assert.NotNull(g.Parts.Missions.ActiveJob);
            Assert.Equal(SideJobKind.Taxi, g.Parts.Missions.ActiveJob.Kind);
            c.PhoneAction(PhoneApp.Jobs, (int)SideJobKind.Taxi);
            Assert.Null(g.Parts.Missions.ActiveJob);
        }

        [Fact]
        public void PhoneAction_jobs_on_foot_does_not_start_a_shift()
        {
            var g = new PlayGame();
            Make(g).PhoneAction(PhoneApp.Jobs, (int)SideJobKind.Taxi);
            Assert.Null(g.Parts.Missions.ActiveJob);
        }

        [Fact]
        public void PhoneAction_gps_row_4_clears_the_waypoint()
        {
            var g = new PlayGame();
            GameMenuCommands c = Make(g);
            c.SetWaypoint(10f, 10f);
            c.PhoneAction(PhoneApp.Gps, 4);
            Assert.False(g.W.HasWaypoint);
        }

        [Fact]
        public void PhoneAction_gps_safehouse_without_property_leaves_the_waypoint()
        {
            var g = new PlayGame();
            GameMenuCommands c = Make(g);
            c.SetWaypoint(10f, 10f);
            c.PhoneAction(PhoneApp.Gps, 3);
            Assert.True(g.W.HasWaypoint);
            Assert.Equal(10f, g.W.Waypoint.X);
        }

        // ---- quick GPS -----------------------------------------------------------------------------------------

        [Fact]
        public void QuickGps_none_clears_the_waypoint()
        {
            var g = new PlayGame();
            GameMenuCommands c = Make(g);
            c.SetWaypoint(10f, 10f);
            c.QuickGps(QuickGpsTarget.None);
            Assert.False(g.W.HasWaypoint);
        }

        [Theory]
        [InlineData(QuickGpsTarget.Ammunation)]
        [InlineData(QuickGpsTarget.Clothes)]
        [InlineData(QuickGpsTarget.Food)]
        [InlineData(QuickGpsTarget.PoliceStation)]
        [InlineData(QuickGpsTarget.Safehouse)]
        public void QuickGps_with_nothing_found_leaves_the_waypoint_unchanged(QuickGpsTarget target)
        {
            var g = new PlayGame();
            GameMenuCommands c = Make(g);
            c.SetWaypoint(33f, 44f);
            c.QuickGps(target);
            Assert.True(g.W.HasWaypoint);
            Assert.Equal(33f, g.W.Waypoint.X);
            Assert.Equal(44f, g.W.Waypoint.Z);
        }

        // ---- wardrobe ------------------------------------------------------------------------------------------

        [Fact]
        public void OpenWardrobe_outside_does_nothing()
        {
            var g = new PlayGame();
            Make(g).OpenWardrobe();
            Assert.False(g.W.Menu.IsOpen);
        }

        [Fact]
        public void OpenWardrobe_inside_an_owned_building_lists_the_outfits()
        {
            var g = new PlayGame();
            InsideBuilding(g, 77, owned: true);
            Make(g).OpenWardrobe();
            Assert.Equal(MenuKind.Wardrobe, g.W.Menu.Kind);
            Assert.Equal((ushort)77, g.W.Menu.Building);
            Assert.Equal(Math.Min(GameMenuCommands.MaxOutfits, g.City.CitizenModelCount), g.W.Menu.Outfits.Count);
        }

        [Fact]
        public void OpenWardrobe_inside_a_building_that_is_not_owned_does_nothing()
        {
            var g = new PlayGame();
            InsideBuilding(g, 78, owned: false);
            Make(g).OpenWardrobe();
            Assert.False(g.W.Menu.IsOpen);
        }

        private static void InsideBuilding(PlayGame g, ushort id, bool owned)
        {
            g.W.Interior.Building = new GtaSkylines.Game.City.BuildingDescriptor { Id = id, Name = "Home" };
            g.W.Interior.Phase = InteriorPhase.Inside;
            if (owned) g.Profile.Properties.Add(id);
        }

        // ---- missions ------------------------------------------------------------------------------------------

        [Fact]
        public void RetryMission_and_AbortMission_are_no_ops_without_a_mission()
        {
            var g = new PlayGame();
            GameMenuCommands c = Make(g);
            c.RetryMission();
            c.AbortMission();
            Assert.Equal(RunnerState.Idle, g.Parts.Missions.State);
            Assert.Null(g.Parts.Missions.ActiveMission);
        }

        [Fact]
        public void AbortMission_ends_the_running_mission()
        {
            var g = new PlayGame();
            Assert.True(g.Parts.Missions.TryStartMission(g.W, g.Parts.Missions.Campaign.Missions[0].Id));
            Make(g).AbortMission();
            Assert.NotEqual(RunnerState.Mission, g.Parts.Missions.State);
        }

        [Fact]
        public void RetryMission_restarts_the_same_mission()
        {
            var g = new PlayGame();
            string id = g.Parts.Missions.Campaign.Missions[0].Id;
            Assert.True(g.Parts.Missions.TryStartMission(g.W, id));
            Make(g).RetryMission();
            Assert.Equal(RunnerState.Mission, g.Parts.Missions.State);
            Assert.Equal(id, g.Parts.Missions.ActiveMission.Id);
        }

        // ---- not running ---------------------------------------------------------------------------------------

        [Fact]
        public void Commands_do_nothing_while_the_session_is_not_running()
        {
            var g = new PlayGame(begin: false);
            Assert.False(g.Parts.Session.Running);
            GameMenuCommands c = Make(g);
            c.QuickGps(QuickGpsTarget.None);
            c.OpenWardrobe();
            c.RetryMission();
            c.AbortMission();
            c.ChooseMenuOption(0);
            c.PhoneAction(PhoneApp.Gps, 4);
            Assert.False(g.W.Menu.IsOpen);
        }
    }
}
