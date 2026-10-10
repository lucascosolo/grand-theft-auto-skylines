using System;

namespace GtaSkylines.Mod.Debug
{
    /// <summary>Per-frame budgets from docs/contracts/M7-wiring.md (render has none); the frame budget is 30 fps.</summary>
    public static class PerfBudget
    {
        /// <summary>One frame at 30 fps.</summary>
        public const double FrameMs = 1000.0 / 30;
        /// <summary>Game simulation, average.</summary>
        public const double SimMs = 4;
        /// <summary>City tick and collision streaming, average.</summary>
        public const double StreamMs = 2;
    }

    /// <summary>Rolling window of the last N frames' timings for the debug overlay. Allocates only in the constructor.</summary>
    public sealed class PerfWindow
    {
        private readonly double[] _frame, _sim, _stream, _render;
        private int _next;

        /// <param name="capacity">Frames kept (at least 1).</param>
        public PerfWindow(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException("capacity");
            _frame = new double[capacity];
            _sim = new double[capacity];
            _stream = new double[capacity];
            _render = new double[capacity];
        }

        /// <summary>Samples retained.</summary>
        public int Count { get; private set; }

        /// <summary>Average frame time, ms (0 when empty).</summary>
        public double FrameMsAvg { get { return Avg(_frame); } }

        /// <summary>Slowest frame in the window, ms.</summary>
        public double FrameMsMax
        {
            get
            {
                double max = 0;
                for (int i = 0; i < Count; i++) if (_frame[i] > max) max = _frame[i];
                return max;
            }
        }

        /// <summary>Average simulation tick, ms.</summary>
        public double SimMsAvg { get { return Avg(_sim); } }

        /// <summary>Average streaming tick, ms.</summary>
        public double StreamMsAvg { get { return Avg(_stream); } }

        /// <summary>Average render build, ms.</summary>
        public double RenderMsAvg { get { return Avg(_render); } }

        /// <summary>Frames per second from the average frame time (0 when empty).</summary>
        public double Fps
        {
            get
            {
                double avg = FrameMsAvg;
                return avg > 0 ? 1000 / avg : 0;
            }
        }

        /// <summary>Adds one frame, dropping the oldest when full.</summary>
        public void Add(double frameMs, double simMs, double streamMs, double renderMs)
        {
            _frame[_next] = frameMs;
            _sim[_next] = simMs;
            _stream[_next] = streamMs;
            _render[_next] = renderMs;
            _next = (_next + 1) % _frame.Length;
            if (Count < _frame.Length) Count++;
        }

        /// <summary>Empties the window.</summary>
        public void Clear()
        {
            Count = 0;
            _next = 0;
        }

        private double Avg(double[] values)
        {
            if (Count == 0) return 0;
            double sum = 0;
            for (int i = 0; i < Count; i++) sum += values[i];
            return sum / Count;
        }
    }
}
