using ColossalFramework;

namespace GtaSkylines.Mod.Audio
{
    /// <summary>
    /// Silences CS1's own world audio (ambient beds, vehicle/citizen/building effects, radio) while the player is indoors,
    /// through <c>AudioManager.MuteAll</c>, the knob CS1's own pause menu uses (PauseMenu.cs:266/281). The ambient group's
    /// volume is not usable: it is a <c>SavedFloat</c> persisted to the player's game settings and re-read every frame
    /// (AudioGroup.cs:450). The previous value is restored exactly on release. Main thread.
    /// </summary>
    internal sealed class AmbienceDucker
    {
        private bool _active;
        private bool _previous;

        /// <summary>True while ducking.</summary>
        public bool Active { get { return _active; } }

        /// <summary>Starts or ends ducking. Ending always clears the state, even when CS1's AudioManager is gone.</summary>
        public void Set(bool duck)
        {
            if (!duck)
            {
                Dispose();
                return;
            }
            if (_active || !Singleton<AudioManager>.exists) return;
            AudioManager am = Singleton<AudioManager>.instance;
            _previous = am.MuteAll;
            _active = true;
            am.MuteAll = true;
        }

        /// <summary>Ends ducking and restores the previous mute. Idempotent.</summary>
        public void Dispose()
        {
            if (!_active) return;
            _active = false;
            if (Singleton<AudioManager>.exists) Singleton<AudioManager>.instance.MuteAll = _previous;
        }

        /// <summary>Per frame: keeps the mute asserted if something else cleared it while ducked.</summary>
        public void Tick()
        {
            if (_active && Singleton<AudioManager>.exists && !Singleton<AudioManager>.instance.MuteAll)
                Singleton<AudioManager>.instance.MuteAll = true;
        }
    }
}
