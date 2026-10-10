using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.World;
using GtaSkylines.Mod.Audio;

namespace GtaSkylines.Mod.Play
{
    /// <summary>A one-shot sound a game event asks for.</summary>
    public enum AudioCue
    {
        /// <summary>Silent.</summary>
        None,
        /// <summary>Gun noise at the event position (weapon from the shooter).</summary>
        Gunshot,
        /// <summary>A bullet hitting something, at the shot's end point.</summary>
        BulletImpact,
        /// <summary>A fist hit.</summary>
        Punch,
        /// <summary>A bat hit.</summary>
        BatHit,
        /// <summary>A vehicle impact; loudness from the event value (m/s).</summary>
        Crash,
        /// <summary>An explosion.</summary>
        Explosion,
        /// <summary>A hard landing.</summary>
        Land,
        /// <summary>A purchase (UI).</summary>
        Purchase,
    }

    /// <summary>Pure mapping from game events and HUD state to audio (the session plays what these return).</summary>
    public static class AudioCues
    {
        /// <summary>Vehicle impacts below this relative speed are silent, m/s.</summary>
        public const float MinCrashSpeed = 2f;

        /// <summary>
        /// Gunshot → Gunshot; Shot that hit an actor (B ≠ 0) → BulletImpact; Punched → BatHit when Text is
        /// <paramref name="batName"/>, else Punch; CarCollision or PedHitByVehicle at ≥ <see cref="MinCrashSpeed"/> →
        /// Crash; Explosion → Explosion; Fell → Land; Purchase → Purchase; anything else → None.
        /// </summary>
        public static AudioCue ForEvent(GameEvent e, string batName)
        {
            switch (e.Type)
            {
                case GameEventType.Gunshot: return AudioCue.Gunshot;
                case GameEventType.Shot: return e.ActorB != 0 ? AudioCue.BulletImpact : AudioCue.None;
                case GameEventType.Punched: return e.Text != null && e.Text == batName ? AudioCue.BatHit : AudioCue.Punch;
                case GameEventType.CarCollision:
                case GameEventType.PedHitByVehicle: return e.Value >= MinCrashSpeed ? AudioCue.Crash : AudioCue.None;
                case GameEventType.Explosion: return AudioCue.Explosion;
                case GameEventType.Fell: return AudioCue.Land;
                case GameEventType.Purchase: return AudioCue.Purchase;
                default: return AudioCue.None;
            }
        }

        /// <summary>The jingle for a big screen that just appeared (<paramref name="previous"/> → <paramref name="current"/>), false when none.</summary>
        public static bool ForBigScreen(BigScreenKind previous, BigScreenKind current, out UiSound sound)
        {
            sound = UiSound.Select;
            if (current == previous) return false;
            switch (current)
            {
                case BigScreenKind.Wasted: sound = UiSound.Wasted; return true;
                case BigScreenKind.Busted: sound = UiSound.Busted; return true;
                case BigScreenKind.MissionPassed: sound = UiSound.MissionPassed; return true;
                case BigScreenKind.MissionFailed: sound = UiSound.MissionFailed; return true;
                default: return false;
            }
        }

        /// <summary>True when the wanted level rose.</summary>
        public static bool WantedUp(int previousStars, int stars) { return stars > previousStars; }

        /// <summary>True when a new phone text arrived (unread count rose).</summary>
        public static bool NewText(int previousUnread, int unread) { return unread > previousUnread; }

        /// <summary>Seconds of wail in one police siren cycle while pursuing at 1-2 stars.</summary>
        public const float WailSeconds = 4f;
        /// <summary>Seconds of yelp burst closing each police siren cycle while pursuing at 1-2 stars.</summary>
        public const float YelpBurstSeconds = 1.5f;
        /// <summary>Wanted stars from which the siren yelps continuously.</summary>
        public const int YelpStars = 3;

        /// <summary>
        /// Police siren pattern (F-VEH-5): at <see cref="YelpStars"/> or more wanted stars, yelp continuously; while
        /// pursuing at 1-2 stars, alternate <see cref="WailSeconds"/> of wail with <see cref="YelpBurstSeconds"/> of yelp
        /// (cycle phase from <paramref name="seconds"/>); otherwise wail. Returns <see cref="SirenKind.PoliceWail"/> or
        /// <see cref="SirenKind.PoliceYelp"/>.
        /// </summary>
        public static SirenKind PoliceSiren(int stars, bool pursuing, float seconds)
        {
            if (stars >= YelpStars) return SirenKind.PoliceYelp;
            if (stars < 1 || !pursuing) return SirenKind.PoliceWail;
            float cycle = WailSeconds + YelpBurstSeconds;
            float phase = seconds - (float)System.Math.Floor(seconds / cycle) * cycle;
            return phase < WailSeconds ? SirenKind.PoliceWail : SirenKind.PoliceYelp;
        }

        /// <summary>Siren pattern of a vehicle kind: ambulance hi-lo, fire horn, police (and anything else) wail.</summary>
        public static SirenKind SirenFor(VehicleKind kind)
        {
            switch (kind)
            {
                case VehicleKind.Ambulance: return SirenKind.AmbulanceHiLo;
                case VehicleKind.FireTruck: return SirenKind.FireHorn;
                default: return SirenKind.PoliceWail;
            }
        }
    }

    /// <summary>Edge detector for the HUD cash delta line appearing or changing (pure, allocation-free).</summary>
    public sealed class CashDeltaWatcher
    {
        private long _delta;
        private float _age = float.MaxValue;

        /// <summary>
        /// Call once per frame with <c>HudModel.CashDelta</c> and <c>CashDeltaAge</c>. True on the frame the line shows a
        /// new amount: the delta is non-zero and either changed or its age went back (a further transaction of the same size).
        /// </summary>
        public bool Update(long delta, float age)
        {
            bool fired = delta != 0 && (delta != _delta || age < _age);
            _delta = delta;
            _age = age;
            return fired;
        }
    }

    /// <summary>Edge detector for a new phone contact notification in the HUD feed (pure, allocation-free).</summary>
    public sealed class PhoneRingWatcher
    {
        private int _count;
        private float _age = float.MaxValue;

        /// <summary>
        /// Call once per frame with <c>HudMessages.NotificationCount</c> and the newest notification's age
        /// (<c>GetNotification(0).Age</c>, ignored when the count is 0) and whether it is a <c>NotificationKind.Contact</c>.
        /// True on the frame a new notification (the count rose from 0, or the newest age went back) is from a contact.
        /// </summary>
        public bool Update(int count, float newestAge, bool newestIsContact)
        {
            bool fresh = count > 0 && (_count == 0 || newestAge < _age);
            _count = count;
            _age = count > 0 ? newestAge : float.MaxValue;
            return fresh && newestIsContact;
        }
    }

    /// <summary>Footstep timing from distance walked on the ground (pure).</summary>
    public sealed class FootstepClock
    {
        /// <summary>Distance between footsteps at walking pace, metres.</summary>
        public const float WalkStride = 0.7f;
        /// <summary>Distance between footsteps when running or sprinting (speed above 4 m/s), metres.</summary>
        public const float RunStride = 1.1f;
        /// <summary>Speed above which the run stride applies, m/s.</summary>
        public const float RunSpeed = 4f;

        private float _travel;
        private bool _has;
        private Vec3 _last;

        /// <summary>
        /// Adds the horizontal distance from the previous call while on the ground; returns true (and keeps the
        /// remainder) when a stride is complete. Off the ground or the first call: no step, the distance is not counted.
        /// </summary>
        public bool Advance(Vec3 feet, bool onGround, float speed)
        {
            bool had = _has;
            Vec3 last = _last;
            _has = true;
            _last = feet;
            if (!had || !onGround) return false;
            _travel += Vec3.DistanceXZ(last, feet);
            float stride = speed > RunSpeed ? RunStride : WalkStride;
            if (_travel < stride) return false;
            _travel -= stride;
            if (_travel > stride) _travel = 0f;
            return true;
        }

        /// <summary>Forgets the position and the distance.</summary>
        public void Reset()
        {
            _has = false;
            _travel = 0f;
        }
    }
}
