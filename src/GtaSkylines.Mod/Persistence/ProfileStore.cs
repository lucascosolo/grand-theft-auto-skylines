using System;
using GtaSkylines.Game.Profile;

namespace GtaSkylines.Mod.Persistence
{
    /// <summary>
    /// The player profile record of the loaded city, between CS1's save data (loading and saving threads) and the play
    /// session (main thread). Nothing is written into a save until the player has entered play once in that city
    /// (DESIGN F-MODE-4); an unreadable record is kept byte for byte under <see cref="UnreadableKey"/>. Thread-safe.
    /// </summary>
    public sealed class ProfileStore
    {
        /// <summary>Save data key of the profile record (<see cref="ProfileRecord"/>).</summary>
        public const string Key = "GtaSkylines.Profile";
        /// <summary>Save data key under which an unreadable record is kept.</summary>
        public const string UnreadableKey = "GtaSkylines.Profile.unreadable";

        private readonly object _sync = new object();
        private readonly Action<string> _log;
        private byte[] _record;
        private byte[] _unreadable;
        private bool _enteredBefore;
        private bool _entered;

        /// <summary>A store that reports through <paramref name="log"/>.</summary>
        public ProfileStore(Action<string> log)
        {
            _log = log;
        }

        /// <summary>The latest valid record, or null (fresh profile).</summary>
        public byte[] Record { get { lock (_sync) return _record; } }

        /// <summary>Raw bytes kept because they could not be read, or null.</summary>
        public byte[] Unreadable { get { lock (_sync) return _unreadable; } }

        /// <summary>The save already held our data when it was loaded.</summary>
        public bool EnteredBefore { get { lock (_sync) return _enteredBefore; } }

        /// <summary>True once the city may carry our data: it did at load, or the player entered play.</summary>
        public bool MayWrite { get { lock (_sync) return _enteredBefore || _entered; } }

        /// <summary>Replaces the state with what a save held under <see cref="Key"/> and <see cref="UnreadableKey"/>.</summary>
        public void Load(byte[] record, byte[] keptUnreadable)
        {
            ProfileDecodeResult decoded = record == null ? null : ProfileRecord.Decode(record);
            lock (_sync)
            {
                _entered = false;
                _enteredBefore = record != null;
                _unreadable = keptUnreadable;
                _record = null;
                if (decoded == null) return;
                if (decoded.Ok)
                {
                    _record = record;
                    return;
                }
                _unreadable = record;
            }
            _log("profile record unreadable (" + decoded.Error + ", " + record.Length + " bytes): starting a fresh profile, raw bytes kept under "
                + UnreadableKey + (keptUnreadable != null ? "; an older kept unreadable record (" + keptUnreadable.Length + " bytes) is replaced" : ""));
        }

        /// <summary>The player entered play in this city: saves carry our data from now on.</summary>
        public void MarkEntered()
        {
            lock (_sync) _entered = true;
        }

        /// <summary>A newer record from the session; null is ignored.</summary>
        public void Update(byte[] record)
        {
            if (record == null) return;
            lock (_sync) _record = record;
        }

        /// <summary>Writes the record and any kept unreadable bytes, or nothing while <see cref="MayWrite"/> is false.</summary>
        public void Save(Action<string, byte[]> write)
        {
            byte[] record, unreadable;
            lock (_sync)
            {
                if (!(_enteredBefore || _entered)) return;
                record = _record;
                unreadable = _unreadable;
            }
            if (record != null) write(Key, record);
            if (unreadable != null) write(UnreadableKey, unreadable);
        }

        /// <summary>Forgets everything (city unloaded).</summary>
        public void Reset()
        {
            lock (_sync)
            {
                _record = null;
                _unreadable = null;
                _enteredBefore = false;
                _entered = false;
            }
        }
    }
}
