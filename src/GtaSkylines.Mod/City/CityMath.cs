using System;

namespace GtaSkylines.Mod.City
{
    /// <summary>CS1 unit conversions (pure; no Unity, no CS1). Sources in docs/CS1-API-NOTES.md (M2).</summary>
    public static class CityMath
    {
        /// <summary>CS1 speed unit 1.0 = 50 km/h.</summary>
        public const float Cs1SpeedUnitMs = 50f / 3.6f;

        /// <summary>Simulation frames per sun cycle (<c>SimulationManager.DAYTIME_FRAMES</c>).</summary>
        public const uint DayFrames = 65536u;

        private const float MaxAdvanceHours = 720f;

        // CS1 has no years formula: Citizen.GetAgeGroup bounds (15, 45, 90, 180 units) are mapped to 13, 20, 30, 65 years.
        private static readonly int[] s_ageUnits = { 0, 15, 45, 90, 180, 255 };
        private static readonly float[] s_ageYears = { 0f, 13f, 20f, 30f, 65f, 95f };

        /// <summary>A CS1 lane speed limit in m/s; 0 for non-positive or non-finite input.</summary>
        public static float SpeedLimitMs(float cs1Speed)
        {
            if (!(cs1Speed > 0f) || float.IsInfinity(cs1Speed)) return 0f;
            return cs1Speed * Cs1SpeedUnitMs;
        }

        /// <summary>CS1 age units (0-255) to years, piecewise linear through the age-group bounds.</summary>
        public static int AgeYears(int ageUnits)
        {
            int a = Math.Max(0, Math.Min(255, ageUnits));
            int i = 1;
            while (i < s_ageUnits.Length - 1 && a > s_ageUnits[i]) i++;
            float t = (a - s_ageUnits[i - 1]) / (float)(s_ageUnits[i] - s_ageUnits[i - 1]);
            double years = s_ageYears[i - 1] + (s_ageYears[i] - s_ageYears[i - 1]) * t;
            return (int)Math.Round(years, MidpointRounding.AwayFromZero);
        }

        /// <summary>Age group (0 child .. 4 senior) from a <c>Citizen.AgePhase</c> name.</summary>
        public static int AgeGroupOfPhase(string agePhaseName)
        {
            if (agePhaseName == null) return 3;
            if (agePhaseName == "Child") return 0;
            if (agePhaseName.StartsWith("Teen", StringComparison.Ordinal)) return 1;
            if (agePhaseName.StartsWith("Young", StringComparison.Ordinal)) return 2;
            if (agePhaseName.StartsWith("Adult", StringComparison.Ordinal)) return 3;
            if (agePhaseName.StartsWith("Senior", StringComparison.Ordinal)) return 4;
            return 3;
        }

        /// <summary>Hours of sun time as simulation frames (at most 30 days); 0 for non-positive or non-finite input.</summary>
        public static uint HoursToDayFrames(float hours)
        {
            if (!(hours > 0f) || float.IsInfinity(hours)) return 0u;
            double h = Math.Min(hours, MaxAdvanceHours);
            return (uint)Math.Round(h / 24.0 * DayFrames, MidpointRounding.AwayFromZero);
        }

        /// <summary>An angle in degrees wrapped to [0, 360).</summary>
        public static float NormalizeYawDeg(float deg)
        {
            float r = deg % 360f;
            if (r < 0f) r += 360f;
            return r >= 360f ? 0f : r;
        }
    }
}
