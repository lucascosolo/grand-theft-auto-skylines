using System;

namespace GtaSkylines.Mod.SelfTest
{
    /// <summary>
    /// Pairs CS1's counter-based autosave pause (<c>AutoSaveTimer.Pause</c>/<c>UnPause</c>, LoadingManager.cs:99-111; the counter
    /// survives level loads) so the self-test pauses it at most once and always undoes exactly what it did.
    /// </summary>
    internal sealed class AutosaveHold
    {
        private readonly Action _pause;
        private readonly Action _unpause;

        public AutosaveHold(Action pause, Action unpause)
        {
            if (pause == null) throw new ArgumentNullException("pause");
            if (unpause == null) throw new ArgumentNullException("unpause");
            _pause = pause;
            _unpause = unpause;
        }

        /// <summary>True while this hold has the autosave paused.</summary>
        public bool Held { get; private set; }

        /// <summary>Pauses once; false when already held.</summary>
        public bool Hold()
        {
            if (Held) return false;
            _pause();
            Held = true;
            return true;
        }

        /// <summary>Undoes <see cref="Hold"/>; false when not held.</summary>
        public bool Release()
        {
            if (!Held) return false;
            _unpause();
            Held = false;
            return true;
        }
    }
}
