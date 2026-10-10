using GtaSkylines.Game.City;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Missions;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.Missions
{
    /// <summary>Each step type completes and fails in a FakeCity session.</summary>
    public class MissionStepTests
    {
        private static readonly Vec3 Target = new Vec3(300f, 0f, 300f);

        [Fact]
        public void BeginWritesObjectiveHelpBlipMarkerAndGpsAndEndRemovesThem()
        {
            Scene s = Scene.Create();
            var step = new GoToStep(Target, 5f, TravelMode.Any, VehicleKind.Car, -1)
            {
                Objective = RichText.Parse("Go to ~y~there~s~."),
                Help = "Hold Sprint.",
            };
            MissionContext ctx = s.Begin(step);
            Assert.Equal("Go to there.", s.World.Mission.Objective);
            Assert.Single(s.World.Mission.Blips);
            Assert.Equal(BlipKind.Destination, s.World.Mission.Blips[0].Kind);
            Assert.Single(s.World.Mission.Markers);
            Assert.Equal(5f, s.World.Mission.Markers[0].Radius);
            Assert.True(s.World.Mission.HasGpsTarget);
            Assert.Equal(Target, s.World.Mission.GpsTarget);
            Assert.Equal("Hold Sprint.", s.World.Messages.HelpText);
            step.End(ctx);
            Assert.Empty(s.World.Mission.Blips);
            Assert.Empty(s.World.Mission.Markers);
        }

        [Fact]
        public void GoToOnFootIsDoneInsideTheRadiusOnly()
        {
            Scene s = Scene.Create();
            var step = new GoToStep(Target, 5f, TravelMode.OnFoot, VehicleKind.Car, -1);
            MissionContext ctx = s.Begin(step);
            Assert.Equal(StepState.Running, s.StepOnce(step, ctx));
            s.Teleport(new Vec3(310f, 0f, 300f));
            Assert.Equal(StepState.Running, s.StepOnce(step, ctx));
            s.Teleport(new Vec3(303f, 0f, 300f));
            Assert.Equal(StepState.Done, s.StepOnce(step, ctx));
        }

        [Fact]
        public void GoToOnFootIsNotDoneInACarAndAnyVehicleNotOnFoot()
        {
            Scene s = Scene.Create();
            var foot = new GoToStep(Target, 8f, TravelMode.OnFoot, VehicleKind.Car, -1);
            var drive = new GoToStep(Target, 8f, TravelMode.AnyVehicle, VehicleKind.Car, -1);
            s.Teleport(Target);
            MissionContext ctx = s.Begin(drive);
            Assert.Equal(StepState.Running, s.StepOnce(drive, ctx));
            s.Drive(VehicleKind.Car, Target);
            MissionContext ctx2 = s.Begin(foot);
            Assert.Equal(StepState.Running, s.StepOnce(foot, ctx2));
            Assert.Equal(StepState.Done, s.StepOnce(drive, ctx));
        }

        [Fact]
        public void GoToVehicleOfKindNeedsThatKind()
        {
            Scene s = Scene.Create();
            var step = new GoToStep(Target, 8f, TravelMode.VehicleOfKind, VehicleKind.FireTruck, -1);
            s.Drive(VehicleKind.Car, Target);
            MissionContext ctx = s.Begin(step);
            Assert.Equal(StepState.Running, s.StepOnce(step, ctx));
        }

        [Fact]
        public void EnterVehicleBySlotAndByKind()
        {
            Scene s = Scene.Create();
            Car taxi = s.Spawn(VehicleKind.Taxi, new Vec3(110f, 0f, 100f));
            var byKind = new EnterVehicleStep(VehicleKind.Taxi);
            MissionContext ctx = s.Begin(byKind);
            Assert.Equal(StepState.Running, s.StepOnce(byKind, ctx));
            s.World.PutInCar(s.World.Player, taxi, 0);
            Assert.Equal(StepState.Done, s.StepOnce(byKind, ctx));
            var other = new EnterVehicleStep(VehicleKind.Police);
            Assert.Equal(StepState.Running, s.StepOnce(other, s.Begin(other)));
        }

        [Fact]
        public void EnterVehicleFailsWhenTheSlotCarIsDestroyed()
        {
            Scene s = Scene.Create();
            var step = new EnterVehicleStep(0);
            MissionContext ctx = s.Begin(step);
            Car car = s.Spawn(VehicleKind.Police, new Vec3(110f, 0f, 100f), CarOwner.Mission);
            ctx.ActorIds[0] = car.Id;
            s.World.RemoveCar(car);
            Assert.Equal(StepState.Failed, s.StepOnce(step, ctx));
            Assert.Equal(FailReasons.VehicleDestroyed, ctx.FailReason);
        }

        [Fact]
        public void StealVehicleNeedsTheEventAndTheSeat()
        {
            Scene s = Scene.Create();
            Car car = s.Spawn(VehicleKind.Car, new Vec3(110f, 0f, 100f));
            var step = new StealVehicleStep(VehicleKind.Car);
            MissionContext ctx = s.Begin(step);
            Assert.Equal(StepState.Running, s.StepOnce(step, ctx));
            s.World.PutInCar(s.World.Player, car, 0);
            Assert.Equal(StepState.Running, s.StepOnce(step, ctx));
            Scene.Event(ctx, GameEventType.CarStolen, s.World.Player.Id, car.Id);
            Assert.Equal(StepState.Done, s.StepOnce(step, ctx));
        }

        [Fact]
        public void ParkedOnlyIgnoresACarjacking()
        {
            Scene s = Scene.Create();
            Car car = s.Spawn(VehicleKind.Car, new Vec3(110f, 0f, 100f));
            var step = new StealVehicleStep(VehicleKind.Car) { ParkedOnly = true };
            MissionContext ctx = s.Begin(step);
            s.World.PutInCar(s.World.Player, car, 0);
            Scene.Event(ctx, GameEventType.CarJacked, s.World.Player.Id, car.Id);
            Assert.Equal(StepState.Running, s.StepOnce(step, ctx));
        }

        [Fact]
        public void StealVehicleAcceptsOnlyTheSlotCarWhenSet()
        {
            Scene s = Scene.Create();
            Car wanted = s.Spawn(VehicleKind.Car, new Vec3(110f, 0f, 100f));
            Car other = s.Spawn(VehicleKind.Car, new Vec3(120f, 0f, 100f));
            var step = new StealVehicleStep(0);
            MissionContext ctx = s.Begin(step);
            ctx.ActorIds[0] = wanted.Id;
            s.World.PutInCar(s.World.Player, other, 0);
            Scene.Event(ctx, GameEventType.CarStolen, s.World.Player.Id, other.Id);
            Assert.Equal(StepState.Running, s.StepOnce(step, ctx));
        }

        [Fact]
        public void GuaranteedWitnessPhonesThePoliceAfterTheDelay()
        {
            Scene s = Scene.Create();
            Car car = s.Spawn(VehicleKind.Car, new Vec3(110f, 0f, 100f));
            var step = new StealVehicleStep(VehicleKind.Car) { GuaranteedWitness = true, WitnessStars = 1, WitnessCallSeconds = 2f };
            MissionContext ctx = s.Begin(step);
            s.World.PutInCar(s.World.Player, car, 0);
            Scene.Event(ctx, GameEventType.CarStolen, s.World.Player.Id, car.Id);
            int pedsBefore = s.World.Peds.Count;
            Assert.Equal(StepState.Done, s.StepOnce(step, ctx));
            Assert.Equal(pedsBefore + 1, s.World.Peds.Count);
            Ped witness = s.World.Peds[s.World.Peds.Count - 1];
            Assert.True(witness.Has(PedFlags.OnPhone));
            Assert.Equal(0, s.World.Wanted.Stars);
            ctx.EventCount = 0;
            for (int i = 0; i < 150; i++) s.StepOnce(step, ctx);
            Assert.True(s.World.Wanted.Stars >= 1);
        }

        [Fact]
        public void DeliverCurrentCarDoneAtTheTargetAndRecordsHealth()
        {
            Scene s = Scene.Create();
            Car car = s.Drive(VehicleKind.Car, Target);
            var step = new DeliverStep(Target, 6f, DeliverWhat.CurrentCar, -1);
            MissionContext ctx = s.Begin(step);
            Assert.Equal(StepState.Done, s.StepOnce(step, ctx));
            Assert.Equal(car.Body.Health, step.CarHealthAtDelivery);
        }

        [Fact]
        public void DeliverFailsWhenTheCarIsTooDamaged()
        {
            Scene s = Scene.Create();
            Car car = s.Drive(VehicleKind.Car, Target);
            s.World.DamageCar(car, 60f, 0, car.Position);
            var step = new DeliverStep(Target, 6f, DeliverWhat.CurrentCar, -1) { MinHealth = 60f };
            MissionContext ctx = s.Begin(step);
            Assert.Equal(StepState.Failed, s.StepOnce(step, ctx));
            Assert.Equal(FailReasons.CarTooDamaged, ctx.FailReason);
        }

        [Fact]
        public void DeliverWithFailIfWantedFailsAsSpotted()
        {
            Scene s = Scene.Create();
            var step = new DeliverStep(Target, 5f, DeliverWhat.Player, -1) { FailIfWanted = true };
            MissionContext ctx = s.Begin(step);
            s.World.Wanted.Stars = 1;
            Assert.Equal(StepState.Failed, s.StepOnce(step, ctx));
            Assert.Equal(FailReasons.Spotted, ctx.FailReason);
        }

        [Fact]
        public void DeliverPlayerIsDoneOnFootAtTheTarget()
        {
            Scene s = Scene.Create();
            var step = new DeliverStep(Target, 5f, DeliverWhat.Player, -1);
            MissionContext ctx = s.Begin(step);
            Assert.Equal(StepState.Running, s.StepOnce(step, ctx));
            s.Teleport(Target);
            Assert.Equal(StepState.Done, s.StepOnce(step, ctx));
        }

        [Fact]
        public void DeliverPassengerLetsThemOutAndRemovesThemAfterTwoSeconds()
        {
            Scene s = Scene.Create();
            Car car = s.Drive(VehicleKind.Taxi, Target);
            Ped passenger = s.World.SpawnPed(PedKind.Civilian, 0, Target, 0f);
            s.World.PutInCar(passenger, car, 1);
            var step = new DeliverStep(Target, 8f, DeliverWhat.Passenger, 0);
            MissionContext ctx = s.Begin(step);
            ctx.ActorIds[0] = passenger.Id;
            Assert.Equal(StepState.Done, s.StepOnce(step, ctx));
            Assert.False(passenger.Has(PedFlags.InVehicle));
            for (int i = 0; i < 130; i++) s.StepOnce(step, ctx);
            Assert.Null(s.World.FindPed(passenger.Id));
        }

        [Fact]
        public void ChaseIsDoneWhenTheCarIsDestroyedAndFailsWhenTheTargetEscapes()
        {
            Scene s = Scene.Create();
            Car car = s.Spawn(VehicleKind.Car, new Vec3(200f, 0f, 100f), CarOwner.Mission);
            var step = new ChaseStep(0);
            MissionContext ctx = s.Begin(step);
            ctx.ActorIds[0] = car.Id;
            Assert.Equal(StepState.Running, s.StepOnce(step, ctx));
            car.Body.Teleport(new Vec3(900f, 0f, 100f), car.Rotation);
            Assert.Equal(StepState.Failed, s.StepOnce(step, ctx));
            Assert.Equal(FailReasons.TargetEscaped, ctx.FailReason);
        }

        [Fact]
        public void ChaseWithTheCarGoneIsDone()
        {
            Scene s = Scene.Create();
            Car car = s.Spawn(VehicleKind.Car, new Vec3(200f, 0f, 100f), CarOwner.Mission);
            var step = new ChaseStep(0);
            MissionContext ctx = s.Begin(step);
            ctx.ActorIds[0] = car.Id;
            s.World.DamageCar(car, 10000f, 0, car.Position);
            for (int i = 0; i < 60 * 30 && s.StepOnce(step, ctx) == StepState.Running; i++) { }
            Assert.Equal(StepState.Done, s.StepOnce(step, ctx));
        }

        [Fact]
        public void EliminateIsDoneWhenAllTargetsAreDeadOrUnspawned()
        {
            Scene s = Scene.Create();
            Ped a = s.World.SpawnPed(PedKind.Civilian, 0, new Vec3(120f, 0f, 100f), 0f);
            var step = new EliminateStep(0, 1);
            MissionContext ctx = s.Begin(step);
            ctx.ActorIds[0] = a.Id;
            Assert.Equal(StepState.Running, s.StepOnce(step, ctx));
            Assert.True(s.World.Mission.Blips.Count == 1);
            a.Flags |= PedFlags.Dead;
            Assert.Equal(StepState.Done, s.StepOnce(step, ctx));
        }

        [Fact]
        public void LoseWantedIsDoneAtZeroStars()
        {
            Scene s = Scene.Create();
            var step = new LoseWantedStep();
            MissionContext ctx = s.Begin(step);
            s.World.Wanted.Stars = 2;
            Assert.Equal(StepState.Running, s.StepOnce(step, ctx));
            s.World.Wanted.Stars = 0;
            Assert.Equal(StepState.Done, s.StepOnce(step, ctx));
        }

        [Fact]
        public void SetWantedRaisesStarsOnceAndNeverLowersThem()
        {
            Scene s = Scene.Create();
            s.World.Wanted.Stars = 3;
            var low = new SetWantedStep(2);
            MissionContext ctx = s.Begin(low);
            Assert.Equal(3, s.World.Wanted.Stars);
            Assert.Equal(StepState.Done, s.StepOnce(low, ctx));
            var high = new SetWantedStep(4);
            MissionContext ctx2 = s.Begin(high);
            Assert.Equal(4, s.World.Wanted.Stars);
            Assert.True(s.World.Wanted.HasLastKnownPosition);
            Assert.Equal(StepState.Done, s.StepOnce(high, ctx2));
        }

        [Fact]
        public void CollectOutsideShowsTheOutsideObjectiveAndIsDoneOnTheInteraction()
        {
            Scene s = Scene.Create();
            var step = new CollectStep(s.Test.Police, s.Door(s.Test.Police), 9000, "Take it")
            {
                Objective = RichText.Parse("Take the box."),
                OutsideObjective = RichText.Parse("Go into the station."),
            };
            MissionContext ctx = s.Begin(step);
            Assert.Equal("Go into the station.", s.World.Mission.Objective);
            Assert.Equal(StepState.Running, s.StepOnce(step, ctx));
            Scene.Event(ctx, GameEventType.CustomInteraction, s.World.Player.Id, 0, 1234f);
            Assert.Equal(StepState.Running, s.StepOnce(step, ctx));
            ctx.EventCount = 0;
            Scene.Event(ctx, GameEventType.CustomInteraction, s.World.Player.Id, 0, 9000f);
            Assert.Equal(StepState.Done, s.StepOnce(step, ctx));
        }

        [Fact]
        public void TimedCountsDownAndFailsWithTimeOut()
        {
            Scene s = Scene.Create();
            var inner = new WaitStep(100f);
            var step = new TimedStep(inner, 2f);
            MissionContext ctx = s.Begin(step);
            Assert.Equal(2f, s.World.Mission.TimerSeconds);
            StepState state = StepState.Running;
            for (int i = 0; i < 200 && state == StepState.Running; i++) state = s.StepOnce(step, ctx);
            Assert.Equal(StepState.Failed, state);
            Assert.Equal(FailReasons.TimeOut, ctx.FailReason);
        }

        [Fact]
        public void TimedIsDoneWhenTheInnerStepIsAndKeepsSecondsLeft()
        {
            Scene s = Scene.Create();
            var step = new TimedStep(new WaitStep(0.5f), 10f);
            MissionContext ctx = s.Begin(step);
            StepState state = StepState.Running;
            for (int i = 0; i < 100 && state == StepState.Running; i++) state = s.StepOnce(step, ctx);
            Assert.Equal(StepState.Done, state);
            Assert.InRange(step.SecondsLeft, 9f, 9.6f);
            step.End(ctx);
            Assert.Equal(-1f, s.World.Mission.TimerSeconds);
        }

        [Fact]
        public void WaitIsDoneAfterItsSeconds()
        {
            Scene s = Scene.Create();
            var step = new WaitStep(1f);
            MissionContext ctx = s.Begin(step);
            Assert.Equal(StepState.Running, s.StepOnce(step, ctx, 0.5f));
            Assert.Equal(StepState.Done, s.StepOnce(step, ctx, 0.5f));
        }

        [Fact]
        public void TalkQueuesEverySubtitleAndWaitsForTheirTotalSeconds()
        {
            Scene s = Scene.Create();
            var step = new TalkStep(new DialogueLine("Vince", "One.", 2f), new DialogueLine("Vince", "Two.", 3f));
            MissionContext ctx = s.Begin(step);
            Assert.Equal(1, s.World.Messages.QueuedSubtitles);
            Assert.Equal(StepState.Running, s.StepOnce(step, ctx, 4f));
            Assert.Equal(StepState.Done, s.StepOnce(step, ctx, 1f));
        }

        [Fact]
        public void ResprayIsDoneOnTheResprayedEvent()
        {
            Scene s = Scene.Create();
            var step = new ResprayStep(s.Test.Garage2, s.Door(s.Test.Garage2), false);
            MissionContext ctx = s.Begin(step);
            Assert.Equal(StepState.Running, s.StepOnce(step, ctx));
            Scene.Event(ctx, GameEventType.Resprayed, s.World.Player.Id, 0);
            Assert.Equal(StepState.Done, s.StepOnce(step, ctx));
        }

        [Fact]
        public void BackyardResprayChargesAndRepaintsAfterThreeStillSeconds()
        {
            Scene s = Scene.Create(null, p => p.AddCash(1000));
            Vec3 door = new Vec3(500f, 0f, 500f);
            Car car = s.Drive(VehicleKind.Car, door);
            int colour = car.ColourRgb;
            long cash = s.Profile.Cash;
            var step = new ResprayStep(0, door, true);
            MissionContext ctx = s.Begin(step);
            StepState state = StepState.Running;
            for (int i = 0; i < 60 * 4 && state == StepState.Running; i++) state = s.StepOnce(step, ctx);
            Assert.Equal(StepState.Done, state);
            Assert.Equal(cash - 300, s.Profile.Cash);
            Assert.NotEqual(colour, car.ColourRgb);
        }

        [Fact]
        public void BuyItemIsDoneOnAMatchingPurchaseEvent()
        {
            Scene s = Scene.Create();
            var step = new BuyItemStep("snack", 0, Target);
            MissionContext ctx = s.Begin(step);
            Scene.Event(ctx, GameEventType.Purchase, s.World.Player.Id, 0, 3f, "water");
            Assert.Equal(StepState.Running, s.StepOnce(step, ctx));
            ctx.EventCount = 0;
            Scene.Event(ctx, GameEventType.Purchase, s.World.Player.Id, 0, 3f, "snack");
            Assert.Equal(StepState.Done, s.StepOnce(step, ctx));
        }

        [Fact]
        public void BuyItemInAShopOnlyCountsInsideThatBuilding()
        {
            Scene s = Scene.Create();
            var step = new BuyItemStep("snack", s.Test.Store, s.Door(s.Test.Store));
            MissionContext ctx = s.Begin(step);
            Scene.Event(ctx, GameEventType.Purchase, s.World.Player.Id, 0, 3f, "snack");
            Assert.Equal(StepState.Running, s.StepOnce(step, ctx));
        }

        [Fact]
        public void PickUpSeatsThePassengerInTheNearbyCar()
        {
            Scene s = Scene.Create();
            Vec3 at = new Vec3(400f, 0f, 400f);
            Car car = s.Drive(VehicleKind.Taxi, at);
            Ped ped = s.World.SpawnPed(PedKind.Civilian, 0, at + new Vec3(4f, 0f, 0f), 0f);
            var step = new PickUpStep(0);
            MissionContext ctx = s.Begin(step);
            ctx.ActorIds[0] = ped.Id;
            StepState state = StepState.Running;
            for (int i = 0; i < 60 * 8 && state == StepState.Running; i++) state = s.StepOnce(step, ctx);
            Assert.Equal(StepState.Done, state);
            Assert.True(ped.Has(PedFlags.InVehicle));
            Assert.Equal(car.Id, ped.VehicleId);
            Assert.True(ped.Seat >= 1);
        }

        [Fact]
        public void HoseFireEmitsDouseEffectsAndFinishesAfterTwelveSeconds()
        {
            Scene s = Scene.Create();
            Vec3 point = s.Door(s.Test.Home1);
            s.Drive(VehicleKind.FireTruck, point + new Vec3(5f, 0f, 0f));
            var step = new HoseFireStep(s.Test.Home1, point);
            MissionContext ctx = s.Begin(step);
            s.World.Input = new InputFrame { Held = InputFrame.Bit(GameButton.Attack) };
            StepState state = StepState.Running;
            int steps = 0;
            for (; steps < 60 * 14 && state == StepState.Running; steps++) state = s.StepOnce(step, ctx);
            Assert.Equal(StepState.Done, state);
            Assert.InRange(steps, 60 * 12 - 2, 60 * 12 + 2);
            Assert.InRange(s.World.Effects.Count, 11, 12);
            CityEffect e = s.World.Effects.Items[0];
            Assert.Equal(CityEffectKind.DouseFire, e.Kind);
            Assert.Equal(s.Test.Home1, e.Building);
            Assert.Equal(0.1f, e.Amount, 3);
        }

        [Fact]
        public void HoseFireMakesNoProgressWithoutHoldingAttack()
        {
            Scene s = Scene.Create();
            Vec3 point = s.Door(s.Test.Home1);
            s.Drive(VehicleKind.FireTruck, point);
            var step = new HoseFireStep(s.Test.Home1, point);
            MissionContext ctx = s.Begin(step);
            s.World.Input = new InputFrame();
            for (int i = 0; i < 60 * 15; i++) Assert.Equal(StepState.Running, s.StepOnce(step, ctx));
            Assert.Equal(0, s.World.Effects.Count);
        }

        [Fact]
        public void HoseFireOfAReportedFireEmitsNothing()
        {
            Scene s = Scene.Create();
            Vec3 point = s.Door(s.Test.Home1);
            s.Drive(VehicleKind.FireTruck, point);
            var step = new HoseFireStep(0, point);
            MissionContext ctx = s.Begin(step);
            s.World.Input = new InputFrame { Held = InputFrame.Bit(GameButton.Attack) };
            for (int i = 0; i < 60 * 13; i++) s.StepOnce(step, ctx);
            Assert.Equal(0, s.World.Effects.Count);
        }

        [Fact]
        public void RaceIsDoneAfterTheLastCheckpointWithFirstPlaceWhenAlone()
        {
            Scene s = Scene.Create();
            Vec3[] cps = { new Vec3(200f, 0f, 0f), new Vec3(400f, 0f, 0f), new Vec3(600f, 0f, 0f) };
            Car car = s.Drive(VehicleKind.Car, new Vec3(0f, 0f, 0f));
            var step = new RaceStep(cps, new int[0]);
            MissionContext ctx = s.Begin(step);
            Assert.Equal(2, s.World.Mission.Blips.Count);
            StepState state = StepState.Running;
            foreach (Vec3 cp in cps)
            {
                Assert.Equal(StepState.Running, state);
                car.Body.Teleport(cp, car.Rotation);
                state = s.StepOnce(step, ctx);
            }
            Assert.Equal(StepState.Done, state);
            Assert.Equal(3, step.Passed);
            Assert.Equal(1, step.FinishPosition);
        }

        [Fact]
        public void RaceCheckpointsMustBePassedInOrder()
        {
            Scene s = Scene.Create();
            Vec3[] cps = { new Vec3(200f, 0f, 0f), new Vec3(400f, 0f, 0f) };
            Car car = s.Drive(VehicleKind.Car, new Vec3(0f, 0f, 0f));
            var step = new RaceStep(cps, new int[0]);
            MissionContext ctx = s.Begin(step);
            car.Body.Teleport(cps[1], car.Rotation);
            Assert.Equal(StepState.Running, s.StepOnce(step, ctx));
            Assert.Equal(0, step.Passed);
        }

        [Fact]
        public void RaceFailsAfterFifteenSecondsOutOfTheCar()
        {
            Scene s = Scene.Create();
            var step = new RaceStep(new[] { new Vec3(200f, 0f, 0f) }, new int[0]);
            MissionContext ctx = s.Begin(step);
            StepState state = StepState.Running;
            for (int i = 0; i < 60 * 16 && state == StepState.Running; i++) state = s.StepOnce(step, ctx);
            Assert.Equal(StepState.Failed, state);
            Assert.Equal(FailReasons.LeftJobVehicle, ctx.FailReason);
        }
    }
}
