using System;

namespace GtaSkylines.Mod.Audio
{
    // Tonal loops are built exactly periodic (every oscillator completes a whole number of cycles in the loop, noise reads
    // a cyclic table) and rendered over two periods; the second period is kept, so filter state is settled and the seam is
    // sample-continuous. Pure-noise loops (screech, room tone) are cross-faded instead.
    internal static partial class Recipes
    {
        /// <summary>Scales a per-sample frequency curve so that it completes a whole number of cycles.</summary>
        private static void WholeCycles(double[] f)
        {
            double c = 0;
            for (int i = 0; i < f.Length; i++) c += f[i];
            c /= Dsp.Sr;
            double k = Math.Max(1.0, Math.Round(c)) / c;
            for (int i = 0; i < f.Length; i++) f[i] *= k;
        }

        private static float[] Horn(bool loop)
        {
            int len = loop ? Dsp.N(1.0) : Dsp.N(0.45);
            int total = loop ? len * 2 : len;
            var b = new float[total];
            double p1 = 0, p2 = 0;
            const double f1 = 370, f2 = 466;
            var bp = new Svf(1900, 1.3);
            var lp = new Svf(3800, 0.7);
            for (int i = 0; i < total; i++)
            {
                double t = i / (double)Dsp.Sr;
                double x = Dsp.Square(p1, f1 / Dsp.Sr) + 0.85 * Dsp.Square(p2, f2 / Dsp.Sr);
                p1 = Dsp.Wrap(p1 + f1 / Dsp.Sr);
                p2 = Dsp.Wrap(p2 + f2 / Dsp.Sr);
                float y = lp.Lp(0.35 * x + 0.9 * bp.Bp(x));
                float env = loop ? 1f : (float)(1.0 - Math.Exp(-t / 0.008));
                b[i] = Dsp.SoftClip(1.8f * y) * env;
            }
            if (!loop) return b;
            Dsp.DcBlock(b);
            return Dsp.SecondPeriod(b);
        }

        // Electronic siren: band-limited square/saw blend swept between 650 and 1450 Hz, through a horn-speaker band.
        private static float[] SirenSweep(double periodSec, int cycles, double rise)
        {
            int len = Dsp.N(periodSec);
            var f = new double[len];
            for (int i = 0; i < len; i++)
            {
                double u = Dsp.Wrap(i / (double)len * cycles);
                double s = u < rise
                    ? 1.0 - (1.0 - u / rise) * (1.0 - u / rise)
                    : 1.0 - Math.Pow((u - rise) / (1.0 - rise), 2.0);
                f[i] = 650 + 800 * s;
            }
            return RenderTone(f, 0.55, 0.45);
        }

        // Ambulance hi-lo: 960 / 770 Hz, half a second each, 8 ms raised-cosine glide.
        private static float[] SirenHiLo()
        {
            int len = Dsp.N(1.0), half = len / 2, glide = Dsp.N(0.008);
            var f = new double[len];
            for (int i = 0; i < len; i++)
            {
                int j = i % half;
                bool hi = i < half;
                double from = hi ? 770 : 960, to = hi ? 960 : 770;
                double w = j < glide ? 0.5 - 0.5 * Math.Cos(Math.PI * j / glide) : 1.0;
                f[i] = from + (to - from) * w;
            }
            return RenderTone(f, 0.3, 0.7);
        }

        private static float[] RenderTone(double[] f, double squareMix, double sineMix)
        {
            WholeCycles(f);
            int len = f.Length;
            var b = new float[len * 2];
            double p = 0;
            var hp = new Svf(450, 0.7);
            var bp = new Svf(1800, 0.9);
            for (int i = 0; i < b.Length; i++)
            {
                double fr = f[i % len], dt = fr / Dsp.Sr;
                double x = squareMix * Dsp.Square(p, dt) + (1 - squareMix) * Dsp.Saw(p, dt) + sineMix * Dsp.Sin(p);
                p = Dsp.Wrap(p + dt);
                float y = hp.Hp(x);
                y = 0.6f * y + bp.Bp(y);
                b[i] = Dsp.SoftClip(1.5f * y);
            }
            Dsp.DcBlock(b);
            return Dsp.SecondPeriod(b);
        }

        // Air horn: two brassy saw voices a third apart, gentle vibrato, resonant body, driven.
        private static float[] FireHorn()
        {
            int len = Dsp.N(1.0);
            var f1 = new double[len];
            var f2 = new double[len];
            for (int i = 0; i < len; i++)
            {
                double vib = 1.0 + 0.006 * Dsp.Sin(5.0 * i / len);
                f1[i] = 233 * vib;
                f2[i] = 294 * vib;
            }
            WholeCycles(f1);
            WholeCycles(f2);
            var b = new float[len * 2];
            double p1 = 0, p2 = 0;
            var lp = new Svf(1300, 2.0);
            var formant = new Svf(720, 2.5);
            for (int i = 0; i < b.Length; i++)
            {
                double d1 = f1[i % len] / Dsp.Sr, d2 = f2[i % len] / Dsp.Sr;
                double x = Dsp.Saw(p1, d1) + 0.9 * Dsp.Saw(p2, d2);
                p1 = Dsp.Wrap(p1 + d1);
                p2 = Dsp.Wrap(p2 + d2);
                float y = lp.Lp(x);
                y += 0.8f * formant.Bp(x);
                b[i] = Dsp.SoftClip(2.2f * y);
            }
            Dsp.DcBlock(b);
            return Dsp.SecondPeriod(b);
        }

        // Four-cylinder engine at a 40 Hz firing rate: decaying exhaust pulses with uneven cylinders and cyclic hiss,
        // through a resonant pipe and a low body formant, plus the firing harmonics.
        private static float[] Engine(ref Noise n)
        {
            const double fire = 40.0;
            int len = Dsp.N(1.0);
            var table = new float[len];
            for (int i = 0; i < len; i++) table[i] = n.Next();
            float[] cyl = { 1f, 0.82f, 0.95f, 0.78f };
            var b = new float[len * 2];
            var pipe = new Svf(320, 3.0);
            var body = new Svf(110, 2.0);
            var hiss = new Svf(1500, 0.8);
            for (int i = 0; i < b.Length; i++)
            {
                double t = (i % len) / (double)Dsp.Sr;
                double pos = t * fire + 0.5; // pulses fall mid-period, so the seam sits in a decay, not on an onset
                int k = (int)Math.Floor(pos);
                double tp = (pos - k) / fire;
                float ex = cyl[k & 3] * (float)Math.Exp(-tp / 0.0045);
                float x = ex * (0.7f + 0.3f * table[i % len]);
                float y = pipe.Lp(x) * 0.9f + body.Bp(x) * 1.4f + hiss.Bp(table[(i * 7) % len]) * ex * 0.35f;
                y += 0.15f * (float)(Dsp.Sin(fire * t) + 0.5 * Dsp.Sin(fire * 0.5 * t));
                b[i] = Dsp.SoftClip(2.5f * y);
            }
            Dsp.DcBlock(b);
            return Dsp.SecondPeriod(b);
        }

        // Rubber stick-slip: a wandering squeal tone with chaotic FM and flutter, over band-passed hiss. Cross-faded loop.
        private static float[] Screech(ref Noise n)
        {
            const double loopSec = 1.5, fade = 0.12;
            var b = Dsp.New(loopSec + fade);
            var wander = new Svf(6, 0.7);
            var flutter = new Svf(35, 0.9);
            var hiss = new Svf(2600, 1.5);
            var tone = new Svf(1100, 6.0);
            double p = 0, p2 = 0;
            for (int i = 0; i < b.Length; i++)
            {
                double fm = wander.Lp(n.Next()) * 2500;
                double f = 1150 + fm;
                p = Dsp.Wrap(p + f / Dsp.Sr);
                p2 = Dsp.Wrap(p2 + 2.02 * f / Dsp.Sr);
                double am = 0.65 + 6.0 * flutter.Lp(n.Next());
                am = am < 0 ? 0 : am;
                double sq = Dsp.Sin(p) + 0.35 * Dsp.Sin(p2);
                float y = (float)(am * sq) * 0.6f + tone.Bp(n.Next()) * 0.5f + hiss.Bp(n.Next()) * 0.4f;
                b[i] = Dsp.SoftClip(1.4f * y);
            }
            Dsp.DcBlock(b);
            return Dsp.CrossfadeLoop(b, fade);
        }

        // Interior: low air rumble, a faint 60 Hz mains hum with harmonics, ventilation hiss. Cross-faded loop.
        private static float[] RoomTone(ref Noise n)
        {
            const double loopSec = 2.0, fade = 0.2;
            var b = Dsp.New(loopSec + fade);
            var rumble = new Svf(140, 0.7);
            var rumble2 = new Svf(140, 0.7);
            var vent = new Svf(520, 0.6);
            for (int i = 0; i < b.Length; i++)
            {
                double t = i / (double)Dsp.Sr;
                float hum = (float)(0.05 * Dsp.Sin(60 * t) + 0.035 * Dsp.Sin(120 * t) + 0.015 * Dsp.Sin(180 * t));
                b[i] = rumble2.Lp(rumble.Lp(n.Next())) * 1.2f + vent.Bp(n.Next()) * 0.12f + hum;
            }
            Dsp.DcBlock(b);
            return Dsp.CrossfadeLoop(b, fade);
        }
    }
}
