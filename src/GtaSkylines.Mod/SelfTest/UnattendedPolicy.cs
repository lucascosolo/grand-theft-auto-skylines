// Unity-free timing for unattended runs (launch.cfg selftest_quit), compiled into GtaSkylines.Mod.Tests as well.
namespace GtaSkylines.Mod.SelfTest
{
    internal enum UnattendedAction { None, AbortSelfTest, Quit }

    /// <summary>
    /// When to quit the game in an unattended run (ported from the reference): 3 s after the self-test report or after an
    /// autoload target was not found; abort the self-test (which writes a partial report) <see cref="SafetySeconds"/> after
    /// the autoloaded level loaded without a report, then quit; quit if an autoload was issued but no level finished
    /// loading within <see cref="LoadTimeoutSeconds"/>. Does nothing when <c>selftest_quit</c> is off.
    /// </summary>
    internal sealed class UnattendedPolicy
    {
        public const double QuitDelaySeconds = 3;
        public const double SafetySeconds = 1500;
        public const double LoadTimeoutSeconds = 1200;

        private readonly bool _quitEnabled;
        private double? _quitDue;
        private double? _loadedAt;
        private double? _issuedAt;
        private bool _quitIssued;

        public UnattendedPolicy(bool quitEnabled)
        {
            _quitEnabled = quitEnabled;
            QuitReason = "";
        }

        public string QuitReason { get; private set; }

        public void ReportWritten(double now) { Schedule(now, "self-test report written"); }

        public void AutoloadNotFound(double now) { Schedule(now, "autoload target not found or failed"); }

        public void AutoloadIssued(double now)
        {
            if (_issuedAt == null) _issuedAt = now;
        }

        public void AutoloadLevelLoaded(double now)
        {
            if (_loadedAt == null) _loadedAt = now;
        }

        public UnattendedAction Tick(double now)
        {
            if (!_quitEnabled || _quitIssued) return UnattendedAction.None;
            if (_quitDue == null)
            {
                if (_loadedAt == null && _issuedAt != null && now - _issuedAt.Value >= LoadTimeoutSeconds)
                {
                    Schedule(now, "the autoloaded level did not finish loading within " + LoadTimeoutSeconds + " s");
                    return UnattendedAction.None;
                }
                if (_loadedAt == null || now - _loadedAt.Value < SafetySeconds) return UnattendedAction.None;
                Schedule(now, "safety timeout: no self-test report " + SafetySeconds + " s after the level load");
                return UnattendedAction.AbortSelfTest;
            }
            if (now < _quitDue.Value) return UnattendedAction.None;
            _quitIssued = true;
            return UnattendedAction.Quit;
        }

        private void Schedule(double now, string reason)
        {
            if (!_quitEnabled || _quitDue != null) return;
            _quitDue = now + QuitDelaySeconds;
            QuitReason = reason;
        }
    }
}
