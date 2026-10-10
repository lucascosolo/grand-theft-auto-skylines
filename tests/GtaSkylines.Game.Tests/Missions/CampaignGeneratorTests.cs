using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Missions;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Roads;
using Xunit;

namespace GtaSkylines.Game.Tests.Missions
{
    public class CampaignGeneratorTests
    {
        private const uint Seed = 99u;

        private static Campaign Gen(TestCity t, PlayerProfile profile = null, uint seed = Seed, bool roads = true)
        {
            return CampaignGenerator.Generate(t.City, roads ? TestCity.Grid().Build() : new RoadGraph(), profile ?? new PlayerProfile(), seed);
        }

        private static StepKind[] Kinds(MissionDefinition m)
        {
            var k = new StepKind[m.Steps.Length];
            for (int i = 0; i < k.Length; i++) k[i] = m.Steps[i].Kind;
            return k;
        }

        private static string AllText(MissionDefinition m)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(m.Title).Append('|').Append(m.UnlockText).Append('|').Append(m.PassedText).Append('|').Append(m.CallLine);
            foreach (DialogueLine l in m.Briefing) sb.Append('|').Append(l.Text);
            foreach (MissionStep s in m.Steps)
            {
                if (s.Objective != null) sb.Append('|').Append(s.Objective.Text);
                sb.Append('|').Append(s.Help);
                var talk = s as TalkStep;
                if (talk != null) foreach (DialogueLine l in talk.Lines) sb.Append('|').Append(l.Text);
            }
            return sb.ToString();
        }

        [Fact]
        public void NullArgumentsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => CampaignGenerator.Generate(null, new RoadGraph(), new PlayerProfile(), 1));
            Assert.Throws<ArgumentNullException>(() => CampaignGenerator.Generate(TestCity.Full().City, null, new PlayerProfile(), 1));
            Assert.Throws<ArgumentNullException>(() => CampaignGenerator.Generate(TestCity.Full().City, new RoadGraph(), null, 1));
        }

        [Fact]
        public void FullCityGivesNineMissionsInCampaignOrder()
        {
            Campaign c = Gen(TestCity.Full());
            Assert.Equal(9, c.Missions.Count);
            for (int i = 0; i < 9; i++)
            {
                Assert.Equal(MissionIds.All[i], c.Missions[i].Id);
                Assert.Same(c.Missions[i], c.Find(MissionIds.All[i]));
            }
            Assert.Null(c.Find("nope"));
        }

        [Fact]
        public void TitlesContactsAndRewardsFollowTheContract()
        {
            Campaign c = Gen(TestCity.Full());
            string[] titles = { "Fresh Start", "Hot Wheels", "Clean Getaway", "Ride Along", "Evidence Locker", "Takedown", "Code Red", "Five-Alarm", "Gridlock" };
            long[] rewards = { 250, 600, 900, 700, 1000, 1500, 800, 900, 5000 };
            ContactId[] contacts = { ContactId.Vince, ContactId.Vince, ContactId.Vince, ContactId.Reyes, ContactId.Reyes, ContactId.Reyes, ContactId.MayorsOffice, ContactId.MayorsOffice, ContactId.MayorsOffice };
            for (int i = 0; i < 9; i++)
            {
                MissionDefinition m = c.Missions[i];
                Assert.Equal(titles[i], m.Title);
                Assert.Equal(rewards[i], m.Reward.Cash);
                Assert.Equal("Mission: " + titles[i], m.Reward.Reason);
                Assert.Equal(contacts[i], m.Contact);
            }
        }

        [Fact]
        public void EveryMissionStartsAtItsContactBuildingDoor()
        {
            var t = TestCity.Full();
            Campaign c = Gen(t);
            foreach (MissionDefinition m in c.Missions)
            {
                CampaignPlace expected = m.Contact == ContactId.Vince ? c.Places.Garage : m.Contact == ContactId.Reyes ? c.Places.PoliceStation : c.Places.CityHall;
                Assert.Same(expected, m.StartPlace);
                Assert.Equal(expected.Door, m.StartMarker);
            }
            Assert.Equal("Rusty Bolt Motors", c.Places.Garage.Name);
            Assert.Equal("Central Precinct", c.Places.PoliceStation.Name);
            Assert.Equal("Testville City Hall", c.Places.CityHall.Name);
        }

        [Fact]
        public void StepSequencesMatchTheContract()
        {
            Campaign c = Gen(TestCity.Full());
            Assert.Equal(new[] { StepKind.GoTo, StepKind.BuyItem, StepKind.StealVehicle, StepKind.Deliver, StepKind.Talk }, Kinds(c.Find("V1")));
            Assert.Equal(new[] { StepKind.StealVehicle, StepKind.Deliver, StepKind.Talk }, Kinds(c.Find("V2")));
            Assert.Equal(new[] { StepKind.SetWanted, StepKind.LoseWanted, StepKind.Respray, StepKind.GoTo, StepKind.Talk }, Kinds(c.Find("V3")));
            Assert.Equal(new[] { StepKind.EnterVehicle, StepKind.GoTo, StepKind.Talk, StepKind.Chase, StepKind.Talk }, Kinds(c.Find("R1")));
            Assert.Equal(new[] { StepKind.Collect, StepKind.Deliver, StepKind.Talk }, Kinds(c.Find("R2")));
            Assert.Equal(new[] { StepKind.GoTo, StepKind.Eliminate, StepKind.SetWanted, StepKind.LoseWanted, StepKind.Talk }, Kinds(c.Find("R3")));
            Assert.Equal(new[] { StepKind.EnterVehicle, StepKind.PickUp, StepKind.Timed }, Kinds(c.Find("C1")));
            Assert.Equal(new[] { StepKind.EnterVehicle, StepKind.PickUp, StepKind.Talk, StepKind.SetWanted, StepKind.Deliver, StepKind.Talk }, Kinds(c.Find("C3")));
        }

        [Fact]
        public void BurningBuildingMakesFiveAlarmAHoseMission()
        {
            var t = TestCity.Full();
            t.City.Buildings[t.Home2].Burning = true;
            MissionDefinition m = Gen(t).Find("C2");
            Assert.Equal(new[] { StepKind.EnterVehicle, StepKind.GoTo, StepKind.HoseFire, StepKind.Talk }, Kinds(m));
            var hose = (HoseFireStep)m.Steps[2];
            Assert.Equal(t.Home2, hose.Building);
            Assert.Equal(12f, hose.Seconds);
            Assert.Equal(15f, hose.Radius);
            Assert.Equal(0.1f, hose.DousePerSecond);
            Assert.Contains("Harbor Cottage", AllText(m));
            Assert.Equal(VehicleKind.FireTruck, m.Actors[0].VehicleKind);
        }

        [Fact]
        public void WithoutAFireTheReportedFireIsAFalseAlarmInspection()
        {
            var t = TestCity.Full();
            MissionDefinition m = Gen(t).Find("C2");
            Assert.Equal(new[] { StepKind.EnterVehicle, StepKind.GoTo, StepKind.GoTo, StepKind.Talk }, Kinds(m));
            var inspect = (GoToStep)m.Steps[2];
            Assert.Equal(TravelMode.OnFoot, inspect.Mode);
            Assert.Equal(4f, inspect.Radius);
            var drive = (GoToStep)m.Steps[1];
            Assert.Equal(TravelMode.MissionVehicle, drive.Mode);
            Assert.Equal(20f, drive.Radius);
            Assert.Equal(0, drive.VehicleSlot);
            Assert.Equal(VehicleKind.FireTruck, m.Actors[0].VehicleKind);
        }

        [Fact]
        public void CodeRedUsesTheCitysSickPatientAndNamesThemAndTheHospital()
        {
            var t = TestCity.Full();
            Vec3 door = t.City.Buildings[t.Home1].Doors[0];
            t.City.PatientList.Add(new Patient { Citizen = 77, Building = t.Home1, Pickup = door });
            MissionDefinition m = Gen(t).Find("C1");
            string text = AllText(m);
            Assert.Contains("Ada Kowalski", text);
            Assert.Contains("St. Mercy Hospital", text);
            Assert.Contains("Elm House", text);
            Assert.Equal(77u, m.Actors[1].Citizen);
            Assert.Equal(VehicleKind.Ambulance, m.Actors[0].VehicleKind);
            Assert.Equal(1, m.Actors[1].SpawnAtStep);
            var timed = (TimedStep)m.Steps[2];
            var deliver = (DeliverStep)timed.Inner;
            Assert.Equal(DeliverWhat.Passenger, deliver.What);
            Assert.Equal(1, deliver.Slot);
            Assert.Equal(8f, deliver.Radius);
            Assert.Equal(0f, timed.Seconds % 5f);
            Assert.True(timed.Seconds >= 35f);
            Assert.Equal(MissionFail.VehicleDestroyed | MissionFail.PassengerKilled, m.Fail);
            Assert.Equal(0, m.VehicleSlot);
            Assert.Equal(1, m.PassengerSlot);
        }

        [Fact]
        public void CodeRedWithNobodySickSpawnsAFallbackPatient()
        {
            var t = TestCity.Full();
            MissionDefinition m = Gen(t).Find("C1");
            Assert.Contains(CampaignGenerator.FallbackPatientName, AllText(m));
            Assert.Equal(0u, m.Actors[1].Citizen);
            Assert.Equal(ActorKind.Ped, m.Actors[1].Kind);
        }

        [Fact]
        public void FreshStartSendsThePlayerToTheNearestConvenienceStoreAndBackToTheGarage()
        {
            var t = TestCity.Full();
            MissionDefinition m = Gen(t).Find("V1");
            string text = AllText(m);
            Assert.Contains("Quick Stop Market", text);
            Assert.Contains("Rusty Bolt Motors", m.UnlockText);
            Assert.Contains("Main Street", m.UnlockText);
            var go = (GoToStep)m.Steps[0];
            Assert.Equal(TravelMode.OnFoot, go.Mode);
            Assert.Equal(3f, go.Radius);
            Assert.Equal(t.City.Buildings[t.Store].Doors[0], go.Target);
            var buy = (BuyItemStep)m.Steps[1];
            Assert.Equal("snack", buy.ItemId);
            Assert.Equal(t.Store, buy.Building);
            var steal = (StealVehicleStep)m.Steps[2];
            Assert.True(steal.ParkedOnly);
            var deliver = (DeliverStep)m.Steps[3];
            Assert.Equal(DeliverWhat.CurrentCar, deliver.What);
            Assert.Equal(6f, deliver.Radius);
            Assert.Equal(m.StartMarker, deliver.Target);
            Assert.Empty(m.Actors);
        }

        [Fact]
        public void HotWheelsParksARedCarInAnotherDistrictAndGuaranteesAWitness()
        {
            var t = TestCity.Full();
            Campaign c = Gen(t);
            MissionDefinition m = c.Find("V2");
            Assert.Single(m.Actors);
            MissionActorSpec car = m.Actors[0];
            Assert.Equal(ActorKind.Car, car.Kind);
            Assert.Equal(ActorDriving.Parked, car.Driving);
            Assert.Equal(0xD01818, car.ColourRgb);
            Assert.Equal(VehicleKind.Car, car.VehicleKind);
            var steal = (StealVehicleStep)m.Steps[0];
            Assert.Equal(0, steal.VehicleSlot);
            Assert.True(steal.GuaranteedWitness);
            Assert.Equal(1, steal.WitnessStars);
            var deliver = (DeliverStep)m.Steps[1];
            Assert.Equal(DeliverWhat.MissionVehicle, deliver.What);
            Assert.Equal(60f, deliver.MinHealth);
            Assert.Equal(6f, deliver.Radius);
            Assert.Contains("Sedan", AllText(m));
            Assert.Equal(MissionFail.VehicleDestroyed, m.Fail);
            Assert.Equal(0, m.VehicleSlot);
            // The car is parked in a different district than the garage (district 2 here), 600-1500 m away.
            Assert.Equal(2, t.City.DistrictAt(car.Position.X, car.Position.Z));
        }

        [Fact]
        public void CleanGetawayResprayAtAnotherGarageNamesIt()
        {
            var t = TestCity.Full();
            MissionDefinition m = Gen(t).Find("V3");
            var wanted = (SetWantedStep)m.Steps[0];
            Assert.Equal(2, wanted.Stars);
            var respray = (ResprayStep)m.Steps[2];
            Assert.False(respray.Backyard);
            Assert.NotEqual(m.StartPlace.Building, respray.Building);
            Assert.Contains("Second Gear Garage", AllText(m));
            var back = (GoToStep)m.Steps[3];
            Assert.Equal(TravelMode.AnyVehicle, back.Mode);
            Assert.Equal(6f, back.Radius);
        }

        [Fact]
        public void RideAlongSetsUpTheCruiserTheSuspectAndTheDriver()
        {
            var t = TestCity.Full();
            var profile = new PlayerProfile();
            profile.DistrictHeat[2] = 70f;
            MissionDefinition m = Gen(t, profile).Find("R1");
            Assert.Equal(3, m.Actors.Length);
            Assert.Equal(VehicleKind.Police, m.Actors[0].VehicleKind);
            Assert.Equal(ActorDriving.Parked, m.Actors[0].Driving);
            Assert.Equal(VehicleKind.Car, m.Actors[1].VehicleKind);
            Assert.Equal(ActorDriving.Flee, m.Actors[1].Driving);
            Assert.Equal(1.25f, m.Actors[1].SpeedFactor);
            Assert.Equal(3, m.Actors[1].SpawnAtStep);
            Assert.Equal(ActorKind.Ped, m.Actors[2].Kind);
            Assert.Equal(1, m.Actors[2].InCarSlot);
            Assert.Equal(0, m.Actors[2].Seat);
            var enter = (EnterVehicleStep)m.Steps[0];
            Assert.Equal(0, enter.VehicleSlot);
            var go = (GoToStep)m.Steps[1];
            Assert.Equal(40f, go.Radius);
            Assert.Equal(TravelMode.MissionVehicle, go.Mode);
            Assert.Contains("Harbor", m.Steps[1].Objective.Text);
            var chase = (ChaseStep)m.Steps[3];
            Assert.Equal(1, chase.CarSlot);
            Assert.Equal(MissionFail.VehicleDestroyed | MissionFail.TargetEscaped, m.Fail);
            Assert.Equal(1, m.TargetSlot);
            Assert.Equal(400f, m.EscapeDistance);
            // The cruiser waits about 12 m in front of the station door.
            Assert.InRange(Vec3.DistanceXZ(m.Actors[0].Position, m.StartMarker), 5f, 40f);
        }

        [Fact]
        public void EvidenceLockerCollectsInsideTheStationAndDeliversToAWarehouse()
        {
            var t = TestCity.Full();
            MissionDefinition m = Gen(t).Find("R2");
            var collect = (CollectStep)m.Steps[0];
            Assert.Equal(t.Police, collect.Building);
            Assert.Equal(CampaignGenerator.EvidenceInteractionId, collect.CustomId);
            Assert.Equal(9000, collect.CustomId);
            Assert.Equal(2, collect.SeenStars);
            var deliver = (DeliverStep)m.Steps[1];
            Assert.Equal(DeliverWhat.Player, deliver.What);
            Assert.Equal(5f, deliver.Radius);
            Assert.True(deliver.FailIfWanted);
            Assert.Contains("Dockside Warehouse", AllText(m));
        }

        [Fact]
        public void TakedownPlacesThreeArmedHostilesWithGrowingArmour()
        {
            var t = TestCity.Full();
            MissionDefinition m = Gen(t).Find("R3");
            Assert.Equal(3, m.Actors.Length);
            float[] armour = { 0f, 25f, 50f };
            for (int i = 0; i < 3; i++)
            {
                Assert.Equal(ActorKind.Ped, m.Actors[i].Kind);
                Assert.True(m.Actors[i].Hostile);
                Assert.Equal(WeaponId.Pistol, m.Actors[i].Weapon);
                Assert.Equal(armour[i], m.Actors[i].Armour);
                Assert.Equal(1, m.Actors[i].SpawnAtStep);
            }
            var elim = (EliminateStep)m.Steps[1];
            Assert.Equal(new[] { 0, 1, 2 }, elim.Slots);
            Assert.Equal(2, ((SetWantedStep)m.Steps[2]).Stars);
            var go = (GoToStep)m.Steps[0];
            Assert.Equal(25f, go.Radius);
            Assert.Equal(TravelMode.Any, go.Mode);
            Assert.True(m.UnlockText.Length > 0);
        }

        [Fact]
        public void GridlockCollectsTheAideThenRaisesFourStarsAndDeliversFarAway()
        {
            var t = TestCity.Full();
            MissionDefinition m = Gen(t).Find("C3");
            Assert.Single(m.Actors);
            Assert.Equal(ActorKind.Ped, m.Actors[0].Kind);
            Assert.Equal(4, ((SetWantedStep)m.Steps[3]).Stars);
            var enter = (EnterVehicleStep)m.Steps[0];
            Assert.Equal(new[] { VehicleKind.Car, VehicleKind.Van, VehicleKind.Taxi, VehicleKind.Police }, enter.Kinds);
            var deliver = (DeliverStep)m.Steps[4];
            Assert.Equal(DeliverWhat.Passenger, deliver.What);
            Assert.Equal(0, deliver.Slot);
            Assert.Equal(8f, deliver.Radius);
            float distance = Vec3.DistanceXZ(deliver.Target, m.StartMarker);
            Assert.InRange(distance, 1000f, 3200f);
            Assert.Equal(MissionFail.PassengerKilled, m.Fail);
            Assert.Equal(0, m.PassengerSlot);
        }

        [Fact]
        public void UnlockRequirementsFormTheCampaignChains()
        {
            Campaign c = Gen(TestCity.Full());
            var expected = new Dictionary<string, string[]>
            {
                { "V1", new string[0] }, { "V2", new[] { "V1" } }, { "V3", new[] { "V2" } },
                { "R1", new[] { "V2" } }, { "R2", new[] { "R1" } }, { "R3", new[] { "R2" } },
                { "C1", new[] { "R1" } }, { "C2", new[] { "C1" } }, { "C3", new[] { "C2", "V3", "R3" } },
            };
            foreach (var kv in expected)
                Assert.Equal(kv.Value.Length == 0 ? kv.Value : SortedCopy(kv.Value), SortedCopy(c.Find(kv.Key).Requires));
        }

        private static string[] SortedCopy(string[] a) { var b = (string[])a.Clone(); Array.Sort(b, StringComparer.Ordinal); return b; }

        private static string[] AvailableIds(Campaign c, PlayerProfile p)
        {
            var list = new List<MissionDefinition>();
            c.Available(p, list);
            var ids = new string[list.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = list[i].Id;
            return ids;
        }

        private static PlayerProfile Done(params string[] ids)
        {
            var p = new PlayerProfile();
            foreach (string id in ids) p.CompleteMission(id);
            return p;
        }

        [Fact]
        public void OnlyFreshStartIsAvailableAtTheBeginning()
        {
            Assert.Equal(new[] { "V1" }, AvailableIds(Gen(TestCity.Full()), new PlayerProfile()));
        }

        [Fact]
        public void AvailableFollowsTheUnlockOrderInCampaignOrder()
        {
            Campaign c = Gen(TestCity.Full());
            Assert.Equal(new[] { "V2" }, AvailableIds(c, Done("V1")));
            Assert.Equal(new[] { "V3", "R1" }, AvailableIds(c, Done("V1", "V2")));
            Assert.Equal(new[] { "V3", "R2", "C1" }, AvailableIds(c, Done("V1", "V2", "R1")));
            Assert.Equal(new[] { "R3", "C2" }, AvailableIds(c, Done("V1", "V2", "V3", "R1", "R2", "C1")));
            Assert.Equal(new[] { "C3" }, AvailableIds(c, Done("V1", "V2", "V3", "R1", "R2", "R3", "C1", "C2")));
            Assert.Empty(AvailableIds(c, Done(MissionIds.All)));
        }

        [Fact]
        public void TheFinaleNeedsV3AndR3AsWellAsC2()
        {
            Campaign c = Gen(TestCity.Full());
            Assert.DoesNotContain("C3", AvailableIds(c, Done("V1", "V2", "R1", "R2", "R3", "C1", "C2")));
            Assert.DoesNotContain("C3", AvailableIds(c, Done("V1", "V2", "V3", "R1", "R2", "C1", "C2")));
            Assert.Contains("C3", AvailableIds(c, Done("V1", "V2", "V3", "R1", "R2", "R3", "C1", "C2")));
        }

        [Fact]
        public void AvailableClearsTheListFirstAndIsAvailableExcludesCompleted()
        {
            Campaign c = Gen(TestCity.Full());
            var list = new List<MissionDefinition> { c.Find("C3") };
            c.Available(new PlayerProfile(), list);
            Assert.Single(list);
            Assert.True(c.Find("V1").IsAvailable(new PlayerProfile()));
            Assert.False(c.Find("V1").IsAvailable(Done("V1")));
            Assert.False(c.Find("V2").IsAvailable(new PlayerProfile()));
        }

        [Fact]
        public void NextForReturnsTheContactsFirstAvailableMission()
        {
            Campaign c = Gen(TestCity.Full());
            Assert.Equal("V1", c.NextFor(ContactId.Vince, new PlayerProfile()).Id);
            Assert.Null(c.NextFor(ContactId.Reyes, new PlayerProfile()));
            Assert.Null(c.NextFor(ContactId.MayorsOffice, new PlayerProfile()));
            PlayerProfile p = Done("V1", "V2");
            Assert.Equal("V3", c.NextFor(ContactId.Vince, p).Id);
            Assert.Equal("R1", c.NextFor(ContactId.Reyes, p).Id);
        }

        [Fact]
        public void NoUnfilledPlaceholdersRemainInAnyMissionText()
        {
            foreach (TestCity t in new[] { TestCity.Full(), TestCity.Sparse(), TestCity.Empty() })
            {
                Campaign c = Gen(t);
                foreach (MissionDefinition m in c.Missions) Assert.True(Txt.Filled(AllText(m)), m.Id + ": " + AllText(m));
            }
        }

        [Fact]
        public void GenerationIsDeterministicForTheSameInputs()
        {
            var t = TestCity.Full();
            Campaign a = Gen(t), b = Gen(t);
            for (int i = 0; i < 9; i++)
            {
                Assert.Equal(AllText(a.Missions[i]), AllText(b.Missions[i]));
                Assert.Equal(a.Missions[i].StartMarker, b.Missions[i].StartMarker);
                Assert.Equal(a.Missions[i].Actors.Length, b.Missions[i].Actors.Length);
                for (int k = 0; k < a.Missions[i].Actors.Length; k++) Assert.Equal(a.Missions[i].Actors[k].Position, b.Missions[i].Actors[k].Position);
            }
        }

        [Fact]
        public void RebuildGivesTheSameMissionWhileTheCityIsUnchanged()
        {
            var t = TestCity.Full();
            RoadGraph roads = TestCity.Grid().Build();
            var profile = new PlayerProfile();
            Campaign c = CampaignGenerator.Generate(t.City, roads, profile, Seed);
            foreach (string id in MissionIds.All)
            {
                MissionDefinition again = CampaignGenerator.Rebuild(id, c, t.City, roads, profile, Seed);
                Assert.Equal(AllText(c.Find(id)), AllText(again));
                Assert.Equal(c.Find(id).Steps.Length, again.Steps.Length);
            }
            Assert.Null(CampaignGenerator.Rebuild("zzz", c, t.City, roads, profile, Seed));
        }

        [Fact]
        public void RebuildSeesPatientsAndFiresThatAppearedAfterGeneration()
        {
            var t = TestCity.Full();
            RoadGraph roads = TestCity.Grid().Build();
            var profile = new PlayerProfile();
            Campaign c = CampaignGenerator.Generate(t.City, roads, profile, Seed);
            Assert.Contains(CampaignGenerator.FallbackPatientName, AllText(c.Find("C1")));
            t.City.PatientList.Add(new Patient { Citizen = 77, Building = t.Home1, Pickup = t.City.Buildings[t.Home1].Doors[0] });
            t.City.Buildings[t.Home3].Burning = true;
            MissionDefinition c1 = CampaignGenerator.Rebuild("C1", c, t.City, roads, profile, Seed);
            MissionDefinition c2 = CampaignGenerator.Rebuild("C2", c, t.City, roads, profile, Seed);
            Assert.Contains("Ada Kowalski", AllText(c1));
            Assert.Contains(StepKind.HoseFire, Kinds(c2));
        }

        [Fact]
        public void MissionSeedMixesTheIndex()
        {
            Assert.Equal(Rng.Seed(5, 1), CampaignGenerator.MissionSeed(5, "V1"));
            Assert.Equal(Rng.Seed(5, 9), CampaignGenerator.MissionSeed(5, "C3"));
            Assert.Throws<ArgumentException>(() => CampaignGenerator.MissionSeed(5, "nope"));
        }

        [Fact]
        public void BaseVarsHoldTheCityAndContactPlace()
        {
            var t = TestCity.Full();
            Campaign c = Gen(t);
            TextVars v = CampaignGenerator.BaseVars(t.City, c.Places.Garage);
            Assert.Equal("Testville", v.Get(TextVars.City));
            Assert.Equal("Rusty Bolt Motors", v.Get(TextVars.Contact));
            Assert.Equal("Main Street", v.Get(TextVars.ContactStreet));
            Assert.Equal("Old Town", v.Get(TextVars.ContactDistrict));
        }

        // ---- cities missing the preferred services ----------------------------------------------------------

        [Fact]
        public void ACityWithoutPoliceHospitalCityHallOrIndustryStillHasNineValidMissions()
        {
            var t = TestCity.Sparse();
            Campaign c = Gen(t);
            Assert.Equal(9, c.Missions.Count);
            foreach (MissionDefinition m in c.Missions) AssertValid(m);
        }

        [Fact]
        public void SparseCityTextsNameTheFallbackContactBuildings()
        {
            var t = TestCity.Sparse();
            Campaign c = Gen(t);
            Assert.Contains(c.Places.Garage.Name, c.Find("V1").UnlockText);
            Assert.Contains(c.Places.PoliceStation.Name, c.Find("R1").UnlockText);
            Assert.Contains(c.Places.CityHall.Name, c.Find("C1").UnlockText);
            Assert.NotEqual(PlaceFinder.NowhereName, c.Places.Garage.Name);
            Assert.Contains("Lonely Mart", AllText(c.Find("V1")));
        }

        [Fact]
        public void WithoutAGarageTheResprayHappensInABackyard()
        {
            var t = TestCity.Sparse();
            var respray = (ResprayStep)Gen(t).Find("V3").Steps[2];
            Assert.True(respray.Backyard);
        }

        [Fact]
        public void WithoutAHospitalCodeRedFallsBackToAShop()
        {
            var t = TestCity.Sparse();
            MissionDefinition m = Gen(t).Find("C1");
            string text = AllText(m);
            Assert.True(Txt.Has(text, "Lonely Mart") || Txt.Has(text, "Far Fashions"), text);
            Assert.Contains(CampaignGenerator.FallbackPatientName, text);
        }

        [Fact]
        public void WithoutAnyBuildingsEveryMissionIsStillValid()
        {
            Campaign c = Gen(TestCity.Empty());
            Assert.Equal(9, c.Missions.Count);
            foreach (MissionDefinition m in c.Missions)
            {
                AssertValid(m);
                Assert.Equal(PlaceFinder.NowhereName, m.StartPlace.Name);
            }
        }

        [Fact]
        public void WithoutAnyRoadsEveryMissionIsStillValid()
        {
            Campaign c = Gen(TestCity.Full(), roads: false);
            Assert.Equal(9, c.Missions.Count);
            foreach (MissionDefinition m in c.Missions) AssertValid(m);
        }

        private static void AssertValid(MissionDefinition m)
        {
            Assert.False(string.IsNullOrEmpty(m.Title), m.Id);
            Assert.False(string.IsNullOrEmpty(m.UnlockText), m.Id);
            Assert.False(string.IsNullOrEmpty(m.CallLine), m.Id);
            Assert.NotEmpty(m.Briefing);
            Assert.NotEmpty(m.Steps);
            Assert.True(m.Reward.Cash > 0, m.Id);
            Assert.Equal("Mission: " + m.Title, m.Reward.Reason);
            Assert.NotNull(m.StartPlace);
            Assert.Equal(m.StartPlace.Door, m.StartMarker);
            foreach (MissionStep s in m.Steps) Assert.NotNull(s);
            foreach (MissionActorSpec a in m.Actors)
            {
                if (a.InCarSlot >= 0)
                {
                    Assert.True(a.InCarSlot < m.Actors.Length);
                    Assert.True(m.Actors[a.InCarSlot].SpawnAtStep <= a.SpawnAtStep);
                }
            }
            Assert.True(Txt.Filled(AllText(m)), m.Id);
        }
    }
}
