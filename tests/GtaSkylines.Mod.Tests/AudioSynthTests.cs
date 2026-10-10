using System;
using System.Collections.Generic;
using System.Diagnostics;
using GtaSkylines.Mod.Audio;
using Xunit;
using Xunit.Abstractions;

namespace GtaSkylines.Mod.Tests
{
    public class AudioSynthTests
    {
        private static readonly Dictionary<string, float[]> Cache = new Dictionary<string, float[]>();
        private readonly ITestOutputHelper _out;

        public AudioSynthTests(ITestOutputHelper output) { _out = output; }

        public static IEnumerable<object[]> AllSounds()
        {
            foreach (SoundId id in Enum.GetValues(typeof(SoundId))) yield return new object[] { id };
        }

        public static IEnumerable<object[]> AllVariants()
        {
            foreach (SoundId id in Enum.GetValues(typeof(SoundId)))
                for (int v = 0; v < SoundBank.VariantCount(id); v++) yield return new object[] { id, v };
        }

        private static float[] Get(SoundId id, int v)
        {
            string key = id + "/" + v;
            lock (Cache)
            {
                float[] a;
                if (!Cache.TryGetValue(key, out a)) { a = SoundBank.Render(id, v); Cache[key] = a; }
                return a;
            }
        }

        private static double Seconds(float[] a) { return a.Length / (double)SoundBank.SampleRate; }

        private static double Rms(float[] a, int from, int count)
        {
            double s = 0;
            for (int i = from; i < from + count; i++) s += (double)a[i] * a[i];
            return Math.Sqrt(s / count);
        }

        private static double MeanAbsDiff(float[] a, float[] b)
        {
            int n = Math.Min(a.Length, b.Length);
            double s = 0;
            for (int i = 0; i < n; i++) s += Math.Abs(a[i] - b[i]);
            return s / n;
        }

        [Fact]
        public void Constants_and_count()
        {
            Assert.Equal(44100, SoundBank.SampleRate);
            Assert.Equal(0.8912509f, SoundBank.PeakLimit);
            Assert.Equal(Enum.GetValues(typeof(SoundId)).Length, SoundBank.Count);
        }

        [Fact]
        public void IsLoop_is_true_exactly_for_the_loop_sounds()
        {
            var loops = new HashSet<SoundId>
            {
                SoundId.HornLoop, SoundId.SirenWail, SoundId.SirenYelp, SoundId.SirenHiLo,
                SoundId.FireHorn, SoundId.EngineLoop, SoundId.ScreechLoop, SoundId.RoomToneLoop
            };
            foreach (SoundId id in Enum.GetValues(typeof(SoundId)))
                Assert.Equal(loops.Contains(id), SoundBank.IsLoop(id));
        }

        [Theory, MemberData(nameof(AllSounds))]
        public void VariantCount_is_between_1_and_8(SoundId id)
        {
            Assert.InRange(SoundBank.VariantCount(id), 1, 8);
        }

        [Theory, MemberData(nameof(AllVariants))]
        public void Length_is_in_range_for_the_kind_of_sound(SoundId id, int v)
        {
            double s = Seconds(Get(id, v));
            Assert.InRange(s, 0.03, 6.0);
            if (SoundBank.IsLoop(id)) Assert.InRange(s, 0.25, 4.0);
            if (id == SoundId.FootHard || id == SoundId.FootSoft || id == SoundId.FootWood) Assert.True(s <= 0.4, "footstep " + s);
            if (id == SoundId.UiMove || id == SoundId.UiSelect || id == SoundId.UiBack) Assert.True(s <= 0.35, "ui " + s);
            if (id == SoundId.MissionPassed || id == SoundId.Wasted || id == SoundId.Busted || id == SoundId.Explosion)
                Assert.True(s >= 1.5, "long cue " + s);
            if (id == SoundId.ShotPistol || id == SoundId.ShotSmg || id == SoundId.ShotShotgun || id == SoundId.ShotCarbine)
                Assert.True(s >= 0.25, "gunshot " + s);
        }

        [Theory, MemberData(nameof(AllVariants))]
        public void Samples_are_finite_and_peak_is_normalised(SoundId id, int v)
        {
            float[] a = Get(id, v);
            float peak = 0;
            foreach (float x in a)
            {
                Assert.False(float.IsNaN(x) || float.IsInfinity(x));
                peak = Math.Max(peak, Math.Abs(x));
            }
            Assert.True(peak <= SoundBank.PeakLimit + 1e-6f, "peak " + peak);
            Assert.True(peak >= 0.85f, "peak " + peak);
        }

        [Theory, MemberData(nameof(AllVariants))]
        public void One_shots_fade_in_and_out(SoundId id, int v)
        {
            if (SoundBank.IsLoop(id)) return;
            float[] a = Get(id, v);
            Assert.True(Math.Abs(a[0]) <= 1e-4f, "first " + a[0]);
            Assert.True(Math.Abs(a[a.Length - 1]) <= 1e-4f, "last " + a[a.Length - 1]);
        }

        [Theory, MemberData(nameof(AllVariants))]
        public void Loops_wrap_seamlessly(SoundId id, int v)
        {
            if (!SoundBank.IsLoop(id)) return;
            float[] a = Get(id, v);
            double sum = 0;
            for (int i = 0; i + 1 < a.Length; i++) sum += Math.Abs(a[i + 1] - a[i]);
            double mean = sum / (a.Length - 1);
            double jump = Math.Abs(a[a.Length - 1] - a[0]);
            Assert.True(jump <= Math.Max(0.05, 3 * mean), "wrap jump " + jump + " mean step " + mean);

            int w = SoundBank.SampleRate / 50;
            double head = Rms(a, 0, w), tail = Rms(a, a.Length - w, w);
            double ratio = Math.Max(head, tail) / Math.Max(Math.Min(head, tail), 1e-12);
            Assert.True(ratio <= 2.5, "head/tail rms ratio " + ratio);
        }

        [Theory, MemberData(nameof(AllVariants))]
        public void Sound_is_not_silent(SoundId id, int v)
        {
            float[] a = Get(id, v);
            Assert.True(Rms(a, 0, a.Length) >= 0.02);
        }

        [Theory, MemberData(nameof(AllVariants))]
        public void Render_is_deterministic_and_returns_a_new_array(SoundId id, int v)
        {
            float[] a = SoundBank.Render(id, v);
            float[] b = SoundBank.Render(id, v);
            Assert.NotSame(a, b);
            Assert.Equal(a.Length, b.Length);
            for (int i = 0; i < a.Length; i++) Assert.Equal(a[i], b[i]);
        }

        [Fact]
        public void Different_sounds_are_audibly_distinct()
        {
            var ids = (SoundId[])Enum.GetValues(typeof(SoundId));
            for (int i = 0; i < ids.Length; i++)
                for (int j = i + 1; j < ids.Length; j++)
                {
                    float[] a = Get(ids[i], 0), b = Get(ids[j], 0);
                    if (a.Length != b.Length) continue;
                    Assert.True(MeanAbsDiff(a, b) >= 0.005, ids[i] + " vs " + ids[j]);
                }
        }

        [Theory, MemberData(nameof(AllSounds))]
        public void Variants_of_one_sound_differ(SoundId id)
        {
            int n = SoundBank.VariantCount(id);
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    float[] a = Get(id, i), b = Get(id, j);
                    bool same = a.Length == b.Length;
                    for (int k = 0; same && k < a.Length; k++) same = a[k] == b[k];
                    Assert.False(same, id + " variants " + i + " and " + j + " are identical");
                }
        }

        [Fact]
        public void Footsteps_have_at_least_three_variants()
        {
            Assert.True(SoundBank.VariantCount(SoundId.FootHard) >= 3);
            Assert.True(SoundBank.VariantCount(SoundId.FootSoft) >= 3);
            Assert.True(SoundBank.VariantCount(SoundId.FootWood) >= 3);
        }

        [Fact]
        public void Heavy_crash_is_longer_than_light_crash()
        {
            Assert.True(Get(SoundId.CrashHeavy, 0).Length > Get(SoundId.CrashLight, 0).Length);
        }

        [Theory, MemberData(nameof(AllSounds))]
        public void Render_rejects_out_of_range_variants(SoundId id)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SoundBank.Render(id, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => SoundBank.Render(id, SoundBank.VariantCount(id)));
        }

        [Fact]
        public void Rendering_everything_takes_under_two_seconds()
        {
            var sw = Stopwatch.StartNew();
            foreach (SoundId id in Enum.GetValues(typeof(SoundId)))
                for (int v = 0; v < SoundBank.VariantCount(id); v++) SoundBank.Render(id, v);
            sw.Stop();
            _out.WriteLine("Rendered all sounds in " + sw.ElapsedMilliseconds + " ms");
            Assert.True(sw.ElapsedMilliseconds < 2000, sw.ElapsedMilliseconds + " ms");
        }
    }
}
