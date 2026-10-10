using System;
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
    /// <c>new Rng(Rng.Seed(seed, requestCount))</c>. Reuses one id list; allocates nothing in steady state.
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

        /// <summary>Creates a spawner.</summary>
        public PoliceSpawner(uint seed)
        {
            throw new NotImplementedException();
        }

        /// <summary>Requests made so far.</summary>
        public int RequestCount { get; private set; }

        /// <summary>A spawn point for a unit near <paramref name="player"/>; false when none was found (retry next second).</summary>
        public bool TryPick(GameWorld world, Vec3 player, out SpawnPoint point)
        {
            throw new NotImplementedException();
        }
    }
}
