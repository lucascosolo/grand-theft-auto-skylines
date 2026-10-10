using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Roads;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Missions
{
    /// <summary>Builds the nine missions as documented on <see cref="CampaignGenerator"/>.</summary>
    internal sealed class CampaignBuilder
    {
        private readonly ICity _city;
        private readonly RoadGraph _roads;
        private readonly PlayerProfile _profile;
        private readonly PlaceFinder _finder;
        private readonly CampaignPlaces _places;
        private readonly uint _seed;

        public CampaignBuilder(ICity city, RoadGraph roads, PlayerProfile profile, CampaignPlaces places, uint seed)
        {
            _city = city;
            _roads = roads;
            _profile = profile;
            _finder = new PlaceFinder(city, roads);
            _places = places ?? _finder.FindCampaignPlaces();
            _seed = seed;
        }

        public CampaignPlaces Places { get { return _places; } }

        public MissionDefinition Build(string id)
        {
            var rng = new Rng(CampaignGenerator.MissionSeed(_seed, id));
            switch (id)
            {
                case MissionIds.V1: return V1();
                case MissionIds.V2: return V2(rng);
                case MissionIds.V3: return V3();
                case MissionIds.R1: return R1(rng);
                case MissionIds.R2: return R2();
                case MissionIds.R3: return R3();
                case MissionIds.C1: return C1(rng);
                case MissionIds.C2: return C2(rng);
                default: return C3(rng);
            }
        }

        // ---- missions -----------------------------------------------------------------------------------------

        private MissionDefinition V1()
        {
            CampaignPlace garage = _places.Garage;
            CampaignPlace store = _finder.ConvenienceStore(garage.Door);
            TextVars v = Vars(garage).SetPlace(store, false);
            MissionDefinition m = Def(MissionIds.V1, CampaignText.V1.Title, ContactId.Vince, garage, 250, v,
                CampaignText.V1.Unlock, CampaignText.V1.Passed, CampaignText.V1.Call, CampaignText.V1.Briefing);
            m.Steps = new MissionStep[]
            {
                With(new GoToStep(store.Door, 3f, TravelMode.OnFoot, VehicleKind.Car, -1), v, CampaignText.V1.GoToStore, CampaignText.V1.GoToStoreHelp),
                With(new BuyItemStep("snack", store.Building, store.Door), v, CampaignText.V1.BuySnack, CampaignText.V1.BuySnackHelp),
                With(new StealVehicleStep() { ParkedOnly = true, StoreSlot = 0 }, v, CampaignText.V1.StealCar, CampaignText.V1.StealCarHelp),
                With(new DeliverStep(garage.Door, 6f, DeliverWhat.CurrentCar, -1), v, CampaignText.V1.Deliver, CampaignText.V1.DeliverHelp),
                Talk(v, CampaignText.V1.Outro),
            };
            return m;
        }

        private MissionDefinition V2(Rng rng)
        {
            CampaignPlace garage = _places.Garage;
            CampaignPlace spot = OtherDistrict(garage, rng);
            int model = _city.FindVehicleModel(VehicleKind.Car, rng.NextUInt());
            VehicleModel vm = _city.GetVehicleModel(model);
            Vec3 park = Ground(_finder.NearestRoadPoint(spot.Door));
            TextVars v = Vars(garage).SetPlace(spot, false).Set(TextVars.Vehicle, vm != null ? vm.Name : "sports car");
            v.Set(TextVars.Street, _city.StreetNameNear(park, PlaceFinder.StreetRadius) ?? spot.Street);
            MissionDefinition m = Def(MissionIds.V2, CampaignText.V2.Title, ContactId.Vince, garage, 600, v,
                CampaignText.V2.Unlock, CampaignText.V2.Passed, CampaignText.V2.Call, CampaignText.V2.Briefing);
            m.Actors = new[]
            {
                new MissionActorSpec { Kind = ActorKind.Car, VehicleKind = VehicleKind.Car, ColourRgb = 0xD01818, Position = park, YawDeg = RoadHeading(park), Seed = rng.NextUInt() },
            };
            m.Steps = new MissionStep[]
            {
                With(new StealVehicleStep(0) { GuaranteedWitness = true, WitnessStars = 1 }, v, CampaignText.V2.Steal, null),
                With(new DeliverStep(garage.Door, 6f, DeliverWhat.MissionVehicle, 0) { MinHealth = 60f }, v, CampaignText.V2.Deliver, CampaignText.V2.DeliverHelp),
                Talk(v, CampaignText.V2.Outro),
            };
            m.Fail = MissionFail.VehicleDestroyed;
            m.VehicleSlot = 0;
            return m;
        }

        private MissionDefinition V3()
        {
            CampaignPlace garage = _places.Garage;
            var exclude = new List<ushort> { garage.Building };
            CampaignPlace shop = _finder.Nearest(ServiceKind.Industrial, InteriorArchetype.Garage, garage.Door, 0f, exclude);
            bool backyard = shop == null || shop.Archetype != InteriorArchetype.Garage;
            if (backyard) shop = garage;
            TextVars v = Vars(garage).SetPlace(shop, false);
            MissionDefinition m = Def(MissionIds.V3, CampaignText.V3.Title, ContactId.Vince, garage, 900, v,
                CampaignText.V3.Unlock, CampaignText.V3.Passed, CampaignText.V3.Call, CampaignText.V3.Briefing);
            m.Steps = new MissionStep[]
            {
                new SetWantedStep(2),
                With(new LoseWantedStep(), v, CampaignText.V3.LoseCops, CampaignText.V3.LoseCopsHelp),
                With(new ResprayStep(backyard ? (ushort)0 : shop.Building, shop.Door, backyard), v, CampaignText.V3.Respray, null),
                With(new GoToStep(garage.Door, 6f, TravelMode.AnyVehicle, VehicleKind.Car, -1), v, CampaignText.V3.Return, null),
                Talk(v, CampaignText.V3.Outro),
            };
            return m;
        }

        private MissionDefinition R1(Rng rng)
        {
            CampaignPlace station = _places.PoliceStation;
            CampaignPlace hot = _finder.HottestDistrict(_profile, station.Door, rng);
            Vec3 suspect = Ground(_finder.RoadPointAt(hot.Door, 300f, rng));
            TextVars v = Vars(station).SetPlace(hot, false)
                .Set(TextVars.Street2, _city.StreetNameNear(suspect, PlaceFinder.StreetRadius) ?? PlaceFinder.UnnamedStreet);
            MissionDefinition m = Def(MissionIds.R1, CampaignText.R1.Title, ContactId.Reyes, station, 700, v,
                CampaignText.R1.Unlock, CampaignText.R1.Passed, CampaignText.R1.Call, CampaignText.R1.Briefing);
            m.Actors = new[]
            {
                new MissionActorSpec { Kind = ActorKind.Car, VehicleKind = VehicleKind.Police, Position = InFront(station, 12f, 0f), YawDeg = FrontYaw(station) + 90f, Seed = rng.NextUInt() },
                new MissionActorSpec { Kind = ActorKind.Car, VehicleKind = VehicleKind.Car, Driving = ActorDriving.Flee, SpeedFactor = 1.25f, SpawnAtStep = 3, Position = suspect, YawDeg = RoadHeading(suspect), Seed = rng.NextUInt() },
                new MissionActorSpec { Kind = ActorKind.Ped, PedKind = PedKind.Civilian, InCarSlot = 1, Seat = 0, SpawnAtStep = 3, Position = suspect, Seed = rng.NextUInt() },
            };
            m.Steps = new MissionStep[]
            {
                With(new EnterVehicleStep(0), v, CampaignText.R1.EnterCruiser, CampaignText.R1.EnterCruiserHelp),
                With(new GoToStep(hot.Door, 40f, TravelMode.MissionVehicle, VehicleKind.Police, 0), v, CampaignText.R1.GoToDistrict, null),
                Talk(v, CampaignText.R1.Radio),
                With(new ChaseStep(1), v, CampaignText.R1.Chase, CampaignText.R1.ChaseHelp),
                Talk(v, CampaignText.R1.Outro),
            };
            m.Fail = MissionFail.VehicleDestroyed | MissionFail.TargetEscaped;
            m.VehicleSlot = 0;
            m.TargetSlot = 1;
            return m;
        }

        private MissionDefinition R2()
        {
            CampaignPlace station = _places.PoliceStation;
            CampaignPlace drop = _finder.Warehouse(station.Door, 400f);
            TextVars v = Vars(station).SetPlace(drop, true);
            MissionDefinition m = Def(MissionIds.R2, CampaignText.R2.Title, ContactId.Reyes, station, 1000, v,
                CampaignText.R2.Unlock, CampaignText.R2.Passed, CampaignText.R2.Call, CampaignText.R2.Briefing);
            var collect = new CollectStep(station.Building, station.Door, CampaignGenerator.EvidenceInteractionId, CampaignText.R2.Prompt)
            {
                SeenStars = 2,
                OutsideObjective = v.Rich(CampaignText.R2.GoInside),
            };
            m.Steps = new MissionStep[]
            {
                With(collect, v, CampaignText.R2.TakeBox, CampaignText.R2.SneakHelp),
                With(new DeliverStep(drop.Door, 5f, DeliverWhat.Player, -1) { FailIfWanted = true }, v, CampaignText.R2.Deliver, null),
                Talk(v, CampaignText.R2.Outro),
            };
            return m;
        }

        private MissionDefinition R3()
        {
            CampaignPlace station = _places.PoliceStation;
            CampaignPlace hideout = _finder.Industrial(station.Door, 500f);
            TextVars v = Vars(station).SetPlace(hideout, true);
            MissionDefinition m = Def(MissionIds.R3, CampaignText.R3.Title, ContactId.Reyes, station, 1500, v,
                CampaignText.R3.Unlock, CampaignText.R3.Passed, CampaignText.R3.Call, CampaignText.R3.Briefing);
            float yaw = FrontYaw(hideout);
            m.Actors = new MissionActorSpec[3];
            for (int i = 0; i < 3; i++)
            {
                m.Actors[i] = new MissionActorSpec
                {
                    Kind = ActorKind.Ped, PedKind = PedKind.Civilian, Hostile = true, Weapon = WeaponId.Pistol, Armour = 25f * i,
                    SpawnAtStep = 1, Position = InFront(hideout, 3f, (i - 1) * 4f), YawDeg = yaw, Seed = (uint)(_seed + 31 * i),
                };
            }
            m.Steps = new MissionStep[]
            {
                With(new GoToStep(hideout.Door, 25f, TravelMode.Any, VehicleKind.Car, -1), v, CampaignText.R3.GoTo, null),
                With(new EliminateStep(0, 1, 2), v, CampaignText.R3.Eliminate, CampaignText.R3.EliminateHelp),
                new SetWantedStep(2),
                With(new LoseWantedStep(), v, CampaignText.R3.Escape, null),
                Talk(v, CampaignText.R3.Outro),
            };
            return m;
        }

        private MissionDefinition C1(Rng rng)
        {
            CampaignPlace hall = _places.CityHall;
            var patients = new List<Patient>();
            _city.Patients(hall.Door, 3000f, patients);
            CampaignPlace pickup = null;
            uint citizen = 0;
            string person = CampaignGenerator.FallbackPatientName;
            if (patients.Count > 0)
            {
                pickup = _finder.Describe(patients[0].Building);
                if (pickup != null)
                {
                    pickup.Door = patients[0].Pickup;
                    citizen = patients[0].Citizen;
                    CitizenProfile p;
                    if (_city.TryGetCitizenProfile(citizen, out p) && !string.IsNullOrEmpty(p.Name)) person = p.Name;
                }
            }
            if (pickup == null) pickup = _finder.InBand(new[] { ServiceKind.Residential }, hall.Door, 300f, 1200f, rng);
            CampaignPlace hospital = _finder.Hospital(pickup.Door);
            TextVars v = Vars(hall).SetPlace(pickup, false).SetPlace(hospital, true).Set(TextVars.Person, person);
            MissionDefinition m = Def(MissionIds.C1, CampaignText.C1.Title, ContactId.MayorsOffice, hall, 800, v,
                CampaignText.C1.Unlock, CampaignText.C1.Passed, CampaignText.C1.Call, CampaignText.C1.Briefing);
            m.Actors = new[]
            {
                new MissionActorSpec { Kind = ActorKind.Car, VehicleKind = VehicleKind.Ambulance, Position = InFront(hall, 12f, 0f), YawDeg = FrontYaw(hall) + 90f, Seed = rng.NextUInt() },
                new MissionActorSpec { Kind = ActorKind.Ped, SpawnAtStep = 1, Citizen = citizen, Position = Ground(pickup.Door), Seed = rng.NextUInt() },
            };
            var deliver = With(new DeliverStep(hospital.Door, 8f, DeliverWhat.Passenger, 1), v, CampaignText.C1.Deliver, CampaignText.C1.DeliverHelp);
            m.Steps = new MissionStep[]
            {
                With(new EnterVehicleStep(0), v, CampaignText.C1.EnterAmbulance, null),
                With(new PickUpStep(1), v, CampaignText.C1.PickUp, null),
                new TimedStep(deliver, RushSeconds(pickup.Door, hospital.Door)),
            };
            m.Fail = MissionFail.VehicleDestroyed | MissionFail.PassengerKilled;
            m.VehicleSlot = 0;
            m.PassengerSlot = 1;
            return m;
        }

        private MissionDefinition C2(Rng rng)
        {
            CampaignPlace hall = _places.CityHall;
            var ids = new List<ushort>();
            _city.BurningBuildings(hall.Door, 3000f, ids);
            CampaignPlace fire = ids.Count > 0 ? _finder.Describe(ids[0]) : null;
            bool burning = fire != null;
            if (!burning)
            {
                double bearing = rng.Range(0f, 360f) * MathUtil.Deg2Rad;
                float d = rng.Range(400f, 1200f);
                var point = new Vec3(hall.Door.X + (float)Math.Sin(bearing) * d, 0f, hall.Door.Z + (float)Math.Cos(bearing) * d);
                _city.BuildingsNear(point, 3000f, ids);
                for (int i = 0; i < ids.Count && fire == null; i++) if (ids[i] != hall.Building) fire = _finder.Describe(ids[i]);
                if (fire == null) fire = _finder.MostCentral(new List<ushort> { hall.Building });
            }
            TextVars v = Vars(hall).SetPlace(fire, false);
            MissionDefinition m = Def(MissionIds.C2, CampaignText.C2.Title, ContactId.MayorsOffice, hall, 900, v,
                CampaignText.C2.Unlock, CampaignText.C2.Passed, CampaignText.C2.Call, burning ? CampaignText.C2.Briefing : CampaignText.C2.BriefingReported);
            m.Actors = new[]
            {
                new MissionActorSpec { Kind = ActorKind.Car, VehicleKind = VehicleKind.FireTruck, Position = InFront(hall, 12f, 0f), YawDeg = FrontYaw(hall) + 90f, Seed = rng.NextUInt() },
            };
            MissionStep enter = With(new EnterVehicleStep(0), v, CampaignText.C2.EnterEngine, null);
            MissionStep drive = With(new GoToStep(fire.Door, 20f, TravelMode.MissionVehicle, VehicleKind.FireTruck, 0), v, CampaignText.C2.GoToFire, null);
            m.Steps = burning
                ? new MissionStep[] { enter, drive, With(new HoseFireStep(fire.Building, fire.Door), v, CampaignText.C2.Hose, CampaignText.C2.HoseHelp), Talk(v, CampaignText.C2.Outro) }
                : new MissionStep[] { enter, drive, With(new GoToStep(fire.Door, 4f, TravelMode.OnFoot, VehicleKind.Car, -1), v, CampaignText.C2.Inspect, null), Talk(v, CampaignText.C2.FalseAlarm) };
            m.Fail = MissionFail.VehicleDestroyed;
            m.VehicleSlot = 0;
            return m;
        }

        private MissionDefinition C3(Rng rng)
        {
            CampaignPlace hall = _places.CityHall;
            var all = (ServiceKind[])Enum.GetValues(typeof(ServiceKind));
            CampaignPlace dest = _finder.InBand(all, hall.Door, 1500f, 3000f, rng);
            TextVars v = Vars(hall).SetPlace(dest, true);
            MissionDefinition m = Def(MissionIds.C3, CampaignText.C3.Title, ContactId.MayorsOffice, hall, 5000, v,
                CampaignText.C3.Unlock, CampaignText.C3.Passed, CampaignText.C3.Call, CampaignText.C3.Briefing);
            m.Actors = new[]
            {
                new MissionActorSpec { Kind = ActorKind.Ped, Position = InFront(hall, 1f, 3f), YawDeg = FrontYaw(hall), Seed = rng.NextUInt() },
            };
            m.Steps = new MissionStep[]
            {
                With(new EnterVehicleStep(VehicleKind.Car, VehicleKind.Van, VehicleKind.Taxi, VehicleKind.Police), v, CampaignText.C3.GetCar, null),
                With(new PickUpStep(0), v, CampaignText.C3.PickUp, null),
                Talk(v, CampaignText.C3.Ambush),
                new SetWantedStep(4),
                With(new DeliverStep(dest.Door, 8f, DeliverWhat.Passenger, 0), v, CampaignText.C3.Deliver, CampaignText.C3.DeliverHelp),
                Talk(v, CampaignText.C3.Outro),
            };
            m.Fail = MissionFail.PassengerKilled;
            m.PassengerSlot = 0;
            return m;
        }

        // ---- helpers ------------------------------------------------------------------------------------------

        private static readonly Dictionary<string, string[]> RequiresById = new Dictionary<string, string[]>
        {
            { MissionIds.V1, new string[0] },
            { MissionIds.V2, new[] { MissionIds.V1 } },
            { MissionIds.V3, new[] { MissionIds.V2 } },
            { MissionIds.R1, new[] { MissionIds.V2 } },
            { MissionIds.R2, new[] { MissionIds.R1 } },
            { MissionIds.R3, new[] { MissionIds.R2 } },
            { MissionIds.C1, new[] { MissionIds.R1 } },
            { MissionIds.C2, new[] { MissionIds.C1 } },
            { MissionIds.C3, new[] { MissionIds.C2, MissionIds.V3, MissionIds.R3 } },
        };

        private TextVars Vars(CampaignPlace contact)
        {
            return CampaignGenerator.BaseVars(_city, contact);
        }

        private static MissionDefinition Def(string id, string title, ContactId contact, CampaignPlace place, long cash, TextVars v,
            string unlock, string passed, string call, DialogueLine[] briefing)
        {
            return new MissionDefinition
            {
                Id = id,
                Title = title,
                Contact = contact,
                Requires = RequiresById[id],
                StartPlace = place,
                StartMarker = place.Door,
                Briefing = Lines(v, briefing),
                Reward = new MissionReward { Cash = cash, Reason = CampaignText.RewardReason(title) },
                UnlockText = v.Fill(unlock),
                PassedText = v.Fill(passed),
                CallLine = v.Fill(call),
            };
        }

        private static DialogueLine[] Lines(TextVars v, DialogueLine[] lines)
        {
            var filled = new DialogueLine[lines.Length];
            for (int i = 0; i < lines.Length; i++) filled[i] = new DialogueLine(lines[i].Speaker, v.Fill(lines[i].Text), lines[i].Seconds);
            return filled;
        }

        private static T With<T>(T step, TextVars v, string objective, string help) where T : MissionStep
        {
            step.Objective = objective == null ? null : v.Rich(objective);
            step.Help = help;
            return step;
        }

        private static TalkStep Talk(TextVars v, DialogueLine[] lines)
        {
            return new TalkStep(Lines(v, lines));
        }

        private Vec3 Ground(Vec3 p)
        {
            return new Vec3(p.X, _city.GroundHeight(p.X, p.Z), p.Z);
        }

        private float FrontYaw(CampaignPlace place)
        {
            BuildingDescriptor b;
            return place.Building != 0 && _city.TryGetBuilding(place.Building, out b) ? b.YawDeg : 0f;
        }

        /// <summary>A ground point <paramref name="forward"/> m out from the place's door along the building's front and <paramref name="right"/> m to its right.</summary>
        private Vec3 InFront(CampaignPlace place, float forward, float right)
        {
            Quat rot = Quat.Yaw(FrontYaw(place));
            return Ground(place.Door + rot * new Vec3(right, 0f, forward));
        }

        private float RoadHeading(Vec3 p)
        {
            RoadPoint rp;
            return _roads.TryNearestPoint(p, 50f, out rp) ? rp.HeadingDeg : 0f;
        }

        private CampaignPlace OtherDistrict(CampaignPlace garage, Rng rng)
        {
            var services = new[] { ServiceKind.Residential, ServiceKind.Commercial };
            CampaignPlace first = null;
            for (int i = 0; i < 6; i++)
            {
                CampaignPlace p = _finder.InBand(services, garage.Door, 600f, 1500f, rng);
                if (first == null) first = p;
                if (p.DistrictId != garage.DistrictId) return p;
            }
            return first;
        }

        private float RushSeconds(Vec3 from, Vec3 to)
        {
            var route = new Route();
            float length = _roads.FindRoute(from, to, route) == RouteStatus.Ok ? route.Length : Vec3.DistanceXZ(from, to) * 1.4f;
            return (float)(Math.Ceiling((30.0 + length / 11.0) / 5.0 - 1e-4) * 5.0);
        }
    }
}
