using System;
using System.Collections.Generic;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.Interiors
{
    /// <summary>
    /// Lays out rooms, doorways, portals and spawn points. Two patterns, both connected by construction:
    /// <b>hall</b> (one main room across the front, a strip of back rooms each opening onto it) and <b>corridor</b>
    /// (a front room across the full width, a corridor running back from its middle, side rooms each opening onto the
    /// corridor, optionally an end room across the back).
    /// </summary>
    internal static class InteriorPlanner
    {
        private struct Req
        {
            public RoomKind Kind;
            public float Weight;
            public bool Required;

            public Req(RoomKind kind, float weight, bool required)
            {
                Kind = kind; Weight = weight; Required = required;
            }
        }

        internal static void Plan(InteriorLayout l, Rng rng, InteriorPalette palette)
        {
            switch (l.Archetype)
            {
                case InteriorArchetype.House:
                    Corridor(l, rng, RoomKind.LivingRoom, Math.Max(3.6f, l.Depth * 0.4f), 2.0f, null,
                        R(RoomKind.Kitchen, 1.0f, true), R(RoomKind.Bedroom, 1.2f, true), R(RoomKind.Bathroom, 0.6f, true), R(RoomKind.Bedroom, 1.0f, false), R(RoomKind.Bathroom, 0.5f, false));
                    break;
                case InteriorArchetype.Apartment:
                    Corridor(l, rng, RoomKind.LivingRoom, Math.Max(3.6f, l.Depth * 0.42f), 2.0f, null,
                        R(RoomKind.Kitchen, 1.0f, true), R(RoomKind.Bedroom, 1.2f, true), R(RoomKind.Bathroom, 0.6f, true), R(RoomKind.Bedroom, 1.0f, false));
                    break;
                case InteriorArchetype.Office:
                    Corridor(l, rng, RoomKind.Lobby, Math.Max(4f, l.Depth * 0.3f), 2.0f, l.Depth >= 24f ? (RoomKind?)RoomKind.OfficeRoom : null,
                        R(RoomKind.OfficeRoom, 1.5f, true), R(RoomKind.MeetingRoom, 1.0f, false), R(RoomKind.OfficeRoom, 1.3f, false), R(RoomKind.Restroom, 0.5f, false), R(RoomKind.OfficeRoom, 1.2f, false), R(RoomKind.OfficeRoom, 1.2f, false));
                    break;
                case InteriorArchetype.PoliceStation:
                    Corridor(l, rng, RoomKind.Lobby, Math.Max(5f, l.Depth * 0.33f), 2.0f, null,
                        R(RoomKind.Cells, 1.3f, true), R(RoomKind.LockerRoom, 1.0f, true), R(RoomKind.Armory, 0.8f, false), R(RoomKind.OfficeRoom, 1.2f, false), R(RoomKind.OfficeRoom, 1.0f, false), R(RoomKind.Restroom, 0.5f, false));
                    break;
                case InteriorArchetype.Hospital:
                    Corridor(l, rng, RoomKind.Lobby, Math.Max(5f, l.Depth * 0.33f), 2.4f, null,
                        R(RoomKind.Ward, 1.5f, true), R(RoomKind.TreatmentRoom, 1.0f, true), R(RoomKind.Ward, 1.3f, false), R(RoomKind.OfficeRoom, 0.8f, false), R(RoomKind.Restroom, 0.5f, false), R(RoomKind.Ward, 1.2f, false));
                    break;
                case InteriorArchetype.School:
                    Corridor(l, rng, RoomKind.Lobby, Math.Max(4f, l.Depth * 0.25f), 2.4f, null,
                        R(RoomKind.Classroom, 1.5f, true), R(RoomKind.Classroom, 1.5f, false), R(RoomKind.OfficeRoom, 0.8f, false), R(RoomKind.Restroom, 0.5f, false), R(RoomKind.Classroom, 1.4f, false), R(RoomKind.Classroom, 1.4f, false));
                    break;
                case InteriorArchetype.ServiceHall:
                    Corridor(l, rng, RoomKind.Lobby, Math.Max(5f, l.Depth * 0.4f), 2.0f, null,
                        R(RoomKind.ControlRoom, 1.4f, true), R(RoomKind.OfficeRoom, 1.0f, false), R(RoomKind.StockRoom, 1.0f, false), R(RoomKind.Restroom, 0.5f, false));
                    break;
                case InteriorArchetype.ApartmentLobby:
                    Hall(l, RoomKind.Lobby);
                    break;
                case InteriorArchetype.ConvenienceStore:
                    Hall(l, RoomKind.ShopFloor, R(RoomKind.StockRoom, 1f, true), R(RoomKind.Restroom, 0.6f, false));
                    break;
                case InteriorArchetype.ClothesShop:
                    Hall(l, RoomKind.ShopFloor, R(RoomKind.StockRoom, 1f, true));
                    break;
                case InteriorArchetype.GunStore:
                    Hall(l, RoomKind.ShopFloor, R(RoomKind.StockRoom, 1f, true), R(RoomKind.Armory, 1f, false));
                    break;
                case InteriorArchetype.Restaurant:
                    Hall(l, RoomKind.DiningRoom, R(RoomKind.Kitchen, 1.2f, true), R(RoomKind.Restroom, 0.6f, true));
                    break;
                case InteriorArchetype.Warehouse:
                    Hall(l, RoomKind.WarehouseFloor, R(RoomKind.OfficeRoom, 1f, true), R(RoomKind.Restroom, 0.5f, false));
                    break;
                case InteriorArchetype.Garage:
                    Hall(l, RoomKind.Workshop, R(RoomKind.OfficeRoom, 1f, true), R(RoomKind.StockRoom, 1f, false));
                    break;
                case InteriorArchetype.FireStation:
                    Hall(l, RoomKind.VehicleBay, R(RoomKind.Bunks, 1.2f, true), R(RoomKind.Kitchen, 1f, false), R(RoomKind.LockerRoom, 0.8f, false));
                    break;
                default:
                    Hall(l, RoomKind.Lobby, R(RoomKind.OfficeRoom, 1f, false), R(RoomKind.StockRoom, 1f, false));
                    break;
            }

            foreach (Room r in l.Rooms)
            {
                r.FloorRecipe = palette.Floor(r.Kind, l.Archetype);
                r.WallRecipe = palette.Wall(r.Kind, l.Archetype);
            }

            float t = InteriorLayout.WallThickness;
            l.Portals.Add(new Portal { Kind = PortalKind.Entrance, Position = new Vec3(0f, 0f, t / 2f), InwardYawDeg = 0f, Room = 0 });
            l.EntranceSpawn = new Vec3(0f, 0f, 1.2f);
            l.EntranceSpawnYawDeg = 0f;
            if (l.Archetype == InteriorArchetype.ApartmentLobby)
            {
                Vec3 p = new Vec3(0f, 0f, l.Depth - t / 2f);
                l.Portals.Add(new Portal { Kind = PortalKind.Elevator, Position = p, InwardYawDeg = 180f, Room = l.RoomAt(0f, l.Depth - 0.5f) });
                l.ElevatorSpawn = new Vec3(0f, 0f, l.Depth - 1.2f);
                l.ElevatorSpawnYawDeg = 180f;
            }
            else
            {
                l.ElevatorSpawn = l.EntranceSpawn;
                l.ElevatorSpawnYawDeg = l.EntranceSpawnYawDeg;
            }
        }

        private static Req R(RoomKind kind, float weight, bool required)
        {
            return new Req(kind, weight, required);
        }

        private static Room AddRoom(InteriorLayout l, RoomKind kind, float minX, float minZ, float maxX, float maxZ)
        {
            var r = new Room { Id = l.Rooms.Count, Kind = kind, Bounds = new Rect2(minX, minZ, maxX, maxZ) };
            l.Rooms.Add(r);
            return r;
        }

        private static void AddDoor(InteriorLayout l, Room a, Room b, Vec3 centre, bool wallAlongX, float width)
        {
            l.Doorways.Add(new Doorway { RoomA = a.Id, RoomB = b.Id, Center = centre, WallAlongX = wallAlongX, Width = width, Height = InteriorLayout.DoorHeight });
        }

        /// <summary>Main room across the front; back rooms side by side along the back wall, each opening onto it.</summary>
        private static void Hall(InteriorLayout l, RoomKind main, params Req[] back)
        {
            float w = l.Width, d = l.Depth, x0 = -w / 2f, x1 = w / 2f;
            bool big = main == RoomKind.WarehouseFloor || main == RoomKind.VehicleBay || main == RoomKind.Workshop;
            float bd = MathUtil.Clamp(Snap(d * (big ? 0.25f : 0.3f)), 3f, big ? 8f : 7f);
            var chosen = new List<Req>();
            if (back.Length > 0 && d - bd >= 5f)
            {
                int capacity = (int)Math.Floor(w / 3f);
                // Required rooms win the capacity cut, then the chosen ones keep the program order (no sort: Mono's
                // and .NET's unstable sorts could order ties differently).
                var take = new bool[back.Length];
                int taken = 0;
                for (int i = 0; i < back.Length && taken < capacity; i++) if (back[i].Required) { take[i] = true; taken++; }
                for (int i = 0; i < back.Length && taken < capacity; i++) if (!back[i].Required) { take[i] = true; taken++; }
                for (int i = 0; i < back.Length; i++) if (take[i]) chosen.Add(back[i]);
            }
            if (chosen.Count == 0)
            {
                AddRoom(l, main, x0, 0f, x1, d);
                return;
            }
            Room front = AddRoom(l, main, x0, 0f, x1, d - bd);
            float[] widths = Split(w, chosen, 3f);
            float x = x0;
            for (int i = 0; i < chosen.Count; i++)
            {
                float xe = i == chosen.Count - 1 ? x1 : x + widths[i];
                Room r = AddRoom(l, chosen[i].Kind, x, d - bd, xe, d);
                AddDoor(l, front, r, new Vec3((x + xe) / 2f, 0f, d - bd), true, InteriorGenerator.DoorWidth);
                x = xe;
            }
        }

        /// <summary>Front room, a corridor back from its middle, side rooms on both sides, optional end room.</summary>
        private static void Corridor(InteriorLayout l, Rng rng, RoomKind frontKind, float frontDepth, float corridorWidth, RoomKind? endKind, params Req[] side)
        {
            float w = l.Width, d = l.Depth, x0 = -w / 2f, x1 = w / 2f;
            float min = InteriorGenerator.MinRoomSide;
            float fd = Snap(MathUtil.Clamp(frontDepth, 3f, d - min));
            float rest = d - fd;
            if (rest < min)
            {
                AddRoom(l, frontKind, x0, 0f, x1, d);
                return;
            }
            float cw = Math.Min(corridorWidth, w - 2f * min);
            cw = Math.Max(cw, 2.0f);
            float ed = 0f;
            if (endKind.HasValue && rest >= 2f * min + 3f) ed = Snap(MathUtil.Clamp(rest * 0.3f, 3f, 8f));
            float ls = rest - ed;

            // Distribute the side rooms: required first, alternating to the lighter side, within capacity.
            int capacity = Math.Max(1, (int)Math.Floor(ls / min));
            var left = new List<Req>();
            var right = new List<Req>();
            float lw = 0f, rw = 0f;
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (Req r in side)
                {
                    if (r.Required != (pass == 0)) continue;
                    bool toLeft = lw <= rw;
                    List<Req> target = toLeft ? left : right;
                    if (target.Count >= capacity)
                    {
                        toLeft = !toLeft;
                        target = toLeft ? left : right;
                        if (target.Count >= capacity) continue;
                    }
                    target.Add(r);
                    if (toLeft) lw += r.Weight; else rw += r.Weight;
                }
            }
            if (left.Count == 0 && right.Count > 1) { left.Add(right[right.Count - 1]); right.RemoveAt(right.Count - 1); }
            if (right.Count == 0 && left.Count > 1) { right.Add(left[left.Count - 1]); left.RemoveAt(left.Count - 1); }
            if (left.Count == 0 || right.Count == 0)
            {
                // Too little to make a corridor worthwhile: fall back to a hall with what there is.
                l.Rooms.Clear();
                l.Doorways.Clear();
                var all = new List<Req>(left);
                all.AddRange(right);
                Hall(l, frontKind, all.ToArray());
                return;
            }
            // Mirror sometimes so homes are not all alike.
            if (rng.Chance(0.5f)) { List<Req> tmp = left; left = right; right = tmp; }

            Room front = AddRoom(l, frontKind, x0, 0f, x1, fd);
            Room corridor = AddRoom(l, RoomKind.Corridor, -cw / 2f, fd, cw / 2f, d - ed);
            AddDoor(l, front, corridor, new Vec3(0f, 0f, fd), true, Math.Max(InteriorGenerator.DoorWidth, cw - 0.2f));
            SideColumn(l, corridor, left, x0, -cw / 2f, fd, d - ed, -cw / 2f);
            SideColumn(l, corridor, right, cw / 2f, x1, fd, d - ed, cw / 2f);
            if (ed > 0f)
            {
                Room end = AddRoom(l, endKind.Value, x0, d - ed, x1, d);
                AddDoor(l, corridor, end, new Vec3(0f, 0f, d - ed), true, Math.Max(InteriorGenerator.DoorWidth, Math.Min(cw - 0.2f, 1.6f)));
            }
        }

        private static void SideColumn(InteriorLayout l, Room corridor, List<Req> rooms, float xa, float xb, float z0, float z1, float doorX)
        {
            float[] depths = Split(z1 - z0, rooms, InteriorGenerator.MinRoomSide);
            float z = z0;
            for (int i = 0; i < rooms.Count; i++)
            {
                float ze = i == rooms.Count - 1 ? z1 : z + depths[i];
                Room r = AddRoom(l, rooms[i].Kind, xa, z, xb, ze);
                AddDoor(l, corridor, r, new Vec3(doorX, 0f, (z + ze) / 2f), false, InteriorGenerator.DoorWidth);
                z = ze;
            }
        }

        /// <summary>Splits a length among rooms by weight, each at least <paramref name="min"/> (when possible), on a 0.5 m grid.</summary>
        private static float[] Split(float length, List<Req> rooms, float min)
        {
            int n = rooms.Count;
            var sizes = new float[n];
            float total = 0f;
            foreach (Req r in rooms) total += r.Weight;
            if (length < min * n)
            {
                for (int i = 0; i < n; i++) sizes[i] = length / n;
                return sizes;
            }
            float used = 0f;
            for (int i = 0; i < n; i++)
            {
                sizes[i] = Math.Max(min, Snap(length * rooms[i].Weight / total));
                used += sizes[i];
            }
            // Take any excess back from the largest rooms, never below min.
            float excess = used - length;
            while (excess > 1e-3f)
            {
                int big = 0;
                for (int i = 1; i < n; i++) if (sizes[i] > sizes[big]) big = i;
                float take = Math.Min(excess, sizes[big] - min);
                if (take <= 1e-4f) break;
                sizes[big] -= take;
                excess -= take;
            }
            return sizes;
        }

        private static float Snap(float v)
        {
            return (float)Math.Round(v * 2f) / 2f;
        }
    }
}
