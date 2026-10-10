using System;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Profile;

namespace GtaSkylines.Mod.Audio
{
    /// <summary>Every procedural sound. Loops are listed in <see cref="SoundBank.IsLoop"/>.</summary>
    public enum SoundId
    {
        /// <summary>Footstep on road, pavement, bridge or building.</summary>
        FootHard,
        /// <summary>Footstep on terrain.</summary>
        FootSoft,
        /// <summary>Footstep on interior wood or tile.</summary>
        FootWood,
        /// <summary>Jump take-off.</summary>
        Jump,
        /// <summary>Landing thud.</summary>
        Land,
        /// <summary>Fist hit.</summary>
        Punch,
        /// <summary>Bat hit.</summary>
        BatHit,
        /// <summary>Pistol shot.</summary>
        ShotPistol,
        /// <summary>SMG shot (one round; rapid fire makes the rattle).</summary>
        ShotSmg,
        /// <summary>Shotgun boom.</summary>
        ShotShotgun,
        /// <summary>Carbine shot.</summary>
        ShotCarbine,
        /// <summary>Empty-magazine click.</summary>
        DryFire,
        /// <summary>Magazine out, in, slide.</summary>
        Reload,
        /// <summary>Bullet hitting a surface.</summary>
        BulletImpact,
        /// <summary>Car crash under 6 m/s.</summary>
        CrashLight,
        /// <summary>Car crash 6 to 14 m/s.</summary>
        CrashMedium,
        /// <summary>Car crash from 14 m/s.</summary>
        CrashHeavy,
        /// <summary>Two-tone horn tap.</summary>
        Horn,
        /// <summary>Two-tone horn held (loop).</summary>
        HornLoop,
        /// <summary>Police wail (loop).</summary>
        SirenWail,
        /// <summary>Police yelp (loop).</summary>
        SirenYelp,
        /// <summary>Ambulance hi-lo (loop).</summary>
        SirenHiLo,
        /// <summary>Fire engine air horn (loop).</summary>
        FireHorn,
        /// <summary>Engine at a reference rpm (loop; pitch follows rpm at playback).</summary>
        EngineLoop,
        /// <summary>Tyre squeal (loop).</summary>
        ScreechLoop,
        /// <summary>Explosion.</summary>
        Explosion,
        /// <summary>Menu cursor move.</summary>
        UiMove,
        /// <summary>Menu select.</summary>
        UiSelect,
        /// <summary>Menu back.</summary>
        UiBack,
        /// <summary>Purchase.</summary>
        UiPurchase,
        /// <summary>Cash gained.</summary>
        UiCash,
        /// <summary>Phone ringtone (one phrase; repeat to keep ringing).</summary>
        PhoneRing,
        /// <summary>Text message tone.</summary>
        TextTone,
        /// <summary>Mission passed jingle.</summary>
        MissionPassed,
        /// <summary>Mission failed sting.</summary>
        MissionFailed,
        /// <summary>Wasted stinger.</summary>
        Wasted,
        /// <summary>Busted stinger.</summary>
        Busted,
        /// <summary>Wanted level up.</summary>
        WantedUp,
        /// <summary>Interior room tone (loop).</summary>
        RoomToneLoop,
        /// <summary>Police helicopter rotor chop (loop).</summary>
        RotorLoop,
    }

    /// <summary>Interface sounds, by intention.</summary>
    public enum UiSound
    {
        /// <summary>Cursor move.</summary>
        Move,
        /// <summary>Select.</summary>
        Select,
        /// <summary>Back.</summary>
        Back,
        /// <summary>Purchase.</summary>
        Purchase,
        /// <summary>Cash gained.</summary>
        Cash,
        /// <summary>Phone ring.</summary>
        PhoneRing,
        /// <summary>Text tone.</summary>
        TextTone,
        /// <summary>Mission passed.</summary>
        MissionPassed,
        /// <summary>Mission failed.</summary>
        MissionFailed,
        /// <summary>Wasted.</summary>
        Wasted,
        /// <summary>Busted.</summary>
        Busted,
        /// <summary>Wanted level up.</summary>
        WantedUp,
    }

    /// <summary>Emergency siren patterns.</summary>
    public enum SirenKind
    {
        /// <summary>Police wail.</summary>
        PoliceWail,
        /// <summary>Police yelp.</summary>
        PoliceYelp,
        /// <summary>Ambulance hi-lo.</summary>
        AmbulanceHiLo,
        /// <summary>Fire engine air horn.</summary>
        FireHorn,
    }

    /// <summary>
    /// Pure procedural synthesis (no Unity): every sound as a mono float buffer at 44 100 Hz, deterministic, peak exactly
    /// −1 dBFS, one-shots faded at both ends, loops seamless.
    /// </summary>
    public static class SoundBank
    {
        /// <summary>Sample rate of every buffer.</summary>
        public const int SampleRate = 44100;

        /// <summary>Peak level of every buffer: −1 dBFS.</summary>
        public const float PeakLimit = 0.8912509f;

        private static readonly int s_count = Enum.GetValues(typeof(SoundId)).Length;

        /// <summary>Number of <see cref="SoundId"/> values.</summary>
        public static int Count { get { return s_count; } }

        /// <summary>True for sounds rendered as seamless loops.</summary>
        public static bool IsLoop(SoundId id)
        {
            switch (id)
            {
                case SoundId.HornLoop:
                case SoundId.SirenWail:
                case SoundId.SirenYelp:
                case SoundId.SirenHiLo:
                case SoundId.FireHorn:
                case SoundId.EngineLoop:
                case SoundId.ScreechLoop:
                case SoundId.RoomToneLoop:
                case SoundId.RotorLoop:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>How many distinct takes a sound has (picked at random at playback against repetition).</summary>
        public static int VariantCount(SoundId id)
        {
            switch (id)
            {
                case SoundId.FootHard:
                case SoundId.FootSoft:
                case SoundId.FootWood:
                    return 4;
                case SoundId.Punch:
                case SoundId.BulletImpact:
                case SoundId.ShotPistol:
                case SoundId.ShotSmg:
                case SoundId.ShotShotgun:
                case SoundId.ShotCarbine:
                    return 3;
                case SoundId.Land:
                case SoundId.BatHit:
                case SoundId.CrashLight:
                case SoundId.CrashMedium:
                case SoundId.CrashHeavy:
                case SoundId.Explosion:
                    return 2;
                default:
                    return 1;
            }
        }

        /// <summary>Renders one take. Returns a new array each call; identical input gives identical output.</summary>
        public static float[] Render(SoundId id, int variant)
        {
            if (variant < 0 || variant >= VariantCount(id)) throw new ArgumentOutOfRangeException("variant");
            uint seed = (uint)((int)id * 7919 + variant * 104729 + 17);
            float[] b = Recipes.Raw(id, variant, seed);
            if (!IsLoop(id)) Dsp.FadeEdges(b, 0.002, 0.012);
            return Dsp.Normalize(b);
        }

        /// <summary>Footstep for a surface: interior wins, then hard surfaces, then terrain or tree; nothing known is hard.</summary>
        public static SoundId Footstep(SurfaceFlags s)
        {
            if ((s & SurfaceFlags.Interior) != 0) return SoundId.FootWood;
            if ((s & (SurfaceFlags.Road | SurfaceFlags.Pavement | SurfaceFlags.Bridge | SurfaceFlags.Building | SurfaceFlags.Prop)) != 0) return SoundId.FootHard;
            if ((s & (SurfaceFlags.Terrain | SurfaceFlags.Tree)) != 0) return SoundId.FootSoft;
            return SoundId.FootHard;
        }

        /// <summary>Attack sound of a weapon (fists punch, bat hits).</summary>
        public static SoundId Gunshot(WeaponId w)
        {
            switch (w)
            {
                case WeaponId.Pistol: return SoundId.ShotPistol;
                case WeaponId.Smg: return SoundId.ShotSmg;
                case WeaponId.Shotgun: return SoundId.ShotShotgun;
                case WeaponId.Carbine: return SoundId.ShotCarbine;
                case WeaponId.Bat: return SoundId.BatHit;
                default: return SoundId.Punch;
            }
        }

        /// <summary>The sound of an interface event.</summary>
        public static SoundId Ui(UiSound u)
        {
            switch (u)
            {
                case UiSound.Move: return SoundId.UiMove;
                case UiSound.Select: return SoundId.UiSelect;
                case UiSound.Back: return SoundId.UiBack;
                case UiSound.Purchase: return SoundId.UiPurchase;
                case UiSound.Cash: return SoundId.UiCash;
                case UiSound.PhoneRing: return SoundId.PhoneRing;
                case UiSound.TextTone: return SoundId.TextTone;
                case UiSound.MissionPassed: return SoundId.MissionPassed;
                case UiSound.MissionFailed: return SoundId.MissionFailed;
                case UiSound.Wasted: return SoundId.Wasted;
                case UiSound.Busted: return SoundId.Busted;
                default: return SoundId.WantedUp;
            }
        }

        /// <summary>The loop of a siren pattern.</summary>
        public static SoundId Siren(SirenKind k)
        {
            switch (k)
            {
                case SirenKind.PoliceYelp: return SoundId.SirenYelp;
                case SirenKind.AmbulanceHiLo: return SoundId.SirenHiLo;
                case SirenKind.FireHorn: return SoundId.FireHorn;
                default: return SoundId.SirenWail;
            }
        }

        /// <summary>Crash take for an impact speed in m/s: under 6 light, under 14 medium, else heavy; NaN or negative light.</summary>
        public static SoundId CrashTier(float impactSpeedMps)
        {
            if (!(impactSpeedMps >= 6f)) return SoundId.CrashLight;
            return impactSpeedMps < 14f ? SoundId.CrashMedium : SoundId.CrashHeavy;
        }

        /// <summary>Crash playback volume: 0.25 at rest (or NaN) rising linearly to 1 at 25 m/s.</summary>
        public static float CrashVolume(float impactSpeedMps)
        {
            if (!(impactSpeedMps > 0f)) return 0.25f;
            float v = 0.25f + 0.75f * impactSpeedMps / 25f;
            return v > 1f ? 1f : v;
        }
    }
}
