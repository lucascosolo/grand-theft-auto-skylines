using GtaSkylines.Mod.Lifecycle;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class SimClockDebtTests
    {
        [Fact]
        public void FlushFrames_is_eight()
        {
            Assert.Equal(8, SimClockDebt.FlushFrames);
        }

        [Fact]
        public void Accrue_at_half_rate_owes_one_per_run_frame()
        {
            var d = new SimClockDebt();
            Assert.Equal(10, d.Accrue(10, 0.5f));
            Assert.Equal(10, d.Pending);
        }

        [Fact]
        public void Accrue_same_ran_twice_adds_nothing()
        {
            var d = new SimClockDebt();
            d.Accrue(10, 0.5f);
            Assert.Equal(10, d.Accrue(10, 0.5f));
        }

        [Fact]
        public void Accrue_adds_only_the_increase()
        {
            var d = new SimClockDebt();
            d.Accrue(10, 0.5f);
            Assert.Equal(14, d.Accrue(14, 0.5f));
        }

        [Fact]
        public void Accrue_at_three_quarter_rate_truncates()
        {
            var d = new SimClockDebt();
            Assert.Equal(1, d.Accrue(3, 0.75f));
        }

        [Fact]
        public void Accrue_never_subtracts()
        {
            var d = new SimClockDebt();
            d.Accrue(10, 0.5f);
            Assert.Equal(10, d.Accrue(2, 0.5f));
            Assert.Equal(10, d.Pending);
        }

        [Fact]
        public void New_instance_has_nothing_pending()
        {
            var d = new SimClockDebt();
            Assert.Equal(0, d.Pending);
            Assert.False(d.ShouldFlush);
        }

        [Fact]
        public void ShouldFlush_turns_true_at_flush_frames()
        {
            var d = new SimClockDebt();
            d.Accrue(7, 0.5f);
            Assert.False(d.ShouldFlush);
            d.Accrue(8, 0.5f);
            Assert.True(d.ShouldFlush);
        }

        [Fact]
        public void Take_returns_pending_and_zeroes_it()
        {
            var d = new SimClockDebt();
            d.Accrue(10, 0.5f);
            Assert.Equal(10, d.Take());
            Assert.Equal(0, d.Pending);
            Assert.False(d.ShouldFlush);
            Assert.Equal(0, d.Take());
        }

        [Fact]
        public void Take_does_not_reset_accounting()
        {
            var d = new SimClockDebt();
            d.Accrue(10, 0.5f);
            d.Take();
            Assert.Equal(0, d.Accrue(10, 0.5f));
            Assert.Equal(2, d.Accrue(12, 0.5f));
        }

        [Fact]
        public void Reset_keeps_pending_debt_and_restarts_accounting()
        {
            var d = new SimClockDebt();
            d.Accrue(10, 0.5f);
            d.Reset();
            Assert.Equal(10, d.Pending);
            Assert.Equal(14, d.Accrue(4, 0.5f));
        }
    }
}
