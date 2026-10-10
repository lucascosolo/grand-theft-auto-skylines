using System;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.Interiors
{
    /// <summary>
    /// Generates GTA-style interiors on demand from a building's descriptor. Pure and deterministic: the same
    /// descriptor (id, prefab name, service, sub-service, level, size), archetype and variant always give the same
    /// layout. Fast enough to run on the frame the player enters (target &lt; 30 ms for a 40 × 40 m interior).
    /// </summary>
    public static class InteriorGenerator
    {
        /// <summary>Smallest interior side, metres.</summary>
        public const float MinSide = 8f;

        /// <summary>Largest interior side, metres.</summary>
        public const float MaxSide = 40f;

        /// <summary>Smallest room side, metres (bathrooms and cells may be 2.0).</summary>
        public const float MinRoomSide = 2.4f;

        /// <summary>Clear width of ordinary doorways between rooms, metres (wide openings such as corridor mouths are wider; none is below 1.0).</summary>
        public const float DoorWidth = 1.2f;

        /// <summary>
        /// The archetype for a building:
        /// abandoned → None; Park → None;
        /// Residential: sub-service containing "Low" (or, with no sub-service, footprint ≤ 400 m² and height ≤ 12 m) → House, else ApartmentLobby;
        /// Commercial: sub-service containing "Leisure" or "Tourist" → Restaurant; otherwise by a hash of id and prefab name:
        /// 10 % GunStore, 25 % ClothesShop, 20 % Restaurant, 45 % ConvenienceStore;
        /// Office → Office; Industrial: 20 % (hash) Garage, else Warehouse;
        /// Police → PoliceStation; Health → Hospital; Fire → FireStation; Education → School;
        /// Garbage, Electricity, Water, Transport, Deathcare, Government, Monument → ServiceHall; Other → Generic.
        /// </summary>
        public static InteriorArchetype ArchetypeFor(BuildingDescriptor building)
        {
            throw new NotImplementedException();
        }

        /// <summary>The street-level interior of a building (archetype from <see cref="ArchetypeFor"/>, variant 0); null for <see cref="InteriorArchetype.None"/>.</summary>
        public static InteriorLayout Generate(BuildingDescriptor building)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// A specific interior of a building. Footprint: the building's width and length scaled per archetype and
        /// clamped to [<see cref="MinSide"/>, <see cref="MaxSide"/>], rounded to 0.5 m (apartments: 9-16 m by 8-14 m from
        /// the variant). Returns null for <see cref="InteriorArchetype.None"/>. Never throws for any descriptor whose
        /// sizes are finite (non-positive sizes are treated as the minimum).
        /// </summary>
        public static InteriorLayout Generate(BuildingDescriptor building, InteriorArchetype archetype, int variant)
        {
            throw new NotImplementedException();
        }

        /// <summary>The generation seed for a building, archetype and variant (exposed for tests).</summary>
        public static uint SeedFor(BuildingDescriptor building, InteriorArchetype archetype, int variant)
        {
            return Rng.Seed(building.Id, StableHash(building.InfoName), Rng.Seed((uint)archetype, (uint)variant));
        }

        /// <summary>FNV-1a hash of a string (stable across runtimes, unlike <c>string.GetHashCode</c>).</summary>
        public static uint StableHash(string s)
        {
            uint h = 2166136261u;
            if (s == null) return h;
            for (int i = 0; i < s.Length; i++)
            {
                h ^= s[i];
                h *= 16777619u;
            }
            return h;
        }
    }
}
