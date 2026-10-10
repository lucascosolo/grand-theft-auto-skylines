using System;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.Police
{
    /// <summary>
    /// Search-zone maths (F-HEAT-4). The zone is a circle on the x, z plane around the last known position; distances
    /// below are horizontal (<see cref="Vec3.DistanceXZ"/>).
    /// </summary>
    public static class SearchZone
    {
        /// <summary>Seconds unseen before Evading is set (and the zone is published).</summary>
        public const float EvadeAfterSeconds = 2f;
        /// <summary>Radius at 0 s unseen, metres.</summary>
        public const float BaseRadius = 40f;
        /// <summary>Growth per second unseen, m/s.</summary>
        public const float GrowthPerSecond = 6f;
        /// <summary>Largest radius, metres.</summary>
        public const float MaxRadius = 400f;
        /// <summary>District heat (at the last known position) above which search times are longer.</summary>
        public const float HotDistrictHeat = 60f;
        /// <summary>Search-time factor in a hot district.</summary>
        public const float HotDistrictFactor = 1.5f;
        /// <summary>Sector centres sit at this fraction of the radius from the centre.</summary>
        public const float SectorCentreFraction = 0.6f;

        private static readonly float[] SearchTimes = { 0f, 20f, 30f, 45f, 60f, 90f };

        /// <summary>Zone radius after <paramref name="secondsSinceSeen"/>: min(400, 40 + 6 × s); s &lt; 0 counts as 0.</summary>
        public static float Radius(float secondsSinceSeen)
        {
            if (secondsSinceSeen < 0f) secondsSinceSeen = 0f;
            return Math.Min(MaxRadius, BaseRadius + GrowthPerSecond * secondsSinceSeen);
        }

        /// <summary>
        /// Seconds unseen that clear the stars: 1★ 20, 2★ 30, 3★ 45, 4★ 60, 5★ 90, × 1.5 when
        /// <paramref name="districtHeat"/> &gt; 60; 0 for 0 stars. Stars outside 0-5 are clamped.
        /// </summary>
        public static float SearchSeconds(int stars, float districtHeat)
        {
            if (stars < 0) stars = 0;
            if (stars > 5) stars = 5;
            float t = SearchTimes[stars];
            return districtHeat > HotDistrictHeat ? t * HotDistrictFactor : t;
        }

        /// <summary>True when <paramref name="point"/> is within <paramref name="radius"/> of <paramref name="centre"/> (horizontal).</summary>
        public static bool Contains(Vec3 centre, float radius, Vec3 point)
        {
            return Vec3.DistanceXZ(centre, point) <= radius;
        }

        /// <summary>
        /// Centre of sector <paramref name="sector"/> of <paramref name="sectorCount"/> equal wedges: the point at
        /// 0.6 × radius from the centre at bearing (yaw) 360 × (sector + 0.5) / sectorCount degrees (0 = +z, 90 = +x),
        /// y = centre.y. One sector (count ≤ 1) → the centre itself.
        /// </summary>
        public static Vec3 SectorCentre(Vec3 centre, float radius, int sector, int sectorCount)
        {
            if (sectorCount <= 1) return centre;
            double a = 360.0 * (sector + 0.5) / sectorCount * MathUtil.Deg2Rad;
            float r = SectorCentreFraction * radius;
            return new Vec3(centre.X + (float)Math.Sin(a) * r, centre.Y, centre.Z + (float)Math.Cos(a) * r);
        }

        /// <summary>The wedge index (0..count−1, same bearings as <see cref="SectorCentre"/>) that contains a point; 0 for count ≤ 1 or the centre.</summary>
        public static int SectorOf(Vec3 centre, Vec3 point, int sectorCount)
        {
            if (sectorCount <= 1) return 0;
            float dx = point.X - centre.X, dz = point.Z - centre.Z;
            if (dx * dx + dz * dz < 1e-8f) return 0;
            double yaw = Math.Atan2(dx, dz) * MathUtil.Rad2Deg;
            if (yaw < 0) yaw += 360.0;
            int i = (int)Math.Floor(yaw / (360.0 / sectorCount));
            return i < 0 ? 0 : i >= sectorCount ? sectorCount - 1 : i;
        }
    }
}
