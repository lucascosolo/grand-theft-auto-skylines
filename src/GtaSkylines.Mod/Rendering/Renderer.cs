using System;
using Skylines.Host;
using Skylines.Host.Camera;
using UnityEngine;

namespace GtaSkylines.Mod.Rendering
{
    /// <summary>
    /// Draws the Game's world in CS1. Per frame: <see cref="Begin"/> (Update phase) returns the cleared
    /// <see cref="RenderFrame"/> to fill; <see cref="Draw"/> (Update phase, after filling) takes effect events and arms
    /// the render hook; CS1's <c>RenderManager.LateUpdate</c> then calls <see cref="RenderHook"/>, which draws peds, cars,
    /// interior, markers, effects and the view model with this frame's <c>CameraInfo</c>; <see cref="End"/> (LateUpdate,
    /// after the session's LateTick) applies the camera and the indoor lighting override. Lifecycle:
    /// <see cref="OnPlayBegin"/>, <see cref="OnPlayEnd"/> and <see cref="OnLevelUnloading"/> release every mesh, material
    /// and texture created here and restore every CS1 setting overridden; prefab instances are released by CS1 once we
    /// stop obtaining them. Main thread only; every entry point is exception-guarded.
    /// </summary>
    public sealed class Renderer
    {
        private readonly HostLog _log;
        private readonly MaterialKit _kit;
        private readonly ActorRenderer _actors;
        private readonly InteriorRenderer _interior;
        private readonly EffectRenderer _effects;
        private readonly IndoorLighting _lighting;
        private readonly RenderFrame _frame = new RenderFrame();
        private bool _playing;
        private bool _armed;
        private int _faults;

        /// <summary>Creates an idle renderer; <paramref name="citizen"/> and <paramref name="vehicle"/> resolve model ids to CS1 prefabs.</summary>
        public Renderer(HostLog log, Func<int, CitizenInfo> citizen, Func<int, VehicleInfo> vehicle)
        {
            _log = log;
            _kit = new MaterialKit(log);
            _actors = new ActorRenderer(citizen, vehicle);
            _interior = new InteriorRenderer(log, _kit);
            _effects = new EffectRenderer(_kit);
            _lighting = new IndoorLighting(log);
        }

        /// <summary>The frame being filled.</summary>
        public RenderFrame Frame { get { return _frame; } }

        /// <summary>True while the indoor lighting override is applied.</summary>
        public bool IndoorLightingActive { get { return _lighting.Active; } }

        /// <summary>Play mode started: registers the render hook.</summary>
        public void OnPlayBegin()
        {
            Guard("begin play", () =>
            {
                RenderHook.Install();
                RenderHook.Target = this;
                _playing = true;
                _faults = 0;
            });
        }

        /// <summary>Play mode ended: stops drawing, restores lighting, destroys everything created. Idempotent.</summary>
        public void OnPlayEnd()
        {
            _playing = false;
            _armed = false;
            if (RenderHook.Target == this) RenderHook.Target = null;
            Guard("restore lighting", _lighting.Restore);
            Guard("release interior", _interior.Release);
            Guard("release effects", _effects.Release);
            Guard("release objects", _kit.ReleaseAll);
            Guard("reset actors", _actors.Reset);
            _frame.Clear();
        }

        /// <summary>The level is unloading: same as <see cref="OnPlayEnd"/>.</summary>
        public void OnLevelUnloading()
        {
            OnPlayEnd();
        }

        /// <summary>Clears and returns the frame to fill (Update phase).</summary>
        public RenderFrame Begin()
        {
            _armed = false;
            _frame.Clear();
            return _frame;
        }

        /// <summary>Takes the filled frame's events and arms the hook for this frame's CS1 rendering (Update phase).</summary>
        public void Draw()
        {
            if (!_playing) return;
            try { _effects.Ingest(_frame); }
            catch (Exception e) { Fault("ingest", e); }
            _armed = true;
        }

        /// <summary>Applies the camera through <paramref name="camera"/> and the indoor lighting (LateUpdate, after LateTick).</summary>
        public void End(CameraTakeover camera)
        {
            if (!_playing) return;
            try
            {
                if (_frame.HasCamera && camera != null)
                {
                    CameraDraw c = _frame.Camera;
                    camera.Drive(MaterialKit.V(c.Position), MaterialKit.Q(c.Rotation), c.FovDeg, c.Near);
                }
                if (_frame.Interior.Layout != null) _lighting.Apply();
                else _lighting.Restore();
            }
            catch (Exception e) { Fault("end", e); }
        }

        internal void RenderFromHook(RenderManager.CameraInfo cam)
        {
            if (!_playing || !_armed) return;
            _actors.DrawPeds(_frame);
            _actors.DrawCars(cam, _frame);
            _interior.Draw(_frame.Interior);
            Camera main = cam.m_camera != null ? cam.m_camera : Camera.main;
            _effects.Draw(_frame, main);
            _effects.DrawViewModel(_frame.ViewModel, main);
        }

        internal void Fault(string what, Exception e)
        {
            if (_faults++ < 5) _log.Error("render: " + what, e);
        }

        private void Guard(string what, Action a)
        {
            try { a(); }
            catch (Exception e) { Fault(what, e); }
        }
    }
}
