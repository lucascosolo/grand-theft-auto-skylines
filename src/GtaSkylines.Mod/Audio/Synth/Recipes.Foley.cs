using System;

namespace GtaSkylines.Mod.Audio
{
    /// <summary>The sound recipes. Each returns an un-normalised buffer; <see cref="SoundBank.Render"/> fades and normalises.</summary>
    internal static partial class Recipes
    {
        public static float[] Raw(SoundId id, int v, uint seed)
        {
            var n = new Noise(seed);
            switch (id)
            {
                case SoundId.FootHard: return FootHard(ref n);
                case SoundId.FootSoft: return FootSoft(ref n);
                case SoundId.FootWood: return FootWood(ref n);
                case SoundId.Jump: return Jump(ref n);
                case SoundId.Land: return Land(ref n, v);
                case SoundId.Punch: return Punch(ref n, v);
                case SoundId.BatHit: return BatHit(ref n, v);
                case SoundId.ShotPistol: return Shot(ref n, Pistol);
                case SoundId.ShotSmg: return Shot(ref n, Smg);
                case SoundId.ShotShotgun: return Shot(ref n, Shotgun);
                case SoundId.ShotCarbine: return Shot(ref n, Carbine);
                case SoundId.DryFire: return DryFire(ref n);
                case SoundId.Reload: return Reload(ref n);
                case SoundId.BulletImpact: return BulletImpact(ref n, v);
                case SoundId.CrashLight: return Crash(ref n, 0.2f);
                case SoundId.CrashMedium: return Crash(ref n, 0.6f);
                case SoundId.CrashHeavy: return Crash(ref n, 1f);
                case SoundId.Horn: return Horn(false);
                case SoundId.HornLoop: return Horn(true);
                case SoundId.SirenWail: return SirenSweep(4.0, 1, 0.4);
                case SoundId.SirenYelp: return SirenSweep(1.2, 4, 0.5);
                case SoundId.SirenHiLo: return SirenHiLo();
                case SoundId.FireHorn: return FireHorn();
                case SoundId.EngineLoop: return Engine(ref n);
                case SoundId.ScreechLoop: return Screech(ref n);
                case SoundId.Explosion: return Explosion(ref n);
                case SoundId.RoomToneLoop: return RoomTone(ref n);
                default: return Ui(id, ref n);
            }
        }

        // Heel strike then toe scuff: a bright band-passed click over a small low thump.
        private static float[] FootHard(ref Noise n)
        {
            float[] b = Dsp.New(0.22);
            double bright = n.Range(2200f, 3600f);
            Dsp.Burst(b, ref n, 0.0, 1, bright, 1.2, 0.0006, 0.012, 1f);
            Dsp.Burst(b, ref n, 0.0, 2, 5000, 0.7, 0.0003, 0.004, 0.5f);
            Dsp.Ping(b, 0.0, n.Range(140f, 180f), 90, 0.01, 0.001, 0.025, 0.5f);
            double toe = n.Range(0.045f, 0.07f);
            Dsp.Burst(b, ref n, toe, 1, bright * 0.8, 0.9, 0.003, 0.02, 0.45f);
            return b;
        }

        // Grass and dirt: a dull low-passed press with a few crunchy grains.
        private static float[] FootSoft(ref Noise n)
        {
            float[] b = Dsp.New(0.26);
            Dsp.Burst(b, ref n, 0.0, 0, n.Range(600f, 900f), 0.8, 0.008, 0.045, 1.4f);
            Dsp.Ping(b, 0.0, 110, 70, 0.02, 0.004, 0.03, 0.4f);
            int grains = 6 + (int)n.Range(0f, 5f);
            for (int g = 0; g < grains; g++)
                Dsp.Burst(b, ref n, n.Range(0.005f, 0.12f), 1, n.Range(1800f, 4500f), 2.0, 0.0002, 0.003, n.Range(0.15f, 0.4f));
            return b;
        }

        // Wood or tile: a knock that rings through a couple of body modes.
        private static float[] FootWood(ref Noise n)
        {
            float[] b = Dsp.New(0.24);
            double f = n.Range(240f, 330f);
            Dsp.Modes(b, ref n, 0.0, new[] { f, f * 2.73, f * 4.1 }, 18, 0.03, 0.5f);
            Dsp.Burst(b, ref n, 0.0, 1, 3500, 1.0, 0.0003, 0.006, 0.5f);
            Dsp.Modes(b, ref n, n.Range(0.05f, 0.07f), new[] { f * 1.1, f * 3.0 }, 14, 0.02, 0.25f);
            return b;
        }

        // Cloth whoosh with a rising band and a push-off scuff.
        private static float[] Jump(ref Noise n)
        {
            float[] b = Dsp.New(0.32);
            var f = new Svf(500, 1.5);
            for (int i = 0; i < b.Length; i++)
            {
                double t = i / (double)Dsp.Sr;
                f.Set(500 + 3500 * t / 0.32, 1.5);
                b[i] += (float)Math.Sin(Math.PI * t / 0.32) * f.Bp(n.Next()) * 0.6f;
            }
            Dsp.Burst(b, ref n, 0.0, 1, 2600, 1.0, 0.001, 0.015, 0.8f);
            return b;
        }

        private static float[] Land(ref Noise n, int v)
        {
            float[] b = Dsp.New(0.38);
            Dsp.Ping(b, 0.0, 95 + v * 12, 50, 0.03, 0.002, 0.07, 1f);
            Dsp.Burst(b, ref n, 0.0, 0, 450, 0.8, 0.002, 0.05, 1.2f);
            Dsp.Burst(b, ref n, 0.0, 1, 2400, 1.0, 0.0005, 0.012, 0.5f);
            Dsp.Burst(b, ref n, 0.06 + v * 0.015, 1, 2000, 1.0, 0.002, 0.02, 0.35f);
            return b;
        }

        // Body blow: low thump, a skin slap and a short fleshy smack.
        private static float[] Punch(ref Noise n, int v)
        {
            float[] b = Dsp.New(0.3);
            Dsp.Ping(b, 0.0, 120 + v * 15, 55, 0.025, 0.0015, 0.06, 1f);
            Dsp.Burst(b, ref n, 0.0, 2, 1400 + v * 300, 0.8, 0.0003, 0.012, 0.9f);
            Dsp.Burst(b, ref n, 0.002, 0, 700, 1.2, 0.002, 0.04, 0.9f);
            Dsp.Burst(b, ref n, 0.0, 1, 4000, 0.7, 0.0002, 0.003, 0.4f);
            for (int i = 0; i < b.Length; i++) b[i] = Dsp.SoftClip(b[i] * 1.6f);
            return b;
        }

        // Hollow wooden thock over a body thump.
        private static float[] BatHit(ref Noise n, int v)
        {
            float[] b = Dsp.New(0.45);
            double f = 620 + v * 70;
            Dsp.Modes(b, ref n, 0.0, new[] { f, f * 2.32, f * 3.9, f * 5.3 }, 25, 0.06, 0.9f);
            Dsp.Ping(b, 0.0, 130, 60, 0.03, 0.0015, 0.07, 0.8f);
            Dsp.Burst(b, ref n, 0.0, 2, 2000, 0.7, 0.0003, 0.008, 0.6f);
            return b;
        }

        // Hammer then sear: two tiny metallic clicks.
        private static float[] DryFire(ref Noise n)
        {
            float[] b = Dsp.New(0.16);
            Dsp.Modes(b, ref n, 0.003, new[] { 3200.0, 5150, 7400 }, 30, 0.012, 0.6f);
            Dsp.Modes(b, ref n, 0.03, new[] { 2700.0, 4300, 6900 }, 30, 0.009, 0.45f);
            Dsp.Burst(b, ref n, 0.003, 2, 6000, 0.7, 0.0002, 0.002, 0.4f);
            return b;
        }

        // Magazine out (click + slide), magazine in (thunk), slide racked (scrape + double click).
        private static float[] Reload(ref Noise n)
        {
            float[] b = Dsp.New(1.05);
            Dsp.Modes(b, ref n, 0.02, new[] { 2900.0, 4700 }, 25, 0.012, 0.5f);
            SlideNoise(b, ref n, 0.04, 0.12, 2200, 0.35f);
            Dsp.Modes(b, ref n, 0.42, new[] { 900.0, 1850, 3100 }, 20, 0.03, 0.8f);
            Dsp.Ping(b, 0.42, 160, 90, 0.02, 0.001, 0.04, 0.5f);
            SlideNoise(b, ref n, 0.74, 0.13, 3000, 0.45f);
            Dsp.Modes(b, ref n, 0.86, new[] { 2400.0, 4100, 6200 }, 25, 0.015, 0.7f);
            Dsp.Modes(b, ref n, 0.93, new[] { 2100.0, 3600, 5500 }, 25, 0.02, 0.8f);
            return b;
        }

        private static void SlideNoise(float[] b, ref Noise n, double start, double dur, double fc, float amp)
        {
            int s = Dsp.N(start), len = Dsp.N(dur);
            var f = new Svf(fc, 3);
            for (int i = 0; i < len && s + i < b.Length; i++)
            {
                double u = i / (double)len;
                f.Set(fc * (0.8 + 0.5 * u), 3);
                float grit = 0.6f + 0.4f * n.Next();
                b[s + i] += amp * (float)Math.Sin(Math.PI * u) * grit * f.Bp(n.Next());
            }
        }

        // 0: concrete chip with debris, 1: ricochet whine, 2: dull thud into soft material.
        private static float[] BulletImpact(ref Noise n, int v)
        {
            float[] b = Dsp.New(v == 1 ? 0.5 : 0.32);
            if (v == 2)
            {
                Dsp.Ping(b, 0.0, 180, 70, 0.015, 0.0008, 0.035, 1f);
                Dsp.Burst(b, ref n, 0.0, 0, 900, 1.0, 0.0005, 0.03, 1f);
                return b;
            }
            Dsp.Burst(b, ref n, 0.0, 2, 3000, 0.8, 0.0002, 0.004, 1f);
            Dsp.Burst(b, ref n, 0.001, 1, 2400, 1.5, 0.001, 0.035, 0.8f);
            for (int g = 0; g < 7; g++)
                Dsp.Burst(b, ref n, n.Range(0.01f, 0.18f), 1, n.Range(2500f, 6000f), 3.0, 0.0002, 0.004, n.Range(0.1f, 0.3f));
            if (v == 1) Dsp.Ping(b, 0.008, 3600, 1500, 0.12, 0.004, 0.09, 0.45f);
            return b;
        }
    }
}
