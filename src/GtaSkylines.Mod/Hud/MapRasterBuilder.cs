using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Roads;

namespace GtaSkylines.Mod.Hud
{
    /// <summary>
    /// Builds the city map raster a slice per frame and only when the road graph's version changed (pure; no Unity;
    /// main thread, because the city adapter reads CS1). Phases: scan the segment ids for the bounds, water by cell rows,
    /// buildings and parks, then paths, roads and highways. A version change mid-build restarts it, so a stale raster is
    /// never delivered. Work units: a segment probe 1, a water cell 4, a building 8, a road segment 16.
    /// </summary>
    public sealed class MapRasterBuilder
    {
        /// <summary>Margin around the road network, metres.</summary>
        public const float Margin = 300f;
        /// <summary>Water is sampled once per square of this many pixels.</summary>
        public const int WaterCellPixels = 4;
        private const int SegmentIds = 65536;

        private enum Phase { Scan, Water, Buildings, Roads, Done }

        private readonly RoadGraph _roads;
        private readonly ICity _city;
        private readonly float _mpp;
        private readonly int _maxPixels;
        private readonly Func<float, float, bool> _isWater;
        private readonly List<int> _segments = new List<int>();
        private readonly List<ushort> _buildings = new List<ushort>();
        private bool _running;
        private int _version;
        private Phase _phase;
        private int _cursor, _pass;
        private float _minX, _minZ, _maxX, _maxZ;
        private MapRaster _raster;

        /// <summary>A builder over <paramref name="roads"/> and <paramref name="city"/> (either may be null).</summary>
        public MapRasterBuilder(RoadGraph roads, ICity city, float metresPerPixel, int maxPixels)
        {
            _roads = roads;
            _city = city;
            _mpp = metresPerPixel;
            _maxPixels = maxPixels;
            if (city != null) _isWater = (x, z) => { float y; return city.TryWaterSurface(x, z, out y); };
            BuiltVersion = -1;
        }

        /// <summary>Road graph version of the last delivered raster; -1 before the first.</summary>
        public int BuiltVersion { get; private set; }
        /// <summary>True while a build is in progress.</summary>
        public bool Building { get { return _running; } }
        /// <summary>Buildings drawn into the last delivered raster.</summary>
        public int BuildingCount { get; private set; }

        /// <summary>
        /// Call once per frame. Starts a build when the road graph's version differs from <see cref="BuiltVersion"/>
        /// (restarting one whose version went stale), advances it by about <paramref name="budget"/> work units (at least
        /// one step) and returns the finished raster once; otherwise null. An empty road graph starts nothing.
        /// </summary>
        public MapRaster Tick(int budget)
        {
            if (_roads == null || _roads.SegmentCount == 0) return null;
            int v = _roads.Version;
            if (_running && v != _version) _running = false;
            if (!_running)
            {
                if (v == BuiltVersion) return null;
                Start(v);
            }
            int left = Math.Max(1, budget);
            while (left > 0 && _phase != Phase.Done) left -= Step();
            if (_phase != Phase.Done) return null;
            _running = false;
            BuiltVersion = _version;
            BuildingCount = _raster != null ? _buildings.Count : 0;
            MapRaster r = _raster;
            _raster = null;
            return r;
        }

        private void Start(int version)
        {
            _running = true;
            _version = version;
            _phase = Phase.Scan;
            _cursor = 0;
            _pass = 0;
            _segments.Clear();
            _buildings.Clear();
            _raster = null;
            _minX = _minZ = float.MaxValue;
            _maxX = _maxZ = float.MinValue;
        }

        private int Step()
        {
            switch (_phase)
            {
                case Phase.Scan: return ScanStep();
                case Phase.Water:
                    if (_cursor >= _raster.CellRows(WaterCellPixels))
                    {
                        QueryBuildings();
                        return 1;
                    }
                    _raster.FillCellRow(_isWater, WaterCellPixels, _cursor++, MapStyle.Water);
                    return 4 * _raster.CellRows(WaterCellPixels);
                case Phase.Buildings:
                    if (_cursor >= _buildings.Count)
                    {
                        Enter(Phase.Roads);
                        return 1;
                    }
                    BuildingDescriptor b;
                    if (_city.TryGetBuilding(_buildings[_cursor++], out b) && b != null)
                        _raster.FillRect(b.Position.X, b.Position.Z, b.YawDeg, b.Width, b.Length,
                            b.Service == ServiceKind.Park ? MapStyle.Park : MapStyle.Building);
                    return 8;
                default: return RoadStep();
            }
        }

        private int ScanStep()
        {
            if (_cursor >= SegmentIds)
            {
                if (_minX > _maxX)
                {
                    _phase = Phase.Done;
                    return 1;
                }
                _raster = MapRaster.ForArea(_minX, _minZ, _maxX, _maxZ, Margin, _mpp, _maxPixels);
                _raster.Clear(MapStyle.Land);
                Enter(_city != null ? Phase.Water : Phase.Roads);
                return 1;
            }
            RoadSegment sg;
            int id = _cursor++;
            if (_roads.TryGetSegment(id, out sg))
            {
                _segments.Add(id);
                Grow(sg.A);
                Grow(sg.D);
            }
            return 1;
        }

        private void QueryBuildings()
        {
            float half = _raster.Size * 0.5f;
            _city.BuildingsNear(new Vec3(_raster.MinX + half, 0f, _raster.MinZ + half), half * 1.4143f, _buildings);
            Enter(Phase.Buildings);
        }

        // Paths, then roads, then highways on top.
        private int RoadStep()
        {
            if (_cursor >= _segments.Count)
            {
                if (++_pass == 3) _phase = Phase.Done;
                _cursor = 0;
                return 1;
            }
            RoadSegment sg;
            if (!_roads.TryGetSegment(_segments[_cursor++], out sg)) return 1;
            bool ped = (sg.Flags & RoadFlags.PedestrianOnly) != 0;
            bool hw = (sg.Flags & RoadFlags.Highway) != 0;
            if ((ped ? 0 : hw ? 2 : 1) != _pass) return 1;
            float w = MapStyle.RoadWidth(sg.ForwardLanes, sg.BackwardLanes, sg.LaneWidth, ped);
            uint c = ped ? MapStyle.Path : hw ? MapStyle.Highway : MapStyle.Road;
            _raster.DrawBezier(sg.A.X, sg.A.Z, sg.B.X, sg.B.Z, sg.C.X, sg.C.Z, sg.D.X, sg.D.Z, w, 8f, c);
            return 16;
        }

        private void Enter(Phase p)
        {
            _phase = p;
            _cursor = 0;
        }

        private void Grow(Vec3 p)
        {
            if (p.X < _minX) _minX = p.X;
            if (p.Z < _minZ) _minZ = p.Z;
            if (p.X > _maxX) _maxX = p.X;
            if (p.Z > _maxZ) _maxZ = p.Z;
        }
    }
}
