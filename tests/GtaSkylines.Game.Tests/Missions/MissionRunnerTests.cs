using GtaSkylines.Game.City;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Missions;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.Missions
{
    public class MissionRunnerTests
    {
        private static MissionDefinition Quick(Scene s, string id = "V1", float seconds = 0.5f)
        {
            return s.Replace(id, new WaitStep(seconds));
        }

        [Fact]
        public void ConstructorInstallsTheDialogueHookAndRejectsNull()
        {
            Scene s = Scene.Create();
            Assert.Same(s.Runner.Dialogue, s.World.Hooks.Dialogue);
            Assert.Throws<System.ArgumentNullException>(() => new MissionRunner(null, 1));
            Assert.Equal(SystemOrder.Missions, s.Runner.Order);
        }

        [Fact]
        public void FirstStepGeneratesTheCampaignUnlocksVinceAndTextsTheFirstMission()
        {
            Scene s = Scene.Create();
            Assert.Null(s.Runner.Campaign);
            s.Tick();
            Assert.NotNull(s.Runner.Campaign);
            Assert.Equal(9, s.Runner.Campaign.Missions.Count);
            Assert.True(s.Runner.Phone.HasContact(ContactId.Vince));
            Assert.False(s.Runner.Phone.HasContact(ContactId.Reyes));
            Assert.True(s.Runner.Phone.Messages.Count == 1);
            Assert.Equal("Vince", s.Runner.Phone.Messages[0].Sender);
            Assert.Contains("Rusty Bolt Motors", s.Runner.Phone.Messages[0].Text);
            s.Tick(30);
            Assert.True(s.Runner.Phone.Messages.Count == 1);
        }

        [Fact]
        public void OnlyAvailableMissionsGetMarkersAsBlipsAndYellowCylinders()
        {
            Scene s = Scene.Create();
            s.Tick();
            Assert.Equal(RunnerState.Idle, s.Runner.State);
            Blip start = s.World.Mission.Blips.Find(b => b.Kind == BlipKind.MissionStart);
            Assert.InRange(start.Id, 1, 99);
            Assert.Single(s.World.Mission.Markers.FindAll(m => m.Id >= 1 && m.Id <= 99));
            WorldMarker marker = s.World.Mission.Markers.Find(m => m.Id >= 1 && m.Id <= 99);
            Assert.Equal(MissionRunner.MarkerRadius, marker.Radius);
            Assert.Equal(MissionRunner.MarkerColour, marker.ColourRgb);
            Assert.Equal(s.Runner.Campaign.Find("V1").StartMarker, marker.Position);
        }

        [Fact]
        public void WalkingIntoAMarkerStartsTheMission()
        {
            Scene s = Scene.Create();
            s.Tick();
            s.Teleport(s.Runner.Campaign.Find("V1").StartMarker);
            s.Tick(2);
            Assert.Equal(RunnerState.Mission, s.Runner.State);
            Assert.Equal("V1", s.Runner.ActiveMission.Id);
            Assert.True(s.World.Mission.Active);
            Assert.Equal("Fresh Start", s.World.Mission.Title);
            Assert.True(s.World.Messages.QueuedSubtitles > 0);
            Assert.DoesNotContain(s.World.Mission.Blips, b => b.Kind == BlipKind.MissionStart);
        }

        [Fact]
        public void ASlowCarInAMarkerStartsTheMission()
        {
            Scene s = Scene.Create();
            s.Tick();
            Vec3 marker = s.Runner.Campaign.Find("V1").StartMarker;
            s.Drive(VehicleKind.Car, marker);
            s.Tick(2);
            Assert.Equal(RunnerState.Mission, s.Runner.State);
        }

        [Fact]
        public void AMissionThatIsNotAvailableDoesNotStartFromItsMarkerPosition()
        {
            Scene s = Scene.Create();
            s.Tick();
            s.Teleport(s.Runner.Campaign.Find("R1").StartMarker);
            s.Tick(3);
            Assert.Equal(RunnerState.Idle, s.Runner.State);
        }

        [Fact]
        public void OnlyOneMissionRunsAtATime()
        {
            Scene s = Scene.Create(null, p => { p.CompleteMission("V1"); });
            s.Replace("V2", new WaitStep(100f));
            Assert.True(s.Runner.TryStartMission(s.World, "V2"));
            Assert.False(s.Runner.TryStartMission(s.World, "V2"));
            Assert.False(s.Runner.TryStartMission(s.World, "V3"));
            Assert.Equal("V2", s.Runner.ActiveMission.Id);
            Assert.DoesNotContain(s.World.Mission.Blips, b => b.Kind == BlipKind.MissionStart);
        }

        [Fact]
        public void UnknownUnavailableAndUngeneratedMissionsDoNotStart()
        {
            Scene s = Scene.Create();
            Assert.False(s.Runner.TryStartMission(s.World, "V1"));
            s.Tick();
            Assert.False(s.Runner.TryStartMission(s.World, "nope"));
            Assert.False(s.Runner.TryStartMission(s.World, "C3"));
            Assert.Equal(RunnerState.Idle, s.Runner.State);
        }

        [Fact]
        public void PassingAddsTheRewardOnceCompletesTheMissionAndShowsThePassedScreen()
        {
            Scene s = Scene.Create();
            MissionDefinition def = Quick(s);
            long cash = s.Profile.Cash;
            Assert.True(s.Runner.TryStartMission(s.World, "V1"));
            s.Seconds(2f);
            Assert.Equal(cash + 250, s.Profile.Cash);
            Assert.True(s.Profile.IsMissionComplete("V1"));
            Assert.Equal(RunnerState.Idle, s.Runner.State);
            Assert.False(s.World.Mission.Active);
            Assert.Equal(1, s.Profile.GetStat(GtaSkylines.Game.Profile.StatNames.JobsDone));
            Assert.Equal(BigScreenKind.MissionPassed, s.World.Messages.BigScreen);
            Assert.Equal(250, s.World.Messages.BigScreenReward);
            s.Seconds(10f);
            Assert.Equal(cash + 250, s.Profile.Cash);
            Assert.Equal("Mission: Fresh Start", def.Reward.Reason);
        }

        [Fact]
        public void PassingSendsTheNextUnlockTextAndPostsToChirper()
        {
            Scene s = Scene.Create();
            Quick(s);
            s.Runner.TryStartMission(s.World, "V1");
            s.Seconds(2f);
            Assert.Contains(s.Runner.Phone.Messages, m => m.Text == s.Runner.Campaign.Find("V2").UnlockText && m.Sender == "Vince");
            Assert.Equal(1, s.Runner.Chirper.PostCount);
        }

        [Fact]
        public void PassingAMissionOfANewContactUnlocksThatContact()
        {
            Scene s = Scene.Create(null, p => { p.CompleteMission("V1"); });
            Quick(s, "V2");
            s.Runner.TryStartMission(s.World, "V2");
            s.Seconds(2f);
            Assert.True(s.Runner.Phone.HasContact(ContactId.Reyes));
            Assert.Contains(s.Runner.Phone.Messages, m => m.Text == s.Runner.Campaign.Find("R1").UnlockText);
        }

        [Fact]
        public void BeingWastedFailsTheMissionWithoutRewardAndWithoutAFailedScreen()
        {
            Scene s = Scene.Create();
            Quick(s, "V1", 100f);
            long cash = s.Profile.Cash;
            s.Runner.TryStartMission(s.World, "V1");
            s.World.Emit(GameEventType.PlayerDied, s.World.Player.Position, s.World.Player.Id, 0, 0f, null);
            s.Tick(2);
            Assert.Equal(FailReasons.Wasted, s.Runner.LastFailReason);
            Assert.Equal(RunnerState.Idle, s.Runner.State);
            Assert.False(s.Profile.IsMissionComplete("V1"));
            Assert.NotEqual(BigScreenKind.MissionFailed, s.World.Messages.BigScreen);
            Assert.False(s.World.Mission.Active);
        }

        [Fact]
        public void BeingBustedFailsTheMissionWithTheBustedReason()
        {
            Scene s = Scene.Create();
            Quick(s, "V1", 100f);
            s.Runner.TryStartMission(s.World, "V1");
            s.World.Emit(GameEventType.PlayerArrested, s.World.Player.Position, s.World.Player.Id, 0, 0f, null);
            s.Tick(2);
            Assert.Equal(FailReasons.Busted, s.Runner.LastFailReason);
            Assert.NotEqual(BigScreenKind.MissionFailed, s.World.Messages.BigScreen);
        }

        [Fact]
        public void ATimedOutStepFailsTheMissionAndShowsTheReason()
        {
            Scene s = Scene.Create();
            s.Replace("V1", new TimedStep(new WaitStep(100f), 1f));
            long cash = s.Profile.Cash;
            s.Runner.TryStartMission(s.World, "V1");
            s.Seconds(2f);
            Assert.Equal(FailReasons.TimeOut, s.Runner.LastFailReason);
            Assert.Equal(BigScreenKind.MissionFailed, s.World.Messages.BigScreen);
            Assert.Equal(FailReasons.TimeOut, s.World.Messages.BigScreenText);
            Assert.Equal(cash, s.Profile.Cash);
            Assert.False(s.Profile.IsMissionComplete("V1"));
        }

        [Fact]
        public void LosingTheMissionVehicleFailsWithVehicleDestroyed()
        {
            Scene s = Scene.Create();
            MissionDefinition def = s.Replace("V1", new WaitStep(100f));
            def.Fail = MissionFail.VehicleDestroyed;
            def.VehicleSlot = 0;
            def.Actors = new[] { new MissionActorSpec { Kind = ActorKind.Car, VehicleKind = VehicleKind.Car, Position = new Vec3(150f, 0f, 100f) } };
            s.Runner.TryStartMission(s.World, "V1");
            s.Tick(2);
            Car car = s.World.Cars[0];
            Assert.Equal(CarOwner.Mission, car.Owner);
            s.World.RemoveCar(car);
            s.Tick(2);
            Assert.Equal(FailReasons.VehicleDestroyed, s.Runner.LastFailReason);
        }

        [Fact]
        public void TheTargetGettingFarAwayFailsWithTargetEscaped()
        {
            Scene s = Scene.Create();
            MissionDefinition def = s.Replace("V1", new WaitStep(100f));
            def.Fail = MissionFail.TargetEscaped;
            def.TargetSlot = 0;
            def.Actors = new[] { new MissionActorSpec { Kind = ActorKind.Ped, Position = new Vec3(150f, 0f, 100f) } };
            s.Runner.TryStartMission(s.World, "V1");
            s.Tick(2);
            s.Teleport(new Vec3(700f, 0f, 100f));
            s.Tick(2);
            Assert.Equal(FailReasons.TargetEscaped, s.Runner.LastFailReason);
        }

        [Fact]
        public void ADeadPassengerFailsWithPassengerKilled()
        {
            Scene s = Scene.Create();
            MissionDefinition def = s.Replace("V1", new WaitStep(100f));
            def.Fail = MissionFail.PassengerKilled;
            def.PassengerSlot = 0;
            def.Actors = new[] { new MissionActorSpec { Kind = ActorKind.Ped, Position = new Vec3(150f, 0f, 100f) } };
            s.Runner.TryStartMission(s.World, "V1");
            s.Tick(2);
            s.World.Peds[s.World.Peds.Count - 1].Flags |= PedFlags.Dead;
            s.Tick(2);
            Assert.Equal(FailReasons.PassengerKilled, s.Runner.LastFailReason);
        }

        [Fact]
        public void MissionActorsAreSpawnedAtTheirStepAndRemovedWhenTheMissionEnds()
        {
            Scene s = Scene.Create();
            MissionDefinition def = s.Replace("V1", new WaitStep(0.2f), new WaitStep(0.2f));
            def.Actors = new[] { new MissionActorSpec { Kind = ActorKind.Ped, SpawnAtStep = 1, Position = new Vec3(150f, 0f, 100f) } };
            s.Runner.TryStartMission(s.World, "V1");
            int baseline = s.World.Peds.Count;
            Assert.Equal(baseline, s.World.Peds.Count);
            s.Tick(2);
            Assert.Equal(baseline, s.World.Peds.Count);
            s.Seconds(0.3f);
            Assert.Equal(baseline + 1, s.World.Peds.Count);
            s.Seconds(1f);
            Assert.Equal(baseline, s.World.Peds.Count);
        }

        [Fact]
        public void AFailedMissionCanBeRetriedFromItsMarker()
        {
            Scene s = Scene.Create();
            s.Replace("V1", new TimedStep(new WaitStep(100f), 0.5f));
            s.Runner.TryStartMission(s.World, "V1");
            s.Seconds(1.5f);
            Assert.Equal(RunnerState.Idle, s.Runner.State);
            s.Teleport(new Vec3(1500f, 0f, 1500f));
            s.Tick(2);
            Assert.Contains(s.World.Mission.Blips, b => b.Kind == BlipKind.MissionStart);
            Assert.True(s.Runner.TryStartMission(s.World, "V1"));
        }

        [Fact]
        public void TheMarkerDoesNotRestartTheMissionWhilePlayerStandsInItAfterItEnded()
        {
            Scene s = Scene.Create();
            s.Tick();
            Vec3 marker = s.Runner.Campaign.Find("V1").StartMarker;
            s.Teleport(marker);
            s.Tick(2);
            Assert.Equal(RunnerState.Mission, s.Runner.State);
            s.World.Emit(GameEventType.PlayerDied, s.World.Player.Position, s.World.Player.Id, 0, 0f, null);
            s.Tick(2);
            s.Teleport(marker);
            s.Seconds(1f);
            Assert.Equal(RunnerState.Idle, s.Runner.State);
        }

        [Fact]
        public void EventsAreConsumedWithOneCursorAndMissionEventsDoNotReplay()
        {
            Scene s = Scene.Create();
            s.Replace("V1", new BuyItemStep("snack", 0, new Vec3(1, 0, 1)));
            s.World.Emit(GameEventType.Purchase, Vec3.Zero, s.World.Player.Id, 0, 1f, "snack");
            s.Runner.TryStartMission(s.World, "V1");
            s.Tick(3);
            // The purchase happened before the mission began: it must not count.
            Assert.Equal(RunnerState.Mission, s.Runner.State);
        }
    }
}
