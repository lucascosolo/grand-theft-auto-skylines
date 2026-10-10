using System;
using System.Collections.Generic;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.Interiors
{
    /// <summary>
    /// A generated interior in interior-local metres: the floor is y = 0; the entrance door is centred on x = 0 in the
    /// front wall, whose centre line is z = 0; the interior extends over x ∈ [−Width/2, Width/2], z ∈ [0, Depth]
    /// (wall centre lines), so +z points into the building from the street door. Everything is deterministic for a
    /// given building descriptor, archetype and variant.
    /// </summary>
    public sealed class InteriorLayout
    {
        /// <summary>Wall thickness, metres.</summary>
        public const float WallThickness = 0.15f;

        /// <summary>Height of open doorways, metres.</summary>
        public const float DoorHeight = 2.2f;

        /// <summary>Building the interior belongs to.</summary>
        public ushort BuildingId;

        /// <summary>Kind of interior.</summary>
        public InteriorArchetype Archetype;

        /// <summary>Variant number (0 for the street-level interior; apartments use the floor number).</summary>
        public int Variant;

        /// <summary>Extent along x between the outer wall centre lines, metres (8-40).</summary>
        public float Width;

        /// <summary>Extent along z between the outer wall centre lines, metres (8-40).</summary>
        public float Depth;

        /// <summary>Floor to ceiling, metres.</summary>
        public float CeilingHeight;

        /// <summary>Rooms; they tile the footprint without overlapping. Room 0 is the room the entrance opens into.</summary>
        public readonly List<Room> Rooms = new List<Room>();

        /// <summary>Openings between rooms; every room is reachable from room 0 through them.</summary>
        public readonly List<Doorway> Doorways = new List<Doorway>();

        /// <summary>Doors out of the interior: exactly one <see cref="PortalKind.Entrance"/>, at most one elevator.</summary>
        public readonly List<Portal> Portals = new List<Portal>();

        /// <summary>Furniture.</summary>
        public readonly List<FurnitureItem> Furniture = new List<FurnitureItem>();

        /// <summary>Interaction points, including one <see cref="InteractionKind.ExitDoor"/> in front of the entrance.</summary>
        public readonly List<InteractionPoint> Interactions = new List<InteractionPoint>();

        /// <summary>NPC spots.</summary>
        public readonly List<NpcSpot> NpcSpots = new List<NpcSpot>();

        /// <summary>Where the player appears when coming in through the entrance (on the floor, ~1.2 m inside).</summary>
        public Vec3 EntranceSpawn;

        /// <summary>Facing at <see cref="EntranceSpawn"/>, degrees (into the room: 0).</summary>
        public float EntranceSpawnYawDeg;

        /// <summary>Where the player appears when arriving by elevator (meaningful when an elevator portal exists).</summary>
        public Vec3 ElevatorSpawn;

        /// <summary>Facing at <see cref="ElevatorSpawn"/>, degrees.</summary>
        public float ElevatorSpawnYawDeg;

        /// <summary>Texture recipes referenced by rooms and mesh parts (at most 32, no duplicates).</summary>
        public readonly List<TextureRecipe> Recipes = new List<TextureRecipe>();

        /// <summary>Render geometry, grouped by recipe.</summary>
        public readonly List<MeshPart> MeshParts = new List<MeshPart>();

        /// <summary>
        /// Collision triangles, 9 floats each (a, b, c), interior-local: floor, ceiling, every wall face (closed at the
        /// entrance and elevator doors, open at doorways), and every solid furniture box. Normals
        /// (<c>Cross(b − a, c − a)</c>) point into walkable space.
        /// </summary>
        public float[] CollisionTriangles = new float[0];

        /// <summary>Number of collision triangles.</summary>
        public int CollisionTriangleCount { get { return CollisionTriangles.Length / 9; } }

        /// <summary>Walkability grid for the player and NPCs (0.25 m cells, 0.35 m clearance).</summary>
        public InteriorNavGrid Nav;

        /// <summary>
        /// A 32-bit hash of the whole content (rooms, doorways, portals, furniture, interactions, NPC spots, recipes,
        /// mesh and collision data), for determinism checks.
        /// </summary>
        public uint ContentHash()
        {
            var h = new Fnv();
            h.Add((int)Archetype); h.Add(Variant); h.Add(BuildingId);
            h.Add(Width); h.Add(Depth); h.Add(CeilingHeight);
            foreach (Room r in Rooms)
            {
                h.Add((int)r.Kind); h.Add(r.Bounds); h.Add(r.FloorRecipe); h.Add(r.WallRecipe);
            }
            foreach (Doorway d in Doorways)
            {
                h.Add(d.RoomA); h.Add(d.RoomB); h.Add(d.Center); h.Add(d.WallAlongX ? 1 : 0); h.Add(d.Width); h.Add(d.Height);
            }
            foreach (Portal p in Portals)
            {
                h.Add((int)p.Kind); h.Add(p.Position); h.Add(p.InwardYawDeg); h.Add(p.Room);
            }
            foreach (FurnitureItem f in Furniture)
            {
                h.Add((int)f.Kind); h.Add(f.Position); h.Add(f.Size); h.Add(f.YawDeg); h.Add(f.Room); h.Add(f.Solid ? 1 : 0); h.Add(f.Footprint);
            }
            foreach (InteractionPoint i in Interactions)
            {
                h.Add((int)i.Kind); h.Add(i.Position); h.Add(i.YawDeg); h.Add(i.Room); h.Add((int)i.Shop);
            }
            foreach (NpcSpot n in NpcSpots)
            {
                h.Add((int)n.Role); h.Add(n.Position); h.Add(n.YawDeg); h.Add(n.Room); h.Add(n.Seated ? 1 : 0);
            }
            h.Add(EntranceSpawn); h.Add(EntranceSpawnYawDeg); h.Add(ElevatorSpawn); h.Add(ElevatorSpawnYawDeg);
            foreach (TextureRecipe t in Recipes)
            {
                h.Add((int)t.Pattern); h.Add(t.BaseRgb); h.Add(t.AccentRgb); h.Add(t.RepeatMetres); h.Add(t.Emissive ? 1 : 0);
            }
            foreach (MeshPart m in MeshParts)
            {
                h.Add((int)m.Slot); h.Add(m.Recipe);
                h.Add(m.Positions); h.Add(m.Normals); h.Add(m.Uvs);
                for (int i = 0; i < m.Indices.Length; i++) h.Add(m.Indices[i]);
            }
            h.Add(CollisionTriangles);
            return h.Value;
        }

        private struct Fnv
        {
            private uint _h;
            private bool _started;

            public uint Value { get { return _started ? _h : 2166136261u; } }

            public void Add(int v)
            {
                if (!_started) { _h = 2166136261u; _started = true; }
                for (int k = 0; k < 4; k++)
                {
                    _h ^= (uint)(v >> (8 * k)) & 0xFFu;
                    _h *= 16777619u;
                }
            }

            public void Add(float f)
            {
                long bits = BitConverter.DoubleToInt64Bits(f);
                Add((int)bits);
                Add((int)(bits >> 32));
            }

            public void Add(Vec3 v) { Add(v.X); Add(v.Y); Add(v.Z); }

            public void Add(Rect2 r) { Add(r.MinX); Add(r.MinZ); Add(r.MaxX); Add(r.MaxZ); }

            public void Add(float[] a)
            {
                Add(a.Length);
                for (int i = 0; i < a.Length; i++) Add(a[i]);
            }
        }

        /// <summary>The room containing the point's x, z, or −1.</summary>
        public int RoomAt(float x, float z)
        {
            for (int i = 0; i < Rooms.Count; i++)
            {
                if (Rooms[i].Bounds.Contains(x, z)) return i;
            }
            // Points exactly on the far outer walls (max edges) still belong to the room there.
            for (int i = 0; i < Rooms.Count; i++)
            {
                Rect2 b = Rooms[i].Bounds;
                if (x >= b.MinX && x <= b.MaxX && z >= b.MinZ && z <= b.MaxZ) return i;
            }
            return -1;
        }

        /// <summary>Maps an interior-local point to world space for an interior placed at <paramref name="origin"/> turned by <paramref name="yawDeg"/>.</summary>
        public static Vec3 ToWorld(Vec3 local, Vec3 origin, float yawDeg)
        {
            return origin + Quat.Yaw(yawDeg) * local;
        }

        /// <summary>Inverse of <see cref="ToWorld"/>.</summary>
        public static Vec3 ToLocal(Vec3 world, Vec3 origin, float yawDeg)
        {
            return Quat.Yaw(-yawDeg) * (world - origin);
        }
    }

    /// <summary>
    /// Walkable cells of an interior on a 0.25 m grid: a cell is free when its centre is inside a room, at least
    /// <see cref="Clearance"/> from every wall face and solid furniture footprint (doorways counted as open).
    /// </summary>
    public sealed class InteriorNavGrid
    {
        /// <summary>Cell edge, metres.</summary>
        public const float CellSize = 0.25f;

        /// <summary>Required distance from obstacles, metres (the player's radius).</summary>
        public const float Clearance = 0.35f;

        private readonly bool[] _free;
        private int[] _stamp;
        private int _stampValue;
        private int[] _queue;
        private float[] _cost;
        private int[] _parent;

        /// <summary>Cells along x.</summary>
        public int CellsX { get; private set; }

        /// <summary>Cells along z.</summary>
        public int CellsZ { get; private set; }

        /// <summary>Interior-local x of the grid's minimum corner.</summary>
        public float OriginX { get; private set; }

        /// <summary>Interior-local z of the grid's minimum corner.</summary>
        public float OriginZ { get; private set; }

        /// <summary>Creates an empty grid covering the rectangle; every cell blocked.</summary>
        public InteriorNavGrid(Rect2 area)
        {
            OriginX = area.MinX;
            OriginZ = area.MinZ;
            CellsX = Math.Max(1, (int)Math.Ceiling(Math.Max(0f, area.Width) / CellSize - 1e-4));
            CellsZ = Math.Max(1, (int)Math.Ceiling(Math.Max(0f, area.Depth) / CellSize - 1e-4));
            _free = new bool[CellsX * CellsZ];
        }

        /// <summary>True when the cell containing the point is free; false outside the grid.</summary>
        public bool IsFree(float x, float z)
        {
            int cx, cz;
            return CellOf(x, z, out cx, out cz) && _free[cz * CellsX + cx];
        }

        /// <summary>Marks the cell containing the point free or blocked (no-op outside the grid).</summary>
        public void Set(float x, float z, bool free)
        {
            int cx, cz;
            if (CellOf(x, z, out cx, out cz)) _free[cz * CellsX + cx] = free;
        }

        /// <summary>Number of free cells.</summary>
        public int FreeCount()
        {
            int n = 0;
            for (int i = 0; i < _free.Length; i++) if (_free[i]) n++;
            return n;
        }

        /// <summary>
        /// The nearest free cell centre to a point within <paramref name="maxDistance"/> m; false when there is none.
        /// </summary>
        public bool NearestFree(Vec3 point, float maxDistance, out Vec3 free)
        {
            free = point;
            if (!(maxDistance >= 0f)) return false;
            int r = (int)Math.Ceiling(maxDistance / CellSize) + 1;
            int px = (int)Math.Floor((point.X - OriginX) / CellSize), pz = (int)Math.Floor((point.Z - OriginZ) / CellSize);
            float best = float.MaxValue;
            bool found = false;
            for (int cz = Math.Max(0, pz - r); cz <= Math.Min(CellsZ - 1, pz + r); cz++)
            {
                for (int cx = Math.Max(0, px - r); cx <= Math.Min(CellsX - 1, px + r); cx++)
                {
                    if (!_free[cz * CellsX + cx]) continue;
                    float x = CentreX(cx), z = CentreZ(cz);
                    float dx = x - point.X, dz = z - point.Z;
                    float d2 = dx * dx + dz * dz;
                    if (d2 <= maxDistance * maxDistance + 1e-6f && d2 < best)
                    {
                        best = d2;
                        free = new Vec3(x, 0f, z);
                        found = true;
                    }
                }
            }
            return found;
        }

        /// <summary>True when a path of free cells (8-connected, no corner cutting) joins the cells of two points.</summary>
        public bool Connected(Vec3 a, Vec3 b)
        {
            int ax, az, bx, bz;
            if (!CellOf(a.X, a.Z, out ax, out az) || !CellOf(b.X, b.Z, out bx, out bz)) return false;
            int start = az * CellsX + ax, goal = bz * CellsX + bx;
            if (!_free[start] || !_free[goal]) return false;
            if (start == goal) return true;
            Flood(start);
            return _stamp[goal] == _stampValue;
        }

        /// <summary>Marks every cell reachable from <paramref name="start"/> with the current stamp.</summary>
        private void Flood(int start)
        {
            EnsureScratch();
            _stampValue++;
            int head = 0, tail = 0;
            _queue[tail++] = start;
            _stamp[start] = _stampValue;
            while (head < tail)
            {
                int c = _queue[head++];
                int cx = c % CellsX, cz = c / CellsX;
                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int n = Step(cx, cz, dx, dz);
                        if (n < 0 || _stamp[n] == _stampValue) continue;
                        _stamp[n] = _stampValue;
                        _queue[tail++] = n;
                    }
                }
            }
        }

        /// <summary>True when every point is in a free cell connected to <paramref name="from"/>'s cell.</summary>
        internal bool AllConnected(Vec3 from, System.Collections.Generic.IList<Vec3> targets, System.Collections.Generic.List<int> failed)
        {
            failed.Clear();
            int fx, fz;
            if (!CellOf(from.X, from.Z, out fx, out fz) || !_free[fz * CellsX + fx])
            {
                for (int i = 0; i < targets.Count; i++) failed.Add(i);
                return false;
            }
            Flood(fz * CellsX + fx);
            for (int i = 0; i < targets.Count; i++)
            {
                int tx, tz;
                if (!CellOf(targets[i].X, targets[i].Z, out tx, out tz) || _stamp[tz * CellsX + tx] != _stampValue) failed.Add(i);
            }
            return failed.Count == 0;
        }

        /// <summary>
        /// A* path of free cell centres from <paramref name="from"/> to <paramref name="to"/> (both snapped to the
        /// nearest free cell within 1 m), simplified to straight runs; written into <paramref name="into"/> (cleared
        /// first), floor height 0. False and an empty list when there is no path.
        /// </summary>
        public bool FindPath(Vec3 from, Vec3 to, List<Vec3> into)
        {
            into.Clear();
            Vec3 s, g;
            if (!NearestFree(from, 1f, out s) || !NearestFree(to, 1f, out g)) return false;
            int sx, sz, gx, gz;
            CellOf(s.X, s.Z, out sx, out sz);
            CellOf(g.X, g.Z, out gx, out gz);
            int start = sz * CellsX + sx, goal = gz * CellsX + gx;
            EnsureScratch();
            _stampValue++;
            // Open list as a simple binary heap of (f, cell).
            var heapF = new List<float>();
            var heapC = new List<int>();
            _cost[start] = 0f;
            _parent[start] = -1;
            _stamp[start] = _stampValue;
            HeapPush(heapF, heapC, Heuristic(sx, sz, gx, gz), start);
            bool reached = start == goal;
            while (heapC.Count > 0 && !reached)
            {
                float f; int c;
                HeapPop(heapF, heapC, out f, out c);
                int cx = c % CellsX, cz = c / CellsX;
                if (f > _cost[c] + Heuristic(cx, cz, gx, gz) + 1e-4f) continue; // stale entry
                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int n = Step(cx, cz, dx, dz);
                        if (n < 0) continue;
                        float ng = _cost[c] + (dx != 0 && dz != 0 ? 1.41421356f : 1f);
                        if (_stamp[n] == _stampValue && ng >= _cost[n] - 1e-5f) continue;
                        _stamp[n] = _stampValue;
                        _cost[n] = ng;
                        _parent[n] = c;
                        if (n == goal) { reached = true; break; }
                        HeapPush(heapF, heapC, ng + Heuristic(n % CellsX, n / CellsX, gx, gz), n);
                    }
                    if (reached) break;
                }
            }
            if (!reached) return false;
            var cells = new List<int>();
            for (int c = goal; c >= 0; c = _parent[c])
            {
                cells.Add(c);
                if (c == start) break;
            }
            cells.Reverse();
            // Keep the endpoints and every cell where the step direction changes.
            for (int i = 0; i < cells.Count; i++)
            {
                if (i > 0 && i < cells.Count - 1)
                {
                    int a = cells[i - 1], b = cells[i], c = cells[i + 1];
                    int d1x = b % CellsX - a % CellsX, d1z = b / CellsX - a / CellsX;
                    int d2x = c % CellsX - b % CellsX, d2z = c / CellsX - b / CellsX;
                    if (d1x == d2x && d1z == d2z) continue;
                }
                into.Add(new Vec3(CentreX(cells[i] % CellsX), 0f, CentreZ(cells[i] / CellsX)));
            }
            return true;
        }

        /// <summary>Marks free (or blocked) every cell whose centre lies in the rectangle (min inclusive, max exclusive).</summary>
        internal void FillRect(Rect2 r, bool free)
        {
            int x0 = Math.Max(0, (int)Math.Ceiling((r.MinX - OriginX) / CellSize - 0.5f - 1e-5f));
            int z0 = Math.Max(0, (int)Math.Ceiling((r.MinZ - OriginZ) / CellSize - 0.5f - 1e-5f));
            int x1 = Math.Min(CellsX - 1, (int)Math.Ceiling((r.MaxX - OriginX) / CellSize - 0.5f - 1e-5f) - 1);
            int z1 = Math.Min(CellsZ - 1, (int)Math.Ceiling((r.MaxZ - OriginZ) / CellSize - 0.5f - 1e-5f) - 1);
            for (int cz = z0; cz <= z1; cz++)
            {
                for (int cx = x0; cx <= x1; cx++)
                {
                    float x = CentreX(cx), z = CentreZ(cz);
                    if (x >= r.MinX && x < r.MaxX && z >= r.MinZ && z < r.MaxZ) _free[cz * CellsX + cx] = free;
                }
            }
        }

        /// <summary>Interior-local x of a cell column's centre.</summary>
        internal float CentreX(int cx) { return OriginX + (cx + 0.5f) * CellSize; }

        /// <summary>Interior-local z of a cell row's centre.</summary>
        internal float CentreZ(int cz) { return OriginZ + (cz + 0.5f) * CellSize; }

        private bool CellOf(float x, float z, out int cx, out int cz)
        {
            cx = (int)Math.Floor((x - OriginX) / CellSize);
            cz = (int)Math.Floor((z - OriginZ) / CellSize);
            return cx >= 0 && cz >= 0 && cx < CellsX && cz < CellsZ;
        }

        /// <summary>The neighbour cell index, or −1 when it is outside, blocked, or a diagonal that cuts a corner.</summary>
        private int Step(int cx, int cz, int dx, int dz)
        {
            int nx = cx + dx, nz = cz + dz;
            if (nx < 0 || nz < 0 || nx >= CellsX || nz >= CellsZ) return -1;
            if (!_free[nz * CellsX + nx]) return -1;
            if (dx != 0 && dz != 0 && (!_free[cz * CellsX + nx] || !_free[nz * CellsX + cx])) return -1;
            return nz * CellsX + nx;
        }

        private static float Heuristic(int ax, int az, int bx, int bz)
        {
            int dx = Math.Abs(ax - bx), dz = Math.Abs(az - bz);
            int mn = Math.Min(dx, dz), mx = Math.Max(dx, dz);
            return mx - mn + mn * 1.41421356f;
        }

        private void EnsureScratch()
        {
            if (_stamp != null) return;
            int n = _free.Length;
            _stamp = new int[n];
            _queue = new int[n];
            _cost = new float[n];
            _parent = new int[n];
        }

        private static void HeapPush(List<float> f, List<int> c, float key, int cell)
        {
            f.Add(key); c.Add(cell);
            int i = f.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (f[p] <= f[i]) break;
                float tf = f[p]; f[p] = f[i]; f[i] = tf;
                int tc = c[p]; c[p] = c[i]; c[i] = tc;
                i = p;
            }
        }

        private static void HeapPop(List<float> f, List<int> c, out float key, out int cell)
        {
            key = f[0]; cell = c[0];
            int last = f.Count - 1;
            f[0] = f[last]; c[0] = c[last];
            f.RemoveAt(last); c.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = 2 * i + 1, r = l + 1, m = i;
                if (l < f.Count && f[l] < f[m]) m = l;
                if (r < f.Count && f[r] < f[m]) m = r;
                if (m == i) break;
                float tf = f[m]; f[m] = f[i]; f[i] = tf;
                int tc = c[m]; c[m] = c[i]; c[i] = tc;
                i = m;
            }
        }
    }
}
