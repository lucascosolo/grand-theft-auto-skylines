using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Roads;
using GtaSkylines.Game.Settings;

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

        /// <summary>Creates an empty world (no player until <c>GameSession.Begin</c>).</summary>
        /// <exception cref="ArgumentNullException">city, settings or profile is null.</exception>
        public GameWorld(ICity city, GameSettings settings, PlayerProfile profile, uint seed)
        {
            throw new NotImplementedException();
        }

        // ---- inputs and services -------------------------------------------------------------------------------

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
        /// <summary>Effects for the city produced this tick (drained by <c>GameSession</c> at the end of each Tick).</summary>
        public CityEffectQueue Effects { get; private set; }
        /// <summary>Event buffer.</summary>
        public GameEvents Events { get; private set; }
        /// <summary>Clock.</summary>
        public GameClock Clock { get; private set; }
        /// <summary>The session's deterministic random source (systems may also derive their own with <c>Rng.Seed</c>).</summary>
        public Rng Rng { get; private set; }
        /// <summary>Extension points (brains, dialogue).</summary>
        public GameHooks Hooks { get; private set; }
        /// <summary>Input for the current fixed step (pressed edges appear in exactly one step).</summary>
        public InputFrame Input;
        /// <summary>The latest city snapshot (copied in by <c>GameSession.Tick</c>; empty lists while inside an interior).</summary>
        public CitySnapshot Ambient { get; private set; }

        // ---- actors --------------------------------------------------------------------------------------------

        /// <summary>The player ped (null before Begin).</summary>
        public Ped Player { get; private set; }
        /// <summary>Player-only state.</summary>
        public PlayerState PlayerState { get; private set; }
        /// <summary>Live peds in creation order (player first).</summary>
        public IList<Ped> Peds { get { throw new NotImplementedException(); } }
        /// <summary>Live cars in creation order.</summary>
        public IList<Car> Cars { get { throw new NotImplementedException(); } }

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
            throw new NotImplementedException();
        }

        /// <summary>The live car with this id, or null.</summary>
        public Car FindCar(int id)
        {
            throw new NotImplementedException();
        }

        /// <summary>The live ped promoted from a CS1 citizen, or null.</summary>
        public Ped FindPedByCitizen(uint citizen)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a ped standing at <paramref name="feet"/> facing <paramref name="yawDeg"/>: next id, health 100, armour
        /// 0, no flags, Anim Idle, no brain, InteriorId = the current interior's building id when <paramref name="feet"/>
        /// is below y −1000 and an interior is loaded, else −1. Its motor uses default <see cref="MotorSettings"/>, is
        /// teleported to the feet and masks out the ped's own tag. Appended to <see cref="Peds"/>. No effect or event.
        /// </summary>
        public Ped SpawnPed(PedKind kind, int model, Vec3 feet, float yawDeg)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a car of model <paramref name="modelId"/> (<c>ICity.GetVehicleModel</c>) standing on
        /// <paramref name="ground"/> (the point under its centre) facing <paramref name="yawDeg"/>: body from
        /// <c>VehicleTuning.FromModel</c>, teleported so its bounds centre is ground + Quat.Yaw(yaw) × model.CenterOffset;
        /// next id; plate from the id; Headlights on at night. Returns null (no id consumed) for an unknown model.
        /// </summary>
        public Car SpawnCar(int modelId, Vec3 ground, float yawDeg, CarOwner owner, int colourRgb)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Seats a ped: sets InVehicle, VehicleId, Seat and the car's seat entry; the ped's motor is parked at the seat
        /// and it leaves the collision world from the next rebuild.
        /// </summary>
        /// <exception cref="ArgumentNullException">ped or car is null.</exception>
        /// <exception cref="InvalidOperationException">The seat is outside [0, min(Model.Seats, MaxSeats)) or taken, or the ped is already in a car.</exception>
        public void PutInCar(Ped ped, Car car, int seat)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Unseats a ped (no-op when on foot): clears InVehicle and the seat, teleports the motor to <paramref name="feet"/>
        /// with the car's velocity, faces <paramref name="yawDeg"/>.
        /// </summary>
        public void TakeOutOfCar(Ped ped, Vec3 feet, float yawDeg)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Ground point beside a car's door: local (∓(halfX + <see cref="DoorGap"/>), 0, 0.1 × length) from the point
        /// under the bounds centre, x negative for the left (driver) side, positive for the right; y is that ground point's
        /// height (bounds centre − CenterOffset).
        /// </summary>
        public Vec3 DoorPosition(Car car, bool leftSide)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Removes a ped from the registry (and from its car seat). When <paramref name="returnCitizen"/> is true, the ped
        /// has a Citizen and is not Dead, emits <c>CityEffect.ReturnCitizen(citizen, position)</c>. Removing the player
        /// throws <see cref="InvalidOperationException"/>.
        /// </summary>
        public void RemovePed(Ped ped, bool returnCitizen)
        {
            throw new NotImplementedException();
        }

        /// <summary>Removes a car; its non-player occupants are removed with it (returnCitizen true). Throws <see cref="InvalidOperationException"/> when the player is inside.</summary>
        public void RemoveCar(Car car)
        {
            throw new NotImplementedException();
        }

        // ---- ambient interop -----------------------------------------------------------------------------------

        /// <summary>True with the snapshot citizen of that CS1 instance (linear search; ≤ a few hundred entries).</summary>
        public bool TryGetAmbientCitizen(ushort instance, out AmbientCitizen citizen)
        {
            throw new NotImplementedException();
        }

        /// <summary>True with the snapshot vehicle of that id and parked flag.</summary>
        public bool TryGetAmbientVehicle(ushort id, bool parked, out AmbientVehicle vehicle)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Promotes a walking CS1 citizen: creates a <see cref="PedKind.Civilian"/> ped with its model, citizen id,
        /// position, yaw and velocity, gives it <c>Hooks.Brains.Create(world, ped, origin, cause, default)</c>, emits
        /// <c>CityEffect.TakeCitizen(citizen, instance)</c> once. A citizen that already has a ped returns that ped with no
        /// effect. From the next step its snapshot entry no longer gets a capsule.
        /// </summary>
        public Ped PromoteCitizen(AmbientCitizen citizen, PedOrigin origin, int cause)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Promotes a CS1 vehicle: emits <c>CityEffect.TakeVehicle(id, parked)</c> once and creates a
        /// <see cref="CarOwner.Civilian"/> car with its model, colour, pose (snapshot position is the bounds centre) and
        /// velocity, SourceCs1Vehicle = id. When <paramref name="withDriver"/> and the vehicle is not parked, a Civilian
        /// driver ped (model: a seeded pick from <c>Rng.Seed(DriverCitizen)</c> over <c>ICity.CitizenModelCount</c>, or the
        /// session Rng when DriverCitizen is 0; Citizen = DriverCitizen) is seated in seat 0 with brain
        /// <c>Hooks.Brains.Create(world, driver, driverOrigin, cause, default)</c>. Police kinds get Owner Police and a Cop
        /// driver. Returns the existing car when that CS1 vehicle was already promoted.
        /// </summary>
        public Car PromoteVehicle(AmbientVehicle vehicle, bool withDriver, PedOrigin driverOrigin, int cause)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// The Game ped a tag names: a Ped tag → that ped (null if removed); an AmbientCitizen tag → when
        /// <paramref name="promote"/>, the result of <see cref="PromoteCitizen"/>(snapshot entry, PromotedCitizen,
        /// <paramref name="cause"/>), else the already-promoted ped or null; anything else → null.
        /// </summary>
        public Ped PedFromTag(int tag, bool promote, int cause)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// The Game car a tag names: a Car tag → that car; an AmbientVehicle/AmbientParkedVehicle tag → when
        /// <paramref name="promote"/>, <see cref="PromoteVehicle"/>(entry, withDriver: true, ShockedDriver,
        /// <paramref name="cause"/>), else the already-promoted car or null; anything else → null.
        /// </summary>
        public Car CarFromTag(int tag, bool promote, int cause)
        {
            throw new NotImplementedException();
        }

        /// <summary>World position of whatever a tag names (ped feet, car bounds centre, snapshot entry); false when it no longer exists.</summary>
        public bool TryGetTagPosition(int tag, out Vec3 position)
        {
            throw new NotImplementedException();
        }

        // ---- per-step helpers ----------------------------------------------------------------------------------

        /// <summary>
        /// Steps a non-player ped's motor with <see cref="Ped.Move"/> (called by the Peds system after the brain).
        /// Skips InVehicle and Dead peds (returns default).
        /// </summary>
        public MotorStepResult MovePed(Ped ped, float dt)
        {
            throw new NotImplementedException();
        }

        /// <summary>Clears the dynamic shapes and re-adds them as described on the class (cars only when <paramref name="includeCars"/>).</summary>
        public void RebuildDynamicShapes(bool includeCars)
        {
            throw new NotImplementedException();
        }

        /// <summary>Adds one box per live Game car at its current pose.</summary>
        public void AddCarShapes()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Appends an event stamped with <c>Clock.GameSeconds</c>; returns its sequence number.
        /// </summary>
        public long Emit(GameEventType type, Vec3 position, int actorA, int actorB, float value, string text)
        {
            throw new NotImplementedException();
        }

        /// <summary>Same as <see cref="Emit(GameEventType, Vec3, int, int, float, string)"/> with a second position (<see cref="GameEventType.Shot"/>).</summary>
        public long Emit(GameEventType type, Vec3 position, Vec3 position2, int actorA, int actorB, float value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Damages a Game car by <paramref name="amount"/> health through its body (G5 <c>VehicleBody.ApplyDamage</c>) and
        /// emits <see cref="GameEventType.CarDamaged"/> (A = <paramref name="causeTag"/>, Value = health actually lost)
        /// when health changed. No-op on an exploded car.
        /// </summary>
        public void DamageCar(Car car, float amount, int causeTag, Vec3 point)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// True when the player's camera could see <paramref name="point"/>: within 250 m of <see cref="ViewPosition"/>,
        /// inside the view cone (half-angle = 0.5 × <see cref="ViewFovDeg"/> × 16/9 horizontally, measured as the angle to
        /// the view forward ≤ that), and a static-only raycast from ViewPosition to the point is clear (NoCamera surfaces
        /// ignored). Used for demotion, despawning and spawning out of view.
        /// </summary>
        public bool CanPlayerSee(Vec3 point)
        {
            throw new NotImplementedException();
        }
    }
}
