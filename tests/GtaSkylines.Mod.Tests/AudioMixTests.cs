using System;
using System.Collections.Generic;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Settings;
using GtaSkylines.Mod.Audio;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class AudioMixTests
    {
        public static IEnumerable<object[]> Categories()
        {
            foreach (WorldCategory c in Enum.GetValues(typeof(WorldCategory))) yield return new object[] { c };
        }

        public static IEnumerable<object[]> Groups()
        {
            foreach (VolumeGroup g in Enum.GetValues(typeof(VolumeGroup)))
                if (g != VolumeGroup.Music) yield return new object[] { g };
        }

        [Theory]
        [InlineData(SurfaceFlags.None, SoundId.FootHard)]
        [InlineData(SurfaceFlags.Road, SoundId.FootHard)]
        [InlineData(SurfaceFlags.Pavement, SoundId.FootHard)]
        [InlineData(SurfaceFlags.Bridge, SoundId.FootHard)]
        [InlineData(SurfaceFlags.Building, SoundId.FootHard)]
        [InlineData(SurfaceFlags.Prop, SoundId.FootHard)]
        [InlineData(SurfaceFlags.Terrain, SoundId.FootSoft)]
        [InlineData(SurfaceFlags.Tree, SoundId.FootSoft)]
        [InlineData(SurfaceFlags.Interior, SoundId.FootWood)]
        [InlineData(SurfaceFlags.Road | SurfaceFlags.Bridge, SoundId.FootHard)]
        [InlineData(SurfaceFlags.Interior | SurfaceFlags.Building, SoundId.FootWood)]
        [InlineData(SurfaceFlags.Terrain | SurfaceFlags.Tree, SoundId.FootSoft)]
        public void Footstep_maps_surface_flags(SurfaceFlags s, SoundId expected)
        {
            Assert.Equal(expected, SoundBank.Footstep(s));
        }

        [Theory]
        [InlineData(WeaponId.Fists, SoundId.Punch)]
        [InlineData(WeaponId.Bat, SoundId.BatHit)]
        [InlineData(WeaponId.Pistol, SoundId.ShotPistol)]
        [InlineData(WeaponId.Smg, SoundId.ShotSmg)]
        [InlineData(WeaponId.Shotgun, SoundId.ShotShotgun)]
        [InlineData(WeaponId.Carbine, SoundId.ShotCarbine)]
        public void Gunshot_maps_weapons(WeaponId w, SoundId expected)
        {
            Assert.Equal(expected, SoundBank.Gunshot(w));
        }

        [Theory]
        [InlineData(UiSound.Move, SoundId.UiMove)]
        [InlineData(UiSound.Select, SoundId.UiSelect)]
        [InlineData(UiSound.Back, SoundId.UiBack)]
        [InlineData(UiSound.Purchase, SoundId.UiPurchase)]
        [InlineData(UiSound.Cash, SoundId.UiCash)]
        [InlineData(UiSound.PhoneRing, SoundId.PhoneRing)]
        [InlineData(UiSound.TextTone, SoundId.TextTone)]
        [InlineData(UiSound.MissionPassed, SoundId.MissionPassed)]
        [InlineData(UiSound.MissionFailed, SoundId.MissionFailed)]
        [InlineData(UiSound.Wasted, SoundId.Wasted)]
        [InlineData(UiSound.Busted, SoundId.Busted)]
        [InlineData(UiSound.WantedUp, SoundId.WantedUp)]
        public void Ui_maps_sounds(UiSound u, SoundId expected)
        {
            Assert.Equal(expected, SoundBank.Ui(u));
        }

        [Theory]
        [InlineData(SirenKind.PoliceWail, SoundId.SirenWail)]
        [InlineData(SirenKind.PoliceYelp, SoundId.SirenYelp)]
        [InlineData(SirenKind.AmbulanceHiLo, SoundId.SirenHiLo)]
        [InlineData(SirenKind.FireHorn, SoundId.FireHorn)]
        public void Siren_maps_kinds(SirenKind k, SoundId expected)
        {
            Assert.Equal(expected, SoundBank.Siren(k));
        }

        [Theory]
        [InlineData(0f, SoundId.CrashLight)]
        [InlineData(5.99f, SoundId.CrashLight)]
        [InlineData(6f, SoundId.CrashMedium)]
        [InlineData(13.99f, SoundId.CrashMedium)]
        [InlineData(14f, SoundId.CrashHeavy)]
        [InlineData(80f, SoundId.CrashHeavy)]
        [InlineData(-5f, SoundId.CrashLight)]
        [InlineData(float.NaN, SoundId.CrashLight)]
        public void CrashTier_thresholds(float speed, SoundId expected)
        {
            Assert.Equal(expected, SoundBank.CrashTier(speed));
        }

        [Fact]
        public void CrashVolume_is_bounded_monotonic_and_saturates()
        {
            Assert.Equal(0.25f, SoundBank.CrashVolume(0f));
            Assert.Equal(0.25f, SoundBank.CrashVolume(-3f));
            Assert.Equal(0.25f, SoundBank.CrashVolume(float.NaN));
            Assert.Equal(1f, SoundBank.CrashVolume(25f));
            Assert.Equal(1f, SoundBank.CrashVolume(200f));
            float prev = 0f;
            for (float v = 0f; v <= 40f; v += 0.5f)
            {
                float g = SoundBank.CrashVolume(v);
                Assert.InRange(g, 0.25f, 1f);
                Assert.True(g >= prev, "decreased at " + v);
                prev = g;
            }
        }

        private static GameSettings Settings(float master, float effects)
        {
            var s = new GameSettings();
            s.MasterVolume = master;
            s.EffectsVolume = effects;
            return s;
        }

        [Theory, MemberData(nameof(Groups))]
        public void GroupGain_is_master_times_effects(VolumeGroup g)
        {
            Assert.Equal(0.35f, AudioMix.GroupGain(Settings(0.5f, 0.7f), g), 5);
        }

        [Theory, MemberData(nameof(Groups))]
        public void GroupGain_clamps_each_factor_and_zeroes_NaN(VolumeGroup g)
        {
            Assert.Equal(1f, AudioMix.GroupGain(Settings(3f, 2f), g), 5);
            Assert.Equal(0f, AudioMix.GroupGain(Settings(-1f, 0.5f), g), 5);
            Assert.Equal(0.5f, AudioMix.GroupGain(Settings(5f, 0.5f), g), 5);
            Assert.Equal(0f, AudioMix.GroupGain(Settings(float.NaN, 0.5f), g), 5);
            Assert.Equal(0f, AudioMix.GroupGain(Settings(0.5f, float.NaN), g), 5);
        }

        [Theory, MemberData(nameof(Categories))]
        public void Distances_are_positive_and_ordered(WorldCategory c)
        {
            Assert.True(AudioMix.MinDistance(c) > 0f);
            Assert.True(AudioMix.MaxDistance(c) > AudioMix.MinDistance(c));
        }

        [Fact]
        public void Max_distance_extremes()
        {
            float explosion = AudioMix.MaxDistance(WorldCategory.Explosion);
            float siren = AudioMix.MaxDistance(WorldCategory.Siren);
            float foot = AudioMix.MaxDistance(WorldCategory.Foot);
            Assert.True(explosion >= 300f);
            Assert.True(siren >= 300f);
            Assert.True(foot <= 60f);
            foreach (WorldCategory c in Enum.GetValues(typeof(WorldCategory)))
            {
                float m = AudioMix.MaxDistance(c);
                Assert.True(m <= Math.Max(explosion, siren));
                Assert.True(m >= foot);
            }
        }

        [Fact]
        public void EnginePitch_rises_strictly_across_the_range()
        {
            Assert.InRange(AudioMix.EnginePitch(0f), 0.5f, 0.9f);
            Assert.InRange(AudioMix.EnginePitch(1f), 1.6f, 2.6f);
            float prev = AudioMix.EnginePitch(0f);
            for (int i = 1; i <= 100; i++)
            {
                float p = AudioMix.EnginePitch(i / 100f);
                Assert.True(p > prev, "not increasing at " + i);
                prev = p;
            }
        }

        [Fact]
        public void EnginePitch_clamps_outside_zero_one()
        {
            Assert.Equal(AudioMix.EnginePitch(0f), AudioMix.EnginePitch(-2f));
            Assert.Equal(AudioMix.EnginePitch(1f), AudioMix.EnginePitch(7f));
        }
    }
}
