using System;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Missions
{
    /// <summary>Step type tag (for tests and the debug overlay).</summary>
    public enum StepKind
    {
        /// <summary><see cref="GoToStep"/>.</summary>
        GoTo,
        /// <summary><see cref="EnterVehicleStep"/>.</summary>
        EnterVehicle,
        /// <summary><see cref="StealVehicleStep"/>.</summary>
        StealVehicle,
        /// <summary><see cref="DeliverStep"/>.</summary>
        Deliver,
        /// <summary><see cref="ChaseStep"/>.</summary>
        Chase,
        /// <summary><see cref="EliminateStep"/>.</summary>
        Eliminate,
        /// <summary><see cref="LoseWantedStep"/>.</summary>
        LoseWanted,
        /// <summary><see cref="CollectStep"/>.</summary>
        Collect,
        /// <summary><see cref="TimedStep"/>.</summary>
        Timed,
        /// <summary><see cref="WaitStep"/>.</summary>
        Wait,
        /// <summary><see cref="TalkStep"/>.</summary>
        Talk,
        /// <summary><see cref="ResprayStep"/>.</summary>
        Respray,
        /// <summary><see cref="BuyItemStep"/>.</summary>
        BuyItem,
        /// <summary><see cref="PickUpStep"/>.</summary>
        PickUp,
        /// <summary><see cref="HoseFireStep"/>.</summary>
        HoseFire,
        /// <summary><see cref="SetWantedStep"/>.</summary>
        SetWanted,
        /// <summary><see cref="RaceStep"/>.</summary>
        Race,
    }

    /// <summary>How the player must travel for a <see cref="GoToStep"/>.</summary>
    public enum TravelMode
    {
        /// <summary>On foot or in any car.</summary>
        Any,
        /// <summary>On foot only (not InVehicle).</summary>
        OnFoot,
        /// <summary>Driving any car.</summary>
        AnyVehicle,
        /// <summary>Driving a car whose model kind is <see cref="GoToStep.VehicleKind"/>.</summary>
        VehicleOfKind,
        /// <summary>Driving the car in <see cref="GoToStep.VehicleSlot"/>.</summary>
        MissionVehicle,
    }

    /// <summary>What a <see cref="DeliverStep"/> brings.</summary>
    public enum DeliverWhat
    {
        /// <summary>Whatever car the player is driving.</summary>
        CurrentCar,
        /// <summary>The car in the step's slot, driven by the player.</summary>
        MissionVehicle,
        /// <summary>The ped in the step's slot, seated in the player's car.</summary>
        Passenger,
        /// <summary>The player themself (carrying an item), on foot or in any car.</summary>
        Player,
    }

    /// <summary>
    /// One objective of a mission or side-job round. Parameters are fixed at construction; runtime fields are reset by
    /// <see cref="Begin"/>, so a step object is reused when a mission is retried. Step paths allocate nothing.
    /// <para>
    /// Common behaviour (the base <see cref="Begin"/>, which overrides call first): writes <see cref="Objective"/> to
    /// <c>world.Mission</c> (<c>RichText.WriteObjective</c>; null keeps the current objective), shows
    /// <see cref="Help"/> (priority 2, 8 s) when set, adds the blip (<see cref="BlipKind"/> at <see cref="BlipPosition"/>,
    /// or following the actor in <see cref="BlipSlot"/> when ≥ 0; id from <c>MissionContext.NextBlipId++</c>), a yellow
    /// world marker of <see cref="MarkerRadius"/> at the blip position when that radius is &gt; 0, and the GPS target
    /// (<c>Mission.HasGpsTarget</c>/<c>GpsTarget</c>) at the blip when <see cref="Gps"/>. The base <see cref="End"/>
    /// removes exactly the blips and markers this step added and clears the timer and status line.
    /// </para>
    /// <para>
    /// Steps that need the player in a vehicle (GoTo with a vehicle mode, Deliver) show
    /// <see cref="CampaignText.GetBackInCar"/> with a Friendly blip on the vehicle while the player is not in it, and
    /// restore their own objective and blip when the player is back.
    /// </para>
    /// </summary>
    public abstract class MissionStep
    {
        /// <summary>Objective text, or null to keep the previous one; <see cref="RichText.Empty"/> clears it.</summary>
        public RichText Objective;
        /// <summary>Help text shown when the step begins (tutorial), or null.</summary>
        public string Help;
        /// <summary>True when the step shows a blip.</summary>
        public bool HasBlip;
        /// <summary>Blip kind.</summary>
        public BlipKind BlipKind = BlipKind.Destination;
        /// <summary>Blip position (ignored while <see cref="BlipSlot"/> names a live actor).</summary>
        public Vec3 BlipPosition;
        /// <summary>Actor slot the blip follows, −1 none.</summary>
        public int BlipSlot = -1;
        /// <summary>True to route the GPS to the blip.</summary>
        public bool Gps = true;
        /// <summary>Radius of the in-world marker at the blip, 0 for none.</summary>
        public float MarkerRadius;

        /// <summary>Type tag.</summary>
        public abstract StepKind Kind { get; }

        /// <summary>Starts the step (see the class for the common part).</summary>
        public virtual void Begin(MissionContext ctx)
        {
            throw new NotImplementedException();
        }

        /// <summary>Advances the step by one fixed step.</summary>
        public abstract StepState Step(MissionContext ctx, float dt);

        /// <summary>Ends the step (pass, fail or abort): removes what <see cref="Begin"/> added.</summary>
        public virtual void End(MissionContext ctx)
        {
            throw new NotImplementedException();
        }

        /// <summary>Sets the blip and returns this (generator convenience).</summary>
        public MissionStep WithBlip(BlipKind kind, Vec3 position, float markerRadius)
        {
            HasBlip = true;
            BlipKind = kind;
            BlipPosition = position;
            MarkerRadius = markerRadius;
            return this;
        }

        /// <summary>Sets a blip that follows an actor slot and returns this.</summary>
        public MissionStep WithActorBlip(BlipKind kind, int slot)
        {
            HasBlip = true;
            BlipKind = kind;
            BlipSlot = slot;
            return this;
        }
    }

    /// <summary>
    /// Reach a point. Done when the player's position (feet on foot, the car's position when driving) is within
    /// <see cref="Radius"/> (XZ) of <see cref="Target"/>, the travel mode is satisfied and the speed (car speed when in
    /// a car, motor speed on foot) is ≤ <see cref="MaxSpeed"/>.
    /// </summary>
    public sealed class GoToStep : MissionStep
    {
        /// <summary>Destination.</summary>
        public readonly Vec3 Target;
        /// <summary>Arrival radius, metres.</summary>
        public readonly float Radius;
        /// <summary>Travel mode.</summary>
        public readonly TravelMode Mode;
        /// <summary>Kind for <see cref="TravelMode.VehicleOfKind"/>.</summary>
        public readonly VehicleKind VehicleKind;
        /// <summary>Slot for <see cref="TravelMode.MissionVehicle"/>.</summary>
        public readonly int VehicleSlot;
        /// <summary>Speed limit to count as arrived, m/s (float.MaxValue = none).</summary>
        public float MaxSpeed = float.MaxValue;

        /// <summary>Creates the step; blip Destination at the target with a marker of the radius.</summary>
        public GoToStep(Vec3 target, float radius, TravelMode mode, VehicleKind vehicleKind, int vehicleSlot)
        {
            Target = target;
            Radius = radius;
            Mode = mode;
            VehicleKind = vehicleKind;
            VehicleSlot = vehicleSlot;
            WithBlip(BlipKind.Destination, target, radius);
        }

        /// <inheritdoc />
        public override StepKind Kind { get { return StepKind.GoTo; } }

        /// <inheritdoc />
        public override StepState Step(MissionContext ctx, float dt) { throw new NotImplementedException(); }
    }

    /// <summary>
    /// Get in a vehicle. Done when the player sits in seat 0 of the car in <see cref="VehicleSlot"/> (when ≥ 0) or of
    /// any car whose model kind is one of <see cref="Kinds"/> (when the slot is −1). With a slot, the blip follows the
    /// car (Friendly). Fails with <see cref="FailReasons.VehicleDestroyed"/> when the slot's car is destroyed.
    /// </summary>
    public sealed class EnterVehicleStep : MissionStep
    {
        /// <summary>Mission car slot, or −1.</summary>
        public readonly int VehicleSlot;
        /// <summary>Accepted kinds when <see cref="VehicleSlot"/> is −1.</summary>
        public readonly VehicleKind[] Kinds;
        /// <summary>When ≥ 0, the car the player entered is stored in this slot (not removed at mission end unless it is a spawned actor).</summary>
        public int StoreSlot = -1;

        /// <summary>Enter the mission car in a slot.</summary>
        public EnterVehicleStep(int vehicleSlot)
        {
            VehicleSlot = vehicleSlot;
            Kinds = new VehicleKind[0];
            WithActorBlip(BlipKind.Friendly, vehicleSlot);
        }

        /// <summary>Enter any car of these kinds.</summary>
        public EnterVehicleStep(params VehicleKind[] kinds)
        {
            VehicleSlot = -1;
            Kinds = kinds ?? new VehicleKind[0];
        }

        /// <inheritdoc />
        public override StepKind Kind { get { return StepKind.EnterVehicle; } }

        /// <inheritdoc />
        public override StepState Step(MissionContext ctx, float dt) { throw new NotImplementedException(); }
    }

    /// <summary>
    /// Steal a car. Done when, after the step began, a <see cref="GameEventType.CarStolen"/> or
    /// <see cref="GameEventType.CarJacked"/> event has A = the player and B = an accepted car, and the player then sits
    /// in its seat 0. Accepted: the car in <see cref="VehicleSlot"/> when ≥ 0; else any car whose kind is in
    /// <see cref="Kinds"/> (empty = any kind), parked only when <see cref="ParkedOnly"/> (CarStolen events only).
    /// <para>
    /// <see cref="GuaranteedWitness"/>: when the car is taken, a Civilian ped is spawned 8-12 m from the car on the
    /// side away from the player (seeded), facing it, with the OnPhone flag and Phone anim (no brain); after
    /// <see cref="WitnessCallSeconds"/>, unless that ped is Dead or KnockedDown, <c>Wanted.Stars</c> becomes at least
    /// <see cref="WitnessStars"/> with the last known position at the player; the witness is then given
    /// <c>Hooks.Brains.Create(…, PromotedCitizen, playerTag, default)</c>. The step is Done at the steal; the witness
    /// runs on in the background until it calls or goes down.
    /// </para>
    /// </summary>
    public sealed class StealVehicleStep : MissionStep
    {
        /// <summary>Target car slot, or −1 for any accepted car.</summary>
        public readonly int VehicleSlot;
        /// <summary>Accepted kinds when no slot (empty = any).</summary>
        public readonly VehicleKind[] Kinds;
        /// <summary>Only parked cars count.</summary>
        public bool ParkedOnly;
        /// <summary>Slot that receives the stolen car's id (−1 none).</summary>
        public int StoreSlot = -1;
        /// <summary>Spawn a witness who calls the police.</summary>
        public bool GuaranteedWitness;
        /// <summary>Seconds the witness's call takes.</summary>
        public float WitnessCallSeconds = 6f;
        /// <summary>Stars after the call.</summary>
        public int WitnessStars = 1;

        /// <summary>Steal the car in a slot (blip Target on it).</summary>
        public StealVehicleStep(int vehicleSlot)
        {
            VehicleSlot = vehicleSlot;
            Kinds = new VehicleKind[0];
            WithActorBlip(BlipKind.Target, vehicleSlot);
        }

        /// <summary>Steal any car of these kinds (none = any kind).</summary>
        public StealVehicleStep(params VehicleKind[] kinds)
        {
            VehicleSlot = -1;
            Kinds = kinds ?? new VehicleKind[0];
        }

        /// <inheritdoc />
        public override StepKind Kind { get { return StepKind.StealVehicle; } }

        /// <inheritdoc />
        public override StepState Step(MissionContext ctx, float dt) { throw new NotImplementedException(); }
    }

    /// <summary>
    /// Bring a car or a passenger to a point. Done when the delivered thing is within <see cref="Radius"/> (XZ) of
    /// <see cref="Target"/> with the player's car at ≤ <see cref="MaxSpeed"/>:
    /// <list type="bullet">
    /// <item>CurrentCar / MissionVehicle: the player drives it. On arrival, when <see cref="MinHealth"/> &gt; 0 and the
    /// car's <c>Body.Health</c> is below it, fails with <see cref="FailReasons.CarTooDamaged"/>.</item>
    /// <item>Passenger: the ped in <see cref="Slot"/> is in the player's car. On arrival it is taken out at the
    /// right-side door (<c>DoorPosition(car, false)</c>) and, when <see cref="RemovePassenger"/>, removed
    /// 2 s later (returnCitizen true); Done at the exit.</item>
    /// <item>Player: the player's position; no speed condition on foot.</item>
    /// </list>
    /// While <see cref="FailIfWanted"/> and <c>Wanted.Stars</c> &gt; 0, fails with <see cref="FailReasons.Spotted"/>.
    /// <see cref="CarHealthAtDelivery"/> holds the car's health at arrival (side jobs scale pay by it).
    /// </summary>
    public sealed class DeliverStep : MissionStep
    {
        /// <summary>Destination.</summary>
        public readonly Vec3 Target;
        /// <summary>Arrival radius.</summary>
        public readonly float Radius;
        /// <summary>What is delivered.</summary>
        public readonly DeliverWhat What;
        /// <summary>Car slot (MissionVehicle) or ped slot (Passenger); ignored otherwise.</summary>
        public readonly int Slot;
        /// <summary>Minimum car health 0-100 at arrival (0 = none).</summary>
        public float MinHealth;
        /// <summary>Fail as soon as the player is wanted.</summary>
        public bool FailIfWanted;
        /// <summary>Speed to count as stopped, m/s.</summary>
        public float MaxSpeed = 2f;
        /// <summary>Remove the passenger after it gets out.</summary>
        public bool RemovePassenger = true;
        /// <summary>Car health at arrival (output, valid after Done).</summary>
        public float CarHealthAtDelivery;

        /// <summary>Creates the step; blip Destination at the target with a marker of the radius.</summary>
        public DeliverStep(Vec3 target, float radius, DeliverWhat what, int slot)
        {
            Target = target;
            Radius = radius;
            What = what;
            Slot = slot;
            WithBlip(BlipKind.Destination, target, radius);
        }

        /// <inheritdoc />
        public override StepKind Kind { get { return StepKind.Deliver; } }

        /// <inheritdoc />
        public override StepState Step(MissionContext ctx, float dt) { throw new NotImplementedException(); }
    }

    /// <summary>
    /// Stop a fleeing car. The car in <see cref="CarSlot"/> has a <see cref="FleeDriver"/>; its driver is the ped in
    /// seat 0. Done when the car is destroyed, or when its driver is out of it (see below) and is Dead, KnockedDown,
    /// Arrested or HandsUp. When the car's speed stays below 1 m/s for <see cref="StoppedSeconds"/>, the step takes the
    /// driver out at the left door with brain <c>Hooks.Brains.Create(world, driver, CarjackedDriver, playerTag,
    /// default)</c> (G7: flee or fight). Fails with <see cref="FailReasons.TargetEscaped"/> when the car (or the driver
    /// once on foot) is farther than <see cref="EscapeDistance"/> from the player. Blip: Target following the car, then
    /// the driver.
    /// </summary>
    public sealed class ChaseStep : MissionStep
    {
        /// <summary>Target car slot.</summary>
        public readonly int CarSlot;
        /// <summary>Escape distance, metres.</summary>
        public float EscapeDistance = 400f;
        /// <summary>Seconds nearly stopped before the driver bails.</summary>
        public float StoppedSeconds = 3f;
        /// <summary>When ≥ 0, a second target car slot (vigilante level ≥ 4); both must be stopped.</summary>
        public int SecondCarSlot = -1;

        /// <summary>Creates the step.</summary>
        public ChaseStep(int carSlot)
        {
            CarSlot = carSlot;
            WithActorBlip(BlipKind.Target, carSlot);
            Gps = false;
        }

        /// <inheritdoc />
        public override StepKind Kind { get { return StepKind.Chase; } }

        /// <inheritdoc />
        public override StepState Step(MissionContext ctx, float dt) { throw new NotImplementedException(); }
    }

    /// <summary>
    /// Kill hostiles. Done when every ped in <see cref="Slots"/> is Dead or removed (an unspawned slot counts as
    /// done). Each live target gets a Target blip that follows it (removed when it dies). No GPS.
    /// </summary>
    public sealed class EliminateStep : MissionStep
    {
        /// <summary>Ped slots to eliminate.</summary>
        public readonly int[] Slots;

        /// <summary>Creates the step.</summary>
        public EliminateStep(params int[] slots)
        {
            Slots = slots ?? new int[0];
            Gps = false;
        }

        /// <inheritdoc />
        public override StepKind Kind { get { return StepKind.Eliminate; } }

        /// <inheritdoc />
        public override StepState Step(MissionContext ctx, float dt) { throw new NotImplementedException(); }
    }

    /// <summary>Lose the police. Done in the first step where <c>Wanted.Stars</c> is 0 (immediately when not wanted). No blip.</summary>
    public sealed class LoseWantedStep : MissionStep
    {
        /// <inheritdoc />
        public override StepKind Kind { get { return StepKind.LoseWanted; } }

        /// <inheritdoc />
        public override StepState Step(MissionContext ctx, float dt) { throw new NotImplementedException(); }
    }

    /// <summary>
    /// Take something inside an interior. Outside the building, the objective is <see cref="OutsideObjective"/> with a
    /// Destination blip at its door. Whenever the player is inside <see cref="Building"/> (on Begin and on each
    /// <see cref="GameEventType.EnteredInterior"/> with Value = the building), the step adds a custom interaction
    /// (<c>InteriorState.AddCustomInteraction(CustomId, local, Prompt)</c>) at the centre of the layout's first
    /// <c>RoomKind.LockerRoom</c> room, else its first StockRoom/Armory/Workshop room, else 3 m in front of
    /// EntranceSpawn, and shows <see cref="MissionStep.Objective"/>. Done on a
    /// <see cref="GameEventType.CustomInteraction"/> event with Value = <see cref="CustomId"/>.
    /// <para>
    /// When <see cref="SeenStars"/> &gt; 0: at the moment of taking, if any live, not knocked-down Cop ped in the same
    /// interior is within <see cref="SeenRange"/> m and a static raycast from its eye (1.6 m) to the player's eye is
    /// clear, <c>Wanted.Stars</c> becomes at least SeenStars with the last known position at the player.
    /// </para>
    /// </summary>
    public sealed class CollectStep : MissionStep
    {
        /// <summary>The building whose interior holds the item.</summary>
        public readonly ushort Building;
        /// <summary>Door of the building (outside blip).</summary>
        public readonly Vec3 Door;
        /// <summary>Custom interaction id (missions use 9000 + step index).</summary>
        public readonly int CustomId;
        /// <summary>Interaction prompt ("Press {Interact} to take the evidence").</summary>
        public readonly string Prompt;
        /// <summary>Objective while outside.</summary>
        public RichText OutsideObjective;
        /// <summary>Stars if a cop sees the taking (0 = none).</summary>
        public int SeenStars;
        /// <summary>Range of that check, metres.</summary>
        public float SeenRange = 12f;

        /// <summary>Creates the step.</summary>
        public CollectStep(ushort building, Vec3 door, int customId, string prompt)
        {
            Building = building;
            Door = door;
            CustomId = customId;
            Prompt = prompt;
            WithBlip(BlipKind.Destination, door, 0f);
        }

        /// <inheritdoc />
        public override StepKind Kind { get { return StepKind.Collect; } }

        /// <inheritdoc />
        public override StepState Step(MissionContext ctx, float dt) { throw new NotImplementedException(); }
    }

    /// <summary>
    /// Runs <see cref="Inner"/> with a time limit: Begin begins the inner step (its objective and blip) and sets
    /// <c>Mission.TimerSeconds</c> = <see cref="Seconds"/>; each step counts down by dt; inner Done → Done; inner Failed →
    /// Failed; the timer reaching 0 first → Failed with <see cref="FailReason"/>. <see cref="SecondsLeft"/> stays readable
    /// after the end (taxi tips, race bonus). End ends the inner step and sets the timer to −1.
    /// </summary>
    public sealed class TimedStep : MissionStep
    {
        /// <summary>Wrapped step.</summary>
        public readonly MissionStep Inner;
        /// <summary>Limit, game seconds.</summary>
        public float Seconds;
        /// <summary>Reason on timeout.</summary>
        public string FailReason = FailReasons.TimeOut;
        /// <summary>Seconds remaining (output).</summary>
        public float SecondsLeft;

        /// <summary>Creates the wrapper. The wrapper itself has no objective or blip (the inner one's are shown).</summary>
        /// <exception cref="ArgumentNullException">inner is null.</exception>
        public TimedStep(MissionStep inner, float seconds)
        {
            if (inner == null) throw new ArgumentNullException("inner");
            Inner = inner;
            Seconds = seconds;
        }

        /// <inheritdoc />
        public override StepKind Kind { get { return StepKind.Timed; } }

        /// <inheritdoc />
        public override StepState Step(MissionContext ctx, float dt) { throw new NotImplementedException(); }
    }

    /// <summary>Waits: Done once <c>MissionContext.StepSeconds</c> ≥ <see cref="Seconds"/>.</summary>
    public sealed class WaitStep : MissionStep
    {
        /// <summary>Duration, game seconds.</summary>
        public readonly float Seconds;

        /// <summary>Creates the step.</summary>
        public WaitStep(float seconds) { Seconds = seconds; }

        /// <inheritdoc />
        public override StepKind Kind { get { return StepKind.Wait; } }

        /// <inheritdoc />
        public override StepState Step(MissionContext ctx, float dt) { throw new NotImplementedException(); }
    }

    /// <summary>
    /// Subtitle lines: Begin queues every line (<c>HudMessages.QueueSubtitle</c>); Done once StepSeconds ≥ the sum of
    /// their seconds. The objective is cleared unless one is set.
    /// </summary>
    public sealed class TalkStep : MissionStep
    {
        /// <summary>Lines in order.</summary>
        public readonly DialogueLine[] Lines;

        /// <summary>Creates the step; Objective = <see cref="RichText.Empty"/>.</summary>
        public TalkStep(params DialogueLine[] lines)
        {
            Lines = lines ?? new DialogueLine[0];
            Objective = RichText.Empty;
        }

        /// <inheritdoc />
        public override StepKind Kind { get { return StepKind.Talk; } }

        /// <inheritdoc />
        public override StepState Step(MissionContext ctx, float dt) { throw new NotImplementedException(); }
    }

    /// <summary>
    /// Respray the car. Done on a <see cref="GameEventType.Resprayed"/> event with A = the player (G6 raises it after a
    /// respray purchase at a RespraySpot). When <see cref="Backyard"/> (the city has no Garage-archetype building), the
    /// step instead completes when the player's car stays within 6 m (XZ) of <see cref="Door"/> below 1 m/s for 3 s:
    /// it charges the respray price (<c>Shop.Buy(Wallet, Respray, "respray")</c>; a failed purchase shows
    /// <see cref="CampaignText.NeedCashForRespray"/> and keeps waiting), sets the car's colour to a seeded different
    /// colour and emits Resprayed (A player, B car, Value colour) itself.
    /// </summary>
    public sealed class ResprayStep : MissionStep
    {
        /// <summary>Respray building (0 for the backyard mode's road point).</summary>
        public readonly ushort Building;
        /// <summary>Its door.</summary>
        public readonly Vec3 Door;
        /// <summary>Backyard respray mode.</summary>
        public readonly bool Backyard;

        /// <summary>Creates the step; blip Destination at the door with a 3 m marker.</summary>
        public ResprayStep(ushort building, Vec3 door, bool backyard)
        {
            Building = building;
            Door = door;
            Backyard = backyard;
            WithBlip(BlipKind.Destination, door, 3f);
        }

        /// <inheritdoc />
        public override StepKind Kind { get { return StepKind.Respray; } }

        /// <inheritdoc />
        public override StepState Step(MissionContext ctx, float dt) { throw new NotImplementedException(); }
    }

    /// <summary>
    /// Buy a shop item. Done on a <see cref="GameEventType.Purchase"/> event whose Text equals <see cref="ItemId"/>
    /// (ordinal), made inside <see cref="Building"/> when it is not 0 (<c>world.Interior.Building.Id</c>).
    /// Blip: Shop at the door, removed while inside.
    /// </summary>
    public sealed class BuyItemStep : MissionStep
    {
        /// <summary><c>ShopItem.Id</c>.</summary>
        public readonly string ItemId;
        /// <summary>Shop building, 0 = any.</summary>
        public readonly ushort Building;

        /// <summary>Creates the step.</summary>
        public BuyItemStep(string itemId, ushort building, Vec3 door)
        {
            ItemId = itemId;
            Building = building;
            WithBlip(BlipKind.Shop, door, 0f);
        }

        /// <inheritdoc />
        public override StepKind Kind { get { return StepKind.BuyItem; } }

        /// <inheritdoc />
        public override StepState Step(MissionContext ctx, float dt) { throw new NotImplementedException(); }
    }

    /// <summary>
    /// Pick up a passenger. The ped in <see cref="Slot"/> waits (no brain; the step drives it). When the player's car
    /// is within <see cref="Radius"/> (XZ) of the ped below 2 m/s and has a free passenger seat, the step walks the ped
    /// to the nearer door (<c>Ped.Move.DesiredVelocity</c> 1.6 m/s toward <c>DoorPosition</c>, Walk anim) and seats it
    /// (<c>PutInCar</c>, lowest free seat ≥ 1) on reaching 1 m from the door or after 6 s; Done when seated. A car with
    /// no free seat shows <see cref="CampaignText.NoRoom"/>. Blip: Friendly following the ped.
    /// </summary>
    public sealed class PickUpStep : MissionStep
    {
        /// <summary>Ped slot.</summary>
        public readonly int Slot;
        /// <summary>Pick-up radius, metres.</summary>
        public float Radius = 8f;

        /// <summary>Creates the step.</summary>
        public PickUpStep(int slot)
        {
            Slot = slot;
            WithActorBlip(BlipKind.Friendly, slot);
        }

        /// <inheritdoc />
        public override StepKind Kind { get { return StepKind.PickUp; } }

        /// <inheritdoc />
        public override StepState Step(MissionContext ctx, float dt) { throw new NotImplementedException(); }
    }

    /// <summary>
    /// Hose a fire from a fire engine. Progress accumulates (game seconds) while the player drives a
    /// <see cref="VehicleKind.FireTruck"/> whose position is within <see cref="Radius"/> (XZ) of <see cref="Point"/> and
    /// holds Attack; every whole second of progress emits <c>CityEffect.DouseFire(Building, DousePerSecond)</c>
    /// (none when Building is 0). <c>Mission.StatusLine</c> shows the progress in tenths
    /// (<see cref="CampaignText.FireProgress"/>, cached strings). Done at <see cref="Seconds"/> of progress.
    /// </summary>
    public sealed class HoseFireStep : MissionStep
    {
        /// <summary>Burning building (0 none).</summary>
        public readonly ushort Building;
        /// <summary>Point to stand near (the door).</summary>
        public readonly Vec3 Point;
        /// <summary>Range, metres.</summary>
        public float Radius = 15f;
        /// <summary>Hosing needed, seconds.</summary>
        public float Seconds = 12f;
        /// <summary>DouseFire amount per second of hosing.</summary>
        public float DousePerSecond = 0.1f;

        /// <summary>Creates the step; blip Target at the point with a marker of the radius.</summary>
        public HoseFireStep(ushort building, Vec3 point)
        {
            Building = building;
            Point = point;
            WithBlip(BlipKind.Target, point, 0f);
        }

        /// <inheritdoc />
        public override StepKind Kind { get { return StepKind.HoseFire; } }

        /// <inheritdoc />
        public override StepState Step(MissionContext ctx, float dt) { throw new NotImplementedException(); }
    }

    /// <summary>
    /// Raise the wanted level (scripted heat). Begin sets <c>Wanted.Stars</c> = max(current, <see cref="Stars"/>),
    /// HasLastKnownPosition true, LastKnownPosition and SearchCentre = the player, SecondsSinceSeen 0, Evading false;
    /// the first Step returns Done. Keeps the previous objective.
    /// </summary>
    public sealed class SetWantedStep : MissionStep
    {
        /// <summary>Stars, 1-5.</summary>
        public readonly int Stars;

        /// <summary>Creates the step.</summary>
        public SetWantedStep(int stars)
        {
            Stars = stars;
            Objective = null;
        }

        /// <inheritdoc />
        public override StepKind Kind { get { return StepKind.SetWanted; } }

        /// <inheritdoc />
        public override StepState Step(MissionContext ctx, float dt) { throw new NotImplementedException(); }
    }
}
