using System;

namespace GtaSkylines.Game.World
{
    /// <summary>Full-screen state shown over everything.</summary>
    public enum BigScreenKind
    {
        /// <summary>Nothing.</summary>
        None,
        /// <summary>WASTED.</summary>
        Wasted,
        /// <summary>BUSTED.</summary>
        Busted,
        /// <summary>MISSION PASSED with <see cref="HudMessages.BigScreenReward"/>.</summary>
        MissionPassed,
        /// <summary>MISSION FAILED with <see cref="HudMessages.BigScreenText"/> as the reason.</summary>
        MissionFailed,
    }

    /// <summary>Where a notification comes from (M4 picks the icon).</summary>
    public enum NotificationKind
    {
        /// <summary>A phone text.</summary>
        Text,
        /// <summary>A mission contact.</summary>
        Contact,
        /// <summary>A Chirper post.</summary>
        Chirper,
        /// <summary>A system message (saved, property bought).</summary>
        System,
    }

    /// <summary>A notification in the feed.</summary>
    public struct Notification
    {
        /// <summary>Sender name, or null.</summary>
        public string Sender;
        /// <summary>Body.</summary>
        public string Text;
        /// <summary>Kind.</summary>
        public NotificationKind Kind;
        /// <summary>Real seconds since it was posted.</summary>
        public float Age;
    }

    /// <summary>A subtitle line.</summary>
    public struct Subtitle
    {
        /// <summary>Speaker name (coloured by M4), or null.</summary>
        public string Speaker;
        /// <summary>Line.</summary>
        public string Text;
        /// <summary>Seconds it stays on screen.</summary>
        public float Seconds;
    }

    /// <summary>
    /// The message blackboard: help text, subtitles, notifications and big screens. Any system writes it; G6's HUD
    /// system ages it every presentation step (real time, also while paused) and copies the current state into the
    /// <c>HudModel</c>. Fixed-size storage: nothing allocates after construction. Timing rules:
    /// <list type="bullet">
    /// <item>Help: a single slot; <see cref="ShowHelp"/> replaces the current help when its
    /// priority is ≥ the current one's or the current one has expired. Help expires after its seconds; seconds ≤ 0
    /// means "this step only" (re-asserted each step by the caller, as G6 does for interaction prompts).</item>
    /// <item>Subtitles: FIFO queue of <see cref="SubtitleCapacity"/>; the head shows for its seconds, then the next;
    /// queuing into a full queue drops the oldest waiting line (never the one showing).</item>
    /// <item>Notifications: the newest <see cref="NotificationCapacity"/> are kept, newest first; each is removed at
    /// <see cref="NotificationSeconds"/> of age.</item>
    /// <item>Big screen: one at a time; a new request replaces the current one.</item>
    /// </list>
    /// </summary>
    public sealed class HudMessages
    {
        /// <summary>Subtitle queue length (the line showing plus those waiting).</summary>
        public const int SubtitleCapacity = 8;
        /// <summary>Notifications kept.</summary>
        public const int NotificationCapacity = 4;
        /// <summary>Seconds a notification stays.</summary>
        public const float NotificationSeconds = 6f;
        /// <summary>Default duration of WASTED and BUSTED.</summary>
        public const float DeathScreenSeconds = 4f;

        private string _help;
        private int _helpPriority;
        private float _helpLeft;
        private bool _helpOneStep;

        private readonly Subtitle[] _subs = new Subtitle[SubtitleCapacity];
        private int _subHead, _subCount;
        private float _subAge;

        private readonly Notification[] _notes = new Notification[NotificationCapacity];
        private int _noteCount;

        private BigScreenKind _big;
        private string _bigText;
        private long _bigReward;
        private float _bigSeconds, _bigAge;

        /// <summary>
        /// Shows help. Text may contain button tokens <c>{ButtonName}</c> (a <c>GameButton</c> name such as
        /// <c>{EnterExit}</c>) which the HUD replaces with <c>~Key~</c> using the primary binding.
        /// </summary>
        public void ShowHelp(string text, int priority, float seconds)
        {
            if (_help != null && priority < _helpPriority) return;
            _help = text;
            _helpPriority = text == null ? 0 : priority;
            _helpOneStep = seconds <= 0f;
            _helpLeft = seconds;
        }

        /// <summary>Removes the current help if its text is the same instance as <paramref name="text"/>.</summary>
        public void ClearHelp(string text)
        {
            if (text != null && ReferenceEquals(text, _help)) { _help = null; _helpPriority = 0; }
        }

        /// <summary>Current help text (tokens not yet substituted), or null.</summary>
        public string HelpText { get { return _help; } }

        /// <summary>Priority of the current help (0 when none).</summary>
        public int HelpPriority { get { return _help == null ? 0 : _helpPriority; } }

        /// <summary>Queues a subtitle line.</summary>
        public void QueueSubtitle(string speaker, string text, float seconds)
        {
            if (_subCount == SubtitleCapacity)
            {
                // Drop the oldest waiting line (index 1), keeping the one showing.
                for (int i = 1; i < _subCount - 1; i++) _subs[(_subHead + i) % SubtitleCapacity] = _subs[(_subHead + i + 1) % SubtitleCapacity];
                _subCount--;
            }
            if (_subCount == 0) _subAge = 0f;
            _subs[(_subHead + _subCount) % SubtitleCapacity] = new Subtitle { Speaker = speaker, Text = text, Seconds = seconds };
            _subCount++;
        }

        /// <summary>True with the subtitle showing now.</summary>
        public bool TryGetSubtitle(out Subtitle current)
        {
            if (_subCount == 0) { current = default(Subtitle); return false; }
            current = _subs[_subHead];
            return true;
        }

        /// <summary>Lines waiting behind the current one.</summary>
        public int QueuedSubtitles { get { return _subCount > 0 ? _subCount - 1 : 0; } }

        /// <summary>Posts a notification (age 0) at the top of the feed.</summary>
        public void Notify(string sender, string text, NotificationKind kind)
        {
            int n = Math.Min(_noteCount, NotificationCapacity - 1);
            for (int i = n; i > 0; i--) _notes[i] = _notes[i - 1];
            _notes[0] = new Notification { Sender = sender, Text = text, Kind = kind, Age = 0f };
            _noteCount = n + 1;
        }

        /// <summary>Notifications in the feed, 0..<see cref="NotificationCapacity"/>.</summary>
        public int NotificationCount { get { return _noteCount; } }

        /// <summary>Notification <paramref name="index"/>, 0 = newest.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Index outside [0, NotificationCount).</exception>
        public Notification GetNotification(int index)
        {
            if (index < 0 || index >= _noteCount) throw new ArgumentOutOfRangeException("index");
            return _notes[index];
        }

        /// <summary>
        /// Requests a big screen for <paramref name="seconds"/>: <paramref name="text"/> is the failure reason for
        /// MissionFailed (else null) and <paramref name="reward"/> the cash for MissionPassed (else 0).
        /// </summary>
        public void ShowBigScreen(BigScreenKind kind, string text, long reward, float seconds)
        {
            _big = kind;
            _bigText = text;
            _bigReward = reward;
            _bigSeconds = seconds;
            _bigAge = 0f;
        }

        /// <summary>Current big screen (None when none or expired).</summary>
        public BigScreenKind BigScreen { get { return _big; } }

        /// <summary>Text of the current big screen.</summary>
        public string BigScreenText { get { return _big == BigScreenKind.None ? null : _bigText; } }

        /// <summary>Reward of the current big screen.</summary>
        public long BigScreenReward { get { return _big == BigScreenKind.None ? 0 : _bigReward; } }

        /// <summary>Progress of the current big screen, 0 at request .. 1 at its end.</summary>
        public float BigScreenProgress
        {
            get { return _big == BigScreenKind.None || _bigSeconds <= 0f ? 0f : Math.Min(1f, _bigAge / _bigSeconds); }
        }

        /// <summary>Advances every timer by <paramref name="realDt"/> seconds (the HUD system calls it once per presentation step).</summary>
        public void Advance(float realDt)
        {
            if (_help != null)
            {
                _helpLeft -= realDt;
                if (_helpOneStep || _helpLeft <= 0f) { _help = null; _helpPriority = 0; }
            }
            if (_subCount > 0)
            {
                _subAge += realDt;
                while (_subCount > 0 && _subAge >= _subs[_subHead].Seconds)
                {
                    _subAge -= _subs[_subHead].Seconds;
                    _subs[_subHead] = default(Subtitle);
                    _subHead = (_subHead + 1) % SubtitleCapacity;
                    _subCount--;
                }
                if (_subCount == 0) _subAge = 0f;
            }
            for (int i = 0; i < _noteCount; i++) _notes[i].Age += realDt;
            while (_noteCount > 0 && _notes[_noteCount - 1].Age >= NotificationSeconds) _notes[--_noteCount] = default(Notification);
            if (_big != BigScreenKind.None)
            {
                _bigAge += realDt;
                if (_bigAge >= _bigSeconds) { _big = BigScreenKind.None; _bigText = null; _bigReward = 0; }
            }
        }

        /// <summary>Removes everything (help, subtitles, notifications, big screen).</summary>
        public void Clear()
        {
            _help = null;
            _helpPriority = 0;
            Array.Clear(_subs, 0, SubtitleCapacity);
            _subHead = _subCount = 0;
            _subAge = 0f;
            Array.Clear(_notes, 0, NotificationCapacity);
            _noteCount = 0;
            _big = BigScreenKind.None;
            _bigText = null;
            _bigReward = 0;
        }
    }
}
