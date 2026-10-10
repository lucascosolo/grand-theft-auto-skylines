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
            if (building == null || building.Abandoned) return InteriorArchetype.None;
            string sub = building.SubService ?? "";
            uint h = Rng.Seed(building.Id, StableHash(building.InfoName));
            switch (building.Service)
            {
                case ServiceKind.Park:
                    return InteriorArchetype.None;
                case ServiceKind.Residential:
                    if (sub.Length == 0)
                    {
                        float area = Math.Max(0f, building.Width) * Math.Max(0f, building.Length);
                        return area <= 400f && building.Height <= 12f ? InteriorArchetype.House : InteriorArchetype.ApartmentLobby;
                    }
                    return sub.IndexOf("Low", StringComparison.Ordinal) >= 0 ? InteriorArchetype.House : InteriorArchetype.ApartmentLobby;
                case ServiceKind.Commercial:
                    if (sub.IndexOf("Leisure", StringComparison.Ordinal) >= 0 || sub.IndexOf("Tourist", StringComparison.Ordinal) >= 0) return InteriorArchetype.Restaurant;
                    {
                        uint pick = h % 100u;
                        if (pick < 10u) return InteriorArchetype.GunStore;
                        if (pick < 35u) return InteriorArchetype.ClothesShop;
                        if (pick < 55u) return InteriorArchetype.Restaurant;
                        return InteriorArchetype.ConvenienceStore;
                    }
                case ServiceKind.Office:
                    return InteriorArchetype.Office;
                case ServiceKind.Industrial:
                    return Rng.Seed(h, 0x1Du) % 100u < 20u ? InteriorArchetype.Garage : InteriorArchetype.Warehouse;
                case ServiceKind.Police:
                    return InteriorArchetype.PoliceStation;
                case ServiceKind.Health:
                    return InteriorArchetype.Hospital;
                case ServiceKind.Fire:
                    return InteriorArchetype.FireStation;
                case ServiceKind.Education:
                    return InteriorArchetype.School;
                case ServiceKind.Garbage:
                case ServiceKind.Electricity:
                case ServiceKind.Water:
                case ServiceKind.Transport:
                case ServiceKind.Deathcare:
                case ServiceKind.Government:
                case ServiceKind.Monument:
                    return InteriorArchetype.ServiceHall;
                default:
                    return InteriorArchetype.Generic;
            }
        }

        /// <summary>The street-level interior of a building (archetype from <see cref="ArchetypeFor"/>, variant 0); null for <see cref="InteriorArchetype.None"/>.</summary>
        public static InteriorLayout Generate(BuildingDescriptor building)
        {
            InteriorArchetype a = ArchetypeFor(building);
            return a == InteriorArchetype.None ? null : Generate(building, a, 0);
        }

        /// <summary>
        /// A specific interior of a building. Footprint: the building's width and length scaled per archetype and
        /// clamped to [<see cref="MinSide"/>, <see cref="MaxSide"/>], rounded to 0.5 m (apartments: 9-16 m by 8-14 m from
        /// the variant). Returns null for <see cref="InteriorArchetype.None"/>. Never throws for any descriptor whose
        /// sizes are finite (non-positive sizes are treated as the minimum).
        /// </summary>
        public static InteriorLayout Generate(BuildingDescriptor building, InteriorArchetype archetype, int variant)
        {
            if (building == null || archetype == InteriorArchetype.None) return null;
            uint seed = SeedFor(building, archetype, variant);
            var rng = new Rng(seed);
            var l = new InteriorLayout { BuildingId = building.Id, Archetype = archetype, Variant = variant };
            if (archetype == InteriorArchetype.Apartment)
            {
                l.Width = 9f + 0.5f * rng.Range(0, 15);
                l.Depth = 8f + 0.5f * rng.Range(0, 13);
            }
            else
            {
                float scale = FootprintScale(archetype), min = MinSideFor(archetype);
                l.Width = Side(building.Width, scale, min);
                l.Depth = Side(building.Length, scale, min);
            }
            l.CeilingHeight = CeilingFor(archetype);
            var palette = new InteriorPalette(l, rng);
            InteriorPlanner.Plan(l, rng, palette);
            new InteriorFurnisher(l, rng).Furnish();
            InteriorMeshBuilder.Build(l, palette, seed);
            return l;
        }

        private static float Side(float v, float scale, float min)
        {
            float s = v > 0f ? (float.IsInfinity(v) ? MaxSide : v * scale) : 0f;
            s = MathUtil.Clamp(s, min, MaxSide);
            s = (float)Math.Round(s * 2f) / 2f;
            return MathUtil.Clamp(s, min, MaxSide);
        }

        private static float FootprintScale(InteriorArchetype a)
        {
            switch (a)
            {
                case InteriorArchetype.House: return 0.8f;
                case InteriorArchetype.ApartmentLobby: return 0.6f;
                case InteriorArchetype.ConvenienceStore:
                case InteriorArchetype.ClothesShop:
                case InteriorArchetype.GunStore:
                case InteriorArchetype.Restaurant:
                case InteriorArchetype.FireStation: return 0.85f;
                case InteriorArchetype.Warehouse:
                case InteriorArchetype.Garage: return 0.9f;
                case InteriorArchetype.PoliceStation: return 0.8f;
                case InteriorArchetype.Office:
                case InteriorArchetype.ServiceHall: return 0.6f;
                default: return 0.7f;
            }
        }

        private static float MinSideFor(InteriorArchetype a)
        {
            switch (a)
            {
                case InteriorArchetype.PoliceStation:
                case InteriorArchetype.Hospital:
                case InteriorArchetype.School: return 14f;
                case InteriorArchetype.Warehouse:
                case InteriorArchetype.FireStation: return 12f;
                case InteriorArchetype.Restaurant:
                case InteriorArchetype.Office:
                case InteriorArchetype.Garage:
                case InteriorArchetype.ServiceHall: return 10f;
                default: return MinSide;
            }
        }

        private static float CeilingFor(InteriorArchetype a)
        {
            switch (a)
            {
                case InteriorArchetype.House:
                case InteriorArchetype.Apartment: return 2.8f;
                case InteriorArchetype.ApartmentLobby:
                case InteriorArchetype.ClothesShop: return 3.4f;
                case InteriorArchetype.Office:
                case InteriorArchetype.Generic: return 3.0f;
                case InteriorArchetype.Warehouse:
                case InteriorArchetype.Garage:
                case InteriorArchetype.FireStation: return 4.5f;
                case InteriorArchetype.ServiceHall: return 4.2f;
                default: return 3.2f;
            }
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
