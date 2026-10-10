using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Profile;
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
            _blipCount = 0;
            _markerCount = 0;
            _mainBlip = -1;
            _detourBlip = -1;
            _detour = false;
            _setGps = false;
            MissionStatus m = ctx.World.Mission;
            if (Objective != null) Objective.WriteObjective(m);
            if (Help != null) ctx.World.Messages.ShowHelp(Help, 2, 8f);
            if (!HasBlip) return;
            Vec3 pos = BlipPosition;
            int follow = 0;
            ActorAt(ctx, BlipSlot, ref pos, ref follow);
            _mainBlip = AddBlip(ctx, BlipKind, pos, follow);
            if (MarkerRadius > 0f) AddMarker(ctx, pos, MarkerRadius);
            if (Gps) SetGps(ctx, pos);
        }

        private readonly int[] _blipIds = new int[8];
        private int _blipCount;
        private readonly int[] _markerIds = new int[4];
        private int _markerCount;
        private int _mainBlip = -1;
        private int _detourBlip = -1;
        private bool _detour;
        private bool _setGps;

        /// <summary>Adds a blip this step owns (removed by <see cref="End"/>); returns its id.</summary>
        protected int AddBlip(MissionContext ctx, BlipKind kind, Vec3 position, int followTag)
        {
            int id = ctx.TakeBlipId();
            ctx.World.Mission.Blips.Add(new Blip { Id = id, Kind = kind, Position = position, FollowTag = followTag });
            if (_blipCount < _blipIds.Length) _blipIds[_blipCount++] = id;
            return id;
        }

        /// <summary>Removes a blip by id.</summary>
        protected void RemoveBlip(MissionContext ctx, int id)
        {
            List<Blip> blips = ctx.World.Mission.Blips;
            for (int i = blips.Count - 1; i >= 0; i--) if (blips[i].Id == id) blips.RemoveAt(i);
            Untrack(_blipIds, ref _blipCount, id);
        }

        private static void Untrack(int[] ids, ref int count, int id)
        {
            for (int i = 0; i < count; i++)
            {
                if (ids[i] != id) continue;
                ids[i] = ids[--count];
                return;
            }
        }

        /// <summary>Adds a yellow world marker this step owns.</summary>
        protected int AddMarker(MissionContext ctx, Vec3 position, float radius)
        {
            int id = ctx.TakeBlipId();
            ctx.World.Mission.Markers.Add(new WorldMarker { Id = id, Position = position, Radius = radius, ColourRgb = MissionRunner.MarkerColour });
            if (_markerCount < _markerIds.Length) _markerIds[_markerCount++] = id;
            return id;
        }

        /// <summary>Removes a marker by id.</summary>
        protected void RemoveMarker(MissionContext ctx, int id)
        {
            List<WorldMarker> markers = ctx.World.Mission.Markers;
            for (int i = markers.Count - 1; i >= 0; i--) if (markers[i].Id == id) markers.RemoveAt(i);
            Untrack(_markerIds, ref _markerCount, id);
        }

        /// <summary>Routes the GPS to a point.</summary>
        protected void SetGps(MissionContext ctx, Vec3 position)
        {
            ctx.World.Mission.HasGpsTarget = true;
            ctx.World.Mission.GpsTarget = position;
            _setGps = true;
        }

        /// <summary>Points the main blip (and the GPS when <see cref="Gps"/>) at the live actor in <see cref="BlipSlot"/>.</summary>
        protected void RefreshSlotBlip(MissionContext ctx)
        {
            if (_mainBlip < 0 || BlipSlot < 0 || _detour) return;
            Vec3 pos = BlipPosition;
            int follow = 0;
            if (!ActorAt(ctx, BlipSlot, ref pos, ref follow)) return;
            SetMainBlipFollow(ctx, follow, pos);
        }

        /// <summary>Makes the main blip follow a tag at a position (and the GPS when <see cref="Gps"/>).</summary>
        protected void SetMainBlipFollow(MissionContext ctx, int followTag, Vec3 position)
        {
            List<Blip> blips = ctx.World.Mission.Blips;
            for (int i = 0; i < blips.Count; i++)
            {
                if (blips[i].Id != _mainBlip) continue;
                Blip b = blips[i];
                b.FollowTag = followTag;
                b.Position = position;
                blips[i] = b;
            }
            if (Gps) SetGps(ctx, position);
        }

        private static bool ActorAt(MissionContext ctx, int slot, ref Vec3 pos, ref int follow)
        {
            if (slot < 0) return false;
            Car car = ctx.ActorCar(slot);
            if (car != null) { pos = car.Position; follow = car.Tag; return true; }
            Ped ped = ctx.ActorPed(slot);
            if (ped != null) { pos = ped.Position; follow = ped.Tag; return true; }
            return false;
        }

        /// <summary>
        /// True when the player drives <paramref name="car"/>. Otherwise switches to the "get back in" objective with a
        /// Friendly blip on the car, restoring the step's own objective and GPS when the player is back.
        /// </summary>
        protected bool RequireCar(MissionContext ctx, Car car)
        {
            bool inIt = ctx.PlayerDrives(car);
            MissionStatus m = ctx.World.Mission;
            if (!inIt && !_detour)
            {
                _detour = true;
                GetBackIn.WriteObjective(m);
                _detourBlip = AddBlip(ctx, BlipKind.Friendly, car.Position, car.Tag);
                SetGps(ctx, car.Position);
            }
            else if (inIt && _detour)
            {
                _detour = false;
                RemoveBlip(ctx, _detourBlip);
                _detourBlip = -1;
                if (Objective != null) Objective.WriteObjective(m);
                if (HasBlip && Gps) SetGps(ctx, BlipPosition);
            }
            else if (!inIt) m.GpsTarget = car.Position;
            return inIt;
        }

        private static readonly RichText GetBackIn = RichText.Parse(CampaignText.GetBackInCar);

        /// <summary>True when <paramref name="kinds"/> is empty or contains <paramref name="kind"/>.</summary>
        protected static bool KindAccepted(VehicleKind[] kinds, VehicleKind kind)
        {
            if (kinds.Length == 0) return true;
            for (int i = 0; i < kinds.Length; i++) if (kinds[i] == kind) return true;
            return false;
        }

        /// <summary>Advances the step by one fixed step.</summary>
        public abstract StepState Step(MissionContext ctx, float dt);

        /// <summary>Ends the step (pass, fail or abort): removes what <see cref="Begin"/> added.</summary>
        public virtual void End(MissionContext ctx)
        {
            MissionStatus m = ctx.World.Mission;
            while (_blipCount > 0) RemoveBlip(ctx, _blipIds[_blipCount - 1]);
            while (_markerCount > 0) RemoveMarker(ctx, _markerIds[_markerCount - 1]);
            _blipCount = 0;
            _markerCount = 0;
            if (_setGps) m.HasGpsTarget = false;
            _setGps = false;
            m.StatusLine = null;
            m.TimerSeconds = -1f;
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
        public override StepState Step(MissionContext ctx, float dt)
        {
            Car car = ctx.PlayerCar;
            bool driving = car != null && ctx.PlayerDrives(car);
            bool ok;
            switch (Mode)
            {
                case TravelMode.OnFoot: ok = car == null; break;
                case TravelMode.AnyVehicle: ok = driving; break;
                case TravelMode.VehicleOfKind: ok = driving && car.Model.Kind == VehicleKind; break;
                case TravelMode.MissionVehicle:
                    Car mission = ctx.ActorCar(VehicleSlot);
                    if (mission == null && ctx.ActorIds[VehicleSlot] != 0) return ctx.Fail(FailReasons.VehicleDestroyed);
                    ok = mission == null ? driving : RequireCar(ctx, mission);
                    break;
                default: ok = true; break;
            }
            if (!ok) return StepState.Running;
            Vec3 at = car != null ? car.Position : ctx.World.Player.Position;
            float speed = car != null ? car.Body.Speed : ctx.World.Player.Motor.Velocity.LengthXZ;
            return Vec3.DistanceXZ(at, Target) <= Radius && speed <= MaxSpeed ? StepState.Done : StepState.Running;
        }
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
        public override StepState Step(MissionContext ctx, float dt)
        {
            Car car = ctx.PlayerCar;
            bool driving = car != null && ctx.PlayerDrives(car);
            if (VehicleSlot >= 0)
            {
                Car target = ctx.ActorCar(VehicleSlot);
                if (ctx.ActorIds[VehicleSlot] != 0 && (target == null || target.Destroyed)) return ctx.Fail(FailReasons.VehicleDestroyed);
                RefreshSlotBlip(ctx);
                if (!driving || (target != null && car != target)) return StepState.Running;
            }
            else if (!driving || !KindAccepted(Kinds, car.Model.Kind)) return StepState.Running;
            if (StoreSlot >= 0) ctx.ActorIds[StoreSlot] = car.Id;
            return StepState.Done;
        }
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
        private int _stolenCarId;
        private bool _done;

        /// <inheritdoc />
        public override void Begin(MissionContext ctx)
        {
            base.Begin(ctx);
            _stolenCarId = 0;
            _done = false;
        }

        /// <inheritdoc />
        public override StepState Step(MissionContext ctx, float dt)
        {
            if (_done)
            {
                ctx.StepWitness(dt);
                return StepState.Done;
            }
            if (VehicleSlot >= 0) RefreshSlotBlip(ctx);
            for (int i = 0; i < ctx.EventCount; i++)
            {
                GameEvent e = ctx.Events[i];
                bool stolen = e.Type == GameEventType.CarStolen || (!ParkedOnly && e.Type == GameEventType.CarJacked);
                if (!stolen || !ctx.IsPlayer(e.ActorA)) continue;
                Car car = ctx.CarOf(e.ActorB);
                if (car != null && Accepts(ctx, car)) _stolenCarId = car.Id;
            }
            if (_stolenCarId == 0) return StepState.Running;
            Car stolenCar = ctx.World.FindCar(_stolenCarId);
            if (stolenCar == null || !ctx.PlayerDrives(stolenCar)) return StepState.Running;
            if (StoreSlot >= 0) ctx.ActorIds[StoreSlot] = stolenCar.Id;
            if (GuaranteedWitness) ctx.StartWitness(stolenCar, WitnessStars, WitnessCallSeconds);
            _done = true;
            return StepState.Done;
        }

        private bool Accepts(MissionContext ctx, Car car)
        {
            if (VehicleSlot >= 0) return ctx.ActorIds[VehicleSlot] == car.Id;
            return KindAccepted(Kinds, car.Model.Kind);
        }
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
        private int _exitedPedId;
        private float _exitSeconds;

        /// <inheritdoc />
        public override void Begin(MissionContext ctx)
        {
            base.Begin(ctx);
            _exitedPedId = 0;
            _exitSeconds = 0f;
        }

        /// <inheritdoc />
        public override StepState Step(MissionContext ctx, float dt)
        {
            GameWorld world = ctx.World;
            if (_exitedPedId != 0)
            {
                _exitSeconds += dt;
                Ped gone = world.FindPed(_exitedPedId);
                if (_exitSeconds >= 2f && gone != null && RemovePassenger)
                {
                    world.RemovePed(gone, true);
                    _exitedPedId = 0;
                }
                return StepState.Done;
            }
            if (FailIfWanted && world.Wanted.Stars > 0) return ctx.Fail(FailReasons.Spotted);
            Car car = null;
            Ped passenger = null;
            switch (What)
            {
                case DeliverWhat.CurrentCar:
                    car = ctx.PlayerCar;
                    if (car == null || !ctx.PlayerDrives(car)) return StepState.Running;
                    break;
                case DeliverWhat.MissionVehicle:
                    car = ctx.ActorCar(Slot);
                    if (car == null) return ctx.Fail(FailReasons.VehicleDestroyed);
                    if (!RequireCar(ctx, car)) return StepState.Running;
                    break;
                case DeliverWhat.Passenger:
                    passenger = ctx.ActorPed(Slot);
                    if (passenger == null || passenger.IsDead) return ctx.Fail(FailReasons.PassengerKilled);
                    car = passenger.VehicleId != 0 ? world.FindCar(passenger.VehicleId) : null;
                    if (car == null || !RequireCar(ctx, car)) return StepState.Running;
                    break;
            }
            Vec3 at = car != null ? car.Position : ctx.PlayerPosition;
            if (Vec3.DistanceXZ(at, Target) > Radius) return StepState.Running;
            if (car != null)
            {
                if (car.Body.Speed > MaxSpeed) return StepState.Running;
                CarHealthAtDelivery = car.Body.Health;
                if (MinHealth > 0f && CarHealthAtDelivery < MinHealth) return ctx.Fail(FailReasons.CarTooDamaged);
            }
            if (passenger != null)
            {
                world.TakeOutOfCar(passenger, world.DoorPosition(car, false), passenger.YawDeg);
                _exitedPedId = passenger.Id;
            }
            return StepState.Done;
        }
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
        private readonly int[] _driverIds = new int[2];
        private readonly float[] _stopped = new float[2];
        private readonly bool[] _done = new bool[2];

        /// <summary>Targets stopped or destroyed so far (output; vigilante pay).</summary>
        public int Stopped;

        /// <inheritdoc />
        public override void Begin(MissionContext ctx)
        {
            base.Begin(ctx);
            for (int i = 0; i < 2; i++) { _driverIds[i] = 0; _stopped[i] = 0f; _done[i] = false; }
            Stopped = 0;
        }

        /// <inheritdoc />
        public override StepState Step(MissionContext ctx, float dt)
        {
            if (_driverIds[0] == 0) RefreshSlotBlip(ctx);
            bool all = true;
            for (int i = 0; i < 2; i++)
            {
                int slot = i == 0 ? CarSlot : SecondCarSlot;
                if (slot < 0 || _done[i]) continue;
                StepState s = StepTarget(ctx, i, slot, dt);
                if (s == StepState.Failed) return s;
                if (s == StepState.Done) { _done[i] = true; Stopped++; }
                else all = false;
            }
            return all ? StepState.Done : StepState.Running;
        }

        private StepState StepTarget(MissionContext ctx, int i, int slot, float dt)
        {
            GameWorld world = ctx.World;
            Vec3 player = ctx.PlayerPosition;
            if (_driverIds[i] != 0)
            {
                Ped driver = world.FindPed(_driverIds[i]);
                if (driver == null || (driver.Flags & (PedFlags.Dead | PedFlags.KnockedDown | PedFlags.Arrested | PedFlags.HandsUp)) != 0) return StepState.Done;
                return Vec3.DistanceXZ(driver.Position, player) > EscapeDistance ? ctx.Fail(FailReasons.TargetEscaped) : StepState.Running;
            }
            Car car = ctx.ActorCar(slot);
            if (car == null || car.Destroyed) return StepState.Done;
            if (Vec3.DistanceXZ(car.Position, player) > EscapeDistance) return ctx.Fail(FailReasons.TargetEscaped);
            _stopped[i] = car.Body.Speed < 1f ? _stopped[i] + dt : 0f;
            if (_stopped[i] < StoppedSeconds) return StepState.Running;
            Ped bail = car.DriverId != 0 ? world.FindPed(car.DriverId) : null;
            if (bail == null) return StepState.Done;
            world.TakeOutOfCar(bail, world.DoorPosition(car, true), bail.YawDeg);
            car.Driver = null;
            if (world.Hooks.Brains != null) bail.Brain = world.Hooks.Brains.Create(world, bail, PedOrigin.CarjackedDriver, world.Player.Tag, default(NpcSpot));
            _driverIds[i] = bail.Id;
            if (i == 0) SetMainBlipFollow(ctx, bail.Tag, bail.Position);
            return StepState.Running;
        }
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
        private int[] _blips = new int[0];

        /// <inheritdoc />
        public override void Begin(MissionContext ctx)
        {
            base.Begin(ctx);
            if (_blips.Length != Slots.Length) _blips = new int[Slots.Length];
            for (int i = 0; i < _blips.Length; i++) _blips[i] = -1;
        }

        /// <inheritdoc />
        public override StepState Step(MissionContext ctx, float dt)
        {
            bool all = true;
            for (int i = 0; i < Slots.Length; i++)
            {
                Ped ped = ctx.ActorPed(Slots[i]);
                bool alive = ped != null && !ped.IsDead;
                if (alive)
                {
                    all = false;
                    if (_blips[i] < 0) _blips[i] = AddBlip(ctx, BlipKind.Target, ped.Position, ped.Tag);
                }
                else if (_blips[i] >= 0)
                {
                    RemoveBlip(ctx, _blips[i]);
                    _blips[i] = -1;
                }
            }
            return all ? StepState.Done : StepState.Running;
        }
    }

    /// <summary>Lose the police. Done in the first step where <c>Wanted.Stars</c> is 0 (immediately when not wanted). No blip.</summary>
    public sealed class LoseWantedStep : MissionStep
    {
        /// <inheritdoc />
        public override StepKind Kind { get { return StepKind.LoseWanted; } }

        /// <inheritdoc />
        public override StepState Step(MissionContext ctx, float dt)
        {
            return ctx.World.Wanted.Stars == 0 ? StepState.Done : StepState.Running;
        }
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
        private bool _inside;

        /// <inheritdoc />
        public override void Begin(MissionContext ctx)
        {
            base.Begin(ctx);
            _inside = false;
            if (OutsideObjective != null) OutsideObjective.WriteObjective(ctx.World.Mission);
            UpdateInside(ctx);
        }

        /// <inheritdoc />
        public override StepState Step(MissionContext ctx, float dt)
        {
            UpdateInside(ctx);
            for (int i = 0; i < ctx.EventCount; i++)
            {
                GameEvent e = ctx.Events[i];
                if (e.Type != GameEventType.CustomInteraction || (int)e.Value != CustomId) continue;
                if (SeenStars > 0 && CopSees(ctx)) ctx.Wanted.ReportCrime(ctx.World, SeenStars, ctx.World.Player.Position);
                return StepState.Done;
            }
            return StepState.Running;
        }

        private void UpdateInside(MissionContext ctx)
        {
            InteriorState interior = ctx.World.Interior;
            bool inside = interior.IsInside && interior.Building != null && interior.Building.Id == Building;
            if (inside == _inside) return;
            _inside = inside;
            if (inside)
            {
                bool present = false;
                for (int i = 0; i < interior.CustomInteractions.Count; i++) if (interior.CustomInteractions[i].Id == CustomId) present = true;
                if (!present && interior.Layout != null) interior.AddCustomInteraction(CustomId, LockerPoint(interior.Layout), Prompt);
                if (Objective != null) Objective.WriteObjective(ctx.World.Mission);
            }
            else if (OutsideObjective != null) OutsideObjective.WriteObjective(ctx.World.Mission);
        }

        /// <summary>Where the item is: the centre of the first locker room, else a stock room, armory or workshop, else 3 m in front of the entrance.</summary>
        public static Vec3 LockerPoint(InteriorLayout layout)
        {
            Room best = null;
            for (int i = 0; i < layout.Rooms.Count && best == null; i++) if (layout.Rooms[i].Kind == RoomKind.LockerRoom) best = layout.Rooms[i];
            for (int i = 0; i < layout.Rooms.Count && best == null; i++)
            {
                RoomKind k = layout.Rooms[i].Kind;
                if (k == RoomKind.StockRoom || k == RoomKind.Armory || k == RoomKind.Workshop) best = layout.Rooms[i];
            }
            float y = layout.EntranceSpawn.Y;
            if (best != null) return new Vec3((best.Bounds.MinX + best.Bounds.MaxX) * 0.5f, y, (best.Bounds.MinZ + best.Bounds.MaxZ) * 0.5f);
            return layout.EntranceSpawn + Quat.Yaw(layout.EntranceSpawnYawDeg) * new Vec3(0f, 0f, 3f);
        }

        private bool CopSees(MissionContext ctx)
        {
            GameWorld world = ctx.World;
            Vec3 eye = world.Player.Position + new Vec3(0f, 1.6f, 0f);
            for (int i = 0; i < world.Peds.Count; i++)
            {
                Ped cop = world.Peds[i];
                if (cop.Kind != PedKind.Cop || cop.InteriorId != Building || (cop.Flags & (PedFlags.Dead | PedFlags.KnockedDown)) != 0) continue;
                Vec3 from = cop.Position + new Vec3(0f, 1.6f, 0f);
                Vec3 d = eye - from;
                float len = d.Length;
                if (len > SeenRange) continue;
                PhysicsHit hit;
                if (len < 0.01f || !world.Collision.Raycast(from, d / len, len, QueryMask.StaticOnly, out hit)) return true;
            }
            return false;
        }
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
        /// <inheritdoc />
        public override void Begin(MissionContext ctx)
        {
            base.Begin(ctx);
            Inner.Begin(ctx);
            SecondsLeft = Seconds;
            ctx.World.Mission.TimerSeconds = Seconds;
        }

        /// <inheritdoc />
        public override StepState Step(MissionContext ctx, float dt)
        {
            SecondsLeft -= dt;
            ctx.World.Mission.TimerSeconds = SecondsLeft > 0f ? SecondsLeft : 0f;
            StepState s = Inner.Step(ctx, dt);
            if (s != StepState.Running) return s;
            return SecondsLeft <= 0f ? ctx.Fail(FailReason) : StepState.Running;
        }

        /// <inheritdoc />
        public override void End(MissionContext ctx)
        {
            Inner.End(ctx);
            base.End(ctx);
            ctx.World.Mission.TimerSeconds = -1f;
        }
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
        public override StepState Step(MissionContext ctx, float dt)
        {
            if (StatusLines != null && StatusLines.Length > 0)
            {
                int i = (int)ctx.StepSeconds;
                ctx.World.Mission.StatusLine = StatusLines[i < StatusLines.Length ? i : StatusLines.Length - 1];
            }
            return ctx.StepSeconds >= Seconds - 1e-4f ? StepState.Done : StepState.Running;
        }

        /// <summary>Status lines shown by whole second (countdown, "Loading..."); the last one stays. Null for none.</summary>
        public string[] StatusLines;
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
        private float _total;

        /// <inheritdoc />
        public override void Begin(MissionContext ctx)
        {
            base.Begin(ctx);
            _total = 0f;
            for (int i = 0; i < Lines.Length; i++)
            {
                ctx.World.Messages.QueueSubtitle(Lines[i].Speaker, Lines[i].Text, Lines[i].Seconds);
                _total += Lines[i].Seconds;
            }
        }

        /// <inheritdoc />
        public override StepState Step(MissionContext ctx, float dt)
        {
            return ctx.StepSeconds >= _total - 1e-4f ? StepState.Done : StepState.Running;
        }
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
        private float _still;
        private bool _warned;

        /// <inheritdoc />
        public override void Begin(MissionContext ctx)
        {
            base.Begin(ctx);
            _still = 0f;
            _warned = false;
            if (ctx.PlayerCar == null) ctx.World.Messages.ShowHelp(CampaignText.NeedACar, 2, 6f);
        }

        /// <inheritdoc />
        public override StepState Step(MissionContext ctx, float dt)
        {
            for (int i = 0; i < ctx.EventCount; i++)
                if (ctx.Events[i].Type == GameEventType.Resprayed && ctx.IsPlayer(ctx.Events[i].ActorA)) return StepState.Done;
            if (!Backyard) return StepState.Running;
            Car car = ctx.PlayerCar;
            if (car == null || !ctx.PlayerDrives(car) || Vec3.DistanceXZ(car.Position, Door) > 6f || car.Body.Speed >= 1f)
            {
                _still = 0f;
                return StepState.Running;
            }
            _still += dt;
            if (_still < 3f) return StepState.Running;
            GameWorld world = ctx.World;
            if (Shop.Buy(world.Wallet, ShopKind.Respray, "respray") != PurchaseResult.Ok)
            {
                if (!_warned) world.Messages.ShowHelp(CampaignText.NeedCashForRespray, 2, 6f);
                _warned = true;
                return StepState.Running;
            }
            int colour = Palette[(int)(Rng.Seed((uint)car.Id, (uint)car.ColourRgb) % (uint)Palette.Length)];
            if (colour == car.ColourRgb) colour = Palette[(Array.IndexOf(Palette, colour) + 1) % Palette.Length];
            car.ColourRgb = colour;
            world.Emit(GameEventType.Resprayed, car.Position, world.Player.Tag, car.Tag, colour, null);
            return StepState.Done;
        }

        private static readonly int[] Palette = { 0x1B1B1B, 0xE8E8E8, 0x1F3F8F, 0x2E6B2E, 0x8C1C1C, 0xC9A227, 0x5A5A5A, 0x6B3FA0 };
    }

    /// <summary>
    /// Buy a shop item. Done on a <see cref="GameEventType.Purchase"/> event whose Text equals <see cref="ItemId"/>
    /// (ordinal), made inside <see cref="Building"/> when it is not 0 (<c>world.Interior.Building.Id</c>).
    /// Blip: Shop at the door.
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
        public override StepState Step(MissionContext ctx, float dt)
        {
            for (int i = 0; i < ctx.EventCount; i++)
            {
                GameEvent e = ctx.Events[i];
                if (e.Type != GameEventType.Purchase || !string.Equals(e.Text, ItemId, StringComparison.Ordinal)) continue;
                if (Building != 0)
                {
                    InteriorState interior = ctx.World.Interior;
                    if (!interior.IsInside || interior.Building == null || interior.Building.Id != Building) continue;
                }
                return StepState.Done;
            }
            return StepState.Running;
        }
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
        private bool _walking;
        private float _walkSeconds;
        private int _seat;
        private int _carId;
        private bool _warned;

        /// <inheritdoc />
        public override void Begin(MissionContext ctx)
        {
            base.Begin(ctx);
            _walking = false;
            _walkSeconds = 0f;
            _carId = 0;
            _warned = false;
        }

        /// <inheritdoc />
        public override StepState Step(MissionContext ctx, float dt)
        {
            GameWorld world = ctx.World;
            Ped ped = ctx.ActorPed(Slot);
            if (ped == null || ped.IsDead) return ctx.Fail(FailReasons.PassengerKilled);
            RefreshSlotBlip(ctx);
            Car car = ctx.PlayerCar;
            if (car == null || !ctx.PlayerDrives(car))
            {
                StopWalking(ped);
                return StepState.Running;
            }
            if (ped.VehicleId == car.Id) return StepState.Done;
            if (_walking && (car.Id != _carId || car.Body.Speed > 3f)) StopWalking(ped);
            if (!_walking)
            {
                if (Vec3.DistanceXZ(ped.Position, car.Position) > Radius || car.Body.Speed >= 2f) return StepState.Running;
                _seat = FreeSeat(car);
                if (_seat < 0)
                {
                    if (!_warned) world.Messages.ShowHelp(CampaignText.NoRoom, 2, 5f);
                    _warned = true;
                    return StepState.Running;
                }
                _walking = true;
                _walkSeconds = 0f;
                _carId = car.Id;
            }
            Vec3 left = world.DoorPosition(car, true), right = world.DoorPosition(car, false);
            Vec3 door = Vec3.DistanceXZ(ped.Position, left) <= Vec3.DistanceXZ(ped.Position, right) ? left : right;
            Vec3 to = (door - ped.Position).Flat;
            float dist = to.Length;
            _walkSeconds += dt;
            if (dist <= 1f || _walkSeconds >= 6f)
            {
                StopWalking(ped);
                world.PutInCar(ped, car, _seat);
                return StepState.Done;
            }
            ped.Move.DesiredVelocity = to * (1.6f / dist);
            ped.Move.FacingYawDeg = (float)(Math.Atan2(to.X, to.Z) / MathUtil.Deg2Rad);
            ped.Anim = PedAnim.Walk;
            return StepState.Running;
        }

        private void StopWalking(Ped ped)
        {
            _walking = false;
            ped.Move.DesiredVelocity = Vec3.Zero;
            if (ped.Anim == PedAnim.Walk) ped.Anim = PedAnim.Idle;
        }

        /// <summary>Lowest free passenger seat (≥ 1) of a car, or −1.</summary>
        public static int FreeSeat(Car car)
        {
            int seats = Math.Min(car.Model.Seats, Car.MaxSeats);
            for (int i = 1; i < seats; i++) if (car.Seats[i] == 0) return i;
            return -1;
        }
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
        private float _progress;
        private int _emitted;

        /// <summary>Seconds of hosing so far (output).</summary>
        public float Progress { get { return _progress; } }

        /// <inheritdoc />
        public override void Begin(MissionContext ctx)
        {
            base.Begin(ctx);
            _progress = 0f;
            _emitted = 0;
            ctx.World.Mission.StatusLine = CampaignText.FireProgress(0);
        }

        /// <inheritdoc />
        public override StepState Step(MissionContext ctx, float dt)
        {
            GameWorld world = ctx.World;
            Car car = ctx.PlayerCar;
            bool hosing = car != null && ctx.PlayerDrives(car) && car.Model.Kind == VehicleKind.FireTruck
                && Vec3.DistanceXZ(car.Position, Point) <= Radius && world.Input.IsHeld(GameButton.Attack);
            if (!hosing) return StepState.Running;
            _progress += dt;
            while (_progress + 1e-4f >= _emitted + 1)
            {
                _emitted++;
                if (Building != 0) world.Effects.Add(CityEffect.DouseFire(Building, DousePerSecond));
            }
            world.Mission.StatusLine = CampaignText.FireProgress((int)(_progress / Seconds * 10f));
            return _progress >= Seconds - 1e-4f ? StepState.Done : StepState.Running;
        }
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
        public override void Begin(MissionContext ctx)
        {
            base.Begin(ctx);
            ctx.Wanted.RaiseStars(ctx.World, Stars, ctx.World.Player.Position);
        }

        /// <inheritdoc />
        public override StepState Step(MissionContext ctx, float dt) { return StepState.Done; }
    }
}
