using System;
using System.Collections.Generic;
using System.Globalization;

// Unity-free part of the in-game self-test, compiled into GtaSkylines.Mod.Tests as well.
namespace GtaSkylines.Mod.SelfTest
{
    /// <summary>Outcome of one self-test step.</summary>
    public enum StepStatus
    {
        /// <summary>Passed.</summary>
        Pass,
        /// <summary>Failed (including timeouts).</summary>
        Fail,
        /// <summary>Not run: a prerequisite or city content was missing, or the run was aborted.</summary>
        Skip,
        /// <summary>The step threw or was aborted while running.</summary>
        Error,
    }

    /// <summary>Ordered key/value measurements; setting an existing key replaces its value in place.</summary>
    internal sealed class Measurements
    {
        private readonly List<KeyValuePair<string, object>> _items = new List<KeyValuePair<string, object>>();

        public Measurements Set(string key, object value)
        {
            int i = IndexOf(key);
            if (i >= 0) _items[i] = new KeyValuePair<string, object>(key, value);
            else _items.Add(new KeyValuePair<string, object>(key, value));
            return this;
        }

        public object Get(string key)
        {
            int i = IndexOf(key);
            return i >= 0 ? _items[i].Value : null;
        }

        public int Count { get { return _items.Count; } }

        public IEnumerable<KeyValuePair<string, object>> Items { get { return _items; } }

        public Measurements With(string key, object value) { return Set(key, value); }

        private int IndexOf(string key)
        {
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].Key == key) return i;
            return -1;
        }
    }

    /// <summary>Result of one step: the first verdict wins.</summary>
    internal sealed class StepRecord
    {
        public string Id = "", Name = "";
        public StepStatus? Status;
        public string Reason = "";
        public double StartedSeconds;
        public double DurationSeconds;
        public double TimeoutSeconds;
        public bool TimedOut;
        /// <summary>How far the step got; appended to a timeout reason.</summary>
        public string Progress;
        public readonly Measurements Values = new Measurements();
        public readonly List<string> Screenshots = new List<string>();

        public bool Finished { get { return Status != null; } }

        public void Pass(string reason) { Set(StepStatus.Pass, reason); }
        public void Fail(string reason) { Set(StepStatus.Fail, reason); }
        public void Skip(string reason) { Set(StepStatus.Skip, reason); }
        public void Error(string reason) { Set(StepStatus.Error, reason); }

        private void Set(StepStatus status, string reason)
        {
            if (Status != null) return;
            Status = status;
            Reason = reason ?? "";
        }
    }

    /// <summary>One scripted step: <c>begin</c> once, then <c>tick(record, secondsInStep)</c> per frame until it gives a verdict or times out.</summary>
    internal sealed class SelfTestStep
    {
        public SelfTestStep(string id, string name, double timeoutSeconds, Action<StepRecord> begin, Action<StepRecord, double> tick)
        {
            Id = id;
            Name = name;
            TimeoutSeconds = timeoutSeconds;
            Begin = begin;
            Tick = tick;
        }

        public string Id { get; private set; }
        public string Name { get; private set; }
        public double TimeoutSeconds { get; private set; }
        public Action<StepRecord> Begin { get; private set; }
        public Action<StepRecord, double> Tick { get; private set; }

        /// <summary>Ids of earlier steps that must have passed; otherwise this step is skipped.</summary>
        public string[] Requires = new string[0];

        public SelfTestStep Needs(params string[] ids)
        {
            Requires = ids ?? new string[0];
            return this;
        }
    }

    /// <summary>Runs steps in order, one at a time, with per-step timeouts, prerequisites and abort.</summary>
    internal sealed class StepRunner
    {
        private readonly IList<SelfTestStep> _steps;
        private readonly List<StepRecord> _records = new List<StepRecord>();
        private int _next;
        private int _current = -1;
        private double _runStart, _stepStart;
        private bool _started;

        public StepRunner(IList<SelfTestStep> steps)
        {
            _steps = steps;
            foreach (SelfTestStep s in steps)
                _records.Add(new StepRecord { Id = s.Id, Name = s.Name, TimeoutSeconds = s.TimeoutSeconds });
        }

        public IList<StepRecord> Records { get { return _records; } }

        public StepRecord Current { get { return _current >= 0 ? _records[_current] : null; } }

        public bool Done { get { return _current < 0 && _next >= _steps.Count; } }

        public bool Aborted { get; private set; }

        public string AbortReason { get; private set; }

        /// <summary>Called once per record as it finishes, in order.</summary>
        public Action<StepRecord> Finished;

        public void Tick(double now)
        {
            if (Done) return;
            if (!_started)
            {
                _started = true;
                _runStart = now;
            }
            if (_current >= 0)
            {
                StepRecord rec = _records[_current];
                double elapsed = now - _stepStart;
                Guarded(rec, () => { if (_steps[_current].Tick != null) _steps[_current].Tick(rec, elapsed); });
                if (!rec.Finished && elapsed >= rec.TimeoutSeconds)
                {
                    rec.Fail("timeout after " + rec.TimeoutSeconds.ToString("0.#", CultureInfo.InvariantCulture) + " s"
                        + (rec.Progress != null ? "; " + rec.Progress : ""));
                    rec.TimedOut = true;
                }
                if (rec.Finished) Complete(elapsed);
                return;
            }
            while (_next < _steps.Count)
            {
                int i = _next++;
                SelfTestStep step = _steps[i];
                StepRecord rec = _records[i];
                rec.StartedSeconds = now - _runStart;
                string missing = Missing(step);
                if (missing != null) rec.Skip("needs " + missing);
                else Guarded(rec, () => { if (step.Begin != null) step.Begin(rec); });
                _current = i;
                _stepStart = now;
                if (!rec.Finished) return;
                Complete(0);
            }
        }

        public void Abort(string reason, double now)
        {
            if (Done) return;
            Aborted = true;
            AbortReason = reason;
            if (_current >= 0)
            {
                _records[_current].Error("aborted: " + reason);
                Complete(now - _stepStart);
            }
            while (_next < _steps.Count)
            {
                _current = _next++;
                _records[_current].Skip("not run: aborted");
                Complete(0);
            }
        }

        private void Complete(double duration)
        {
            StepRecord rec = _records[_current];
            rec.DurationSeconds = duration;
            _current = -1;
            if (Finished != null) Finished(rec);
        }

        private string Missing(SelfTestStep step)
        {
            foreach (string id in step.Requires)
            {
                StepRecord r = null;
                foreach (StepRecord x in _records)
                    if (x.Id == id) { r = x; break; }
                if (r == null) return id + " (unknown step)";
                if (r.Status != StepStatus.Pass) return id + " (" + (r.Status == null ? "not run" : r.Status.ToString().ToLowerInvariant()) + ")";
            }
            return null;
        }

        private static void Guarded(StepRecord rec, Action a)
        {
            try { a(); }
            catch (Exception e) { rec.Error(e.GetType().Name + ": " + e.Message); }
        }
    }
}
