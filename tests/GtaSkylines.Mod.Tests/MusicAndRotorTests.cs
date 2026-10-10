using System;
using GtaSkylines.Game.Settings;
using GtaSkylines.Mod.Audio;
using GtaSkylines.Mod.Play;
using Xunit;
using GtaSkylines.Game.World;

namespace GtaSkylines.Mod.Tests
{
    public class MusicAndRotorTests
    {
        [Fact]
        public void Music_gain_is_master_times_music_and_ignores_effects()
        {
            var s = new GameSettings { MasterVolume = 0.5f, MusicVolume = 0.4f, EffectsVolume = 0f, UiVolume = 0f };
            Assert.Equal(0.2f, AudioMix.GroupGain(s, VolumeGroup.Music), 5);
        }

        [Fact]
        public void Music_gain_clamps_and_zeroes_NaN()
        {
            Assert.Equal(1f, AudioMix.GroupGain(new GameSettings { MasterVolume = 3f, MusicVolume = 2f }, VolumeGroup.Music), 5);
            Assert.Equal(0f, AudioMix.GroupGain(new GameSettings { MasterVolume = 1f, MusicVolume = float.NaN }, VolumeGroup.Music), 5);
            Assert.Equal(0f, AudioMix.GroupGain(new GameSettings { MasterVolume = -1f, MusicVolume = 1f }, VolumeGroup.Music), 5);
        }

        [Fact]
        public void Music_gain_follows_the_slider_live()
        {
            var s = new GameSettings { MasterVolume = 1f, MusicVolume = 0.2f };
            float low = AudioMix.GroupGain(s, VolumeGroup.Music);
            s.MusicVolume = 0.8f;
            Assert.True(AudioMix.GroupGain(s, VolumeGroup.Music) > low);
        }

        [Theory]
        [InlineData(UiSound.MissionPassed)]
        [InlineData(UiSound.MissionFailed)]
        [InlineData(UiSound.Wasted)]
        [InlineData(UiSound.Busted)]
        [InlineData(UiSound.WantedUp)]
        public void Jingles_map_to_the_Music_bus(UiSound u)
        {
            Assert.Equal(VolumeGroup.Music, AudioMix.GroupOf(u));
        }

        [Theory]
        [InlineData(UiSound.Move)]
        [InlineData(UiSound.Select)]
        [InlineData(UiSound.Cash)]
        [InlineData(UiSound.PhoneRing)]
        [InlineData(UiSound.TextTone)]
        public void Other_ui_sounds_stay_on_the_Ui_bus(UiSound u)
        {
            Assert.Equal(VolumeGroup.Ui, AudioMix.GroupOf(u));
        }

        [Theory]
        [InlineData(BigScreenKind.Wasted)]
        [InlineData(BigScreenKind.Busted)]
        [InlineData(BigScreenKind.MissionPassed)]
        [InlineData(BigScreenKind.MissionFailed)]
        public void Big_screen_jingles_are_played_on_Music(BigScreenKind k)
        {
            UiSound sound;
            Assert.True(AudioCues.ForBigScreen(BigScreenKind.None, k, out sound));
            Assert.Equal(VolumeGroup.Music, AudioMix.GroupOf(sound));
        }

        [Fact]
        public void Rotor_is_a_seamless_loop_chopped_at_15_Hz()
        {
            Assert.True(SoundBank.IsLoop(SoundId.RotorLoop));
            float[] b = SoundBank.Render(SoundId.RotorLoop, 0);
            Assert.Equal(SoundBank.SampleRate, b.Length);
            double p15 = Power(b, 15), p9 = Power(b, 9), p21 = Power(b, 21);
            Assert.True(p15 > 4 * p9 && p15 > 4 * p21, "15 Hz " + p15 + " vs " + p9 + ", " + p21);
        }

        // Spectral power of the squared signal (its envelope) at hz.
        private static double Power(float[] b, double hz)
        {
            double re = 0, im = 0;
            for (int i = 0; i < b.Length; i++)
            {
                double e = b[i] * (double)b[i];
                double a = 2 * Math.PI * hz * i / SoundBank.SampleRate;
                re += e * Math.Cos(a);
                im += e * Math.Sin(a);
            }
            return re * re + im * im;
        }
    }
}
