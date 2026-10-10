using System;

namespace GtaSkylines.Mod.Hud
{
    /// <summary>Timing curves of the HUD (all results clamped to their stated range). Unity-free.</summary>
    public static class HudAnim
    {
        /// <summary>Seconds of one evading-star pulse (white → grey → white).</summary>
        public const float StarPulsePeriod = 1f;

        /// <summary>Default fade time of banners, help and notifications, seconds.</summary>
        public const float Fade = 0.4f;

        /// <summary>
        /// Brightness 0..1 of a filled star while evading at <paramref name="timeSeconds"/>:
        /// 0.5 + 0.5·cos(2π·t / <see cref="StarPulsePeriod"/>), so 1 (white) at t = 0, 0 (grey) at half a period.
        /// </summary>
        public static float StarPulse(float timeSeconds)
        {
            return 0.5f + 0.5f * (float)Math.Cos(2.0 * Math.PI * timeSeconds / StarPulsePeriod);
        }

        /// <summary>
        /// Opacity 0..1 of something shown for <paramref name="total"/> seconds at <paramref name="age"/> seconds: ramps
        /// linearly 0→1 over the first <paramref name="fade"/> seconds, holds 1, ramps 1→0 over the last
        /// <paramref name="fade"/> seconds; 0 when age &lt; 0, age ≥ total, or age is NaN. When 2·fade &gt; total the
        /// result is the smaller of the two ramps. fade ≤ 0 means no ramps (1 inside [0, total)).
        /// </summary>
        public static float Window(float age, float total, float fade)
        {
            if (float.IsNaN(age) || age < 0f || age >= total) return 0f;
            if (fade <= 0f) return 1f;
            return Math.Min(1f, Math.Min(age / fade, (total - age) / fade));
        }

        /// <summary>
        /// Opacity 0..1 of a full-screen state from its progress 0..1: linear 0→1 over [0, 0.1], 1 until 0.85, linear
        /// 1→0 over [0.85, 1]; 0 outside [0, 1].
        /// </summary>
        public static float BigScreenAlpha(float progress)
        {
            if (!(progress >= 0f && progress <= 1f)) return 0f;
            if (progress < 0.1f) return progress / 0.1f;
            if (progress <= 0.85f) return 1f;
            return (1f - progress) / 0.15f;
        }

        /// <summary>
        /// Title scale of a full-screen state (the GTA "slam"): 1.4 at progress 0 easing (quadratic ease-out) to 1.0 at
        /// progress 0.12, then 1.0; progress below 0 gives 1.4 and above 1 gives 1.0.
        /// </summary>
        public static float BigScreenTitleScale(float progress)
        {
            if (!(progress > 0f)) return 1.4f;
            if (progress >= 0.12f) return 1f;
            float k = 1f - progress / 0.12f;
            return 1f + 0.4f * k * k;
        }

        /// <summary>Moves <paramref name="current"/> towards <paramref name="target"/> by at most rate·dt (never overshoots).</summary>
        public static float Approach(float current, float target, float rate, float dt)
        {
            float d = target - current;
            float step = rate * dt;
            if (d > step) return current + step;
            if (d < -step) return current - step;
            return target;
        }
    }
}
