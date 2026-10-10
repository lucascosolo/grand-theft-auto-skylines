using System;

namespace GtaSkylines.Game.Mathx
{
    /// <summary>Scalar helpers.</summary>
    public static class MathUtil
    {
        /// <summary>Degrees to radians.</summary>
        public const double Deg2Rad = Math.PI / 180.0;

        /// <summary>Radians to degrees.</summary>
        public const double Rad2Deg = 180.0 / Math.PI;

        /// <summary>Clamps to [min, max].</summary>
        public static float Clamp(float v, float min, float max) { return v < min ? min : v > max ? max : v; }

        /// <summary>Clamps to [min, max].</summary>
        public static int Clamp(int v, int min, int max) { return v < min ? min : v > max ? max : v; }

        /// <summary>Clamps to [0, 1].</summary>
        public static float Clamp01(float v) { return v < 0f ? 0f : v > 1f ? 1f : v; }

        /// <summary>Linear interpolation, t not clamped.</summary>
        public static float Lerp(float a, float b, float t) { return a + (b - a) * t; }

        /// <summary>Where <paramref name="v"/> lies between a and b as a fraction, clamped to [0, 1]; 0 when a == b.</summary>
        public static float InverseLerp(float a, float b, float v) { return a == b ? 0f : Clamp01((v - a) / (b - a)); }

        /// <summary>Moves <paramref name="current"/> toward <paramref name="target"/> by at most <paramref name="maxDelta"/>.</summary>
        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta) return target;
            return current + Math.Sign(target - current) * maxDelta;
        }

        /// <summary>Wraps an angle in degrees into (-180, 180].</summary>
        public static float WrapDeg(float a)
        {
            a %= 360f;
            if (a > 180f) a -= 360f;
            else if (a <= -180f) a += 360f;
            return a;
        }

        /// <summary>Shortest signed difference b − a in degrees, in (-180, 180].</summary>
        public static float DeltaAngleDeg(float a, float b) { return WrapDeg(b - a); }

        /// <summary>Moves an angle in degrees toward a target along the shorter way by at most <paramref name="maxDelta"/>.</summary>
        public static float MoveTowardsAngleDeg(float current, float target, float maxDelta)
        {
            float d = DeltaAngleDeg(current, target);
            if (Math.Abs(d) <= maxDelta) return WrapDeg(target);
            return WrapDeg(current + Math.Sign(d) * maxDelta);
        }

        /// <summary>
        /// Frame-rate independent exponential approach: the fraction of the remaining distance to cover in
        /// <paramref name="dt"/> seconds when half of it is covered every <paramref name="halfLifeSeconds"/>.
        /// </summary>
        public static float ApproachFactor(float halfLifeSeconds, float dt)
        {
            if (halfLifeSeconds <= 0f) return 1f;
            return 1f - (float)Math.Pow(0.5, dt / halfLifeSeconds);
        }

        /// <summary>Smooth Hermite step between 0 and 1.</summary>
        public static float SmoothStep01(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
