using System;
using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.UI;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Session;
using GtaSkylines.Mod.City;
using GtaSkylines.Game.Settings;
using GameSettings = GtaSkylines.Game.Settings.GameSettings;
using GtaSkylines.Mod.Input;
using GtaSkylines.Mod.Persistence;
using Skylines.Host;
using Skylines.Host.Camera;
using Skylines.Host.Input;
using Skylines.Host.Saves;
using UnityEngine;
using UInput = UnityEngine.Input;

namespace GtaSkylines.Mod.Lifecycle
{
    /// <summary>
    /// The one controller object of a loaded city: enter play (Ctrl+Shift+G at the terrain under the cursor, Ctrl+Shift+H or a click on the walk-in
    /// button at the last safehouse, or a walk-in drop) through the unpause and first-entry backup gates; while playing, camera taken over, CS1's UI hidden
    /// and its shortcuts blocked (<see cref="ShortcutBlocker"/>), cursor locked, simulation slowed, input mapped to the
    /// session; exit restores all of it. Every Unity callback is exception-guarded. Main thread only.
    /// </summary>
    public sealed class PlayController : MonoBehaviour
    {
        private const int MaxLoggedErrors = 20;

        private static PlayController s_current;

        private readonly CameraTakeover _camera = new CameraTakeover();
        private readonly ShortcutBlocker _blocker = new ShortcutBlocker();
        private readonly InputCapture _capture = new InputCapture(new int[0]);
        private readonly InputMapper _mapper = new InputMapper();
        private HostLog _log;
        private ProfileStore _store;
        private GameSettings _settings;
        private SimRate _simRate;
        private Action _pauseSim, _resumeSim;
        private RenderOverrides _render;
        private PauseGate _pauseGate;
        private FirstEntryBackup _backupGate;
        private WalkInButton _walkIn;
        private CityAdapter _spawnCity;
        private SessionServices _services;
        private IPlaySession _session;
        private int _settingsVersion = -1;
        private bool _requested;
        private bool _waiting;
        private Vector3 _requestFeet;
        private float _requestYaw;
        private bool _menuPaused;
        private bool _forcedPausedBefore;
        private bool _wasSaving;
        private int _errors;
        private bool _skipBackup;
        private Debug.DebugOverlay _debug;

        /// <summary>True while play mode is on.</summary>
        public bool Playing { get { return _session != null; } }

        /// <summary>The controller of the loaded city, or null (self-test seam).</summary>
        internal static PlayController Current { get { return s_current; } }

        /// <summary>The running session, null when not playing (self-test seam).</summary>
        internal IPlaySession Session { get { return _session; } }

        /// <summary>True while the city camera is taken over (self-test seam).</summary>
        internal bool CameraTaken { get { return _camera.Active; } }

        /// <summary>Self-test seam: when set, replaces the keyboard and mouse frame on every playing frame.</summary>
        internal Func<InputFrame, InputFrame> InputOverride;

        /// <summary>
        /// Self-test seam: requests entry like a walk-in drop, through the same gates. The first-entry backup offer is
        /// skipped in the new game the autoloader started (nothing there to protect) and auto-declined in an unattended run
        /// (nobody to answer; the self-test never saves, so the city on disk stays as it was).
        /// </summary>
        internal void RequestScripted(Vector3 feet, float yawDeg, bool unattended)
        {
            if (unattended) _backupGate.AutoDecline("unattended self-test");
            _skipBackup = SaveAutoloader.InOurNewGame();
            Request(feet, yawDeg);
        }

        /// <summary>Creates the controller for the loaded city (replacing a stale one).</summary>
        internal static void Create()
        {
            DestroyCurrent();
            var go = new GameObject("GtaSkylines.Controller");
            s_current = go.AddComponent<PlayController>();
            s_current.Init();
        }

        /// <summary>
        /// Leaves play mode immediately, removes the walk-in button and destroys the controller object; also drops the
        /// render hook's target (the hook itself outlives level loads). Called from OnLevelUnloading and OnDisabled.
        /// </summary>
        internal static void DestroyCurrent()
        {
            PlayController c = s_current;
            s_current = null;
            Rendering.RenderHook.Target = null;
            if (c == null) return;
            c.Shutdown();
            Destroy(c.gameObject);
        }

        private void Init()
        {
            _log = ModRuntime.Log;
            _store = ModRuntime.Profiles;
            _settings = ModRuntime.Settings;
            _simRate = new SimRate(_log);
            _pauseSim = PauseSim;
            _resumeSim = ResumeSim;
            _render = new RenderOverrides(_log);
            _pauseGate = new PauseGate(_log, Retry, Cancel);
            _backupGate = new FirstEntryBackup(_log, _store, Retry, Cancel);
            _walkIn = new WalkInButton(_log, hit => Request(hit, CurrentYaw()), RequestAtSafehouse);
            _walkIn.Create();
            _debug = new Debug.DebugOverlay(_log);
            _services = new SessionServices { Camera = _camera, Settings = _settings, SetMenuOpen = SetMenuOpen };
            _log.Info("controller created");
        }

        private void Update()
        {
            try
            {
                ModRuntime.FlushSettings(false);
                _simRate.Tick();
                _blocker.Tick();
                _backupGate.Update();
                _walkIn.Update(Playing);
                if (Playing) UpdatePlaying();
                else UpdateOff();
                _debug.Update(_session as GtaSkylines.Mod.Play.GtaPlaySession);
            }
            catch (Exception e)
            {
                Failed("Update", e);
            }
        }

        private void LateUpdate()
        {
            if (!Playing) return;
            try
            {
                _session.LateTick();
                _render.Apply();
            }
            catch (Exception e)
            {
                Failed("LateUpdate", e);
            }
        }

        private void OnGUI()
        {
            if (!Playing) return;
            try
            {
                _session.Gui();
                _debug.Gui();
            }
            catch (Exception e) { Failed("OnGUI", e); }
        }

        private void OnDestroy()
        {
            if (s_current == this) s_current = null;
            try { Shutdown(); }
            catch (Exception e) { Failed("OnDestroy", e); }
        }

        private void UpdateOff()
        {
            if (EnterKeyPressed(KeyCode.G)) RequestAtCursor();
            else if (EnterKeyPressed(KeyCode.H) && !RequestAtSafehouse()) RequestAtCursor();
            if (_requested && !_waiting) Proceed();
        }

        private void UpdatePlaying()
        {
            if (_settingsVersion != ModRuntime.SettingsVersion)
            {
                RebuildInput();
                _simRate.SetRate(SimRateSetting.Multiplier(_settings.SimulationRate));
            }
            InputFrame frame = _mapper.Read(_capture, _settings);
            if (InputOverride != null) frame = InputOverride(frame);
            _session.Tick(Time.deltaTime, frame);
            if (_session.WantsExit)
            {
                string fault = _session.ExitFault;
                if (fault == null) Exit("player returned to the city builder", false);
                else ExitWithNotice(fault);
                return;
            }
            // A CS1 save (ours, a quicksave or an autosave) starts with a screenshot coroutine before serialising, so a
            // snapshot taken on the frame SavePanel.isSaving rises (SavePanel.cs:85, :370-380) is in the save.
            bool saving = SavePanel.isSaving;
            if (saving && !_wasSaving) _store.Update(_session.SaveProfile());
            _wasSaving = saving;
        }

        private static bool EnterKeyPressed(KeyCode key)
        {
            return UInput.GetKeyDown(key)
                && (UInput.GetKey(KeyCode.LeftControl) || UInput.GetKey(KeyCode.RightControl))
                && (UInput.GetKey(KeyCode.LeftShift) || UInput.GetKey(KeyCode.RightShift))
                && !UIView.HasModalInput() && !UIView.HasInputFocus();
        }

        private void RequestAtCursor()
        {
            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam == null || !TerrainManager.exists) return;
            Ray ray = cam.ScreenPointToRay(UInput.mousePosition);
            Vector3 hit;
            DropSurface surface = SurfaceUnderRay(new Segment3(ray.origin, ray.origin + ray.direction * cam.farClipPlane), out hit);
            if (surface == DropSurface.None)
            {
                _log.Info("play mode: no surface under the cursor");
                return;
            }
            _log.Info("play mode: drop on " + surface);
            Request(hit, cam.transform.eulerAngles.y);
        }

        // The walkable surface nearest along the ray: roads, paths and bridges (NetManager.RayCast, NetManager.cs:4136), building
        // roofs (BuildingManager.RayCast, BuildingManager.cs:6389) and terrain (TerrainManager.cs:1065), chosen like
        // ToolBase.RayCast (ToolBase.cs:562-600) with the same service, layer and flag defaults as the walk-in drop (MapDropTool).
        private static DropSurface SurfaceUnderRay(Segment3 ray, out Vector3 hit)
        {
            var pick = new SurfacePick();
            hit = Vector3.zero;
            Vector3 h;
            if (Singleton<TerrainManager>.instance.RayCast(ray, out h) && pick.Offer(DropSurface.Terrain, Vector3.Distance(h, ray.a))) hit = h;
            ushort node, segment, building;
            if (NetManager.exists && Singleton<NetManager>.instance.RayCast(null, ray, -1000f, false, ItemClass.Service.None, ItemClass.Service.None,
                    ItemClass.SubService.None, ItemClass.SubService.None, ItemClass.Layer.Default, ItemClass.Layer.Default,
                    NetNode.Flags.None, NetSegment.Flags.None, out h, out node, out segment)
                && pick.Offer(DropSurface.Road, Vector3.Distance(h, ray.a))) hit = h;
            if (BuildingManager.exists && Singleton<BuildingManager>.instance.RayCast(ray, ItemClass.Service.None, ItemClass.SubService.None,
                    ItemClass.Layer.Default, Building.Flags.None, out h, out building)
                && pick.Offer(DropSurface.Building, Vector3.Distance(h, ray.a))) hit = h;
            return pick.Surface;
        }

        // True when the profile has a last safehouse that still exists and entry was requested at its door.
        private bool RequestAtSafehouse()
        {
            if (Playing || _waiting) return true;
            ProfileDecodeResult r = _store.Record == null ? null : ProfileRecord.Decode(_store.Record);
            if (r == null || !r.Ok) return false;
            if (_spawnCity == null) _spawnCity = new CityAdapter(_log, new Catalogues(_log));
            Vec3 feet;
            float yaw;
            if (!SpawnPoint.TryLastSafehouse(r.Profile, _spawnCity, out feet, out yaw)) return false;
            Request(new Vector3(feet.X, feet.Y, feet.Z), yaw);
            return true;
        }

        private float CurrentYaw()
        {
            UnityEngine.Camera cam = UnityEngine.Camera.main;
            return cam == null ? 0f : cam.transform.eulerAngles.y;
        }

        private void Request(Vector3 feet, float yawDeg)
        {
            if (Playing || _waiting) return;
            _requested = true;
            _requestFeet = feet;
            _requestYaw = yawDeg;
        }

        private void Proceed()
        {
            string wait = _pauseGate.Check() ?? (_skipBackup ? null : _backupGate.Check());
            if (!_requested) return;
            if (wait != null)
            {
                _waiting = true;
                return;
            }
            _requested = false;
            Enter(_requestFeet, _requestYaw);
        }

        private void Retry() { _waiting = false; }

        private void Cancel()
        {
            _waiting = false;
            _requested = false;
        }

        private void Enter(Vector3 feet, float yawDeg)
        {
            string refusal = _camera.Acquire();
            if (refusal != null)
            {
                _log.Info("play mode refused: " + refusal);
                return;
            }
            try
            {
                _simRate.Begin(SimRateSetting.Multiplier(_settings.SimulationRate));
                _render.Begin();
                string blocked = _blocker.Begin();
                if (blocked != null) throw new InvalidOperationException("shortcut blocker: " + blocked);
                _capture.Begin();
                RebuildInput();
                _store.MarkEntered();
                _wasSaving = SavePanel.isSaving;
                IPlaySession session = UInput.GetKey(KeyCode.LeftAlt) ? (IPlaySession)new FreeCameraSession(_services) : new GtaSkylines.Mod.Play.GtaPlaySession(_services, _log);
                _session = session;
                session.LoadProfile(_store.Record);
                session.Begin(feet, yawDeg);
                _log.Info("play mode on at (" + feet.x.ToString("0.0") + ", " + feet.y.ToString("0.0") + ", " + feet.z.ToString("0.0") + "), yaw " + yawDeg.ToString("0"));
            }
            catch (Exception e)
            {
                _log.Error("play mode enter", e);
                Exit("could not enter: " + e.Message, true);
            }
        }

        private void RebuildInput()
        {
            _settingsVersion = ModRuntime.SettingsVersion;
            _mapper.Rebuild(_settings.Bindings, _log.Warn);
        }

        /// <summary>
        /// Leaves play mode and restores session, pause, sim rate, renderer, cursor, camera and shortcuts. Never throws.
        /// <paramref name="immediate"/> removes the shortcut blocker now instead of after Esc is released.
        /// </summary>
        private void Exit(string reason, bool immediate)
        {
            IPlaySession session = _session;
            _session = null;
            Guard("debug overlay", _debug.Hide);
            if (session != null)
            {
                Guard("save profile", () => _store.Update(session.SaveProfile()));
                Guard("end session", session.End);
            }
            Guard("menu pause", () => SetMenuOpen(false));
            Guard("restore sim rate", _simRate.End);
            Guard("restore render", _render.End);
            Guard("release input", _capture.End);
            Guard("restore camera", () =>
            {
                string problems = _camera.Release();
                if (problems != null) _log.Warn("camera restore incomplete: " + problems);
            });
            Guard("release blocker", () =>
            {
                if (immediate)
                {
                    string problem = _blocker.ReleaseNow();
                    if (problem != null) _log.Warn("shortcut blocker still up: " + problem);
                }
                else
                {
                    _blocker.End();
                }
            });
            if (session != null) _log.Info("play mode off: " + reason);
        }

        // The pause menu pauses the simulation the way CS1's own pause menu does: ForcedSimulationPaused written on the
        // simulation thread through AddAction (PauseMenu.cs:257-262, :272-277); the actions run in queue order. Frees the cursor.
        private void SetMenuOpen(bool open)
        {
            if (open == _menuPaused) return;
            _menuPaused = open;
            _capture.CursorFree = open;
            if (!SimulationManager.exists) return;
            Singleton<SimulationManager>.instance.AddAction(open ? _pauseSim : _resumeSim);
        }

        // Simulation thread (also after the controller is gone, so it logs through ModRuntime).
        private void PauseSim()
        {
            try
            {
                SimulationManager sim = Singleton<SimulationManager>.instance;
                _forcedPausedBefore = sim.ForcedSimulationPaused;
                sim.ForcedSimulationPaused = true;
            }
            catch (Exception e) { ModRuntime.Log.Error("menu pause", e); }
        }

        private void ResumeSim()
        {
            try { Singleton<SimulationManager>.instance.ForcedSimulationPaused = _forcedPausedBefore; }
            catch (Exception e) { ModRuntime.Log.Error("menu resume", e); }
        }

        private void Shutdown()
        {
            if (_log == null) return;
            Exit("city unloading or mod disabled", true);
            Cancel();
            Guard("gates", () =>
            {
                _pauseGate.Dispose();
                _backupGate.Dispose();
            });
            Guard("walk-in button", _walkIn.Dispose);
            Guard("debug overlay", _debug.Dispose);
            _log.Info("controller removed");
            _log = null;
        }

        private void Failed(string where, Exception e)
        {
            if (_log == null) return;
            if (_errors < MaxLoggedErrors)
            {
                _errors++;
                _log.Error("controller " + where + (_errors == MaxLoggedErrors ? " (further errors not logged)" : ""), e);
            }
            if (Playing) ExitWithNotice("error in " + where + ": " + e.Message);
        }

        // A fault ended play: restore everything, then tell the player in the city view.
        private void ExitWithNotice(string reason)
        {
            Exit(reason, true);
            Guard("notice", () => GameDialogs.Error("GTA: Skylines", "Play mode stopped: " + reason
                + ".\nThe city has been restored. Details are in ModLogs/GtaSkylines.log."));
        }

        private void Guard(string what, Action step)
        {
            try { step(); }
            catch (Exception e) { _log.Error("play mode exit, " + what, e); }
        }
    }
}
