using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Missions;
using GtaSkylines.Game.Roads;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.Missions
{
    public class SideJobTests
    {
        private static readonly Vec3 Spot = new Vec3(1000f, 0f, 1000f);

        private static VehicleKind VehicleFor(SideJobKind k)
        {
            switch (k)
            {
                case SideJobKind.Taxi: return VehicleKind.Taxi;
                case SideJobKind.Paramedic: return VehicleKind.Ambulance;
                case SideJobKind.Firefighter: return VehicleKind.FireTruck;
                case SideJobKind.Vigilante: return VehicleKind.Police;
                case SideJobKind.Delivery: return VehicleKind.Van;
                default: return VehicleKind.Car;
            }
        }

        private static Scene Primed(System.Action<GtaSkylines.Game.Profile.PlayerProfile> setup = null)
        {
            Scene s = Scene.Create(null, setup);
            s.Tick();
            s.Teleport(Spot);
            return s;
        }

        [Theory]
        [InlineData(SideJobKind.Taxi)]
        [InlineData(SideJobKind.Paramedic)]
        [InlineData(SideJobKind.Firefighter)]
        [InlineData(SideJobKind.Vigilante)]
        [InlineData(SideJobKind.Delivery)]
        public void EachJobStartsInItsOwnVehicle(SideJobKind kind)
        {
            Scene s = Primed();
            s.Drive(VehicleFor(kind), Spot);
            Assert.Equal(SideJobStartResult.Started, s.Runner.TryStartSideJob(s.World, kind));
            Assert.Equal(RunnerState.SideJob, s.Runner.State);
            Assert.Equal(kind, s.Runner.ActiveJob.Kind);
            Assert.Equal(1, s.Runner.ActiveJob.Level);
        }

        [Theory]
        [InlineData(SideJobKind.Taxi)]
        [InlineData(SideJobKind.Paramedic)]
        [InlineData(SideJobKind.Firefighter)]
        [InlineData(SideJobKind.Vigilante)]
        [InlineData(SideJobKind.Delivery)]
        [InlineData(SideJobKind.StreetRace)]
        public void OnFootIsTheWrongVehicleForEveryJob(SideJobKind kind)
        {
            Scene s = Primed();
            Assert.Equal(SideJobStartResult.WrongVehicle, s.Runner.TryStartSideJob(s.World, kind));
            Assert.Equal(RunnerState.Idle, s.Runner.State);
            Assert.Null(s.Runner.ActiveJob);
        }

        [Fact]
        public void ATaxiCannotRunAnAmbulanceJobAndACarCannotRunTheTaxi()
        {
            Scene s = Primed();
            s.Drive(VehicleKind.Car, Spot);
            Assert.Equal(SideJobStartResult.WrongVehicle, s.Runner.TryStartSideJob(s.World, SideJobKind.Taxi));
            Assert.Equal(SideJobStartResult.WrongVehicle, s.Runner.TryStartSideJob(s.World, SideJobKind.Paramedic));
        }

        [Fact]
        public void HoldingInteractNeverStartsASideJob()
        {
            Scene s = Primed();
            s.Drive(VehicleKind.Taxi, Spot);
            s.Hold(GameButton.Interact, true);
            s.Seconds(3f);
            Assert.Equal(RunnerState.Idle, s.Runner.State);
        }

        [Fact]
        public void ASecondShiftAndAShiftDuringAMissionAreRefused()
        {
            Scene s = Primed();
            s.Drive(VehicleKind.Taxi, Spot);
            Assert.Equal(SideJobStartResult.Started, s.Runner.TryStartSideJob(s.World, SideJobKind.Taxi));
            Assert.Equal(SideJobStartResult.AlreadyRunning, s.Runner.TryStartSideJob(s.World, SideJobKind.Taxi));
            s.Runner.EndSideJob(s.World, FailReasons.Cancelled);
            s.Replace("V1", new WaitStep(100f));
            s.Runner.TryStartMission(s.World, "V1");
            Assert.Equal(SideJobStartResult.MissionRunning, s.Runner.TryStartSideJob(s.World, SideJobKind.Taxi));
        }

        [Fact]
        public void AMissionPausesARunningShiftAndTheShiftResumesAfterwards()
        {
            Scene s = Primed();
            s.Drive(VehicleKind.Taxi, Spot);
            s.Runner.TryStartSideJob(s.World, SideJobKind.Taxi);
            s.Replace("V1", new WaitStep(0.3f));
            Assert.True(s.Runner.TryStartMission(s.World, "V1"));
            Assert.True(s.Runner.JobSuspended);
            s.Seconds(1f);
            Assert.False(s.Runner.JobSuspended);
            Assert.Equal(RunnerState.SideJob, s.Runner.State);
        }

        [Fact]
        public void EndSideJobEndsTheShiftWithoutPayAndNoOpsWhenIdle()
        {
            Scene s = Primed();
            s.Runner.EndSideJob(s.World, "x");
            s.Drive(VehicleKind.Taxi, Spot);
            s.Runner.TryStartSideJob(s.World, SideJobKind.Taxi);
            long cash = s.Profile.Cash;
            s.Runner.EndSideJob(s.World, FailReasons.Cancelled);
            Assert.Equal(RunnerState.Idle, s.Runner.State);
            Assert.Equal(cash, s.Profile.Cash);
            Assert.Contains("Taxi over", s.World.Messages.HelpText);
        }

        [Fact]
        public void LeavingTheJobVehicleEndsTheShift()
        {
            Scene s = Primed();
            Car taxi = s.Drive(VehicleKind.Taxi, Spot);
            s.Runner.TryStartSideJob(s.World, SideJobKind.Taxi);
            s.Tick(2);
            s.World.TakeOutOfCar(s.World.Player, Spot + new Vec3(4f, 0f, 0f), 0f);
            s.World.Emit(GameEventType.PlayerExitedCar, Spot, s.World.Player.Id, taxi.Id, 0f, null);
            s.Tick(2);
            Assert.Equal(RunnerState.Idle, s.Runner.State);
            Assert.Equal(FailReasons.LeftJobVehicle, s.Runner.LastFailReason);
        }

        [Fact]
        public void TheLevelComesFromTheProfileRounds()
        {
            Scene s = Primed(p => p.SideJobLevels["taxi"] = 4);
            s.Drive(VehicleKind.Taxi, Spot);
            s.Runner.TryStartSideJob(s.World, SideJobKind.Taxi);
            Assert.Equal(4, s.Runner.ActiveJob.Level);
        }

        // ---- taxi ---------------------------------------------------------------------------------------------

        private static AmbientCitizen Fare(Vec3 at) { return new AmbientCitizen { Instance = 5, Citizen = 77, Position = at, Model = 1 }; }

        [Fact]
        public void TaxiFarePromotionEmitsTakeCitizenOnce()
        {
            Scene s = Primed();
            s.Snapshot.Citizens.Add(Fare(Spot + new Vec3(60f, 0f, 0f)));
            s.Drive(VehicleKind.Taxi, Spot);
            s.Runner.TryStartSideJob(s.World, SideJobKind.Taxi);
            s.Tick(5);
            Assert.Equal(1, s.CountEffects(CityEffectKind.TakeCitizen));
            Assert.NotNull(s.World.FindPedByCitizen(77));
            s.Tick(60);
            Assert.Equal(1, s.CountEffects(CityEffectKind.TakeCitizen));
        }

        [Fact]
        public void TaxiWithNoCitizenNearbySpawnsAFare()
        {
            Scene s = Primed();
            s.Drive(VehicleKind.Taxi, Spot);
            int peds = s.World.Peds.Count;
            s.Runner.TryStartSideJob(s.World, SideJobKind.Taxi);
            s.Tick(5);
            Assert.Equal(peds + 1, s.World.Peds.Count);
            Assert.Equal(0, s.CountEffects(CityEffectKind.TakeCitizen));
        }

        [Fact]
        public void ATaxiRoundPaysCountsTheRoundAndRaisesTheLevel()
        {
            Scene s = Primed();
            s.Profile.AddStat("job_taxi_rounds", 2);
            s.Snapshot.Citizens.Add(Fare(Spot + new Vec3(60f, 0f, 0f)));
            Car taxi = s.Drive(VehicleKind.Taxi, Spot);
            s.Runner.TryStartSideJob(s.World, SideJobKind.Taxi);
            long cash = s.Profile.Cash;
            // Drive to the fare and wait for them to get in.
            taxi.Body.Teleport(Spot + new Vec3(60f, 0f, 3f), taxi.Rotation);
            s.Seconds(8f);
            var timed = (TimedStep)s.Runner.ActiveJob.Steps[1];
            var drop = (DeliverStep)timed.Inner;
            Assert.Equal(DeliverWhat.Passenger, drop.What);
            float km = Vec3.DistanceXZ(Spot + new Vec3(60f, 0f, 0f), drop.Target) / 1000f;
            Assert.InRange(km * 1000f, 300f, 1600f);
            taxi.Body.Teleport(drop.Target, taxi.Rotation);
            s.Seconds(5f);
            Assert.True(s.Profile.Cash >= cash + SideJobTable.Pay(SideJobKind.Taxi, 1));
            Assert.Equal(3, s.Profile.GetStat("job_taxi_rounds"));
            Assert.Equal(1, s.Profile.GetStat(GtaSkylines.Game.Profile.StatNames.JobsDone));
            Assert.Equal(2, s.Profile.GetSideJobLevel("taxi"));
            Assert.Equal(2, s.Runner.ActiveJob.Level);
            Assert.Equal(1, s.Runner.ActiveJob.RoundsThisShift);
        }

        // ---- paramedic ----------------------------------------------------------------------------------------

        [Fact]
        public void ParamedicTakesTheCitysPatientsFirst()
        {
            Scene s = Primed();
            Vec3 door = s.Door(s.Test.Home2);
            s.City.PatientList.Add(new Patient { Citizen = 77, Building = s.Test.Home2, Pickup = door });
            s.Teleport(door + new Vec3(0f, 0f, 200f));
            s.Drive(VehicleKind.Ambulance, door + new Vec3(0f, 0f, 200f));
            s.Runner.TryStartSideJob(s.World, SideJobKind.Paramedic);
            s.Tick(5);
            Ped patient = s.World.FindPedByCitizen(77);
            Assert.NotNull(patient);
            Assert.InRange(Vec3.DistanceXZ(patient.Position, door), 0f, 10f);
        }

        [Fact]
        public void ParamedicWithoutPatientsSpawnsOneAndDeliversToTheNearestHospital()
        {
            Scene s = Primed();
            s.Drive(VehicleKind.Ambulance, Spot);
            int peds = s.World.Peds.Count;
            s.Runner.TryStartSideJob(s.World, SideJobKind.Paramedic);
            s.Tick(5);
            Assert.Equal(peds + 1, s.World.Peds.Count);
            var timed = (TimedStep)s.Runner.ActiveJob.Steps[1];
            var drop = (DeliverStep)timed.Inner;
            Assert.Equal(s.Door(s.Test.Hospital), drop.Target);
            Assert.True(timed.Seconds > 0f);
        }

        // ---- firefighter --------------------------------------------------------------------------------------

        [Fact]
        public void FirefighterHosesABurningBuildingAndEmitsDouseFire()
        {
            Scene s = Primed();
            s.City.Buildings[s.Test.Home2].Burning = true;
            Vec3 door = s.Door(s.Test.Home2);
            Car truck = s.Drive(VehicleKind.FireTruck, Spot);
            s.Runner.TryStartSideJob(s.World, SideJobKind.Firefighter);
            s.Tick(5);
            truck.Body.Teleport(door + new Vec3(6f, 0f, 0f), truck.Rotation);
            s.Hold(GameButton.Attack, true);
            s.Seconds(6f);
            Assert.True(s.CountEffects(CityEffectKind.DouseFire) >= 3);
            CityEffect e = s.AllEffects.Find(x => x.Kind == CityEffectKind.DouseFire);
            Assert.Equal(s.Test.Home2, e.Building);
            Assert.Equal(0.1f, e.Amount, 3);
        }

        [Fact]
        public void FirefighterWithNoFireGetsAReportedFireWithoutDouseEffects()
        {
            Scene s = Primed();
            Car truck = s.Drive(VehicleKind.FireTruck, Spot);
            s.Runner.TryStartSideJob(s.World, SideJobKind.Firefighter);
            s.Tick(5);
            Assert.Equal(RunnerState.SideJob, s.Runner.State);
            var go = (GoToStep)s.Runner.ActiveJob.Steps[0];
            Assert.Equal(TravelMode.VehicleOfKind, go.Mode);
            Assert.Equal(20f, go.Radius);
            truck.Body.Teleport(go.Target, truck.Rotation);
            s.Hold(GameButton.Attack, true);
            s.Seconds(14f);
            Assert.Equal(0, s.CountEffects(CityEffectKind.DouseFire));
        }

        // ---- vigilante, delivery -----------------------------------------------------------------------------

        [Fact]
        public void VigilanteStartsAFleeingCriminalCarAbout300MetresAway()
        {
            Scene s = Primed();
            Car cruiser = s.Drive(VehicleKind.Police, Spot);
            s.Runner.TryStartSideJob(s.World, SideJobKind.Vigilante);
            s.Tick(5);
            Car criminal = null;
            foreach (Car c in s.World.Cars) if (c.Id != cruiser.Id && c.Driver is FleeDriver) criminal = c;
            Assert.NotNull(criminal);
            Assert.InRange(Vec3.DistanceXZ(criminal.Position, Spot), 200f, 400f);
            Assert.IsType<ChaseStep>(s.Runner.ActiveJob.Steps[0]);
        }

        [Fact]
        public void VigilanteAtLevelFourAddsASecondCar()
        {
            Scene s = Primed(p => p.SideJobLevels["vigilante"] = 4);
            s.Drive(VehicleKind.Police, Spot);
            s.Runner.TryStartSideJob(s.World, SideJobKind.Vigilante);
            s.Tick(5);
            int fleeing = 0;
            foreach (Car c in s.World.Cars) if (c.Driver is FleeDriver) fleeing++;
            Assert.Equal(2, fleeing);
            Assert.Equal(1.15f, ((FleeDriver)s.World.Cars[1].Driver).SpeedFactor, 3);
        }

        [Fact]
        public void DeliveryRoundIsPickUpLoadAndATimedDrop()
        {
            Scene s = Primed();
            s.Drive(VehicleKind.Van, Spot);
            s.Runner.TryStartSideJob(s.World, SideJobKind.Delivery);
            s.Tick(2);
            MissionStep[] steps = s.Runner.ActiveJob.Steps;
            Assert.Equal(new[] { StepKind.GoTo, StepKind.Wait, StepKind.Timed }, new[] { steps[0].Kind, steps[1].Kind, steps[2].Kind });
            var drop = (DeliverStep)((TimedStep)steps[2]).Inner;
            Assert.Equal(DeliverWhat.CurrentCar, drop.What);
            Assert.Equal(10f, drop.Radius);
        }

        // ---- street race --------------------------------------------------------------------------------------

        [Fact]
        public void TheRaceNeedsTheRaceMarkerAndACar()
        {
            Scene s = Primed();
            s.Drive(VehicleKind.Car, Spot);
            Assert.Equal(SideJobStartResult.WrongVehicle, s.Runner.TryStartSideJob(s.World, SideJobKind.StreetRace));
        }

        [Fact]
        public void TheRaceMarkerIsOnTheRoadsAndShownAsACheckpointBlip()
        {
            Scene s = Primed();
            RoadPoint rp;
            Assert.True(s.World.Roads.TryNearestPoint(s.Runner.RaceMarker, 5f, out rp));
            Assert.Contains(s.World.Mission.Blips, b => b.Kind == BlipKind.Checkpoint && b.Id == 100);
        }

        [Fact]
        public void RaceCheckpointsFollowTheRoadGraphAndRivalsLineUp()
        {
            Scene s = Primed();
            Vec3 marker = s.Runner.RaceMarker;
            s.Teleport(marker);
            s.Drive(VehicleKind.Car, marker);
            Assert.Equal(SideJobStartResult.Started, s.Runner.TryStartSideJob(s.World, SideJobKind.StreetRace));
            s.Tick(3);
            var race = (RaceStep)s.Runner.ActiveJob.Steps[1];
            Assert.Equal(SideJobTable.RaceCheckpoints[0], race.Checkpoints.Length);
            Vec3 previous = marker;
            foreach (Vec3 cp in race.Checkpoints)
            {
                RoadPoint rp;
                Assert.True(s.World.Roads.TryNearestPoint(cp, 2f, out rp), "checkpoint on a road");
                Assert.True(Vec3.DistanceXZ(previous, cp) <= 260f, "checkpoints at most 250 m apart");
                var route = new Route();
                Assert.Equal(RouteStatus.Ok, s.World.Roads.FindRoute(previous, cp, route));
                previous = cp;
            }
            Assert.Equal(3, race.RivalSlots.Length);
            int rivals = 0;
            foreach (Car c in s.World.Cars) if (c.Driver is RivalDriver) rivals++;
            Assert.Equal(3, rivals);
        }

        [Fact]
        public void TheShiftEndsAfterOneRace()
        {
            Scene s = Primed();
            Vec3 marker = s.Runner.RaceMarker;
            s.Teleport(marker);
            Car car = s.Drive(VehicleKind.Car, marker);
            s.Runner.TryStartSideJob(s.World, SideJobKind.StreetRace);
            s.Seconds(4f);
            var race = (RaceStep)s.Runner.ActiveJob.Steps[1];
            long cash = s.Profile.Cash;
            foreach (Vec3 cp in race.Checkpoints)
            {
                car.Body.Teleport(cp, car.Rotation);
                s.Tick(2);
            }
            s.Tick(10);
            Assert.Equal(RunnerState.Idle, s.Runner.State);
            Assert.Equal(1, s.Profile.GetStat("job_street_race_rounds"));
            Assert.True(s.Profile.Cash >= cash);
        }
    }
}
