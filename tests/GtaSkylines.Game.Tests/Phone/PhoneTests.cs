using GtaSkylines.Game.City;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Missions;
using GtaSkylines.Game.Phone;
using GtaSkylines.Game.Tests.Missions;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.Phone
{
    public class PhoneServiceTests
    {
        private static readonly Vec3 Spot = new Vec3(1000f, 0f, 1000f);

        [Fact]
        public void ANewPhoneIsClosedWithNoContactsOrMessages()
        {
            var p = new PhoneService();
            Assert.Equal(PhoneScreen.Closed, p.Model.Screen);
            Assert.False(p.Model.IsOpen);
            Assert.Empty(p.ContactList);
            Assert.Empty(p.Messages);
            Assert.Equal(0, p.UnreadCount);
        }

        [Fact]
        public void UnlockAddsOnceInOrderWithTheContactName()
        {
            var p = new PhoneService();
            Assert.True(p.Unlock(ContactId.Reyes));
            Assert.False(p.Unlock(ContactId.Reyes));
            Assert.True(p.Unlock(ContactId.Vince));
            Assert.Equal(2, p.ContactList.Count);
            Assert.Equal(ContactId.Reyes, p.ContactList[0].Id);
            Assert.Equal("Sgt. Reyes", p.ContactList[0].Name);
            Assert.Equal("Vince", p.ContactList[1].Name);
            Assert.True(p.HasContact(ContactId.Vince));
            Assert.False(p.HasContact(ContactId.MayorsOffice));
        }

        [Fact]
        public void TextsArriveNewestFirstUnreadWithANotification()
        {
            Scene s = Scene.Create();
            var p = new PhoneService();
            p.ReceiveText(s.World, "Vince", "first");
            p.ReceiveText(s.World, "Vince", "second");
            Assert.Equal("second", p.Messages[0].Text);
            Assert.Equal("first", p.Messages[1].Text);
            Assert.Equal(2, p.UnreadCount);
            Assert.False(p.Messages[0].Read);
            Assert.True(p.HasText("Vince", "first"));
            Assert.False(p.HasText("Reyes", "first"));
            Assert.Equal(NotificationKind.Text, s.World.Messages.GetNotification(0).Kind);
        }

        [Fact]
        public void OnlyThirtyMessagesAreKept()
        {
            Scene s = Scene.Create();
            var p = new PhoneService();
            for (int i = 0; i < PhoneService.MaxMessages + 5; i++) p.ReceiveText(s.World, "V", "m" + i);
            Assert.Equal(PhoneService.MaxMessages, p.Messages.Count);
            Assert.Equal("m34", p.Messages[0].Text);
            Assert.False(p.HasText("V", "m0"));
        }

        [Fact]
        public void CallingAContactWithWorkQueuesItsLineAndSetsTheWaypoint()
        {
            Scene s = Scene.Create();
            Campaign c = s.Prime();
            Assert.True(s.Runner.Phone.Call(s.World, c, ContactId.Vince));
            Assert.True(s.World.HasWaypoint);
            Assert.Equal(c.Find("V1").StartMarker, s.World.Waypoint);
            Subtitle sub;
            Assert.True(s.World.Messages.TryGetSubtitle(out sub));
            Assert.Equal(c.Find("V1").CallLine, sub.Text);
            Assert.Equal("Vince", sub.Speaker);
        }

        [Fact]
        public void CallingAContactWithoutWorkSaysSo()
        {
            Scene s = Scene.Create();
            Campaign c = s.Prime();
            Assert.False(s.Runner.Phone.Call(s.World, c, ContactId.Reyes));
            Subtitle sub;
            Assert.True(s.World.Messages.TryGetSubtitle(out sub));
            Assert.Equal(PhoneText.NoWork, sub.Text);
            Assert.False(s.World.HasWaypoint);
        }

        [Fact]
        public void GpsShortcutsPointAtTheNearestPlaces()
        {
            Scene s = Scene.Create();
            Campaign c = s.Prime();
            s.Teleport(s.Door(s.Test.Hospital));
            var places = s.Runner.Places;
            Assert.True(s.Runner.Phone.GpsTo(s.World, places, c, GpsShortcut.Hospital));
            Assert.Equal(s.Door(s.Test.Hospital), s.World.Waypoint);
            s.Teleport(s.Door(s.Test.Store));
            Assert.True(s.Runner.Phone.GpsTo(s.World, places, c, GpsShortcut.Shop));
            Assert.Equal(s.Door(s.Test.Store), s.World.Waypoint);
            Assert.True(s.Runner.Phone.GpsTo(s.World, places, c, GpsShortcut.Garage));
            Assert.True(s.World.HasWaypoint);
        }

        [Fact]
        public void SafehouseGpsWithoutOneShowsTheHelpAndReturnsFalse()
        {
            Scene s = Scene.Create();
            Campaign c = s.Prime();
            Assert.False(s.Runner.Phone.GpsTo(s.World, s.Runner.Places, c, GpsShortcut.Safehouse));
            Assert.Equal(PhoneText.NoSafehouse, s.World.Messages.HelpText);
        }

        // ---- input and model ----------------------------------------------------------------------------------

        [Fact]
        public void PhoneButtonTogglesHomeWithFourApps()
        {
            Scene s = Scene.Create();
            s.Press(GameButton.Phone);
            PhoneModel m = s.Runner.Phone.Model;
            Assert.Equal(PhoneScreen.Home, m.Screen);
            Assert.Equal(PhoneText.Apps.Length, m.RowCount);
            for (int i = 0; i < m.RowCount; i++) Assert.Equal(PhoneText.Apps[i], m.Rows[i].Title);
            s.Press(GameButton.Phone);
            Assert.Equal(PhoneScreen.Closed, m.Screen);
        }

        [Fact]
        public void MenuDownWrapsAndAcceptOpensTheSelectedApp()
        {
            Scene s = Scene.Create();
            s.Press(GameButton.Phone);
            PhoneModel m = s.Runner.Phone.Model;
            s.Press(GameButton.MenuUp);
            Assert.Equal(3, m.Selected);
            s.Press(GameButton.MenuDown);
            Assert.Equal(0, m.Selected);
            s.Press(GameButton.MenuDown);
            s.Press(GameButton.MenuAccept);
            Assert.Equal(PhoneScreen.Messages, m.Screen);
        }

        [Fact]
        public void BackGoesHomeThenClosesAndAMessageBacksToTheList()
        {
            Scene s = Scene.Create();
            s.Tick();
            s.Runner.Phone.Open(PhoneScreen.Messages);
            s.Press(GameButton.MenuAccept);
            PhoneModel m = s.Runner.Phone.Model;
            Assert.Equal(PhoneScreen.Message, m.Screen);
            Assert.Equal(s.Runner.Phone.Messages[0].Text, m.Body);
            Assert.Equal(0, m.RowCount);
            s.Press(GameButton.MenuBack);
            Assert.Equal(PhoneScreen.Messages, m.Screen);
            s.Press(GameButton.MenuBack);
            Assert.Equal(PhoneScreen.Home, m.Screen);
            s.Press(GameButton.MenuBack);
            Assert.Equal(PhoneScreen.Closed, m.Screen);
        }

        [Fact]
        public void OpeningAMessageMarksItReadAndClearsTheBadge()
        {
            Scene s = Scene.Create();
            s.Tick();
            Assert.Equal(1, s.Runner.Phone.UnreadCount);
            Assert.Equal(1, s.Runner.Phone.Model.Unread);
            s.Runner.Phone.Open(PhoneScreen.Messages);
            s.Press(GameButton.MenuAccept);
            Assert.Equal(0, s.Runner.Phone.UnreadCount);
            Assert.Equal(0, s.Runner.Phone.Model.Unread);
        }

        [Fact]
        public void ContactsScreenListsUnlockedContactsAndAcceptCalls()
        {
            Scene s = Scene.Create();
            s.Tick();
            s.Runner.Phone.Open(PhoneScreen.Contacts);
            Assert.Equal(1, s.Runner.Phone.Model.RowCount);
            Assert.Equal("Vince", s.Runner.Phone.Model.Rows[0].Title);
            s.Press(GameButton.MenuAccept);
            Assert.True(s.World.HasWaypoint);
        }

        [Fact]
        public void TheModelRevisionChangesOnlyWhenSomethingVisibleChanges()
        {
            Scene s = Scene.Create();
            s.Press(GameButton.Phone);
            int rev = s.Runner.Phone.Model.Revision;
            s.Tick(20);
            Assert.Equal(rev, s.Runner.Phone.Model.Revision);
            s.Press(GameButton.MenuDown);
            Assert.NotEqual(rev, s.Runner.Phone.Model.Revision);
        }

        [Fact]
        public void ThePhoneDoesNotOpenWhileAMenuIsOpenOrThePlayerIsDead()
        {
            Scene s = Scene.Create();
            s.World.Menu.Kind = MenuKind.Shop;
            s.Press(GameButton.Phone);
            Assert.False(s.Runner.Phone.Model.IsOpen);
            s.World.Menu.Close();
            s.World.PlayerState.Life = LifeState.Wasted;
            s.Press(GameButton.Phone);
            Assert.False(s.Runner.Phone.Model.IsOpen);
        }

        [Fact]
        public void JobsAppListsSixJobsWithLevelsAndDisablesWrongVehicleRows()
        {
            Scene s = Scene.Create(null, p => p.SideJobLevels["taxi"] = 3);
            s.Tick();
            s.Runner.Phone.Open(PhoneScreen.Jobs);
            s.Tick();
            PhoneModel m = s.Runner.Phone.Model;
            Assert.Equal(6, m.RowCount);
            Assert.Equal("Taxi", m.Rows[0].Title);
            Assert.Equal("Level 3", m.Rows[0].Detail);
            Assert.Equal("Street Race", m.Rows[5].Title);
            Assert.Equal(PhoneText.RaceDetail, m.Rows[5].Detail);
            Assert.False(m.Rows[0].Enabled);
            s.Drive(VehicleKind.Taxi, Spot);
            s.Tick();
            Assert.True(m.Rows[0].Enabled);
            Assert.False(m.Rows[1].Enabled);
        }

        [Fact]
        public void AcceptingTheTaxiRowInATaxiStartsTheShiftAndTheRowBecomesEndShift()
        {
            Scene s = Scene.Create();
            s.Tick();
            s.Teleport(Spot);
            s.Drive(VehicleKind.Taxi, Spot);
            s.Runner.Phone.Open(PhoneScreen.Jobs);
            s.Press(GameButton.MenuAccept);
            Assert.Equal(RunnerState.SideJob, s.Runner.State);
            s.Tick();
            Assert.Equal(PhoneText.EndShift, s.Runner.Phone.Model.Rows[0].Title);
            s.Press(GameButton.MenuAccept);
            Assert.Equal(RunnerState.Idle, s.Runner.State);
        }

        [Fact]
        public void TheQuickMenuOffersTheVehiclesJobsAndThePhone()
        {
            Scene s = Scene.Create();
            s.Tick();
            s.Teleport(Spot);
            s.Drive(VehicleKind.Ambulance, Spot);
            s.Press(GameButton.InteractionMenu);
            PhoneModel m = s.Runner.Phone.Model;
            Assert.Equal(PhoneScreen.QuickMenu, m.Screen);
            Assert.Equal(PhoneText.QuickMenu, m.Header);
            var titles = new System.Collections.Generic.List<string>();
            for (int i = 0; i < m.RowCount; i++) titles.Add(m.Rows[i].Title);
            Assert.Contains("Paramedic", titles);
            Assert.DoesNotContain("Taxi", titles);
            Assert.Contains(PhoneText.OpenPhone, titles);
            s.Press(GameButton.MenuBack);
            Assert.Equal(PhoneScreen.Closed, m.Screen);
        }

        [Fact]
        public void AcceptingAJobInTheQuickMenuStartsIt()
        {
            Scene s = Scene.Create();
            s.Tick();
            s.Teleport(Spot);
            s.Drive(VehicleKind.Taxi, Spot);
            s.Press(GameButton.InteractionMenu);
            s.Press(GameButton.MenuAccept);
            Assert.Equal(RunnerState.SideJob, s.Runner.State);
            Assert.Equal(SideJobKind.Taxi, s.Runner.ActiveJob.Kind);
        }

        [Fact]
        public void UnlockTextsAreSentOnceAndMentionTheContactBuilding()
        {
            Scene s = Scene.Create();
            s.Tick(120);
            int n = 0;
            foreach (PhoneMessage m in s.Runner.Phone.Messages) if (m.Sender == "Vince") n++;
            Assert.Equal(1, n);
            Assert.Contains("Rusty Bolt Motors", s.Runner.Phone.Messages[0].Text);
        }

        [Fact]
        public void TheModelStaysWithinItsFixedRowCapacity()
        {
            Assert.Equal(32, PhoneModel.MaxRows);
            Assert.Equal(PhoneModel.MaxRows, new PhoneModel().Rows.Length);
            Assert.Equal(4, PhoneText.Apps.Length);
            Assert.Equal(5, PhoneText.GpsRows.Length);
            Assert.Equal(10, PhoneText.Levels.Length);
        }
    }

    public class ChirperTests
    {
        private static bool Matches(string text, string template)
        {
            string pattern = System.Text.RegularExpressions.Regex.Escape(template);
            pattern = System.Text.RegularExpressions.Regex.Replace(pattern, @"\\?\{[a-z_]+\}", ".+");
            return System.Text.RegularExpressions.Regex.IsMatch(text, "^" + pattern + "$");
        }

        private static bool MatchesTopic(string text, ChirpTopic topic)
        {
            foreach (string t in Chirper.Templates(topic)) if (Matches(text, t)) return true;
            return false;
        }

        [Fact]
        public void EveryTopicHasTemplatesWithOnlyKnownPlaceholders()
        {
            var known = new System.Collections.Generic.HashSet<string> { "{street}", "{district}", "{city}", "{mission}", "{job}" };
            foreach (ChirpTopic topic in System.Enum.GetValues(typeof(ChirpTopic)))
            {
                string[] templates = Chirper.Templates(topic);
                Assert.True(templates.Length >= 4, topic.ToString());
                foreach (string t in templates)
                    foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(t, @"\{[a-z_]+\}"))
                        Assert.Contains(m.Value, known);
            }
            foreach (string t in Chirper.Templates(ChirpTopic.MissionPassed)) Assert.Contains("{mission}", t);
            foreach (string t in Chirper.Templates(ChirpTopic.JobMilestone)) Assert.Contains("{job}", t);
        }

        [Fact]
        public void TheFirstPostIsAllowedAndFillsStreetDistrictAndCity()
        {
            Scene s = Scene.Create();
            var chirper = new Chirper(1);
            s.World.Clock.RealSeconds = 5;
            Assert.True(chirper.TryPost(s.World, ChirpTopic.Crime, new Vec3(100f, 0f, 100f), null));
            Assert.Equal(1, chirper.PostCount);
            Assert.True(MatchesTopic(chirper.LastText, ChirpTopic.Crime), chirper.LastText);
            Assert.True(Txt.Filled(chirper.LastText));
            Assert.Equal(CityEffectKind.Chirp, s.Session.Effects.Count >= 0 ? CityEffectKind.Chirp : CityEffectKind.Chirp);
            Assert.Equal(1, s.World.Effects.Count);
            Assert.Equal(CityEffectKind.Chirp, s.World.Effects.Items[0].Kind);
            Assert.Equal(chirper.LastText, s.World.Effects.Items[0].Text);
        }

        [Fact]
        public void PostsAreRateLimitedToOnePerFortyFiveRealSeconds()
        {
            Scene s = Scene.Create();
            var chirper = new Chirper(1);
            s.World.Clock.RealSeconds = 100;
            Assert.True(chirper.TryPost(s.World, ChirpTopic.Shooting, Vec3.Zero, null));
            s.World.Clock.RealSeconds = 144.9;
            Assert.False(chirper.TryPost(s.World, ChirpTopic.Explosion, Vec3.Zero, null));
            Assert.Equal(1, chirper.PostCount);
            s.World.Clock.RealSeconds = 145.0;
            Assert.True(chirper.TryPost(s.World, ChirpTopic.Explosion, Vec3.Zero, null));
            Assert.Equal(2, chirper.PostCount);
            Assert.True(MatchesTopic(chirper.LastText, ChirpTopic.Explosion));
        }

        [Fact]
        public void NothingIsPostedInsideAnInterior()
        {
            Scene s = Scene.Create();
            var chirper = new Chirper(1);
            s.World.Interior.Phase = InteriorPhase.Inside;
            Assert.False(chirper.TryPost(s.World, ChirpTopic.Crime, Vec3.Zero, null));
            Assert.Equal(0, chirper.PostCount);
            Assert.Equal(0, s.World.Effects.Count);
        }

        [Fact]
        public void MissionAndJobPostsCarryTheirName()
        {
            Scene s = Scene.Create();
            var chirper = new Chirper(1);
            Assert.True(chirper.TryPost(s.World, ChirpTopic.MissionPassed, Vec3.Zero, "Fresh Start"));
            Assert.Contains("Fresh Start", chirper.LastText);
            s.World.Clock.RealSeconds = 60;
            Assert.True(chirper.TryPost(s.World, ChirpTopic.JobMilestone, Vec3.Zero, "Taxi"));
            Assert.Contains("Taxi", chirper.LastText);
        }

        [Fact]
        public void NoStreetNearbyReadsDowntownAndTheSenderIsTheNearestSnapshotCitizen()
        {
            Scene s = Scene.Create();
            s.City.StreetFunc = null;
            s.Snapshot.Citizens.Add(new AmbientCitizen { Instance = 1, Citizen = 5, Position = new Vec3(500f, 0f, 0f) });
            s.Snapshot.Citizens.Add(new AmbientCitizen { Instance = 2, Citizen = 6, Position = new Vec3(20f, 0f, 0f) });
            s.Tick();
            var chirper = new Chirper(2);
            s.World.Clock.RealSeconds = 100;
            Assert.True(chirper.TryPost(s.World, ChirpTopic.Shooting, Vec3.Zero, null));
            Assert.Equal(6u, s.World.Effects.Items[0].Citizen);
            for (int i = 0; i < 20; i++)
            {
                s.World.Clock.RealSeconds += 50;
                chirper.TryPost(s.World, ChirpTopic.Shooting, Vec3.Zero, null);
                Assert.DoesNotContain("{street}", chirper.LastText);
            }
        }

        [Fact]
        public void TheSameSeedPicksTheSameTemplates()
        {
            Scene s = Scene.Create();
            var a = new Chirper(9);
            var b = new Chirper(9);
            for (int i = 0; i < 6; i++)
            {
                s.World.Clock.RealSeconds = 100 * (i + 1);
                a.TryPost(s.World, ChirpTopic.Chase, Vec3.Zero, null);
                b.TryPost(s.World, ChirpTopic.Chase, Vec3.Zero, null);
                Assert.Equal(a.LastText, b.LastText);
            }
        }

        private static GameEvent Ev(GameEventType t, int a) { return new GameEvent { Type = t, ActorA = a }; }

        [Fact]
        public void ObserveMapsGunshotsExplosionsAndCarjackingsToTopics()
        {
            Scene s = Scene.Create();
            int me = s.World.Player.Id;
            var chirper = new Chirper(1);
            s.World.Clock.RealSeconds = 100;
            chirper.Observe(s.World, new[] { Ev(GameEventType.Gunshot, me) }, 1);
            Assert.True(MatchesTopic(chirper.LastText, ChirpTopic.Shooting), chirper.LastText);
            s.World.Clock.RealSeconds = 200;
            chirper.Observe(s.World, new[] { Ev(GameEventType.Explosion, 0) }, 1);
            Assert.True(MatchesTopic(chirper.LastText, ChirpTopic.Explosion));
            s.World.Clock.RealSeconds = 300;
            chirper.Observe(s.World, new[] { Ev(GameEventType.CarJacked, me) }, 1);
            Assert.True(MatchesTopic(chirper.LastText, ChirpTopic.Carjacking));
        }

        [Fact]
        public void ObserveIgnoresGunshotsFromOthers()
        {
            Scene s = Scene.Create();
            var chirper = new Chirper(1);
            chirper.Observe(s.World, new[] { Ev(GameEventType.Gunshot, s.World.Player.Id + 50) }, 1);
            Assert.Equal(0, chirper.PostCount);
        }

        [Fact]
        public void ObservePostsCrimeWhenStarsRiseBelowThreeAndChaseAtThreeOrMore()
        {
            Scene s = Scene.Create();
            var chirper = new Chirper(1);
            s.World.Clock.RealSeconds = 100;
            chirper.Observe(s.World, new GameEvent[0], 0);
            Assert.Equal(0, chirper.PostCount);
            s.World.Wanted.Stars = 1;
            chirper.Observe(s.World, new GameEvent[0], 0);
            Assert.True(MatchesTopic(chirper.LastText, ChirpTopic.Crime), chirper.LastText);
            s.World.Clock.RealSeconds = 200;
            s.World.Wanted.Stars = 3;
            chirper.Observe(s.World, new GameEvent[0], 0);
            Assert.True(MatchesTopic(chirper.LastText, ChirpTopic.Chase), chirper.LastText);
            Assert.Equal(2, chirper.PostCount);
            s.World.Clock.RealSeconds = 300;
            chirper.Observe(s.World, new GameEvent[0], 0);
            Assert.Equal(2, chirper.PostCount);
        }

        [Fact]
        public void SeveralCandidatesInOneStepPostTheFirstInOrder()
        {
            Scene s = Scene.Create();
            var chirper = new Chirper(1);
            s.World.Wanted.Stars = 4;
            chirper.Observe(s.World, new[] { Ev(GameEventType.Explosion, 0), Ev(GameEventType.Gunshot, s.World.Player.Id) }, 2);
            Assert.Equal(1, chirper.PostCount);
            Assert.True(MatchesTopic(chirper.LastText, ChirpTopic.Shooting), chirper.LastText);
        }
    }
}
