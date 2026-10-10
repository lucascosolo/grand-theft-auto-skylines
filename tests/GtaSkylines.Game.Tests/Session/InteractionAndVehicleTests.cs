using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Session;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.SessionTests
{
    /// <summary>
    /// A sedan (half extents 0.9 x 0.75 x 2.2) parks with its ground point at the origin facing +z. Its driver door is at
    /// (-1.35, 0, 0.44). The player stands 1.65 m west of the door facing east.
    /// </summary>
    internal static class Scene
    {
        public const ushort ParkedId = 21;
        public static readonly Vec3 CarCentre = new Vec3(0f, 0.75f, 0f);
        public static readonly Vec3 PlayerSpot = new Vec3(-3f, 0f, 0.44f);

        public static Sim ParkedCar()
        {
            var sim = new Sim();
            sim.AddParkedCar(ParkedId, CarCentre, 0f);
            sim.Place(PlayerSpot, 90f);
            sim.Run(0.2, In.Idle);
            return sim;
        }
    }


    public class InteractionTargetingTests
    {
        [Fact]
        public void ParkedCarInFrontWithin3m_OffersEnterVehicleWithTheModelName()
        {
            Sim sim = Scene.ParkedCar();
            InteractionTarget t = sim.PS.Interaction;
            Assert.Equal(InteractionTargetKind.EnterVehicle, t.Kind);
            Assert.Equal(WorldTag.ForAmbientVehicle(Scene.ParkedId, true), t.TargetTag);
            Assert.Contains("Sedan", t.Prompt);
            Assert.Contains("{EnterExit}", t.Prompt);
            Assert.Equal("Press ~F~ to enter Sedan", sim.S.Hud.HelpText);
        }

        [Fact]
        public void Prompt_UsesTheRebindablePrimaryKeyName()
        {
            var sim = new Sim(settings: s => s.Bindings.Set(GameButton.EnterExit, 0, "G"));
            sim.AddParkedCar(Scene.ParkedId, Scene.CarCentre, 0f);
            sim.Place(Scene.PlayerSpot, 90f);
            sim.Run(0.2, In.Idle);
            Assert.Equal("Press ~G~ to enter Sedan", sim.S.Hud.HelpText);
        }


        [Fact]
        public void CarBehindThePlayer_IsNotOffered()
        {
            Sim sim = Scene.ParkedCar();
            sim.Place(Scene.PlayerSpot, 270f);
            sim.Run(0.2, In.Idle);
            Assert.NotEqual(InteractionTargetKind.EnterVehicle, sim.PS.Interaction.Kind);
        }

        [Fact]
        public void CarFartherThan3m_IsNotOffered()
        {
            Sim sim = Scene.ParkedCar();
            sim.Place(new Vec3(-5.2f, 0f, 0.44f), 90f);
            sim.Run(0.2, In.Idle);
            Assert.Equal(InteractionTargetKind.None, sim.PS.Interaction.Kind);
            Assert.Null(sim.S.Hud.HelpText);
        }

        [Fact]
        public void MovingAmbientCar_IsOfferedBelow8mps_AndNotAbove()
        {
            var sim = new Sim();
            sim.AddMovingCar(40, Scene.CarCentre, 0f, new Vec3(0f, 0f, 5f), 77);
            sim.Place(Scene.PlayerSpot, 90f);
            sim.Run(0.1, In.Idle);
            Assert.Equal(InteractionTargetKind.EnterVehicle, sim.PS.Interaction.Kind);
            Assert.Equal(WorldTag.ForAmbientVehicle(40, false), sim.PS.Interaction.TargetTag);

            var fast = new Sim();
            fast.AddMovingCar(40, Scene.CarCentre, 0f, new Vec3(0f, 0f, 12f), 77);
            fast.Place(Scene.PlayerSpot, 90f);
            fast.Run(0.1, In.Idle);
            Assert.NotEqual(InteractionTargetKind.EnterVehicle, fast.PS.Interaction.Kind);
        }

        [Fact]
        public void LockedGameCar_IsNotOffered_UnlockedIs()
        {
            var sim = new Sim();
            Car car = sim.W.SpawnCar(0, Vec3.Zero, 0f, CarOwner.Abandoned, 0x112233);
            car.Locked = true;
            sim.Place(Scene.PlayerSpot, 90f);
            sim.Run(0.1, In.Idle);
            Assert.NotEqual(InteractionTargetKind.EnterVehicle, sim.PS.Interaction.Kind);
            car.Locked = false;
            sim.Run(0.1, In.Idle);
            Assert.Equal(InteractionTargetKind.EnterVehicle, sim.PS.Interaction.Kind);
            Assert.Equal(car.Tag, sim.PS.Interaction.TargetTag);
        }

        [Fact]
        public void BuildingDoorWithin2_5m_OffersEnterBuilding()
        {
            var sim = new Sim();
            BuildingDescriptor b = Sim.Building(30, ServiceKind.Commercial, "", new Vec3(60f, 0f, 60f), 90f, "Corner Mart");
            sim.City.AddBuilding(b);
            sim.Place(new Vec3(70f, 0f, 60f), 270f);
            sim.Run(0.2, In.Idle);
            Assert.Equal(InteractionTargetKind.BuildingDoor, sim.PS.Interaction.Kind);
            Assert.Equal((ushort)30, sim.PS.Interaction.Building);
            Assert.Equal("Press ~E~ to enter Corner Mart", sim.S.Hud.HelpText);
            sim.Place(new Vec3(71.2f, 0f, 60f), 270f);
            sim.Run(0.2, In.Idle);
            Assert.Equal(InteractionTargetKind.None, sim.PS.Interaction.Kind);
        }

        [Fact]
        public void AbandonedBuildingDoor_IsNotOffered()
        {
            var sim = new Sim();
            BuildingDescriptor b = Sim.Building(30, ServiceKind.Commercial, "", new Vec3(60f, 0f, 60f), 90f);
            b.Abandoned = true;
            sim.City.AddBuilding(b);
            sim.Place(new Vec3(69f, 0f, 60f), 270f);
            sim.Run(0.2, In.Idle);
            Assert.Equal(InteractionTargetKind.None, sim.PS.Interaction.Kind);
        }

        [Fact]
        public void PersonInFrontWithin2_5m_OffersTalk_AndInteractEmitsTalkedWithTheHookLine()
        {
            var sim = new Sim();
            sim.W.Hooks.Dialogue = new FixedLine("Nice weather.");
            sim.AddCitizen(50, 5000, new Vec3(2f, 0f, 0f));
            sim.Place(Vec3.Zero, 90f);
            sim.Run(0.2, In.Idle);
            Assert.Equal(InteractionTargetKind.Talk, sim.PS.Interaction.Kind);
            Assert.Equal(WorldTag.ForAmbientCitizen(50), sim.PS.Interaction.TargetTag);
            Assert.Equal("Press ~E~ to talk", sim.S.Hud.HelpText);
            sim.Tick(In.Press(GameButton.Interact));
            Assert.Equal(1, sim.Count(GameEventType.Talked));
            Assert.Equal("Nice weather.", sim.Last(GameEventType.Talked).Text);
        }

        [Fact]
        public void Talk_WithoutADialogueHook_StillEmitsTalked()
        {
            var sim = new Sim();
            sim.AddCitizen(50, 5000, new Vec3(2f, 0f, 0f));
            sim.Place(Vec3.Zero, 90f);
            sim.Run(0.2, In.Idle);
            sim.Tick(In.Press(GameButton.Interact));
            Assert.Equal(1, sim.Count(GameEventType.Talked));
        }

        [Fact]
        public void Priority_CarBeatsBuildingDoorBeatsPerson()
        {
            var sim = new Sim();
            BuildingDescriptor b = Sim.Building(30, ServiceKind.Commercial, "", new Vec3(0f, 0f, 9f), 180f, "Mart");
            b.Doors = new[] { new Vec3(0.2f, 0f, 1f) };
            sim.City.AddBuilding(b);
            sim.AddParkedCar(Scene.ParkedId, new Vec3(0f, 0.75f, 2.4f), 0f);
            sim.AddCitizen(50, 5000, new Vec3(0f, 0f, 1.5f));
            sim.Place(Vec3.Zero, 0f);
            sim.Run(0.2, In.Idle);
            // Car door at (-1.35, 0, 2.84): 3.1 m away diagonally... move so every target is in reach.
            sim.Place(new Vec3(-1.2f, 0f, 1.2f), 0f);
            sim.Run(0.2, In.Idle);
            Assert.Equal(InteractionTargetKind.EnterVehicle, sim.PS.Interaction.Kind);
            sim.Snap.Vehicles.Clear();
            sim.Run(0.2, In.Idle);
            Assert.Equal(InteractionTargetKind.BuildingDoor, sim.PS.Interaction.Kind);
            sim.City.Buildings.Clear();
            sim.Run(0.2, In.Idle);
            Assert.Equal(InteractionTargetKind.Talk, sim.PS.Interaction.Kind);
        }

        [Fact]
        public void HelpPrompt_DisappearsTheStepTheTargetLeavesReach()
        {
            Sim sim = Scene.ParkedCar();
            Assert.NotNull(sim.S.Hud.HelpText);
            sim.Place(new Vec3(-30f, 0f, 0f), 90f);
            sim.Run(0.1, In.Idle);
            Assert.Null(sim.S.Hud.HelpText);
        }
    }

    internal sealed class FixedLine : IDialogueHook
    {
        private readonly string _line;
        public FixedLine(string line) { _line = line; }
        public string Line(GameWorld world, DialogueContext context, uint citizen, Ped ped) { return _line; }
    }

    public class EnterParkedCarTests
    {
        private static Sim Enter()
        {
            Sim sim = Scene.ParkedCar();
            sim.Tick(In.Press(GameButton.EnterExit));
            Assert.True(sim.Until(() => sim.Player.Has(PedFlags.InVehicle), 4.0, In.Idle), "player never got in");
            return sim;
        }

        [Fact]
        public void F_OnAParkedCar_TakesItOnce_AndSeatsThePlayer()
        {
            Sim sim = Enter();
            Assert.Equal(1, sim.CountEffects(CityEffectKind.TakeParkedVehicle));
            Assert.Equal(Scene.ParkedId, sim.FirstEffect(CityEffectKind.TakeParkedVehicle).Vehicle);
            Assert.Single(sim.W.Cars);
            Car car = sim.W.Cars[0];
            Assert.Equal(car.Id, sim.Player.VehicleId);
            Assert.Equal(0, sim.Player.Seat);
            Assert.Equal(sim.Player.Id, car.DriverId);
            Assert.Equal(CarOwner.Player, car.Owner);
            Assert.Equal(Scene.ParkedId, car.SourceCs1Vehicle);
            Assert.True(car.SourceParked);
            Assert.Equal(0x3366CC, car.ColourRgb);
            Assert.Equal("Sedan", car.Model.Name);
            Sim.Near(Scene.CarCentre, car.Position, 0.6f, "taken car keeps its pose");
        }

        [Fact]
        public void F_OnAParkedCar_EmitsCarStolenAndPlayerEnteredCar_AndCountsTheTheft()
        {
            Sim sim = Enter();
            Car car = sim.W.Cars[0];
            GameEvent stolen = sim.Last(GameEventType.CarStolen);
            Assert.Equal(sim.Player.Tag, stolen.ActorA);
            Assert.Equal(car.Tag, stolen.ActorB);
            Assert.Equal(0f, stolen.Value);
            GameEvent entered = sim.Last(GameEventType.PlayerEnteredCar);
            Assert.Equal(sim.Player.Tag, entered.ActorA);
            Assert.Equal(car.Tag, entered.ActorB);
            Assert.Equal(1, sim.Profile.GetStat(StatNames.VehiclesStolen));
            Assert.Equal("Sedan", sim.S.Hud.VehicleName);
            Assert.True(sim.S.Hud.VehicleBannerAge < HudBannerSlack);
        }

        private const float HudBannerSlack = 3.5f;

        [Fact]
        public void TheSnapshotKeepingTheTakenCar_DoesNotCreateASecondOne()
        {
            Sim sim = Enter();
            sim.Run(1.0, In.Idle);
            Assert.Single(sim.W.Cars);
            Assert.Equal(1, sim.CountEffects(CityEffectKind.TakeParkedVehicle));
        }

        [Fact]
        public void WalkingAway_DuringTheWalkToDoor_CancelsTheEntry()
        {
            Sim sim = Scene.ParkedCar();
            sim.Tick(In.Press(GameButton.EnterExit));
            sim.Run(0.3, In.Move(0f, -1f));
            sim.Run(3.0, In.Move(0f, -1f));
            Assert.False(sim.Player.Has(PedFlags.InVehicle));
            Assert.Equal(VehicleTransition.None, sim.PS.Transition);
        }

        [Fact]
        public void DrivingWithW_MovesTheCarForward()
        {
            Sim sim = Enter();
            Car car = sim.W.Cars[0];
            Vec3 start = car.Position;
            sim.Run(3.0, In.Forward);
            Assert.True((car.Position - start).Length > 4f, "car moved");
            Assert.True(Vec3.Dot(car.Position - start, car.Rotation.Forward) > 3f, "forward");
            Assert.True(sim.S.Hud.ShowSpeed);
            Assert.True(sim.S.Hud.SpeedKmh > 10);
            Assert.InRange(sim.Player.Position.X, car.Position.X - 3f, car.Position.X + 3f);
        }

        [Fact]
        public void ExitingAtStandstill_PutsThePlayerBesideTheDriverDoor_AndAbandonsTheCar()
        {
            Sim sim = Enter();
            sim.Run(0.5, In.Idle);
            Car car = sim.W.Cars[0];
            sim.Tick(In.Press(GameButton.EnterExit));
            Assert.True(sim.Until(() => !sim.Player.Has(PedFlags.InVehicle), 3.0, In.Idle));
            sim.Run(1.0, In.Idle);
            Vec3 door = sim.W.DoorPosition(car, true);
            Assert.True(Vec3.DistanceXZ(door, sim.Player.Position) < 1.5f, "within 1.5 m of the driver door");
            Assert.Equal(0, sim.Player.VehicleId);
            Assert.Equal(CarOwner.Abandoned, car.Owner);
            GameEvent ex = sim.Last(GameEventType.PlayerExitedCar);
            Assert.Equal(0f, ex.Value);
            Assert.Equal(car.Tag, ex.ActorB);
            Assert.Equal(car.Id, sim.PS.LastCarId);
        }

        [Fact]
        public void ExitingAtSpeedAbove8_BailsOutWithFiveDamage_AndKeepsTheVelocity()
        {
            Sim sim = Enter();
            Car car = sim.W.Cars[0];
            Assert.True(sim.Until(() => car.Body.Speed > 10f, 15.0, In.Forward), "car never reached 10 m/s");
            float hpBefore = sim.Player.Health;
            sim.Tick(In.Forward.WithPress(GameButton.EnterExit));
            Assert.True(sim.Until(() => !sim.Player.Has(PedFlags.InVehicle), 2.0, In.Forward));
            Assert.Equal(1f, sim.Last(GameEventType.PlayerExitedCar).Value);
            sim.Run(1.5, In.Idle);
            Sim.Near(hpBefore - PlayerRules.BailDamage, sim.Player.Health, 2f, "bail damage");
        }

        [Fact]
        public void ExitingBetween3And8mps_BrakesFirstAndExitsBelow3()
        {
            Sim sim = Enter();
            Car car = sim.W.Cars[0];
            Assert.True(sim.Until(() => car.Body.Speed > 6f, 10.0, In.Forward));
            Assert.True(car.Body.Speed < 8f, "test needs to catch the car in the 3-8 band");
            sim.Tick(In.Press(GameButton.EnterExit));
            Assert.True(sim.Player.Has(PedFlags.InVehicle), "still inside while the car is fast");
            Assert.True(sim.Until(() => !sim.Player.Has(PedFlags.InVehicle), 8.0, In.Idle));
            Assert.Equal(0f, sim.Last(GameEventType.PlayerExitedCar).Value);
        }

        [Fact]
        public void HornWhileHeldInACar_SirenTapOnEmergencyModels()
        {
            var sim = new Sim();
            Car car = sim.W.SpawnCar(1, Vec3.Zero, 0f, CarOwner.Abandoned, 0xFFFFFF);
            sim.W.PutInCar(sim.Player, car, 0);
            sim.Tick(In.Press(GameButton.Interact));
            sim.Tick(In.Idle.WithPress(GameButton.Interact).Continued());
            sim.Run(0.1, In.Idle);
            sim.Tick(In.Hold(GameButton.Interact));
            sim.Tick(In.Idle);
            Assert.True(car.Siren || car.LightBar, "a tap toggles the siren on a police car");

            Car sedan = sim.W.SpawnCar(0, new Vec3(20f, 0f, 20f), 0f, CarOwner.Abandoned, 0xFFFFFF);
            sim.W.TakeOutOfCar(sim.Player, new Vec3(18f, 0f, 20f), 0f);
            sim.W.PutInCar(sim.Player, sedan, 0);
            sim.Run(0.6, In.Hold(GameButton.Interact));
            Assert.True(sedan.Horn, "held Interact sounds the horn");
            sim.Run(0.2, In.Idle);
            Assert.False(sedan.Horn);
        }

        [Fact]
        public void HeadlightsPressed_TogglesTheLights()
        {
            var sim = new Sim(city: c => c.Hours = 12f);
            Car car = sim.W.SpawnCar(0, Vec3.Zero, 0f, CarOwner.Abandoned, 0xFFFFFF);
            sim.W.PutInCar(sim.Player, car, 0);
            sim.W.Clock.Hour = 12f;
            sim.Run(0.1, In.Idle);
            bool before = car.Headlights;
            sim.Tick(In.Press(GameButton.Headlights));
            Assert.NotEqual(before, car.Headlights);
            sim.Tick(In.Press(GameButton.Headlights));
            Assert.Equal(before, car.Headlights);
        }
    }

    public class CarjackTests
    {
        private static Sim Jack()
        {
            var sim = new Sim();
            sim.AddMovingCar(40, Scene.CarCentre, 0f, new Vec3(0f, 0f, 1f), 77);
            sim.Place(Scene.PlayerSpot, 90f);
            sim.Run(0.2, In.Idle);
            sim.Tick(In.Press(GameButton.EnterExit));
            return sim;
        }

        [Fact]
        public void F_OnAMovingAmbientCar_TakesItAndPullsTheDriverOut()
        {
            Sim sim = Jack();
            Assert.True(sim.Until(() => sim.Player.Has(PedFlags.InVehicle), 5.0, In.Idle), "player never got in");
            Assert.Equal(1, sim.CountEffects(CityEffectKind.TakeVehicle));
            Assert.Equal((ushort)40, sim.FirstEffect(CityEffectKind.TakeVehicle).Vehicle);
            Car car = sim.W.Cars[0];
            Assert.Equal(CarOwner.Player, car.Owner);
            Ped driver = sim.W.FindPedByCitizen(77);
            Assert.NotNull(driver);
            Assert.Equal(PedKind.Civilian, driver.Kind);
            Assert.False(driver.Has(PedFlags.InVehicle));
            Assert.Equal(0, driver.VehicleId);
            Assert.True(Vec3.DistanceXZ(driver.Position, sim.W.DoorPosition(car, true)) < 3.5f, "driver stands near the driver door");
            GameEvent jacked = sim.Last(GameEventType.CarJacked);
            Assert.Equal(sim.Player.Tag, jacked.ActorA);
            Assert.Equal(car.Tag, jacked.ActorB);
            Assert.Equal((float)driver.Id, jacked.Value);
            Assert.Equal(1, sim.Profile.GetStat(StatNames.VehiclesStolen));
        }

        [Fact]
        public void TheJackTakesAtLeastThePullOutTime()
        {
            Sim sim = Jack();
            sim.Run(PlayerRules.PullOutSeconds * 0.8, In.Idle);
            Assert.False(sim.Player.Has(PedFlags.InVehicle), "the pull-out has not finished yet");
        }
    }
}
