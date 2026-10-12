using System;
using System.Collections.Generic;
using System.Diagnostics;
using ColossalFramework;
using GtaSkylines.Game.Physics;
using Skylines.Core.Geometry;
using Skylines.Core.Streaming;
using Skylines.Host;
using Skylines.Host.Geometry;
using UnityEngine;

namespace GtaSkylines.Mod.World
{
    /// <summary>
    /// Streams CS1's static collision (terrain, roads, bridges, tunnels, buildings, trees, props, area walls) around the
    /// player into the Game's <see cref="CollisionWorld"/> in 16 m regions: within 160 m, nearest first (regions ahead
    /// of a fast player first), evicted beyond 220 m, within a per-frame time budget. Regions touched by city edits
    /// are rebuilt, and so are those around a building whose prefab, completion or drawn construction height changed (polled,
    /// <see cref="PollPerFrame"/> buildings a frame, since CS1 raises no event for those). Ported from the reference's CollisionStreamer without the Minecraft conversion. Main thread only,
    /// except <see cref="RequestRebuild"/>.
    /// </summary>
    public sealed class CollisionStreamer : IDisposable
    {
        /// <summary>Region edge, metres.</summary>
        public const float RegionSize = 16f;
        /// <summary>Regions are loaded within this distance of the player.</summary>
        public const float LoadRadius = 160f;
        /// <summary>Loaded regions farther than this are removed.</summary>
        public const float EvictRadius = 220f;
        /// <summary>Default build time per frame, milliseconds.</summary>
        public const double DefaultBudgetMs = 2.0;

        private const float TerrainStep = 2f;
        private const ushort TerrainFlag = 1;
        private const float AheadSpeed = 8f, AheadSeconds = 2f, AheadMax = 64f, AheadRadius = 48f;
        private const int MaxBuildAttempts = 3;
        /// <summary>Buildings checked for shape changes per frame (a full sweep of 49152 takes 48 frames).</summary>
        public const int PollPerFrame = 1024;

        private struct Offset
        {
            public int Dx, Dz;
            public float Distance;
        }

        private static readonly List<long> s_requests = new List<long>();

        private readonly HostLog _log;
        private readonly CollisionWorld _world;
        private readonly RegionGrid _grid = new RegionGrid(RegionSize);
        private readonly Offset[] _offsets;
        private readonly HashSet<long> _loaded = new HashSet<long>();
        private readonly HashSet<long> _dirty = new HashSet<long>();
        private readonly List<long> _scratch = new List<long>();
        private readonly Dictionary<long, int> _failures = new Dictionary<long, int>();
        private readonly TerrainSampler _terrain = new TerrainSampler();
        private readonly NetGeometry _net = new NetGeometry();
        private readonly BuildingGeometry _buildings = new BuildingGeometry();
        private readonly ObstacleGeometry _obstacles = new ObstacleGeometry();
        private readonly TriangleBuffer _buffer = new TriangleBuffer();
        private readonly Stopwatch _clock = new Stopwatch();
        private readonly ChangeDetector _changes = new ChangeDetector(BuildingManager.MAX_BUILDING_COUNT);
        private int _pollCursor;
        private ushort[] _flags = new ushort[1024];
        private BuildingManager.BuildingCreatedHandler _onCreated;
        private BuildingManager.BuildingReleasedHandler _onReleased;
        private int _lastRx = int.MinValue, _lastRz = int.MinValue;
        private int _regionsBuilt;
        private double _lastBuildMs;

        /// <summary>Streams into <paramref name="world"/>; subscribes to CS1's building events for rebuilds.</summary>
        public CollisionStreamer(HostLog log, CollisionWorld world)
        {
            _log = log;
            _world = world;
            Viewer = new CollisionViewer(log);
            _offsets = BuildOffsets();
            if (BuildingManager.exists)
            {
                _onCreated = OnBuildingChanged;
                _onReleased = OnBuildingChanged;
                Singleton<BuildingManager>.instance.EventBuildingCreated += _onCreated;
                Singleton<BuildingManager>.instance.EventBuildingReleased += _onReleased;
            }
        }

        /// <summary>The Ctrl+Shift+F9 wireframe of the loaded collision.</summary>
        public CollisionViewer Viewer { get; private set; }

        /// <summary>Regions currently in the collision world.</summary>
        public int LoadedCount { get { return _loaded.Count; } }

        /// <summary>One line for an overlay.</summary>
        public string Stats
        {
            get { return "collision: " + _loaded.Count + " regions loaded, " + _regionsBuilt + " built, last " + _lastBuildMs.ToString("0.0") + " ms"; }
        }

        /// <summary>Thread-safe: rebuilds every loaded region within <paramref name="radius"/> m of CS1 point (x, z) on a later frame.</summary>
        public static void RequestRebuild(float x, float z, float radius)
        {
            int r0x = (int)Math.Floor((x - radius) / RegionSize), r1x = (int)Math.Floor((x + radius) / RegionSize);
            int r0z = (int)Math.Floor((z - radius) / RegionSize), r1z = (int)Math.Floor((z + radius) / RegionSize);
            lock (s_requests)
            {
                for (int rx = r0x; rx <= r1x; rx++)
                    for (int rz = r0z; rz <= r1z; rz++)
                        s_requests.Add(RegionGrid.Key(rx, rz));
            }
        }

        /// <summary>Marks every loaded region for rebuilding (the road network changed).</summary>
        public void InvalidateAll()
        {
            foreach (long k in _loaded) _dirty.Add(k);
        }

        /// <summary>Removes every region from the collision world and forgets them (city unload, play mode end).</summary>
        public void Clear()
        {
            foreach (long k in _loaded) _world.RemoveRegion(k);
            _loaded.Clear();
            _dirty.Clear();
            _failures.Clear();
            Viewer.Clear();
            _changes.Reset();
            _lastRx = int.MinValue;
            if (_buildings.ShapesScaled + _buildings.ShapesSkipped > 0)
                _log.Info("collision: " + _buildings.ShapesScaled + " building shapes cut to their construction height, " + _buildings.ShapesSkipped + " left empty (construction not visible yet) this session");
        }

        /// <summary>
        /// Per frame: evicts far regions, then builds due regions (rebuilds first, then regions ahead of a fast player,
        /// then nearest first) until <paramref name="budgetMs"/> is used; at least one region when one is due.
        /// </summary>
        public void Tick(Vector3 feet, Vector3 velocity, double budgetMs)
        {
            Viewer.Track(feet);
            PollBuildings();
            DrainRequests();
            int prx, prz;
            _grid.RegionOf(feet.x, feet.z, out prx, out prz);
            if (prx != _lastRx || prz != _lastRz)
            {
                Evict(feet);
                _lastRx = prx;
                _lastRz = prz;
            }
            _clock.Reset();
            _clock.Start();
            int built = 0;
            long key;
            while (built == 0 || _clock.Elapsed.TotalMilliseconds < budgetMs)
            {
                if (!NextDue(feet, velocity, prx, prz, out key)) break;
                Build(key, feet);
                built++;
            }
            _clock.Stop();
        }

        /// <summary>Unsubscribes from CS1 and the camera.</summary>
        public void Dispose()
        {
            if (BuildingManager.exists)
            {
                if (_onCreated != null) Singleton<BuildingManager>.instance.EventBuildingCreated -= _onCreated;
                if (_onReleased != null) Singleton<BuildingManager>.instance.EventBuildingReleased -= _onReleased;
            }
            _onCreated = null;
            _onReleased = null;
            Viewer.Dispose();
        }

        /// <summary>
        /// Builds one region's collision in CS1 coordinates (Host geometry flags): terrain with holes inside tunnel
        /// portals, terrain cut under sunken roads, roads, buildings, area walls, then trees and props. A failing layer
        /// is dropped and its exception returned (null when everything built). The reference's <c>BuildCs</c>.
        /// </summary>
        public static Exception BuildRegion(TerrainSampler terrain, NetGeometry net, BuildingGeometry buildings, ObstacleGeometry obstacles,
            float minX, float minZ, float maxX, float maxZ, TriangleBuffer into)
        {
            into.Clear();
            Exception failed = null;
            StripFootprint portals = null;
            try { portals = net.PortalFootprint(minX, minZ, maxX, maxZ); }
            catch (Exception e) { failed = e; }
            Heightfield.Triangulate(terrain.AsFunc(), terrain.HoleFunc(TerrainStep, portals), minX, minZ, maxX, maxZ, TerrainStep, TerrainFlag, into);
            bool cutTerrain = false, copied = false;
            try
            {
                s_cut.Clear();
                net.SunkenRoadCuts(minX, minZ, maxX, maxZ, s_cut);
                if (s_cut.Count > 0)
                {
                    Copy(into, s_uncut);
                    copied = true;
                    into.Clear();
                    s_cut.Apply(s_uncut, 0, s_uncut.Count, into);
                    cutTerrain = true;
                }
            }
            catch (Exception e)
            {
                if (copied) Copy(s_uncut, into);
                cutTerrain = false;
                if (failed == null) failed = e;
            }
            int terrainOnly = into.Count;
            try
            {
                net.Emit(minX, minZ, maxX, maxZ, into);
                buildings.Emit(terrain, minX, minZ, maxX, maxZ, into);
            }
            catch (Exception e)
            {
                if (cutTerrain) Copy(s_uncut, into);
                else into.Truncate(terrainOnly);
                return e;
            }
            try { s_boundary.Emit(minX, minZ, maxX, maxZ, into); }
            catch (Exception e) { if (failed == null) failed = e; }
            int solid = into.Count;
            try
            {
                obstacles.Emit(minX, minZ, maxX, maxZ, into);
                return failed;
            }
            catch (Exception e)
            {
                into.Truncate(solid);
                return e;
            }
        }

        private static readonly ConvexCut s_cut = new ConvexCut();
        private static readonly AreaBoundary s_boundary = new AreaBoundary();
        private static readonly TriangleBuffer s_uncut = new TriangleBuffer();

        private static void Copy(TriangleBuffer from, TriangleBuffer to)
        {
            to.Clear();
            float[] p = from.Positions;
            for (int t = 0, o = 0; t < from.Count; t++, o += 9)
                to.Add(p[o], p[o + 1], p[o + 2], p[o + 3], p[o + 4], p[o + 5], p[o + 6], p[o + 7], p[o + 8], from.Flags[t]);
        }

        private static Offset[] BuildOffsets()
        {
            int n = (int)Math.Ceiling(LoadRadius / RegionSize) + 1;
            var list = new List<Offset>();
            for (int dx = -n; dx <= n; dx++)
                for (int dz = -n; dz <= n; dz++)
                {
                    // Distance from the centre of the player's region to the nearest point of the other region.
                    float ex = Math.Max(0, Math.Abs(dx) - 1) * RegionSize + RegionSize * 0.5f * (dx != 0 ? 1 : 0);
                    float ez = Math.Max(0, Math.Abs(dz) - 1) * RegionSize + RegionSize * 0.5f * (dz != 0 ? 1 : 0);
                    float d = (float)Math.Sqrt(ex * ex + ez * ez);
                    if (d <= LoadRadius) list.Add(new Offset { Dx = dx, Dz = dz, Distance = d });
                }
            list.Sort((a, b) => a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance) : (a.Dx != b.Dx ? a.Dx.CompareTo(b.Dx) : a.Dz.CompareTo(b.Dz)));
            return list.ToArray();
        }

        private void OnBuildingChanged(ushort building)
        {
            try
            {
                Building[] buf = Singleton<BuildingManager>.instance.m_buildings.m_buffer;
                if (building == 0 || building >= buf.Length) return;
                Vector3 p = buf[building].m_position;
                RequestRebuild(p.x, p.z, Math.Max(buf[building].Width, buf[building].Length) * 8f);
            }
            catch (Exception e) { _log.Error("collision: building event " + building, e); }
        }

        private void PollBuildings()
        {
            if (_loaded.Count == 0 || !BuildingManager.exists) return;
            Building[] buf = Singleton<BuildingManager>.instance.m_buildings.m_buffer;
            uint frame = Singleton<SimulationManager>.instance.m_referenceFrameIndex;
            int count = Math.Min(buf.Length, BuildingManager.MAX_BUILDING_COUNT);
            for (int n = 0; n < PollPerFrame; n++)
            {
                if (_pollCursor >= count) _pollCursor = 0;
                ushort id = (ushort)_pollCursor++;
                if (id != 0 && _changes.Observe(id, BuildingGeometry.CollisionSignature(id, ref buf[id], frame))) OnBuildingChanged(id);
            }
        }

        private void DrainRequests()
        {
            lock (s_requests)
            {
                if (s_requests.Count == 0) return;
                for (int i = 0; i < s_requests.Count; i++)
                    if (_loaded.Contains(s_requests[i])) _dirty.Add(s_requests[i]);
                s_requests.Clear();
            }
        }

        private void Evict(Vector3 feet)
        {
            _scratch.Clear();
            foreach (long k in _loaded)
            {
                int rx, rz;
                RegionGrid.Unkey(k, out rx, out rz);
                if (RegionDistance(rx, rz, feet.x, feet.z) > EvictRadius) _scratch.Add(k);
            }
            for (int i = 0; i < _scratch.Count; i++)
            {
                _world.RemoveRegion(_scratch[i]);
                _loaded.Remove(_scratch[i]);
                _dirty.Remove(_scratch[i]);
                _failures.Remove(_scratch[i]);
                Viewer.Remove(_scratch[i]);
            }
        }

        private static float RegionDistance(int rx, int rz, float x, float z)
        {
            float minX = rx * RegionSize, minZ = rz * RegionSize;
            float dx = Math.Max(0f, Math.Max(minX - x, x - (minX + RegionSize)));
            float dz = Math.Max(0f, Math.Max(minZ - z, z - (minZ + RegionSize)));
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        private bool NextDue(Vector3 feet, Vector3 velocity, int prx, int prz, out long key)
        {
            if (_dirty.Count > 0)
            {
                foreach (long k in _dirty)
                {
                    key = k;
                    _dirty.Remove(k);
                    return true;
                }
            }
            float speed = Mathf.Sqrt(velocity.x * velocity.x + velocity.z * velocity.z);
            if (speed > AheadSpeed)
            {
                float ahead = Mathf.Min(AheadMax, speed * AheadSeconds) / speed;
                int arx, arz;
                _grid.RegionOf(feet.x + velocity.x * ahead, feet.z + velocity.z * ahead, out arx, out arz);
                if (FirstMissing(arx, arz, AheadRadius, out key)) return true;
            }
            return FirstMissing(prx, prz, LoadRadius, out key);
        }

        private bool FirstMissing(int cx, int cz, float radius, out long key)
        {
            for (int i = 0; i < _offsets.Length && _offsets[i].Distance <= radius; i++)
            {
                long k = RegionGrid.Key(cx + _offsets[i].Dx, cz + _offsets[i].Dz);
                if (_loaded.Contains(k)) continue;
                int failures;
                if (_failures.TryGetValue(k, out failures) && failures >= MaxBuildAttempts) continue;
                key = k;
                return true;
            }
            key = 0;
            return false;
        }

        private void Build(long key, Vector3 feet)
        {
            int rx, rz;
            RegionGrid.Unkey(key, out rx, out rz);
            float minX, minZ, maxX, maxZ;
            _grid.Bounds(rx, rz, out minX, out minZ, out maxX, out maxZ);
            try
            {
                double start = _clock.Elapsed.TotalMilliseconds;
                Exception partial = BuildRegion(_terrain, _net, _buildings, _obstacles, minX, minZ, maxX, maxZ, _buffer);
                if (partial != null) _log.Error("collision region (" + rx + "," + rz + "): a layer failed to build, keeping the rest", partial);
                int n = _buffer.Count;
                if (_flags.Length < n) _flags = new ushort[Math.Max(n, _flags.Length * 2)];
                ushort[] host = _buffer.Flags;
                for (int i = 0; i < n; i++) _flags[i] = (ushort)SurfaceMap.ToSurface(host[i]);
                _world.SetRegion(key, _buffer.Positions, _flags, n);
                _loaded.Add(key);
                _failures.Remove(key);
                Viewer.Store(key, minX, minZ, maxX, maxZ, _buffer);
                _regionsBuilt++;
                _lastBuildMs = _clock.Elapsed.TotalMilliseconds - start;
            }
            catch (Exception e)
            {
                int failures;
                _failures.TryGetValue(key, out failures);
                _failures[key] = ++failures;
                _log.Error("collision region (" + rx + "," + rz + ") failed to build (attempt " + failures + " of " + MaxBuildAttempts + ")", e);
            }
        }
    }
}
