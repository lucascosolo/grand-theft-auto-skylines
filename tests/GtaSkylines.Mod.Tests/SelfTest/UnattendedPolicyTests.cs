using GtaSkylines.Mod.SelfTest;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class UnattendedPolicyTests
    {
        [Fact]
        public void Constants_match_the_contract()
        {
            Assert.Equal(3, UnattendedPolicy.QuitDelaySeconds);
            Assert.Equal(1500, UnattendedPolicy.SafetySeconds);
            Assert.Equal(1200, UnattendedPolicy.LoadTimeoutSeconds);
        }

        [Fact]
        public void Fresh_policy_does_nothing_and_has_no_reason()
        {
            var p = new UnattendedPolicy(true);
            Assert.Equal("", p.QuitReason);
            Assert.Equal(UnattendedAction.None, p.Tick(0));
            Assert.Equal(UnattendedAction.None, p.Tick(100000));
        }

        [Fact]
        public void ReportWritten_quits_after_the_delay()
        {
            var p = new UnattendedPolicy(true);
            p.ReportWritten(100);
            Assert.NotEqual("", p.QuitReason);
            Assert.Equal(UnattendedAction.None, p.Tick(102.9));
            Assert.Equal(UnattendedAction.Quit, p.Tick(103));
        }

        [Fact]
        public void Quit_is_returned_exactly_once()
        {
            var p = new UnattendedPolicy(true);
            p.ReportWritten(0);
            Assert.Equal(UnattendedAction.Quit, p.Tick(10));
            Assert.Equal(UnattendedAction.None, p.Tick(11));
            Assert.Equal(UnattendedAction.None, p.Tick(5000));
        }

        [Fact]
        public void First_schedule_wins()
        {
            var p = new UnattendedPolicy(true);
            p.ReportWritten(100);
            string reason = p.QuitReason;
            p.ReportWritten(500);
            p.AutoloadNotFound(600);
            Assert.Equal(reason, p.QuitReason);
            Assert.Equal(UnattendedAction.Quit, p.Tick(103));
        }

        [Fact]
        public void AutoloadNotFound_schedules_quit_with_autoload_reason()
        {
            var p = new UnattendedPolicy(true);
            p.AutoloadNotFound(10);
            Assert.Contains("autoload", p.QuitReason);
            Assert.Equal(UnattendedAction.None, p.Tick(12));
            Assert.Equal(UnattendedAction.Quit, p.Tick(13));
        }

        [Fact]
        public void Disabled_policy_never_schedules_or_acts()
        {
            var p = new UnattendedPolicy(false);
            p.ReportWritten(0);
            p.AutoloadNotFound(0);
            p.AutoloadIssued(0);
            Assert.Equal("", p.QuitReason);
            Assert.Equal(UnattendedAction.None, p.Tick(1));
            Assert.Equal(UnattendedAction.None, p.Tick(100000));
        }

        [Fact]
        public void Load_timeout_schedules_quit_but_returns_none_first()
        {
            var p = new UnattendedPolicy(true);
            p.AutoloadIssued(100);
            Assert.Equal(UnattendedAction.None, p.Tick(100 + UnattendedPolicy.LoadTimeoutSeconds - 1));
            Assert.Equal("", p.QuitReason);
            Assert.Equal(UnattendedAction.None, p.Tick(100 + UnattendedPolicy.LoadTimeoutSeconds));
            Assert.Contains("did not finish loading", p.QuitReason);
        }

        [Fact]
        public void Load_timeout_quit_follows_after_the_delay()
        {
            var p = new UnattendedPolicy(true);
            p.AutoloadIssued(0);
            double t = UnattendedPolicy.LoadTimeoutSeconds;
            p.Tick(t);
            Assert.Equal(UnattendedAction.None, p.Tick(t + 1));
            Assert.Equal(UnattendedAction.Quit, p.Tick(t + UnattendedPolicy.QuitDelaySeconds));
        }

        [Fact]
        public void AutoloadIssued_first_call_wins()
        {
            var p = new UnattendedPolicy(true);
            p.AutoloadIssued(0);
            p.AutoloadIssued(1000);
            p.Tick(1200);
            Assert.Contains("did not finish loading", p.QuitReason);
        }

        [Fact]
        public void Level_loaded_in_time_prevents_the_load_timeout()
        {
            var p = new UnattendedPolicy(true);
            p.AutoloadIssued(0);
            p.AutoloadLevelLoaded(60);
            Assert.Equal(UnattendedAction.None, p.Tick(1300));
            Assert.DoesNotContain("did not finish loading", p.QuitReason);
        }

        [Fact]
        public void Safety_timeout_after_level_loaded_aborts_selftest_then_quits()
        {
            var p = new UnattendedPolicy(true);
            p.AutoloadIssued(0);
            p.AutoloadLevelLoaded(100);
            Assert.Equal(UnattendedAction.None, p.Tick(100 + UnattendedPolicy.SafetySeconds - 1));
            Assert.Equal(UnattendedAction.AbortSelfTest, p.Tick(100 + UnattendedPolicy.SafetySeconds));
            Assert.Contains("safety timeout", p.QuitReason);
            Assert.Equal(UnattendedAction.None, p.Tick(100 + UnattendedPolicy.SafetySeconds + 1));
            Assert.Equal(UnattendedAction.Quit, p.Tick(100 + UnattendedPolicy.SafetySeconds + UnattendedPolicy.QuitDelaySeconds));
        }

        [Fact]
        public void AutoloadLevelLoaded_first_call_wins()
        {
            var p = new UnattendedPolicy(true);
            p.AutoloadLevelLoaded(0);
            p.AutoloadLevelLoaded(1000);
            Assert.Equal(UnattendedAction.AbortSelfTest, p.Tick(UnattendedPolicy.SafetySeconds));
        }

        [Fact]
        public void Report_after_load_quits_without_safety_abort()
        {
            var p = new UnattendedPolicy(true);
            p.AutoloadIssued(0);
            p.AutoloadLevelLoaded(50);
            p.ReportWritten(200);
            Assert.Equal(UnattendedAction.None, p.Tick(201));
            Assert.Equal(UnattendedAction.Quit, p.Tick(203));
        }

        [Fact]
        public void Without_autoload_the_safety_timer_does_not_run()
        {
            var p = new UnattendedPolicy(true);
            Assert.Equal(UnattendedAction.None, p.Tick(UnattendedPolicy.SafetySeconds + UnattendedPolicy.LoadTimeoutSeconds + 1));
            Assert.Equal("", p.QuitReason);
        }
    }
}
