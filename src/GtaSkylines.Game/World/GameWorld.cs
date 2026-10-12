using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Roads;
using GtaSkylines.Game.Settings;
using GtaSkylines.Game.Vehicles;

namespace GtaSkylines.Game.World
{
    /// <summary>
    /// The shared state every <see cref="IGameSystem"/> reads and writes: the city interfaces, the collision world and
    /// road graph, registries of Game peds and cars, blackboards (wanted, mission, HUD messages, menu, interior) and the
    /// event buffer. Created by <c>GameSession</c>; all members are main-thread only.
    /// <para>
    /// Ids: peds and cars draw ids from one counter starting at 1 (the player, created by <c>GameSession.Begin</c>, is
    /// id 1); an id is never reused within the session. <see cref="Peds"/> and <see cref="Cars"/> list live actors in
    /// creation order (removal keeps the order of the rest), which every system iterates for determinism.
    /// </para>
    /// <para>
    /// Dynamic collision shapes (tags per <see cref="WorldTag"/>): at the start of each step the Ambient system calls
    /// <see cref="RebuildDynamicShapes"/>(true): one box per snapshot vehicle (half extents from its model, rotation from
    /// the snapshot; skipped once that vehicle has been promoted), one capsule (radius 0.3 m, height 1.8 m) per snapshot
    /// citizen (skipped once promoted), one capsule per Game ped on foot (motor radius and height; not InVehicle,
    /// Invisible or Dead) and one box per Game car (body pose and half extents). While inside an interior the snapshot is
    /// ignored (no ambient shapes). The Vehicles system calls <see cref="RebuildDynamicShapes"/>(false) before stepping
    /// car bodies (so cars see ambient boxes and ped capsules but not each other, and car–car contacts go through
    /// <c>VehicleBody.ResolveCollision</c>), then <see cref="AddCarShapes"/>. Every later system sees peds at their
    /// start-of-step positions and cars at their post-step poses.
    /// </para>
    /// </summary>
    public sealed class GameWorld
    {
        /// <summary>Seed of <see cref="Rng"/> when the session is created without one.</summary>
        public const uint DefaultSeed = 0x6A5C1E5u;
        /// <summary>Radius of an ambient citizen's capsule, metres.</summary>
        public const float AmbientCitizenRadius = 0.3f;
        /// <summary>Height of an ambient citizen's capsule, metres.</summary>
        public const float AmbientCitizenHeight = 1.8f;
        /// <summary>Gap between a car's side and the door position, metres.</summary>
        public const float DoorGap = 0.45f;
        /// <summary>Range of <see cref="CanPlayerSee"/>, metres.</summary>
        public const float SightRange = 250f;

        private static readonly Vec3 DefaultHalfExtents = new Vec3(0.9f, 0.75f, 2.2f);

        private readonly List<Ped> _peds = new List<Ped>();
        private readonly List<Car> _cars = new List<Car>();
        private readonly Dictionary<int, Ped> _pedById = new Dictionary<int, Ped>();
        private readonly Dictionary<int, Car> _carById = new Dictionary<int, Car>();
        private readonly Dictionary<uint, Ped> _pedByCitizen = new Dictionary<uint, Ped>();
        private readonly Dictionary<int, Car> _carBySource = new Dictionary<int, Car>();
        private int _nextId = 1;

        /// <summary>Creates an empty world (no player until <c>GameSession.Begin</c>).</summary>
        /// <exception cref="ArgumentNullException">city, settings or profile is null.</exception>
        public GameWorld(ICity city, GameSettings settings, PlayerProfile profile, uint seed)
        {
            if (city == null) throw new ArgumentNullException("city");
            if (settings == null) throw new ArgumentNullException("settings");
            if (profile == null) throw new ArgumentNullException("profile");
            City = city;
            Settings = settings;
            Profile = profile;
            Seed = seed;
            Wallet = new Wallet(profile);
            Collision = new CollisionWorld();
            Roads = new RoadGraph();
            Gps = new GpsRoute(Roads);
            Effects = new CityEffectQueue();
            Events = new GameEvents();
            Clock = new GameClock();
            Clock.Hour = city.TimeOfDayHours;
            Rng = new Rng(seed);
            Hooks = new GameHooks();
            Ambient = new CitySnapshot();
            PlayerState = new PlayerState();
            Wanted = new WantedStatus(profile);
            Mission = new MissionStatus();
            Messages = new HudMessages();
            Menu = new MenuRequest();
            Interior = new InteriorState();
        }

        // ---- inputs and services -------------------------------------------------------------------------------

        /// <summary>The session's seed; later systems derive their own <c>Rng</c> from it (<c>Rng.Seed(Seed, salt)</c>).</summary>
        public uint Seed { get; private set; }
        /// <summary>The city (read-only queries).</summary>
        public ICity City { get; private set; }
        /// <summary>Settings (FOV, views, bindings, police difficulty).</summary>
        public GameSettings Settings { get; private set; }
        /// <summary>The player's profile (cash, weapons, heat, stats).</summary>
        public PlayerProfile Profile { get; private set; }
        /// <summary>Wallet over <see cref="Profile"/>; the HUD drains its transactions for the cash delta.</summary>
        public Wallet Wallet { get; private set; }
        /// <summary>Static triangles (streamed by the Mod; the interior region by G6) and per-step dynamic shapes.</summary>
        public CollisionWorld Collision { get; private set; }
        /// <summary>Road graph (rebuilt by the Mod).</summary>
        public RoadGraph Roads { get; private set; }
        /// <summary>The HUD's GPS route over <see cref="Roads"/>; target = mission GPS target, else waypoint, else none (set by the HUD system).</summary>
        public GpsRoute Gps { get; private set; }
        /// <summary>
        /// Effects for the city produced this tick (drained by <c>GameSession</c> at the end of each Tick).
        /// <c>CityEffect.Chirp(0, text)</c> means "the Mod picks a nearby citizen as the sender".
        /// </summary>
        public CityEffectQueue Effects { get; private set; }
        /// <summary>Event buffer.</summary>
        public GameEvents Events { get; private set; }
        /// <summary>Clock.</summary>
        public GameClock Clock { get; private set; }
        /// <summary>The session's deterministic random source.</summary>
        public Rng Rng { get; private set; }
        /// <summary>Extension points (brains, dialogue, time scale, ammo readout).</summary>
        public GameHooks Hooks { get; private set; }
        /// <summary>Input for the current fixed step (pressed edges appear in exactly one step).</summary>
        public InputFrame Input;
        /// <summary>The latest city snapshot (copied in by <c>GameSession.Tick</c>; ignored while inside an interior).</summary>
        public CitySnapshot Ambient { get; private set; }

        // ---- actors --------------------------------------------------------------------------------------------

        /// <summary>The player ped (null before Begin).</summary>
        public Ped Player { get; private set; }
        /// <summary>Player-only state.</summary>
        public PlayerState PlayerState { get; private set; }
        /// <summary>Live peds in creation order (player first).</summary>
        public IList<Ped> Peds { get { return _peds; } }
        /// <summary>Live cars in creation order.</summary>
        public IList<Car> Cars { get { return _cars; } }

        internal List<Ped> PedList { get { return _peds; } }
        internal List<Car> CarList { get { return _cars; } }

        // ---- blackboards ---------------------------------------------------------------------------------------

        /// <summary>Police blackboard (G8).</summary>
        public WantedStatus Wanted { get; private set; }
        /// <summary>Mission blackboard (G9).</summary>
        public MissionStatus Mission { get; private set; }
        /// <summary>Help, subtitles, notifications, big screens.</summary>
        public HudMessages Messages { get; private set; }
        /// <summary>Open menu, if any.</summary>
        public MenuRequest Menu { get; private set; }
        /// <summary>The loaded interior.</summary>
        public InteriorState Interior { get; private set; }
        /// <summary>True when a map waypoint is set (<c>GameSession.SetWaypoint</c>).</summary>
        public bool HasWaypoint;
        /// <summary>The waypoint.</summary>
        public Vec3 Waypoint;
        /// <summary>Screen fade 0 (clear) .. 1 (black), written by the Interiors and Respawn systems.</summary>
        public float FadeAlpha;
        /// <summary>Camera position of the last step (written by the camera rig), for visibility tests.</summary>
        public Vec3 ViewPosition;
        /// <summary>Camera rotation of the last step.</summary>
        public Quat ViewRotation = Quat.Identity;
        /// <summary>Camera vertical field of view of the last step, degrees.</summary>
        public float ViewFovDeg = 70f;

        // ---- registry ------------------------------------------------------------------------------------------

        /// <summary>The live ped with this id, or null.</summary>
        public Ped FindPed(int id)
        {
            Ped p;
            return _pedById.TryGetValue(id, out p) ? p : null;
        }

        /// <summary>The live car with this id, or null.</summary>
        public Car FindCar(int id)
        {
            Car c;
            return _carById.TryGetValue(id, out c) ? c : null;
        }

        /// <summary>
        /// Fills <paramref name="into"/> with up to its length ambient citizens within <paramref name="maxDistance"/> (XZ)
        /// of <paramref name="at"/>, nearest first, equal distances by citizen id; <paramref name="skipPromoted"/> leaves out
        /// citizens that already have a Game ped. Returns the count. No allocation.
        /// </summary>
        public int NearestAmbientCitizens(Vec3 at, float maxDistance, bool skipPromoted, AmbientCitizen[] into)
        {
            int n = 0;
            if (Ambient == null) return 0;
            List<AmbientCitizen> cs = Ambient.Citizens;
            for (int i = 0; i < cs.Count; i++)
            {
                AmbientCitizen c = cs[i];
                float d = Vec3.DistanceXZ(c.Position, at);
                if (d > maxDistance || (skipPromoted && FindPedByCitizen(c.Citizen) != null)) continue;
                int k = n < into.Length ? n++ : into.Length;
                while (k > 0 && Closer(d, c.Citizen, into[k - 1], at))
                {
                    if (k < into.Length) into[k] = into[k - 1];
                    k--;
                }
                if (k < into.Length) into[k] = c;
            }
            return n;
        }

        private static bool Closer(float d, uint citizen, AmbientCitizen other, Vec3 at)
        {
            float od = Vec3.DistanceXZ(other.Position, at);
            return d < od || (d == od && citizen < other.Citizen);
        }

        /// <summary>The live ped promoted from (or created for) a CS1 citizen, or null.</summary>
        public Ped FindPedByCitizen(uint citizen)
        {
            Ped p;
            return citizen != 0 && _pedByCitizen.TryGetValue(citizen, out p) ? p : null;
        }

        /// <summary>
        /// Creates a ped standing at <paramref name="feet"/> facing <paramref name="yawDeg"/>: next id, health 100, armour
        /// 0, no flags, Anim Idle, no brain, InteriorId = the current interior's building id when <paramref name="feet"/>
        /// is below y −1000 and an interior is loaded, else −1. Its motor uses default <see cref="MotorSettings"/>, is
        /// teleported to the feet and masks out the ped's own tag. Appended to <see cref="Peds"/>. No effect or event.
        /// </summary>
        public Ped SpawnPed(PedKind kind, int model, Vec3 feet, float yawDeg)
        {
            int id = _nextId++;
            var motor = new CharacterMotor(new MotorSettings());
            motor.Mask = QueryMask.All.Ignoring(WorldTag.ForPed(id));
            motor.Teleport(feet);
            var ped = new Ped(id, kind, model, motor);
            ped.YawDeg = yawDeg;
            ped.PrevYawDeg = yawDeg;
            ped.PrevPosition = feet;
            if (feet.Y < -1000f && Interior.Building != null) ped.InteriorId = Interior.Building.Id;
            _peds.Add(ped);
            _pedById[id] = ped;
            return ped;
        }

        /// <summary>
        /// Creates a car of model <paramref name="modelId"/> (<c>ICity.GetVehicleModel</c>) standing on
        /// <paramref name="ground"/> (the point under its centre) facing <paramref name="yawDeg"/>: body from
        /// <c>VehicleTuning.FromModel</c>, teleported so its bounds centre is ground + Quat.Yaw(yaw) × model.CenterOffset;
        /// next id; plate from the id; Headlights on at night. Returns null (no id consumed) for an unknown model.
        /// </summary>
        public Car SpawnCar(int modelId, Vec3 ground, float yawDeg, CarOwner owner, int colourRgb)
        {
            VehicleModel model = City.GetVehicleModel(modelId);
            if (model == null) return null;
            Quat rot = Quat.Yaw(yawDeg);
            return CreateCar(model, ground + rot * model.CenterOffset, rot, owner, colourRgb);
        }

        private Car CreateCar(VehicleModel model, Vec3 centre, Quat rotation, CarOwner owner, int colourRgb)
        {
            var body = new VehicleBody(VehicleTuning.FromModel(model));
            body.Teleport(centre, rotation);
            int id = _nextId++;
            var car = new Car(id, model, body);
            car.Owner = owner;
            car.ColourRgb = colourRgb;
            car.Headlights = Clock.IsNight;
            car.Plate = "GS" + (id % 100000).ToString("D5");
            car.PrevPosition = centre;
            car.PrevRotation = rotation;
            _cars.Add(car);
            _carById[id] = car;
            return car;
        }

        /// <summary>
        /// Seats a ped: sets InVehicle, VehicleId, Seat and the car's seat entry; the ped's motor is parked at the seat
        /// and it leaves the collision world from the next rebuild.
        /// </summary>
        /// <exception cref="ArgumentNullException">ped or car is null.</exception>
        /// <exception cref="InvalidOperationException">The seat is outside [0, min(Model.Seats, MaxSeats)) or taken, or the ped is already in a car.</exception>
        public void PutInCar(Ped ped, Car car, int seat)
        {
            if (ped == null) throw new ArgumentNullException("ped");
            if (car == null) throw new ArgumentNullException("car");
            int seats = Math.Min(car.Model.Seats, Car.MaxSeats);
            if (seat < 0 || seat >= seats) throw new InvalidOperationException("no such seat");
            if (car.Seats[seat] != 0) throw new InvalidOperationException("seat taken");
            if (ped.VehicleId != 0) throw new InvalidOperationException("already in a car");
            car.Seats[seat] = ped.Id;
            ped.VehicleId = car.Id;
            ped.Seat = seat;
            ped.Flags |= PedFlags.InVehicle;
            ped.Anim = PedAnim.Sit;
            ped.Motor.Teleport(car.Position);
            car.Body.Wake();
        }

        /// <summary>
        /// Unseats a ped (no-op when on foot): clears InVehicle and the seat, teleports the motor to <paramref name="feet"/>
        /// with the car's velocity, faces <paramref name="yawDeg"/>.
        /// </summary>
        public void TakeOutOfCar(Ped ped, Vec3 feet, float yawDeg)
        {
            if (ped == null || ped.VehicleId == 0) return;
            Car car = FindCar(ped.VehicleId);
            if (car != null && ped.Seat >= 0 && car.Seats[ped.Seat] == ped.Id) car.Seats[ped.Seat] = 0;
            ped.VehicleId = 0;
            ped.Seat = -1;
            ped.Flags &= ~PedFlags.InVehicle;
            ped.Anim = PedAnim.Idle;
            ped.Motor.Teleport(feet);
            if (car != null) ped.Motor.Velocity = car.Body.Velocity.Flat;
            ped.YawDeg = yawDeg;
            ped.PrevPosition = feet;
        }

        /// <summary>
        /// Ground point beside a car's door: local (∓(halfX + <see cref="DoorGap"/>), 0, 0.1 × length) from the point
        /// under the bounds centre, x negative for the left (driver) side, positive for the right; y is that ground point's
        /// height (bounds centre − CenterOffset).
        /// </summary>
        public Vec3 DoorPosition(Car car, bool leftSide)
        {
            return DoorOf(car.Model, car.Position, car.Rotation, leftSide);
        }

        internal static Vec3 DoorOf(VehicleModel model, Vec3 centre, Quat rotation, bool leftSide)
        {
            Vec3 h = model != null ? model.HalfExtents : DefaultHalfExtents;
            Vec3 off = model != null ? model.CenterOffset : new Vec3(0f, h.Y, 0f);
            Vec3 ground = centre - rotation * off;
            float side = h.X + DoorGap;
            return ground + Quat.Yaw(rotation.YawDeg) * new Vec3(leftSide ? -side : side, 0f, 0.2f * h.Z);
        }

        /// <summary>
        /// Removes a ped from the registry (and from its car seat). When <paramref name="returnCitizen"/> is true, the ped
        /// has a Citizen and is not Dead, emits <c>CityEffect.ReturnCitizen</c> with its position, yaw and errand (not
        /// arrived). Removing the player throws <see cref="InvalidOperationException"/>.
        /// </summary>
        public void RemovePed(Ped ped, bool returnCitizen)
        {
            Remove(ped, returnCitizen, false);
        }

        /// <summary>A ped went in at its errand building's door: removed as by <see cref="RemovePed"/>, the return marked arrived.</summary>
        public void ArriveCitizen(Ped ped)
        {
            Remove(ped, true, true);
        }

        private void Remove(Ped ped, bool returnCitizen, bool arrived)
        {
            if (ped == null) return;
            if (ped == Player) throw new InvalidOperationException("the player cannot be removed");
            if (!_pedById.Remove(ped.Id)) return;
            if (ped.VehicleId != 0)
            {
                Car car = FindCar(ped.VehicleId);
                if (car != null && ped.Seat >= 0 && car.Seats[ped.Seat] == ped.Id) car.Seats[ped.Seat] = 0;
            }
            _peds.Remove(ped);
            Ped byCitizen;
            if (ped.Citizen != 0 && _pedByCitizen.TryGetValue(ped.Citizen, out byCitizen) && byCitizen == ped) _pedByCitizen.Remove(ped.Citizen);
            Interior.Occupants.Remove(ped.Id);
            if (returnCitizen && ped.Citizen != 0 && !ped.IsDead)
                Effects.Add(CityEffect.ReturnCitizen(ped.Citizen, ped.Position, ped.YawDeg, ped.ErrandBuilding, ped.Errand, arrived));
        }

        /// <summary>Removes a car; its non-player occupants are removed with it (returnCitizen true). Throws <see cref="InvalidOperationException"/> when the player is inside.</summary>
        public void RemoveCar(Car car)
        {
            if (car == null || !_carById.ContainsKey(car.Id)) return;
            for (int i = 0; i < Car.MaxSeats; i++)
                if (Player != null && car.Seats[i] == Player.Id) throw new InvalidOperationException("the player is inside");
            for (int i = 0; i < Car.MaxSeats; i++)
            {
                if (car.Seats[i] == 0) continue;
                Ped p = FindPed(car.Seats[i]);
                car.Seats[i] = 0;
                if (p != null) RemovePed(p, true);
            }
            _carById.Remove(car.Id);
            _cars.Remove(car);
            if (car.SourceCs1Vehicle != 0) _carBySource.Remove(SourceKey(car.SourceCs1Vehicle, car.SourceParked));
        }

        internal void RegisterCitizen(Ped ped)
        {
            if (ped.Citizen != 0 && !_pedByCitizen.ContainsKey(ped.Citizen)) _pedByCitizen[ped.Citizen] = ped;
        }

        internal void SetPlayer(Ped ped)
        {
            Player = ped;
        }

        private static int SourceKey(ushort id, bool parked) { return (parked ? 0x10000 : 0) | id; }

        internal bool IsPromotedVehicle(ushort id, bool parked) { return _carBySource.ContainsKey(SourceKey(id, parked)); }

        internal bool IsPromotedCitizen(uint citizen) { return citizen != 0 && _pedByCitizen.ContainsKey(citizen); }

        // ---- ambient interop -----------------------------------------------------------------------------------

        /// <summary>True with the snapshot citizen of that CS1 instance (linear search; ≤ a few hundred entries).</summary>
        public bool TryGetAmbientCitizen(ushort instance, out AmbientCitizen citizen)
        {
            List<AmbientCitizen> list = Ambient.Citizens;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Instance == instance) { citizen = list[i]; return true; }
            }
            citizen = default(AmbientCitizen);
            return false;
        }

        /// <summary>True with the snapshot vehicle of that id and parked flag.</summary>
        public bool TryGetAmbientVehicle(ushort id, bool parked, out AmbientVehicle vehicle)
        {
            List<AmbientVehicle> list = Ambient.Vehicles;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Id == id && list[i].Parked == parked) { vehicle = list[i]; return true; }
            }
            vehicle = default(AmbientVehicle);
            return false;
        }

        /// <summary>
        /// Promotes a walking CS1 citizen: creates a <see cref="PedKind.Civilian"/> ped with its model, citizen id,
        /// position, yaw and velocity, gives it <c>Hooks.Brains.Create(world, ped, origin, cause, default)</c>, emits
        /// <c>CityEffect.TakeCitizen(citizen, instance)</c> once. A citizen that already has a ped returns that ped with no
        /// effect. From the next step its snapshot entry no longer gets a capsule.
        /// </summary>
        public Ped PromoteCitizen(AmbientCitizen citizen, PedOrigin origin, int cause)
        {
            Ped existing = FindPedByCitizen(citizen.Citizen);
            if (existing != null) return existing;
            Ped ped = SpawnPed(PedKind.Civilian, citizen.Model, citizen.Position, citizen.YawDeg);
            ped.Citizen = citizen.Citizen;
            ped.ErrandBuilding = citizen.ErrandBuilding;
            ped.Errand = citizen.Errand;
            ped.Motor.Velocity = citizen.Velocity;
            RegisterCitizen(ped);
            Effects.Add(CityEffect.TakeCitizen(citizen.Citizen, citizen.Instance));
            ped.Brain = CreateBrain(ped, origin, cause, default(NpcSpot));
            return ped;
        }

        internal IPedBrain CreateBrain(Ped ped, PedOrigin origin, int cause, NpcSpot spot)
        {
            return Hooks.Brains == null ? null : Hooks.Brains.Create(this, ped, origin, cause, spot);
        }

        /// <summary>
        /// Promotes a CS1 vehicle: emits <c>CityEffect.TakeVehicle(id, parked)</c> once and creates a
        /// <see cref="CarOwner.Civilian"/> car with its model, colour, pose (snapshot position is the bounds centre) and
        /// velocity, SourceCs1Vehicle = id. When <paramref name="withDriver"/> and the vehicle is not parked, a Civilian
        /// driver ped (model: a seeded pick from <c>Rng.Seed(DriverCitizen, Seed)</c> over <c>ICity.CitizenModelCount</c>,
        /// or the session Rng when DriverCitizen is 0; Citizen = DriverCitizen, errand = DriverErrandBuilding/DriverErrand;
        /// a non-zero DriverCitizen is held with <c>CityEffect.TakeCitizen(DriverCitizen, 0)</c>) is seated in seat 0 with brain
        /// <c>Hooks.Brains.Create(world, driver, driverOrigin, cause, default)</c>. Police kinds get Owner Police and a Cop
        /// driver. Returns the existing car when that CS1 vehicle was already promoted; null for an unknown model.
        /// </summary>
        public Car PromoteVehicle(AmbientVehicle vehicle, bool withDriver, PedOrigin driverOrigin, int cause)
        {
            Car existing;
            int key = SourceKey(vehicle.Id, vehicle.Parked);
            if (_carBySource.TryGetValue(key, out existing)) return existing;
            VehicleModel model = City.GetVehicleModel(vehicle.Model);
            if (model == null) return null;
            bool police = model.Kind == VehicleKind.Police || vehicle.Kind == VehicleKind.Police;
            Car car = CreateCar(model, vehicle.Position, vehicle.Rotation, police ? CarOwner.Police : CarOwner.Civilian, vehicle.ColorRgb);
            car.SourceCs1Vehicle = vehicle.Id;
            car.SourceParked = vehicle.Parked;
            car.Headlights = !vehicle.Parked && Clock.IsNight;
            _carBySource[key] = car;
            Effects.Add(CityEffect.TakeVehicle(vehicle.Id, vehicle.Parked));
            if (!vehicle.Parked && vehicle.Velocity.IsFinite) car.Body.SetVelocity(vehicle.Velocity, Vec3.Zero);
            if (withDriver && !vehicle.Parked)
            {
                int count = Math.Max(1, City.CitizenModelCount);
                int driverModel = vehicle.DriverCitizen != 0
                    ? new Rng(Rng.Seed(vehicle.DriverCitizen, Seed)).Range(0, count)
                    : Rng.Range(0, count);
                int policeModel = police ? CitizenModels.Police(City, (uint)driverModel) : -1;
                driverModel = policeModel >= 0 ? policeModel : CitizenModels.Civilian(City, driverModel);
                Ped driver = SpawnPed(police ? PedKind.Cop : PedKind.Civilian, driverModel, car.Position, car.Rotation.YawDeg);
                driver.Citizen = vehicle.DriverCitizen;
                driver.ErrandBuilding = vehicle.DriverErrandBuilding;
                driver.Errand = vehicle.DriverErrand;
                RegisterCitizen(driver);
                if (driver.Citizen != 0) Effects.Add(CityEffect.TakeCitizen(driver.Citizen, 0));
                PutInCar(driver, car, 0);
                driver.Brain = CreateBrain(driver, driverOrigin, cause, default(NpcSpot));
            }
            return car;
        }

        /// <summary>
        /// The Game ped a tag names: a Ped tag → that ped (null if removed); an AmbientCitizen tag → when
        /// <paramref name="promote"/>, the result of <see cref="PromoteCitizen"/>(snapshot entry, PromotedCitizen,
        /// <paramref name="cause"/>), else the already-promoted ped or null; anything else → null.
        /// </summary>
        public Ped PedFromTag(int tag, bool promote, int cause)
        {
            TagKind kind = WorldTag.KindOf(tag);
            if (kind == TagKind.Ped) return FindPed(WorldTag.IdOf(tag));
            if (kind != TagKind.AmbientCitizen) return null;
            AmbientCitizen c;
            if (!TryGetAmbientCitizen((ushort)WorldTag.IdOf(tag), out c)) return null;
            return promote ? PromoteCitizen(c, PedOrigin.PromotedCitizen, cause) : FindPedByCitizen(c.Citizen);
        }

        /// <summary>
        /// The Game car a tag names: a Car tag → that car; an AmbientVehicle/AmbientParkedVehicle tag → when
        /// <paramref name="promote"/>, <see cref="PromoteVehicle"/>(entry, withDriver: true, ShockedDriver,
        /// <paramref name="cause"/>), else the already-promoted car or null; anything else → null.
        /// </summary>
        public Car CarFromTag(int tag, bool promote, int cause)
        {
            TagKind kind = WorldTag.KindOf(tag);
            if (kind == TagKind.Car) return FindCar(WorldTag.IdOf(tag));
            if (kind != TagKind.AmbientVehicle && kind != TagKind.AmbientParkedVehicle) return null;
            ushort id = (ushort)WorldTag.IdOf(tag);
            bool parked = kind == TagKind.AmbientParkedVehicle;
            Car existing;
            if (_carBySource.TryGetValue(SourceKey(id, parked), out existing)) return existing;
            if (!promote) return null;
            AmbientVehicle v;
            return TryGetAmbientVehicle(id, parked, out v) ? PromoteVehicle(v, true, PedOrigin.ShockedDriver, cause) : null;
        }

        /// <summary>World position of whatever a tag names (ped feet, car bounds centre, snapshot entry); false when it no longer exists.</summary>
        public bool TryGetTagPosition(int tag, out Vec3 position)
        {
            position = Vec3.Zero;
            int id = WorldTag.IdOf(tag);
            switch (WorldTag.KindOf(tag))
            {
                case TagKind.Ped:
                    Ped p = FindPed(id);
                    if (p == null) return false;
                    position = p.Position;
                    return true;
                case TagKind.Car:
                    Car c = FindCar(id);
                    if (c == null) return false;
                    position = c.Position;
                    return true;
                case TagKind.AmbientCitizen:
                    AmbientCitizen ac;
                    if (!TryGetAmbientCitizen((ushort)id, out ac)) return false;
                    position = ac.Position;
                    return true;
                case TagKind.AmbientVehicle:
                case TagKind.AmbientParkedVehicle:
                    AmbientVehicle av;
                    if (!TryGetAmbientVehicle((ushort)id, WorldTag.KindOf(tag) == TagKind.AmbientParkedVehicle, out av)) return false;
                    position = av.Position;
                    return true;
                default:
                    return false;
            }
        }

        // ---- per-step helpers ----------------------------------------------------------------------------------

        /// <summary>
        /// Steps a non-player ped's motor with <see cref="Ped.Move"/> (called by the Peds system after the brain).
        /// Skips InVehicle and Dead peds (returns default).
        /// </summary>
        public MotorStepResult MovePed(Ped ped, float dt)
        {
            if (ped == null || ped.IsDead || ped.Has(PedFlags.InVehicle)) return default(MotorStepResult);
            return ped.Motor.Step(dt, ped.Move, Collision, City);
        }

        /// <summary>Clears the dynamic shapes and re-adds them as described on the class (cars only when <paramref name="includeCars"/>).</summary>
        public void RebuildDynamicShapes(bool includeCars)
        {
            Collision.ClearDynamic();
            if (!Interior.IsInside)
            {
                List<AmbientVehicle> vs = Ambient.Vehicles;
                for (int i = 0; i < vs.Count; i++)
                {
                    AmbientVehicle v = vs[i];
                    if (v.Id == 0 || IsPromotedVehicle(v.Id, v.Parked)) continue;
                    VehicleModel m = City.GetVehicleModel(v.Model);
                    Collision.AddBox(v.Position, m != null ? m.HalfExtents : DefaultHalfExtents, v.Rotation, WorldTag.ForAmbientVehicle(v.Id, v.Parked));
                }
                List<AmbientCitizen> cs = Ambient.Citizens;
                for (int i = 0; i < cs.Count; i++)
                {
                    AmbientCitizen c = cs[i];
                    if (c.Instance == 0 || IsPromotedCitizen(c.Citizen)) continue;
                    Collision.AddCapsule(c.Position, AmbientCitizenRadius, AmbientCitizenHeight, WorldTag.ForAmbientCitizen(c.Instance));
                }
            }
            for (int i = 0; i < _peds.Count; i++)
            {
                Ped p = _peds[i];
                if ((p.Flags & (PedFlags.InVehicle | PedFlags.Invisible | PedFlags.Dead)) != 0) continue;
                MotorSettings s = p.Motor.Settings;
                Collision.AddCapsule(p.Position, s.Radius, s.Height, p.Tag);
            }
            if (includeCars) AddCarShapes();
        }

        /// <summary>Adds one box per live Game car at its current pose.</summary>
        public void AddCarShapes()
        {
            for (int i = 0; i < _cars.Count; i++)
            {
                Car c = _cars[i];
                Collision.AddBox(c.Position, c.Body.Tuning.HalfExtents, c.Rotation, c.Tag);
            }
        }

        /// <summary>Appends an event stamped with <c>Clock.GameSeconds</c>; returns its sequence number.</summary>
        public long Emit(GameEventType type, Vec3 position, int actorA, int actorB, float value, string text)
        {
            return Events.Emit(new GameEvent { Type = type, GameTime = Clock.GameSeconds, Position = position, ActorA = actorA, ActorB = actorB, Value = value, Text = text });
        }

        /// <summary>Same as <see cref="Emit(GameEventType, Vec3, int, int, float, string)"/> with a second position (<see cref="GameEventType.Shot"/>).</summary>
        public long Emit(GameEventType type, Vec3 position, Vec3 position2, int actorA, int actorB, float value)
        {
            return Events.Emit(new GameEvent { Type = type, GameTime = Clock.GameSeconds, Position = position, Position2 = position2, ActorA = actorA, ActorB = actorB, Value = value });
        }

        /// <summary>
        /// Damages a Game car by <paramref name="amount"/> health through its body (G5 <c>VehicleBody.ApplyDamage</c>) and
        /// emits <see cref="GameEventType.CarDamaged"/> (A = <paramref name="causeTag"/>, Value = health actually lost)
        /// when health changed. No-op on an exploded car or a non-positive amount.
        /// </summary>
        public void DamageCar(Car car, float amount, int causeTag, Vec3 point)
        {
            if (car == null || amount <= 0f || car.Destroyed) return;
            float before = car.Body.Health;
            car.Body.ApplyDamage(amount);
            float lost = before - car.Body.Health;
            if (lost > 0f) Emit(GameEventType.CarDamaged, point, causeTag, car.Tag, lost, null);
        }

        /// <summary>
        /// True when the player's camera could see <paramref name="point"/>: within 250 m of <see cref="ViewPosition"/>,
        /// inside the view cone (half-angle = 0.5 × <see cref="ViewFovDeg"/> × 16/9, measured as the angle to the view
        /// forward), and a static-only raycast from ViewPosition to the point is clear (NoCamera surfaces ignored).
        /// </summary>
        public bool CanPlayerSee(Vec3 point)
        {
            Vec3 d = point - ViewPosition;
            float dist = d.Length;
            if (dist > SightRange) return false;
            if (dist < 1e-3f) return true;
            Vec3 dir = d / dist;
            float half = 0.5f * ViewFovDeg * 16f / 9f;
            float cos = Vec3.Dot(dir, ViewRotation * Vec3.Forward);
            if (cos < (float)Math.Cos(half * MathUtil.Deg2Rad)) return false;
            PhysicsHit hit;
            QueryMask mask = QueryMask.StaticOnly.Excluding(SurfaceFlags.NoCamera);
            return !Collision.Raycast(ViewPosition, dir, dist - 0.05f, mask, out hit);
        }

        internal void ClearActors()
        {
            for (int i = _cars.Count - 1; i >= 0; i--)
            {
                Car c = _cars[i];
                for (int s = 0; s < Car.MaxSeats; s++) c.Seats[s] = 0;
            }
            _cars.Clear();
            _carById.Clear();
            _carBySource.Clear();
            for (int i = _peds.Count - 1; i >= 0; i--)
                if (_peds[i] != Player) RemovePed(_peds[i], true);
        }
    }
}
