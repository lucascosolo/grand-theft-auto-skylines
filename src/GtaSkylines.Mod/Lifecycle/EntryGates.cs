using System;
using ColossalFramework;
using GtaSkylines.Mod.Persistence;
using Skylines.Host;
using Skylines.Host.Saves;

namespace GtaSkylines.Mod.Lifecycle
{
    /// <summary>
    /// Asked before entering (ported from the reference's PauseGate): on a paused city a dialog says the game will be
    /// unpaused. Yes unpauses and retries the entry; No cancels it. Main thread only.
    /// </summary>
    internal sealed class PauseGate
    {
        private readonly HostLog _log;
        private readonly Action _retry;
        private readonly Action _cancel;
        private bool _asking;
        private bool _disposed;

        public PauseGate(HostLog log, Action retry, Action cancel)
        {
            _log = log;
            _retry = retry;
            _cancel = cancel;
        }

        /// <summary>Null to proceed, else what is being waited for.</summary>
        public string Check()
        {
            if (!SimulationManager.exists || !Singleton<SimulationManager>.instance.SimulationPaused) return null;
            if (_asking) return "answer the unpause dialog";
            _asking = true;
            _log.Info("play mode: the city is paused; asking to unpause");
            GameDialogs.Confirm("GTA: Skylines",
                "The game is paused. Play mode runs with the city going, so the game will be unpaused. Continue?",
                yes =>
                {
                    _asking = false;
                    if (_disposed) return;
                    if (!yes)
                    {
                        _log.Info("play mode: stayed paused; not entering");
                        _cancel();
                        return;
                    }
                    Singleton<SimulationManager>.instance.SimulationPaused = false;
                    _log.Info("play mode: unpaused the city");
                    _retry();
                });
            return "answer the unpause dialog";
        }

        public void Dispose() { _disposed = true; }
    }

    /// <summary>
    /// First entry per city (DESIGN F-MODE-4): offers a backup save through the reference's verified routine
    /// (<see cref="CityBackup"/>). Yes writes and verifies the backup, then retries the entry; No enters without one; a
    /// failed backup cancels the entry with an error dialog. Not asked once the city carries our data. Main thread only.
    /// </summary>
    internal sealed class FirstEntryBackup
    {
        private const string Label = "before GTA";

        private enum Step { None, Asking, Saving, Done }

        private readonly HostLog _log;
        private readonly ProfileStore _store;
        private readonly Action _retry;
        private readonly Action _cancel;
        private readonly CityBackup _backup = new CityBackup();
        private Step _step;
        private bool _disposed;

        public FirstEntryBackup(HostLog log, ProfileStore store, Action retry, Action cancel)
        {
            _log = log;
            _store = store;
            _retry = retry;
            _cancel = cancel;
        }

        /// <summary>Null to proceed, else what is being waited for.</summary>
        public string Check()
        {
            if (_step == Step.Done || _store.MayWrite) return null;
            if (_step == Step.Asking) return "answer the backup dialog";
            if (_step == Step.Saving) return "writing the backup save";
            string name;
            try { name = CityBackup.NextName(CityState.Capture().CityName, Label); }
            catch (Exception e)
            {
                _log.Error("backup name", e);
                GameDialogs.Error("GTA: Skylines", "No name for a backup save could be found: " + e.Message + "\nThe city was not changed.");
                _cancel();
                return "no backup name";
            }
            _step = Step.Asking;
            _log.Info("play mode: first entry in this city; offering backup '" + name + "'");
            GameDialogs.Confirm("GTA: Skylines: first time in this city",
                "Save a backup of the city as it is now before you play?\n\n\"" + name + "\"\n\n"
                + "Yes saves the backup (it appears in the load menu), then you enter. No enters without a backup.",
                yes => Answered(yes, name));
            return "answer the backup dialog";
        }

        /// <summary>Per frame: follows a running backup.</summary>
        public void Update()
        {
            if (_step != Step.Saving) return;
            _backup.Update();
            if (_backup.IsRunning) return;
            if (_backup.State == CityBackup.Phase.Verified)
            {
                _step = Step.Done;
                _log.Info("play mode: backup '" + _backup.Name + "' " + _backup.Detail);
                _retry();
                return;
            }
            Failed(_backup.Detail);
        }

        public void Dispose() { _disposed = true; }

        private void Answered(bool yes, string name)
        {
            if (_disposed || _step != Step.Asking) return;
            if (!yes)
            {
                _step = Step.Done;
                _log.Info("play mode: backup declined; entering without one");
                _retry();
                return;
            }
            string error = _backup.Begin(name);
            if (error != null)
            {
                Failed(error);
                return;
            }
            _step = Step.Saving;
            _log.Info("play mode: writing backup save '" + name + "' to " + _backup.FilePath);
        }

        private void Failed(string why)
        {
            _step = Step.None;
            _log.Warn("play mode: backup failed: " + why);
            GameDialogs.Error("GTA: Skylines", "The backup save could not be written or verified:\n" + why + "\n\nThe city was not changed and play mode was not entered.");
            _cancel();
        }
    }
}
