using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Hud;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Session;
using GtaSkylines.Game.Tests.SessionTests;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.HudTests
{
    public class HudMessagesTests
    {
        [Fact]
        public void Help_HigherOrEqualPriorityReplaces_LowerDoesNot_ExpiresAfterItsSeconds()
        {
            var m = new HudMessages();
            m.ShowHelp("low", 1, 2f);
            m.ShowHelp("lower", 0, 2f);
            Assert.Equal("low", m.HelpText);
            m.ShowHelp("high", 5, 2f);
            Assert.Equal("high", m.HelpText);
            Assert.Equal(5, m.HelpPriority);
            m.ShowHelp("same", 5, 2f);
            Assert.Equal("same", m.HelpText);
            m.Advance(2.1f);
            Assert.Null(m.HelpText);
            Assert.Equal(0, m.HelpPriority);
            m.ShowHelp("late", 0, 2f);
            Assert.Equal("late", m.HelpText);
        }

        [Fact]
        public void Help_ZeroSecondsMeansThisStepOnly_AndClearHelpNeedsTheSameInstance()
        {
            var m = new HudMessages();
            m.ShowHelp("step", 1, 0f);
            Assert.Equal("step", m.HelpText);
            m.Advance(0.016f);
            Assert.Null(m.HelpText);
            string t = "keep";
            m.ShowHelp(t, 1, 5f);
            m.ClearHelp(new string('k', 1) + "eep");
            Assert.Equal("keep", m.HelpText);
            m.ClearHelp(t);
            Assert.Null(m.HelpText);
        }

        [Fact]
        public void Subtitles_QueueInOrder_DropOldestWaiting_NeverTheOneShowing()
        {
            var m = new HudMessages();
            for (int i = 0; i < HudMessages.SubtitleCapacity + 3; i++) m.QueueSubtitle("A", "line" + i, 1f);
            Subtitle s;
            Assert.True(m.TryGetSubtitle(out s));
            Assert.Equal("line0", s.Text);
            Assert.Equal(HudMessages.SubtitleCapacity - 1, m.QueuedSubtitles);
            m.Advance(1.05f);
            Assert.True(m.TryGetSubtitle(out s));
            Assert.Equal("line4", s.Text);
        }

        [Fact]
        public void Subtitles_EndWhenTheQueueRunsOut()
        {
            var m = new HudMessages();
            m.QueueSubtitle("Bob", "Hi", 1f);
            m.Advance(1.1f);
            Subtitle s;
            Assert.False(m.TryGetSubtitle(out s));
        }

        [Fact]
        public void Notifications_KeepTheNewestFour_NewestFirst_ExpireAtSixSeconds()
        {
            var m = new HudMessages();
            for (int i = 0; i < 6; i++) m.Notify("S", "n" + i, NotificationKind.Text);
            Assert.Equal(HudMessages.NotificationCapacity, m.NotificationCount);
            Assert.Equal("n5", m.GetNotification(0).Text);
            Assert.Equal("n2", m.GetNotification(3).Text);
            Assert.Throws<ArgumentOutOfRangeException>(() => m.GetNotification(4));
            m.Advance(3f);
            Sim.Near(3f, m.GetNotification(0).Age, 0.01f);
            m.Advance(3.1f);
            Assert.Equal(0, m.NotificationCount);
        }

        [Fact]
        public void BigScreen_ReplacesProgressesAndExpires()
        {
            var m = new HudMessages();
            m.ShowBigScreen(BigScreenKind.Wasted, null, 0, 4f);
            Assert.Equal(BigScreenKind.Wasted, m.BigScreen);
            m.Advance(2f);
            Sim.Near(0.5f, m.BigScreenProgress, 0.02f);
            m.ShowBigScreen(BigScreenKind.MissionPassed, null, 750, 3f);
            Assert.Equal(BigScreenKind.MissionPassed, m.BigScreen);
            Assert.Equal(750L, m.BigScreenReward);
            m.ShowBigScreen(BigScreenKind.MissionFailed, "Too slow", 0, 3f);
            Assert.Equal("Too slow", m.BigScreenText);
            m.Advance(3.1f);
            Assert.Equal(BigScreenKind.None, m.BigScreen);
        }

        [Fact]
        public void Clear_RemovesEverything()
        {
            var m = new HudMessages();
            m.ShowHelp("x", 1, 5f);
            m.Notify("a", "b", NotificationKind.System);
            m.QueueSubtitle("a", "b", 2f);
            m.ShowBigScreen(BigScreenKind.Busted, null, 0, 4f);
            m.Clear();
            Subtitle s;
            Assert.Null(m.HelpText);
            Assert.Equal(0, m.NotificationCount);
            Assert.False(m.TryGetSubtitle(out s));
            Assert.Equal(BigScreenKind.None, m.BigScreen);
        }
    }

    public class HudModelTests
    {
        private static Sim NewSim(Action<Fakes.FakeCity> city = null, Action<PlayerProfile> profile = null, Vec3? feet = null, float yaw = 0f)
        {
            return new Sim(city: city, profile: profile, feet: feet, yaw: yaw);
        }

        [Fact]
        public void FormatCash_GroupsThousands_AndSignsNegatives()
        {
            Assert.Equal("$0", HudModel.FormatCash(0));
            Assert.Equal("$999", HudModel.FormatCash(999));
            Assert.Equal("$1,234,567", HudModel.FormatCash(1234567));
            Assert.Equal("-$1,234", Sim.Ascii(HudModel.FormatCash(-1234)));
        }

        [Fact]
        public void Vitals_AreFractions()
        {
            Sim sim = NewSim();
            sim.Player.Health = 50f;
            sim.Player.Armour = 25f;
            sim.PS.Stamina = 40f;
            sim.W.Wanted.PlayerHeat = 60f;
            sim.Run(0.1, In.Idle);
            HudModel h = sim.S.Hud;
            Sim.Near(0.5f, h.Health, 1e-3f);
            Sim.Near(0.25f, h.Armour, 1e-3f);
            Sim.Near(0.4f, h.Stamina, 0.02f);
            Sim.Near(0.6f, h.Heat, 1e-3f);
            Assert.False(h.ShowBreath);
        }

        [Fact]
        public void Stars_AndFlashingFollowTheWantedBlackboard()
        {
            Sim sim = NewSim();
            sim.W.Wanted.Stars = 3;
            sim.W.Wanted.Evading = true;
            sim.Run(0.1, In.Idle);
            Assert.Equal(3, sim.S.Hud.Stars);
            Assert.True(sim.S.Hud.StarsFlashing);
            sim.W.Wanted.Evading = false;
            sim.Run(0.1, In.Idle);
            Assert.False(sim.S.Hud.StarsFlashing);
        }

        [Fact]
        public void Cash_ShowsText_Delta_AndTheDeltaExpires()
        {
            Sim sim = NewSim();
            sim.Run(0.1, In.Idle);
            Assert.Equal(PlayerProfile.StartCash, sim.S.Hud.Cash);
            Assert.Equal("$500", sim.S.Hud.CashText);
            string cached = sim.S.Hud.CashText;
            sim.Run(0.2, In.Idle);
            Assert.Same(cached, sim.S.Hud.CashText);
            sim.W.Wallet.Earn(200, "job");
            sim.Run(0.1, In.Idle);
            Assert.Equal(200L, sim.S.Hud.CashDelta);
            Assert.Equal("+$200", sim.S.Hud.CashDeltaText);
            Assert.Equal("$700", sim.S.Hud.CashText);
            sim.W.Wallet.TrySpend(50, "snack");
            sim.Run(0.1, In.Idle);
            Assert.Equal(150L, sim.S.Hud.CashDelta);
            sim.Run(3.5, In.Idle);
            Assert.True(sim.S.Hud.CashDeltaAge >= HudModel.CashDeltaSeconds);
            Assert.Equal(0L, sim.S.Hud.CashDelta);
        }

        [Fact]
        public void Weapon_NameAndAmmo()
        {
            Sim sim = NewSim(profile: p => { p.GiveWeapon(WeaponId.Pistol); p.SetAmmo(WeaponId.Pistol, 36); });
            sim.Player.CurrentWeapon = (int)WeaponId.Pistol;
            sim.Player.MagazineAmmo = 12;
            sim.Run(0.1, In.Idle);
            Assert.Equal("Pistol", sim.S.Hud.WeaponName);
            Assert.Equal(12, sim.S.Hud.AmmoInMagazine);
            Assert.Equal(36, sim.S.Hud.AmmoReserve);
            Assert.True(sim.S.Hud.ShowAmmo);
            sim.Player.CurrentWeapon = (int)WeaponId.Fists;
            sim.Run(0.1, In.Idle);
            Assert.Null(sim.S.Hud.WeaponName);
            Assert.False(sim.S.Hud.ShowAmmo);
        }

        [Fact]
        public void StreetAndDistrictBanner_ShowOnChange_ForFourSeconds()
        {
            Sim sim = NewSim(city: c =>
            {
                c.StreetFunc = p => p.X < 50f ? "Main Street" : "Elm Street";
                c.DistrictFunc = (x, z) => (byte)(x < 50f ? 0 : 3);
                c.Districts[3] = "Old Town";
            });
            sim.Run(0.2, In.Idle);
            Assert.Equal("Main Street", sim.S.Hud.StreetName);
            Assert.Equal("Testville", sim.S.Hud.DistrictName);
            Assert.True(sim.S.Hud.LocationBannerAge < 1f);
            sim.Run(4.5, In.Idle);
            Assert.True(sim.S.Hud.LocationBannerAge >= HudModel.LocationBannerSeconds);
            string same = sim.S.Hud.StreetName;
            sim.Run(0.2, In.Idle);
            Assert.Same(same, sim.S.Hud.StreetName);
            sim.Place(new Vec3(80f, 0f, 0f), 0f);
            sim.Run(0.2, In.Idle);
            Assert.Equal("Elm Street", sim.S.Hud.StreetName);
            Assert.Equal("Old Town", sim.S.Hud.DistrictName);
            Assert.True(sim.S.Hud.LocationBannerAge < 1f);
        }

        [Fact]
        public void HelpTokens_AreReplacedWithTheBoundKey()
        {
            Sim sim = NewSim();
            sim.W.Messages.ShowHelp("Press {Interact} or {Phone}", 3, 3f);
            sim.Run(0.1, In.Idle);
            Assert.Equal("Press ~E~ or ~Up~", sim.S.Hud.HelpText);
            sim.Run(3.2, In.Idle);
            Assert.Null(sim.S.Hud.HelpText);
        }

        [Fact]
        public void BigScreen_Wasted_ProgressesAndTheFadeIsCopied()
        {
            Sim sim = NewSim();
            sim.Player.Health = 0f;
            sim.Run(2.5, In.Idle);
            Assert.Equal(BigScreenKind.Wasted, sim.S.Hud.BigScreen);
            Assert.InRange(sim.S.Hud.BigScreenProgress, 0.3f, 0.8f);
            sim.W.FadeAlpha = 0.6f;
            sim.Tick(In.Idle);
            Sim.Near(sim.W.FadeAlpha, sim.S.Hud.FadeAlpha, 0.01f);
        }

        [Fact]
        public void MissionPassedAndFailed_PassThroughWithRewardAndReason()
        {
            Sim sim = NewSim();
            sim.W.Messages.ShowBigScreen(BigScreenKind.MissionPassed, null, 1500, 3f);
            sim.Tick(In.Idle);
            Assert.Equal(BigScreenKind.MissionPassed, sim.S.Hud.BigScreen);
            Assert.Equal(1500L, sim.S.Hud.BigScreenReward);
            sim.W.Messages.ShowBigScreen(BigScreenKind.MissionFailed, "Target escaped", 0, 3f);
            sim.Tick(In.Idle);
            Assert.Equal("Target escaped", sim.S.Hud.BigScreenText);
        }

        [Fact]
        public void Mission_ObjectiveSpansTimerStatusAndNotifications()
        {
            Sim sim = NewSim();
            sim.W.Mission.Active = true;
            sim.W.Mission.Objective = "Get to the docks";
            sim.W.Mission.ObjectiveSpans.Add(new TextSpan { Start = 11, Length = 5, Kind = HighlightKind.Place });
            sim.W.Mission.TimerSeconds = 125f;
            sim.W.Mission.StatusLine = "Position 2/4";
            for (int i = 0; i < 5; i++) sim.W.Messages.Notify("Ann", "msg" + i, NotificationKind.Text);
            sim.W.Messages.QueueSubtitle("Ann", "Hurry up", 3f);
            sim.Run(0.1, In.Idle);
            HudModel h = sim.S.Hud;
            Assert.Equal("Get to the docks", h.Objective);
            Assert.Single(h.ObjectiveSpans);
            Assert.Equal(11, h.ObjectiveSpans[0].Start);
            Assert.Equal("2:05", h.TimerText);
            Assert.Equal("Position 2/4", h.StatusLine);
            Assert.Equal(4, h.Notifications.Count);
            Assert.Equal("msg4", h.Notifications[0].Text);
            Assert.Equal("Ann", h.SubtitleSpeaker);
            Assert.Equal("Hurry up", h.SubtitleText);
            sim.W.Mission.Clear();
            sim.Run(0.1, In.Idle);
            Assert.Null(h.Objective);
            Assert.Null(h.TimerText);
        }

        [Fact]
        public void Blips_PlayerFirst_Waypoint_MissionBlipsFollowingActors_CopsOnlyWhileWanted()
        {
            Sim sim = NewSim(yaw: 45f);
            Ped cop = sim.W.SpawnPed(PedKind.Cop, 1, new Vec3(30f, 0f, 30f), 0f);
            Ped friend = sim.W.SpawnPed(PedKind.Civilian, 1, new Vec3(-20f, 0f, 10f), 0f);
            sim.S.SetWaypoint(new Vec3(100f, 0f, 100f));
            sim.W.Mission.Blips.Add(new Blip { Id = 1, Kind = BlipKind.Friendly, FollowTag = friend.Tag, Position = Vec3.Zero });
            sim.W.Mission.Blips.Add(new Blip { Id = 2, Kind = BlipKind.Shop, Position = new Vec3(5f, 0f, 5f), Flashing = true });
            sim.Run(0.1, In.Idle);
            List<HudBlip> b = sim.S.Hud.Blips;
            Assert.Equal(BlipKind.Player, b[0].Kind);
            Sim.Near(45f, b[0].YawDeg, 2f, "player arrow yaw");
            Assert.Contains(b, x => x.Kind == BlipKind.Waypoint && (x.Position - new Vec3(100f, 0f, 100f)).Length < 0.1f);
            Assert.Contains(b, x => x.Kind == BlipKind.Friendly && (x.Position - friend.Position).Length < 0.1f);
            Assert.Contains(b, x => x.Kind == BlipKind.Shop && x.Flashing);
            Assert.DoesNotContain(b, x => x.Kind == BlipKind.Cop);
            sim.W.Wanted.Stars = 2;
            sim.Run(0.1, In.Idle);
            Assert.Contains(sim.S.Hud.Blips, x => x.Kind == BlipKind.Cop && (x.Position - cop.Position).Length < 0.1f);
        }

        [Fact]
        public void SearchZone_ShowsWhileWantedWithARadius()
        {
            Sim sim = NewSim();
            sim.W.Wanted.Stars = 2;
            sim.W.Wanted.SearchCentre = new Vec3(30f, 0f, 30f);
            sim.W.Wanted.SearchRadius = 80f;
            sim.Run(0.1, In.Idle);
            Assert.True(sim.S.Hud.ShowSearchZone);
            Assert.Equal(80f, sim.S.Hud.SearchRadius);
            Sim.Near(new Vec3(30f, 0f, 30f), sim.S.Hud.SearchCentre, 1e-3f);
            sim.W.Wanted.Reset();
            sim.Run(0.1, In.Idle);
            Assert.False(sim.S.Hud.ShowSearchZone);
        }

        [Fact]
        public void Gps_FollowsTheRoadToTheWaypoint_MissionTargetOverridesIt()
        {
            Sim sim = NewSim(feet: new Vec3(0f, 0f, 10f));
            sim.S.SetWaypoint(new Vec3(0f, 0f, 300f));
            sim.Run(0.5, In.Idle);
            HudModel h = sim.S.Hud;
            Assert.InRange(h.GpsPoints.Count, 20, HudModel.MaxGpsPoints);
            Assert.True(Vec3.DistanceXZ(h.GpsPoints[0], sim.Player.Position) < 12f);
            Assert.True(Vec3.DistanceXZ(h.GpsPoints[h.GpsPoints.Count - 1], new Vec3(0f, 0f, 300f)) < 15f);
            Sim.Near(HudModel.GpsPointSpacing, Vec3.DistanceXZ(h.GpsPoints[1], h.GpsPoints[2]), 2f, "spacing");
            Assert.True(h.GpsRevision > 0);
            sim.W.Mission.HasGpsTarget = true;
            sim.W.Mission.GpsTarget = new Vec3(0f, 0f, 150f);
            sim.Run(0.5, In.Idle);
            Assert.True(Vec3.DistanceXZ(h.GpsPoints[h.GpsPoints.Count - 1], new Vec3(0f, 0f, 150f)) < 15f);
            sim.W.Mission.HasGpsTarget = false;
            sim.S.ClearWaypoint();
            sim.Run(0.5, In.Idle);
            Assert.Empty(h.GpsPoints);
        }

        [Fact]
        public void Crosshair_DotInFirstPersonOrAiming_NoneInACar_RedOnAHostileTarget()
        {
            var sim = new Sim(settings: s => s.FootView = 0);
            sim.Run(0.2, In.Idle);
            Assert.Equal(CrosshairKind.Dot, sim.S.Hud.Crosshair);
            Ped foe = sim.W.SpawnPed(PedKind.Civilian, 1, new Vec3(0f, 0f, 8f), 0f);
            foe.Flags |= PedFlags.Hostile;
            sim.PS.AimTargetTag = foe.Tag;
            sim.Run(0.2, In.Hold(GameButton.Aim));
            Assert.Equal(CrosshairKind.Hostile, sim.S.Hud.Crosshair);
            sim.PS.AimTargetTag = 0;
            Car car = sim.W.SpawnCar(0, new Vec3(30f, 0f, 30f), 0f, CarOwner.Player, 0);
            sim.W.PutInCar(sim.Player, car, 0);
            sim.Run(0.2, In.Idle);
            Assert.Equal(CrosshairKind.None, sim.S.Hud.Crosshair);
        }

        [Fact]
        public void PedCard_ShowsForACitizenWithin6mUnderTheCrosshair()
        {
            Sim sim = NewSim(city: c =>
            {
                c.Profiles[5000] = new CitizenProfile { Citizen = 5000, Name = "Ann Lee", Age = 34, HomeBuilding = 7, WorkBuilding = 8, Happiness = 66, Employed = true };
                c.Profiles[5001] = new CitizenProfile { Citizen = 5001, Name = "Old Joe", Age = 70, Happiness = 40 };
                c.AddBuilding(Sim.Building(7, ServiceKind.Residential, "", new Vec3(500f, 0f, 500f), 0f, "Elm Apts"));
                c.AddBuilding(Sim.Building(8, ServiceKind.Office, "", new Vec3(600f, 0f, 500f), 0f, "Acme Co"));
            }, yaw: 90f);
            sim.AddCitizen(50, 5000, new Vec3(4f, 0f, 0f));
            sim.Run(0.3, In.Idle);
            HudModel h = sim.S.Hud;
            Assert.True(h.HasPedCard);
            Assert.Equal("Ann Lee", h.PedCard.Name);
            Assert.Equal(34, h.PedCard.Age);
            Assert.Equal("Elm Apts", h.PedCard.HomeName);
            Assert.Equal("Acme Co", h.PedCard.WorkName);
            Assert.Equal(66, h.PedCard.Happiness);
            Assert.True(h.PedCard.Employed);
            Assert.False(h.PedCard.Retired);
            sim.Snap.Citizens.Clear();
            sim.AddCitizen(50, 5000, new Vec3(9f, 0f, 0f));
            sim.Run(0.3, In.Idle);
            Assert.False(sim.S.Hud.HasPedCard, "beyond 6 m");
            sim.Snap.Citizens.Clear();
            sim.AddCitizen(51, 5001, new Vec3(4f, 0f, 0f));
            sim.Run(0.3, In.Idle);
            Assert.True(sim.S.Hud.PedCard.Retired);
        }

        [Fact]
        public void MinimapRadius_IsMinAtRest_AndMapRotatesWithTheCamera()
        {
            Sim sim = NewSim(yaw: 70f);
            sim.Run(3.0, In.Idle);
            HudModel h = sim.S.Hud;
            Sim.Near(HudModel.MinimapRadiusMin, h.MinimapRadius, 6f);
            Sim.Near(70f, h.MapRotationDeg, 3f);
            Sim.Near(sim.Player.Position, h.PlayerPosition, 0.01f);
            Assert.Same(sim.W.Menu, h.Menu);
            Assert.False(h.InInterior);
        }

        [Fact]
        public void OnFoot_NoSpeedBanner_InACar_NameAndSpeedShow()
        {
            Sim sim = NewSim();
            Assert.False(sim.S.Hud.ShowSpeed);
            Assert.Equal(0, sim.S.Hud.SpeedKmh);
            Car car = sim.W.SpawnCar(0, new Vec3(0f, 0f, 20f), 0f, CarOwner.Player, 0);
            sim.W.PutInCar(sim.Player, car, 0);
            sim.Run(4.0, In.Forward);
            Assert.True(sim.S.Hud.ShowSpeed);
            Assert.Equal((int)Math.Round(car.Body.Speed * 3.6f), sim.S.Hud.SpeedKmh);
            Assert.True(sim.S.Hud.MinimapRadius > HudModel.MinimapRadiusMin + 1f, "minimap zooms out with speed");
        }

        [Fact]
        public void HudSystem_RejectsNullModel_AndRunsAtOrderHud()
        {
            Assert.Throws<ArgumentNullException>(() => new HudSystem(null));
            Assert.Equal(SystemOrder.Hud, new HudSystem(new HudModel()).Order);
        }
    }
}
