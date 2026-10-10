using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.Tests.Interiors
{
    /// <summary>Shared builders and geometry helpers for the interior tests.</summary>
    public static class InteriorTestSupport
    {
        /// <summary>Number of seeds/sizes generated per archetype.</summary>
        public const int Cases = 30;

        /// <summary>Footprint/height triples (width, length, height) cycled over the cases: zero, negative, tiny, huge, ordinary.</summary>
        public static readonly float[][] Sizes =
        {
            new[] { 0f, 0f, 0f },
            new[] { -10f, -10f, -5f },
            new[] { 4f, 4f, 4f },
            new[] { 200f, 200f, 60f },
            new[] { 16f, 16f, 10f },
            new[] { 30f, 12f, 8f },
            new[] { 12f, 30f, 40f },
            new[] { 8f, 8f, 3f },
            new[] { 40f, 40f, 20f },
            new[] { 25.3f, 17.7f, 15f },
            new[] { 100f, 10f, 10f },
            new[] { 10f, 100f, 10f },
            new[] { 4f, 200f, 12f },
            new[] { 200f, 4f, 12f },
            new[] { 21.1f, 9.4f, 6f },
        };

        public static ushort IdFor(int i)
        {
            if (i == 0) return 0;
            if (i == 1) return 65535;
            return (ushort)(i * 2531 + 7);
        }

        public static BuildingDescriptor Building(int id, ServiceKind service, string sub = "", float w = 16f, float l = 16f,
            float h = 10f, bool abandoned = false, string name = null)
        {
            return new BuildingDescriptor
            {
                Id = (ushort)id,
                Name = "Building " + id,
                InfoName = name ?? ("Prefab_" + service + "_" + id),
                Service = service,
                SubService = sub,
                Level = 1 + (id % 5),
                Width = w,
                Length = l,
                Height = h,
                Abandoned = abandoned,
            };
        }

        public static ServiceKind ServiceFor(InteriorArchetype a)
        {
            switch (a)
            {
                case InteriorArchetype.House:
                case InteriorArchetype.ApartmentLobby:
                case InteriorArchetype.Apartment: return ServiceKind.Residential;
                case InteriorArchetype.ConvenienceStore:
                case InteriorArchetype.ClothesShop:
                case InteriorArchetype.GunStore:
                case InteriorArchetype.Restaurant: return ServiceKind.Commercial;
                case InteriorArchetype.Office: return ServiceKind.Office;
                case InteriorArchetype.Warehouse:
                case InteriorArchetype.Garage: return ServiceKind.Industrial;
                case InteriorArchetype.PoliceStation: return ServiceKind.Police;
                case InteriorArchetype.Hospital: return ServiceKind.Health;
                case InteriorArchetype.FireStation: return ServiceKind.Fire;
                case InteriorArchetype.School: return ServiceKind.Education;
                case InteriorArchetype.ServiceHall: return ServiceKind.Water;
                default: return ServiceKind.Other;
            }
        }

        public static BuildingDescriptor CaseBuilding(InteriorArchetype a, int i)
        {
            float[] s = Sizes[i % Sizes.Length];
            return Building(IdFor(i), ServiceFor(a), "", s[0], s[1], s[2]);
        }

        public static int CaseVariant(InteriorArchetype a, int i)
        {
            return a == InteriorArchetype.Apartment ? i : 0;
        }

        private static readonly ConcurrentDictionary<InteriorArchetype, Lazy<InteriorLayout[]>> Cache =
            new ConcurrentDictionary<InteriorArchetype, Lazy<InteriorLayout[]>>();

        /// <summary>The <see cref="Cases"/> generated layouts of an archetype (generated once, shared).</summary>
        public static InteriorLayout[] Layouts(InteriorArchetype a)
        {
            return Cache.GetOrAdd(a, k => new Lazy<InteriorLayout[]>(() =>
            {
                var r = new InteriorLayout[Cases];
                for (int i = 0; i < Cases; i++)
                    r[i] = InteriorGenerator.Generate(CaseBuilding(k, i), k, CaseVariant(k, i));
                return r;
            })).Value;
        }

        /// <summary>Every archetype that has an interior, as theory data.</summary>
        public static IEnumerable<object[]> AllArchetypes()
        {
            foreach (InteriorArchetype a in Enum.GetValues(typeof(InteriorArchetype)))
                if (a != InteriorArchetype.None) yield return new object[] { a };
        }

        public static IEnumerable<InteriorArchetype> AllArchetypeValues()
        {
            foreach (InteriorArchetype a in Enum.GetValues(typeof(InteriorArchetype)))
                if (a != InteriorArchetype.None) yield return a;
        }

        public static bool Near(float a, float b, float eps) { return Math.Abs(a - b) <= eps; }

        public static string Tag(InteriorArchetype a, int i) { return a + " case " + i; }

        /// <summary>Centres of all free nav cells.</summary>
        public static List<Vec3> FreeCells(InteriorNavGrid nav)
        {
            var r = new List<Vec3>();
            for (int cz = 0; cz < nav.CellsZ; cz++)
                for (int cx = 0; cx < nav.CellsX; cx++)
                {
                    float x = nav.OriginX + (cx + 0.5f) * InteriorNavGrid.CellSize;
                    float z = nav.OriginZ + (cz + 0.5f) * InteriorNavGrid.CellSize;
                    if (nav.IsFree(x, z)) r.Add(new Vec3(x, 0f, z));
                }
            return r;
        }

        /// <summary>Distance on the floor plane from a point to a rectangle (0 inside).</summary>
        public static float DistToRect(float x, float z, Rect2 r)
        {
            float dx = Math.Max(Math.Max(r.MinX - x, 0f), x - r.MaxX);
            float dz = Math.Max(Math.Max(r.MinZ - z, 0f), z - r.MaxZ);
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Nearest two-sided ray hit among the layout's collision triangles (Moller-Trumbore); returns the distance
        /// along the unit direction, or -1 when nothing is hit within <paramref name="maxDist"/>. The triangle index of
        /// the hit is written to <paramref name="tri"/>.
        /// </summary>
        public static float Raycast(float[] t, Vec3 o, Vec3 d, float maxDist, out int tri)
        {
            tri = -1;
            double best = double.MaxValue;
            int n = t.Length / 9;
            for (int k = 0; k < n; k++)
            {
                int b = k * 9;
                double e1x = t[b + 3] - t[b], e1y = t[b + 4] - t[b + 1], e1z = t[b + 5] - t[b + 2];
                double e2x = t[b + 6] - t[b], e2y = t[b + 7] - t[b + 1], e2z = t[b + 8] - t[b + 2];
                double px = d.Y * e2z - d.Z * e2y, py = d.Z * e2x - d.X * e2z, pz = d.X * e2y - d.Y * e2x;
                double det = e1x * px + e1y * py + e1z * pz;
                if (Math.Abs(det) < 1e-12) continue;
                double inv = 1.0 / det;
                double tx = o.X - t[b], ty = o.Y - t[b + 1], tz = o.Z - t[b + 2];
                double u = (tx * px + ty * py + tz * pz) * inv;
                if (u < -1e-6 || u > 1 + 1e-6) continue;
                double qx = ty * e1z - tz * e1y, qy = tz * e1x - tx * e1z, qz = tx * e1y - ty * e1x;
                double v = (d.X * qx + d.Y * qy + d.Z * qz) * inv;
                if (v < -1e-6 || u + v > 1 + 1e-6) continue;
                double dist = (e2x * qx + e2y * qy + e2z * qz) * inv;
                if (dist > 1e-6 && dist <= maxDist && dist < best) { best = dist; tri = k; }
            }
            return tri < 0 ? -1f : (float)best;
        }

        /// <summary>Triangle normal (not normalised): Cross(b - a, c - a).</summary>
        public static Vec3 TriNormal(float[] t, int k)
        {
            int b = k * 9;
            var a = new Vec3(t[b], t[b + 1], t[b + 2]);
            var bb = new Vec3(t[b + 3], t[b + 4], t[b + 5]);
            var c = new Vec3(t[b + 6], t[b + 7], t[b + 8]);
            return Vec3.Cross(bb - a, c - a);
        }
    }
}
