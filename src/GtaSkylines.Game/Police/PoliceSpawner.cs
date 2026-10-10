using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Roads;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Police
{
    /// <summary>Where a unit appears.</summary>
    public struct SpawnPoint
    {
        /// <summary>Ground point on the road (road point nearest the door, or the random road point).</summary>
        public Vec3 Ground;
        /// <summary>Facing, degrees (the road heading closest to the bearing toward the player).</summary>
        public float YawDeg;
        /// <summary>True when the unit leaves a police station; false for an off-screen road point.</summary>
        public bool FromStation;
        /// <summary>The station's building id when FromStation, else 0.</summary>
        public ushort Station;
    }

    /// <summary>
    /// Picks spawn points (F-HEAT-7 spawning). Station rule: the nearest building of
    /// <c>ICity.ServiceBuildings(ServiceKind.Police)</c> whose nearest door is within <see cref="StationRange"/> of the
    /// player (horizontal distance) → the road point nearest that door within <see cref="StationRoadSnap"/> (else the door
    /// itself). Otherwise up to <see cref="Attempts"/> random bearings and distances U[250, 400) from the player, each
    /// snapped with <c>Roads.TryNearestPoint(…, 60)</c>; a candidate is accepted when its horizontal distance to the
    /// player is in [250, 400] and <c>world.CanPlayerSee(ground + 1 m up)</c> is false. Random numbers from
    /// <c>Rng.Seed(seed, requestCount, attempt-index)</c> (allocation-free). Reuses one id list; allocates nothing in steady state.
    /// </summary>
    public sealed class PoliceSpawner
    {
        /// <summary>Use a station within this distance of the player, metres.</summary>
        public const float StationRange = 1500f;
        /// <summary>Snap distance from the station door to a road, metres.</summary>
        public const float StationRoadSnap = 60f;
        /// <summary>Off-screen spawn distance bounds, metres.</summary>
        public const float MinDistance = 250f, MaxDistance = 400f;
        /// <summary>Random candidates tried per request.</summary>
        public const int Attempts = 12;

        private readonly uint _seed;
        private readonly List<ushort> _ids = new List<ushort>(16);

        /// <summary>Creates a spawner.</summary>
        public PoliceSpawner(uint seed)
        {
            _seed = seed;
        }

        /// <summary>Requests made so far.</summary>
        public int RequestCount { get; private set; }

        /// <summary>
        /// A spawn point for a unit near <paramref name="player"/>; false when none was found (retry next second). A
        /// station spawn faces the player (it drives out toward them); a road spawn faces the road direction closest to
        /// the bearing toward the player.
        /// </summary>
        public bool TryPick(GameWorld world, Vec3 player, out SpawnPoint point)
        {
            RequestCount++;
            point = default(SpawnPoint);
            ushort station;
            Vec3 door;
            if (NearestStation(world, player, out station, out door))
            {
                RoadPoint rp;
                Vec3 ground = world.Roads.TryNearestPoint(door, StationRoadSnap, out rp) ? rp.Position : door;
                point.Ground = ground;
                point.YawDeg = Vec3.YawDegOf(player - ground);
                point.FromStation = true;
                point.Station = station;
                return true;
            }
            for (int i = 0; i < Attempts; i++)
            {
                float bearing = Seeded.Range(_seed, (uint)RequestCount, (uint)(2 * i), 0f, 360f);
                float dist = Seeded.Range(_seed, (uint)RequestCount, (uint)(2 * i + 1), MinDistance, MaxDistance);
                Vec3 cand = player + Vec3.FromYawDeg(bearing) * dist;
                RoadPoint rp;
                if (!world.Roads.TryNearestPoint(cand, StationRoadSnap, out rp)) continue;
                float d = Vec3.DistanceXZ(rp.Position, player);
                if (d < MinDistance || d > MaxDistance) continue;
                if (world.CanPlayerSee(rp.Position + new Vec3(0f, 1f, 0f))) continue;
                point.Ground = rp.Position;
                float toPlayer = Vec3.YawDegOf(player - rp.Position);
                float h = rp.HeadingDeg;
                point.YawDeg = Math.Abs(MathUtil.DeltaAngleDeg(h, toPlayer)) <= 90f ? h : MathUtil.WrapDeg(h + 180f);
                return true;
            }
            return false;
        }

        private bool NearestStation(GameWorld world, Vec3 player, out ushort station, out Vec3 door)
        {
            var search = new DoorSearch { MaxDistance = StationRange, SkipAbandoned = true, PositionWithoutDoors = true };
            return ServiceDoors.Nearest(world.City, ServiceKind.Police, player, search, _ids, out station, out door);
        }
    }
}
