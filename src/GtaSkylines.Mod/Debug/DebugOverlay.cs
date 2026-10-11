using System;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Police;
using GtaSkylines.Game.Session;
using GtaSkylines.Game.World;
using GtaSkylines.Mod.City;
using GtaSkylines.Mod.Play;
using GtaSkylines.Mod.World;
using Skylines.Host;
using UnityEngine;
using UnityEngine.Rendering;
using UInput = UnityEngine.Input;

namespace GtaSkylines.Mod.Debug
{
    /// <summary>
    /// F-QA-3 debug overlay, Ctrl+Shift+F10 while playing: turns on the collision wireframe (<see cref="CollisionViewer"/>),
    /// labels every ped (kind, brain/anim, health), car (owner, driver, speed, damage) and departing parked-car ghost
    /// (F-VEH-7) within <see cref="LabelRange"/>,
    /// draws each police unit's line to its target point coloured by role, the search zone circle and last known position,
    /// and an on-screen panel with the wanted blackboard, the units and FPS/frame/session/streaming/render times against
    /// <see cref="PerfBudget"/>. Hidden: one key check per frame, nothing allocated. Shown: text rebuilt every
    /// <see cref="RefreshSeconds"/>, drawn from the cached strings. Main thread only.
    /// </summary>
    internal sealed class DebugOverlay
    {
        private const float RefreshSeconds = 0.25f;
        private const float LabelRange = 120f;
        private const int MaxLabels = 64;
        private const int CircleSegments = 48;
        private const float PanelWidth = 430f;

        private static readonly Color[] RoleColours =
        {
            new Color(0.6f, 0.6f, 0.6f), new Color(0.3f, 0.6f, 1f), Color.red, new Color(1f, 0.5f, 0f), Color.yellow,
            Color.magenta, Color.cyan, new Color(1f, 0.2f, 0.6f), Color.green, Color.white, new Color(0.4f, 0.4f, 0.4f),
        };

        private readonly HostLog _log;
        private readonly Camera.CameraCallback _draw;
        private readonly PerfWindow _perf = new PerfWindow(120);
        private readonly Ped[] _peds = new Ped[MaxLabels];
        private readonly string[] _pedText = new string[MaxLabels];
        private readonly Car[] _cars = new Car[MaxLabels];
        private readonly string[] _carText = new string[MaxLabels];
        private readonly Vec3[] _ghosts = new Vec3[MaxLabels];
        private readonly string[] _ghostText = new string[MaxLabels];
        private readonly PoliceUnit[] _units = new PoliceUnit[PoliceSystem.MaxUnits];
        private readonly Car[] _unitCars = new Car[PoliceSystem.MaxUnits];
        private int _pedCount, _carCount, _ghostCount, _unitCount;
        private GtaPlaySession _session;
        private Material _material;
        private GUIStyle _style;
        private readonly GUIContent _panel = new GUIContent("");
        private bool _on;
        private bool _viewerWasOn;
        private float _refreshIn;
        private long _lastFrames;
        private double _lastSim, _lastStream, _lastRender;

        internal DebugOverlay(HostLog log)
        {
            _log = log;
            _draw = Draw;
        }

        /// <summary>Per frame from the controller's Update; <paramref name="session"/> null when not playing a GTA session.</summary>
        public void Update(GtaPlaySession session)
        {
            if (session == null)
            {
                if (_on) SetOn(null);
                return;
            }
            if (KeyPressed())
            {
                SetOn(_on ? null : session);
                _log.Info("debug overlay " + (_on ? "on" : "off"));
            }
            if (!_on) return;
            if (session != _session) SetOn(session);
            Sample();
            _refreshIn -= Time.unscaledDeltaTime;
            if (_refreshIn > 0) return;
            _refreshIn = RefreshSeconds;
            Refresh();
        }

        /// <summary>From OnGUI after the HUD.</summary>
        public void Gui()
        {
            if (!_on) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = false };
                _style.normal.textColor = Color.white;
            }
            Camera cam = Camera.main;
            if (cam != null)
            {
                for (int i = 0; i < _pedCount; i++) Label(cam, _peds[i].Position, 2.1f, _pedText[i]);
                for (int i = 0; i < _carCount; i++) Label(cam, _cars[i].Position, 2.4f, _carText[i]);
                for (int i = 0; i < _ghostCount; i++) Label(cam, _ghosts[i], 2.4f, _ghostText[i]);
            }
            float h = _style.CalcHeight(_panel, PanelWidth);
            var box = new Rect(Screen.width - PanelWidth - 20f, 120f, PanelWidth + 10f, h + 10f);
            GUI.Box(box, GUIContent.none);
            GUI.Label(new Rect(box.x + 5f, box.y + 5f, PanelWidth, h), _panel, _style);
        }

        /// <summary>Turns the overlay off (play mode ending).</summary>
        public void Hide() { SetOn(null); }

        /// <summary>Releases the material.</summary>
        public void Dispose()
        {
            SetOn(null);
            if (_material != null) UnityEngine.Object.Destroy(_material);
            _material = null;
        }

        private static bool KeyPressed()
        {
            return UInput.GetKeyDown(KeyCode.F10)
                && (UInput.GetKey(KeyCode.LeftControl) || UInput.GetKey(KeyCode.RightControl))
                && (UInput.GetKey(KeyCode.LeftShift) || UInput.GetKey(KeyCode.RightShift));
        }

        // Null turns the overlay off; a session turns it on for that session (restoring the previous session's viewer first).
        private void SetOn(GtaPlaySession session)
        {
            if (_on)
            {
                Camera.onPostRender -= _draw;
                CollisionViewer old = _session == null ? null : _session.CollisionViewer;
                if (old != null) old.On = _viewerWasOn;
                Array.Clear(_peds, 0, MaxLabels);
                Array.Clear(_cars, 0, MaxLabels);
                Array.Clear(_units, 0, _units.Length);
                Array.Clear(_unitCars, 0, _unitCars.Length);
                _pedCount = _carCount = _ghostCount = _unitCount = 0;
                _session = null;
                _on = false;
            }
            if (session == null) return;
            if (_material == null) _material = Rendering.ColoredMaterial.Translucent("GtaSkylines.DebugOverlay");
            _session = session;
            CollisionViewer viewer = session.CollisionViewer;
            _viewerWasOn = viewer != null && viewer.On;
            if (viewer != null) viewer.On = true;
            _perf.Clear();
            PhaseTotals t = session.Totals;
            _lastFrames = t.Frames;
            _lastSim = t.SimMs;
            _lastStream = t.StreamMs;
            _lastRender = t.RenderMs;
            _refreshIn = 0;
            Camera.onPostRender += _draw;
            _on = true;
        }

        // One sample per Update; the session's phase timings are cumulative, so a frame's share is the difference.
        private void Sample()
        {
            PhaseTotals t = _session.Totals;
            if (t.Frames == _lastFrames) return;
            _perf.Add(Time.unscaledDeltaTime * 1000.0, t.SimMs - _lastSim, t.StreamMs - _lastStream, t.RenderMs - _lastRender);
            _lastFrames = t.Frames;
            _lastSim = t.SimMs;
            _lastStream = t.StreamMs;
            _lastRender = t.RenderMs;
        }

        private void Refresh()
        {
            GameParts game = _session.Game;
            GameWorld w = game.World;
            Vec3 at = w.Player.Position;
            _pedCount = 0;
            for (int i = 0; i < w.Peds.Count && _pedCount < MaxLabels; i++)
            {
                Ped p = w.Peds[i];
                if (p.VehicleId != 0 || !Near(p.Position, at)) continue;
                _peds[_pedCount] = p;
                _pedText[_pedCount++] = DebugText.Ped(p);
            }
            _carCount = 0;
            for (int i = 0; i < w.Cars.Count && _carCount < MaxLabels; i++)
            {
                Car c = w.Cars[i];
                if (!Near(c.Position, at)) continue;
                _cars[_carCount] = c;
                _carText[_carCount++] = DebugText.Car(c);
            }
            _ghostCount = 0;
            DepartureTracker departures = _session.Departures;
            for (int i = 0; departures != null && i < departures.Ghosts.Count && _ghostCount < MaxLabels; i++)
            {
                DepartureGhost g = departures.Ghosts[i];
                if (!Near(g.Centre, at)) continue;
                _ghosts[_ghostCount] = g.Centre;
                _ghostText[_ghostCount++] = "GHOST v" + g.Vehicle + " parked " + g.Parked + " owner " + g.Owner
                    + (g.Elsewhere ? " (spawned elsewhere)" : " (waiting to spawn)");
            }
            string units = "";
            _unitCount = 0;
            for (int i = 0; i < game.Police.Units.Count; i++)
            {
                PoliceUnit u = game.Police.Units[i];
                if (!u.InUse) continue;
                _units[_unitCount] = u;
                _unitCars[_unitCount++] = u.Kind == UnitKind.Car ? w.FindCar(u.CarId) : null;
                units += "\n" + DebugText.Unit(u);
            }
            _panel.text = "DEBUG  Ctrl+Shift+F10 (collision Ctrl+Shift+F9)\n"
                + DebugText.Fps(_perf) + "\n"
                + DebugText.Budget("session tick", _perf.SimMsAvg, PerfBudget.SimMs) + "\n"
                + DebugText.Budget("streaming", _perf.StreamMsAvg, PerfBudget.StreamMs) + "\n"
                + DebugText.Budget("render", _perf.RenderMsAvg, 0) + "\n\n"
                + DebugText.Wanted(w.Wanted) + "\n\n"
                + "Police units " + _unitCount + units + "\n\n"
                + DebugText.Ped(w.Player);
        }

        private static bool Near(Vec3 p, Vec3 at)
        {
            float dx = p.X - at.X, dz = p.Z - at.Z;
            return dx * dx + dz * dz <= LabelRange * LabelRange;
        }

        private void Label(Camera cam, Vec3 world, float lift, string text)
        {
            Vector3 s = cam.WorldToScreenPoint(new Vector3(world.X, world.Y + lift, world.Z));
            if (s.z <= 0f) return;
            GUI.Label(new Rect(s.x - 100f, Screen.height - s.y - 8f, 300f, 20f), text, _style);
        }

        private void Draw(Camera cam)
        {
            try
            {
                if (!_on || cam != Camera.main || _material == null || _session == null) return;
                GameParts game = _session.Game;
                WantedStatus wanted = game.World.Wanted;
                _material.SetInt("_ZTest", (int)CompareFunction.Always);
                _material.SetPass(0);
                GL.PushMatrix();
                GL.MultMatrix(Matrix4x4.identity);
                GL.Begin(GL.LINES);
                if (wanted.Stars > 0 && wanted.SearchRadius > 0) Circle(wanted.SearchCentre, wanted.SearchRadius, new Color(0.3f, 0.6f, 1f));
                if (wanted.HasLastKnownPosition) Cross(wanted.LastKnownPosition, 3f, Color.yellow);
                PoliceHelicopter heli = game.Police.Helicopter;
                for (int i = 0; i < _unitCount; i++)
                {
                    PoliceUnit u = _units[i];
                    int role = (int)u.Role;
                    GL.Color(RoleColour(role));
                    bool hasFrom = u.Kind == UnitKind.Helicopter ? heli != null && heli.Active : _unitCars[i] != null;
                    if (hasFrom)
                    {
                        Vec3 from = u.Kind == UnitKind.Helicopter ? heli.Position : _unitCars[i].Position;
                        GL.Vertex3(from.X, from.Y + 2f, from.Z);
                        GL.Vertex3(u.Target.X, u.Target.Y + 0.5f, u.Target.Z);
                    }
                    Cross(u.Target, 1.5f, RoleColour(role));
                }
                GL.End();
                GL.PopMatrix();
            }
            catch (Exception e)
            {
                _log.Error("debug overlay draw", e);
                SetOn(null);
            }
        }

        private static Color RoleColour(int role) { return role >= 0 && role < RoleColours.Length ? RoleColours[role] : Color.white; }

        private static void Cross(Vec3 c, float r, Color colour)
        {
            GL.Color(colour);
            GL.Vertex3(c.X - r, c.Y + 0.5f, c.Z);
            GL.Vertex3(c.X + r, c.Y + 0.5f, c.Z);
            GL.Vertex3(c.X, c.Y + 0.5f, c.Z - r);
            GL.Vertex3(c.X, c.Y + 0.5f, c.Z + r);
            GL.Vertex3(c.X, c.Y, c.Z);
            GL.Vertex3(c.X, c.Y + 2f * r, c.Z);
        }

        private static void Circle(Vec3 c, float r, Color colour)
        {
            GL.Color(colour);
            for (int i = 0; i < CircleSegments; i++)
            {
                float a0 = i * Mathf.PI * 2f / CircleSegments, a1 = (i + 1) * Mathf.PI * 2f / CircleSegments;
                GL.Vertex3(c.X + r * Mathf.Cos(a0), c.Y + 1f, c.Z + r * Mathf.Sin(a0));
                GL.Vertex3(c.X + r * Mathf.Cos(a1), c.Y + 1f, c.Z + r * Mathf.Sin(a1));
            }
        }
    }
}
