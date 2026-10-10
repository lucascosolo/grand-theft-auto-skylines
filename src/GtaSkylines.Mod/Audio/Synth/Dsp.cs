using System;

namespace GtaSkylines.Mod.Audio
{
    /// <summary>Deterministic xorshift32 noise in [-1, 1).</summary>
    internal struct Noise
    {
        private uint _s;

        public Noise(uint seed) { _s = seed == 0 ? 0x9E3779B9u : seed * 2654435761u + 1u; }

        public float Next()
        {
            _s ^= _s << 13;
            _s ^= _s >> 17;
            _s ^= _s << 5;
            return (_s & 0xFFFFFF) / 8388608f - 1f;
        }

        /// <summary>Uniform in [lo, hi).</summary>
        public float Range(float lo, float hi) { return lo + (Next() + 1f) * 0.5f * (hi - lo); }
    }

    /// <summary>Topology-preserving state-variable filter (stable at any cutoff, cheap to retune).</summary>
    internal struct Svf
    {
        private double _g, _k, _a1, _a2, _a3, _ic1, _ic2;
        public double Low, Band, High;

        public Svf(double cutoffHz, double q) : this() { Set(cutoffHz, q); }

        public void Set(double cutoffHz, double q)
        {
            double fc = cutoffHz < 10 ? 10 : (cutoffHz > 20000 ? 20000 : cutoffHz);
            _g = Math.Tan(Math.PI * fc / SoundBank.SampleRate);
            _k = 1.0 / q;
            _a1 = 1.0 / (1.0 + _g * (_g + _k));
            _a2 = _g * _a1;
            _a3 = _g * _a2;
        }

        public void Process(double x)
        {
            double v3 = x - _ic2;
            double v1 = _a1 * _ic1 + _a2 * v3;
            double v2 = _ic2 + _a2 * _ic1 + _a3 * v3;
            _ic1 = 2 * v1 - _ic1;
            _ic2 = 2 * v2 - _ic2;
            Low = v2;
            Band = v1;
            High = x - _k * v1 - v2;
        }

        public float Lp(double x) { Process(x); return (float)Low; }
        public float Bp(double x) { Process(x); return (float)(Band * _k); }
        public float Hp(double x) { Process(x); return (float)High; }
    }

    /// <summary>Small Schroeder reverb (four damped combs, two allpasses): the street echo behind shots and blasts.</summary>
    internal sealed class Reverb
    {
        private static readonly int[] CombMs10 = { 297, 371, 411, 437 };
        private readonly float[][] _comb = new float[4][];
        private readonly int[] _ci = new int[4];
        private readonly float[] _damp = new float[4];
        private readonly float[] _ap1 = new float[221], _ap2 = new float[75];
        private int _a1, _a2;
        private readonly float _fb, _d;

        public Reverb(float size, float damping)
        {
            for (int i = 0; i < 4; i++) _comb[i] = new float[(int)(CombMs10[i] * size * SoundBank.SampleRate / 10000f) + 1];
            _fb = 0.70f + 0.14f * size;
            _d = damping;
        }

        public float Process(float x)
        {
            float sum = 0f;
            for (int i = 0; i < 4; i++)
            {
                float[] c = _comb[i];
                float y = c[_ci[i]];
                _damp[i] = y * (1f - _d) + _damp[i] * _d;
                c[_ci[i]] = x + _damp[i] * _fb;
                if (++_ci[i] == c.Length) _ci[i] = 0;
                sum += y;
            }
            sum *= 0.25f;
            sum = AllPass(_ap1, ref _a1, sum);
            return AllPass(_ap2, ref _a2, sum);
        }

        private static float AllPass(float[] buf, ref int idx, float x)
        {
            float b = buf[idx];
            float y = -x + b;
            buf[idx] = x + b * 0.5f;
            if (++idx == buf.Length) idx = 0;
            return y;
        }

        /// <summary>Adds <paramref name="wet"/> of the reverb of the whole buffer onto it.</summary>
        public static void Apply(float[] b, float wet, float size, float damping)
        {
            var r = new Reverb(size, damping);
            for (int i = 0; i < b.Length; i++) b[i] += wet * r.Process(b[i]);
        }
    }

    /// <summary>Buffer helpers shared by the recipes.</summary>
    internal static class Dsp
    {
        public const double TwoPi = Math.PI * 2.0;
        public const float Sr = SoundBank.SampleRate;

        public static float[] New(double seconds) { return new float[(int)(seconds * SoundBank.SampleRate)]; }

        public static int N(double seconds) { return (int)(seconds * SoundBank.SampleRate); }

        /// <summary>Attack-decay envelope: rises with time constant <paramref name="atk"/>, falls with <paramref name="dec"/>.</summary>
        public static float Ad(double t, double atk, double dec)
        {
            if (t < 0) return 0f;
            return (float)((1.0 - Math.Exp(-t / atk)) * Math.Exp(-t / dec));
        }

        public static float Dec(double t, double tau) { return t < 0 ? 0f : (float)Math.Exp(-t / tau); }

        /// <summary>Tanh-like saturation (Padé approximant, exact ±1 beyond ±3).</summary>
        public static float SoftClip(float x)
        {
            if (x >= 3f) return 1f;
            if (x <= -3f) return -1f;
            float x2 = x * x;
            return x * (27f + x2) / (27f + 9f * x2);
        }

        private const int SinSize = 4096;
        private static readonly float[] s_sin = BuildSin();

        private static float[] BuildSin()
        {
            var t = new float[SinSize + 1];
            for (int i = 0; i <= SinSize; i++) t[i] = (float)Math.Sin(TwoPi * i / SinSize);
            return t;
        }

        /// <summary>Table sine of a phase in cycles (any real value), linearly interpolated.</summary>
        public static float Sin(double cycles)
        {
            double p = (cycles - Math.Floor(cycles)) * SinSize;
            int i = (int)p;
            float f = (float)(p - i);
            return s_sin[i] + (s_sin[i + 1] - s_sin[i]) * f;
        }

        /// <summary>PolyBLEP correction for a discontinuity at phase 0 (phase in cycles, dt = f / sr).</summary>
        public static double Blep(double p, double dt)
        {
            if (p < dt) { p /= dt; return p + p - p * p - 1.0; }
            if (p > 1.0 - dt) { p = (p - 1.0) / dt; return p * p + p + p + 1.0; }
            return 0.0;
        }

        /// <summary>Band-limited saw; <paramref name="p"/> phase in cycles [0,1).</summary>
        public static double Saw(double p, double dt) { return 2.0 * p - 1.0 - Blep(p, dt); }

        /// <summary>Band-limited square.</summary>
        public static double Square(double p, double dt)
        {
            double q = p + 0.5;
            if (q >= 1.0) q -= 1.0;
            return (p < 0.5 ? 1.0 : -1.0) + Blep(p, dt) - Blep(q, dt);
        }

        public static double Wrap(double p) { return p - Math.Floor(p); }

        /// <summary>Adds a sine ping (frequency glides exponentially from f0 to f1) starting at <paramref name="start"/> seconds.</summary>
        public static void Ping(float[] b, double start, double f0, double f1, double glideTau, double atk, double dec, float amp)
        {
            int s = N(start);
            int len = Math.Min(b.Length - s, N(dec * 8 + atk));
            double ph = 0, ea = 1, ed = amp, eg = f0 - f1;
            double ka = Math.Exp(-1.0 / (atk * Sr)), kd = Math.Exp(-1.0 / (dec * Sr)), kg = Math.Exp(-1.0 / (glideTau * Sr));
            for (int i = 0; i < len; i++)
            {
                ph += (f1 + eg) / Sr;
                b[s + i] += (float)((1.0 - ea) * ed) * Sin(ph);
                ea *= ka;
                ed *= kd;
                eg *= kg;
            }
        }

        /// <summary>Adds a filtered noise burst. mode 0 low-pass, 1 band-pass, 2 high-pass.</summary>
        public static void Burst(float[] b, ref Noise n, double start, int mode, double fc, double q, double atk, double dec, float amp)
        {
            int s = N(start);
            int len = Math.Min(b.Length - s, N(dec * 7 + atk));
            var f = new Svf(fc, q);
            double ea = 1, ed = amp;
            double ka = Math.Exp(-1.0 / (atk * Sr)), kd = Math.Exp(-1.0 / (dec * Sr));
            for (int i = 0; i < len; i++)
            {
                float x = n.Next();
                float y = mode == 0 ? f.Lp(x) : (mode == 1 ? f.Bp(x) : f.Hp(x));
                b[s + i] += (float)((1.0 - ea) * ed) * y;
                ea *= ka;
                ed *= kd;
            }
        }

        /// <summary>Adds a struck resonator bank (modal synthesis): an impulse into band-passes at the given frequencies.</summary>
        public static void Modes(float[] b, ref Noise n, double start, double[] freqs, double q, double dec, float amp)
        {
            int s = N(start);
            for (int m = 0; m < freqs.Length; m++)
            {
                double md = dec / (1.0 + m * 0.3);
                int len = Math.Min(b.Length - s, N(md * 5.5));
                var f = new Svf(freqs[m], q);
                double e = amp * 4.0 / (1.0 + m * 0.6), k = Math.Exp(-1.0 / (md * Sr));
                for (int i = 0; i < len; i++)
                {
                    float x = i < 64 ? n.Next() * (1f - i / 64f) : 0f;
                    b[s + i] += (float)e * f.Bp(x);
                    e *= k;
                }
            }
        }

        /// <summary>Linear fade from silence at sample 0 and to silence at the last sample.</summary>
        public static void FadeEdges(float[] b, double inSec, double outSec)
        {
            int fi = Math.Max(1, N(inSec)), fo = Math.Max(1, N(outSec));
            for (int i = 0; i < fi && i < b.Length; i++) b[i] *= i / (float)fi;
            for (int i = 0; i < fo && i < b.Length; i++) b[b.Length - 1 - i] *= i / (float)fo;
        }

        /// <summary>Scales the buffer so its peak sits exactly at <see cref="SoundBank.PeakLimit"/>.</summary>
        public static float[] Normalize(float[] b)
        {
            float peak = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float a = Math.Abs(b[i]);
                if (a > peak) peak = a;
            }
            if (peak <= 0f) return b;
            float g = SoundBank.PeakLimit / peak;
            for (int i = 0; i < b.Length; i++)
            {
                float v = b[i] * g;
                b[i] = v > SoundBank.PeakLimit ? SoundBank.PeakLimit : (v < -SoundBank.PeakLimit ? -SoundBank.PeakLimit : v);
            }
            return b;
        }

        /// <summary>Removes DC (one-pole high-pass at ~20 Hz); loops must not drift.</summary>
        public static void DcBlock(float[] b)
        {
            double x1 = 0, y1 = 0;
            for (int i = 0; i < b.Length; i++)
            {
                double y = b[i] - x1 + 0.9972 * y1;
                x1 = b[i];
                y1 = y;
                b[i] = (float)y;
            }
        }

        /// <summary>
        /// Turns <paramref name="raw"/> (loop length plus <paramref name="fadeSec"/>) into a seamless loop: the overhang is
        /// equal-power cross-faded into the head, so the last output sample runs straight into the first.
        /// </summary>
        public static float[] CrossfadeLoop(float[] raw, double fadeSec)
        {
            int f = N(fadeSec), len = raw.Length - f;
            var o = new float[len];
            Array.Copy(raw, o, len);
            for (int i = 0; i < f; i++)
            {
                double w = (i + 0.5) / f * Math.PI * 0.5;
                o[i] = (float)(raw[i] * Math.Sin(w) + raw[len + i] * Math.Cos(w));
            }
            return o;
        }

        /// <summary>Copies the second half of a two-period render: every filter has settled into the periodic state.</summary>
        public static float[] SecondPeriod(float[] twoPeriods)
        {
            int len = twoPeriods.Length / 2;
            var o = new float[len];
            Array.Copy(twoPeriods, len, o, 0, len);
            return o;
        }

        public static double MidiHz(double note) { return 440.0 * Math.Pow(2.0, (note - 69.0) / 12.0); }
    }
}
