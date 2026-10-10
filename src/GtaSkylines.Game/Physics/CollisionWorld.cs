using System;
using System.Collections.Generic;
using GtaSkylines.Game.Mathx;
using Skylines.Core.Geometry;

namespace GtaSkylines.Game.Physics
{
    /// <summary>
    /// The Game's collision scene in CS1 world space (metres, y up, left-handed).
    /// <para>
    /// Static geometry is a set of triangle regions keyed by a caller-chosen <c>long</c> (the Mod streams one region per
    /// map area; G3 interiors use their own key). Triangles are copied in, double-sided, and indexed in a uniform grid of
    /// <see cref="CellSize"/> m cells on x and z (unbounded in y and in extent). Regions may overlap; queries see the
    /// union. Degenerate (zero-area) or non-finite triangles are stored but never hit.
    /// </para>
    /// <para>
    /// Dynamic geometry is oriented boxes and vertical capsules, each with an int tag (CS1 vehicle or citizen id, our
    /// vehicle id), cleared and re-added every frame.
    /// </para>
    /// <para>
    /// Queries return the nearest hit only and allocate nothing in steady state (internal buffers grow to the largest
    /// query seen and are reused). Ties between equally near hits resolve arbitrarily. An empty world hits nothing. Not
    /// thread-safe: one thread owns the instance.
    /// </para>
    /// </summary>
    public sealed class CollisionWorld
    {
        /// <summary>Grid cell edge on x and z, metres.</summary>
        public const float CellSize = 4f;

        private const float CellMargin = 1e-3f;

        /// <summary>Triangles spanning more grid cells than this skip the grid and go to the oversize list.</summary>
        private const long MaxGridCells = 256;

        private static readonly int[] BoxTris = { 0, 4, 6, 0, 6, 2, 1, 3, 7, 1, 7, 5, 0, 1, 5, 0, 5, 4, 2, 6, 7, 2, 7, 3, 0, 2, 3, 0, 3, 1, 4, 5, 7, 4, 7, 6 };

        private struct Tri
        {
            public Vec3 A, B, C, N;
            public SurfaceFlags F;
            public bool Valid;
        }

        private sealed class Region
        {
            public Tri[] Tris;
            public int[] Stamps;
            public int Count;
        }

        private struct Entry
        {
            public Region R;
            public int I;
        }

        private sealed class Cell
        {
            public Entry[] Items = new Entry[4];
            public int Count;

            public void Add(Region r, int i)
            {
                if (Count == Items.Length) Array.Resize(ref Items, Count * 2);
                Items[Count].R = r;
                Items[Count].I = i;
                Count++;
            }

            public void RemoveRegion(Region r)
            {
                int w = 0;
                for (int k = 0; k < Count; k++)
                {
                    if (Items[k].R != r) Items[w++] = Items[k];
                }
                for (int k = w; k < Count; k++) Items[k] = default(Entry);
                Count = w;
            }
        }

        /// <summary>A triangle too large for the grid, tested by every query whose bounds overlap its footprint.</summary>
        private struct Oversize
        {
            public Region R;
            public int I;
            public float MinX, MinZ, MaxX, MaxZ;
        }

        private struct BoxShape
        {
            public Vec3 C, H;
            public Quat R, Inv;
            public int Tag;
        }

        private struct CapShape
        {
            public Vec3 Feet;
            public float Radius, Height;
            public int Tag;
        }

        private enum Mode { Ray, Sweep, Overlap }

        private readonly Dictionary<long, Region> _regions = new Dictionary<long, Region>();
        private readonly Dictionary<long, Cell> _cells = new Dictionary<long, Cell>();
        private int _triangleCount;
        private Oversize[] _oversize = new Oversize[4];
        private int _oversizeCount;
        private BoxShape[] _boxes = new BoxShape[8];
        private int _boxCount;
        private CapShape[] _caps = new CapShape[8];
        private int _capCount;
        private int _stamp;
        private readonly Vec3[] _corners = new Vec3[8];

        // State of the query in progress (one at a time; the world is single-threaded).
        private Mode _mode;
        private Vec3 _o, _d;
        private float _len, _r, _maxT;
        private QueryMask _mask;
        private bool _has;
        private float _bestT;
        private Vec3 _bestP, _bestN;
        private SurfaceFlags _bestF;
        private int _bestTag;
        private HitKind _bestKind;
        private List<Contact> _into;
        private bool _hasDeep;
        private Contact _deep;

        /// <summary>Number of static regions.</summary>
        public int RegionCount
        {
            get { return _regions.Count; }
        }

        /// <summary>Total static triangles across all regions (degenerate ones included).</summary>
        public int TriangleCount
        {
            get { return _triangleCount; }
        }

        /// <summary>Number of dynamic boxes plus capsules currently present.</summary>
        public int DynamicCount
        {
            get { return _boxCount + _capCount; }
        }

        /// <summary>
        /// Adds or replaces the static region <paramref name="key"/> with a copy of <paramref name="triangleCount"/>
        /// triangles: <paramref name="positions"/> holds nine floats per triangle (a.x, a.y, a.z, b.x, …, c.z) and
        /// <paramref name="flags"/> one <see cref="SurfaceFlags"/> value per triangle; only the first
        /// <c>9 * triangleCount</c> / <c>triangleCount</c> entries are read. The arrays are not retained, so the caller may
        /// reuse them immediately. A count of 0 stores an empty region (it still counts in <see cref="RegionCount"/>).
        /// Replacing a region removes all of its previous triangles from every grid cell they occupied.
        /// </summary>
        /// <exception cref="ArgumentNullException">An array is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="triangleCount"/> is negative or the arrays are shorter than it requires.</exception>
        public void SetRegion(long key, float[] positions, ushort[] flags, int triangleCount)
        {
            if (positions == null) throw new ArgumentNullException("positions");
            if (flags == null) throw new ArgumentNullException("flags");
            if (triangleCount < 0 || positions.Length < 9L * triangleCount || flags.Length < triangleCount)
            {
                throw new ArgumentOutOfRangeException("triangleCount");
            }
            RemoveRegion(key);
            var rg = new Region { Tris = new Tri[triangleCount], Stamps = new int[triangleCount], Count = triangleCount };
            for (int i = 0; i < triangleCount; i++)
            {
                int o = i * 9;
                Vec3 a = new Vec3(positions[o], positions[o + 1], positions[o + 2]);
                Vec3 b = new Vec3(positions[o + 3], positions[o + 4], positions[o + 5]);
                Vec3 c = new Vec3(positions[o + 6], positions[o + 7], positions[o + 8]);
                Vec3 n = Vec3.Cross(b - a, c - a);
                float area2 = n.Length;
                bool valid = a.IsFinite && b.IsFinite && c.IsFinite && area2 > 1e-9f && !float.IsInfinity(area2);
                rg.Tris[i] = new Tri { A = a, B = b, C = c, N = valid ? n / area2 : Vec3.Zero, F = (SurfaceFlags)flags[i], Valid = valid };
                if (!valid) continue;
                int x0, x1, z0, z1;
                float minX = Math.Min(a.X, Math.Min(b.X, c.X)), minZ = Math.Min(a.Z, Math.Min(b.Z, c.Z));
                float maxX = Math.Max(a.X, Math.Max(b.X, c.X)), maxZ = Math.Max(a.Z, Math.Max(b.Z, c.Z));
                CellRange(minX, minZ, maxX, maxZ, -1e-4f, out x0, out z0, out x1, out z1);
                if (IsOversize(x0, z0, x1, z1))
                {
                    if (_oversizeCount == _oversize.Length) Array.Resize(ref _oversize, _oversizeCount * 2);
                    _oversize[_oversizeCount++] = new Oversize { R = rg, I = i, MinX = minX, MinZ = minZ, MaxX = maxX, MaxZ = maxZ };
                    continue;
                }
                for (int cx = x0; cx <= x1; cx++)
                {
                    for (int cz = z0; cz <= z1; cz++)
                    {
                        long k = Key(cx, cz);
                        Cell cell;
                        if (!_cells.TryGetValue(k, out cell))
                        {
                            cell = new Cell();
                            _cells.Add(k, cell);
                        }
                        cell.Add(rg, i);
                    }
                }
            }
            _regions[key] = rg;
            _triangleCount += triangleCount;
        }

        /// <summary>Same as <see cref="SetRegion(long, float[], ushort[], int)"/> with the buffer's arrays and count.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="triangles"/> is null.</exception>
        public void SetRegion(long key, TriangleBuffer triangles)
        {
            if (triangles == null) throw new ArgumentNullException("triangles");
            SetRegion(key, triangles.Positions, triangles.Flags, triangles.Count);
        }

        /// <summary>Removes a static region and its triangles; false when no region has that key.</summary>
        public bool RemoveRegion(long key)
        {
            Region rg;
            if (!_regions.TryGetValue(key, out rg)) return false;
            for (int i = 0; i < rg.Count; i++)
            {
                if (!rg.Tris[i].Valid) continue;
                Tri t = rg.Tris[i];
                int x0, x1, z0, z1;
                CellRange(Math.Min(t.A.X, Math.Min(t.B.X, t.C.X)), Math.Min(t.A.Z, Math.Min(t.B.Z, t.C.Z)),
                    Math.Max(t.A.X, Math.Max(t.B.X, t.C.X)), Math.Max(t.A.Z, Math.Max(t.B.Z, t.C.Z)), -1e-4f, out x0, out z0, out x1, out z1);
                if (IsOversize(x0, z0, x1, z1)) continue;
                for (int cx = x0; cx <= x1; cx++)
                {
                    for (int cz = z0; cz <= z1; cz++)
                    {
                        long k = Key(cx, cz);
                        Cell cell;
                        if (!_cells.TryGetValue(k, out cell)) continue;
                        cell.RemoveRegion(rg);
                        if (cell.Count == 0) _cells.Remove(k);
                    }
                }
            }
            int w = 0;
            for (int i = 0; i < _oversizeCount; i++)
            {
                if (_oversize[i].R != rg) _oversize[w++] = _oversize[i];
            }
            for (int i = w; i < _oversizeCount; i++) _oversize[i] = default(Oversize);
            _oversizeCount = w;
            _regions.Remove(key);
            _triangleCount -= rg.Count;
            return true;
        }

        /// <summary>True when a static region with this key exists.</summary>
        public bool HasRegion(long key)
        {
            return _regions.ContainsKey(key);
        }

        /// <summary>Removes every static region and every dynamic shape.</summary>
        public void Clear()
        {
            _regions.Clear();
            _cells.Clear();
            Array.Clear(_oversize, 0, _oversizeCount);
            _oversizeCount = 0;
            _triangleCount = 0;
            ClearDynamic();
        }

        /// <summary>Removes every dynamic box and capsule (static regions stay). Call once per frame before re-adding them.</summary>
        public void ClearDynamic()
        {
            _boxCount = 0;
            _capCount = 0;
        }

        /// <summary>
        /// Adds a dynamic oriented box: <paramref name="center"/> in world space, <paramref name="halfExtents"/> along the
        /// box's local x, y, z axes (each &gt; 0), rotated by <paramref name="rotation"/> (normalised internally). Tags
        /// need not be unique.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">A half extent is not a finite value &gt; 0.</exception>
        public void AddBox(Vec3 center, Vec3 halfExtents, Quat rotation, int tag)
        {
            if (!halfExtents.IsFinite || !(halfExtents.X > 0f) || !(halfExtents.Y > 0f) || !(halfExtents.Z > 0f))
            {
                throw new ArgumentOutOfRangeException("halfExtents");
            }
            if (_boxCount == _boxes.Length) Array.Resize(ref _boxes, _boxCount * 2);
            Quat q = rotation.Normalized;
            _boxes[_boxCount++] = new BoxShape { C = center, H = halfExtents, R = q, Inv = q.Inverse, Tag = tag };
        }

        /// <summary>
        /// Adds a dynamic vertical capsule standing on <paramref name="feet"/> (its lowest point): axis from
        /// <c>feet + (0, radius, 0)</c> to <c>feet + (0, height − radius, 0)</c>, total height <paramref name="height"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> is not &gt; 0, or <paramref name="height"/> &lt; 2 × radius.</exception>
        public void AddCapsule(Vec3 feet, float radius, float height, int tag)
        {
            CheckCapsule(radius, height);
            if (_capCount == _caps.Length) Array.Resize(ref _caps, _capCount * 2);
            _caps[_capCount++] = new CapShape { Feet = feet, Radius = radius, Height = height, Tag = tag };
        }

        /// <summary>
        /// Nearest hit of the ray from <paramref name="origin"/> along <paramref name="direction"/> (any non-zero length;
        /// normalised internally) within <paramref name="maxDistance"/> m. A hit at exactly maxDistance counts. A ray that
        /// starts inside a dynamic shape hits it at distance 0 with normal <c>−direction</c>; a ray starting on a triangle
        /// hits it at distance 0. Returns false (and <c>default</c> hit) for a zero direction, maxDistance ≤ 0 or
        /// non-finite, or no hit.
        /// </summary>
        public bool Raycast(Vec3 origin, Vec3 direction, float maxDistance, QueryMask mask, out PhysicsHit hit)
        {
            hit = default(PhysicsHit);
            float dl = direction.Length;
            if (!(maxDistance > 0f) || float.IsInfinity(maxDistance) || !(dl > 1e-9f) || float.IsInfinity(dl) || !origin.IsFinite) return false;
            Begin(Mode.Ray, origin, direction / dl, 0f, 0f, maxDistance, mask, null);
            if (!mask.SkipStatic) TraverseStatic();
            if (!mask.SkipDynamic) RayDynamic();
            return Finish(out hit);
        }

        /// <summary>
        /// Sweeps a sphere of <paramref name="radius"/> whose centre starts at <paramref name="origin"/> along
        /// <paramref name="direction"/> (normalised internally) for up to <paramref name="maxDistance"/> m and returns the
        /// first contact: <see cref="PhysicsHit.Distance"/> is how far the centre travelled, <see cref="PhysicsHit.Point"/>
        /// the touched surface point, <see cref="PhysicsHit.Normal"/> from that point toward the centre at contact.
        /// <para>
        /// A sphere that already penetrates something at <paramref name="origin"/> (depth &gt; 0, as
        /// <see cref="OverlapSphere"/> defines it) returns true with distance 0 and the point and normal of the deepest
        /// contact. A zero direction or maxDistance of 0 is therefore an overlap test. Merely touching (depth 0) at the
        /// start is not a hit unless the sweep moves into the surface. Continuous: no thickness of geometry is skipped at
        /// any distance. Returns false for maxDistance &lt; 0 or non-finite.
        /// </para>
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> is not a finite value &gt; 0.</exception>
        public bool SphereCast(Vec3 origin, float radius, Vec3 direction, float maxDistance, QueryMask mask, out PhysicsHit hit)
        {
            CheckRadius(radius);
            return Cast(origin, 0f, radius, direction, maxDistance, mask, out hit);
        }

        /// <summary>
        /// Sweeps a vertical capsule (as <see cref="AddCapsule"/> defines it: lowest point <paramref name="feet"/>, total
        /// <paramref name="height"/>) along <paramref name="direction"/> for up to <paramref name="maxDistance"/> m. The
        /// capsule stays vertical; <see cref="PhysicsHit.Distance"/> is how far <paramref name="feet"/> travelled and
        /// <see cref="PhysicsHit.Normal"/> points from the contact toward the capsule's axis. Start-overlap, zero-length,
        /// continuity and invalid-distance rules are those of <see cref="SphereCast"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> is not &gt; 0, or <paramref name="height"/> &lt; 2 × radius.</exception>
        public bool CapsuleCast(Vec3 feet, float radius, float height, Vec3 direction, float maxDistance, QueryMask mask, out PhysicsHit hit)
        {
            CheckCapsule(radius, height);
            return Cast(new Vec3(feet.X, feet.Y + radius, feet.Z), height - 2f * radius, radius, direction, maxDistance, mask, out hit);
        }

        /// <summary>
        /// Clears <paramref name="into"/>, then adds one <see cref="Contact"/> per triangle or dynamic shape the sphere
        /// penetrates with depth &gt; 0 (touching is not a contact), in no particular order, and returns the count. Adjacent
        /// triangles of one mesh each give their own contact. The list is never reallocated beyond its capacity growth.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="into"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> is not a finite value &gt; 0.</exception>
        public int OverlapSphere(Vec3 center, float radius, QueryMask mask, List<Contact> into)
        {
            if (into == null) throw new ArgumentNullException("into");
            CheckRadius(radius);
            return Overlap(center, 0f, radius, mask, into);
        }

        /// <summary>Contacts of the vertical capsule standing on <paramref name="feet"/>, as <see cref="OverlapSphere"/> reports them.</summary>
        internal int OverlapCapsule(Vec3 feet, float radius, float height, QueryMask mask, List<Contact> into)
        {
            return Overlap(new Vec3(feet.X, feet.Y + radius, feet.Z), height - 2f * radius, radius, mask, into);
        }

        // ------------------------------------------------------------------ query plumbing

        private static void CheckRadius(float radius)
        {
            if (!(radius > 0f) || float.IsInfinity(radius)) throw new ArgumentOutOfRangeException("radius");
        }

        private static void CheckCapsule(float radius, float height)
        {
            CheckRadius(radius);
            if (!(height >= 2f * radius) || float.IsInfinity(height)) throw new ArgumentOutOfRangeException("height");
        }

        private int Overlap(Vec3 s0, float len, float radius, QueryMask mask, List<Contact> into)
        {
            into.Clear();
            if (!s0.IsFinite) return 0;
            Begin(Mode.Overlap, s0, Vec3.Zero, len, radius, 0f, mask, into);
            if (!mask.SkipStatic) TraverseStatic();
            if (!mask.SkipDynamic) OverlapDynamic();
            _into = null;
            return into.Count;
        }

        private bool Cast(Vec3 s0, float len, float radius, Vec3 direction, float maxDistance, QueryMask mask, out PhysicsHit hit)
        {
            hit = default(PhysicsHit);
            if (!(maxDistance >= 0f) || float.IsInfinity(maxDistance) || !s0.IsFinite || !direction.IsFinite) return false;

            Begin(Mode.Overlap, s0, Vec3.Zero, len, radius, 0f, mask, null);
            if (!mask.SkipStatic) TraverseStatic();
            if (!mask.SkipDynamic) OverlapDynamic();
            if (_hasDeep)
            {
                hit = new PhysicsHit { Point = _deep.Point, Normal = _deep.Normal, Distance = 0f, Flags = _deep.Flags, Tag = _deep.Tag, Kind = _deep.Kind };
                return true;
            }

            float dl = direction.Length;
            if (!(dl > 1e-9f) || maxDistance == 0f) return false;
            Begin(Mode.Sweep, s0, direction / dl, len, radius, maxDistance, mask, null);
            if (!mask.SkipStatic) TraverseStatic();
            if (!mask.SkipDynamic) SweepDynamic();
            return Finish(out hit);
        }

        private void Begin(Mode mode, Vec3 o, Vec3 d, float len, float r, float maxT, QueryMask mask, List<Contact> into)
        {
            _mode = mode;
            _o = o;
            _d = d;
            _len = len;
            _r = r;
            _maxT = maxT;
            _mask = mask;
            _into = into;
            _has = false;
            _bestT = float.MaxValue;
            _hasDeep = false;
        }

        private bool Finish(out PhysicsHit hit)
        {
            if (!_has)
            {
                hit = default(PhysicsHit);
                return false;
            }
            hit = new PhysicsHit { Point = _bestP + _o, Normal = _bestN, Distance = _bestT, Flags = _bestF, Tag = _bestTag, Kind = _bestKind };
            return true;
        }

        private void Record(float t, Vec3 relPoint, Vec3 n, SurfaceFlags f, int tag, HitKind kind)
        {
            _has = true;
            _bestT = t;
            _bestP = relPoint;
            _bestN = n;
            _bestF = f;
            _bestTag = tag;
            _bestKind = kind;
        }

        private void AddContact(Vec3 relPoint, Vec3 n, float depth, SurfaceFlags f, int tag, HitKind kind)
        {
            var c = new Contact { Point = relPoint + _o, Normal = n, Depth = depth, Flags = f, Tag = tag, Kind = kind };
            if (_into != null) _into.Add(c);
            if (!_hasDeep || depth > _deep.Depth)
            {
                _deep = c;
                _hasDeep = true;
            }
        }

        private bool SkipTag(int tag)
        {
            return _mask.HasIgnoreTag && tag == _mask.IgnoreTag;
        }

        // ------------------------------------------------------------------ static grid

        private static long Key(int cx, int cz)
        {
            return ((long)cx << 32) | (uint)cz;
        }

        private static int CellOf(float v)
        {
            return (int)Math.Floor(v * (1f / CellSize));
        }

        private static void CellRange(float minX, float minZ, float maxX, float maxZ, float margin, out int x0, out int z0, out int x1, out int z1)
        {
            x0 = CellOf(minX - margin);
            z0 = CellOf(minZ - margin);
            x1 = CellOf(maxX + margin);
            z1 = CellOf(maxZ + margin);
            if (x1 < x0) x1 = x0;
            if (z1 < z0) z1 = z0;
        }

        private void NextStamp()
        {
            if (_stamp == int.MaxValue)
            {
                foreach (Region rg in _regions.Values) Array.Clear(rg.Stamps, 0, rg.Stamps.Length);
                _stamp = 0;
            }
            _stamp++;
        }

        /// <summary>Visits the triangles the query's shape can reach, in chunks along the sweep so a near hit ends early.</summary>
        private void TraverseStatic()
        {
            if (_regions.Count == 0) return;
            NextStamp();
            float rad = _r;
            if (_mode == Mode.Overlap)
            {
                Visit(_o.X - rad, _o.Z - rad, _o.X + rad, _o.Z + rad);
                return;
            }
            float across = Math.Max(Math.Abs(_d.X), Math.Abs(_d.Z));
            float chunk = across > 1e-6f ? Math.Max(CellSize, CellSize / across) : _maxT;
            float s0 = 0f;
            while (true)
            {
                float s1 = Math.Min(s0 + chunk, _maxT);
                float xa = _o.X + _d.X * s0, xb = _o.X + _d.X * s1, za = _o.Z + _d.Z * s0, zb = _o.Z + _d.Z * s1;
                Visit(Math.Min(xa, xb) - rad, Math.Min(za, zb) - rad, Math.Max(xa, xb) + rad, Math.Max(za, zb) + rad);
                if (_bestT <= s1 || s1 >= _maxT) break;
                s0 = s1;
            }
        }

        private void Visit(float minX, float minZ, float maxX, float maxZ)
        {
            int x0, x1, z0, z1;
            CellRange(minX, minZ, maxX, maxZ, CellMargin, out x0, out z0, out x1, out z1);
            for (int cx = x0; cx <= x1; cx++)
            {
                for (int cz = z0; cz <= z1; cz++)
                {
                    Cell cell;
                    if (!_cells.TryGetValue(Key(cx, cz), out cell)) continue;
                    for (int i = 0; i < cell.Count; i++)
                    {
                        Region rg = cell.Items[i].R;
                        int ti = cell.Items[i].I;
                        if (rg.Stamps[ti] == _stamp) continue;
                        rg.Stamps[ti] = _stamp;
                        if ((rg.Tris[ti].F & _mask.ExcludeFlags) != 0) continue;
                        TestTri(ref rg.Tris[ti]);
                    }
                }
            }
            minX -= CellMargin;
            minZ -= CellMargin;
            maxX += CellMargin;
            maxZ += CellMargin;
            for (int i = 0; i < _oversizeCount; i++)
            {
                if (_oversize[i].MinX > maxX || _oversize[i].MaxX < minX || _oversize[i].MinZ > maxZ || _oversize[i].MaxZ < minZ) continue;
                Region rg = _oversize[i].R;
                int ti = _oversize[i].I;
                if (rg.Stamps[ti] == _stamp) continue;
                rg.Stamps[ti] = _stamp;
                if ((rg.Tris[ti].F & _mask.ExcludeFlags) != 0) continue;
                TestTri(ref rg.Tris[ti]);
            }
        }

        private static bool IsOversize(int x0, int z0, int x1, int z1)
        {
            return ((long)x1 - x0 + 1) * ((long)z1 - z0 + 1) > MaxGridCells;
        }

        private void TestTri(ref Tri t)
        {
            Vec3 a = t.A - _o, b = t.B - _o, c = t.C - _o;
            switch (_mode)
            {
                case Mode.Ray:
                {
                    float tt;
                    Vec3 n;
                    if (RayTri(_d, a, b, c, t.N, out tt, out n) && tt <= _maxT && tt < _bestT) Record(tt, _d * tt, n, t.F, 0, HitKind.Static);
                    break;
                }
                case Mode.Sweep:
                    if (Geo.SweepTri(_len, _r, _d, _maxT, a, b, c, t.N, ref _bestT, ref _bestP, ref _bestN))
                    {
                        _has = true;
                        _bestF = t.F;
                        _bestTag = 0;
                        _bestKind = HitKind.Static;
                    }
                    break;
                default:
                {
                    Vec3 p, n;
                    float depth;
                    if (Geo.OverlapTri(Vec3.Zero, _len, _r, a, b, c, t.N, out p, out n, out depth)) AddContact(p, n, depth, t.F, 0, HitKind.Static);
                    break;
                }
            }
        }

        /// <summary>Double-sided Möller–Trumbore from the origin; the normal faces the ray.</summary>
        private static bool RayTri(Vec3 d, Vec3 a, Vec3 b, Vec3 c, Vec3 n, out float t, out Vec3 normal)
        {
            t = 0f;
            normal = Vec3.Zero;
            float dn = Vec3.Dot(d, n);
            if (Math.Abs(dn) < 1e-7f) return false;
            Vec3 e1 = b - a, e2 = c - a;
            Vec3 pv = Vec3.Cross(d, e2);
            float det = Vec3.Dot(e1, pv);
            if (det == 0f) return false;
            float inv = 1f / det;
            Vec3 tv = -a;
            float u = Vec3.Dot(tv, pv) * inv;
            if (u < -1e-6f || u > 1f + 1e-6f) return false;
            Vec3 qv = Vec3.Cross(tv, e1);
            float v = Vec3.Dot(d, qv) * inv;
            if (v < -1e-6f || u + v > 1f + 1e-6f) return false;
            t = Vec3.Dot(e2, qv) * inv;
            if (t < -1e-5f) return false;
            if (t < 0f) t = 0f;
            normal = dn < 0f ? n : -n;
            return true;
        }

        // ------------------------------------------------------------------ dynamic shapes

        private static float Comp(Vec3 v, int i)
        {
            return i == 0 ? v.X : i == 1 ? v.Y : v.Z;
        }

        private static Vec3 Axis(int i, float s)
        {
            return i == 0 ? new Vec3(s, 0f, 0f) : i == 1 ? new Vec3(0f, s, 0f) : new Vec3(0f, 0f, s);
        }

        private static Vec3 ClampBox(Vec3 p, Vec3 h)
        {
            return new Vec3(MathUtil.Clamp(p.X, -h.X, h.X), MathUtil.Clamp(p.Y, -h.Y, h.Y), MathUtil.Clamp(p.Z, -h.Z, h.Z));
        }

        private void RayDynamic()
        {
            for (int i = 0; i < _boxCount; i++)
            {
                if (SkipTag(_boxes[i].Tag)) continue;
                BoxShape bx = _boxes[i];
                Vec3 lo = bx.Inv * (_o - bx.C), ld = bx.Inv * _d;
                if (Math.Abs(lo.X) <= bx.H.X && Math.Abs(lo.Y) <= bx.H.Y && Math.Abs(lo.Z) <= bx.H.Z)
                {
                    if (0f < _bestT) Record(0f, Vec3.Zero, -_d, SurfaceFlags.None, bx.Tag, HitKind.Box);
                    continue;
                }
                float tmin = float.MinValue, tmax = float.MaxValue;
                int axis = -1;
                float sign = 0f;
                bool miss = false;
                for (int k = 0; k < 3 && !miss; k++)
                {
                    float o = Comp(lo, k), d = Comp(ld, k), h = Comp(bx.H, k);
                    if (Math.Abs(d) < 1e-12f)
                    {
                        if (Math.Abs(o) > h) miss = true;
                        continue;
                    }
                    float t1 = (-h - o) / d, t2 = (h - o) / d, s = -1f;
                    if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; s = 1f; }
                    if (t1 > tmin) { tmin = t1; axis = k; sign = s; }
                    if (t2 < tmax) tmax = t2;
                    if (tmin > tmax) miss = true;
                }
                if (miss || axis < 0 || tmin < 0f || tmin > _maxT || tmin >= _bestT) continue;
                Record(tmin, _d * tmin, bx.R * Axis(axis, sign), SurfaceFlags.None, bx.Tag, HitKind.Box);
            }
            for (int i = 0; i < _capCount; i++)
            {
                if (SkipTag(_caps[i].Tag)) continue;
                CapShape cp = _caps[i];
                float y0 = cp.Feet.Y + cp.Radius, y1 = cp.Feet.Y + cp.Height - cp.Radius;
                Vec3 q0 = new Vec3(cp.Feet.X, MathUtil.Clamp(_o.Y, y0, y1), cp.Feet.Z);
                if ((_o - q0).LengthSquared < cp.Radius * cp.Radius)
                {
                    if (0f < _bestT) Record(0f, Vec3.Zero, -_d, SurfaceFlags.None, cp.Tag, HitKind.Capsule);
                    continue;
                }
                float t;
                if (!Geo.RayVerticalCapsule(_o, _d, cp.Feet.X, y0, y1, cp.Feet.Z, cp.Radius, out t) || t > _maxT || t >= _bestT) continue;
                Vec3 p = _o + _d * t;
                Vec3 q = new Vec3(cp.Feet.X, MathUtil.Clamp(p.Y, y0, y1), cp.Feet.Z);
                Record(t, _d * t, (p - q).Normalized, SurfaceFlags.None, cp.Tag, HitKind.Capsule);
            }
        }

        private void SweepDynamic()
        {
            for (int i = 0; i < _boxCount; i++)
            {
                if (SkipTag(_boxes[i].Tag)) continue;
                BoxShape bx = _boxes[i];
                Vec3 c = bx.C - _o;
                for (int k = 0; k < 8; k++)
                {
                    _corners[k] = c + bx.R * new Vec3((k & 1) != 0 ? bx.H.X : -bx.H.X, (k & 2) != 0 ? bx.H.Y : -bx.H.Y, (k & 4) != 0 ? bx.H.Z : -bx.H.Z);
                }
                bool hit = false;
                for (int k = 0; k < BoxTris.Length; k += 3)
                {
                    Vec3 a = _corners[BoxTris[k]], b = _corners[BoxTris[k + 1]], d = _corners[BoxTris[k + 2]];
                    Vec3 n = Vec3.Cross(b - a, d - a).Normalized;
                    hit |= Geo.SweepTri(_len, _r, _d, _maxT, a, b, d, n, ref _bestT, ref _bestP, ref _bestN);
                }
                if (hit)
                {
                    _has = true;
                    _bestF = SurfaceFlags.None;
                    _bestTag = bx.Tag;
                    _bestKind = HitKind.Box;
                }
            }
            for (int i = 0; i < _capCount; i++)
            {
                if (SkipTag(_caps[i].Tag)) continue;
                CapShape cp = _caps[i];
                Vec3 b0 = new Vec3(cp.Feet.X, cp.Feet.Y + cp.Radius, cp.Feet.Z) - _o;
                Vec3 b1 = new Vec3(b0.X, b0.Y + cp.Height - 2f * cp.Radius, b0.Z);
                Vec3 up = new Vec3(0f, _len, 0f);
                float rr = _r + cp.Radius;
                float s, u, t;
                Vec3 ca, cb;
                float dist = (float)Math.Sqrt(Geo.ClosestSegSeg(Vec3.Zero, up, b0, b1, out s, out u, out ca, out cb));
                if (dist < rr + 1e-5f)
                {
                    Vec3 n0 = dist > 1e-6f ? (ca - cb) / dist : Vec3.Right;
                    if (Vec3.Dot(_d, n0) < -Geo.ApproachEps && 0f < _bestT) Record(0f, cb + n0 * cp.Radius, n0, SurfaceFlags.None, cp.Tag, HitKind.Capsule);
                    continue;
                }
                if (!Geo.RayVerticalCapsule(Vec3.Zero, _d, b0.X, b0.Y - _len, b1.Y, b0.Z, rr, out t) || t > _maxT || t >= _bestT) continue;
                Vec3 a0 = _d * t;
                Geo.ClosestSegSeg(a0, a0 + up, b0, b1, out s, out u, out ca, out cb);
                Vec3 n = (ca - cb).Normalized;
                if (Vec3.Dot(_d, n) >= -Geo.ApproachEps) continue;
                Record(t, cb + n * cp.Radius, n, SurfaceFlags.None, cp.Tag, HitKind.Capsule);
            }
        }

        private void OverlapDynamic()
        {
            Vec3 top = new Vec3(_o.X, _o.Y + _len, _o.Z);
            for (int i = 0; i < _boxCount; i++)
            {
                if (SkipTag(_boxes[i].Tag)) continue;
                BoxShape bx = _boxes[i];
                Vec3 p0 = bx.Inv * (_o - bx.C), p1 = bx.Inv * (top - bx.C);
                float lo = 0f, hi = 1f;
                if (_len > 0f)
                {
                    for (int k = 0; k < 40; k++)
                    {
                        float m1 = lo + (hi - lo) / 3f, m2 = hi - (hi - lo) / 3f;
                        Vec3 q1 = Vec3.Lerp(p0, p1, m1), q2 = Vec3.Lerp(p0, p1, m2);
                        if ((q1 - ClampBox(q1, bx.H)).LengthSquared < (q2 - ClampBox(q2, bx.H)).LengthSquared) hi = m2;
                        else lo = m1;
                    }
                }
                Vec3 p = Vec3.Lerp(p0, p1, (lo + hi) * 0.5f);
                Vec3 q = ClampBox(p, bx.H);
                float dsq = (p - q).LengthSquared;
                Vec3 nl, pl;
                float depth;
                if (dsq > 1e-12f)
                {
                    float dist = (float)Math.Sqrt(dsq);
                    depth = _r - dist;
                    if (depth <= Geo.PenEps) continue;
                    nl = (p - q) / dist;
                    pl = q;
                }
                else
                {
                    depth = float.MaxValue;
                    nl = Vec3.Zero;
                    int axis = 0;
                    for (int k = 0; k < 3; k++)
                    {
                        float a = Comp(p0, k), b = Comp(p1, k), h = Comp(bx.H, k);
                        float up = h + _r - Math.Min(a, b), down = h + _r + Math.Max(a, b);
                        if (up < depth) { depth = up; nl = Axis(k, 1f); axis = k; }
                        if (down < depth) { depth = down; nl = Axis(k, -1f); axis = k; }
                    }
                    pl = ClampBox(Vec3.Lerp(p0, p1, 0.5f), bx.H);
                    float face = Comp(nl, axis) * Comp(bx.H, axis);
                    pl = axis == 0 ? new Vec3(face, pl.Y, pl.Z) : axis == 1 ? new Vec3(pl.X, face, pl.Z) : new Vec3(pl.X, pl.Y, face);
                }
                AddContact(bx.C + bx.R * pl - _o, bx.R * nl, depth, SurfaceFlags.None, bx.Tag, HitKind.Box);
            }
            for (int i = 0; i < _capCount; i++)
            {
                if (SkipTag(_caps[i].Tag)) continue;
                CapShape cp = _caps[i];
                Vec3 b0 = new Vec3(cp.Feet.X, cp.Feet.Y + cp.Radius, cp.Feet.Z);
                Vec3 b1 = new Vec3(b0.X, cp.Feet.Y + cp.Height - cp.Radius, b0.Z);
                float s, u;
                Vec3 ca, cb;
                float dist = (float)Math.Sqrt(Geo.ClosestSegSeg(_o, top, b0, b1, out s, out u, out ca, out cb));
                float depth = _r + cp.Radius - dist;
                if (depth <= Geo.PenEps) continue;
                Vec3 n = dist > 1e-6f ? (ca - cb) / dist : Vec3.Right;
                AddContact(cb + n * cp.Radius - _o, n, depth, SurfaceFlags.None, cp.Tag, HitKind.Capsule);
            }
        }
    }
}
