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
