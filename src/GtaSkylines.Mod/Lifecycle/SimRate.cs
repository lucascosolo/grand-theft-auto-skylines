using System;
using ColossalFramework;
using Skylines.Host;
using UnityEngine;

namespace GtaSkylines.Mod.Lifecycle
{
    /// <summary>
    /// Slows the city simulation while playing (ported from the reference project). CS1 advances its simulation by
    /// Time.deltaTime / Time.fixedDeltaTime frames per frame (SimulationManager.Update), so stretching
    /// Time.fixedDeltaTime by 1 / rate slows what the simulation moves; <see cref="Tick"/> adds the withheld frames to
    /// SimulationManager's day-time and date offsets so the sun and calendar keep normal pace. <see cref="End"/>
    /// restores the fixed step exactly; idempotent, never throws. Main thread only.
    /// </summary>
    internal sealed class SimRate
    {
        private readonly HostLog _log;
        private float _saved;
        private bool _active;
        private float _rate;
        private uint _startFrame;
        private uint _added;

        public SimRate(HostLog log)
        {
            _log = log;
        }

        /// <summary>Applies <paramref name="rate"/> (0.1 .. &lt;1); 1x or anything else changes nothing.</summary>
        public void Begin(float rate)
        {
            if (_active || !(rate >= 0.1f && rate < 0.999f)) return;
            try
            {
                _saved = Time.fixedDeltaTime;
                Time.fixedDeltaTime = _saved / rate;
                _rate = rate;
                _startFrame = SimulationManager.exists ? Singleton<SimulationManager>.instance.m_referenceFrameIndex : 0u;
                _added = 0;
                _active = true;
                _log.Info("sim rate: " + rate.ToString("0.00") + "x (fixed step " + _saved.ToString("0.0000") + " -> " + Time.fixedDeltaTime.ToString("0.0000") + " s)");
            }
            catch (Exception e)
            {
                _log.Error("sim rate: begin", e);
            }
        }

        /// <summary>Per frame while active: keeps the sun and the date at normal pace.</summary>
        public void Tick()
        {
            if (!_active || !SimulationManager.exists) return;
            try
            {
                SimulationManager sim = Singleton<SimulationManager>.instance;
                uint ran = sim.m_referenceFrameIndex - _startFrame;
                uint owed = (uint)(ran / (double)_rate) - ran;
                if (owed <= _added) return;
                uint extra = owed - _added;
                _added = owed;
                sim.m_dayTimeOffsetFrames += extra;
                sim.m_timeOffsetTicks += extra * sim.m_timePerFrame.Ticks;
            }
            catch (Exception e)
            {
                _log.Error("sim rate: day clock", e);
                End();
            }
        }

        public void End()
        {
            if (!_active) return;
            _active = false;
            try
            {
                Time.fixedDeltaTime = _saved;
                _log.Info("sim rate: normal speed restored");
            }
            catch (Exception e)
            {
                _log.Error("sim rate: restore", e);
            }
        }
    }
}
