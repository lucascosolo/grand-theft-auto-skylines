using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Roads;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Missions
{
    /// <summary>
    /// One side job's shift. The <see cref="MissionRunner"/> starts it (vehicle checked with
    /// <see cref="SideJobTable.AcceptsVehicle"/>), then loops: <see cref="BuildRound"/> → run <see cref="Steps"/> with
    /// the shared <see cref="MissionContext"/> (actors from <see cref="Actors"/> spawned like mission actors; slots a
    /// round fills itself, such as a promoted taxi fare, are removed the same way at the round's end) → on all steps Done,
    /// <see cref="RoundPay"/> is earned (<c>Wallet.Earn(pay, RewardReason)</c>), the rounds stat +1, the
    /// <c>jobs_done</c> stat +1, the profile level updated from <see cref="SideJobTable.LevelForRounds"/>, and the next
    /// round is built 3 s later. A step failure ends the shift (help text "<i>Name</i> over: <i>reason</i>", no big
    /// screen). When <see cref="BuildRound"/> returns false (nothing to do right now) the runner shows
    /// <see cref="SideJobText.Waiting"/> and retries every 5 s.
    /// Rounds are built on round start only (they allocate their steps); stepping a round allocates nothing.
    /// </summary>
    public abstract class SideJob
    {
        /// <summary>Creates a job of a kind at a level (clamped 1-10).</summary>
        protected SideJob(SideJobKind kind, int level)
        {
            Kind = kind;
            Level = SideJobTable.Clamp(level);
            Steps = new MissionStep[0];
            Actors = new MissionActorSpec[0];
        }

        /// <summary>Kind.</summary>
        public SideJobKind Kind { get; private set; }

        /// <summary>Current level 1-10 (raised by the runner between rounds).</summary>
        public int Level;

        /// <summary>Rounds completed in this shift.</summary>
        public int RoundsThisShift;

        /// <summary>Dollars earned in this shift.</summary>
        public long EarnedThisShift;

        /// <summary>The current round's steps.</summary>
        public MissionStep[] Steps { get; protected set; }

        /// <summary>The current round's actors (slots 0..Length−1; rounds may fill higher slots themselves).</summary>
        public MissionActorSpec[] Actors { get; protected set; }

        /// <summary>Wallet reason of a round's pay ("Taxi fare").</summary>
        public abstract string RewardReason { get; }

        /// <summary>
        /// Builds the next round from the city around the player (<c>ctx.World</c>), filling <see cref="Steps"/> and
        /// <see cref="Actors"/>. Returns false when no round is possible now.
        /// </summary>
        public abstract bool BuildRound(MissionContext ctx, PlaceFinder places, Rng rng);

        /// <summary>Pay for the round just completed (reads the steps' outputs).</summary>
        public abstract long RoundPay(MissionContext ctx);

        /// <summary>The point with the terrain height under it.</summary>
        protected static Vec3 Ground(GameWorld world, Vec3 p)
        {
            return new Vec3(p.X, world.City.GroundHeight(p.X, p.Z), p.Z);
        }

        /// <summary>Road route length between two points, else 1.4 × the straight-line distance.</summary>
        protected static float RouteLength(GameWorld world, Vec3 from, Vec3 to)
        {
            var route = new Route();
            return world.Roads.FindRoute(from, to, route) == RouteStatus.Ok ? route.Length : Vec3.DistanceXZ(from, to) * 1.4f;
        }

        /// <summary>Heading of the road nearest a point (0 when none within 50 m).</summary>
        protected static float RoadHeading(GameWorld world, Vec3 p)
        {
            RoadPoint rp;
            return world.Roads.TryNearestPoint(p, 50f, out rp) ? rp.HeadingDeg : 0f;
        }

        /// <summary>Spawns a civilian (seeded model) standing at a point.</summary>
        protected static Ped SpawnWalker(GameWorld world, Vec3 feet, Rng rng)
        {
            int models = world.City.CitizenModelCount;
            return world.SpawnPed(PedKind.Civilian, models > 0 ? CitizenModels.Civilian(world.City, rng.Range(0, models)) : 0, feet, rng.Range(0f, 360f));
        }

        /// <summary>Sets a step's objective from a template and returns the step.</summary>
        protected static T Obj<T>(T step, TextVars vars, string template) where T : MissionStep
        {
            step.Objective = vars.Rich(template);
            return step;
        }

        /// <summary>Creates the job object for a kind.</summary>
        public static SideJob Create(SideJobKind kind, int level)
        {
            switch (kind)
            {
                case SideJobKind.Taxi: return new TaxiJob(level);
                case SideJobKind.Paramedic: return new ParamedicJob(level);
                case SideJobKind.Firefighter: return new FirefighterJob(level);
                case SideJobKind.Vigilante: return new VigilanteJob(level);
                case SideJobKind.Delivery: return new DeliveryJob(level);
                default: return new StreetRaceJob(level);
            }
        }
    }

    /// <summary>
    /// Taxi. A fare is a snapshot citizen within 150 m of the player (XZ) that is not promoted, chosen with the
    /// seeded Rng among the five nearest; it is promoted with <c>PromoteCitizen(entry, PromotedCitizen, playerTag)</c>
    /// (TakeCitizen once), its brain cleared, and put in slot 0. With no snapshot citizen, a Civilian ped (seeded model) is
    /// spawned at <c>RoadPointAt(player, 80)</c>. Destination: <c>InBand(Residential/Commercial/Office, fare position,
    /// 300, 1500)</c>. Steps: PickUp(0) · Timed(Deliver(Passenger 0 → destination door, 8 m), TimeLimit(level, route
    /// length)). Pay: <see cref="SideJobTable.TaxiFare"/>(level, pick-up→drop distance, timer seconds left / limit).
    /// </summary>
    public sealed class TaxiJob : SideJob
    {
        /// <summary>Creates the job.</summary>
        public TaxiJob(int level) : base(SideJobKind.Taxi, level) { }

        /// <inheritdoc />
        public override string RewardReason { get { return SideJobText.TaxiReason; } }

        /// <inheritdoc />
        public override bool BuildRound(MissionContext ctx, PlaceFinder places, Rng rng)
        {
            GameWorld world = ctx.World;
            Vec3 at = ctx.PlayerPosition;
            Ped fare = PromoteFare(world, at, rng);
            if (fare == null)
            {
                fare = SpawnWalker(world, Ground(world, places.RoadPointAt(at, 80f, rng)), rng);
            }
            fare.Brain = null;
            fare.Flags |= PedFlags.Persistent;
            ctx.ActorIds[0] = fare.Id;
            _pickup = fare.Position;
            CampaignPlace dest = places.InBand(new[] { ServiceKind.Residential, ServiceKind.Commercial, ServiceKind.Office },
                _pickup, SideJobTable.TaxiMinDistance, SideJobTable.TaxiMaxDistance, rng);
            TextVars v = new TextVars().SetPlace(dest, false);
            _timed = new TimedStep(Obj(new DeliverStep(dest.Door, 8f, DeliverWhat.Passenger, 0), v, SideJobText.TaxiDeliver),
                SideJobTable.TimeLimit(Level, RouteLength(world, _pickup, dest.Door)));
            Actors = new MissionActorSpec[0];
            Steps = new MissionStep[] { Obj(new PickUpStep(0), v, SideJobText.TaxiPickUp), _timed };
            return true;
        }

        private Vec3 _pickup;
        private TimedStep _timed;

        private readonly AmbientCitizen[] _fares = new AmbientCitizen[5];

        private Ped PromoteFare(GameWorld world, Vec3 at, Rng rng)
        {
            int n = world.NearestAmbientCitizens(at, 150f, true, _fares);
            if (n == 0) return null;
            return world.PromoteCitizen(_fares[rng.Range(0, n)], PedOrigin.PromotedCitizen, world.Player.Tag);
        }

        /// <inheritdoc />
        public override long RoundPay(MissionContext ctx)
        {
            var drop = (DeliverStep)_timed.Inner;
            float left = _timed.Seconds > 0f ? Math.Max(0f, _timed.SecondsLeft) / _timed.Seconds : 0f;
            return SideJobTable.TaxiFare(Level, Vec3.DistanceXZ(_pickup, drop.Target), left);
        }
    }

    /// <summary>
    /// Paramedic. Patients: <c>ICity.Patients(player, 2000)</c> first (nearest first, skipping citizens already used
    /// this shift), else a spawned Civilian at <c>InBand(Residential, player, 300, 1200)</c>'s door. A round has
    /// <see cref="SideJobTable.ParamedicPatients"/>[level] patients; for each: PickUp(patient slot) ·
    /// Timed(Deliver(Passenger → <c>Hospital(patient door)</c> door, 8 m), TimeLimit(level, route length)).
    /// Patient peds carry Citizen = the CS1 citizen. Pay: BasePay × patients delivered.
    /// </summary>
    public sealed class ParamedicJob : SideJob
    {
        /// <summary>Creates the job.</summary>
        public ParamedicJob(int level) : base(SideJobKind.Paramedic, level) { }

        /// <inheritdoc />
        public override string RewardReason { get { return SideJobText.ParamedicReason; } }

        /// <inheritdoc />
        public override bool BuildRound(MissionContext ctx, PlaceFinder places, Rng rng)
        {
            GameWorld world = ctx.World;
            Vec3 at = ctx.PlayerPosition;
            var patients = new List<Patient>();
            world.City.Patients(at, 2000f, patients);
            int count = SideJobTable.ParamedicPatients[Level - 1];
            var actors = new List<MissionActorSpec>();
            var steps = new List<MissionStep>();
            int p = 0;
            for (int i = 0; i < count; i++)
            {
                while (p < patients.Count && (_used.Contains(patients[p].Citizen) || world.FindPedByCitizen(patients[p].Citizen) != null)) p++;
                Vec3 door;
                uint citizen = 0;
                CampaignPlace pickup;
                if (p < patients.Count)
                {
                    citizen = patients[p].Citizen;
                    _used.Add(citizen);
                    door = patients[p].Pickup;
                    pickup = places.Describe(patients[p].Building) ?? places.RoadPlace(door);
                    p++;
                }
                else
                {
                    pickup = places.InBand(new[] { ServiceKind.Residential }, at, 300f, 1200f, rng);
                    door = pickup.Door;
                }
                CampaignPlace hospital = places.Hospital(door);
                TextVars v = new TextVars().SetPlace(pickup, false).SetPlace(hospital, true);
                actors.Add(new MissionActorSpec { Kind = ActorKind.Ped, Citizen = citizen, Position = Ground(world, door), SpawnAtStep = 2 * i, Seed = rng.NextUInt() });
                steps.Add(Obj(new PickUpStep(i), v, SideJobText.ParamedicPickUp));
                steps.Add(new TimedStep(Obj(new DeliverStep(hospital.Door, 8f, DeliverWhat.Passenger, i), v, SideJobText.ParamedicDeliver),
                    SideJobTable.TimeLimit(Level, RouteLength(world, door, hospital.Door))));
                at = hospital.Door;
            }
            Actors = actors.ToArray();
            Steps = steps.ToArray();
            return true;
        }

        private readonly HashSet<uint> _used = new HashSet<uint>();

        /// <inheritdoc />
        public override long RoundPay(MissionContext ctx)
        {
            return SideJobTable.Pay(Kind, Level) * Actors.Length;
        }
    }

    /// <summary>
    /// Firefighter. Fires: <c>ICity.BurningBuildings(player, 2000)</c> first, else a reported fire (the building
    /// nearest a seeded point 300-1000 m away). A round has <see cref="SideJobTable.FirefighterFires"/>[level] fires; for
    /// each: GoTo(door, 20 m, VehicleOfKind FireTruck) · HoseFire(building) (DouseFire effects only for burning ones).
    /// Pay: BasePay × fires.
    /// </summary>
    public sealed class FirefighterJob : SideJob
    {
        /// <summary>Creates the job.</summary>
        public FirefighterJob(int level) : base(SideJobKind.Firefighter, level) { }

        /// <inheritdoc />
        public override string RewardReason { get { return SideJobText.FirefighterReason; } }

        /// <inheritdoc />
        public override bool BuildRound(MissionContext ctx, PlaceFinder places, Rng rng)
        {
            GameWorld world = ctx.World;
            Vec3 at = ctx.PlayerPosition;
            var burning = new List<ushort>();
            world.City.BurningBuildings(at, 2000f, burning);
            int count = SideJobTable.FirefighterFires[Level - 1];
            var steps = new List<MissionStep>();
            int b = 0;
            for (int i = 0; i < count; i++)
            {
                while (b < burning.Count && _used.Contains(burning[b])) b++;
                CampaignPlace fire = null;
                bool real = false;
                if (b < burning.Count)
                {
                    fire = places.Describe(burning[b]);
                    _used.Add(burning[b]);
                    b++;
                    real = fire != null;
                }
                if (fire == null) fire = ReportedFire(world, places, at, rng);
                TextVars v = new TextVars().SetPlace(fire, false);
                steps.Add(Obj(new GoToStep(fire.Door, 20f, TravelMode.VehicleOfKind, VehicleKind.FireTruck, -1), v, SideJobText.FireGoTo));
                steps.Add(Obj(new HoseFireStep(real ? fire.Building : (ushort)0, fire.Door), v, SideJobText.FireHose));
                at = fire.Door;
            }
            Actors = new MissionActorSpec[0];
            Steps = steps.ToArray();
            _fires = count;
            return true;
        }

        private readonly HashSet<ushort> _used = new HashSet<ushort>();
        private int _fires;

        /// <summary>The building nearest a seeded point 300-1000 m away (a "reported fire"), else a road place.</summary>
        internal static CampaignPlace ReportedFire(GameWorld world, PlaceFinder places, Vec3 at, Rng rng)
        {
            double bearing = rng.Range(0f, 360f) * MathUtil.Deg2Rad;
            float d = rng.Range(300f, 1000f);
            var point = new Vec3(at.X + (float)Math.Sin(bearing) * d, 0f, at.Z + (float)Math.Cos(bearing) * d);
            var ids = new List<ushort>();
            world.City.BuildingsNear(point, 2000f, ids);
            for (int i = 0; i < ids.Count; i++)
            {
                CampaignPlace p = places.Describe(ids[i]);
                if (p != null) return p;
            }
            return places.RoadPlace(point);
        }

        /// <inheritdoc />
        public override long RoundPay(MissionContext ctx)
        {
            return SideJobTable.Pay(Kind, Level) * _fires;
        }
    }

    /// <summary>
    /// Vigilante. Slot 0 = criminal car (VehicleKind.Car, Flee, SpeedFactor = VigilanteSpeedFactor[level]) at
    /// <c>RoadPointAt(player, 300)</c>, slot 1 its driver; from level 4 slot 2/3 = a second car and driver 25 m behind;
    /// from level 7 slot 4 = a hostile Pistol passenger in seat 1 of the first car. Steps: Chase(0, second car 2 when
    /// present). Pay: BasePay × cars stopped.
    /// </summary>
    public sealed class VigilanteJob : SideJob
    {
        /// <summary>Creates the job.</summary>
        public VigilanteJob(int level) : base(SideJobKind.Vigilante, level) { }

        /// <inheritdoc />
        public override string RewardReason { get { return SideJobText.VigilanteReason; } }

        /// <inheritdoc />
        public override bool BuildRound(MissionContext ctx, PlaceFinder places, Rng rng)
        {
            GameWorld world = ctx.World;
            Vec3 start = Ground(world, places.RoadPointAt(ctx.PlayerPosition, SideJobTable.VigilanteStartDistance, rng));
            float yaw = RoadHeading(world, start);
            float speed = SideJobTable.VigilanteSpeedFactor[Level - 1];
            var actors = new List<MissionActorSpec>
            {
                new MissionActorSpec { Kind = ActorKind.Car, VehicleKind = VehicleKind.Car, Driving = ActorDriving.Flee, SpeedFactor = speed, Position = start, YawDeg = yaw, Seed = rng.NextUInt() },
                new MissionActorSpec { Kind = ActorKind.Ped, InCarSlot = 0, Seat = 0, Position = start, Seed = rng.NextUInt() },
            };
            var chase = new ChaseStep(0);
            if (Level >= SideJobTable.VigilanteSecondCarLevel)
            {
                Vec3 behind = Ground(world, start - Quat.Yaw(yaw) * new Vec3(0f, 0f, 25f));
                actors.Add(new MissionActorSpec { Kind = ActorKind.Car, VehicleKind = VehicleKind.Car, Driving = ActorDriving.Flee, SpeedFactor = speed, Position = behind, YawDeg = yaw, Seed = rng.NextUInt() });
                actors.Add(new MissionActorSpec { Kind = ActorKind.Ped, InCarSlot = 2, Seat = 0, Position = behind, Seed = rng.NextUInt() });
                chase.SecondCarSlot = 2;
            }
            if (Level >= SideJobTable.VigilanteArmedLevel)
                actors.Add(new MissionActorSpec { Kind = ActorKind.Ped, InCarSlot = 0, Seat = 1, Hostile = true, Weapon = WeaponId.Pistol, Position = start, Seed = rng.NextUInt() });
            Actors = actors.ToArray();
            _chase = chase;
            Steps = new MissionStep[] { Obj(chase, new TextVars(), chase.SecondCarSlot >= 0 ? SideJobText.VigilanteChaseTwo : SideJobText.VigilanteChase) };
            return true;
        }

        private ChaseStep _chase;

        /// <inheritdoc />
        public override long RoundPay(MissionContext ctx)
        {
            return SideJobTable.Pay(Kind, Level) * Math.Max(1, _chase.Stopped);
        }
    }

    /// <summary>
    /// Delivery. Pick-up: <c>Industrial(player, 0)</c>; drop: <c>InBand(Commercial, pick-up door, 400, 2000)</c>.
    /// Steps: GoTo(pick-up door, 10 m, VehicleOfKind Van or Truck: the job's vehicle) · Wait(3 s, "Loading…") ·
    /// Timed(Deliver(CurrentCar → drop door, 10 m), TimeLimit(level, route length)). Pay:
    /// <see cref="SideJobTable.DeliveryPay"/>(level, the Deliver step's CarHealthAtDelivery).
    /// </summary>
    public sealed class DeliveryJob : SideJob
    {
        /// <summary>Creates the job.</summary>
        public DeliveryJob(int level) : base(SideJobKind.Delivery, level) { }

        /// <inheritdoc />
        public override string RewardReason { get { return SideJobText.DeliveryReason; } }

        /// <inheritdoc />
        public override bool BuildRound(MissionContext ctx, PlaceFinder places, Rng rng)
        {
            GameWorld world = ctx.World;
            Car car = ctx.PlayerCar;
            VehicleKind kind = car != null ? car.Model.Kind : VehicleKind.Van;
            CampaignPlace pickup = places.Industrial(ctx.PlayerPosition, 0f);
            CampaignPlace drop = places.InBand(new[] { ServiceKind.Commercial }, pickup.Door, 400f, 2000f, rng);
            TextVars v = new TextVars().SetPlace(pickup, false).SetPlace(drop, true);
            _drop = Obj(new DeliverStep(drop.Door, 10f, DeliverWhat.CurrentCar, -1), v, SideJobText.DeliveryDeliver);
            Actors = new MissionActorSpec[0];
            Steps = new MissionStep[]
            {
                Obj(new GoToStep(pickup.Door, 10f, TravelMode.VehicleOfKind, kind, -1), v, SideJobText.DeliveryPickUp),
                new WaitStep(3f) { StatusLines = new[] { SideJobText.DeliveryLoading } },
                new TimedStep(_drop, SideJobTable.TimeLimit(Level, RouteLength(world, pickup.Door, drop.Door))),
            };
            return true;
        }

        private DeliverStep _drop;

        /// <inheritdoc />
        public override long RoundPay(MissionContext ctx)
        {
            return SideJobTable.DeliveryPay(Level, _drop.CarHealthAtDelivery);
        }
    }

    /// <summary>
    /// Street race. Started by driving into the race marker (or from the menus while within 30 m of it). Checkpoints:
    /// <see cref="SideJobTable.RaceCheckpoints"/>[level] points along chained road routes: from the marker,
    /// <c>FindRoute</c> to a seeded <c>RoadPointAt(current end, 500)</c> target, repeated until the chained length
    /// reaches 250 m × checkpoint count; checkpoints are placed every 250 m along the chain (the last at its end). Slots
    /// 0-2: rival cars (VehicleKind.Car, Driverless <see cref="RivalDriver"/> at RaceRivalSpeedFactor[level]) lined up
    /// 8 m apart behind the marker; Steps: Wait(3 s countdown, StatusLine "3", "2", "1") · Race. Pay:
    /// <see cref="SideJobTable.RacePrize"/>(level, FinishPosition). A race round is one race; the shift ends after it.
    /// </summary>
    public sealed class StreetRaceJob : SideJob
    {
        /// <summary>Creates the job.</summary>
        public StreetRaceJob(int level) : base(SideJobKind.StreetRace, level) { }

        /// <inheritdoc />
        public override string RewardReason { get { return SideJobText.RaceReason; } }

        /// <inheritdoc />
        public override bool BuildRound(MissionContext ctx, PlaceFinder places, Rng rng)
        {
            GameWorld world = ctx.World;
            Vec3 start = places.NearestRoadPoint(ctx.PlayerPosition);
            int count = SideJobTable.RaceCheckpoints[Level - 1];
            var checkpoints = new List<Vec3>(count);
            var route = new Route();
            Vec3 cur = start;
            float carry = 0f;
            float heading = float.NaN;
            for (int leg = 0; leg < 30 && checkpoints.Count < count; leg++)
            {
                Vec3 target = places.RoadPointAt(cur, 500f, rng);
                if (world.Roads.FindRoute(cur, target, route) != RouteStatus.Ok || route.Length < 1f) continue;
                if (float.IsNaN(heading)) heading = route[0].HeadingDeg;
                float d = Spacing - carry;
                while (d <= route.Length && checkpoints.Count < count)
                {
                    checkpoints.Add(route.Sample(d).Position);
                    d += Spacing;
                }
                carry = route.Length - (d - Spacing);
                cur = route.End;
            }
            if (checkpoints.Count == 0) return false;
            Vec3[] cps = checkpoints.ToArray();
            if (float.IsNaN(heading)) heading = 0f;
            float speed = SideJobTable.RaceRivalSpeedFactor[Level - 1];
            int model = world.City.FindVehicleModel(VehicleKind.Car, rng.NextUInt());
            var slots = new List<int>();
            for (int i = 0; i < 3 && model >= 0; i++)
            {
                Vec3 grid = Ground(world, start - Quat.Yaw(heading) * new Vec3(i % 2 == 0 ? -2f : 2f, 0f, 8f * (i + 1)));
                Car rival = world.SpawnCar(model, grid, heading, CarOwner.Mission, RivalColours[i]);
                if (rival == null) continue;
                rival.Driver = new RivalDriver(cps, speed) { StartGameSeconds = world.Clock.GameSeconds + 3.0 };
                ctx.ActorIds[i] = rival.Id;
                slots.Add(i);
            }
            _race = new RaceStep(cps, slots.ToArray());
            Obj(_race, new TextVars(), SideJobText.Race);
            Actors = new MissionActorSpec[0];
            Steps = new MissionStep[] { new WaitStep(3f) { StatusLines = SideJobText.Countdown, Objective = RichText.Parse(SideJobText.Race) }, _race };
            return true;
        }

        private const float Spacing = 250f;
        private static readonly int[] RivalColours = { 0xF2C200, 0x1E90FF, 0x2EB82E };
        private RaceStep _race;

        /// <inheritdoc />
        public override long RoundPay(MissionContext ctx)
        {
            return SideJobTable.RacePrize(Level, _race.FinishPosition);
        }
    }

    /// <summary>Authored side-job text.</summary>
    public static class SideJobText
    {
        /// <summary>Wallet reason.</summary>
        public const string TaxiReason = "Taxi fare";
        /// <summary>Wallet reason.</summary>
        public const string ParamedicReason = "Paramedic";
        /// <summary>Wallet reason.</summary>
        public const string FirefighterReason = "Firefighter";
        /// <summary>Wallet reason.</summary>
        public const string VigilanteReason = "Vigilante";
        /// <summary>Wallet reason.</summary>
        public const string DeliveryReason = "Delivery";
        /// <summary>Wallet reason.</summary>
        public const string RaceReason = "Street race";

        /// <summary>Taxi pick-up objective.</summary>
        public const string TaxiPickUp = "Pick up the ~b~fare~s~.";
        /// <summary>Taxi drop objective ({place}, {street}).</summary>
        public const string TaxiDeliver = "Take the ~b~fare~s~ to ~y~{place}~s~ on {street}.";
        /// <summary>Paramedic pick-up objective.</summary>
        public const string ParamedicPickUp = "Pick up the ~b~patient~s~ at ~y~{place}~s~.";
        /// <summary>Paramedic drop objective.</summary>
        public const string ParamedicDeliver = "Rush the ~b~patient~s~ to ~y~{place2}~s~.";
        /// <summary>Firefighter drive objective.</summary>
        public const string FireGoTo = "Fire reported at ~y~{place}~s~ on {street}.";
        /// <summary>Firefighter hose objective.</summary>
        public const string FireHose = "Put out the ~r~fire~s~.";
        /// <summary>Vigilante objective.</summary>
        public const string VigilanteChase = "Take down the ~r~suspect~s~.";
        /// <summary>Vigilante with two cars.</summary>
        public const string VigilanteChaseTwo = "Take down both ~r~suspect cars~s~.";
        /// <summary>Delivery pick-up.</summary>
        public const string DeliveryPickUp = "Collect the cargo at ~y~{place}~s~.";
        /// <summary>Delivery loading status.</summary>
        public const string DeliveryLoading = "Loading...";
        /// <summary>Delivery drop.</summary>
        public const string DeliveryDeliver = "Deliver the cargo to ~y~{place2}~s~ on {street2}. Mind the bumps.";
        /// <summary>Race objective.</summary>
        public const string Race = "Win the ~y~race~s~.";
        /// <summary>Race countdown status lines (index 0 = "3").</summary>
        public static readonly string[] Countdown = { "3", "2", "1", "GO!" };
        /// <summary>Race marker help when not in a car.</summary>
        public const string RaceNeedsCar = "Street races need a car.";
        /// <summary>No round available yet.</summary>
        public const string Waiting = "Waiting for the next call...";
        /// <summary>Help when started in the wrong vehicle ({0} is not used; one string per kind below).</summary>
        public static readonly string[] WrongVehicle =
        {
            "You need a taxi for this job.",
            "You need an ambulance for this job.",
            "You need a fire engine for this job.",
            "You need a police car for this job.",
            "You need a van or a truck for this job.",
            "Drive into the race marker to start a street race.",
        };
        /// <summary>Help when a mission is running.</summary>
        public const string MissionRunning = "Finish your mission first.";
        /// <summary>Level-up notification per level 2-10 (index = level − 2).</summary>
        public static readonly string[] LevelUp =
        {
            "Level 2 reached.", "Level 3 reached.", "Level 4 reached.", "Level 5 reached.", "Level 6 reached.",
            "Level 7 reached.", "Level 8 reached.", "Level 9 reached.", "Level 10 reached. Top of the game.",
        };
    }
}
