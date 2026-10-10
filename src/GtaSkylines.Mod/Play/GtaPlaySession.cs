using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using ColossalFramework.UI;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Combat;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Session;
using GtaSkylines.Game.Vehicles;
using GtaSkylines.Game.World;
using GtaSkylines.Mod.Audio;
using GtaSkylines.Mod.City;
using GtaSkylines.Mod.Lifecycle;
using GtaSkylines.Mod.Hud;
using GtaSkylines.Mod.Menus;
using GtaSkylines.Mod.Rendering;
using Skylines.Host;
using UnityEngine;
using Renderer = GtaSkylines.Mod.Rendering.Renderer;

namespace GtaSkylines.Mod.Play
{
    /// <summary>
    /// The real play session (replaces M1's free camera). Per frame from Update: menu keys → city tick (roads, collision
    /// streaming, snapshot) → <see cref="GameSession.Tick"/> → city effects → render frame filled and handed to the
    /// renderer (camera driven here, before RenderManager.LateUpdate reads it for culling) → audio. LateUpdate re-applies
    /// the camera and the indoor lighting override; OnGUI draws the HUD and menus. Each phase is guarded on its own so
    /// one bad frame logs and play continues. Every override (lighting, ducking, audio, menus) is undone in
    /// <see cref="End"/>, which the controller calls on exit, level unload, mod disable and errors.
    /// </summary>
    public sealed class GtaPlaySession : IPlaySession, IPlayHost, IUiSounds, IPromotionGate
    {
        private const double StreamBudgetMs = 2.0;
        private const double PrimeBudgetMs = 250.0;
        private const float PerfLogSeconds = 10f;
        private const int MaxLoggedFaults = 20;
        private const uint MenuMask = (1u << (int)GameButton.Phone) | (1u << (int)GameButton.InteractionMenu)
            | (1u << (int)GameButton.Pause) | (1u << (int)GameButton.BigMap) | (1u << (int)GameButton.MenuAccept)
            | (1u << (int)GameButton.MenuBack) | (1u << (int)GameButton.MenuUp) | (1u << (int)GameButton.MenuDown)
            | (1u << (int)GameButton.MenuLeft) | (1u << (int)GameButton.MenuRight);

        private static readonly FieldInfo s_lastSaveName = typeof(SavePanel).GetField("m_LastSaveName", BindingFlags.NonPublic | BindingFlags.Static);

        private readonly SessionServices _s;
        private readonly HostLog _log;
        private readonly CitySnapshot _snapshot = new CitySnapshot();
        private readonly WorldFrameBuilder _frames = new WorldFrameBuilder();
        private readonly FootstepClock _steps = new FootstepClock();
        private readonly Dictionary<uint, ushort> _pendingCitizens = new Dictionary<uint, ushort>();
        private readonly Stopwatch _watch = new Stopwatch();
        private PlayerProfile _profile = new PlayerProfile();
        private CityServices _city;
        private GameParts _game;
        private Renderer _renderer;
        private GtaUi _ui;
        private GameObject _host;
        private AudioEvents _audio;
        private VoiceHandle _engine, _screech, _siren, _horn;
        private int _sirenCar;
        private long _audioCursor = -1;
        private int _stars, _unread;
        private BigScreenKind _bigScreen;
        private bool _menuOpen, _paused, _running;
        private string _batName;
        private int _faults;
        private float _perfSeconds;
        private int _perfFrames, _perfSteps;
        private double _simMs, _streamMs, _simMaxMs;

        /// <summary>A session using the lifecycle's services.</summary>
        public GtaPlaySession(SessionServices services, HostLog log)
        {
            _s = services;
            _log = log;
            _frames.Gate = this;
        }

        /// <inheritdoc />
        public bool WantsExit { get; private set; }

        /// <summary>The running game (null before <see cref="Begin"/>).</summary>
        public GameParts Game { get { return _game; } }

        /// <inheritdoc />
        public void Begin(Vector3 spawnFeet, float yawDeg)
        {
            _city = new CityServices(_log);
            _game = GameFactory.Create(_city.City, _s.Settings, _profile, (uint)Environment.TickCount);
            GameWorld w = _game.World;
            _city.Attach(w.Collision, w.Roads);
            _city.Collision.Tick(spawnFeet, Vector3.zero, PrimeBudgetMs);
            _city.Snapshot.Fill(_snapshot, spawnFeet);
            _renderer = new Renderer(_log, _city.Catalogues.CitizenInfoOf, _city.Catalogues.VehicleInfoOf);
            _renderer.OnPlayBegin();
            _host = new GameObject("GtaSkylines.Play");
            _audio = new AudioEvents(AudioPlayer.Create(_host.transform));
            _ui = new GtaUi(_log, _s.Settings, new GameMenuCommands(_game, this), this, w.Roads, _city.City);
            _batName = WeaponTable.Get(WeaponId.Bat).Name;
            _frames.Reset();
            _steps.Reset();
            _game.Session.Begin(V(spawnFeet), yawDeg);
            _stars = 0;
            _unread = _game.Missions.Phone.UnreadCount;
            _bigScreen = BigScreenKind.None;
            _running = true;
            _log.Info("game session started: " + _city.Catalogues.VehicleCount + " vehicle and " + _city.Catalogues.CitizenCount
                + " citizen models, " + w.Roads.SegmentCount + " road segments");
        }

        /// <inheritdoc />
        public void Tick(float dt, InputFrame input)
        {
            if (!_running) return;
            GameSession session = _game.Session;
            GameWorld w = session.World;
            try { MenuKeys(input); }
            catch (Exception e) { Fault("menus", e); }
            InputFrame game = Filter(input);

            Vector3 feet = U(w.Player.Position);
            _city.Effects.ChirpNear = feet;
            _watch.Reset();
            _watch.Start();
            try { _city.Tick(dt, feet, U(w.Player.Motor.Velocity), _snapshot, StreamBudgetMs); }
            catch (Exception e) { Fault("city tick", e); }
            _streamMs += _watch.Elapsed.TotalMilliseconds;

            _watch.Reset();
            _watch.Start();
            session.Tick(dt, game, _snapshot);
            double ms = _watch.Elapsed.TotalMilliseconds;
            _simMs += ms;
            if (ms > _simMaxMs) _simMaxMs = ms;
            _perfSteps += session.StepsLastTick;

            try
            {
                TrackPromotions(session.Effects);
                _city.Effects.Apply(session.Effects);
            }
            catch (Exception e) { Fault("effects", e); }
            if (session.ConsumeSaveRequest()) SaveGame();

            try
            {
                RenderFrame frame = _renderer.Begin();
                _frames.Fill(frame, _game, dt);
                _renderer.Draw();
                _renderer.End(_s.Camera);
            }
            catch (Exception e) { Fault("render frame", e); }
            try { Audio(w); }
            catch (Exception e) { Fault("audio", e); }
            try
            {
                _ui.Tick();
                if (_ui.PhoneOpen) ViewFill.Phone(_ui.PhoneView, _game);
            }
            catch (Exception e) { Fault("ui tick", e); }
            Perf(dt);
        }

        /// <inheritdoc />
        public void LateTick()
        {
            if (!_running) return;
            try { _renderer.End(_s.Camera); }
            catch (Exception e) { Fault("late render", e); }
        }

        /// <inheritdoc />
        public void Gui()
        {
            if (!_running) return;
            _ui.OnGui(_game.Session.Hud, _game.World.Menu);
            HudRenderer hud = _ui.Hud;
            if (hud == null) return;
            if (!_ui.PauseOpen) hud.NorthUp = _s.Settings.MinimapNorthUp;
            else if (hud.NorthUp != _s.Settings.MinimapNorthUp)
            {
                _s.Settings.MinimapNorthUp = hud.NorthUp;
                ModRuntime.SettingsChanged();
            }
        }

        /// <inheritdoc />
        public void End()
        {
            bool was = _running;
            _running = false;
            if (was) Guard("end game", () => _profile = _game.Session.End());
            if (_renderer != null) Guard("renderer", _renderer.OnPlayEnd);
            if (_audio != null) Guard("audio", () =>
            {
                _audio.SetIndoors(false);
                _audio.SetPaused(false);
                _audio.Stop(ref _engine);
                _audio.Stop(ref _screech);
                _audio.Stop(ref _siren);
                _audio.Stop(ref _horn);
            });
            Guard("menu", () => SetMenu(false));
            if (_ui != null) Guard("ui", _ui.Dispose);
            if (_city != null) Guard("city", _city.Dispose);
            if (_host != null) Guard("audio object", () => UnityEngine.Object.Destroy(_host));
            _renderer = null;
            _audio = null;
            _ui = null;
            _city = null;
            _host = null;
            _pendingCitizens.Clear();
        }

        /// <inheritdoc />
        public byte[] SaveProfile()
        {
            if (_running)
            {
                Ped p = _game.World.Player;
                if (p != null)
                {
                    _profile.Health = p.IsDead || p.Health <= 0f ? PlayerProfile.MaxVital : (int)Math.Round(p.Health);
                    _profile.Armour = (int)Math.Round(p.Armour);
                    _profile.OutfitId = p.Model;
                }
            }
            return ProfileRecord.Encode(_profile);
        }

        /// <inheritdoc />
        public void LoadProfile(byte[] data)
        {
            ProfileDecodeResult r = data == null ? null : ProfileRecord.Decode(data);
            _profile = r != null && r.Ok ? r.Profile : new PlayerProfile();
        }

        // ---- menus ---------------------------------------------------------------------------------------------------

        private void MenuKeys(InputFrame input)
        {
            GameWorld w = _game.World;
            if (input.WasPressed(GameButton.Pause) && !_ui.Rebinding)
            {
                if (_ui.PauseOpen) _ui.ClosePause();
                else
                {
                    _ui.OpenPause(U(w.Player.Position));
                    FillPause();
                }
            }
            if (input.WasPressed(GameButton.Phone)) _ui.TogglePhone();
            if (input.WasPressed(GameButton.InteractionMenu))
            {
                ViewFill.Interaction(_ui.InteractionView, _game);
                _ui.ToggleInteraction();
            }
            if (input.WasPressed(GameButton.BigMap)) _ui.ToggleBigMap();
            if (_game.Combat.Wheel.IsOpen)
            {
                if (!_ui.WheelOpen)
                {
                    ViewFill.Wheel(_ui.WheelView, w.Profile, w.Player.CurrentWeapon);
                    _ui.OpenWheel();
                }
                _ui.FeedWheel(input.LookYawDeg, input.LookPitchDeg);
            }
            else if (_ui.WheelOpen) _ui.CloseWheel(true);
            bool paused = _ui.PauseOpen;
            if (paused != _paused)
            {
                _paused = paused;
                _game.Session.Paused = paused;
                _audio.SetPaused(paused);
            }
            SetMenu(_ui.WantsCursor);
        }

        private InputFrame Filter(InputFrame input)
        {
            if (_ui.PauseOpen || _ui.WantsCursor || _ui.Rebinding) return default(InputFrame);
            input.Pressed &= ~MenuMask;
            input.Held &= ~MenuMask;
            input.Released &= ~MenuMask;
            return input;
        }

        private void SetMenu(bool open)
        {
            if (open == _menuOpen) return;
            _menuOpen = open;
            _s.SetMenuOpen(open);
        }

        private void FillPause()
        {
            GameWorld w = _game.World;
            PauseView v = _ui.PauseView;
            v.MissionTitle = w.Mission.Active ? w.Mission.Title : null;
            v.Objective = w.Mission.Active ? w.Mission.Objective : null;
            v.CashText = GtaSkylines.Game.Hud.HudModel.FormatCash(w.Profile.Cash);
            v.CityName = w.City.CityName;
            v.Stats.Clear();
            v.Stats.Add(new KeyValuePair<string, string>("Heat", w.Profile.Heat.ToString("0")));
            foreach (KeyValuePair<string, long> kv in w.Profile.Stats) v.Stats.Add(new KeyValuePair<string, string>(kv.Key.Replace('_', ' '), kv.Value.ToString()));
        }

        // ---- IPlayHost, IUiSounds --------------------------------------------------------------------------------------

        /// <inheritdoc />
        public void Resume() { _ui.ClosePause(); }

        /// <inheritdoc />
        public void ApplySettings() { ModRuntime.SettingsChanged(); }

        /// <summary>Snapshots the profile into the store, then starts CS1's own save under the last save name.</summary>
        public void SaveGame()
        {
            Guard("save", () =>
            {
                ModRuntime.Profiles.Update(SaveProfile());
                SavePanel panel = UIView.library != null ? UIView.library.Get<SavePanel>("SavePanel") : null;
                string name = s_lastSaveName == null ? null : s_lastSaveName.GetValue(null) as string;
                if (panel == null || string.IsNullOrEmpty(name) || SavePanel.isSaving)
                {
                    _game.World.Messages.ShowHelp("The game cannot save right now.", 2, 3f);
                    return;
                }
                if (panel.SaveGame(name)) _game.World.Messages.ShowHelp("Game saved.", 2, 3f);
            });
        }

        /// <inheritdoc />
        public void ReturnToCityBuilder() { WantsExit = true; }

        void IUiSounds.Navigate() { _audio.Ui(UiSound.Move); }
        void IUiSounds.Select() { _audio.Ui(UiSound.Select); }
        void IUiSounds.Back() { _audio.Ui(UiSound.Back); }
        void IUiSounds.Error() { _audio.Ui(UiSound.Back); }
        void IUiSounds.Purchase() { _audio.Ui(UiSound.Purchase); }
        void IUiSounds.Open() { _audio.Ui(UiSound.Select); }
        void IUiSounds.Tab() { _audio.Ui(UiSound.Move); }

        // ---- promotion -------------------------------------------------------------------------------------------------

        private void TrackPromotions(IList<CityEffect> effects)
        {
            for (int i = 0; i < effects.Count; i++)
                if (effects[i].Kind == CityEffectKind.TakeCitizen) _pendingCitizens[effects[i].Citizen] = effects[i].Instance;
        }

        /// <summary>A car taken from CS1 is drawn from the frame its CS1 original is released (no double, no gap).</summary>
        public bool CarReady(Car car)
        {
            return car.SourceCs1Vehicle == 0 || !_city.Effects.Taken.Vehicle(car.SourceCs1Vehicle, car.SourceParked);
        }

        /// <summary>A promoted citizen is drawn from the frame its CS1 instance is released.</summary>
        public bool PedReady(Ped ped)
        {
            ushort instance;
            if (ped.Citizen == 0 || !_pendingCitizens.TryGetValue(ped.Citizen, out instance)) return true;
            if (_city.Effects.Taken.Instance(instance)) return false;
            _pendingCitizens.Remove(ped.Citizen);
            return true;
        }

        // ---- audio -----------------------------------------------------------------------------------------------------

        private void Audio(GameWorld w)
        {
            if (_audioCursor < 0) _audioCursor = w.Events.NextSeq;
            GameEvent e;
            while (w.Events.Read(ref _audioCursor, out e))
            {
                Vector3 at = U(e.Type == GameEventType.Shot ? e.Position2 : e.Position);
                switch (AudioCues.ForEvent(e, _batName))
                {
                    case AudioCue.Gunshot:
                        Ped shooter = w.PedFromTag(e.ActorA, false, 0);
                        _audio.Gunshot(shooter == null ? WeaponId.Pistol : (WeaponId)shooter.CurrentWeapon, at);
                        break;
                    case AudioCue.BulletImpact: _audio.BulletImpact(at); break;
                    case AudioCue.Punch: _audio.Punch(at); break;
                    case AudioCue.BatHit: _audio.BatHit(at); break;
                    case AudioCue.Crash: _audio.Crash(e.Value, at); break;
                    case AudioCue.Explosion: _audio.Explosion(at); break;
                    case AudioCue.Land: _audio.Land(at, 9f + e.Value / 9f); break;
                    case AudioCue.Purchase: _audio.Ui(UiSound.Purchase); break;
                }
            }
            if (AudioCues.WantedUp(_stars, w.Wanted.Stars)) _audio.Ui(UiSound.WantedUp);
            _stars = w.Wanted.Stars;
            int unread = _game.Missions.Phone.UnreadCount;
            if (AudioCues.NewText(_unread, unread)) _audio.Ui(UiSound.TextTone);
            _unread = unread;
            UiSound jingle;
            BigScreenKind big = _game.Session.Hud.BigScreen;
            if (AudioCues.ForBigScreen(_bigScreen, big, out jingle)) _audio.Ui(jingle);
            _bigScreen = big;

            InteriorPhase phase = w.Interior.Phase;
            _audio.SetIndoors(w.Interior.IsInside || (phase == InteriorPhase.FadingOut && w.Interior.Layout != null));

            Ped p = w.Player;
            Car car = p.VehicleId == 0 ? null : w.FindCar(p.VehicleId);
            if (car == null)
            {
                _audio.Stop(ref _engine);
                _audio.Stop(ref _screech);
                _audio.Stop(ref _horn);
                if (_steps.Advance(p.Position, p.Motor.OnGround, p.Motor.Velocity.LengthXZ)) _audio.Footstep(p.Motor.GroundFlags, U(p.Position));
            }
            else
            {
                _steps.Reset();
                Vector3 at = U(car.Position);
                VehicleBody b = car.Body;
                if (!_engine.IsSet) _engine = _audio.StartEngine(at);
                _audio.UpdateEngine(_engine, at, b.EngineRpm, car.Controls.Throttle);
                if (!_screech.IsSet) _screech = _audio.StartScreech(at);
                float slip = 0f;
                for (int i = 0; i < VehicleBody.WheelCount; i++) slip = Math.Max(slip, b.GetWheel(i).SlipSpeed);
                _audio.UpdateScreech(_screech, at, Mathf.Clamp01(slip / 8f));
                if (car.Horn && !_horn.IsSet) _horn = _audio.StartHorn(at);
                else if (!car.Horn) _audio.Stop(ref _horn);
                else _audio.Move(_horn, at);
            }
            Siren(w);
        }

        private void Siren(GameWorld w)
        {
            Car best = null;
            float bestSq = 150f * 150f;
            Vec3 me = w.Player.Position;
            IList<Car> cars = w.Cars;
            for (int i = 0; i < cars.Count; i++)
            {
                Car c = cars[i];
                if (!c.Siren || c.Destroyed) continue;
                float d = (c.Position - me).LengthSquared;
                if (d < bestSq) { bestSq = d; best = c; }
            }
            if (best == null || (_siren.IsSet && best.Id != _sirenCar)) _audio.Stop(ref _siren);
            if (best == null) return;
            if (!_siren.IsSet)
            {
                _siren = _audio.StartSiren(AudioCues.SirenFor(best.Model.Kind), U(best.Position));
                _sirenCar = best.Id;
            }
            else _audio.Move(_siren, U(best.Position));
        }

        // ---- budget, faults ------------------------------------------------------------------------------------------

        private void Perf(float dt)
        {
            _perfFrames++;
            _perfSeconds += dt;
            if (_perfSeconds < PerfLogSeconds) return;
            GameWorld w = _game.World;
            _log.Info("perf: sim " + (_simMs / _perfFrames).ToString("0.00") + " ms avg (max " + _simMaxMs.ToString("0.0") + "), stream "
                + (_streamMs / _perfFrames).ToString("0.00") + " ms avg, " + _perfFrames + " frames, " + _perfSteps + " steps, "
                + w.Peds.Count + " peds, " + w.Cars.Count + " cars, " + _game.Police.ActiveUnitCount + " police units, "
                + _city.Collision.LoadedCount + " regions");
            _perfSeconds = 0f;
            _perfFrames = _perfSteps = 0;
            _simMs = _streamMs = _simMaxMs = 0;
        }

        private void Guard(string what, Action a)
        {
            try { a(); }
            catch (Exception e) { Fault(what, e); }
        }

        private void Fault(string what, Exception e)
        {
            if (_faults++ < MaxLoggedFaults) _log.Error("play session: " + what, e);
        }

        private static Vec3 V(Vector3 v) { return new Vec3(v.x, v.y, v.z); }

        private static Vector3 U(Vec3 v) { return new Vector3(v.X, v.Y, v.Z); }
    }
}
