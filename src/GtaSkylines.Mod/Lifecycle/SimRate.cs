using System;
using System.Threading;
using ColossalFramework;
using Skylines.Host;
using UnityEngine;

namespace GtaSkylines.Mod.Lifecycle
{
    /// <summary>
    /// Slows the city simulation while playing (ported from the reference project). CS1 advances its simulation by
    /// Time.deltaTime / Time.fixedDeltaTime frames per frame (SimulationManager.Update), so stretching
    /// Time.fixedDeltaTime by 1 / rate slows what the simulation moves; <see cref="Tick"/> accrues the withheld frames
    /// and, once <see cref="SimClockDebt.FlushFrames"/> are owed, queues one cached action through
    /// <c>SimulationManager.AddAction</c> that adds them to the day-time and date offsets on the simulation thread
    /// (where CS1 writes them itself, SimulationManager.cs:923-928), so the sun and calendar keep normal pace.
    /// <see cref="End"/> restores the fixed step exactly and flushes what is still owed; idempotent, never throws.
    /// Main thread, except the queued action.
    /// </summary>
    internal sealed class SimRate
    {
        private readonly HostLog _log;
        private readonly SimClockDebt _debt = new SimClockDebt();
        private readonly Action _applyDebt;
        private float _saved;
        private bool _active;
        private float _rate;
        private uint _startFrame;
        private int _queued;

        public SimRate(HostLog log)
        {
            _log = log;
            _applyDebt = ApplyDebt;
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
                _debt.Reset();
                _active = true;
                _log.Info("sim rate: " + rate.ToString("0.00") + "x (fixed step " + _saved.ToString("0.0000") + " -> " + Time.fixedDeltaTime.ToString("0.0000") + " s)");
            }
            catch (Exception e)
            {
                _log.Error("sim rate: begin", e);
            }
        }

        /// <summary>Per frame while active: keeps the sun and the date at normal pace. Allocates only when it queues.</summary>
        public void Tick()
        {
            if (!_active || !SimulationManager.exists) return;
            try
            {
                _debt.Accrue(Singleton<SimulationManager>.instance.m_referenceFrameIndex - _startFrame, _rate);
                if (_debt.ShouldFlush) Queue();
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
                if (_debt.Pending > 0) Queue();
                _log.Info("sim rate: normal speed restored");
            }
            catch (Exception e)
            {
                _log.Error("sim rate: restore", e);
            }
        }

        // One outstanding action at a time; AddAction allocates CS1's AsyncAction wrapper (SimulationManager.cs:725-741),
        // so at most one per FlushFrames owed frames.
        private void Queue()
        {
            if (!SimulationManager.exists || Interlocked.CompareExchange(ref _queued, 1, 0) != 0) return;
            Singleton<SimulationManager>.instance.AddAction(_applyDebt);
        }

        // Simulation thread.
        private void ApplyDebt()
        {
            Interlocked.Exchange(ref _queued, 0);
            try
            {
                int frames = _debt.Take();
                if (frames <= 0) return;
                SimulationManager sim = Singleton<SimulationManager>.instance;
                sim.m_dayTimeOffsetFrames += (uint)frames;
                sim.m_timeOffsetTicks += frames * sim.m_timePerFrame.Ticks;
            }
            catch (Exception e)
            {
                _log.Error("sim rate: day clock (simulation thread)", e);
            }
        }
    }
}
