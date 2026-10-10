using GtaSkylines.Mod.Rendering;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class RenderFaultBudgetTests
    {
        [Fact]
        public void Limits_are_five_and_ten()
        {
            Assert.Equal(5, RenderFaultBudget.LoggedFaults);
            Assert.Equal(10, RenderFaultBudget.ExitFaults);
        }

        [Fact]
        public void New_budget_is_empty()
        {
            var b = new RenderFaultBudget();
            Assert.Equal(0, b.Count);
            Assert.False(b.Exhausted);
        }

        [Fact]
        public void Record_logs_first_five_only()
        {
            var b = new RenderFaultBudget();
            for (int i = 1; i <= 5; i++) Assert.True(b.Record());
            Assert.False(b.Record());
            Assert.False(b.Record());
            Assert.Equal(7, b.Count);
        }

        [Fact]
        public void Exhausted_at_ten_not_nine()
        {
            var b = new RenderFaultBudget();
            for (int i = 0; i < 9; i++) b.Record();
            Assert.False(b.Exhausted);
            b.Record();
            Assert.True(b.Exhausted);
        }

        [Fact]
        public void Reset_clears_count_and_resumes_logging()
        {
            var b = new RenderFaultBudget();
            for (int i = 0; i < 10; i++) b.Record();
            b.Reset();
            Assert.Equal(0, b.Count);
            Assert.False(b.Exhausted);
            Assert.True(b.Record());
        }
    }
}
