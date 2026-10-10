using System;

namespace GtaSkylines.Mod.Audio
{
    internal static partial class Recipes
    {
        private struct ShotSpec
        {
            public double Length, CrackDec, CrackHp, BodyFc, BodyDec, ThumpHz, ThumpDec, TailFc, TailDec, Wet, Drive, Mech;
        }

        private static readonly ShotSpec Pistol = new ShotSpec
        {
            Length = 0.95, CrackDec = 0.006, CrackHp = 1500, BodyFc = 1200, BodyDec = 0.035, ThumpHz = 85, ThumpDec = 0.08,
            TailFc = 2200, TailDec = 0.16, Wet = 0.5, Drive = 2.2, Mech = 0.0,
        };

        private static readonly ShotSpec Smg = new ShotSpec
        {
            Length = 0.45, CrackDec = 0.004, CrackHp = 1800, BodyFc = 1700, BodyDec = 0.022, ThumpHz = 105, ThumpDec = 0.045,
            TailFc = 2600, TailDec = 0.07, Wet = 0.3, Drive = 2.6, Mech = 0.35,
        };

        private static readonly ShotSpec Shotgun = new ShotSpec
        {
            Length = 1.6, CrackDec = 0.011, CrackHp = 900, BodyFc = 650, BodyDec = 0.08, ThumpHz = 58, ThumpDec = 0.19,
            TailFc = 1500, TailDec = 0.3, Wet = 0.6, Drive = 3.2, Mech = 0.0,
        };

        private static readonly ShotSpec Carbine = new ShotSpec
        {
            Length = 1.15, CrackDec = 0.005, CrackHp = 2400, BodyFc = 1450, BodyDec = 0.03, ThumpHz = 75, ThumpDec = 0.1,
            TailFc = 2800, TailDec = 0.2, Wet = 0.55, Drive = 2.8, Mech = 0.0,
        };

        // Layered gunshot: supersonic crack, muzzle body, low thump, a closing low-passed tail, saturated, then street reverb.
        private static float[] Shot(ref Noise n, ShotSpec s)
        {
            float[] b = Dsp.New(s.Length);
            float jitter = n.Range(0.92f, 1.08f);
            Dsp.Burst(b, ref n, 0.0, 2, s.CrackHp * jitter, 0.7, 0.0001, s.CrackDec, 1.2f);
            Dsp.Burst(b, ref n, 0.0005, 1, s.BodyFc * jitter, 0.9, 0.0004, s.BodyDec, 1.4f);
            Dsp.Ping(b, 0.0, s.ThumpHz * 2.2, s.ThumpHz, 0.012, 0.0008, s.ThumpDec, 1.3f);
            var lp = new Svf(s.TailFc, 0.7);
            int len = b.Length;
            double ea = 1, ed = 0.45, ka = Math.Exp(-1.0 / (0.002 * Dsp.Sr)), kd = Math.Exp(-1.0 / (s.TailDec * Dsp.Sr));
            for (int i = 0; i < len; i++)
            {
                if ((i & 31) == 0) lp.Set(250 + (s.TailFc - 250) * Math.Exp(-i / (double)Dsp.Sr / (s.TailDec * 0.7)), 0.7);
                b[i] += (float)((1.0 - ea) * ed) * lp.Lp(n.Next());
                ea *= ka;
                ed *= kd;
            }
            if (s.Mech > 0)
                Dsp.Modes(b, ref n, 0.028, new[] { 2600.0, 4400, 6100 }, 25, 0.01, (float)s.Mech);
            float drive = (float)s.Drive;
            for (int i = 0; i < len; i++) b[i] = Dsp.SoftClip(b[i] * drive);
            Reverb.Apply(b, (float)s.Wet, 1.0f, 0.35f);
            return b;
        }

        // Blast: broadband crack, sub boom, a rumble whose cutoff closes over two seconds, falling debris, big reverb.
        private static float[] Explosion(ref Noise n)
        {
            float[] b = Dsp.New(3.2);
            Dsp.Burst(b, ref n, 0.0, 2, 600, 0.7, 0.0002, 0.02, 1.5f);
            Dsp.Ping(b, 0.0, 70, 28, 0.25, 0.004, 0.6, 1.6f);
            var lp = new Svf(4000, 0.8);
            var lp2 = new Svf(4000, 0.8);
            double ea = 1, ed = 1.4, ka = Math.Exp(-1.0 / (0.006 * Dsp.Sr)), kd = Math.Exp(-1.0 / (0.9 * Dsp.Sr));
            for (int i = 0; i < b.Length; i++)
            {
                double t = i / (double)Dsp.Sr;
                if ((i & 31) == 0)
                {
                    double fc = 140 + 3800 * Math.Exp(-t / 0.35);
                    lp.Set(fc, 0.8);
                    lp2.Set(fc, 0.8);
                }
                b[i] += (float)((1.0 - ea) * ed) * lp2.Lp(lp.Lp(n.Next()));
                ea *= ka;
                ed *= kd;
            }
            for (int g = 0; g < 40; g++)
            {
                double at = 0.25 + Math.Pow(n.Range(0f, 1f), 1.8) * 1.6;
                float amp = 0.35f * Dsp.Dec(at, 0.7);
                Dsp.Burst(b, ref n, at, 1, n.Range(1500f, 6000f), 2.0, 0.0003, n.Range(0.004f, 0.015f), amp);
            }
            for (int i = 0; i < b.Length; i++) b[i] = Dsp.SoftClip(b[i] * 1.8f);
            Reverb.Apply(b, 0.55f, 1.3f, 0.5f);
            return b;
        }

        // Car crash: body thud, crumpling metal (a train of impulses into inharmonic panel modes), glass when it is hard.
        private static float[] Crash(ref Noise n, float k)
        {
            float[] b = Dsp.New(0.7 + 1.4 * k);
            Dsp.Ping(b, 0.0, 90, 45, 0.05, 0.002, 0.12 + 0.2 * k, 1.4f);
            Dsp.Burst(b, ref n, 0.0, 0, 1200, 0.8, 0.001, 0.05 + 0.08 * k, 1.2f);
            double[] panel = { 410, 870, 1330, 2210, 3170 };
            int hits = 3 + (int)(k * 5);
            for (int h = 0; h < hits; h++)
            {
                double at = h == 0 ? 0.0 : n.Range(0f, 0.08f + 0.25f * k);
                double scale = n.Range(0.85f, 1.2f);
                var f = new double[panel.Length];
                for (int m = 0; m < f.Length; m++) f[m] = panel[m] * scale;
                Dsp.Modes(b, ref n, at, f, 22, 0.06 + 0.16 * k, (h == 0 ? 0.9f : 0.5f));
                Dsp.Burst(b, ref n, at, 1, n.Range(800f, 2500f), 1.2, 0.0005, 0.02, 0.5f);
            }
            if (k > 0.5f)
            {
                int shards = (int)(30 * k);
                for (int s = 0; s < shards; s++)
                {
                    double at = 0.03 + Math.Pow(n.Range(0f, 1f), 1.5) * 0.7 * k;
                    Dsp.Ping(b, at, n.Range(3500f, 8000f), n.Range(3500f, 8000f), 1, 0.0002, n.Range(0.01f, 0.04f), n.Range(0.08f, 0.22f));
                }
                Dsp.Burst(b, ref n, 0.02, 2, 4000, 0.7, 0.001, 0.12, 0.6f);
            }
            for (int i = 0; i < b.Length; i++) b[i] = Dsp.SoftClip(b[i] * 1.5f);
            Reverb.Apply(b, 0.3f, 0.9f, 0.4f);
            return b;
        }
    }
}
