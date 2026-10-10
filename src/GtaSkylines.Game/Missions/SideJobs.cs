using System;
using GtaSkylines.Game.Mathx;

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
    /// Taxi. A fare is the nearest snapshot citizen 25-150 m from the player (XZ) that is not promoted, chosen with the
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
        public override bool BuildRound(MissionContext ctx, PlaceFinder places, Rng rng) { throw new NotImplementedException(); }

        /// <inheritdoc />
        public override long RoundPay(MissionContext ctx) { throw new NotImplementedException(); }
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
        public override bool BuildRound(MissionContext ctx, PlaceFinder places, Rng rng) { throw new NotImplementedException(); }

        /// <inheritdoc />
        public override long RoundPay(MissionContext ctx) { throw new NotImplementedException(); }
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
        public override bool BuildRound(MissionContext ctx, PlaceFinder places, Rng rng) { throw new NotImplementedException(); }

        /// <inheritdoc />
        public override long RoundPay(MissionContext ctx) { throw new NotImplementedException(); }
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
        public override bool BuildRound(MissionContext ctx, PlaceFinder places, Rng rng) { throw new NotImplementedException(); }

        /// <inheritdoc />
        public override long RoundPay(MissionContext ctx) { throw new NotImplementedException(); }
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
        public override bool BuildRound(MissionContext ctx, PlaceFinder places, Rng rng) { throw new NotImplementedException(); }

        /// <inheritdoc />
        public override long RoundPay(MissionContext ctx) { throw new NotImplementedException(); }
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
        public override bool BuildRound(MissionContext ctx, PlaceFinder places, Rng rng) { throw new NotImplementedException(); }

        /// <inheritdoc />
        public override long RoundPay(MissionContext ctx) { throw new NotImplementedException(); }
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
