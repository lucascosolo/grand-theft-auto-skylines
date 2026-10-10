using System;
using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.UI;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Settings;
using GameSettings = GtaSkylines.Game.Settings.GameSettings;
using GtaSkylines.Mod.Input;
using GtaSkylines.Mod.Persistence;
using Skylines.Host;
using Skylines.Host.Camera;
using Skylines.Host.Input;
using UnityEngine;
using UInput = UnityEngine.Input;

namespace GtaSkylines.Mod.Lifecycle
{
    /// <summary>
    /// The one controller object of a loaded city: enter play (Ctrl+Shift+G at the terrain under the cursor, or a
    /// walk-in drop) through the unpause and first-entry backup gates; while playing, camera taken over, CS1's UI hidden
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
        private RenderOverrides _render;
        private PauseGate _pauseGate;
        private FirstEntryBackup _backupGate;
        private WalkInButton _walkIn;
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

        /// <summary>True while play mode is on.</summary>
        public bool Playing { get { return _session != null; } }

        /// <summary>Creates the controller for the loaded city (replacing a stale one).</summary>
        internal static void Create()
        {
            DestroyCurrent();
            var go = new GameObject("GtaSkylines.Controller");
            s_current = go.AddComponent<PlayController>();
            s_current.Init();
        }

        /// <summary>Leaves play mode immediately, removes the walk-in button and destroys the controller object.</summary>
        internal static void DestroyCurrent()
        {
            PlayController c = s_current;
            s_current = null;
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
            _render = new RenderOverrides(_log);
            _pauseGate = new PauseGate(_log, Retry, Cancel);
            _backupGate = new FirstEntryBackup(_log, _store, Retry, Cancel);
            _walkIn = new WalkInButton(_log, hit => Request(hit, CurrentYaw()));
            _walkIn.Create();
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
            try { _session.Gui(); }
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
            if (EnterKeyPressed()) RequestAtCursor();
            if (_requested && !_waiting) Proceed();
        }

        private void UpdatePlaying()
        {
            if (_settingsVersion != ModRuntime.SettingsVersion) RebuildInput();
            InputFrame frame = _mapper.Read(_capture, _settings);
            _session.Tick(Time.deltaTime, frame);
            if (_session.WantsExit)
            {
                Exit("player returned to the city builder", false);
                return;
            }
            // A CS1 save (ours, a quicksave or an autosave) starts with a screenshot coroutine before serialising, so a
            // snapshot taken on the frame SavePanel.isSaving rises (SavePanel.cs:85, :370-380) is in the save.
            bool saving = SavePanel.isSaving;
            if (saving && !_wasSaving) _store.Update(_session.SaveProfile());
            _wasSaving = saving;
        }

        private static bool EnterKeyPressed()
        {
            return UInput.GetKeyDown(KeyCode.G)
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
            if (!Singleton<TerrainManager>.instance.RayCast(new Segment3(ray.origin, ray.origin + ray.direction * cam.farClipPlane), out hit))
            {
                _log.Info("play mode: Ctrl+Shift+G with no terrain under the cursor");
                return;
            }
            Request(hit, cam.transform.eulerAngles.y);
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
            string wait = _pauseGate.Check() ?? _backupGate.Check();
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

        // The pause menu pauses the simulation the way CS1's own pause menu does (ForcedSimulationPaused) and frees the cursor.
        private void SetMenuOpen(bool open)
        {
            if (open == _menuPaused) return;
            _menuPaused = open;
            _capture.CursorFree = open;
            if (!SimulationManager.exists) return;
            SimulationManager sim = Singleton<SimulationManager>.instance;
            if (open)
            {
                _forcedPausedBefore = sim.ForcedSimulationPaused;
                sim.ForcedSimulationPaused = true;
            }
            else
            {
                sim.ForcedSimulationPaused = _forcedPausedBefore;
            }
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
            if (Playing) Exit("error in " + where + ": " + e.Message, true);
        }

        private void Guard(string what, Action step)
        {
            try { step(); }
            catch (Exception e) { _log.Error("play mode exit, " + what, e); }
        }
    }
}
