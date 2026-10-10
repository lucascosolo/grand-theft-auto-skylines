using System;
using System.Collections.Generic;
using GtaSkylines.Game.Camera;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Crime;
using GtaSkylines.Game.Hud;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Missions;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Settings;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Session
{
    /// <summary>
    /// One play session in one city: owns the <see cref="GameWorld"/>, the systems, the <see cref="CameraRig"/> and the
    /// <see cref="HudModel"/>, and runs them at a fixed 60 Hz step.
    /// <para><b>Systems.</b> The constructor adds G6's systems: Ambient (5), PlayerControl (10), Vehicles (20),
    /// Interiors (40), Respawn (90), the camera rig (95) and the HUD system (100). Later chunks add theirs with
    /// <see cref="AddSystem"/> (orders in <see cref="SystemOrder"/>).</para>
    /// <para><b>Ticking.</b> <see cref="Tick"/> copies the snapshot into <c>World.Ambient</c>, adds realDt to an
    /// accumulator (capped at <see cref="MaxStepsPerTick"/> × <see cref="FixedDt"/>, so a long frame drops time rather than
    /// spiralling) and runs whole steps while it holds ≥ FixedDt, at most MaxStepsPerTick, carrying the remainder.
    /// Each step: Clock.Hour from the city; <c>World.Input</c> = the latest frame's Held and axes, plus every Pressed and
    /// Released bit and look delta accumulated since the last step (so an edge reaches exactly one step even when a
    /// frame runs no step; later steps of the same Tick get no edges and no look delta); then every system in order.
    /// Simulation systems (order &lt; 95) get dt = FixedDt × min(<see cref="TimeScale"/>, the <c>Hooks.TimeScale</c> source clamped to [0.05, 1]) and are skipped while
    /// <see cref="Paused"/>; presentation systems always run with dt = FixedDt. Clock.GameSeconds and Clock.Steps advance
    /// only with simulation; Clock.RealSeconds by realDt. After the steps: <see cref="Interpolation"/> = remainder ÷
    /// FixedDt, the camera rig interpolates its outputs, and the world's effects are drained into
    /// <see cref="Effects"/>.</para>
    /// <para><b>On foot</b> (PlayerControl, constants in <see cref="PlayerRules"/>): look input adds to
    /// PlayerState.ViewYawDeg/ViewPitchDeg; the move vector (MoveX, MoveY) clamped to length 1 is rotated by the view yaw
    /// (camera-relative) and scaled by the speed: Sprint held with stamina &gt; 0 and MoveY &gt; 0 → 6.8 (drains 18/s;
    /// at 0 the sprint stops until Sprint is pressed again), sneaking → 1.6, else 3.6. Stamina refills 25/s once 1 s has
    /// passed without sprinting. Facing: in third person it turns toward the move direction at 720°/s; in first person and
    /// while aiming (Aim held) it equals the view yaw. Jump pressed → motor jump or ledge climb. Sneak pressed toggles
    /// sneaking (flag Sneaking). HandsUp held sets HandsUp (and emits HandsUp once when it starts). A landing with impact
    /// speed v &gt; 9 costs (v − 9) × 9 health (armour not used) and emits Fell. Anim: Idle / Walk (≤ 2 m/s) / Run /
    /// Sprint, Fall when airborne, Climb, Swim, HandsUp, Aim. Distance walked feeds the stats.</para>
    /// <para><b>Interaction targeting</b> each step, first match wins: in a car → ExitVehicle; on foot → the nearest car
    /// door within 3 m whose direction is within ±60° of the facing (a parked ambient vehicle, a moving ambient one under
    /// 8 m/s, or an unlocked Game car) → "Press {EnterExit} to enter &lt;model name&gt;"; inside an interior → the
    /// nearest interaction point (layout or custom) within 1.2 m; outside → the nearest door of
    /// <c>ICity.BuildingsNear</c> within 2.5 m of a building that is not Abandoned → "Press {Interact} to enter
    /// &lt;building name&gt;"; a person (Game ped or ambient citizen) within 2.5 m in the front half (±90°) → "Press {Interact} to talk".
    /// The prompt is shown as help (priority 1, this step only) and stored in PlayerState.Interaction.</para>
    /// <para><b>Entering a car</b> (EnterExit pressed on an EnterVehicle target): WalkToDoor moves the player toward the
    /// driver door (≤ 1.2 s; any move input cancels), then: parked ambient → PromoteVehicle (TakeVehicle parked),
    /// CarStolen; moving ambient → PromoteVehicle with its driver (TakeVehicle), the driver is unseated at the door with
    /// origin CarjackedDriver, CarJacked, PullOut 1.6 s; Game car with a driver ped → same pull-out; then GetIn 0.6 s and
    /// the player is seated (InVehicle, removed from collision), Owner Player, PlayerEnteredCar, vehicles-stolen stat on
    /// steals and jacks, the vehicle name banner. EnterExit while driving: below 3 m/s exit at the driver door (else the
    /// right door, else the nearest free spot found by an overlap test), above 8 m/s bail (5 damage, roll 0.8 s, the
    /// player keeps the car's velocity), between 3 and 8 the car brakes until below 3. On exit the car becomes Abandoned
    /// (PlayerExitedCar).</para>
    /// <para><b>Driving</b> (Vehicles): controls from input: Throttle = max(MoveY, 0), Brake = max(−MoveY, 0) (the body
    /// reverses when stopped), Steer = MoveX, Handbrake = Jump held. Interact: on a siren model a tap (&lt; 0.3 s)
    /// toggles siren and light bar; otherwise held sounds the horn. Headlights pressed toggles lights. Impacts from the
    /// step result: an ambient vehicle box at ≥ 6 m/s → PromoteVehicle(withDriver, ShockedDriver) and CarCollision; a
    /// capsule → PedHitByVehicle (ambient citizen tags are not promoted here; G7 promotes); Game-vs-Game contacts → CarCollision.
    /// Health lost → CarDamaged. Exploded → CarDestroyed and Explosion (radius 8); a player inside dies (PedKilled,
    /// health 0). Every other car is driven by its <see cref="IDriver"/>; a car with neither
    /// brakes (handbrake on when nobody sits at the wheel). Stamina does not refill while Sprint stays held after it ran
    /// out.</para>
    /// <para><b>Promotion and demotion</b> (Ambient): each step a promoted ped that is not Persistent, not the player,
    /// not in a car driven by the player, and whose brain is null or inactive is demoted (RemovePed, ReturnCitizen when
    /// alive) when farther than 150 m, or farther than 60 m and not visible (<c>CanPlayerSee</c> of its head) — no grace
    /// period. Dead or Injured peds are removed (no ReturnCitizen) beyond 120 m. Abandoned cars farther than 300 m and not
    /// visible for 10 s are removed. Mission and Police cars are never removed by this system.</para>
    /// <para><b>Interiors</b>: Interact on a BuildingDoor → <c>InteriorGenerator.Generate(building)</c>; null → help
    /// "Closed" (priority 2, 3 s) and no transition. Otherwise FadingOut 0.4 s (input ignored), then the
    /// layout's triangles are loaded as region <see cref="InteriorState.RegionKey"/> (world-space via
    /// <see cref="InteriorState.ToWorld"/>, flags Interior), the player is teleported to ToWorld(EntranceSpawn) facing
    /// EntranceSpawnYawDeg + YawDeg, occupants are created as Npc peds at the NpcSpots (models: residents for
    /// homes / workers for other buildings from <c>ICity.Occupants</c> in list order, then seeded random) with
    /// <c>Hooks.Brains</c> origin InteriorOccupant, EnteredInterior is emitted (Trespass too for a Residential building
    /// not owned), FadingIn 0.4 s. ExitDoor → fade out, interior region removed, occupants removed, player at
    /// <see cref="InteriorState.Door"/> + 1 m outward facing away from the building, ExitedInterior. ElevatorDoor in a
    /// lobby → the Apartment variant 1 + building id % 20 at ElevatorSpawn (an apartment has no elevator door: its ExitDoor
    /// leads back to the lobby's ElevatorSpawn). ShopCounter/HospitalDesk/PoliceDesk/RespraySpot → a Shop <see cref="MenuRequest"/>; Bed in an owned
    /// building → Bed menu; Wardrobe in an owned building → Wardrobe menu.</para>
    /// <para><b>Death and arrest</b> (Respawn): player health ≤ 0 → Dead, PlayerDied, Wasted screen 4 s, deaths stat.
    /// <c>Wanted.BustedRequested</c> → PlayerArrested, Busted screen 4 s, arrests stat. Then: fade, the player leaves any
    /// car and interior, respawns at the door of the nearest <c>ICity.ServiceBuildings(Health)</c> (Busted: Police)
    /// building to where they fell, else at the Begin position, health 100, armour kept (Busted: 0), flags cleared;
    /// Wallet.Fine(500, "Hospital") or Fine(500, "Bail") takes what there is; Busted also strips weapons
    /// (<c>Profile.StripWeapons</c>); Wasted clears the current weapon's ammo (<c>Profile.SetAmmo(current, 0)</c>);
    /// <c>Wanted.Reset()</c>; PlayerRespawned.</para>
    /// <para>Steady-state Tick allocates nothing.</para>
    /// </summary>
    public sealed class GameSession
    {
        /// <summary>Fixed simulation step, seconds.</summary>
        public const float FixedDt = 1f / 60f;
        /// <summary>Most fixed steps per Tick.</summary>
        public const int MaxStepsPerTick = 5;

        private readonly List<IGameSystem> _systems = new List<IGameSystem>();
        private readonly List<CityEffect> _effects = new List<CityEffect>();
        private readonly PlayerControlSystem _control;
        private readonly InteriorSystem _interiors;
        private double _acc;
        private uint _pendingPressed, _pendingReleased;
        private float _pendingYaw, _pendingPitch;
        private float _timeScale = 1f;

        /// <summary>A session with <see cref="GameWorld.DefaultSeed"/>.</summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public GameSession(ICity city, GameSettings settings, PlayerProfile profile)
            : this(city, settings, profile, GameWorld.DefaultSeed)
        {
        }

        /// <summary>A session with an explicit random seed (tests, self-test); <c>World.Seed</c> is this value.</summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public GameSession(ICity city, GameSettings settings, PlayerProfile profile, uint seed)
        {
            World = new GameWorld(city, settings, profile, seed);
            Camera = new CameraRig(settings.FootView, settings.VehicleView);
            Hud = new HudModel();
            _interiors = new InteriorSystem(this);
            _control = new PlayerControlSystem(Camera, _interiors);
            AddSystem(new AmbientSystem());
            AddSystem(_control);
            AddSystem(new VehicleSystem(_control));
            AddSystem(_interiors);
            AddSystem(new RespawnSystem(this, _interiors));
            AddSystem(Camera);
            AddSystem(new HudSystem(Hud) { Rig = Camera });
        }

        /// <summary>The world.</summary>
        public GameWorld World { get; private set; }
        /// <summary>The camera.</summary>
        public CameraRig Camera { get; private set; }
        /// <summary>The HUD model (filled every presentation step).</summary>
        public HudModel Hud { get; private set; }
        /// <summary>True between <see cref="Begin"/> and <see cref="End"/>.</summary>
        public bool Running { get; private set; }
        /// <summary>Pause menu open: simulation systems stop, presentation systems keep running.</summary>
        public bool Paused { get; set; }
        /// <summary>Simulation speed factor, clamped to 0..1, default 1. A <c>Hooks.TimeScale</c> source can lower it further.</summary>
        public float TimeScale
        {
            get { return _timeScale; }
            set { _timeScale = MathUtil.Clamp01(value); }
        }
        /// <summary>Fraction 0..1 of a step left in the accumulator after the last Tick, for rendering between steps.</summary>
        public float Interpolation { get; private set; }
        /// <summary>Fixed steps run by the last Tick (0..5).</summary>
        public int StepsLastTick { get; private set; }
        /// <summary>City effects produced by the last Tick, oldest first (replaced at each Tick).</summary>
        public IList<CityEffect> Effects { get { return _effects; } }
        /// <summary>True after the player chose Save at a safehouse bed until <see cref="ConsumeSaveRequest"/>.</summary>
        public bool SaveRequested { get; internal set; }

        internal Vec3 BeginPosition { get; private set; }

        /// <summary>
        /// Adds a system; it runs from the next step in order (stable among equal orders). Once a <see cref="CrimeSystem"/>
        /// and a <see cref="MissionRunner"/> are both added, the runner's wanted writes go through a <see cref="CrimeWantedControl"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="system"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The same instance was already added.</exception>
        public void AddSystem(IGameSystem system)
        {
            if (system == null) throw new ArgumentNullException("system");
            if (_systems.Contains(system)) throw new InvalidOperationException("system already added");
            int order = system.Order;
            int at = _systems.Count;
            while (at > 0 && _systems[at - 1].Order > order) at--;
            _systems.Insert(at, system);
            WireWantedControl();
        }

        /// <summary>Missions report wanted changes through the crime system once both are present (unless set explicitly).</summary>
        private void WireWantedControl()
        {
            CrimeSystem crime = null;
            MissionRunner runner = null;
            for (int i = 0; i < _systems.Count; i++)
            {
                if (crime == null) crime = _systems[i] as CrimeSystem;
                if (runner == null) runner = _systems[i] as MissionRunner;
            }
            if (crime != null && runner != null && runner.WantedControl == DirectWantedControl.Instance)
                runner.WantedControl = new CrimeWantedControl(crime);
        }

        /// <summary>
        /// Starts play: creates the player ped (model = Profile.OutfitId when in [0, CitizenModelCount) else 0,
        /// health and armour from the profile; a profile health of 0 starts at 100) standing at <paramref name="feet"/>
        /// facing <paramref name="yawDeg"/> (view yaw the same, pitch 0), clears the blackboards, stamina 100, camera views
        /// from <c>Settings.FootView/VehicleView</c>. Feet are where the Mod found walkable ground; the motor settles them.
        /// </summary>
        /// <exception cref="InvalidOperationException">Already running.</exception>
        public void Begin(Vec3 feet, float yawDeg)
        {
            if (Running) throw new InvalidOperationException("already running");
            GameWorld w = World;
            w.ClearActors();
            w.Mission.Clear();
            w.Wanted.Reset();
            w.Messages.Clear();
            w.Menu.Close();
            w.HasWaypoint = false;
            w.FadeAlpha = 0f;
            w.Clock.Hour = w.City.TimeOfDayHours;
            PlayerProfile prof = w.Profile;
            int model = prof.OutfitId >= 0 && prof.OutfitId < w.City.CitizenModelCount ? prof.OutfitId : 0;
            Ped p = w.Player;
            if (p == null)
            {
                p = w.SpawnPed(PedKind.Player, model, feet, yawDeg);
                w.SetPlayer(p);
            }
            else
            {
                p.Model = model;
                p.Motor.Teleport(feet);
                p.YawDeg = yawDeg;
                p.Flags = PedFlags.None;
            }
            p.Health = prof.Health <= 0 ? 100f : prof.Health;
            p.Armour = prof.Armour;
            PlayerState ps = w.PlayerState;
            ps.Stamina = PlayerRules.MaxStamina;
            ps.ViewYawDeg = yawDeg;
            ps.ViewPitchDeg = 0f;
            ps.Life = LifeState.Alive;
            ps.Transition = VehicleTransition.None;
            Camera.FootView = (FootView)MathUtil.Clamp(w.Settings.FootView, 0, 3);
            Camera.VehicleView = (VehicleView)MathUtil.Clamp(w.Settings.VehicleView, 0, 3);
            BeginPosition = feet;
            _acc = 0;
            _pendingPressed = _pendingReleased = 0;
            _pendingYaw = _pendingPitch = 0f;
            Running = true;
        }

        /// <summary>Advances the session by a real frame (see the class). <paramref name="snapshot"/> may be null (keeps the last one).</summary>
        /// <exception cref="InvalidOperationException">Not running.</exception>
        public void Tick(float realDt, InputFrame input, CitySnapshot snapshot)
        {
            if (!Running) throw new InvalidOperationException("not running");
            GameWorld w = World;
            _effects.Clear();
            if (snapshot != null && snapshot != w.Ambient)
            {
                CitySnapshot a = w.Ambient;
                a.Citizens.Clear();
                a.Citizens.AddRange(snapshot.Citizens);
                a.Vehicles.Clear();
                a.Vehicles.AddRange(snapshot.Vehicles);
                a.Center = snapshot.Center;
                a.Radius = snapshot.Radius;
            }
            if (!(realDt > 0f)) realDt = 0f;
            w.Clock.RealSeconds += realDt;
            _pendingPressed |= input.Pressed;
            _pendingReleased |= input.Released;
            _pendingYaw += input.LookYawDeg;
            _pendingPitch += input.LookPitchDeg;

            _acc = Math.Min(_acc + realDt, MaxStepsPerTick * (double)FixedDt);
            int steps = (int)Math.Floor(_acc / FixedDt + 1e-4);
            if (steps > MaxStepsPerTick) steps = MaxStepsPerTick;
            _acc = Math.Max(0.0, _acc - steps * (double)FixedDt);

            float scale = _timeScale;
            ITimeScaleSource src = w.Hooks.TimeScale;
            if (src != null) scale = Math.Min(scale, MathUtil.Clamp(src.DesiredTimeScale, 0.05f, 1f));

            for (int s = 0; s < steps; s++)
            {
                InputFrame f = input;
                f.Pressed = _pendingPressed;
                f.Released = _pendingReleased;
                f.LookYawDeg = _pendingYaw;
                f.LookPitchDeg = _pendingPitch;
                _pendingPressed = _pendingReleased = 0;
                _pendingYaw = _pendingPitch = 0f;
                w.Input = f;
                RunStep(w, scale);
            }
            StepsLastTick = steps;
            Interpolation = MathUtil.Clamp01((float)(_acc / FixedDt));
            Camera.Present(Interpolation);
            w.Effects.DrainTo(_effects);
        }

        private void RunStep(GameWorld w, float scale)
        {
            bool simulate = !Paused;
            w.Clock.Hour = w.City.TimeOfDayHours;
            float simDt = FixedDt * scale;
            if (simulate)
            {
                List<Ped> peds = w.PedList;
                for (int i = 0; i < peds.Count; i++) { peds[i].PrevPosition = peds[i].Position; peds[i].PrevYawDeg = peds[i].YawDeg; }
                List<Car> cars = w.CarList;
                for (int i = 0; i < cars.Count; i++) { cars[i].PrevPosition = cars[i].Position; cars[i].PrevRotation = cars[i].Rotation; }
                w.Clock.Steps++;
                w.Clock.GameSeconds += simDt;
            }
            for (int i = 0; i < _systems.Count; i++)
            {
                IGameSystem sys = _systems[i];
                bool presentation = sys.Order >= SystemOrder.Presentation;
                if (!presentation && !simulate) continue;
                sys.Step(w, presentation ? FixedDt : simDt);
            }
        }

        /// <summary>
        /// Answers the open menu. Shop: <c>Shop.Buy(Wallet, menu.Shop, Items[index].Id)</c> with the player's health and
        /// armour synced into the profile first; on Ok emits Purchase, applies Respray to the player's current car or last
        /// car within 40 m of the building (new colour from the session Rng, Wanted.Description.CarColour updated when it
        /// names that car, Resprayed event), and syncs the player ped's health/armour/model from the profile. Property:
        /// <c>Property.Buy</c>, closing on Ok. Bed: 0 Save (<see cref="SaveRequested"/>, last safehouse set), 1 Sleep
        /// (AdvanceTime 6 h effect, Slept, health 100), 2 close. Wardrobe: sets the outfit (profile and ped model),
        /// OutfitChanged. Shop menus stay open after a purchase. Returns the purchase result (Ok for non-purchase options);
        /// <see cref="PurchaseResult.UnknownItem"/> for a bad index or no open menu.
        /// </summary>
        public PurchaseResult ChooseMenuOption(int index)
        {
            return _interiors.Choose(World, index);
        }

        /// <summary>Closes the open menu (no-op when none).</summary>
        public void CloseMenu()
        {
            World.Menu.Close();
        }

        /// <summary>Sets the map waypoint (the GPS routes to it unless a mission GPS target is set).</summary>
        public void SetWaypoint(Vec3 position)
        {
            World.HasWaypoint = true;
            World.Waypoint = position;
        }

        /// <summary>Clears the waypoint.</summary>
        public void ClearWaypoint()
        {
            World.HasWaypoint = false;
        }

        /// <summary>Returns and clears <see cref="SaveRequested"/>.</summary>
        public bool ConsumeSaveRequest()
        {
            bool r = SaveRequested;
            SaveRequested = false;
            return r;
        }

        /// <summary>
        /// A 32-bit FNV-1a digest of the simulation state for determinism tests: clock steps, every ped's id, position,
        /// velocity, yaw, health, armour and flags, every car's id, position, rotation, velocity and health, player stamina,
        /// stars, cash, and the event sequence number. Same seed and inputs → same value.
        /// </summary>
        public uint StateHash()
        {
            GameWorld w = World;
            var h = new Fnv1a();
            h.Add(w.Clock.Steps);
            List<Ped> peds = w.PedList;
            for (int i = 0; i < peds.Count; i++)
            {
                Ped p = peds[i];
                h.Add((long)p.Id);
                h.Add(p.Position);
                h.Add(p.Motor.Velocity);
                h.Add(p.YawDeg);
                h.Add(p.Health);
                h.Add(p.Armour);
                h.Add((long)p.Flags);
            }
            List<Car> cars = w.CarList;
            for (int i = 0; i < cars.Count; i++)
            {
                Car c = cars[i];
                Quat q = c.Rotation;
                h.Add((long)c.Id);
                h.Add(c.Position);
                h.Add(q.X); h.Add(q.Y); h.Add(q.Z); h.Add(q.W);
                h.Add(c.Body.Velocity);
                h.Add(c.Body.Health);
            }
            h.Add(w.PlayerState.Stamina);
            h.Add((long)w.Wanted.Stars);
            h.Add(w.Profile.Cash);
            h.Add(w.Events.NextSeq);
            return h.Value;
        }

        /// <summary>
        /// Ends play and returns the profile: health and armour from the player ped (rounded, a dead player saves 100),
        /// outfit = the player's model, pending distance flushed into the stats; the interior region is removed and the
        /// player leaves any car. Position is not kept (the last safehouse is). Idempotent after the first call.
        /// </summary>
        public PlayerProfile End()
        {
            GameWorld w = World;
            if (!Running) return w.Profile;
            Running = false;
            Ped p = w.Player;
            _control.FlushDistance(w);
            if (p.VehicleId != 0)
            {
                Car car = w.FindCar(p.VehicleId);
                if (car != null && p.Seat >= 0 && car.Seats[p.Seat] == p.Id) car.Seats[p.Seat] = 0;
                p.VehicleId = 0;
                p.Seat = -1;
                p.Flags &= ~PedFlags.InVehicle;
            }
            _interiors.Unload(w);
            return ProfileForSave();
        }

        /// <summary>
        /// Copies the live player's health and armour (rounded; a dead player saves 100 health) and outfit into the profile,
        /// which holds them only for saving, and returns it. The Mod calls it before writing a save.
        /// </summary>
        public PlayerProfile ProfileForSave()
        {
            GameWorld w = World;
            Ped p = w.Player;
            if (p == null) return w.Profile;
            w.Profile.Health = p.IsDead || p.Health <= 0f ? PlayerProfile.MaxVital : (int)Math.Round(p.Health);
            w.Profile.Armour = (int)Math.Round(p.Armour);
            w.Profile.OutfitId = p.Model;
            return w.Profile;
        }
    }
}
