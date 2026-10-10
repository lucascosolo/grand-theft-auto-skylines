using System;

namespace GtaSkylines.Mod.Audio
{
    internal static partial class Recipes
    {
        private const int Blip = 0, Pluck = 1, Bell = 2, Brass = 3;

        private static float[] Ui(SoundId id, ref Noise n)
        {
            switch (id)
            {
                case SoundId.UiMove: return UiMove(ref n);
                case SoundId.UiSelect: return Seq(0.24, Pluck, 0.06, 0.11, 88, 95);
                case SoundId.UiBack: return Seq(0.24, Pluck, 0.06, 0.11, 83, 76);
                case SoundId.UiPurchase: return Purchase(ref n);
                case SoundId.UiCash: return Cash(ref n);
                case SoundId.PhoneRing: return PhoneRing();
                case SoundId.TextTone: return Seq(0.5, Bell, 0.13, 0.3, 100, 100);
                case SoundId.MissionPassed: return MissionPassed(ref n);
                case SoundId.MissionFailed: return MissionFailed();
                case SoundId.Wasted: return Wasted(ref n);
                case SoundId.Busted: return Busted(ref n);
                case SoundId.WantedUp: return WantedUp();
                default: throw new ArgumentOutOfRangeException("id");
            }
        }

        /// <summary>
        /// Adds one synth note. Blip: sine with a fast decay. Pluck: FM 1:2 with a falling index. Bell: inharmonic FM 1:3.5.
        /// Brass: FM 1:1 whose index follows the envelope (bright attack), held for <paramref name="dur"/> then released.
        /// </summary>
        private static void Note(float[] b, double start, double midi, double dur, int kind, float amp)
        {
            double f = Dsp.MidiHz(midi);
            double ratio = kind == Bell ? 3.5 : (kind == Pluck ? 2.0 : 1.0);
            double release = kind == Brass ? 0.12 : 0.0;
            int s = Dsp.N(start), len = Math.Min(b.Length - s, Dsp.N(dur + release * 6));
            double pc = 0, pm = 0, dc = f / Dsp.Sr, dm = f * ratio / Dsp.Sr;
            double atk = kind == Brass ? 0.025 : (kind == Bell ? 0.0008 : 0.001);
            double dec = kind == Blip ? dur * 0.3 : (kind == Pluck ? dur * 0.35 : (kind == Bell ? dur * 0.4 : 0.15));
            double itau = kind == Pluck ? 0.04 : 0.25;
            double ea = 1, ed = 1, ei = 1, er = 1;
            double ka = Math.Exp(-1.0 / (atk * Dsp.Sr)), kd = Math.Exp(-1.0 / (dec * Dsp.Sr)), ki = Math.Exp(-1.0 / (itau * Dsp.Sr));
            double kr = release > 0 ? Math.Exp(-1.0 / (release * Dsp.Sr)) : 1.0;
            int hold = Dsp.N(dur), ramp = Dsp.N(atk);
            for (int i = 0; i < len; i++)
            {
                double env, index;
                if (kind == Brass)
                {
                    env = Math.Min(1.0, i / (double)ramp) * (0.8 + 0.2 * ed);
                    if (i > hold) { er *= kr; env *= er; }
                    index = 0.6 + 2.2 * env;
                }
                else
                {
                    env = (1.0 - ea) * ed;
                    index = kind == Blip ? 0.0 : (kind == Pluck ? 2.5 : 3.0) * ei;
                }
                ea *= ka;
                ed *= kd;
                ei *= ki;
                pm += dm;
                pc += dc;
                if (pm >= 1.0) pm -= 1.0;
                if (pc >= 1.0) pc -= 1.0;
                b[s + i] += amp * (float)env * Dsp.Sin(pc + index / Dsp.TwoPi * Dsp.Sin(pm));
            }
        }

        private static void Chord(float[] b, double start, double dur, int kind, float amp, params double[] notes)
        {
            for (int i = 0; i < notes.Length; i++) Note(b, start, notes[i], dur, kind, amp);
        }

        private static float[] Seq(double length, int kind, double gap, double dur, double a, double c)
        {
            float[] b = Dsp.New(length);
            Note(b, 0.0, a, dur, kind, 1f);
            Note(b, gap, c, dur, kind, 0.9f);
            return b;
        }

        private static void Kick(float[] b, double t, float amp) { Dsp.Ping(b, t, 150, 48, 0.03, 0.001, 0.14, amp); }

        private static void Snare(float[] b, ref Noise n, double t, float amp)
        {
            Dsp.Burst(b, ref n, t, 1, 2600, 0.8, 0.0005, 0.07, amp);
            Dsp.Ping(b, t, 210, 180, 0.02, 0.0005, 0.04, amp * 0.6f);
        }

        private static void Cymbal(float[] b, ref Noise n, double t, double dec, float amp)
        {
            Dsp.Burst(b, ref n, t, 2, 5500, 0.6, 0.001, dec, amp);
        }

        private static float[] UiMove(ref Noise n)
        {
            float[] b = Dsp.New(0.09);
            Note(b, 0.0, 93, 0.06, Blip, 1f);
            Dsp.Burst(b, ref n, 0.0, 2, 6000, 0.7, 0.0002, 0.002, 0.3f);
            return b;
        }

        // Till: drawer clunk, two-bell "cha-ching", coins settling.
        private static float[] Purchase(ref Noise n)
        {
            float[] b = Dsp.New(0.85);
            Dsp.Modes(b, ref n, 0.0, new[] { 520.0, 1240, 2650 }, 15, 0.03, 0.6f);
            Chord(b, 0.07, 0.5, Bell, 0.55f, 96, 100);
            Chord(b, 0.17, 0.6, Bell, 0.6f, 100, 103);
            for (int i = 0; i < 9; i++)
                Dsp.Ping(b, n.Range(0.15f, 0.55f), n.Range(4200f, 7200f), n.Range(4200f, 7200f), 1, 0.0002, 0.025, n.Range(0.08f, 0.2f));
            return b;
        }

        private static float[] Cash(ref Noise n)
        {
            float[] b = Dsp.New(0.6);
            Note(b, 0.0, 91, 0.35, Bell, 0.6f);
            Note(b, 0.06, 98, 0.35, Bell, 0.5f);
            for (int i = 0; i < 10; i++)
                Dsp.Ping(b, n.Range(0.02f, 0.38f), n.Range(4000f, 7500f), n.Range(4000f, 7500f), 1, 0.0002, n.Range(0.015f, 0.04f), n.Range(0.1f, 0.25f));
            return b;
        }

        // A bright two-bar ringtone phrase followed by a rest; repeated by the caller while ringing.
        private static float[] PhoneRing()
        {
            float[] b = Dsp.New(2.0);
            double[] tune = { 88, 91, 96, 91, 88, 91, 96, 100, 98, 95, 91, 95, 98, 95, 91, 88 };
            for (int i = 0; i < tune.Length; i++) Note(b, i * 0.075, tune[i], 0.12, Pluck, 0.8f);
            return b;
        }

        // Fanfare in C: three pickup stabs, IV - V - I with bass, kick, snare roll and a cymbal on the resolution.
        private static float[] MissionPassed(ref Noise n)
        {
            float[] b = Dsp.New(3.4);
            Note(b, 0.00, 72, 0.1, Brass, 0.5f);
            Note(b, 0.15, 76, 0.1, Brass, 0.5f);
            Note(b, 0.30, 79, 0.1, Brass, 0.55f);
            Chord(b, 0.45, 0.25, Brass, 0.4f, 65, 69, 72, 77);
            Note(b, 0.45, 41, 0.25, Brass, 0.6f);
            Chord(b, 0.75, 0.25, Brass, 0.4f, 67, 71, 74, 79);
            Note(b, 0.75, 43, 0.25, Brass, 0.6f);
            Chord(b, 1.05, 1.5, Brass, 0.42f, 72, 76, 79, 84);
            Note(b, 1.05, 36, 1.5, Brass, 0.7f);
            Chord(b, 1.1, 1.6, Bell, 0.18f, 96, 100, 103);
            Kick(b, 0.45, 0.8f);
            Kick(b, 0.75, 0.8f);
            Kick(b, 1.05, 1f);
            for (int i = 0; i < 4; i++) Snare(b, ref n, 0.9 + i * 0.0375, 0.25f + 0.1f * i);
            Cymbal(b, ref n, 1.05, 0.5, 0.35f);
            Reverb.Apply(b, 0.35f, 1.0f, 0.4f);
            return b;
        }

        // Sad brass: a chromatic fall to a long low note.
        private static float[] MissionFailed()
        {
            float[] b = Dsp.New(2.6);
            Note(b, 0.0, 67, 0.32, Brass, 0.6f);
            Note(b, 0.4, 66, 0.32, Brass, 0.6f);
            Note(b, 0.8, 65, 0.32, Brass, 0.6f);
            Note(b, 1.2, 64, 0.9, Brass, 0.65f);
            Note(b, 1.2, 40, 0.9, Brass, 0.5f);
            var lp = new Svf(2500, 1.5);
            for (int i = 0; i < b.Length; i++)
            {
                double t = i / (double)Dsp.Sr;
                if ((i & 31) == 0) lp.Set(700 + 600 * Math.Sin(Dsp.TwoPi * 2.5 * t) * Math.Sin(Dsp.TwoPi * 2.5 * t) + 400, 1.5);
                b[i] = lp.Lp(b[i]);
            }
            Reverb.Apply(b, 0.3f, 1.0f, 0.4f);
            return b;
        }

        // Wasted: sub boom and a bell hit, then a dark A-minor saw pad whose filter closes as it fades.
        private static float[] Wasted(ref Noise n)
        {
            float[] b = Dsp.New(3.4);
            double[] notes = { 45, 48, 52, 57 };
            var p = new double[notes.Length * 2];
            var dt = new double[p.Length];
            for (int k = 0; k < p.Length; k++) dt[k] = Dsp.MidiHz(notes[k >> 1]) * (k % 2 == 0 ? 1.0 : 1.006) / Dsp.Sr;
            var lp = new Svf(2000, 1.2);
            double ea = 1, ed = 0.18, ka = Math.Exp(-1.0 / (0.15 * Dsp.Sr)), kd = Math.Exp(-1.0 / (1.4 * Dsp.Sr));
            for (int i = 0; i < b.Length; i++)
            {
                double t = i / (double)Dsp.Sr;
                double x = 0;
                for (int k = 0; k < p.Length; k++)
                {
                    x += Dsp.Saw(p[k], dt[k]);
                    p[k] += dt[k];
                    if (p[k] >= 1.0) p[k] -= 1.0;
                }
                if ((i & 31) == 0) lp.Set(180 + 2200 * Math.Exp(-t / 0.9), 1.2);
                b[i] = (float)((1.0 - ea) * ed) * lp.Lp(x);
                ea *= ka;
                ed *= kd;
            }
            Dsp.Ping(b, 0.0, 80, 32, 0.15, 0.003, 0.5, 1.2f);
            Note(b, 0.0, 81, 1.5, Bell, 0.4f);
            Note(b, 0.0, 69, 1.5, Bell, 0.3f);
            Cymbal(b, ref n, 0.0, 0.3, 0.2f);
            Reverb.Apply(b, 0.55f, 1.3f, 0.5f);
            return b;
        }

        // Busted: two low stabs, a falling siren whoop, then a D-minor hit with snare.
        private static float[] Busted(ref Noise n)
        {
            float[] b = Dsp.New(2.5);
            Chord(b, 0.0, 0.14, Brass, 0.5f, 50, 57);
            Chord(b, 0.25, 0.14, Brass, 0.5f, 50, 57);
            Kick(b, 0.0, 0.8f);
            Kick(b, 0.25, 0.8f);
            Dsp.Ping(b, 0.5, 1300, 480, 0.25, 0.01, 0.25, 0.35f);
            Chord(b, 1.0, 0.9, Brass, 0.4f, 62, 65, 69, 74);
            Note(b, 1.0, 38, 0.9, Brass, 0.6f);
            Kick(b, 1.0, 1f);
            Snare(b, ref n, 1.0, 0.6f);
            Reverb.Apply(b, 0.35f, 1.0f, 0.4f);
            return b;
        }

        private static float[] WantedUp()
        {
            float[] b = Dsp.New(0.6);
            Note(b, 0.0, 84, 0.07, Pluck, 0.7f);
            Note(b, 0.08, 88, 0.07, Pluck, 0.7f);
            Note(b, 0.16, 91, 0.2, Pluck, 0.8f);
            Dsp.Ping(b, 0.0, 110, 50, 0.03, 0.001, 0.1, 0.8f);
            return b;
        }
    }
}
