using System;
using GtaSkylines.Mod.Debug;
using Xunit;

namespace GtaSkylines.Mod.Tests.DebugTests
{
    public class PerfWindowTests
    {
        [Fact]
        public void Budgets_match_the_contract()
        {
            Assert.Equal(1000.0 / 30, PerfBudget.FrameMs, 9);
            Assert.Equal(4.0, PerfBudget.SimMs);
            Assert.Equal(2.0, PerfBudget.StreamMs);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-3)]
        public void Capacity_below_one_throws(int capacity)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PerfWindow(capacity));
        }

        [Fact]
        public void Empty_window_is_all_zero()
        {
            var w = new PerfWindow(5);
            Assert.Equal(0, w.Count);
            Assert.Equal(0.0, w.FrameMsAvg);
            Assert.Equal(0.0, w.FrameMsMax);
            Assert.Equal(0.0, w.SimMsAvg);
            Assert.Equal(0.0, w.StreamMsAvg);
            Assert.Equal(0.0, w.RenderMsAvg);
            Assert.Equal(0.0, w.Fps);
        }

        [Fact]
        public void Averages_max_and_fps_over_samples()
        {
            var w = new PerfWindow(5);
            w.Add(10, 1, 2, 3);
            w.Add(30, 3, 4, 5);
            Assert.Equal(2, w.Count);
            Assert.Equal(20.0, w.FrameMsAvg, 9);
            Assert.Equal(30.0, w.FrameMsMax, 9);
            Assert.Equal(2.0, w.SimMsAvg, 9);
            Assert.Equal(3.0, w.StreamMsAvg, 9);
            Assert.Equal(4.0, w.RenderMsAvg, 9);
            Assert.Equal(50.0, w.Fps, 9);
        }

        [Fact]
        public void Ring_drops_oldest_samples()
        {
            var w = new PerfWindow(2);
            w.Add(100, 10, 10, 10);
            w.Add(10, 1, 1, 1);
            w.Add(20, 3, 3, 3);
            Assert.Equal(2, w.Count);
            Assert.Equal(15.0, w.FrameMsAvg, 9);
            Assert.Equal(20.0, w.FrameMsMax, 9);
            Assert.Equal(2.0, w.SimMsAvg, 9);
        }

        [Fact]
        public void Capacity_one_keeps_only_latest()
        {
            var w = new PerfWindow(1);
            w.Add(5, 5, 5, 5);
            w.Add(8, 2, 3, 4);
            Assert.Equal(1, w.Count);
            Assert.Equal(8.0, w.FrameMsAvg, 9);
            Assert.Equal(4.0, w.RenderMsAvg, 9);
        }

        [Fact]
        public void Clear_empties_and_window_is_reusable()
        {
            var w = new PerfWindow(3);
            w.Add(10, 1, 1, 1);
            w.Clear();
            Assert.Equal(0, w.Count);
            Assert.Equal(0.0, w.FrameMsAvg);
            Assert.Equal(0.0, w.FrameMsMax);
            Assert.Equal(0.0, w.Fps);
            w.Add(40, 1, 1, 1);
            Assert.Equal(1, w.Count);
            Assert.Equal(40.0, w.FrameMsAvg, 9);
        }
    }
}
