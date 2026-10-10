using System;
using System.Collections.Generic;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.City
{
    /// <summary>Which doors <see cref="ServiceDoors.Nearest"/> considers; each caller states its own filters.</summary>
    public struct DoorSearch
    {
        /// <summary>Doors closer than this (XZ, metres) are skipped.</summary>
        public float MinDistance;
        /// <summary>Doors farther than this (XZ, metres, inclusive) are skipped.</summary>
        public float MaxDistance;
        /// <summary>Skip abandoned buildings.</summary>
        public bool SkipAbandoned;
        /// <summary>Only a building's first door counts (a place is its main entrance).</summary>
        public bool FirstDoorOnly;
        /// <summary>A building without doors counts at its position; otherwise it is skipped.</summary>
        public bool PositionWithoutDoors;
        /// <summary>Building ids to skip (may be null).</summary>
        public ICollection<ushort> Exclude;
        /// <summary>Extra acceptance test (may be null); keep it a cached delegate in per-frame code.</summary>
        public Predicate<BuildingDescriptor> Accept;

        /// <summary>Every door of every building of the service, no distance limit.</summary>
        public static DoorSearch Any { get { return new DoorSearch { MaxDistance = float.MaxValue }; } }
    }

    /// <summary>The one "nearest door of a service building" search over <see cref="ICity"/> (respawn, police stations, campaign places).</summary>
    public static class ServiceDoors
    {
        /// <summary>
        /// The door nearest <paramref name="from"/> (XZ) among <c>city.ServiceBuildings(service)</c> passing
        /// <paramref name="search"/>; ties keep the earlier building and door. <paramref name="scratch"/> is cleared and
        /// reused (no allocation). False, with building 0 and door zero, when none qualifies.
        /// </summary>
        public static bool Nearest(ICity city, ServiceKind service, Vec3 from, DoorSearch search, List<ushort> scratch,
            out ushort building, out Vec3 door)
        {
            building = 0;
            door = Vec3.Zero;
            scratch.Clear();
            city.ServiceBuildings(service, scratch);
            float best = float.MaxValue;
            for (int i = 0; i < scratch.Count; i++)
            {
                BuildingDescriptor b;
                if (!city.TryGetBuilding(scratch[i], out b)) continue;
                if (search.SkipAbandoned && b.Abandoned) continue;
                if (search.Exclude != null && search.Exclude.Contains(b.Id)) continue;
                if (search.Accept != null && !search.Accept(b)) continue;
                bool hasDoors = b.Doors != null && b.Doors.Length > 0;
                if (!hasDoors && !search.PositionWithoutDoors) continue;
                int n = !hasDoors || search.FirstDoorOnly ? 1 : b.Doors.Length;
                for (int k = 0; k < n; k++)
                {
                    Vec3 dp = hasDoors ? b.Doors[k] : b.Position;
                    float d = Vec3.DistanceXZ(dp, from);
                    if (d < search.MinDistance || d > search.MaxDistance || d >= best) continue;
                    best = d;
                    building = b.Id;
                    door = dp;
                }
            }
            return building != 0;
        }
    }
}
