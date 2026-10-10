using System;
using Skylines.Host;
using UnityEngine;

namespace GtaSkylines.Mod.Lifecycle
{
    /// <summary>
    /// Renderer state CameraController normally sets each frame and does not while disabled (ported from the reference):
    /// RenderManager.CameraHeight (vehicle and citizen render distance) and QualitySettings.shadowDistance. Recorded at
    /// <see cref="Begin"/>, set each LateUpdate, restored by <see cref="End"/> (idempotent, never throws). Main thread.
    /// </summary>
    internal sealed class RenderOverrides
    {
        private const float CameraHeight = 300f;
        private const float ShadowDistance = 250f;

        private readonly HostLog _log;
        private bool _active;
        private float _cameraHeight;
        private float _shadowDistance;

        public RenderOverrides(HostLog log)
        {
            _log = log;
        }

        public void Begin()
        {
            if (_active) return;
            _cameraHeight = RenderManager.exists ? RenderManager.instance.CameraHeight : 0f;
            _shadowDistance = QualitySettings.shadowDistance;
            _active = true;
        }

        public void Apply()
        {
            if (!_active) return;
            if (RenderManager.exists) RenderManager.instance.CameraHeight = CameraHeight;
            QualitySettings.shadowDistance = ShadowDistance;
        }

        public void End()
        {
            if (!_active) return;
            _active = false;
            try { if (RenderManager.exists) RenderManager.instance.CameraHeight = _cameraHeight; }
            catch (Exception e) { _log.Error("render: restore CameraHeight", e); }
            try { QualitySettings.shadowDistance = _shadowDistance; }
            catch (Exception e) { _log.Error("render: restore shadowDistance", e); }
        }
    }
}
