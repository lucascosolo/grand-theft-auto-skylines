using System;
using System.Collections.Generic;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.Roads
{
    /// <summary>
    /// The city's road network for navigation, built by the Mod from CS1's NetManager and rebuilt wholesale when the
    /// network changes. All positions are CS1 world metres; headings are degrees as <see cref="Vec3.YawDegOf"/>
    /// (0 = +z, 90 = +x). Not thread-safe: queries reuse internal scratch buffers, so call from the game thread only.
    /// No query allocates once the graph is built and the caller's <see cref="Route"/> / lists have grown to size.
    /// <para>
    /// Legality: a segment may be driven forward (start → end) when <see cref="RoadSegment.ForwardLanes"/> &gt; 0 and
    /// backward when <see cref="RoadSegment.BackwardLanes"/> &gt; 0; <see cref="RoadFlags.PedestrianOnly"/> segments
    /// and segments with no lanes are ignored by every query. Routing (<see cref="FindRoute"/>,
    /// <see cref="FindWanderRoute"/>) obeys these directions; prediction (<see cref="Predict"/>,
    /// <see cref="FindJunctionsAhead"/>) models a fleeing driver and ignores lane direction.
    /// </para>
    /// <para>
    /// Lanes: with right-hand traffic the lanes for a direction of travel lie to the right of the centreline seen in
    /// that direction; <see cref="LeftHandTraffic"/> mirrors this. There is no median.
    /// </para>
    /// </summary>
    public sealed class RoadGraph
    {
        /// <summary>Incremented by every <see cref="Rebuild"/> (starts at 0 for a never-built graph).</summary>
        public int Version { get; private set; }

        /// <summary>True when the city drives on the left; set by <see cref="Rebuild"/>, copied into every route.</summary>
        public bool LeftHandTraffic { get; private set; }

        /// <summary>Number of nodes accepted by the last <see cref="Rebuild"/>.</summary>
        public int NodeCount
        {
            get { return _nodeCount; }
        }

        /// <summary>Number of segments accepted by the last <see cref="Rebuild"/> (drivable or not).</summary>
        public int SegmentCount
        {
            get { return _segCount; }
        }

        /// <summary>
        /// Replaces the whole network with copies of <paramref name="nodes"/> and <paramref name="segments"/> and
        /// increments <see cref="Version"/>. A node with a negative id is skipped; a later node with the same id
        /// replaces an earlier one. A segment is skipped when its id is negative, its start or end node is missing,
        /// or any control point is not finite; a later segment with the same id replaces an earlier one. Segment
        /// arc lengths, junction degrees and the spatial index are computed here. Allocates; not for per-frame use.
        /// Null lists count as empty.
        /// </summary>
        /// <returns>The number of segments accepted.</returns>
        public int Rebuild(IList<RoadNode> nodes, IList<RoadSegment> segments, bool leftHandTraffic)
        {
            return RebuildCore(nodes, segments, leftHandTraffic);
        }

        /// <summary>Looks up a node by id; false when absent.</summary>
        public bool TryGetNode(int id, out RoadNode node)
        {
            int i;
            if (_nodeIndex.TryGetValue(id, out i)) { node = new RoadNode(id, _nodePos[i]); return true; }
            node = default(RoadNode);
            return false;
        }

        /// <summary>Looks up a segment by id as it was passed to <see cref="Rebuild"/>; false when absent.</summary>
        public bool TryGetSegment(int id, out RoadSegment segment)
        {
            int i;
            if (_segIndex.TryGetValue(id, out i)) { segment = _segs[i]; return true; }
            segment = default(RoadSegment);
            return false;
        }

        /// <summary>Arc length in metres of a segment's centreline; 0 when the id is absent.</summary>
        public float SegmentLength(int id)
        {
            int i;
            return _segIndex.TryGetValue(id, out i) ? _sLen[i] : 0f;
        }

        /// <summary>
        /// Number of drivable segments meeting at a node (0 when absent). A node is a junction when this is
        /// ≥ <see cref="RoadDefaults.JunctionMinDegree"/>, a dead end when it is 1.
        /// </summary>
        public int NodeDegree(int nodeId)
        {
            int i;
            return _nodeIndex.TryGetValue(nodeId, out i) ? _degree[i] : 0;
        }

        /// <summary>
        /// Finds the nearest point (3D distance) on any drivable segment's centreline to <paramref name="position"/>.
        /// False when the graph has no drivable segment or the nearest point is farther than
        /// <paramref name="maxDistance"/> metres; <paramref name="result"/> is then default.
        /// </summary>
        public bool TryNearestPoint(Vec3 position, float maxDistance, out RoadPoint result)
        {
            int seg; float t, along;
            if (!Nearest(position, maxDistance, out seg, out t, out along)) { result = default(RoadPoint); return false; }
            result = MakePoint(seg, t, along, position);
            return true;
        }

        /// <summary>
        /// Fastest legal route by travel time (segment length ÷ speed limit; A* with an admissible straight-line
        /// heuristic) from <paramref name="from"/> to <paramref name="to"/>, written into <paramref name="route"/>
        /// (cleared first, then refilled; its <see cref="Route.Revision"/> increments either way).
        /// <para>
        /// Both ends are snapped with <see cref="TryNearestPoint"/> within <see cref="RoadDefaults.RouteSnapDistance"/>;
        /// the route runs from the snapped start to the snapped target along centrelines (off-road legs are not
        /// included). From a mid-segment start either legal direction may be taken. U-turns happen only at the start
        /// point, never at a node. When both ends snap to the same segment and the target is reachable directly in a
        /// legal direction, the route is that piece of the segment. Curves are subdivided to at most
        /// <see cref="RoadDefaults.RouteSampleSpacing"/> metres; a node appears as a single point.
        /// </para>
        /// On failure the route holds no points and the matching status; an empty graph gives
        /// <see cref="RouteStatus.NoRoadNearStart"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="route"/> is null.</exception>
        public RouteStatus FindRoute(Vec3 from, Vec3 to, Route route)
        {
            if (route == null) throw new ArgumentNullException("route");
            return FindRouteCore(from, to, route);
        }

        /// <summary>
        /// A random legal patrol route of at least <paramref name="minLength"/> metres from the road point nearest to
        /// <paramref name="from"/> (within <see cref="RoadDefaults.RouteSnapDistance"/>), written into
        /// <paramref name="route"/> like <see cref="FindRoute"/>. The first direction is the legal direction whose
        /// heading is closest to <paramref name="headingDeg"/> (NaN: chosen with <paramref name="rng"/>). At each node
        /// the next segment is picked uniformly with <paramref name="rng"/> among legal outgoing segments other than
        /// the one arrived on; at a dead end the route turns back along the same segment if that direction is legal,
        /// otherwise it ends there. The route ends at the end of the segment on which its length reaches
        /// <paramref name="minLength"/>, or earlier when it cannot continue. Same seed and graph give the same route.
        /// </summary>
        /// <returns><see cref="RouteStatus.Ok"/>, or <see cref="RouteStatus.NoRoadNearStart"/> (also for an empty graph).</returns>
        /// <exception cref="ArgumentNullException"><paramref name="rng"/> or <paramref name="route"/> is null.</exception>
        public RouteStatus FindWanderRoute(Vec3 from, float headingDeg, float minLength, Rng rng, Route route)
        {
            if (rng == null) throw new ArgumentNullException("rng");
            if (route == null) throw new ArgumentNullException("route");
            return WanderCore(from, headingDeg, minLength, rng, route);
        }

        /// <summary>
        /// Predicts where a vehicle at <paramref name="position"/> heading <paramref name="headingDeg"/> at
        /// <paramref name="speed"/> m/s will be after <paramref name="seconds"/>, assuming constant speed along the
        /// road. The vehicle is snapped to the nearest drivable road within <see cref="RoadDefaults.PredictSnapDistance"/>
        /// and travels along the segment in the direction (forward or backward, lane direction ignored) whose heading
        /// is closer to <paramref name="headingDeg"/>. At each node it continues onto the other drivable segment whose
        /// departing heading differs least from its arriving heading; at a dead end it stops at the node.
        /// Off-road (no road in range): straight-line dead reckoning on the x, z plane, y unchanged,
        /// <see cref="RoadPrediction.OnRoad"/> false. Speed or seconds ≤ 0: the snapped point (or the position itself
        /// off-road) with distance 0.
        /// </summary>
        public RoadPrediction Predict(Vec3 position, float headingDeg, float speed, float seconds)
        {
            return PredictCore(position, headingDeg, speed, seconds);
        }

        /// <summary>
        /// Walks the same path as <see cref="Predict"/> for <paramref name="maxDistance"/> metres and appends every
        /// junction met (in order of increasing distance; a junction exactly at the snapped start is excluded) to
        /// <paramref name="results"/>, which is cleared first. Off-road or empty graph: no results. The walk stops at a
        /// dead end or after visiting the same node twice.
        /// </summary>
        /// <returns>The number of junctions written.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="results"/> is null.</exception>
        public int FindJunctionsAhead(Vec3 position, float headingDeg, float maxDistance, List<JunctionAhead> results)
        {
            if (results == null) throw new ArgumentNullException("results");
            results.Clear();
            int s; float along; bool fwd; int startNode;
            if (!BeginWalk(position, headingDeg, out s, out along, out fwd, out startNode)) return 0;
            float travelled;
            Walk(ref s, ref along, ref fwd, startNode, maxDistance, results, out travelled);
            return results.Count;
        }
    
        private const float NodeEpsilon = 0.05f;
        private const float GridCell = 64f;
        private const float SampleSpacing = 4f;

        private Dictionary<int, int> _nodeIndex = new Dictionary<int, int>();
        private Dictionary<int, int> _segIndex = new Dictionary<int, int>();
        private int _nodeCount, _segCount;
        private Vec3[] _nodePos = new Vec3[0];
        private int[] _nodeIds = new int[0];
        private int[] _degree = new int[0];
        private RoadSegment[] _segs = new RoadSegment[0];
        private int[] _sStart = new int[0], _sEnd = new int[0];
        private float[] _sLen = new float[0], _sSpeed = new float[0], _sLaneW = new float[0];
        private bool[] _sDrive = new bool[0];
        private int[] _sampleOff = new int[0], _sampleN = new int[0];
        private Vec3[] _samplePos = new Vec3[0];
        private float[] _sampleCum = new float[0];
        private int[] _adjStart = new int[1], _adjSeg = new int[0];
        private Dictionary<long, List<int>> _grid = new Dictionary<long, List<int>>();
        private int _gMinX, _gMaxX, _gMinZ, _gMaxZ;
        private bool _anyDrivable;
        private float _vmax = 1f;

        // Scratch buffers, sized at rebuild; queries reuse them.
        private int[] _segStamp = new int[0];
        private int _segQuery;
        private float[] _g = new float[1];
        private int[] _pNode = new int[1], _pSeg = new int[1], _stamp = new int[1], _closed = new int[1];
        private int _search;
        private float[] _heapKey = new float[64];
        private int[] _heapVal = new int[64];
        private int _heapCount;
        private int[] _path = new int[64];
        private int[] _visit = new int[0];
        private int _visitId;
        private int[] _cand = new int[16];

        private int RebuildCore(IList<RoadNode> nodes, IList<RoadSegment> segments, bool leftHandTraffic)
        {
            Version++;
            LeftHandTraffic = leftHandTraffic;

            var nodeIndex = new Dictionary<int, int>();
            var nodePos = new List<Vec3>();
            var nodeIds = new List<int>();
            if (nodes != null)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    RoadNode n = nodes[i];
                    if (n.Id < 0) continue;
                    int idx;
                    if (nodeIndex.TryGetValue(n.Id, out idx)) { nodePos[idx] = n.Position; continue; }
                    nodeIndex[n.Id] = nodePos.Count;
                    nodePos.Add(n.Position);
                    nodeIds.Add(n.Id);
                }
            }

            var segIndex = new Dictionary<int, int>();
            var segs = new List<RoadSegment>();
            if (segments != null)
            {
                for (int i = 0; i < segments.Count; i++)
                {
                    RoadSegment sg = segments[i];
                    if (sg.Id < 0 || !nodeIndex.ContainsKey(sg.StartNode) || !nodeIndex.ContainsKey(sg.EndNode)) continue;
                    if (!sg.A.IsFinite || !sg.B.IsFinite || !sg.C.IsFinite || !sg.D.IsFinite) continue;
                    int idx;
                    if (segIndex.TryGetValue(sg.Id, out idx)) { segs[idx] = sg; continue; }
                    segIndex[sg.Id] = segs.Count;
                    segs.Add(sg);
                }
            }

            int nn = nodePos.Count, ns = segs.Count;
            _nodeIndex = nodeIndex;
            _segIndex = segIndex;
            _nodeCount = nn;
            _segCount = ns;
            _nodePos = nodePos.ToArray();
            _nodeIds = nodeIds.ToArray();
            _degree = new int[nn];
            _segs = segs.ToArray();
            _sStart = new int[ns];
            _sEnd = new int[ns];
            _sLen = new float[ns];
            _sSpeed = new float[ns];
            _sLaneW = new float[ns];
            _sDrive = new bool[ns];
            _sampleOff = new int[ns];
            _sampleN = new int[ns];
            _segStamp = new int[ns];
            _vmax = 1f;
            _anyDrivable = false;

            int totalSamples = 0;
            for (int s = 0; s < ns; s++)
            {
                RoadSegment sg = _segs[s];
                float poly = Vec3.Distance(sg.A, sg.B) + Vec3.Distance(sg.B, sg.C) + Vec3.Distance(sg.C, sg.D);
                int n = MathUtil.Clamp((int)Math.Ceiling(poly / SampleSpacing), 4, 128);
                _sampleOff[s] = totalSamples;
                _sampleN[s] = n;
                totalSamples += n + 1;
            }
            _samplePos = new Vec3[totalSamples];
            _sampleCum = new float[totalSamples];

            var adjCount = new int[nn + 1];
            for (int s = 0; s < ns; s++)
            {
                RoadSegment sg = _segs[s];
                _sStart[s] = nodeIndex[sg.StartNode];
                _sEnd[s] = nodeIndex[sg.EndNode];
                _sSpeed[s] = sg.SpeedLimit > 0f && !float.IsInfinity(sg.SpeedLimit) ? sg.SpeedLimit : RoadDefaults.SpeedLimit;
                _sLaneW[s] = sg.LaneWidth > 0f && !float.IsInfinity(sg.LaneWidth) ? sg.LaneWidth : RoadDefaults.LaneWidth;
                _sDrive[s] = sg.IsDrivable;
                int off = _sampleOff[s], n = _sampleN[s];
                float cum = 0f;
                for (int k = 0; k <= n; k++)
                {
                    Vec3 p = Bezier(ref sg, (float)k / n);
                    if (k > 0) cum += Vec3.Distance(_samplePos[off + k - 1], p);
                    _samplePos[off + k] = p;
                    _sampleCum[off + k] = cum;
                }
                _sLen[s] = cum;
                if (_sDrive[s])
                {
                    _anyDrivable = true;
                    if (_sSpeed[s] > _vmax) _vmax = _sSpeed[s];
                    _degree[_sStart[s]]++;
                    _degree[_sEnd[s]]++;
                    adjCount[_sStart[s]]++;
                    adjCount[_sEnd[s]]++;
                }
            }

            _adjStart = new int[nn + 1];
            for (int i = 0; i < nn; i++) _adjStart[i + 1] = _adjStart[i] + adjCount[i];
            _adjSeg = new int[_adjStart[nn]];
            var fill = new int[nn];
            for (int s = 0; s < ns; s++)
            {
                if (!_sDrive[s]) continue;
                int a = _sStart[s], b = _sEnd[s];
                _adjSeg[_adjStart[a] + fill[a]++] = s;
                _adjSeg[_adjStart[b] + fill[b]++] = s;
            }

            BuildGrid();

            _g = new float[nn + 1];
            _pNode = new int[nn + 1];
            _pSeg = new int[nn + 1];
            _stamp = new int[nn + 1];
            _closed = new int[nn + 1];
            _visit = new int[nn];
            _search = 0;
            _visitId = 0;
            return ns;
        }

        private void BuildGrid()
        {
            _grid = new Dictionary<long, List<int>>();
            _gMinX = _gMinZ = int.MaxValue;
            _gMaxX = _gMaxZ = int.MinValue;
            for (int s = 0; s < _segCount; s++)
            {
                if (!_sDrive[s]) continue;
                int off = _sampleOff[s], n = _sampleN[s];
                for (int k = 0; k < n; k++)
                {
                    Vec3 a = _samplePos[off + k], b = _samplePos[off + k + 1];
                    int x0 = Cell(Math.Min(a.X, b.X)), x1 = Cell(Math.Max(a.X, b.X));
                    int z0 = Cell(Math.Min(a.Z, b.Z)), z1 = Cell(Math.Max(a.Z, b.Z));
                    for (int cx = x0; cx <= x1; cx++)
                        for (int cz = z0; cz <= z1; cz++)
                        {
                            long key = Key(cx, cz);
                            List<int> list;
                            if (!_grid.TryGetValue(key, out list)) { list = new List<int>(); _grid[key] = list; }
                            if (list.Count == 0 || list[list.Count - 1] != s) list.Add(s);
                            if (cx < _gMinX) _gMinX = cx;
                            if (cx > _gMaxX) _gMaxX = cx;
                            if (cz < _gMinZ) _gMinZ = cz;
                            if (cz > _gMaxZ) _gMaxZ = cz;
                        }
                }
            }
        }

        private static int Cell(float v) { return (int)Math.Floor(v / GridCell); }

        private static long Key(int cx, int cz) { return ((long)cx << 32) ^ (uint)cz; }

        private static Vec3 Bezier(ref RoadSegment sg, float t)
        {
            float u = 1f - t;
            return sg.A * (u * u * u) + sg.B * (3f * u * u * t) + sg.C * (3f * u * t * t) + sg.D * (t * t * t);
        }

        // Unit tangent on the x, z plane in the forward direction at t.
        private Vec3 Tangent(int s, float t)
        {
            RoadSegment sg = _segs[s];
            float u = 1f - t;
            Vec3 d = (sg.B - sg.A) * (3f * u * u) + (sg.C - sg.B) * (6f * u * t) + (sg.D - sg.C) * (3f * t * t);
            d = d.Flat;
            if (d.LengthSquared < 1e-8f) d = (Bezier(ref sg, Math.Min(1f, t + 1e-3f)) - Bezier(ref sg, Math.Max(0f, t - 1e-3f))).Flat;
            if (d.LengthSquared < 1e-12f) d = (sg.D - sg.A).Flat;
            if (d.LengthSquared < 1e-12f) d = Vec3.Forward;
            return d.Normalized;
        }

        // Travel heading at arc length `along` on segment s, forward or backward.
        private float HeadingAt(int s, float along, bool forward)
        {
            Vec3 t = Tangent(s, TAt(s, along));
            return Vec3.YawDegOf(forward ? t : -t);
        }

        private float TAt(int s, float along)
        {
            int off = _sampleOff[s], n = _sampleN[s];
            float len = _sLen[s];
            if (along <= 0f || len <= 0f) return 0f;
            if (along >= len) return 1f;
            int lo = 0, hi = n;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (_sampleCum[off + mid] <= along) lo = mid; else hi = mid;
            }
            float c0 = _sampleCum[off + lo], c1 = _sampleCum[off + lo + 1];
            float f = c1 > c0 ? (along - c0) / (c1 - c0) : 0f;
            return (lo + f) / n;
        }

        private Vec3 PositionAt(int s, float along)
        {
            RoadSegment sg = _segs[s];
            return Bezier(ref sg, TAt(s, along));
        }

        private RoadPoint MakePoint(int s, float t, float along, Vec3 query)
        {
            RoadSegment sg = _segs[s];
            Vec3 pos = Bezier(ref sg, t);
            Vec3 tan = Tangent(s, t);
            var right = new Vec3(tan.Z, 0f, -tan.X);
            return new RoadPoint
            {
                SegmentId = sg.Id,
                T = t,
                Position = pos,
                Distance = Vec3.Distance(query, pos),
                LateralOffset = Vec3.Dot((query - pos).Flat, right),
                HeadingDeg = Vec3.YawDegOf(tan),
                DistanceAlong = along,
            };
        }

        private bool Nearest(Vec3 q, float maxDistance, out int bestSeg, out float bestT, out float bestAlong)
        {
            bestSeg = -1; bestT = 0f; bestAlong = 0f;
            if (!_anyDrivable || !(maxDistance >= 0f)) return false;
            float best = float.PositiveInfinity;
            _segQuery++;
            int cx = Cell(q.X), cz = Cell(q.Z);
            for (int r = 0; ; r++)
            {
                float bound = (r - 1) * GridCell;
                if (r > 0 && (bound > best || bound > maxDistance)) break;
                if (cx - r < _gMinX && cx + r > _gMaxX && cz - r < _gMinZ && cz + r > _gMaxZ && r > 0)
                {
                    // Ring lies entirely outside the occupied grid; earlier rings covered everything.
                    break;
                }
                int x0 = Math.Max(cx - r, _gMinX), x1 = Math.Min(cx + r, _gMaxX);
                int z0 = Math.Max(cz - r, _gMinZ), z1 = Math.Min(cz + r, _gMaxZ);
                for (int x = x0; x <= x1; x++)
                {
                    bool edgeX = x == cx - r || x == cx + r;
                    for (int z = z0; z <= z1; z++)
                    {
                        if (!edgeX && z != cz - r && z != cz + r)
                        {
                            if (z < cz + r) z = cz + r - 1; // skip the interior of the ring
                            continue;
                        }
                        List<int> list;
                        if (!_grid.TryGetValue(Key(x, z), out list)) continue;
                        for (int i = 0; i < list.Count; i++)
                        {
                            int s = list[i];
                            if (_segStamp[s] == _segQuery) continue;
                            _segStamp[s] = _segQuery;
                            NearestOnSegment(s, q, ref best, ref bestSeg, ref bestT, ref bestAlong);
                        }
                    }
                }
            }
            return bestSeg >= 0 && best <= maxDistance;
        }

        private void NearestOnSegment(int s, Vec3 q, ref float best, ref int bestSeg, ref float bestT, ref float bestAlong)
        {
            int off = _sampleOff[s], n = _sampleN[s];
            for (int k = 0; k < n; k++)
            {
                Vec3 a = _samplePos[off + k], b = _samplePos[off + k + 1];
                Vec3 ab = b - a;
                float l2 = ab.LengthSquared;
                float f = l2 > 1e-12f ? MathUtil.Clamp01(Vec3.Dot(q - a, ab) / l2) : 0f;
                float d = Vec3.Distance(q, a + ab * f);
                if (d < best)
                {
                    best = d;
                    bestSeg = s;
                    bestT = (k + f) / n;
                    bestAlong = _sampleCum[off + k] + (_sampleCum[off + k + 1] - _sampleCum[off + k]) * f;
                }
            }
        }

        // Node index when `along` lies at an end of segment s, else -1.
        private int NodeAt(int s, float along)
        {
            if (along <= NodeEpsilon) return _sStart[s];
            if (along >= _sLen[s] - NodeEpsilon) return _sEnd[s];
            return -1;
        }

        private bool Legal(int s, bool forward)
        {
            return forward ? _segs[s].ForwardLanes > 0 : _segs[s].BackwardLanes > 0;
        }

        // ---- A* ----

        private RouteStatus Fail(Route route, RouteStatus status, Vec3 from, Vec3 to)
        {
            route.Reset(status);
            route.GraphVersion = Version;
            route.LeftHandTraffic = LeftHandTraffic;
            route.RequestedStart = from;
            route.RequestedTarget = to;
            return status;
        }

        private RouteStatus FindRouteCore(Vec3 from, Vec3 to, Route route)
        {
            int ss, ts; float st, sa, tt, ta;
            if (!Nearest(from, RoadDefaults.RouteSnapDistance, out ss, out st, out sa)) return Fail(route, RouteStatus.NoRoadNearStart, from, to);
            if (!Nearest(to, RoadDefaults.RouteSnapDistance, out ts, out tt, out ta)) return Fail(route, RouteStatus.NoRoadNearTarget, from, to);

            int goal = _nodeCount;
            int sNode = NodeAt(ss, sa), tNode = NodeAt(ts, ta);
            _search++;
            _heapCount = 0;
            Vec3 target = PositionAt(ts, ta);

            if (sNode >= 0) Relax(sNode, 0f, -1, -1, target);
            else
            {
                if (Legal(ss, true)) Relax(_sEnd[ss], (_sLen[ss] - sa) / _sSpeed[ss], -1, ss, target);
                if (Legal(ss, false)) Relax(_sStart[ss], sa / _sSpeed[ss], -1, ss, target);
                if (tNode < 0 && ts == ss && ((ta >= sa && Legal(ss, true)) || (ta <= sa && Legal(ss, false))))
                    Relax(goal, Math.Abs(ta - sa) / _sSpeed[ss], -1, ss, target);
            }

            bool found = false;
            while (_heapCount > 0)
            {
                float key; int u;
                Pop(out key, out u);
                if (_closed[u] == _search) continue;
                _closed[u] = _search;
                if (u == goal) { found = true; break; }
                float gu = _g[u];
                if (tNode >= 0)
                {
                    if (u == tNode) Relax(goal, gu, u, -1, target);
                }
                else
                {
                    if (u == _sStart[ts] && Legal(ts, true)) Relax(goal, gu + ta / _sSpeed[ts], u, ts, target);
                    if (u == _sEnd[ts] && Legal(ts, false)) Relax(goal, gu + (_sLen[ts] - ta) / _sSpeed[ts], u, ts, target);
                }
                for (int i = _adjStart[u]; i < _adjStart[u + 1]; i++)
                {
                    int s = _adjSeg[i];
                    bool fwd = _sStart[s] == u;
                    if (!Legal(s, fwd)) continue;
                    int v = fwd ? _sEnd[s] : _sStart[s];
                    if (v == u || _closed[v] == _search) continue;
                    Relax(v, gu + _sLen[s] / _sSpeed[s], u, s, target);
                }
            }
            if (!found) return Fail(route, RouteStatus.Unreachable, from, to);

            route.Reset(RouteStatus.Ok);
            route.GraphVersion = Version;
            route.LeftHandTraffic = LeftHandTraffic;
            route.RequestedStart = from;
            route.RequestedTarget = to;

            int gp = _pNode[goal], gs = _pSeg[goal];
            if (gp < 0)
            {
                AppendPiece(route, gs, sa, ta);
            }
            else
            {
                int count = 0;
                for (int u = gp; u >= 0; u = _pNode[u])
                {
                    if (count == _path.Length) Array.Resize(ref _path, count * 2);
                    _path[count++] = u;
                }
                int first = _path[count - 1];
                int fs = _pSeg[first];
                if (fs >= 0) AppendPiece(route, fs, sa, _sStart[fs] == first ? 0f : _sLen[fs]);
                for (int k = count - 2; k >= 0; k--)
                {
                    int u = _path[k], prev = _path[k + 1], s = _pSeg[u];
                    bool fwd = _sStart[s] == prev;
                    AppendPiece(route, s, fwd ? 0f : _sLen[s], fwd ? _sLen[s] : 0f);
                }
                if (gs >= 0) AppendPiece(route, gs, _sStart[gs] == gp ? 0f : _sLen[gs], ta);
                else if (route.Count == 0) AppendPoint(route, ss, sa, true);
            }
            if (route.Count == 1) route.AddPoint(route.LastPoint);
            return RouteStatus.Ok;
        }

        private void Relax(int v, float g, int parentNode, int parentSeg, Vec3 target)
        {
            if (_stamp[v] == _search && g >= _g[v]) return;
            _stamp[v] = _search;
            _g[v] = g;
            _pNode[v] = parentNode;
            _pSeg[v] = parentSeg;
            float h = v == _nodeCount ? 0f : Vec3.Distance(_nodePos[v], target) / _vmax;
            Push(g + h, v);
        }

        private void Push(float key, int val)
        {
            if (_heapCount == _heapKey.Length)
            {
                Array.Resize(ref _heapKey, _heapCount * 2);
                Array.Resize(ref _heapVal, _heapCount * 2);
            }
            int i = _heapCount++;
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (_heapKey[parent] <= key) break;
                _heapKey[i] = _heapKey[parent];
                _heapVal[i] = _heapVal[parent];
                i = parent;
            }
            _heapKey[i] = key;
            _heapVal[i] = val;
        }

        private void Pop(out float key, out int val)
        {
            key = _heapKey[0];
            val = _heapVal[0];
            int n = --_heapCount;
            if (n == 0) return;
            float k = _heapKey[n];
            int v = _heapVal[n];
            int i = 0;
            while (true)
            {
                int c = 2 * i + 1;
                if (c >= n) break;
                if (c + 1 < n && _heapKey[c + 1] < _heapKey[c]) c++;
                if (_heapKey[c] >= k) break;
                _heapKey[i] = _heapKey[c];
                _heapVal[i] = _heapVal[c];
                i = c;
            }
            _heapKey[i] = k;
            _heapVal[i] = v;
        }

        // ---- polyline output ----

        private RoutePoint MakeRoutePoint(int s, float along, bool forward)
        {
            int node = NodeAt(s, along);
            RoadSegment sg = _segs[s];
            return new RoutePoint
            {
                Position = PositionAt(s, along),
                HeadingDeg = HeadingAt(s, along, forward),
                SegmentId = sg.Id,
                SpeedLimit = _sSpeed[s],
                Lanes = Math.Max(1, forward ? sg.ForwardLanes : sg.BackwardLanes),
                LaneWidth = _sLaneW[s],
                JunctionNodeId = node >= 0 && _degree[node] >= RoadDefaults.JunctionMinDegree ? _nodeIds[node] : -1,
            };
        }

        private void AppendPoint(Route route, int s, float along, bool forward)
        {
            RoutePoint p = MakeRoutePoint(s, along, forward);
            if (route.Count > 0)
            {
                RoutePoint last = route.LastPoint;
                if (Vec3.Distance(last.Position, p.Position) < 0.01f)
                {
                    // Same place (a node): keep one point carrying the outgoing segment's data.
                    p.Position = last.Position;
                    p.Distance = last.Distance;
                    if (p.JunctionNodeId < 0) p.JunctionNodeId = last.JunctionNodeId;
                    route.ReplaceLast(p);
                    return;
                }
            }
            route.AddPoint(p);
        }

        private void AppendPiece(Route route, int s, float a0, float a1)
        {
            float len = Math.Abs(a1 - a0);
            if (len < 1e-3f && route.Count > 0) return;
            bool forward = a1 >= a0;
            int n = Math.Max(1, (int)Math.Ceiling(len / (RoadDefaults.RouteSampleSpacing * 0.98f)));
            for (int k = 0; k <= n; k++)
            {
                float along = a0 + (a1 - a0) * k / n;
                if (k == 0) AppendPoint(route, s, along, forward);
                else route.AddPoint(MakeRoutePoint(s, along, forward));
            }
            if (route.Count < 2) route.AddPoint(route.LastPoint);
            route.AddSegment(_segs[s].Id);
        }

        // ---- wander ----

        private RouteStatus WanderCore(Vec3 from, float headingDeg, float minLength, Rng rng, Route route)
        {
            int s; float t, along;
            if (!Nearest(from, RoadDefaults.RouteSnapDistance, out s, out t, out along)) return Fail(route, RouteStatus.NoRoadNearStart, from, from);
            route.Reset(RouteStatus.Ok);
            route.GraphVersion = Version;
            route.LeftHandTraffic = LeftHandTraffic;
            route.RequestedStart = from;

            int node = NodeAt(s, along);
            int count = 0;
            if (node >= 0) count = Departures(node, -1, true);
            else
            {
                if (Legal(s, true)) AddCand(count++, s * 2 + 1);
                if (Legal(s, false)) AddCand(count++, s * 2);
            }
            if (count == 0)
            {
                AppendPoint(route, s, along, true);
                route.AddPoint(route.LastPoint);
            }
            else
            {
                int pick = float.IsNaN(headingDeg) ? _cand[rng.Range(0, count)] : MostAligned(count, node, s, along, headingDeg);
                int cs = pick >> 1;
                bool fwd = (pick & 1) == 1;
                float a0 = node >= 0 ? (fwd ? 0f : _sLen[cs]) : along;
                float a1 = fwd ? _sLen[cs] : 0f;
                AppendPiece(route, cs, a0, a1);
                int arrived = cs;
                node = fwd ? _sEnd[cs] : _sStart[cs];
                for (int guard = 0; route.Length < minLength && guard < 100000; guard++)
                {
                    count = Departures(node, arrived, true);
                    if (count == 0)
                    {
                        bool back = _sStart[arrived] == node;
                        if (!Legal(arrived, back)) break;
                        pick = arrived * 2 + (back ? 1 : 0);
                    }
                    else pick = _cand[rng.Range(0, count)];
                    cs = pick >> 1;
                    fwd = (pick & 1) == 1;
                    AppendPiece(route, cs, fwd ? 0f : _sLen[cs], fwd ? _sLen[cs] : 0f);
                    arrived = cs;
                    node = fwd ? _sEnd[cs] : _sStart[cs];
                }
            }
            route.RequestedTarget = route.End;
            return RouteStatus.Ok;
        }

        private void AddCand(int i, int value)
        {
            if (i >= _cand.Length) Array.Resize(ref _cand, _cand.Length * 2);
            _cand[i] = value;
        }

        // Writes departures from node (segment * 2 + 1 for forward, * 2 for backward) into _cand; returns the count.
        private int Departures(int node, int exclude, bool legalOnly)
        {
            int count = 0;
            for (int i = _adjStart[node]; i < _adjStart[node + 1]; i++)
            {
                int s = _adjSeg[i];
                if (s == exclude || _sStart[s] == _sEnd[s]) continue;
                bool fwd = _sStart[s] == node;
                if (legalOnly && !Legal(s, fwd)) continue;
                AddCand(count++, s * 2 + (fwd ? 1 : 0));
            }
            return count;
        }

        private int MostAligned(int count, int node, int s, float along, float headingDeg)
        {
            int best = _cand[0];
            float bestDelta = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                int cs = _cand[i] >> 1;
                bool fwd = (_cand[i] & 1) == 1;
                float a = node >= 0 ? (fwd ? 0f : _sLen[cs]) : along;
                float d = Math.Abs(MathUtil.DeltaAngleDeg(headingDeg, HeadingAt(cs, a, fwd)));
                if (d < bestDelta) { bestDelta = d; best = _cand[i]; }
            }
            return best;
        }

        // ---- prediction ----

        private bool BeginWalk(Vec3 position, float headingDeg, out int s, out float along, out bool fwd, out int startNode)
        {
            float t;
            fwd = true;
            startNode = -1;
            if (!Nearest(position, RoadDefaults.PredictSnapDistance, out s, out t, out along)) return false;
            int node = NodeAt(s, along);
            int count;
            if (node >= 0)
            {
                startNode = node;
                count = Departures(node, -1, false);
            }
            else
            {
                AddCand(0, s * 2 + 1);
                AddCand(1, s * 2);
                count = 2;
            }
            int pick = MostAligned(count, node, s, along, headingDeg);
            s = pick >> 1;
            fwd = (pick & 1) == 1;
            if (node >= 0) along = fwd ? 0f : _sLen[s];
            return true;
        }

        // Follows the most aligned road from (s, along, fwd) for up to maxDistance metres.
        private void Walk(ref int s, ref float along, ref bool fwd, int startNode, float maxDistance, List<JunctionAhead> junctions, out float travelled)
        {
            travelled = 0f;
            float remaining = Math.Max(0f, maxDistance);
            _visitId++;
            if (startNode >= 0) _visit[startNode] = _visitId;
            while (true)
            {
                float toEnd = fwd ? _sLen[s] - along : along;
                if (remaining <= toEnd)
                {
                    along += fwd ? remaining : -remaining;
                    travelled += remaining;
                    return;
                }
                travelled += toEnd;
                remaining -= toEnd;
                along = fwd ? _sLen[s] : 0f;
                int node = fwd ? _sEnd[s] : _sStart[s];
                if (junctions != null && _degree[node] >= RoadDefaults.JunctionMinDegree && travelled > 1e-3f)
                {
                    junctions.Add(new JunctionAhead
                    {
                        NodeId = _nodeIds[node],
                        Position = _nodePos[node],
                        Distance = travelled,
                        Degree = _degree[node],
                        ArrivingSegmentId = _segs[s].Id,
                    });
                }
                if (_visit[node] == _visitId) return;
                _visit[node] = _visitId;
                int count = Departures(node, s, false);
                if (count == 0) return;
                float arriving = HeadingAt(s, along, fwd);
                int pick = MostAligned(count, node, s, along, arriving);
                s = pick >> 1;
                fwd = (pick & 1) == 1;
                along = fwd ? 0f : _sLen[s];
            }
        }

        private RoadPrediction PredictCore(Vec3 position, float headingDeg, float speed, float seconds)
        {
            float dist = speed > 0f && seconds > 0f ? speed * seconds : 0f;
            int s; float along; bool fwd; int startNode;
            if (!BeginWalk(position, headingDeg, out s, out along, out fwd, out startNode))
            {
                Vec3 p = position + Vec3.FromYawDeg(headingDeg) * dist;
                return new RoadPrediction
                {
                    Position = new Vec3(p.X, position.Y, p.Z),
                    HeadingDeg = MathUtil.WrapDeg(headingDeg),
                    SegmentId = -1,
                    OnRoad = false,
                    DistanceTravelled = dist,
                };
            }
            float travelled;
            Walk(ref s, ref along, ref fwd, startNode, dist, null, out travelled);
            return new RoadPrediction
            {
                Position = PositionAt(s, along),
                HeadingDeg = HeadingAt(s, along, fwd),
                SegmentId = _segs[s].Id,
                OnRoad = true,
                DistanceTravelled = travelled,
            };
        }
    }
}
