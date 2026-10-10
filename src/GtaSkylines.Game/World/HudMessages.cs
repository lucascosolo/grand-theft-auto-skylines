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
        /// <summary>Subtitle queue length.</summary>
        public const int SubtitleCapacity = 8;
        /// <summary>Notifications kept.</summary>
        public const int NotificationCapacity = 4;
        /// <summary>Seconds a notification stays.</summary>
        public const float NotificationSeconds = 6f;
        /// <summary>Default duration of WASTED and BUSTED.</summary>
        public const float DeathScreenSeconds = 4f;

        /// <summary>
        /// Shows help. Text may contain button tokens <c>{ButtonName}</c> (a <c>GameButton</c> name such as
        /// <c>{EnterExit}</c>) which the HUD replaces with <c>~Key~</c> using the primary binding.
        /// </summary>
        public void ShowHelp(string text, int priority, float seconds)
        {
            throw new NotImplementedException();
        }

        /// <summary>Removes the current help if its text is the same instance as <paramref name="text"/>.</summary>
        public void ClearHelp(string text)
        {
            throw new NotImplementedException();
        }

        /// <summary>Current help text (tokens not yet substituted), or null.</summary>
        public string HelpText
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>Priority of the current help (0 when none).</summary>
        public int HelpPriority
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>Queues a subtitle line.</summary>
        public void QueueSubtitle(string speaker, string text, float seconds)
        {
            throw new NotImplementedException();
        }

        /// <summary>True with the subtitle showing now.</summary>
        public bool TryGetSubtitle(out Subtitle current)
        {
            throw new NotImplementedException();
        }

        /// <summary>Lines waiting behind the current one.</summary>
        public int QueuedSubtitles
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>Posts a notification (age 0) at the top of the feed.</summary>
        public void Notify(string sender, string text, NotificationKind kind)
        {
            throw new NotImplementedException();
        }

        /// <summary>Notifications in the feed, 0..<see cref="NotificationCapacity"/>.</summary>
        public int NotificationCount
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>Notification <paramref name="index"/>, 0 = newest.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Index outside [0, NotificationCount).</exception>
        public Notification GetNotification(int index)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Requests a big screen for <paramref name="seconds"/>: <paramref name="text"/> is the failure reason for
        /// MissionFailed (else null) and <paramref name="reward"/> the cash for MissionPassed (else 0).
        /// </summary>
        public void ShowBigScreen(BigScreenKind kind, string text, long reward, float seconds)
        {
            throw new NotImplementedException();
        }

        /// <summary>Current big screen (None when none or expired).</summary>
        public BigScreenKind BigScreen
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>Text of the current big screen.</summary>
        public string BigScreenText
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>Reward of the current big screen.</summary>
        public long BigScreenReward
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>Progress of the current big screen, 0 at request .. 1 at its end.</summary>
        public float BigScreenProgress
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>Advances every timer by <paramref name="realDt"/> seconds (the HUD system calls it once per presentation step).</summary>
        public void Advance(float realDt)
        {
            throw new NotImplementedException();
        }

        /// <summary>Removes everything (help, subtitles, notifications, big screen).</summary>
        public void Clear()
        {
            throw new NotImplementedException();
        }
    }
}
