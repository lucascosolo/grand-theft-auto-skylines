using System;
using System.Collections.Generic;
using System.IO;
using ColossalFramework;
using ColossalFramework.UI;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Missions;
using GtaSkylines.Game.Police;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Roads;
using GtaSkylines.Game.Session;
using GtaSkylines.Game.World;
using GtaSkylines.Mod.Lifecycle;
using GtaSkylines.Mod.Play;
using Skylines.Host;
using UnityEngine;

namespace GtaSkylines.Mod.SelfTest
{
    /// <summary>
    /// One self-test run (M6 contract): drives the real <see cref="GtaPlaySession"/> through scripted
    /// <see cref="InputFrame"/>s fed in by <see cref="PlayController.InputOverride"/>, step by step with timeouts
    /// (<see cref="StepRunner"/>), and writes <c>selftest-report.json</c>/<c>.txt</c> plus screenshots to the mod data
    /// folder. Missing city content (no parked car, citizen) is replaced by Game-side fixtures, never by CS1 changes;
    /// steps that still lack what they need are skipped with the reason. Never saves the city. Main thread only.
    /// </summary>
    internal sealed class SelfTestRun
    {
        private const float WalkStuckSeconds = 1.5f;

        private readonly HostLog _log;
        private readonly string _folder, _shots;
        private readonly Action _written;
        private readonly SelfTestReport _report = new SelfTestReport();
        private readonly StepRunner _runner;
        private readonly List<GameEvent> _events = new List<GameEvent>();
        private readonly Route _route = new Route();
        private readonly RouteFollower _follower = new RouteFollower();
        private readonly float _start = Time.realtimeSinceStartup;
        private PlayController _pc;
        private GtaPlaySession _session;
        private InputFrame _input;
        private long _cursor = -1;
        private int _shotIndex;
        private string _logPath;
        private int _logStartLength;
        private bool _finished;
        private string _abort;
        // frame timing
        private long _frames;
        private float _worstFrame, _lastFrameAt;
        private int _slowFrames;
        // per-step scratch
        private Vector3 _cityCamPos;
        private bool _cursorVisibleBefore;
        private Vec3 _a;
        private float _f0, _f1;
        private int _i0, _i1;
        private double _t0;
        private bool _b0;
        private Car _car;
        private Vec3 _walkLast;
        private float _walkStill;
        private Vec3 _routeTarget;
        private int _sub;
        private readonly bool _unattended;

        // unattended: started by launch.cfg selftest = city_load, so nobody is there to answer the first-entry backup offer.
        public SelfTestRun(HostLog log, string trigger, string dataFolder, Action written, bool unattended)
        {
            _log = log;
            _unattended = unattended;
            _folder = dataFolder;
            _shots = Path.Combine(dataFolder, "selftest-screenshots");
            _written = written;
            _report.RunStartedUtc = DateTime.UtcNow;
            _report.Trigger = trigger;
            _report.GameVersion = BuildConfig.applicationVersion;
            _report.ModVersion = typeof(SelfTestRun).Assembly.GetName().Version.ToString();
            _report.CityName = SimulationManager.exists && Singleton<SimulationManager>.instance.m_metaData != null
                ? Singleton<SimulationManager>.instance.m_metaData.m_CityName : "";
            _logPath = log.FilePath;
            _logStartLength = ReadLog().Length;
            Directory.CreateDirectory(_shots);
            _runner = new StepRunner(Steps());
            _runner.Finished = r => _log.Info("self-test: " + r.Status.ToString().ToUpperInvariant() + " " + r.Id + " ("
                + r.DurationSeconds.ToString("0.0") + " s) " + r.Reason);
            _log.Info("self-test: started by " + trigger + "; output " + _folder);
        }

        /// <summary>True once the report has been written.</summary>
        public bool Done { get { return _finished; } }

        /// <summary>Stops at the current step; the next <see cref="Update"/> writes the partial report.</summary>
        public void Abort(string reason) { if (_abort == null) _abort = reason; }

        /// <summary>Per frame from the self-test pump.</summary>
        public void Update()
        {
            if (_finished) return;
            double now = Time.realtimeSinceStartup - _start;
            FrameTiming();
            CollectEvents();
            if (_abort != null) _runner.Abort(_abort, now);
            else _runner.Tick(now);
            if (_runner.Done) Finish();
        }

        // ---- steps ---------------------------------------------------------------------------------------------------

        private List<SelfTestStep> Steps()
        {
            return new List<SelfTestStep>
            {
                S("enter_play", "Enter play on a road", 60, BeginEnter, TickEnter),
                S("walk", "Walk 10 m", 20, r => _a = W.Player.Position, TickWalk).Needs("enter_play"),
                S("jump", "Jump", 8, BeginJump, TickJump).Needs("enter_play"),
                S("sprint", "Sprint (stamina drains)", 15, r => { _f0 = W.PlayerState.Stamina; _f1 = 0f; _b0 = false; }, TickSprint).Needs("enter_play"),
                S("enter_car", "Enter the nearest parked car", 40, BeginEnterCar, TickEnterCar).Needs("enter_play"),
                S("drive", "Drive 150 m along a road (route follower)", 90, BeginDrive, TickDrive).Needs("enter_car"),
                S("crash", "Crash at speed (damage)", 40, BeginCrash, TickCrash).Needs("enter_car"),
                S("exit_car", "Exit the car", 20, BeginExitCar, TickExitCar).Needs("enter_car"),
                S("building", "Enter the nearest building and leave", 150, BeginBuilding, TickBuilding).Needs("enter_play"),
                S("punch", "Punch an ambient citizen", 60, BeginPunch, TickPunch).Needs("enter_play"),
                S("pistol", "Fire a pistol (witness, stars)", 45, BeginPistol, TickPistol).Needs("enter_play"),
                S("police", "Police spawn and pursue", 150, BeginPolice, TickPolice).Needs("pistol"),
                S("busted", "Hands up: busted", 90, r => _t0 = 0, TickBusted).Needs("police"),
                S("respawn", "Respawn after busted", 30, null, TickRespawn).Needs("busted"),
                S("mission_v1", "Mission V1 with scripted moves", 480, BeginMission, TickMission).Needs("enter_play"),
                S("menus", "Open and close every menu", 90, r => _sub = 0, TickMenus).Needs("enter_play"),
                S("profile", "Profile save and load round trip", 10, BeginProfile, null).Needs("enter_play"),
                S("leave_play", "Leave play: camera and UI restored", 30, BeginLeave, TickLeave).Needs("enter_play"),
            };
        }

        private SelfTestStep S(string id, string name, double timeout, Action<StepRecord> begin, Action<StepRecord, double> tick)
        {
            return new SelfTestStep(id, name, timeout, r =>
            {
                _events.Clear();
                _input = default(InputFrame);
                _walkStill = 0f;
                if (W != null) _walkLast = W.Player.Position;
                if (begin != null) begin(r);
            }, (r, t) =>
            {
                if (_session != null && _session.ExitFault != null)
                {
                    r.Fail("session exit fault: " + _session.ExitFault);
                    return;
                }
                if (r.Id != "enter_play" && r.Id != "leave_play" && (_pc == null || !_pc.Playing))
                {
                    r.Fail("play mode ended unexpectedly");
                    return;
                }
                if (tick != null) tick(r, t);
                if (r.Finished) { _input = default(InputFrame); Shot(r, "end"); }
            });
        }

        private GameParts G { get { return _session == null ? null : _session.Game; } }

        private GameWorld W { get { return G == null ? null : G.World; } }

        // enter_play
        private void BeginEnter(StepRecord r)
        {
            _pc = PlayController.Current;
            if (_pc == null) { r.Fail("no play controller (no city loaded?)"); return; }
            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam == null) { r.Fail("no main camera"); return; }
            _cityCamPos = cam.transform.position;
            _cursorVisibleBefore = Cursor.visible;
            Vector3 road;
            if (!NearestRoad(cam.transform.position, out road)) { r.Fail("no road segment in the city"); return; }
            if (Singleton<SimulationManager>.instance.SimulationPaused) Singleton<SimulationManager>.instance.SimulationPaused = false;
            _pc.InputOverride = Feed;
            _pc.RequestScripted(road, cam.transform.eulerAngles.y, _unattended);
            r.Values.Set("spawn", V(road));
        }

        private void TickEnter(StepRecord r, double t)
        {
            if (_pc == null || !_pc.Playing) { r.Progress = "waiting for play mode (entry gates)"; return; }
            _session = _pc.Session as GtaPlaySession;
            if (_session == null || G == null) { r.Fail("play session is not the GTA session (Alt held?)"); return; }
            if (_t0 == 0) _t0 = t;
            if (t - _t0 < 2 || !W.Player.Motor.OnGround) { r.Progress = "settling"; return; }
            r.Values.Set("player", V(W.Player.Position)).Set("road_segments", W.Roads.SegmentCount)
                .Set("ambient_citizens", W.Ambient.Citizens.Count).Set("ambient_vehicles", W.Ambient.Vehicles.Count);
            r.Pass("on foot at the road");
        }

        // walk, jump, sprint
        private void TickWalk(StepRecord r, double t)
        {
            float d = Vec3.DistanceXZ(_a, W.Player.Position);
            _input = new InputFrame { MoveY = 1f };
            r.Progress = "walked " + d.ToString("0.0") + " m";
            if (d < 10f) return;
            r.Values.Set("distance_m", d).Set("speed_mps", d / (float)t);
            r.Pass("walked 10 m");
        }

        private void BeginJump(StepRecord r)
        {
            _a = W.Player.Position;
            _f0 = 0f;
            _b0 = false;
            _input = ScriptInput.Press(GameButton.Jump);
        }

        private void TickJump(StepRecord r, double t)
        {
            _f0 = Math.Max(_f0, W.Player.Position.Y - _a.Y);
            bool ground = W.Player.Motor.OnGround;
            if (!ground) _b0 = true;
            r.Progress = "rise " + _f0.ToString("0.00") + " m, airborne " + _b0;
            if (!_b0 || !ground) return;
            r.Values.Set("max_rise_m", _f0);
            if (_f0 > 0.2f) r.Pass("jumped and landed");
            else r.Fail("left the ground but rose only " + _f0.ToString("0.00") + " m");
        }

        private void TickSprint(StepRecord r, double t)
        {
            PlayerState ps = W.PlayerState;
            _input = new InputFrame { MoveY = 1f, Held = InputFrame.Bit(GameButton.Sprint) };
            if (ps.Sprinting) _b0 = true;
            _f1 = Math.Max(_f1, W.Player.Motor.Velocity.LengthXZ);
            r.Progress = "stamina " + ps.Stamina.ToString("0") + ", sprinting " + _b0;
            if (t < 3 || !_b0 || ps.Stamina >= _f0) return;
            r.Values.Set("stamina_start", _f0).Set("stamina_now", ps.Stamina).Set("max_speed_mps", _f1);
            r.Pass("sprinting drains stamina");
        }

        // enter_car, drive, crash, exit_car
        private void BeginEnterCar(StepRecord r)
        {
            Vec3 me = W.Player.Position;
            float best = 80f;
            _a = me;
            string fixture = null;
            foreach (AmbientVehicle v in W.Ambient.Vehicles)
            {
                float d = Vec3.DistanceXZ(me, v.Position);
                if (v.Parked && d < best) { best = d; _a = v.Position; fixture = "cs1_parked"; }
            }
            if (fixture == null)
            {
                _car = SpawnCarAhead(6f);
                if (_car == null) { r.Skip("no parked car within 80 m and no car model to spawn"); return; }
                _a = _car.Position;
                fixture = "game_car_spawned";
            }
            r.Values.Set("fixture", fixture).Set("distance_m", Vec3.DistanceXZ(me, _a));
            _t0 = 0;
        }

        private void TickEnterCar(StepRecord r, double t)
        {
            Ped p = W.Player;
            PlayerState ps = W.PlayerState;
            if (p.VehicleId != 0 && ps.Transition == VehicleTransition.None)
            {
                _car = W.FindCar(p.VehicleId);
                r.Values.Set("car", _car == null ? "" : _car.Model.Name).Set("entered_event", Saw(GameEventType.PlayerEnteredCar));
                r.Pass("driving");
                return;
            }
            if (ps.Transition != VehicleTransition.None) { _input = default(InputFrame); r.Progress = "transition " + ps.Transition; return; }
            if (ps.Interaction.Kind == InteractionTargetKind.EnterVehicle && t - _t0 > 1.0)
            {
                _t0 = t;
                _input = ScriptInput.Press(GameButton.EnterExit);
                return;
            }
            WalkTo(_a, false);
            r.Progress = "walking to the car, " + Vec3.DistanceXZ(p.Position, _a).ToString("0.0") + " m";
        }

        private void BeginDrive(StepRecord r)
        {
            _car = W.FindCar(W.Player.VehicleId);
            if (_car == null) { r.Fail("not in a car"); return; }
            RouteStatus s = W.Roads.FindWanderRoute(_car.Position, _car.Body.Rotation.YawDeg, 170f, new Rng(7u), _route);
            if (s != RouteStatus.Ok) { r.Fail("no 170 m route from here: " + s); return; }
            _follower.Reset();
            _follower.MaxSpeed = 14f;
            _a = _car.Position;
            _f0 = _f1 = 0f;
            r.Values.Set("route_m", _route.Length);
        }

        private void TickDrive(StepRecord r, double t)
        {
            VehicleBodyView b = Body();
            DriveCommand cmd = _follower.Update(_route, _car.Position, b.Yaw, b.Speed);
            _input = ScriptInput.Drive(cmd);
            _f0 += Vec3.DistanceXZ(_a, _car.Position);
            _a = _car.Position;
            float off;
            _route.Project(_car.Position, out off);
            _f1 = Math.Max(_f1, off);
            r.Progress = "drove " + _f0.ToString("0") + " m";
            if (_f0 < 150f && !cmd.Arrived) return;
            r.Values.Set("driven_m", _f0).Set("max_off_route_m", _f1).Set("avg_speed_mps", _f0 / (float)t).Set("health", _car.Body.Health);
            if (_f0 >= 150f) r.Pass("followed the road 150 m");
            else r.Fail("route ended after " + _f0.ToString("0") + " m");
        }

        private void BeginCrash(StepRecord r)
        {
            _car = W.FindCar(W.Player.VehicleId);
            if (_car == null) { r.Fail("not in a car"); return; }
            _f0 = _car.Body.Health;
            _f1 = 0f;
            Car obstacle = SpawnCarAt(_car.Position + Quat.Yaw(_car.Body.Rotation.YawDeg) * new Vec3(0f, 0f, 45f), _car.Body.Rotation.YawDeg + 90f);
            _a = obstacle != null ? obstacle.Position : _car.Position + Quat.Yaw(_car.Body.Rotation.YawDeg) * new Vec3(0f, 0f, 200f);
            r.Values.Set("obstacle", obstacle != null ? "game_car_spawned" : "none");
        }

        private void TickCrash(StepRecord r, double t)
        {
            VehicleBodyView b = Body();
            _f1 = Math.Max(_f1, b.Speed);
            float steer = MathUtil.Clamp(MathUtil.DeltaAngleDeg(b.Yaw, ScriptInput.YawToward(_car.Position, _a)) / 30f, -1f, 1f);
            _input = new InputFrame { MoveX = steer, MoveY = 1f };
            bool damaged = Saw(GameEventType.CarDamaged) || _car.Body.Health < _f0 - 0.5f;
            r.Progress = "max speed " + _f1.ToString("0.0") + " m/s, health " + _car.Body.Health.ToString("0");
            if (!damaged) return;
            r.Values.Set("max_speed_mps", _f1).Set("health_before", _f0).Set("health_after", _car.Body.Health)
                .Set("damage_state", _car.Body.DamageState.ToString());
            r.Pass("impact registered");
        }

        private void BeginExitCar(StepRecord r) { _t0 = -10; }

        private void TickExitCar(StepRecord r, double t)
        {
            PlayerState ps = W.PlayerState;
            if (W.Player.VehicleId == 0 && ps.Transition == VehicleTransition.None)
            {
                r.Values.Set("exited_event", Saw(GameEventType.PlayerExitedCar));
                r.Pass("on foot");
                return;
            }
            _input = new InputFrame { Held = InputFrame.Bit(GameButton.Jump) };
            if (ps.Transition == VehicleTransition.None && t - _t0 > 2.0)
            {
                _t0 = t;
                _input.Pressed = InputFrame.Bit(GameButton.EnterExit);
                _input.Held |= _input.Pressed;
            }
            r.Progress = "transition " + ps.Transition;
        }

        // building
        private void BeginBuilding(StepRecord r)
        {
            var near = new List<ushort>();
            Vec3 me = W.Player.Position;
            W.City.BuildingsNear(me, 300f, near);
            float best = float.MaxValue;
            foreach (ushort id in near)
            {
                BuildingDescriptor b;
                if (!W.City.TryGetBuilding(id, out b) || b.Abandoned || b.Doors == null) continue;
                foreach (Vec3 d in b.Doors)
                {
                    float dist = Vec3.DistanceXZ(me, d);
                    if (dist < best) { best = dist; _a = d; _i0 = id; }
                }
            }
            if (best == float.MaxValue) { r.Skip("no building with a door within 300 m"); return; }
            _sub = 0;
            _t0 = -10;
            r.Values.Set("building", _i0).Set("door_distance_m", best);
        }

        private void TickBuilding(StepRecord r, double t)
        {
            InteriorState it = W.Interior;
            PlayerState ps = W.PlayerState;
            if (_sub == 0)
            {
                if (it.IsInside) { _sub = 1; _input = default(InputFrame); return; }
                if (ps.Interaction.Kind == InteractionTargetKind.BuildingDoor && t - _t0 > 2.0)
                {
                    _t0 = t;
                    _input = ScriptInput.Press(GameButton.Interact);
                    return;
                }
                WalkTo(_a, false);
                r.Progress = "walking to the door, " + Vec3.DistanceXZ(W.Player.Position, _a).ToString("0.0") + " m";
                return;
            }
            if (_sub == 1)
            {
                if (it.Phase != InteriorPhase.Inside) { r.Progress = "fading in"; return; }
                Shot(r, "inside");
                r.Values.Set("archetype", it.Layout.Archetype.ToString()).Set("entered_event", Saw(GameEventType.EnteredInterior));
                _b0 = false;
                foreach (InteractionPoint ip in it.Layout.Interactions)
                    if (ip.Kind == InteractionKind.ExitDoor) { _a = it.ToWorld(ip.Position); _b0 = true; break; }
                if (!_b0) { r.Fail("interior has no exit door point"); return; }
                _sub = 2;
                return;
            }
            if (!it.IsInside && it.Phase == InteriorPhase.Outside)
            {
                r.Values.Set("exited_event", Saw(GameEventType.ExitedInterior));
                r.Pass("entered and left through the exit");
                return;
            }
            if (it.Phase != InteriorPhase.Inside) { _input = default(InputFrame); r.Progress = "fading out"; return; }
            if (ps.Interaction.Kind == InteractionTargetKind.InteriorPoint && t - _t0 > 2.0)
            {
                _t0 = t;
                _input = ScriptInput.Press(GameButton.Interact);
                return;
            }
            WalkTo(_a, false);
            r.Progress = "walking to the exit, " + Vec3.DistanceXZ(W.Player.Position, _a).ToString("0.0") + " m";
        }

        // punch
        private void BeginPunch(StepRecord r)
        {
            G.Combat.PlayerWeapon.Equip(W.Player, WeaponId.Fists);
            Vec3 me = W.Player.Position;
            float best = 80f;
            _i0 = 0;
            _i1 = 0;
            foreach (AmbientCitizen c in W.Ambient.Citizens)
            {
                float d = Vec3.DistanceXZ(me, c.Position);
                if (d < best) { best = d; _i0 = c.Instance; _a = c.Position; }
            }
            if (_i0 != 0) { r.Values.Set("target", "cs1_citizen").Set("distance_m", best); return; }
            Ped ped = W.SpawnPed(PedKind.Civilian, 0, me + Quat.Yaw(W.PlayerState.ViewYawDeg) * new Vec3(0f, 0f, 4f), 0f);
            _i1 = ped.Id;
            _a = ped.Position;
            r.Values.Set("target", "game_ped_spawned (no CS1 citizen within 80 m)");
            _t0 = -10;
        }

        private void TickPunch(StepRecord r, double t)
        {
            Ped target = _i1 != 0 ? W.FindPed(_i1) : null;
            if (target == null && _i0 != 0)
            {
                bool found = false;
                foreach (AmbientCitizen c in W.Ambient.Citizens)
                    if (c.Instance == _i0) { _a = c.Position; found = true; break; }
                if (!found) r.Progress = "citizen instance gone from the snapshot (promoted or despawned)";
            }
            else if (target != null) _a = target.Position;
            foreach (GameEvent e in _events)
            {
                if (e.Type != GameEventType.Punched || e.ActorA != W.Player.Tag) continue;
                Ped victim = W.PedFromTag(e.ActorB, false, 0);
                r.Values.Set("victim_promoted", victim != null && victim.Citizen != 0).Set("victim_anim", victim == null ? "" : victim.Anim.ToString());
                r.Pass("punch landed");
                return;
            }
            float d = Vec3.DistanceXZ(W.Player.Position, _a);
            if (d > 1.4f) { WalkTo(_a, false); r.Progress = "approaching, " + d.ToString("0.0") + " m"; return; }
            _input = ScriptInput.Walk(W.PlayerState.ViewYawDeg, W.Player.Position, _a, false);
            _input.MoveY = 0f;
            if (t - _t0 > 0.5)
            {
                _t0 = t;
                _input.Pressed = _input.Held = InputFrame.Bit(GameButton.Attack);
            }
        }

        // pistol, police, busted, respawn
        private void BeginPistol(StepRecord r)
        {
            PlayerProfile prof = W.Profile;
            prof.GiveWeapon(WeaponId.Pistol);
            prof.SetAmmo(WeaponId.Pistol, 60);
            G.Combat.PlayerWeapon.Equip(W.Player, WeaponId.Pistol);
            _i0 = 0;
            _t0 = -10;
            r.Values.Set("granted", "pistol, 60 rounds");
        }

        private void TickPistol(StepRecord r, double t)
        {
            int shots = 0;
            bool call = false;
            foreach (GameEvent e in _events)
            {
                if (e.Type == GameEventType.Gunshot && e.ActorA == W.Player.Tag) shots++;
                if (e.Type == GameEventType.CallStarted) call = true;
            }
            int stars = W.Wanted.Stars;
            r.Progress = shots + " shots, stars " + stars + ", witness call " + call;
            if (shots > 0 && stars > 0)
            {
                r.Values.Set("shots", shots).Set("stars", stars).Set("witness_call", call);
                r.Pass("shots fired, wanted level " + stars);
                return;
            }
            _input = ScriptInput.Hold(GameButton.Aim);
            if (_i0 < 3 && t - _t0 > 0.6)
            {
                _t0 = t;
                _i0++;
                _input.Pressed = InputFrame.Bit(GameButton.Attack);
                _input.Held |= _input.Pressed;
            }
        }

        private void BeginPolice(StepRecord r)
        {
            if (W.Wanted.Stars == 0) r.Skip("not wanted");
            _t0 = -1;
        }

        private void TickPolice(StepRecord r, double t)
        {
            int units = G.Police.ActiveUnitCount;
            float nearest = NearestCop();
            bool sees = false;
            foreach (PoliceUnit u in G.Police.Units) sees |= u.SeesPlayer;
            if (units > 0 && _t0 < 0) _t0 = t;
            r.Progress = units + " units, nearest cop " + nearest.ToString("0") + " m, sees player " + sees + ", stars " + W.Wanted.Stars;
            if (W.Wanted.Stars == 0) { r.Fail("wanted level dropped before police pursued"); return; }
            if (units == 0 || !sees || nearest > 40f) return;
            r.Values.Set("units", units).Set("seconds_to_first_unit", _t0).Set("nearest_cop_m", nearest).Set("stars", W.Wanted.Stars);
            r.Pass("police in pursuit");
        }

        private void TickBusted(StepRecord r, double t)
        {
            _input = ScriptInput.Hold(GameButton.HandsUp);
            r.Progress = "hands up; nearest cop " + NearestCop().ToString("0") + " m";
            if (W.PlayerState.Life != LifeState.Busted && !Saw(GameEventType.PlayerArrested)) return;
            r.Values.Set("handsup_event", Saw(GameEventType.HandsUp)).Set("cash", W.Profile.Cash);
            Shot(r, "busted");
            r.Pass("busted");
        }

        private void TickRespawn(StepRecord r, double t)
        {
            if (W.PlayerState.Life != LifeState.Alive || !Saw(GameEventType.PlayerRespawned)) { r.Progress = "life " + W.PlayerState.Life; return; }
            r.Values.Set("position", V(W.Player.Position)).Set("stars", W.Wanted.Stars).Set("cash", W.Profile.Cash);
            r.Pass("respawned");
        }

        // mission V1
        private void BeginMission(StepRecord r)
        {
            _t0 = 0;
            _i0 = -1;
            _b0 = false;
        }

        private void TickMission(StepRecord r, double t)
        {
            MissionRunner m = G.Missions;
            if (!_b0)
            {
                if (W.PlayerState.Life != LifeState.Alive) { r.Progress = "waiting for the player to respawn"; return; }
                _b0 = m.TryStartMission(W, MissionIds.V1);
                if (_b0) { r.Values.Set("steps", m.ActiveMission.Steps.Length); return; }
                if (t > 15) r.Skip(m.Campaign == null ? "campaign not generated (the city lacks mission places)" : "V1 not available (state " + m.State + ")");
                return;
            }
            if (W.Profile.CompletedMissions.Contains(MissionIds.V1)) { r.Values.Set("reached_step", _i0); r.Pass("mission passed"); return; }
            if (m.State != RunnerState.Mission)
            {
                r.Values.Set("reached_step", _i0);
                r.Fail("mission ended without passing at step " + _i0 + ": " + (m.LastFailReason ?? "?"));
                return;
            }
            int step = m.Context.StepIndex;
            MissionStep s = m.ActiveMission.Steps[step];
            if (step != _i0) { _i0 = step; _sub = 0; _t1Reset(); Shot(r, "step" + step); }
            r.Progress = "step " + (step + 1) + "/" + m.ActiveMission.Steps.Length + " " + s.Kind + ": " + (W.Mission.Objective ?? "");
            MissionMove(s.Kind, t);
        }

        private void _t1Reset() { _route.Clear(); _routeTarget = new Vec3(float.NaN, 0f, 0f); }

        private void MissionMove(StepKind kind, double t)
        {
            Ped p = W.Player;
            PlayerState ps = W.PlayerState;
            if (W.Menu.IsOpen)
            {
                for (int i = 0; i < W.Menu.Items.Count; i++)
                    if (W.Menu.Items[i].Id == "snack") { G.Session.ChooseMenuOption(i); break; }
                G.Session.CloseMenu();
                return;
            }
            switch (kind)
            {
                case StepKind.BuyItem:
                    if (W.Interior.Phase == InteriorPhase.Inside)
                    {
                        foreach (InteractionPoint ip in W.Interior.Layout.Interactions)
                            if (ip.Kind == InteractionKind.ShopCounter) { _a = W.Interior.ToWorld(ip.Position); break; }
                        if (ps.Interaction.Kind == InteractionTargetKind.InteriorPoint && t - _t0 > 2.0) { _t0 = t; _input = ScriptInput.Press(GameButton.Interact); }
                        else WalkTo(_a, false);
                        return;
                    }
                    if (W.Interior.IsInside) { _input = default(InputFrame); return; }
                    if (ps.Interaction.Kind == InteractionTargetKind.BuildingDoor && t - _t0 > 2.0) { _t0 = t; _input = ScriptInput.Press(GameButton.Interact); return; }
                    TravelTo(Target(), t);
                    return;
                case StepKind.StealVehicle:
                case StepKind.EnterVehicle:
                    if (p.VehicleId != 0) { _input = default(InputFrame); return; }
                    if (ps.Transition != VehicleTransition.None) { _input = default(InputFrame); return; }
                    if (ps.Interaction.Kind == InteractionTargetKind.EnterVehicle && t - _t0 > 1.0) { _t0 = t; _input = ScriptInput.Press(GameButton.EnterExit); return; }
                    float best = 150f;
                    foreach (AmbientVehicle v in W.Ambient.Vehicles)
                        if (v.Parked && Vec3.DistanceXZ(p.Position, v.Position) < best) { best = Vec3.DistanceXZ(p.Position, v.Position); _a = v.Position; }
                    if (best < 150f) WalkTo(_a, true);
                    return;
                case StepKind.Talk:
                case StepKind.Wait:
                    _input = default(InputFrame);
                    return;
                default:
                    TravelTo(Target(), t);
                    return;
            }
        }

        private Vec3 Target()
        {
            if (W.Mission.HasGpsTarget) return W.Mission.GpsTarget;
            foreach (Blip b in W.Mission.Blips) return b.Position;
            return W.Player.Position;
        }

        // Walks (sprinting) or drives along a road route to the target; the last 25 m straight.
        private void TravelTo(Vec3 target, double t)
        {
            Ped p = W.Player;
            Car car = p.VehicleId != 0 ? W.FindCar(p.VehicleId) : null;
            Vec3 from = car != null ? car.Position : p.Position;
            if (Vec3.DistanceXZ(from, target) < 25f)
            {
                if (car != null) _input = new InputFrame { Held = InputFrame.Bit(GameButton.Jump) };
                else WalkTo(target, false);
                return;
            }
            if (Vec3.DistanceXZ(_routeTarget, target) > 5f || _route.Count == 0)
            {
                _routeTarget = target;
                if (W.Roads.FindRoute(from, target, _route) != RouteStatus.Ok) _route.Clear();
                _follower.Reset();
                _follower.MaxSpeed = 14f;
            }
            if (_route.Count == 0) { if (car == null) WalkTo(target, true); return; }
            if (car != null)
            {
                VehicleBodyView b = Body(car);
                _input = ScriptInput.Drive(_follower.Update(_route, car.Position, b.Yaw, b.Speed));
                return;
            }
            float off;
            float progress = _route.Project(from, out off);
            WalkTo(_route.Sample(Math.Min(progress + 6f, _route.Length)).Position, true);
        }

        // menus
        private void TickMenus(StepRecord r, double t)
        {
            var ui = _session.Ui;
            // pairs: open press, open check; close press, closed check
            GameButton[] buttons = { GameButton.Pause, GameButton.Phone, GameButton.InteractionMenu, GameButton.WeaponWheel, GameButton.BigMap };
            string[] names = { "pause", "phone", "interaction", "weapon_wheel", "big_map" };
            int menu = _sub / 3, phase = _sub % 3;
            if (menu >= buttons.Length)
            {
                int ok = 0;
                foreach (string n in names) if (Equals(r.Values.Get(n), true)) ok++;
                if (ok == names.Length) r.Pass("all menus opened and closed");
                else r.Fail(ok + "/" + names.Length + " menus opened and closed");
                return;
            }
            GameButton btn = buttons[menu];
            bool open = btn == GameButton.Pause ? ui.PauseOpen : btn == GameButton.Phone ? ui.PhoneOpen
                : btn == GameButton.InteractionMenu ? ui.InteractionOpen : btn == GameButton.WeaponWheel ? ui.WheelOpen
                : ui.Hud != null && ui.Hud.BigMap;
            bool wheel = btn == GameButton.WeaponWheel;
            if (phase == 0)
            {
                _t0 = t;
                _input = wheel ? ScriptInput.Press(btn) : ScriptInput.Press(btn);
                _sub++;
                return;
            }
            if (phase == 1)
            {
                if (wheel) _input = ScriptInput.Hold(btn);
                if (!open && t - _t0 < 5) return;
                r.Values.Set(names[menu] + "_opened", open);
                if (open) Shot(r, names[menu]);
                _t0 = t;
                // The phone key only takes the phone out (MenuRouter); Escape (Pause) puts it away.
                _input = wheel ? default(InputFrame) : ScriptInput.Press(btn == GameButton.Phone ? GameButton.Pause : btn);
                _sub++;
                return;
            }
            if (open && t - _t0 < 5) return;
            r.Values.Set(names[menu], Equals(r.Values.Get(names[menu] + "_opened"), true) && !open);
            _input = default(InputFrame);
            _sub++;
        }

        // profile
        private void BeginProfile(StepRecord r)
        {
            byte[] bytes = _session.SaveProfile();
            ProfileDecodeResult d = ProfileRecord.Decode(bytes);
            PlayerProfile live = W.Profile;
            r.Values.Set("bytes", bytes.Length).Set("decoded", d.Ok);
            if (!d.Ok) { r.Fail("record did not decode"); return; }
            bool same = d.Profile.Cash == live.Cash && d.Profile.OwnsWeapon(WeaponId.Pistol) == live.OwnsWeapon(WeaponId.Pistol)
                && d.Profile.Stats.Count == live.Stats.Count && d.Profile.CompletedMissions.Count == live.CompletedMissions.Count;
            byte[] again = ProfileRecord.Encode(d.Profile);
            r.Values.Set("cash", d.Profile.Cash).Set("stats", d.Profile.Stats.Count).Set("reencoded_identical", Same(bytes, again));
            if (same && Same(bytes, again)) r.Pass("save, load and re-save agree");
            else r.Fail("loaded profile differs from the live one");
        }

        // leave_play
        private void BeginLeave(StepRecord r)
        {
            if (_session == null) { r.Fail("no session"); return; }
            Totals();
            _input = default(InputFrame);
            _session.ReturnToCityBuilder();
        }

        private void TickLeave(StepRecord r, double t)
        {
            UnityEngine.Camera cam = UnityEngine.Camera.main;
            UIView view = UIView.GetAView();
            bool uiCam = view != null && view.uiCamera != null && view.uiCamera.enabled;
            bool modal = UIView.HasModalInput();
            float camOff = cam == null ? -1f : (cam.transform.position - _cityCamPos).magnitude;
            r.Progress = "playing " + _pc.Playing + ", camera taken " + _pc.CameraTaken + ", UI camera " + uiCam + ", modal " + modal;
            if (_pc.Playing || _pc.CameraTaken || !uiCam || modal) return;
            if (t < 1.0) return;
            r.Values.Set("exit_fault", _session.ExitFault).Set("camera_offset_m", camOff).Set("cursor_visible", Cursor.visible).Set("cursor_visible_before", _cursorVisibleBefore)
                .Set("ui_camera_enabled", uiCam);
            Shot(r, "city");
            if (camOff >= 0 && camOff < 5f && Cursor.visible == _cursorVisibleBefore) r.Pass("city view, cursor and UI restored");
            else r.Fail("camera " + camOff.ToString("0.0") + " m from where it was, cursor visible " + Cursor.visible);
        }

        // ---- helpers -------------------------------------------------------------------------------------------------

        private InputFrame Feed(InputFrame real)
        {
            InputFrame f = _input;
            _input.Pressed = 0;
            return f;
        }

        private void WalkTo(Vec3 target, bool sprint)
        {
            Ped p = W.Player;
            _input = ScriptInput.Walk(W.PlayerState.ViewYawDeg, p.Position, target, sprint);
            if (Vec3.DistanceXZ(_walkLast, p.Position) > 0.3f) { _walkLast = p.Position; _walkStill = 0f; return; }
            _walkStill += Time.deltaTime;
            if (_walkStill < WalkStuckSeconds) return;
            _walkStill = 0f;
            _input.Pressed |= InputFrame.Bit(GameButton.Jump);
            _input.Held |= InputFrame.Bit(GameButton.Jump);
        }

        private struct VehicleBodyView { public float Yaw, Speed; }

        private VehicleBodyView Body() { return Body(_car); }

        private static VehicleBodyView Body(Car car)
        {
            return new VehicleBodyView { Yaw = car.Body.Rotation.YawDeg, Speed = Vec3.Dot(car.Body.Velocity, car.Body.Rotation.Forward) };
        }

        private Car SpawnCarAhead(float metres)
        {
            Vec3 at = W.Player.Position + Quat.Yaw(W.PlayerState.ViewYawDeg) * new Vec3(3f, 0f, metres);
            return SpawnCarAt(at, W.PlayerState.ViewYawDeg);
        }

        private Car SpawnCarAt(Vec3 ground, float yaw)
        {
            int model = W.City.FindVehicleModel(VehicleKind.Car, 7u);
            return model < 0 ? null : W.SpawnCar(model, ground, yaw, CarOwner.Civilian, 0xB0B0B0);
        }

        private float NearestCop()
        {
            float best = 9999f;
            foreach (Ped p in W.Peds)
                if (p.Kind == PedKind.Cop && !p.IsDead) best = Math.Min(best, Vec3.DistanceXZ(p.Position, W.Player.Position));
            return best;
        }

        private bool Saw(GameEventType type)
        {
            foreach (GameEvent e in _events) if (e.Type == type) return true;
            return false;
        }

        private void CollectEvents()
        {
            if (W == null) return;
            if (_cursor < 0) _cursor = W.Events.NextSeq;
            GameEvent e;
            while (W.Events.Read(ref _cursor, out e)) _events.Add(e);
        }

        private void FrameTiming()
        {
            float now = Time.realtimeSinceStartup;
            if (_frames > 0)
            {
                float dt = now - _lastFrameAt;
                if (dt > _worstFrame) _worstFrame = dt;
                if (dt > 1f / 30f) _slowFrames++;
            }
            _lastFrameAt = now;
            _frames++;
        }

        private void Shot(StepRecord r, string label)
        {
            string name = (++_shotIndex).ToString("00") + "-" + r.Id + "-" + label + ".png";
            try
            {
                Application.CaptureScreenshot(Path.Combine(_shots, name));
                r.Screenshots.Add("selftest-screenshots/" + name);
            }
            catch (Exception e) { _log.Error("self-test screenshot " + name, e); }
        }

        private void Totals()
        {
            if (_session == null) return;
            PhaseTotals p = _session.Totals;
            double n = Math.Max(1, p.Frames);
            _report.Performance.Set("play_frames", p.Frames).Set("sim_ms_avg", p.SimMs / n).Set("sim_ms_max", p.SimMaxMs)
                .Set("stream_ms_avg", p.StreamMs / n).Set("render_ms_avg", p.RenderMs / n).Set("audio_ui_ms_avg", p.AudioUiMs / n)
                .Set("session_faults", _session.Faults).Set("exit_fault", _session.ExitFault);
        }

        private void Finish()
        {
            _finished = true;
            if (_pc != null)
            {
                _pc.InputOverride = null;
                if (_pc.Playing && _session != null) _session.ReturnToCityBuilder();
            }
            if (_report.Performance.Get("play_frames") == null) Totals();
            float seconds = Time.realtimeSinceStartup - _start;
            _report.Performance.Set("run_seconds", seconds).Set("fps_avg", _frames / Math.Max(0.001f, seconds))
                .Set("worst_frame_ms", _worstFrame * 1000f).Set("frames_over_33ms", _slowFrames);
            string since = LogScan.Since(ReadLog(), _logStartLength);
            _report.ExceptionCount = LogScan.CountErrors(since);
            _report.ExceptionLines.AddRange(LogScan.ErrorLines(since, 20));
            _report.Aborted = _runner.Aborted;
            _report.AbortReason = _runner.AbortReason ?? "";
            _report.Steps.AddRange(_runner.Records);
            string json = Path.Combine(_folder, "selftest-report.json");
            try
            {
                File.WriteAllText(json, _report.ToJson());
                File.WriteAllText(Path.Combine(_folder, "selftest-report.txt"), _report.ToText());
                _log.Info(_report.SummaryLine() + " - report at " + json);
                _log.Info("self-test performance: " + Json.Write(_report.Performance));
            }
            catch (Exception e) { _log.Error("self-test report", e); }
            _written();
        }

        private string ReadLog()
        {
            if (string.IsNullOrEmpty(_logPath)) return "";
            try
            {
                using (var fs = new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs)) return sr.ReadToEnd();
            }
            catch (Exception) { return ""; }
        }

        private static bool Same(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static string V(Vector3 v) { return v.x.ToString("0.0") + "," + v.y.ToString("0.0") + "," + v.z.ToString("0.0"); }

        private static string V(Vec3 v) { return v.X.ToString("0.0") + "," + v.Y.ToString("0.0") + "," + v.Z.ToString("0.0"); }

        // The road segment (NetSegment Created, RoadBaseAI) whose middle is nearest the camera (NetManager.cs:1669, NetSegment.cs:14/:83, RoadBaseAI.cs:8).
        private static bool NearestRoad(Vector3 near, out Vector3 at)
        {
            at = Vector3.zero;
            if (!NetManager.exists) return false;
            NetSegment[] segs = Singleton<NetManager>.instance.m_segments.m_buffer;
            float best = float.MaxValue;
            for (int i = 1; i < segs.Length; i++)
            {
                if ((segs[i].m_flags & NetSegment.Flags.Created) == 0) continue;
                NetInfo info = segs[i].Info;
                if (info == null || !(info.m_netAI is RoadBaseAI)) continue;
                Vector3 m = segs[i].m_middlePosition;
                float d = (new Vector2(m.x, m.z) - new Vector2(near.x, near.z)).sqrMagnitude;
                if (d < best) { best = d; at = m; }
            }
            return best < float.MaxValue;
        }
    }
}
