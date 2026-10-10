using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Crime;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Roads;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Police
{
    /// <summary>
    /// Pursuit, search, evasion and the police units (F-HEAT-4/5/7/8/9), order <see cref="SystemOrder.Police"/> (70),
    /// after <see cref="CrimeSystem"/>. Each step, in this order:
    /// <list type="number">
    /// <item>Events (own cursor): PlayerDied or PlayerRespawned → every unit despawns (pursuit over; G6 resets
    /// <c>WantedStatus</c>); EnteredInterior while a unit recognised the player within the last 2 s → LastKnownPosition =
    /// the door (event Position).</item>
    /// <item>Recognition: for every unit's observer (car occupants, cops on foot, helicopter)
    /// <see cref="Recognition.Sees"/>. Any recognition while wanted → LastKnownPosition = player position,
    /// HasLastKnownPosition = true, SecondsSinceSeen = 0, Description = <see cref="Recognition.Describe"/>. Not wanted and
    /// a patrol recognises (heat rules in <see cref="Recognition"/>) → Stars = 1 with no heat added (radio Spotted).</item>
    /// <item>Evasion while wanted: SecondsSinceSeen += dt unless seen this step; Evading = SecondsSinceSeen ≥
    /// <see cref="SearchZone.EvadeAfterSeconds"/>; while Evading SearchCentre = LastKnownPosition and SearchRadius =
    /// <see cref="SearchZone.Radius"/>(SecondsSinceSeen), else SearchRadius = 0. When SecondsSinceSeen ≥
    /// <see cref="SearchZone.SearchSeconds"/>(Stars, district heat at LastKnownPosition): Stars = 0, Evading = false,
    /// SearchRadius = 0, HasLastKnownPosition = false, Description unknown (heat kept), radio Cleared, units Leave.</item>
    /// <item>Interiors at 4★+: the player inside for U[20, 40) s (seeded per entry) → two cops spawn at the interior's
    /// EntranceSpawn with Search brains, event CopsEnteredInterior (Value = building id).</item>
    /// <item>Budget (<see cref="Dispatcher.BudgetFor"/>) vs units: below budget → after the response delay
    /// (<see cref="Dispatcher.ResponseDelay"/>, timed from the step Stars went 0 → ≥ 1) one unit per
    /// <see cref="Dispatcher.ReinforcementInterval"/> s at <see cref="PoliceSpawner.TryPick"/>; car model
    /// <c>ICity.FindVehicleModel(VehicleKind.Police, seed)</c> (none → no car units), Owner Police, colour 0xFFFFFF,
    /// Siren on, two Cop peds (seats 0 and 1) with <see cref="CopBrain"/>s, Car.Driver = the unit's
    /// <see cref="PoliceDriver"/>. Roadblock units spawn already placed at <see cref="Dispatcher.FindRoadblockJunctions"/>
    /// junctions (two cars per junction, yawed across the road, Locked, cops Guard beside them; with no junction ahead,
    /// across the road <see cref="RoadblockFallbackDistance"/> ahead on the predicted route). Above budget → the
    /// farthest units Leave. Not wanted → <see cref="Dispatcher.PatrolCount"/> patrol units (district heat at the player).</item>
    /// <item>Every <see cref="Dispatcher.AssignInterval"/> s: <see cref="Dispatcher.AssignRoles"/>; every step each unit's
    /// role sets its driver mode and target (Pursue → Chase at the player; Intercept → Route to the prediction; Search →
    /// Wander in its sector, siren off at 1-2★ once inside the search zone; Roadblock/BoxIn in slot → Park; Pit → Pit; OnFoot → cops get out
    /// (TakeOutOfCar at the door positions) and their brains take over; Leave → Route away, siren off).</item>
    /// <item>Helicopter (<see cref="PoliceHelicopter.Step"/>) toward the player when seen, else the last known position.</item>
    /// <item>Despawn: a unit whose car (or the helicopter) is &gt; <see cref="DespawnDistance"/> from the player and not
    /// visible (<c>world.CanPlayerSee</c>) is removed with its cops (<c>RemoveCar</c>, <c>RemovePed</c>); destroyed cars
    /// and units with no living cop are removed the same way once out of view.</item>
    /// <item>Radio cues (<see cref="PoliceRadio"/>).</item>
    /// </list>
    /// Deterministic for a seed; allocates nothing in steady state (unit pool, cop brains and lists created once).
    /// </summary>
    public sealed class PoliceSystem : IGameSystem
    {
        /// <summary>Unit slots (cars + roadblocks + helicopter).</summary>
        public const int MaxUnits = 24;
        /// <summary>Units farther than this and unseen are removed, metres.</summary>
        public const float DespawnDistance = 400f;
        /// <summary>Interior entry delay bounds at 4★+, seconds.</summary>
        public const float InteriorEntryMinSeconds = 20f, InteriorEntryMaxSeconds = 40f;
        /// <summary>Stars from which cops enter interiors.</summary>
        public const int InteriorEntryStars = 4;

        /// <summary>Without a junction 200-500 m ahead, a roadblock closes the road this far ahead on the predicted route, metres.</summary>
        public const float RoadblockFallbackDistance = 300f;
        /// <summary>Patrol wander radius around the player, metres.</summary>
        public const float PatrolRadius = 300f;

        private readonly PoliceUnit[] _units = new PoliceUnit[MaxUnits];
        private readonly CopBrain[] _brains = new CopBrain[MaxUnits * PoliceUnit.CopsPerCar];
        private readonly CopBrain[] _interiorBrains = new CopBrain[2];
        private readonly int[] _interiorCops = new int[2];
        private readonly List<Vec3> _junctions = new List<Vec3>(8);
        private int _nextUnitId = 1;
        private long _cursor = 1;
        private int _episode;
        private float _response;
        private float _reinforce;
        private float _assign;
        private float _slowSeconds;
        private bool _seen;
        private double _lastSeenTime = double.NegativeInfinity;
        private bool _wasEvading;
        private float _lastSeenRadio;
        private float _interiorSeconds;
        private float _interiorDelay;
        private bool _interiorTimed;
        private int _spawnCount;
        private int _heliModel = -1;
        private bool _heliChecked;
        private UnitBudget _budget;

        /// <summary>Creates the system over the crime system (crime reporting, recent cop sightings, G7 services).</summary>
        /// <exception cref="ArgumentNullException"><paramref name="crime"/> is null.</exception>
        public PoliceSystem(CrimeSystem crime, uint seed)
        {
            if (crime == null) throw new ArgumentNullException("crime");
            Crime = crime;
            Seed = seed;
            Dispatcher = new Dispatcher(seed);
            Spawner = new PoliceSpawner(Rng.Seed(seed, 0x5150u));
            Radio = new PoliceRadio();
            Helicopter = new PoliceHelicopter();
            for (int i = 0; i < MaxUnits; i++) _units[i] = new PoliceUnit();
            for (int i = 0; i < _brains.Length; i++) _brains[i] = new CopBrain(this);
            for (int i = 0; i < _interiorBrains.Length; i++) _interiorBrains[i] = new CopBrain(this);
            LastPlayerGunshotTime = double.NegativeInfinity;
        }

        /// <inheritdoc />
        public int Order { get { return SystemOrder.Police; } }

        /// <summary>The crime system.</summary>
        public CrimeSystem Crime { get; private set; }
        /// <summary>Seed of every police choice.</summary>
        public uint Seed { get; private set; }
        /// <summary>The dispatcher.</summary>
        public Dispatcher Dispatcher { get; private set; }
        /// <summary>The spawner.</summary>
        public PoliceSpawner Spawner { get; private set; }
        /// <summary>The radio.</summary>
        public PoliceRadio Radio { get; private set; }
        /// <summary>The helicopter (inactive when not flying).</summary>
        public PoliceHelicopter Helicopter { get; private set; }
        /// <summary>Game seconds of the player's last Gunshot event (−∞ none), read by <see cref="CopBrain"/>.</summary>
        public double LastPlayerGunshotTime { get; private set; }
        /// <summary>Where that gunshot was.</summary>
        public Vec3 LastPlayerGunshotPosition { get; private set; }

        /// <summary>Every unit slot (<see cref="MaxUnits"/>; check <see cref="PoliceUnit.InUse"/>), in slot order.</summary>
        public IList<PoliceUnit> Units { get { return _units; } }

        /// <summary>Units in use.</summary>
        public int ActiveUnitCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < MaxUnits; i++) if (_units[i].InUse) n++;
                return n;
            }
        }

        /// <summary>The budget of the current wanted level (patrol cars only when not wanted).</summary>
        public UnitBudget Budget { get { return _budget; } }

        /// <summary>Seconds until the first unit of this episode may spawn (0 once passed or when not wanted).</summary>
        public float ResponseSecondsRemaining { get { return _response; } }

        /// <summary>True when any unit recognised the player this step.</summary>
        public bool PlayerSeen { get { return _seen; } }

        /// <summary>The unit in use with this id, or null.</summary>
        public PoliceUnit FindUnit(int unitId)
        {
            if (unitId == 0) return null;
            for (int i = 0; i < MaxUnits; i++) if (_units[i].Id == unitId) return _units[i];
            return null;
        }

        /// <summary>The unit in use that owns this Game car, or null.</summary>
        public PoliceUnit FindUnitByCar(int carId)
        {
            if (carId == 0) return null;
            for (int i = 0; i < MaxUnits; i++) if (_units[i].InUse && _units[i].CarId == carId) return _units[i];
            return null;
        }

        /// <summary>The unit a cop ped belongs to, or null.</summary>
        public PoliceUnit FindUnitByCop(int pedId)
        {
            if (pedId == 0) return null;
            for (int i = 0; i < MaxUnits; i++)
            {
                PoliceUnit u = _units[i];
                if (!u.InUse) continue;
                for (int k = 0; k < PoliceUnit.CopsPerCar; k++) if (u.CopIds[k] == pedId) return u;
            }
            return null;
        }

        /// <inheritdoc />
        public void Step(GameWorld world, float dt)
        {
            Ped player = world.Player;
            if (player == null) return;
            WantedStatus wanted = world.Wanted;
            ReadEvents(world);

            TrackEpisode(world);
            Recognise(world, dt);
            Evasion(world, dt);
            Interiors(world, dt);

            Car car = Recognition.PlayerCar(world);
            _slowSeconds = car != null && car.Body.Speed < Dispatcher.BoxInSpeed ? _slowSeconds + dt : 0f;
            Maintain(world, dt);

            _assign -= dt;
            if (_assign <= 0f)
            {
                _assign = Dispatcher.AssignInterval;
                Dispatcher.AssignRoles(world, Context(world), _units);
            }
            for (int i = 0; i < MaxUnits; i++)
            {
                PoliceUnit u = _units[i];
                if (!u.InUse) continue;
                u.RoleSeconds += dt;
                Apply(world, u, i, dt);
            }
            Despawn(world, false);
            Radio.Update(world);
        }

        // ---- events, episodes ----------------------------------------------------------------------------------------

        private void ReadEvents(GameWorld world)
        {
            GameEvent e;
            int playerTag = world.Player.Tag;
            while (world.Events.Read(ref _cursor, out e))
            {
                switch (e.Type)
                {
                    case GameEventType.PlayerDied:
                    case GameEventType.PlayerRespawned:
                        Despawn(world, true);
                        Helicopter.Active = false;
                        Radio.ClearPending();
                        break;
                    case GameEventType.Gunshot:
                        if (e.ActorA == playerTag)
                        {
                            LastPlayerGunshotTime = e.GameTime;
                            LastPlayerGunshotPosition = e.Position;
                        }
                        break;
                    case GameEventType.EnteredInterior:
                        _interiorTimed = false;
                        if (world.Wanted.IsWanted && world.Clock.GameSeconds - _lastSeenTime <= 2.0)
                        {
                            world.Wanted.LastKnownPosition = e.Position;
                            world.Wanted.HasLastKnownPosition = true;
                        }
                        break;
                    case GameEventType.ExitedInterior:
                        RemoveInteriorCops(world);
                        break;
                    case GameEventType.CrimeKnown:
                        if (Crime.CrimeCount > 0)
                        {
                            CrimeRecord r = Crime.GetCrime(0);
                            if (r.State == CrimeState.Known && r.SeenByCopTag == 0) Radio.Say(world, RadioCue.CrimeReported, r.Type, r.Position);
                        }
                        break;
                }
            }
        }

        /// <summary>Starts the response once per <see cref="WantedStatus.EpisodeNumber"/>.</summary>
        private void TrackEpisode(GameWorld world)
        {
            world.Wanted.TrackEpisode();
            if (world.Wanted.EpisodeNumber == _episode) return;
            _episode = world.Wanted.EpisodeNumber;
            BeginEpisode(world);
        }

        /// <summary>Stars went 0 → ≥ 1 (a crime, a patrol, or a script writing <c>WantedStatus</c>): start the response.</summary>
        private void BeginEpisode(GameWorld world)
        {
            WantedStatus w = world.Wanted;
            Ped player = world.Player;
            if (!w.HasLastKnownPosition)
            {
                w.LastKnownPosition = PlayerTarget(world);
                w.HasLastKnownPosition = true;
                w.SecondsSinceSeen = 0f;
                if (w.Description.OutfitModel < 0 && w.Description.CarModel < 0) w.Description = Recognition.Describe(world);
            }
            byte district = world.City.DistrictAt(player.Position.X, player.Position.Z);
            float heat = Math.Max(w.PlayerHeat, w.GetDistrictHeat(district));
            _response = Dispatcher.ResponseDelay(heat, world.Settings.Police);
            _reinforce = 0f;
            _assign = 0f;
            _wasEvading = false;
            _heliChecked = false;
            Helicopter.Leaving = false;
        }

        // ---- sight -----------------------------------------------------------------------------------------------------

        private void Recognise(GameWorld world, float dt)
        {
            WantedStatus w = world.Wanted;
            _seen = false;
            for (int i = 0; i < MaxUnits; i++)
            {
                PoliceUnit u = _units[i];
                if (!u.InUse) continue;
                bool crime;
                if (!UpdateObserver(world, u, out crime))
                {
                    u.SeesPlayer = false;
                    u.SecondsSinceSeen += dt;
                    continue;
                }
                RecognitionReason r = Recognition.Sees(world, ref u.Observer, crime, dt);
                u.SeesPlayer = r != RecognitionReason.None;
                if (u.SeesPlayer) { u.SecondsSinceSeen = 0f; _seen = true; }
                else u.SecondsSinceSeen += dt;
            }
            for (int i = 0; i < _interiorCops.Length; i++)
            {
                Ped cop = _interiorCops[i] != 0 ? world.FindPed(_interiorCops[i]) : null;
                if (cop == null || cop.IsDead) continue;
                PoliceObserver o = new PoliceObserver { Eye = cop.Position + new Vec3(0f, 1.6f, 0f), YawDeg = cop.YawDeg, SelfTag = cop.Tag, CopTag = cop.Tag };
                if (Recognition.Sees(world, ref o, Crime.CopSawCrimeRecently(world, cop.Tag), dt) != RecognitionReason.None) _seen = true;
            }
            if (!_seen) return;

            _lastSeenTime = world.Clock.GameSeconds;
            bool wasWanted = w.IsWanted;
            if (!wasWanted)
            {
                w.Stars = 1;
                TrackEpisode(world);
            }
            if (!wasWanted || w.Evading) Radio.Say(world, RadioCue.Spotted, CrimeType.None, PlayerTarget(world));
            w.LastKnownPosition = PlayerTarget(world);
            w.HasLastKnownPosition = true;
            w.SecondsSinceSeen = 0f;
            w.Description = Recognition.Describe(world);
        }

        /// <summary>Refreshes a unit's observer (helicopter, a cop on foot, or the car); false when nobody can look.</summary>
        private bool UpdateObserver(GameWorld world, PoliceUnit u, out bool crime)
        {
            crime = false;
            if (u.Kind == UnitKind.Helicopter)
            {
                if (!Helicopter.Active) return false;
                u.Observer.Eye = Helicopter.Position;
                u.Observer.Helicopter = true;
                u.Observer.SelfTag = 0;
                u.Observer.CopTag = 0;
                return true;
            }
            Ped onFoot = null, seated = null;
            for (int k = 0; k < PoliceUnit.CopsPerCar; k++)
            {
                Ped cop = u.CopIds[k] != 0 ? world.FindPed(u.CopIds[k]) : null;
                if (cop == null || cop.IsDead) continue;
                if (Crime.CopSawCrimeRecently(world, cop.Tag)) crime = true;
                if (cop.VehicleId != 0) { if (seated == null) seated = cop; }
                else if (onFoot == null) onFoot = cop;
            }
            u.Observer.Helicopter = false;
            if (onFoot != null)
            {
                u.Observer.Eye = onFoot.Position + new Vec3(0f, 1.6f, 0f);
                u.Observer.YawDeg = onFoot.YawDeg;
                u.Observer.SelfTag = onFoot.Tag;
                u.Observer.CopTag = onFoot.Tag;
                return true;
            }
            Car car = seated != null ? world.FindCar(seated.VehicleId) : null;
            if (car == null) return false;
            u.Observer.Eye = car.Position + new Vec3(0f, 0.6f, 0f);
            u.Observer.YawDeg = car.Rotation.YawDeg;
            u.Observer.SelfTag = car.Tag;
            u.Observer.CopTag = seated.Tag;
            return true;
        }

        private void Evasion(GameWorld world, float dt)
        {
            WantedStatus w = world.Wanted;
            if (!w.IsWanted)
            {
                _wasEvading = false;
                return;
            }
            if (!_seen) w.SecondsSinceSeen += dt;
            w.Evading = w.SecondsSinceSeen >= SearchZone.EvadeAfterSeconds;
            if (w.Evading)
            {
                w.SearchCentre = w.LastKnownPosition;
                w.SearchRadius = SearchZone.Radius(w.SecondsSinceSeen);
                if (!_wasEvading)
                {
                    Radio.Say(world, RadioCue.LostVisual, CrimeType.None);
                    _lastSeenRadio = 0f;
                }
                _lastSeenRadio += dt;
                if (_lastSeenRadio >= PoliceRadio.LastSeenInterval)
                {
                    _lastSeenRadio = 0f;
                    Radio.Say(world, RadioCue.LastSeen, CrimeType.None);
                }
            }
            else w.SearchRadius = 0f;
            _wasEvading = w.Evading;

            byte district = world.City.DistrictAt(w.LastKnownPosition.X, w.LastKnownPosition.Z);
            if (w.SecondsSinceSeen >= SearchZone.SearchSeconds(w.Stars, w.GetDistrictHeat(district)))
            {
                w.Stars = 0;
                w.Evading = false;
                w.SearchRadius = 0f;
                w.HasLastKnownPosition = false;
                w.SecondsSinceSeen = 0f;
                w.Description = SuspectDescription.Unknown;
                _wasEvading = false;
                _response = 0f;
                Radio.Say(world, RadioCue.Cleared, CrimeType.None);
                for (int i = 0; i < MaxUnits; i++)
                    if (_units[i].InUse && (_units[i].IsRoadblock || _units[i].Kind == UnitKind.Helicopter)) SetRole(_units[i], UnitRole.Leave);
                Helicopter.Leaving = true;
            }
        }

        // ---- interiors -------------------------------------------------------------------------------------------------

        private void Interiors(GameWorld world, float dt)
        {
            InteriorState interior = world.Interior;
            if (!interior.IsInside || interior.Phase != InteriorPhase.Inside || world.Wanted.Stars < InteriorEntryStars || interior.Layout == null)
            {
                _interiorSeconds = 0f;
                if (!interior.IsInside) RemoveInteriorCops(world);
                return;
            }
            uint building = interior.Building != null ? interior.Building.Id : 0u;
            if (!_interiorTimed)
            {
                _interiorTimed = true;
                _interiorSeconds = 0f;
                _interiorDelay = Seeded.Range(Seed, building, (uint)Dispatcher.AssignmentCount, InteriorEntryMinSeconds, InteriorEntryMaxSeconds);
            }
            _interiorSeconds += dt;
            if (_interiorSeconds < _interiorDelay || _interiorCops[0] != 0) return;
            Vec3 spawn = interior.ToWorld(interior.Layout.EntranceSpawn);
            for (int i = 0; i < _interiorCops.Length; i++)
            {
                Ped cop = world.SpawnPed(PedKind.Cop, CopModel(world, (uint)(1000 + i)), spawn + new Vec3(0.8f * i, 0f, 0f), interior.YawDeg);
                cop.CurrentWeapon = (int)WeaponId.Pistol;
                _interiorBrains[i].Reset(0);
                _interiorBrains[i].Mode = CopMode.Exiting;
                cop.Brain = _interiorBrains[i];
                _interiorCops[i] = cop.Id;
            }
            world.Emit(GameEventType.CopsEnteredInterior, spawn, 0, 0, building, null);
        }

        private void RemoveInteriorCops(GameWorld world)
        {
            for (int i = 0; i < _interiorCops.Length; i++)
            {
                if (_interiorCops[i] == 0) continue;
                Ped cop = world.FindPed(_interiorCops[i]);
                if (cop != null) world.RemovePed(cop, false);
                _interiorCops[i] = 0;
            }
        }

        // ---- budget and spawning ----------------------------------------------------------------------------------------

        private void Maintain(GameWorld world, float dt)
        {
            WantedStatus w = world.Wanted;
            if (!_heliChecked)
            {
                _heliChecked = true;
                _heliModel = world.City.FindVehicleModel(VehicleKind.PoliceHelicopter, Seed);
            }
            int cars = 0, blocks = 0;
            bool heli = false;
            for (int i = 0; i < MaxUnits; i++)
            {
                PoliceUnit u = _units[i];
                if (!u.InUse || u.Role == UnitRole.Leave) continue;
                if (u.Kind == UnitKind.Helicopter) heli = true;
                else if (u.IsRoadblock) blocks++;
                else cars++;
            }

            Ped player = world.Player;
            if (w.IsWanted)
            {
                _budget = Dispatcher.BudgetFor(w.Stars, world.Settings.Police, _heliModel >= 0);
                if (_response > 0f)
                {
                    _response = Math.Max(0f, _response - dt);
                    if (_response > 0f) return;
                }
            }
            else
            {
                byte district = world.City.DistrictAt(player.Position.X, player.Position.Z);
                _budget = new UnitBudget { Cars = Dispatcher.PatrolCount(w.GetDistrictHeat(district), world.Settings.Police) };
                if (heli) Helicopter.Leaving = true;
            }

            if (cars > _budget.Cars) { SendFarthestAway(world); return; }

            _reinforce -= dt;
            if (_reinforce > 0f) return;
            if (cars < _budget.Cars) { if (SpawnCarUnit(world)) _reinforce = Dispatcher.ReinforcementInterval; return; }
            if (blocks < 2 * _budget.Roadblocks && SpawnRoadblock(world)) { _reinforce = Dispatcher.ReinforcementInterval; return; }
            if (_budget.Helicopter && !heli && SpawnHelicopter(world)) _reinforce = Dispatcher.ReinforcementInterval;
        }

        private void SendFarthestAway(GameWorld world)
        {
            Vec3 p = PlayerTarget(world);
            PoliceUnit far = null;
            float best = -1f;
            for (int i = 0; i < MaxUnits; i++)
            {
                PoliceUnit u = _units[i];
                if (!u.InUse || u.Role == UnitRole.Leave || u.IsRoadblock || u.Kind != UnitKind.Car) continue;
                float d = Vec3.DistanceXZ(Dispatcher.UnitPosition(world, u), p);
                if (d > best) { best = d; far = u; }
            }
            if (far != null) SetRole(far, UnitRole.Leave);
        }

        private int FreeSlot()
        {
            for (int i = 0; i < MaxUnits; i++) if (!_units[i].InUse) return i;
            return -1;
        }

        private bool SpawnCarUnit(GameWorld world)
        {
            if (FreeSlot() < 0) return false;
            SpawnPoint sp;
            if (!Spawner.TryPick(world, PlayerTarget(world), out sp)) return false;
            Vec3 ground = sp.Ground;
            if (sp.FromStation) ground -= Vec3.FromYawDeg(sp.YawDeg) * (8f * (_spawnCount % 4));
            PoliceUnit u = CreateCarUnit(world, ground, sp.YawDeg, false);
            if (u == null) return false;
            u.Role = world.Wanted.IsWanted ? UnitRole.Respond : UnitRole.Patrol;
            u.Target = world.Wanted.HasLastKnownPosition ? world.Wanted.LastKnownPosition : PlayerTarget(world);
            return true;
        }

        private bool SpawnRoadblock(GameWorld world)
        {
            if (FreeSlot() < 0) return false;
            DispatchContext ctx = Context(world);
            int found = Dispatcher.FindRoadblockJunctions(world, ctx, 4, _junctions);
            bool onRoad = false;
            float roadHeading = 0f;
            if (found == 0)
            {
                // No junction ahead: block the road itself 300 m ahead on the predicted route.
                float heading = ctx.PlayerVelocity.LengthXZ > 2f ? Vec3.YawDegOf(ctx.PlayerVelocity) : ctx.PlayerHeadingDeg;
                RoadPrediction p = world.Roads.Predict(ctx.PlayerPosition, heading, 1f, RoadblockFallbackDistance);
                if (!p.OnRoad || p.DistanceTravelled < Dispatcher.RoadblockMinDistance) return false;
                _junctions.Add(p.Position);
                found = 1;
                onRoad = true;
                roadHeading = p.HeadingDeg;
            }
            for (int j = 0; j < found; j++)
            {
                Vec3 at = _junctions[j];
                if (RoadblockAt(at)) continue;
                float approach = onRoad ? roadHeading : Vec3.YawDegOf(at - ctx.PlayerPosition);
                Vec3 across = Vec3.FromYawDeg(approach + 90f);
                Vec3 behind = -Vec3.FromYawDeg(approach);
                for (int side = 0; side < 2; side++)
                {
                    if (FreeSlot() < 0) break;
                    PoliceUnit u = CreateCarUnit(world, at + across * (side == 0 ? -2.4f : 2.4f), approach + 90f, true);
                    if (u == null) return side > 0;
                    u.Role = UnitRole.Roadblock;
                    u.Target = at;
                    Car car = world.FindCar(u.CarId);
                    car.Locked = true;
                    car.Siren = true;
                    car.LightBar = true;
                    int slot = SlotIndex(u);
                    for (int k = 0; k < PoliceUnit.CopsPerCar; k++)
                    {
                        Ped cop = world.FindPed(u.CopIds[k]);
                        if (cop == null) continue;
                        Vec3 feet = at + across * (side == 0 ? -2.4f : 2.4f) + behind * 2.5f + across * (k == 0 ? -1f : 1f);
                        world.TakeOutOfCar(cop, feet, approach + 180f);
                        _brains[slot * PoliceUnit.CopsPerCar + k].Mode = CopMode.Exiting;
                    }
                }
                Radio.Say(world, RadioCue.Roadblock, CrimeType.None, at);
                return true;
            }
            return false;
        }

        private bool RoadblockAt(Vec3 at)
        {
            for (int i = 0; i < MaxUnits; i++)
                if (_units[i].InUse && _units[i].IsRoadblock && _units[i].Role != UnitRole.Leave && Vec3.DistanceXZ(_units[i].Target, at) < 20f) return true;
            return false;
        }

        private bool SpawnHelicopter(GameWorld world)
        {
            int slot = FreeSlot();
            if (slot < 0 || _heliModel < 0) return false;
            Vec3 p = PlayerTarget(world);
            float bearing = Seeded.Range(Seed, 0x4E11u, (uint)_nextUnitId, 0f, 360f);
            Vec3 at = p + Vec3.FromYawDeg(bearing) * PoliceHelicopter.SpawnDistance;
            at.Y = world.City.GroundHeight(at.X, at.Z) + PoliceHelicopter.HoverHeight;
            Helicopter.Spawn(world, _heliModel, at, bearing + 180f);
            PoliceUnit u = _units[slot];
            ResetUnit(u);
            u.Kind = UnitKind.Helicopter;
            u.Role = UnitRole.Helicopter;
            u.Observer.Eye = at;
            Radio.Say(world, RadioCue.AirUnit, CrimeType.None);
            return true;
        }

        private PoliceUnit CreateCarUnit(GameWorld world, Vec3 ground, float yaw, bool roadblock)
        {
            int slot = FreeSlot();
            if (slot < 0) return null;
            int model = world.City.FindVehicleModel(VehicleKind.Police, Rng.Seed(Seed, (uint)_nextUnitId));
            if (model < 0) return null;
            Car car = world.SpawnCar(model, ground, yaw, CarOwner.Police, 0xFFFFFF);
            if (car == null) return null;
            _spawnCount++;
            PoliceUnit u = _units[slot];
            ResetUnit(u);
            u.Kind = UnitKind.Car;
            u.IsRoadblock = roadblock;
            u.CarId = car.Id;
            bool wanted = world.Wanted.IsWanted;
            car.Siren = wanted;
            car.LightBar = wanted;
            car.Driver = u.Driver;
            u.Driver.Siren = wanted;
            u.Driver.Seed = Rng.Seed(Seed, (uint)u.Id);
            u.Driver.Mode = roadblock ? PoliceDriveMode.Park : PoliceDriveMode.Route;
            int seats = Math.Min(car.Model.Seats, Car.MaxSeats);
            for (int k = 0; k < PoliceUnit.CopsPerCar && k < seats; k++)
            {
                Ped cop = world.SpawnPed(PedKind.Cop, CopModel(world, (uint)(u.Id * 2 + k)), ground, yaw);
                cop.CurrentWeapon = (int)WeaponId.Pistol;
                cop.MagazineAmmo = 12;
                world.PutInCar(cop, car, k);
                CopBrain brain = _brains[slot * PoliceUnit.CopsPerCar + k];
                brain.Reset(u.Id);
                cop.Brain = brain;
                u.CopIds[k] = cop.Id;
            }
            u.Observer.Eye = car.Position + new Vec3(0f, 0.6f, 0f);
            u.Observer.YawDeg = yaw;
            u.Observer.SelfTag = car.Tag;
            return u;
        }

        private void ResetUnit(PoliceUnit u)
        {
            u.Id = _nextUnitId++;
            u.Kind = UnitKind.Car;
            u.Role = UnitRole.Respond;
            u.RoleSeconds = 0f;
            u.CarId = 0;
            for (int k = 0; k < PoliceUnit.CopsPerCar; k++) u.CopIds[k] = 0;
            u.Slot = 0;
            u.Target = Vec3.Zero;
            u.Driver.Reset();
            u.Observer = default(PoliceObserver);
            u.SeesPlayer = false;
            u.SecondsSinceSeen = 0f;
            u.IsRoadblock = false;
        }

        /// <summary>
        /// A seeded police model (CS1's PoliceOfficerAI uniforms) when the city has one, else a seeded adult civilian
        /// model.
        /// </summary>
        private int CopModel(GameWorld world, uint salt)
        {
            int count = world.City.CitizenModelCount;
            if (count <= 0) return 0;
            int police = CitizenModels.Police(world.City, Rng.Seed(Seed, salt));
            if (police >= 0) return police;
            int pick = 0;
            for (uint t = 0; t < 8; t++)
            {
                pick = (int)(Rng.Seed(Seed, salt, t) % (uint)count);
                CitizenModel m = world.City.GetCitizenModel(pick);
                if (m != null && m.Role == CitizenModelRole.Civilian && (m.AgeGroup == 2 || m.AgeGroup == 3)) return pick;
            }
            return CitizenModels.Civilian(world.City, pick);
        }

        // ---- roles -------------------------------------------------------------------------------------------------

        private DispatchContext Context(GameWorld world)
        {
            WantedStatus w = world.Wanted;
            Ped player = world.Player;
            Car car = Recognition.PlayerCar(world);
            DispatchContext c = new DispatchContext();
            c.Stars = w.Stars;
            c.InCar = car != null;
            c.PlayerPosition = car != null ? car.Position : player.Position;
            c.PlayerVelocity = car != null ? car.Body.Velocity : player.Motor.Velocity;
            c.PlayerHeadingDeg = car != null ? car.Rotation.YawDeg : player.YawDeg;
            c.SlowSeconds = _slowSeconds;
            c.Seen = _seen;
            c.SearchCentre = w.SearchCentre;
            c.SearchRadius = w.Evading ? w.SearchRadius : 0f;
            return c;
        }

        private static void SetRole(PoliceUnit u, UnitRole role)
        {
            if (u.Role == role) return;
            u.Role = role;
            u.RoleSeconds = 0f;
        }

        private void Apply(GameWorld world, PoliceUnit u, int slot, float dt)
        {
            if (u.Kind == UnitKind.Helicopter)
            {
                Vec3 target = _seen || !world.Wanted.HasLastKnownPosition ? PlayerTarget(world) : world.Wanted.LastKnownPosition;
                if (u.Role == UnitRole.Leave) Helicopter.Leaving = true;
                Helicopter.Step(world, target, dt);
                return;
            }
            Car car = u.CarId != 0 ? world.FindCar(u.CarId) : null;
            if (car != null && (car.Owner != CarOwner.Police || HasPlayer(world, car)))
            {
                if (car.Driver == u.Driver) car.Driver = null;
                u.CarId = 0;
                car = null;
            }
            if (car == null) return;

            WantedStatus w = world.Wanted;
            PoliceDriver d = u.Driver;
            Car playerCar = Recognition.PlayerCar(world);
            Vec3 player = PlayerTarget(world);
            d.SpeedFactor = 1f;
            d.Ram = w.Stars >= 3;
            d.Siren = w.IsWanted && u.Role != UnitRole.Leave && u.Role != UnitRole.Patrol;
            d.TargetVelocity = Vec3.Zero;
            switch (u.Role)
            {
                case UnitRole.Pursue:
                    d.Mode = PoliceDriveMode.Chase;
                    d.Target = player;
                    d.TargetVelocity = playerCar != null ? playerCar.Body.Velocity : world.Player.Motor.Velocity;
                    break;
                case UnitRole.Intercept:
                case UnitRole.Respond:
                    d.Mode = PoliceDriveMode.Route;
                    d.Target = u.Target;
                    if (u.Role == UnitRole.Intercept && Vec3.DistanceXZ(car.Position, u.Target) < 12f) d.Mode = PoliceDriveMode.Park;
                    break;
                case UnitRole.Search:
                    d.Mode = PoliceDriveMode.Wander;
                    d.Target = u.Target;
                    d.WanderRadius = Math.Max(60f, w.SearchRadius * 0.6f);
                    d.SpeedFactor = 0.6f;
                    d.Siren = w.Stars >= 3 || !w.Evading || !SearchZone.Contains(w.SearchCentre, w.SearchRadius, car.Position);
                    break;
                case UnitRole.Roadblock:
                    d.Mode = PoliceDriveMode.Park;
                    d.Siren = true;
                    break;
                case UnitRole.BoxIn:
                    if (Vec3.DistanceXZ(car.Position, u.Target) <= 3f)
                    {
                        d.Mode = PoliceDriveMode.Park;
                        if (car.Body.Speed < 1f) CopsOut(world, u, slot, car);
                    }
                    else
                    {
                        d.Mode = PoliceDriveMode.Chase;
                        d.Target = u.Target;
                        d.Ram = false;
                    }
                    break;
                case UnitRole.Pit:
                    d.Mode = PoliceDriveMode.Pit;
                    d.Target = playerCar != null ? PoliceDriver.PitAimPoint(playerCar, car.Position) : player;
                    break;
                case UnitRole.OnFoot:
                    d.Mode = PoliceDriveMode.Park;
                    if (car.Body.Speed < 2f) CopsOut(world, u, slot, car);
                    break;
                case UnitRole.Leave:
                {
                    Vec3 away = (car.Position - player).Flat;
                    away = away.LengthSquared > 1e-4f ? away.Normalized : Vec3.Forward;
                    d.Mode = PoliceDriveMode.Route;
                    d.Target = player + away * 700f;
                    d.Siren = false;
                    break;
                }
                default:
                    d.Mode = PoliceDriveMode.Wander;
                    d.Target = player;
                    d.WanderRadius = PatrolRadius;
                    d.SpeedFactor = 0.8f;
                    break;
            }
            if (u.Role != UnitRole.OnFoot && u.Role != UnitRole.BoxIn && u.Role != UnitRole.Roadblock && !CopsSeated(world, u, car))
                d.Mode = PoliceDriveMode.Park;
            if (car.Driver == null) car.Driver = d;
            car.Siren = d.Siren && car.Model.HasSiren;
            car.LightBar = car.Siren;
        }

        private static bool CopsSeated(GameWorld world, PoliceUnit u, Car car)
        {
            for (int k = 0; k < PoliceUnit.CopsPerCar; k++)
            {
                Ped cop = u.CopIds[k] != 0 ? world.FindPed(u.CopIds[k]) : null;
                if (cop != null && !cop.IsDead && cop.VehicleId != car.Id) return false;
            }
            return true;
        }

        private void CopsOut(GameWorld world, PoliceUnit u, int slot, Car car)
        {
            for (int k = 0; k < PoliceUnit.CopsPerCar; k++)
            {
                Ped cop = u.CopIds[k] != 0 ? world.FindPed(u.CopIds[k]) : null;
                if (cop == null || cop.IsDead || cop.VehicleId != car.Id) continue;
                Vec3 door = world.DoorPosition(car, k == 0);
                world.TakeOutOfCar(cop, door, car.Rotation.YawDeg + (k == 0 ? -90f : 90f));
                CopBrain b = _brains[slot * PoliceUnit.CopsPerCar + k];
                b.Mode = CopMode.Exiting;
                b.ModeSeconds = 0f;
            }
        }

        private static bool HasPlayer(GameWorld world, Car car)
        {
            return world.Player != null && world.Player.VehicleId == car.Id;
        }

        private int SlotIndex(PoliceUnit u)
        {
            for (int i = 0; i < MaxUnits; i++) if (_units[i] == u) return i;
            return 0;
        }

        // ---- despawn ---------------------------------------------------------------------------------------------------

        private void Despawn(GameWorld world, bool all)
        {
            Vec3 player = PlayerTarget(world);
            for (int i = 0; i < MaxUnits; i++)
            {
                PoliceUnit u = _units[i];
                if (!u.InUse) continue;
                if (!all && !ShouldDespawn(world, u, player)) continue;
                if (u.Kind == UnitKind.Helicopter) Helicopter.Active = false;
                Car car = u.CarId != 0 ? world.FindCar(u.CarId) : null;
                for (int k = 0; k < PoliceUnit.CopsPerCar; k++)
                {
                    Ped cop = u.CopIds[k] != 0 ? world.FindPed(u.CopIds[k]) : null;
                    if (cop != null && cop != world.Player) world.RemovePed(cop, false);
                    u.CopIds[k] = 0;
                }
                if (car != null && !HasPlayer(world, car)) world.RemoveCar(car);
                else if (car != null && car.Driver == u.Driver) car.Driver = null;
                u.Id = 0;
                u.CarId = 0;
            }
            if (all) RemoveInteriorCops(world);
        }

        private bool ShouldDespawn(GameWorld world, PoliceUnit u, Vec3 player)
        {
            if (u.Kind == UnitKind.Helicopter)
                return !Helicopter.Active || (Helicopter.Leaving && Vec3.DistanceXZ(Helicopter.Position, player) > 500f);
            Vec3 pos = Dispatcher.UnitPosition(world, u);
            bool alive = false;
            for (int k = 0; k < PoliceUnit.CopsPerCar; k++)
            {
                Ped cop = u.CopIds[k] != 0 ? world.FindPed(u.CopIds[k]) : null;
                if (cop != null && !cop.IsDead)
                {
                    alive = true;
                    if (u.CarId == 0) pos = cop.Position;
                }
            }
            Car car = u.CarId != 0 ? world.FindCar(u.CarId) : null;
            bool broken = !alive || car == null || car.Destroyed;
            bool far = Vec3.DistanceXZ(pos, player) > DespawnDistance;
            if (!far && !broken) return false;
            return !world.CanPlayerSee(pos);
        }

        private static Vec3 PlayerTarget(GameWorld world)
        {
            Car car = Recognition.PlayerCar(world);
            return car != null ? car.Position : world.Player.Position;
        }
    }
}
