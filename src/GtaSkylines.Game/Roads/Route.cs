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
            get { throw new NotImplementedException(); }
        }

        /// <summary>Point <paramref name="index"/> in [0, <see cref="Count"/>).</summary>
        /// <exception cref="ArgumentOutOfRangeException">Index outside [0, Count).</exception>
        public RoutePoint this[int index]
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>Number of distinct segments travelled, in order (a segment appears again only if re-entered).</summary>
        public int SegmentCount
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>The <paramref name="index"/>-th segment id travelled.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Index outside [0, SegmentCount).</exception>
        public int GetSegmentId(int index)
        {
            throw new NotImplementedException();
        }

        /// <summary>Total polyline length in metres (the last point's <see cref="RoutePoint.Distance"/>); 0 when empty.</summary>
        public float Length
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>Travel time in seconds at the speed limits: sum over polyline pieces of length ÷ limit; 0 when empty.</summary>
        public float EtaSeconds
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>The first point's position (the snapped start); <see cref="Vec3.Zero"/> when empty.</summary>
        public Vec3 Start
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>The last point's position (the snapped target); <see cref="Vec3.Zero"/> when empty.</summary>
        public Vec3 End
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>Removes all points, sets <see cref="Status"/> to <see cref="RouteStatus.None"/> and increments <see cref="Revision"/>.</summary>
        public void Clear()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Travel time in seconds at the speed limits from <paramref name="fromDistance"/> metres along the route to its
        /// end (distance clamped to [0, <see cref="Length"/>]); 0 when empty.
        /// </summary>
        public float EtaFrom(float fromDistance)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// The point at <paramref name="distance"/> metres along the route, clamped to [0, <see cref="Length"/>]:
        /// position interpolated linearly between polyline points, heading of the piece it lies on (the heading of
        /// the point itself when exactly on a point), other fields from the piece's start point and
        /// <see cref="RoutePoint.Distance"/> = the clamped distance. Default when empty.
        /// </summary>
        public RoutePoint Sample(float distance)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Projects <paramref name="position"/> onto the polyline (nearest point, 3D) and returns its distance along
        /// the route in metres; <paramref name="distanceToRoute"/> receives the 3D distance to that point. Empty route:
        /// returns 0 and <paramref name="distanceToRoute"/> = <see cref="float.PositiveInfinity"/>.
        /// </summary>
        public float Project(Vec3 position, out float distanceToRoute)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Like <see cref="Project(Vec3, out float)"/> but only considers the part of the route between
        /// <paramref name="minDistance"/> and <paramref name="maxDistance"/> metres (clamped to the route), so a
        /// vehicle is not snapped to another pass of a route that crosses itself.
        /// </summary>
        public float Project(Vec3 position, float minDistance, float maxDistance, out float distanceToRoute)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }
    }
}
