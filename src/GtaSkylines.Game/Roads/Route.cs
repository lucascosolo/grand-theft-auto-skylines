using System;
using System.Collections.Generic;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.Roads
{
    /// <summary>
    /// A route polyline owned by the caller and refilled in place by <see cref="RoadGraph.FindRoute"/> and
    /// <see cref="RoadGraph.FindWanderRoute"/>, so steady-state recomputes reuse its storage. Points lie on segment
    /// centrelines in order of travel.
    /// </summary>
    public sealed class Route
    {
        private readonly List<RoutePoint> _points = new List<RoutePoint>();
        private readonly List<float> _time = new List<float>();
        private readonly List<int> _segments = new List<int>();

        /// <summary>Outcome of the last fill; <see cref="RouteStatus.None"/> for a new or cleared route.</summary>
        public RouteStatus Status { get; internal set; }

        /// <summary>Incremented on every fill and every <see cref="Clear"/>; followers use it to detect a new route.</summary>
        public int Revision { get; internal set; }

        /// <summary><see cref="RoadGraph.Version"/> of the graph that filled the route.</summary>
        public int GraphVersion { get; internal set; }

        /// <summary>The graph's <see cref="RoadGraph.LeftHandTraffic"/> when the route was filled.</summary>
        public bool LeftHandTraffic { get; internal set; }

        /// <summary>The start position the route was requested from (may be off-road).</summary>
        public Vec3 RequestedStart { get; internal set; }

        /// <summary>The target position requested (may be off-road); for a wander route, equal to the last point.</summary>
        public Vec3 RequestedTarget { get; internal set; }

        /// <summary>Number of polyline points: 0 unless <see cref="Status"/> is <see cref="RouteStatus.Ok"/>, then ≥ 2.</summary>
        public int Count
        {
            get { return _points.Count; }
        }

        /// <summary>Point <paramref name="index"/> in [0, <see cref="Count"/>).</summary>
        /// <exception cref="ArgumentOutOfRangeException">Index outside [0, Count).</exception>
        public RoutePoint this[int index]
        {
            get
            {
                if (index < 0 || index >= _points.Count) throw new ArgumentOutOfRangeException("index");
                return _points[index];
            }
        }

        /// <summary>Number of distinct segments travelled, in order (a segment appears again only if re-entered).</summary>
        public int SegmentCount
        {
            get { return _segments.Count; }
        }

        /// <summary>The <paramref name="index"/>-th segment id travelled.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Index outside [0, SegmentCount).</exception>
        public int GetSegmentId(int index)
        {
            if (index < 0 || index >= _segments.Count) throw new ArgumentOutOfRangeException("index");
            return _segments[index];
        }

        /// <summary>Total polyline length in metres (the last point's <see cref="RoutePoint.Distance"/>); 0 when empty.</summary>
        public float Length
        {
            get { return _points.Count == 0 ? 0f : _points[_points.Count - 1].Distance; }
        }

        /// <summary>Travel time in seconds at the speed limits: sum over polyline pieces of length ÷ limit; 0 when empty.</summary>
        public float EtaSeconds
        {
            get { return _time.Count == 0 ? 0f : _time[_time.Count - 1]; }
        }

        /// <summary>The first point's position (the snapped start); <see cref="Vec3.Zero"/> when empty.</summary>
        public Vec3 Start
        {
            get { return _points.Count == 0 ? Vec3.Zero : _points[0].Position; }
        }

        /// <summary>The last point's position (the snapped target); <see cref="Vec3.Zero"/> when empty.</summary>
        public Vec3 End
        {
            get { return _points.Count == 0 ? Vec3.Zero : _points[_points.Count - 1].Position; }
        }

        /// <summary>Removes all points, sets <see cref="Status"/> to <see cref="RouteStatus.None"/> and increments <see cref="Revision"/>.</summary>
        public void Clear()
        {
            Reset(RouteStatus.None);
        }

        /// <summary>
        /// Travel time in seconds at the speed limits from <paramref name="fromDistance"/> metres along the route to its
        /// end (distance clamped to [0, <see cref="Length"/>]); 0 when empty.
        /// </summary>
        public float EtaFrom(float fromDistance)
        {
            if (_points.Count == 0) return 0f;
            return EtaSeconds - TimeAt(fromDistance);
        }

        /// <summary>
        /// The point at <paramref name="distance"/> metres along the route, clamped to [0, <see cref="Length"/>]:
        /// position interpolated linearly between polyline points, heading of the piece it lies on (the heading of
        /// the point itself when exactly on a point), other fields from the piece's start point and
        /// <see cref="RoutePoint.Distance"/> = the clamped distance. Default when empty.
        /// </summary>
        public RoutePoint Sample(float distance)
        {
            int n = _points.Count;
            if (n == 0) return default(RoutePoint);
            float d = MathUtil.Clamp(distance, 0f, Length);
            int i = PieceAt(d);
            RoutePoint a = _points[i];
            if (i == n - 1 || d == a.Distance) { a.Distance = d; return a; }
            RoutePoint b = _points[i + 1];
            float span = b.Distance - a.Distance;
            float f = span > 1e-6f ? (d - a.Distance) / span : 0f;
            Vec3 chord = b.Position - a.Position;
            a.Position = Vec3.Lerp(a.Position, b.Position, f);
            if (chord.LengthXZ > 1e-4f) a.HeadingDeg = Vec3.YawDegOf(chord);
            a.Distance = d;
            a.JunctionNodeId = -1;
            return a;
        }

        /// <summary>
        /// Projects <paramref name="position"/> onto the polyline (nearest point, 3D) and returns its distance along
        /// the route in metres; <paramref name="distanceToRoute"/> receives the 3D distance to that point. Empty route:
        /// returns 0 and <paramref name="distanceToRoute"/> = <see cref="float.PositiveInfinity"/>.
        /// </summary>
        public float Project(Vec3 position, out float distanceToRoute)
        {
            return Project(position, float.NegativeInfinity, float.PositiveInfinity, out distanceToRoute);
        }

        /// <summary>
        /// Like <see cref="Project(Vec3, out float)"/> but only considers the part of the route between
        /// <paramref name="minDistance"/> and <paramref name="maxDistance"/> metres (clamped to the route), so a
        /// vehicle is not snapped to another pass of a route that crosses itself.
        /// </summary>
        public float Project(Vec3 position, float minDistance, float maxDistance, out float distanceToRoute)
        {
            int n = _points.Count;
            distanceToRoute = float.PositiveInfinity;
            if (n == 0) return 0f;
            float lo = MathUtil.Clamp(minDistance, 0f, Length), hi = MathUtil.Clamp(maxDistance, 0f, Length);
            if (hi < lo) hi = lo;
            if (n == 1 || hi - lo < 1e-6f)
            {
                distanceToRoute = Vec3.Distance(position, Sample(lo).Position);
                return lo;
            }
            float best = float.PositiveInfinity, bestAlong = lo;
            for (int i = PieceAt(lo); i < n - 1; i++)
            {
                RoutePoint a = _points[i], b = _points[i + 1];
                if (a.Distance > hi) break;
                float span = b.Distance - a.Distance;
                float d0 = Math.Max(a.Distance, lo), d1 = Math.Min(b.Distance, hi);
                Vec3 p0 = a.Position, p1 = b.Position;
                if (span > 1e-6f)
                {
                    p0 = Vec3.Lerp(a.Position, b.Position, (d0 - a.Distance) / span);
                    p1 = Vec3.Lerp(a.Position, b.Position, (d1 - a.Distance) / span);
                }
                Vec3 seg = p1 - p0;
                float l2 = seg.LengthSquared;
                float f = l2 > 1e-12f ? MathUtil.Clamp01(Vec3.Dot(position - p0, seg) / l2) : 0f;
                float dist = Vec3.Distance(position, p0 + seg * f);
                if (dist < best)
                {
                    best = dist;
                    bestAlong = d0 + (d1 - d0) * f;
                }
            }
            distanceToRoute = best;
            return bestAlong;
        }

        /// <summary>
        /// Appends to <paramref name="output"/> (cleared first) the positions at <paramref name="fromDistance"/> +
        /// k × <paramref name="spacing"/> for k = 1, 2, ... while the distance does not exceed <see cref="Length"/>,
        /// at most <paramref name="maxCount"/> positions. Nothing when empty or <paramref name="spacing"/> ≤ 0.
        /// </summary>
        /// <returns>The number of positions written.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="output"/> is null.</exception>
        public int PointsAhead(float fromDistance, float spacing, int maxCount, List<Vec3> output)
        {
            if (output == null) throw new ArgumentNullException("output");
            output.Clear();
            if (_points.Count == 0 || spacing <= 0f) return 0;
            float length = Length;
            for (int k = 1; output.Count < maxCount; k++)
            {
                float d = fromDistance + k * spacing;
                if (d > length) break;
                output.Add(Sample(d).Position);
            }
            return output.Count;
        }
    
        internal void Reset(RouteStatus status)
        {
            _points.Clear();
            _time.Clear();
            _segments.Clear();
            Status = status;
            Revision++;
        }

        internal RoutePoint LastPoint { get { return _points[_points.Count - 1]; } }

        internal void ReplaceLast(RoutePoint p)
        {
            _points[_points.Count - 1] = p;
        }

        internal void AddPoint(RoutePoint p)
        {
            int n = _points.Count;
            if (n == 0)
            {
                p.Distance = 0f;
                _points.Add(p);
                _time.Add(0f);
                return;
            }
            RoutePoint prev = _points[n - 1];
            float step = Vec3.Distance(prev.Position, p.Position);
            p.Distance = prev.Distance + step;
            _points.Add(p);
            _time.Add(_time[n - 1] + (prev.SpeedLimit > 0f ? step / prev.SpeedLimit : 0f));
        }

        internal void AddSegment(int id)
        {
            _segments.Add(id);
        }

        // Index of the polyline point at or before distance d (d already in [0, Length]).
        private int PieceAt(float d)
        {
            int lo = 0, hi = _points.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (_points[mid].Distance <= d) lo = mid; else hi = mid - 1;
            }
            return lo;
        }

        private float TimeAt(float distance)
        {
            int n = _points.Count;
            float d = MathUtil.Clamp(distance, 0f, Length);
            int i = PieceAt(d);
            if (i == n - 1) return _time[i];
            float span = _points[i + 1].Distance - _points[i].Distance;
            float f = span > 1e-6f ? (d - _points[i].Distance) / span : 0f;
            return _time[i] + (_time[i + 1] - _time[i]) * f;
        }
    }
}
