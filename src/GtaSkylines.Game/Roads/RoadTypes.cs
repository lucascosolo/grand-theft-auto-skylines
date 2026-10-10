using System;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.Roads
{
    /// <summary>Road properties copied from CS1's net info. Informational except where a member says otherwise.</summary>
    [Flags]
    public enum RoadFlags
    {
        /// <summary>An ordinary road.</summary>
        None = 0,
        /// <summary>A highway or ramp.</summary>
        Highway = 1,
        /// <summary>
        /// Traffic flows one way. Informational: routing reads the lane counts
        /// (<see cref="RoadSegment.ForwardLanes"/>, <see cref="RoadSegment.BackwardLanes"/>), never this flag.
        /// </summary>
        OneWay = 2,
        /// <summary>The segment is a bridge or elevated.</summary>
        Bridge = 4,
        /// <summary>The segment is in a tunnel.</summary>
        Tunnel = 8,
        /// <summary>Footpath or pedestrian street: never drivable, whatever its lane counts.</summary>
        PedestrianOnly = 16,
    }

    /// <summary>Global defaults of the roads chunk.</summary>
    public static class RoadDefaults
    {
        /// <summary>Lane width in metres used when a segment's <see cref="RoadSegment.LaneWidth"/> is ≤ 0 or not finite.</summary>
        public const float LaneWidth = 3f;

        /// <summary>Speed limit in m/s (50 km/h) used when a segment's <see cref="RoadSegment.SpeedLimit"/> is ≤ 0 or not finite.</summary>
        public const float SpeedLimit = 13.89f;

        /// <summary>
        /// Maximum distance in metres (3D) from a route's requested start or target to the nearest drivable road point.
        /// Farther than this, routing fails with <see cref="RouteStatus.NoRoadNearStart"/> / <see cref="RouteStatus.NoRoadNearTarget"/>.
        /// </summary>
        public const float RouteSnapDistance = 250f;

        /// <summary>
        /// Maximum distance in metres (3D) from a vehicle to the nearest drivable road point for prediction and
        /// junction queries to follow the road; beyond it the vehicle is treated as off-road.
        /// </summary>
        public const float PredictSnapDistance = 25f;

        /// <summary>Maximum spacing in metres between consecutive route polyline points (curves are subdivided to it).</summary>
        public const float RouteSampleSpacing = 4f;

        /// <summary>A node is a junction when at least this many drivable segments meet at it.</summary>
        public const int JunctionMinDegree = 3;

        /// <summary>GPS: the route is recomputed when the player is strictly farther than this (metres, 3D) from it.</summary>
        public const float GpsOffRouteDistance = 25f;

        /// <summary>GPS: a moving target that drifts strictly farther than this (metres, 3D) from the route's requested target triggers a recompute.</summary>
        public const float GpsTargetMoveDistance = 25f;

        /// <summary>GPS: minimum seconds between two rate-limited recomputes (straying, target drift, retry after failure).</summary>
        public const float GpsMinRecomputeInterval = 3f;
    }

    /// <summary>A road network node (a CS1 net node): junction, bend point or dead end.</summary>
    public struct RoadNode
    {
        /// <summary>Node id (CS1's ushort node id). Must be ≥ 0.</summary>
        public int Id;

        /// <summary>World position in metres.</summary>
        public Vec3 Position;

        /// <summary>Creates a node.</summary>
        public RoadNode(int id, Vec3 position)
        {
            Id = id;
            Position = position;
        }
    }

    /// <summary>
    /// A road segment (a CS1 net segment) between two nodes. Its centreline is the cubic Bézier
    /// <see cref="A"/>, <see cref="B"/>, <see cref="C"/>, <see cref="D"/>; t = 0 is at <see cref="A"/> (start node),
    /// t = 1 at <see cref="D"/> (end node). The graph uses the control points for all geometry, never the node
    /// positions. "Forward" means travelling from the start node to the end node.
    /// </summary>
    public struct RoadSegment
    {
        /// <summary>Segment id (CS1's ushort segment id). Must be ≥ 0.</summary>
        public int Id;

        /// <summary>Id of the node at t = 0.</summary>
        public int StartNode;

        /// <summary>Id of the node at t = 1.</summary>
        public int EndNode;

        /// <summary>Bézier control points in world metres.</summary>
        public Vec3 A, B, C, D;

        /// <summary>Lanes carrying traffic forward (start → end). 0 means forward travel is not allowed.</summary>
        public int ForwardLanes;

        /// <summary>Lanes carrying traffic backward (end → start). 0 means backward travel is not allowed.</summary>
        public int BackwardLanes;

        /// <summary>Width of one lane in metres; ≤ 0 means <see cref="RoadDefaults.LaneWidth"/>.</summary>
        public float LaneWidth;

        /// <summary>Speed limit in m/s; ≤ 0 means <see cref="RoadDefaults.SpeedLimit"/>.</summary>
        public float SpeedLimit;

        /// <summary>Street name as CS1 shows it; may be null or empty.</summary>
        public string Name;

        /// <summary>Road properties.</summary>
        public RoadFlags Flags;

        /// <summary>
        /// True when vehicles may use the segment at all: not <see cref="RoadFlags.PedestrianOnly"/> and at least one
        /// lane in some direction.
        /// </summary>
        public bool IsDrivable
        {
            get { return (Flags & RoadFlags.PedestrianOnly) == 0 && (ForwardLanes > 0 || BackwardLanes > 0); }
        }
    }

    /// <summary>The nearest point on a drivable road's centreline to a query position.</summary>
    public struct RoadPoint
    {
        /// <summary>The segment the point lies on.</summary>
        public int SegmentId;

        /// <summary>Bézier parameter of the point, in [0, 1].</summary>
        public float T;

        /// <summary>The point on the centreline, world metres.</summary>
        public Vec3 Position;

        /// <summary>3D distance in metres from the query position to <see cref="Position"/>.</summary>
        public float Distance;

        /// <summary>
        /// Signed distance in metres on the x, z plane from the centreline to the query position: positive when the
        /// query lies to the right of the segment's forward direction (start → end), negative to the left. Independent
        /// of the city's traffic side.
        /// </summary>
        public float LateralOffset;

        /// <summary>Heading in degrees (0 = +z, 90 = +x, as <see cref="Vec3.YawDegOf"/>) of the forward tangent at <see cref="T"/>, in (-180, 180].</summary>
        public float HeadingDeg;

        /// <summary>Arc length in metres from the segment's start (t = 0) to the point.</summary>
        public float DistanceAlong;
    }

    /// <summary>Outcome of a route computation.</summary>
    public enum RouteStatus
    {
        /// <summary>No route was requested yet, or the route was cleared.</summary>
        None = 0,
        /// <summary>A route was found; the route holds at least two points.</summary>
        Ok,
        /// <summary>No drivable road within <see cref="RoadDefaults.RouteSnapDistance"/> of the start (also: empty graph).</summary>
        NoRoadNearStart,
        /// <summary>No drivable road within <see cref="RoadDefaults.RouteSnapDistance"/> of the target.</summary>
        NoRoadNearTarget,
        /// <summary>Both ends snap to roads but no legal path connects them (disconnected network or one-way lanes).</summary>
        Unreachable,
    }

    /// <summary>One point of a route polyline. Points lie on segment centrelines; lane offsets are applied by the follower.</summary>
    public struct RoutePoint
    {
        /// <summary>World position on the centreline, metres.</summary>
        public Vec3 Position;

        /// <summary>Heading in degrees of the direction of travel at this point, in (-180, 180].</summary>
        public float HeadingDeg;

        /// <summary>Cumulative 3D polyline distance in metres from the route's first point (0 at the first point).</summary>
        public float Distance;

        /// <summary>Segment travelled from this point to the next (for the last point: the segment it lies on).</summary>
        public int SegmentId;

        /// <summary>Speed limit in m/s on <see cref="SegmentId"/> (defaults already applied).</summary>
        public float SpeedLimit;

        /// <summary>Lanes on <see cref="SegmentId"/> in the direction of travel (≥ 1).</summary>
        public int Lanes;

        /// <summary>Lane width in metres on <see cref="SegmentId"/> (defaults already applied).</summary>
        public float LaneWidth;

        /// <summary>
        /// Id of the node at this point when it is a junction (≥ <see cref="RoadDefaults.JunctionMinDegree"/> drivable
        /// segments meet there), otherwise -1.
        /// </summary>
        public int JunctionNodeId;
    }

    /// <summary>Where a vehicle is predicted to be after following the road for some time.</summary>
    public struct RoadPrediction
    {
        /// <summary>Predicted position: on a centreline when <see cref="OnRoad"/>, else straight-line dead reckoning.</summary>
        public Vec3 Position;

        /// <summary>Predicted heading of travel in degrees, in (-180, 180].</summary>
        public float HeadingDeg;

        /// <summary>Segment of the predicted position, or -1 when <see cref="OnRoad"/> is false.</summary>
        public int SegmentId;

        /// <summary>False when no drivable road was within <see cref="RoadDefaults.PredictSnapDistance"/> of the start.</summary>
        public bool OnRoad;

        /// <summary>Distance in metres actually travelled; less than speed × seconds when the path hits a dead end.</summary>
        public float DistanceTravelled;
    }

    /// <summary>A junction found ahead of a vehicle along its predicted path.</summary>
    public struct JunctionAhead
    {
        /// <summary>Node id of the junction.</summary>
        public int NodeId;

        /// <summary>The node's position (the meeting point of the segment centrelines), metres.</summary>
        public Vec3 Position;

        /// <summary>Path distance in metres from the vehicle's snapped road point to the junction.</summary>
        public float Distance;

        /// <summary>Number of drivable segments meeting at the node (≥ <see cref="RoadDefaults.JunctionMinDegree"/>).</summary>
        public int Degree;

        /// <summary>Segment on which the predicted path arrives at the junction.</summary>
        public int ArrivingSegmentId;
    }

    /// <summary>Controls for one vehicle for one step, produced by <see cref="RouteFollower"/>.</summary>
    public struct DriveCommand
    {
        /// <summary>Steering in [-1, 1]: +1 is full lock to the right (heading increasing), -1 full left.</summary>
        public float Steer;

        /// <summary>Throttle in [0, 1]. Never above 0 while <see cref="Brake"/> is above 0.</summary>
        public float Throttle;

        /// <summary>Brake in [0, 1].</summary>
        public float Brake;

        /// <summary>The speed in m/s the follower is aiming for this step (≥ 0).</summary>
        public float TargetSpeed;

        /// <summary>The vehicle's progress: distance in metres along the route of its projected position.</summary>
        public float Progress;

        /// <summary>Route length minus <see cref="Progress"/>, metres, ≥ 0.</summary>
        public float Remaining;

        /// <summary>True once <see cref="Remaining"/> ≤ <see cref="RouteFollower.ArrivalDistance"/>; then throttle is 0 and brake 1.</summary>
        public bool Arrived;

        /// <summary>True when a stop point is set and the vehicle is within <see cref="RouteFollower.StopTolerance"/> of it or past it; then throttle is 0 and brake 1.</summary>
        public bool AtStopPoint;
    }
}
