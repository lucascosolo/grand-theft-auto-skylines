using System;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Missions
{
    /// <summary>What a step reports each <see cref="MissionStep.Step"/>.</summary>
    public enum StepState
    {
        /// <summary>Keep stepping.</summary>
        Running,
        /// <summary>Completed: the runner ends it and begins the next step (in the same fixed step).</summary>
        Done,
        /// <summary>Failed: the mission (or job round) fails with <see cref="MissionContext.FailReason"/>.</summary>
        Failed,
    }

    /// <summary>
    /// Runtime state shared by the steps of the running mission or side-job round. Owned and reused by
    /// <see cref="MissionRunner"/>: created once with fixed capacities, reset per run, never reallocated.
    /// </summary>
    public sealed class MissionContext
    {
        /// <summary>Actor slots per run.</summary>
        public const int MaxActors = 16;
        /// <summary>Events copied per fixed step; later ones in the same step are dropped (and counted in <see cref="DroppedEvents"/>).</summary>
        public const int EventCapacity = 64;

        /// <summary>Creates an empty context.</summary>
        public MissionContext()
        {
            ActorIds = new int[MaxActors];
            Events = new GameEvent[EventCapacity];
        }

        /// <summary>The world (set by the runner before every step).</summary>
        public GameWorld World;
        /// <summary>The running definition (null for a side-job round).</summary>
        public MissionDefinition Definition;
        /// <summary>Game ids of spawned actors by slot (0 = not spawned or removed).</summary>
        public readonly int[] ActorIds;
        /// <summary>Index of the current step.</summary>
        public int StepIndex;
        /// <summary>Game seconds since the current step began.</summary>
        public float StepSeconds;
        /// <summary>Game seconds since the run began.</summary>
        public float RunSeconds;
        /// <summary>This fixed step's events (from the runner's single cursor), valid indices [0, <see cref="EventCount"/>).</summary>
        public readonly GameEvent[] Events;
        /// <summary>Number of valid <see cref="Events"/>.</summary>
        public int EventCount;
        /// <summary>Events dropped because a step produced more than <see cref="EventCapacity"/> (debug).</summary>
        public long DroppedEvents;
        /// <summary>Reason set by <see cref="Fail"/>.</summary>
        public string FailReason;
        /// <summary>Id the runner hands out to blips and markers of this run (blip ids 1000-1999 belong to missions).</summary>
        public int NextBlipId = 1000;

        /// <summary>Sets <see cref="FailReason"/> and returns <see cref="StepState.Failed"/>.</summary>
        public StepState Fail(string reason)
        {
            FailReason = reason;
            return StepState.Failed;
        }

        /// <summary>The live ped in a slot, or null (unspawned, removed, out of range).</summary>
        public Ped ActorPed(int slot)
        {
            return slot < 0 || slot >= MaxActors || ActorIds[slot] == 0 ? null : World.FindPed(ActorIds[slot]);
        }

        /// <summary>The live car in a slot, or null.</summary>
        public Car ActorCar(int slot)
        {
            return slot < 0 || slot >= MaxActors || ActorIds[slot] == 0 ? null : World.FindCar(ActorIds[slot]);
        }

        /// <summary>The car the player is sitting in (any seat), or null.</summary>
        public Car PlayerCar
        {
            get
            {
                Ped p = World.Player;
                return p == null || p.VehicleId == 0 ? null : World.FindCar(p.VehicleId);
            }
        }

        /// <summary>True when the player is in seat 0 of <paramref name="car"/>.</summary>
        public bool PlayerDrives(Car car)
        {
            Ped p = World.Player;
            return car != null && p != null && p.VehicleId == car.Id && p.Seat == 0 && p.Has(PedFlags.InVehicle);
        }

        /// <summary>The player's position: the car's when sitting in one, else the feet.</summary>
        public Vec3 PlayerPosition
        {
            get
            {
                Car car = PlayerCar;
                return car != null ? car.Position : World.Player.Position;
            }
        }

        /// <summary>
        /// True when an event actor value names <paramref name="tag"/>: the tag itself, or the bare id (events written
        /// by hand with ids instead of tags).
        /// </summary>
        public static bool Names(int value, int tag, int id)
        {
            return value != 0 && (value == tag || (value == id && WorldTag.KindOf(value) == TagKind.None));
        }

        /// <summary>True when an event actor value names the player.</summary>
        public bool IsPlayer(int value)
        {
            Ped p = World.Player;
            return p != null && Names(value, p.Tag, p.Id);
        }

        /// <summary>The car an event actor value names (tag or bare id), or null.</summary>
        public Car CarOf(int value)
        {
            if (value == 0) return null;
            TagKind k = WorldTag.KindOf(value);
            if (k == TagKind.Car) return World.FindCar(WorldTag.IdOf(value));
            return k == TagKind.None ? World.FindCar(value) : null;
        }

        /// <summary>True when any of this step's events has the type and (when not 0) the given actors.</summary>
        public bool HasEvent(GameEventType type, int actorA, int actorB)
        {
            for (int i = 0; i < EventCount; i++)
            {
                if (Events[i].Type != type) continue;
                if (actorA != 0 && !SameActor(Events[i].ActorA, actorA)) continue;
                if (actorB != 0 && !SameActor(Events[i].ActorB, actorB)) continue;
                return true;
            }
            return false;
        }

        private static bool SameActor(int value, int wanted)
        {
            return value == wanted || (WorldTag.KindOf(value) == TagKind.None && value == WorldTag.IdOf(wanted));
        }

        /// <summary>Clears per-run state (slots, step counters, events, fail reason) for a new run.</summary>
        public void Reset(GameWorld world, MissionDefinition definition)
        {
            if (world == null) throw new ArgumentNullException("world");
            World = world;
            Definition = definition;
            Array.Clear(ActorIds, 0, MaxActors);
            StepIndex = 0;
            StepSeconds = 0f;
            RunSeconds = 0f;
            EventCount = 0;
            FailReason = null;
            WitnessId = 0;
        }

        /// <summary>Raises stars and reports scripted crimes (G8 seam; default writes <c>WantedStatus</c> directly).</summary>
        public IWantedControl Wanted = DirectWantedControl.Instance;

        /// <summary>
        /// Brain for a spawned mission ped (G7 seam). Default: hostile specs get
        /// <c>Hooks.Brains.Create(world, ped, PedOrigin.Mission, playerTag, default)</c>, others no brain.
        /// </summary>
        public MissionBrainFactory Brains = DefaultBrains;

        /// <summary>The pending witness ped id (0 none).</summary>
        public int WitnessId { get; private set; }
        private float _witnessSeconds, _witnessCall;
        private int _witnessStars;

        /// <summary>
        /// Spawns a witness 8-12 m from <paramref name="car"/> on the side away from the player, phoning the police
        /// (OnPhone, Phone anim, Persistent, no brain). See <see cref="StepWitness"/>.
        /// </summary>
        public void StartWitness(Car car, int stars, float callSeconds)
        {
            GameWorld w = World;
            Vec3 away = (car.Position - w.Player.Position).Flat;
            float len = away.Length;
            away = len > 0.01f ? away / len : new Vec3(0f, 0f, 1f);
            var rng = new Rng(Rng.Seed((uint)car.Id, w.Seed, 0x717Eu));
            Vec3 feet = car.Position + away * rng.Range(8f, 12f);
            feet.Y = w.City.GroundHeight(feet.X, feet.Z);
            int models = w.City.CitizenModelCount;
            Ped ped = w.SpawnPed(PedKind.Civilian, models > 0 ? rng.Range(0, models) : 0, feet, (float)(Math.Atan2(-away.X, -away.Z) / MathUtil.Deg2Rad));
            ped.Flags |= PedFlags.OnPhone | PedFlags.Persistent;
            ped.Anim = PedAnim.Phone;
            WitnessId = ped.Id;
            _witnessSeconds = 0f;
            _witnessCall = callSeconds;
            _witnessStars = stars;
        }

        /// <summary>
        /// Advances a pending witness call: after its seconds, unless the witness is dead, knocked down or gone, reports
        /// the crime (<see cref="Wanted"/>), then hands the witness a normal brain.
        /// </summary>
        public void StepWitness(float dt)
        {
            if (WitnessId == 0) return;
            Ped ped = World.FindPed(WitnessId);
            if (ped == null || (ped.Flags & (PedFlags.Dead | PedFlags.KnockedDown)) != 0)
            {
                WitnessId = 0;
                return;
            }
            _witnessSeconds += dt;
            if (_witnessSeconds < _witnessCall) return;
            Wanted.ReportCrime(World, _witnessStars, World.Player.Position);
            ped.Flags &= ~(PedFlags.OnPhone | PedFlags.Persistent);
            ped.Anim = PedAnim.Idle;
            if (World.Hooks.Brains != null) ped.Brain = World.Hooks.Brains.Create(World, ped, PedOrigin.PromotedCitizen, World.Player.Tag, default(NpcSpot));
            WitnessId = 0;
        }

        /// <summary>
        /// Spawns <paramref name="spec"/> into <paramref name="slot"/> (see <see cref="MissionActorSpec"/>): cars with
        /// Owner Mission and their driver AI, peds Persistent (never demoted) with Hostile, armour, weapon, citizen
        /// (registered so <c>FindPedByCitizen</c> finds it) and the brain from <see cref="Brains"/>, seated when
        /// <c>InCarSlot</c> names a live car. Returns false when nothing could be spawned (no model).
        /// </summary>
        public bool SpawnActor(MissionActorSpec spec, int slot)
        {
            GameWorld w = World;
            var rng = new Rng(Rng.Seed(spec.Seed, (uint)slot + 1u, 0xAC70u));
            if (spec.Kind == ActorKind.Car)
            {
                int model = w.City.FindVehicleModel(spec.VehicleKind, spec.Seed);
                if (model < 0) model = w.City.FindVehicleModel(VehicleKind.Car, spec.Seed);
                if (model < 0) return false;
                int colour = spec.ColourRgb >= 0 ? spec.ColourRgb : CarColours[rng.Range(0, CarColours.Length)];
                Car car = w.SpawnCar(model, spec.Position, spec.YawDeg, CarOwner.Mission, colour);
                if (car == null) return false;
                car.Locked = spec.Locked;
                if (spec.Driving == ActorDriving.Flee) car.Driver = new FleeDriver(spec.Seed, spec.SpeedFactor);
                else if (spec.Driving == ActorDriving.DriveTo)
                {
                    var driver = new RouteDriver();
                    driver.Follower.SpeedFactor = spec.SpeedFactor;
                    driver.SetDestination(spec.DriveTo);
                    car.Driver = driver;
                }
                ActorIds[slot] = car.Id;
                return true;
            }
            int models = w.City.CitizenModelCount;
            int pedModel = spec.Model >= 0 ? spec.Model : models > 0 ? rng.Range(0, models) : 0;
            Ped ped = w.SpawnPed(spec.PedKind, pedModel, spec.Position, spec.YawDeg);
            ped.Flags |= PedFlags.Persistent;
            if (spec.Hostile) ped.Flags |= PedFlags.Hostile;
            ped.Armour = spec.Armour;
            ped.CurrentWeapon = (int)spec.Weapon;
            if (spec.Citizen != 0 && w.FindPedByCitizen(spec.Citizen) == null)
            {
                ped.Citizen = spec.Citizen;
                w.RegisterCitizen(ped);
            }
            ped.Brain = Brains == null ? null : Brains(w, ped, spec);
            if (spec.InCarSlot >= 0)
            {
                Car car = ActorCar(spec.InCarSlot);
                if (car != null && spec.Seat < Math.Min(car.Model.Seats, Car.MaxSeats) && car.Seats[spec.Seat] == 0) w.PutInCar(ped, car, spec.Seat);
            }
            ActorIds[slot] = ped.Id;
            return true;
        }

        /// <summary>
        /// Removes the actors in slots [0, <paramref name="slots"/>): peds (returnCitizen true), cars unless the player
        /// sits in them (those become Owner Abandoned with no AI). Slots are cleared.
        /// </summary>
        public void RemoveActors(int slots)
        {
            if (World == null) return;
            int n = Math.Min(slots, MaxActors);
            for (int i = 0; i < n; i++)
            {
                int id = ActorIds[i];
                ActorIds[i] = 0;
                if (id == 0) continue;
                Ped ped = World.FindPed(id);
                if (ped != null)
                {
                    if (ped != World.Player) World.RemovePed(ped, true);
                    continue;
                }
                Car car = World.FindCar(id);
                if (car == null) continue;
                if (World.Player != null && World.Player.VehicleId == car.Id)
                {
                    car.Owner = CarOwner.Abandoned;
                    car.Driver = null;
                }
                else World.RemoveCar(car);
            }
        }

        private static readonly int[] CarColours = { 0x202020, 0xE0E0E0, 0x8A1C1C, 0x1C3A8A, 0x2F6B3A, 0xB0B0B0, 0xC28A1A };

        /// <summary>Next blip id for a step.</summary>
        public int TakeBlipId()
        {
            if (NextBlipId < 1000 || NextBlipId > 1999) NextBlipId = 1000;
            return NextBlipId++;
        }

        private static IPedBrain DefaultBrains(GameWorld world, Ped ped, MissionActorSpec spec)
        {
            if (spec == null || !spec.Hostile || world.Hooks.Brains == null) return null;
            return world.Hooks.Brains.Create(world, ped, PedOrigin.Mission, world.Player == null ? 0 : world.Player.Tag, default(NpcSpot));
        }
    }

    /// <summary>Creates the brain of a spawned mission ped (the wiring chunk supplies G7's HostileBrain here).</summary>
    public delegate IPedBrain MissionBrainFactory(GameWorld world, Ped ped, MissionActorSpec spec);

    /// <summary>The wanted-level writes missions make (the wiring chunk routes them to G8's crime system).</summary>
    public interface IWantedControl
    {
        /// <summary>Scripted heat: stars become at least <paramref name="stars"/>, last seen at <paramref name="lastKnown"/>.</summary>
        void RaiseStars(GameWorld world, int stars, Vec3 lastKnown);

        /// <summary>A crime a mission guarantees is known (a witness call, being seen by cops): at least <paramref name="stars"/>.</summary>
        void ReportCrime(GameWorld world, int stars, Vec3 position);
    }

    /// <summary>Writes <see cref="WantedStatus"/> directly (stars max'ed, last known position set, not evading).</summary>
    public sealed class DirectWantedControl : IWantedControl
    {
        /// <summary>The shared instance.</summary>
        public static readonly DirectWantedControl Instance = new DirectWantedControl();

        /// <inheritdoc />
        public void RaiseStars(GameWorld world, int stars, Vec3 lastKnown)
        {
            WantedStatus w = world.Wanted;
            int s = stars > WantedStatus.MaxStars ? WantedStatus.MaxStars : stars;
            if (s > w.Stars) w.Stars = s;
            w.HasLastKnownPosition = true;
            w.LastKnownPosition = lastKnown;
            w.SearchCentre = lastKnown;
            w.SecondsSinceSeen = 0f;
            w.Evading = false;
        }

        /// <inheritdoc />
        public void ReportCrime(GameWorld world, int stars, Vec3 position)
        {
            RaiseStars(world, stars, position);
        }
    }
}
