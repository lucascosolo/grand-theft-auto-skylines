using GtaSkylines.Game.Combat;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Missions;
using GtaSkylines.Game.Phone;
using PhoneRow = GtaSkylines.Mod.Hud.PhoneRow;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.World;
using GtaSkylines.Mod.Hud;
using GtaSkylines.Mod.Play;
using Xunit;

namespace GtaSkylines.Mod.Tests.Play
{
    public class ViewFillTests
    {
        [Theory]
        [InlineData(0f, "00:00")]
        [InlineData(9.5f, "09:30")]
        [InlineData(13.25f, "13:15")]
        [InlineData(23.99f, "23:59")]
        [InlineData(24f, "00:00")]
        [InlineData(25.5f, "01:30")]
        [InlineData(-1f, "23:00")]
        public void ClockText_formats_and_wraps_the_day(float hours, string expected)
        {
            Assert.Equal(expected, ViewFill.ClockText(hours, null));
        }

        [Fact]
        public void ClockText_returns_the_same_instance_when_nothing_changed()
        {
            string first = ViewFill.ClockText(9.5f, null);
            Assert.Same(first, ViewFill.ClockText(9.5f, first));
            Assert.Same(first, ViewFill.ClockText(9.508f, first)); // same minute
            Assert.NotSame(first, ViewFill.ClockText(9.6f, first));
        }

        [Fact]
        public void Phone_lists_jobs_and_gps_rows_and_the_clock()
        {
            var g = new PlayGame();
            var view = new PhoneView();
            Assert.True(ViewFill.Phone(view, g.Parts));
            Assert.Equal(SideJobTable.KindCount, view.Jobs.Count);
            Assert.Equal(SideJobTable.Name(SideJobKind.Taxi), view.Jobs[(int)SideJobKind.Taxi].Title);
            Assert.Equal(PhoneText.GpsRows.Length, view.Gps.Count);
            Assert.Equal(PhoneText.GpsRows[0], view.Gps[0].Title);
            Assert.Equal(PhoneText.RaceDetail, view.Jobs[(int)SideJobKind.StreetRace].Detail);
            Assert.Equal(5, view.Clock.Length);
            Assert.Equal(':', view.Clock[2]);
            Assert.Equal(1, view.Revision);
        }

        [Fact]
        public void Phone_with_no_change_returns_false_and_keeps_the_revision()
        {
            var g = new PlayGame();
            var view = new PhoneView();
            ViewFill.Phone(view, g.Parts);
            int rev = view.Revision;
            Assert.False(ViewFill.Phone(view, g.Parts));
            Assert.Equal(rev, view.Revision);
        }

        [Fact]
        public void Phone_messages_show_sender_text_and_unread_highlight()
        {
            var g = new PlayGame();
            var view = new PhoneView();
            ViewFill.Phone(view, g.Parts);
            g.Parts.Missions.Phone.ReceiveText(g.W, "Vince", "Call me");
            int rev = view.Revision;
            Assert.True(ViewFill.Phone(view, g.Parts));
            Assert.Equal(rev + 1, view.Revision);
            PhoneRow row = view.Messages[0];
            Assert.Equal("Vince", row.Title);
            Assert.Equal("Call me", row.Detail);
            Assert.True(row.Highlight);

            g.Parts.Missions.Phone.Messages[0].Read = true;
            Assert.True(ViewFill.Phone(view, g.Parts));
            Assert.False(view.Messages[0].Highlight);
        }

        [Fact]
        public void Phone_clock_change_alone_bumps_the_revision()
        {
            var g = new PlayGame();
            var view = new PhoneView();
            ViewFill.Phone(view, g.Parts);
            g.W.Clock.Hour = (g.W.Clock.Hour + 3f) % 24f;
            int rev = view.Revision;
            Assert.True(ViewFill.Phone(view, g.Parts));
            Assert.Equal(rev + 1, view.Revision);
        }

        [Fact]
        public void Phone_running_job_row_says_end_shift_and_is_highlighted()
        {
            var g = new PlayGame();
            Car taxi = g.SpawnCar(4, new Vec3(10, 0, 10));
            g.W.PutInCar(g.Player, taxi, 0);
            g.Tick();
            Assert.Equal(SideJobStartResult.Started, g.Parts.Missions.TryStartSideJob(g.W, SideJobKind.Taxi));
            var view = new PhoneView();
            ViewFill.Phone(view, g.Parts);
            PhoneRow row = view.Jobs[(int)SideJobKind.Taxi];
            Assert.Equal(PhoneText.EndShift, row.Title);
            Assert.True(row.Highlight);
            Assert.False(view.Jobs[(int)SideJobKind.Paramedic].Highlight);
        }

        [Fact]
        public void Wheel_has_one_slot_per_weapon_with_ownership_and_ammo()
        {
            var profile = new PlayerProfile();
            profile.GiveWeapon(WeaponId.Pistol);
            profile.SetAmmo(WeaponId.Pistol, 42);
            var view = new WeaponWheelView();
            ViewFill.Wheel(view, profile, (int)WeaponId.Pistol);
            Assert.Equal(WeaponInfo.Count, view.Slots.Count);
            Assert.Equal(WeaponTable.Get(WeaponId.Pistol).Name, view.Slots[(int)WeaponId.Pistol].Name);
            Assert.Equal("42", view.Slots[(int)WeaponId.Pistol].AmmoText);
            Assert.True(view.Slots[(int)WeaponId.Pistol].Owned);
            Assert.Equal("", view.Slots[(int)WeaponId.Bat].AmmoText);
            Assert.False(view.Slots[(int)WeaponId.Carbine].Owned);
            Assert.Equal((int)WeaponId.Pistol, view.Equipped);
        }

        [Fact]
        public void Wheel_rewrites_in_place_without_growing()
        {
            var profile = new PlayerProfile();
            var view = new WeaponWheelView();
            ViewFill.Wheel(view, profile, 0);
            ViewFill.Wheel(view, profile, 1);
            Assert.Equal(WeaponInfo.Count, view.Slots.Count);
            Assert.Equal(1, view.Equipped);
        }

        [Fact]
        public void Interaction_is_quiet_with_no_mission_and_outside()
        {
            var g = new PlayGame();
            var view = new InteractionView { InSafehouse = true, MissionActive = true, CanRetry = true };
            ViewFill.Interaction(view, g.Parts);
            Assert.False(view.InSafehouse);
            Assert.False(view.MissionActive);
            Assert.False(view.CanRetry);
        }

        [Fact]
        public void Interaction_reports_a_running_mission_as_active_and_retryable()
        {
            var g = new PlayGame();
            string id = g.Parts.Missions.Campaign.Missions[0].Id;
            Assert.True(g.Parts.Missions.TryStartMission(g.W, id));
            var view = new InteractionView();
            ViewFill.Interaction(view, g.Parts);
            Assert.True(view.MissionActive);
            Assert.True(view.CanRetry);
        }
    }
}
