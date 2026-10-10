using GtaSkylines.Game.Settings;

namespace GtaSkylines.Mod.Audio
{
    /// <summary>Volume group a voice belongs to.</summary>
    public enum VolumeGroup
    {
        /// <summary>Foot, weapons, impacts, explosions.</summary>
        Effects,
        /// <summary>Engines, horns, sirens, tyres, crashes.</summary>
        Vehicles,
        /// <summary>Menus, phone, text tone.</summary>
        Ui,
        /// <summary>Mission and death jingles and stingers (master × the music slider).</summary>
        Music,
    }

    /// <summary>World sound category: sets the 3D distance range.</summary>
    public enum WorldCategory
    {
        /// <summary>Footsteps, jumps, punches.</summary>
        Foot,
        /// <summary>Gunshots.</summary>
        Weapon,
        /// <summary>Bullet impacts, crashes.</summary>
        Impact,
        /// <summary>Engines, horns, tyres.</summary>
        Vehicle,
        /// <summary>Sirens.</summary>
        Siren,
        /// <summary>Explosions.</summary>
        Explosion,
    }

    /// <summary>Pure mixing rules: group gains from settings, 3D ranges, engine pitch, per-sound base levels.</summary>
    public static class AudioMix
    {
        private static readonly float[] s_min = { 2f, 8f, 4f, 6f, 15f, 20f };
        private static readonly float[] s_max = { 40f, 250f, 120f, 150f, 400f, 600f };

        /// <summary>
        /// Gain of a group: master × effects, × the vehicles slider for Vehicles and × the UI slider for Ui; Music is
        /// master × the music slider only. Each factor is clamped to [0,1]; NaN counts as 0.
        /// </summary>
        public static float GroupGain(GameSettings s, VolumeGroup grp)
        {
            if (grp == VolumeGroup.Music) return C01(s.MasterVolume) * C01(s.MusicVolume);
            float g = C01(s.MasterVolume) * C01(s.EffectsVolume);
            if (grp == VolumeGroup.Vehicles) g *= C01(s.VehiclesVolume);
            else if (grp == VolumeGroup.Ui) g *= C01(s.UiVolume);
            return g;
        }

        /// <summary>Volume group of an interface sound: the mission and death jingles are Music, the rest Ui.</summary>
        public static VolumeGroup GroupOf(UiSound u)
        {
            switch (u)
            {
                case UiSound.MissionPassed:
                case UiSound.MissionFailed:
                case UiSound.Wasted:
                case UiSound.Busted: return VolumeGroup.Music;
                default: return VolumeGroup.Ui;
            }
        }

        /// <summary>Distance (m) inside which a world sound plays at full volume.</summary>
        public static float MinDistance(WorldCategory c) { return s_min[(int)c]; }

        /// <summary>Distance (m) at which a world sound reaches silence (linear rolloff).</summary>
        public static float MaxDistance(WorldCategory c) { return s_max[(int)c]; }

        /// <summary>Playback pitch of <see cref="SoundId.EngineLoop"/> for a normalised rpm (clamped to [0,1]).</summary>
        public static float EnginePitch(float rpm01) { return 0.6f + 1.5f * C01(rpm01); }

        /// <summary>Playback level of a sound relative to the loudest (all buffers are normalised to the same peak).</summary>
        public static float BaseVolume(SoundId id)
        {
            switch (id)
            {
                case SoundId.FootHard:
                case SoundId.FootSoft:
                case SoundId.FootWood: return 0.35f;
                case SoundId.Jump:
                case SoundId.Land:
                case SoundId.DryFire:
                case SoundId.UiMove: return 0.45f;
                case SoundId.Reload:
                case SoundId.RoomToneLoop:
                case SoundId.UiBack:
                case SoundId.UiSelect: return 0.5f;
                case SoundId.EngineLoop:
                case SoundId.ScreechLoop:
                case SoundId.BulletImpact:
                case SoundId.Punch: return 0.65f;
                case SoundId.ShotShotgun:
                case SoundId.Explosion:
                case SoundId.CrashHeavy:
                case SoundId.SirenWail:
                case SoundId.SirenYelp:
                case SoundId.SirenHiLo:
                case SoundId.FireHorn: return 1f;
                default: return 0.8f;
            }
        }

        private static float C01(float v) { return !(v > 0f) ? 0f : (v > 1f ? 1f : v); }
    }
}
