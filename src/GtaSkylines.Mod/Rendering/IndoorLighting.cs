using System;
using Skylines.Host;
using UnityEngine;
using UnityEngine.Rendering;

namespace GtaSkylines.Mod.Rendering
{
    /// <summary>
    /// Indoor lighting while the player is inside. <c>DayNightProperties.Update → Refresh</c> (DayNightProperties.cs:839-871)
    /// rewrites the sun rotation (UpdateSun :742), sun intensity/colour/enabled, moon and the trilight ambient colours
    /// (UpdateLighting :914) every frame, so <see cref="Apply"/> runs every LateUpdate (after it) while inside. The first
    /// Apply records every value it overrides (the reference's FrameOverride pattern); <see cref="Restore"/> writes them
    /// back once, including the fog image effects' <c>enabled</c> (FogEffect, DayNightFogEffect on the main camera) and
    /// the sun's shadow mode, which DayNight never rewrites. Idempotent; never throws. Main thread only.
    /// </summary>
    internal sealed class IndoorLighting
    {
        private static readonly Color IndoorAmbient = new Color(0.42f, 0.41f, 0.40f, 1f);
        private static readonly Color IndoorSun = new Color(1f, 0.96f, 0.9f, 1f);

        private readonly HostLog _log;
        private bool _saved;
        private Light _sun, _moon;
        private Quaternion _sunRotation;
        private float _sunIntensity;
        private Color _sunColour;
        private bool _sunEnabled, _moonEnabled;
        private LightShadows _sunShadows;
        private AmbientMode _ambientMode;
        private Color _sky, _equator, _ground;
        private Behaviour _fog, _dayNightFog;
        private bool _fogEnabled, _dayNightFogEnabled;

        public IndoorLighting(HostLog log)
        {
            _log = log;
        }

        public bool Active { get { return _saved; } }

        public void Apply()
        {
            if (!_saved) Save();
            if (_sun != null)
            {
                _sun.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                _sun.intensity = 1.0f;
                _sun.color = IndoorSun;
                _sun.shadows = LightShadows.None;
                _sun.enabled = true;
            }
            if (_moon != null) _moon.enabled = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = IndoorAmbient;
            RenderSettings.ambientEquatorColor = IndoorAmbient;
            RenderSettings.ambientGroundColor = IndoorAmbient * 0.8f;
            if (_fog != null) _fog.enabled = false;
            if (_dayNightFog != null) _dayNightFog.enabled = false;
        }

        public void Restore()
        {
            if (!_saved) return;
            _saved = false;
            Try("sun", () =>
            {
                if (_sun == null) return;
                _sun.transform.rotation = _sunRotation;
                _sun.intensity = _sunIntensity;
                _sun.color = _sunColour;
                _sun.shadows = _sunShadows;
                _sun.enabled = _sunEnabled;
            });
            Try("moon", () => { if (_moon != null) _moon.enabled = _moonEnabled; });
            Try("ambient", () =>
            {
                RenderSettings.ambientMode = _ambientMode;
                RenderSettings.ambientSkyColor = _sky;
                RenderSettings.ambientEquatorColor = _equator;
                RenderSettings.ambientGroundColor = _ground;
            });
            Try("fog", () =>
            {
                if (_fog != null) _fog.enabled = _fogEnabled;
                if (_dayNightFog != null) _dayNightFog.enabled = _dayNightFogEnabled;
            });
            _sun = _moon = null;
            _fog = _dayNightFog = null;
            _log.Info("render: indoor lighting restored");
        }

        private void Save()
        {
            DayNightProperties dn = DayNightProperties.instance;
            _sun = dn != null ? dn.sunLightSource : null;
            _moon = dn != null ? dn.moonLightSource : null;
            if (_sun != null)
            {
                _sunRotation = _sun.transform.rotation;
                _sunIntensity = _sun.intensity;
                _sunColour = _sun.color;
                _sunShadows = _sun.shadows;
                _sunEnabled = _sun.enabled;
            }
            if (_moon != null) _moonEnabled = _moon.enabled;
            _ambientMode = RenderSettings.ambientMode;
            _sky = RenderSettings.ambientSkyColor;
            _equator = RenderSettings.ambientEquatorColor;
            _ground = RenderSettings.ambientGroundColor;
            Camera cam = Camera.main;
            _fog = cam != null ? cam.GetComponent<FogEffect>() : null;
            _dayNightFog = cam != null ? cam.GetComponent<DayNightFogEffect>() : null;
            if (_fog != null) _fogEnabled = _fog.enabled;
            if (_dayNightFog != null) _dayNightFogEnabled = _dayNightFog.enabled;
            _saved = true;
            _log.Info("render: indoor lighting on (sun " + (_sun != null) + ", moon " + (_moon != null) + ", FogEffect " + (_fog != null)
                + ", DayNightFogEffect " + (_dayNightFog != null) + ")");
        }

        private void Try(string what, Action a)
        {
            try { a(); }
            catch (Exception e) { _log.Error("render: restore " + what, e); }
        }
    }
}
