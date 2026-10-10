using System;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.Roads
{
    /// <summary>
    /// Drives one AI vehicle along a <see cref="Route"/>: pure-pursuit steering toward a lookahead point in the
    /// kerb-side lane, and a target speed limited by the speed limit, curves, junction turns, the stop point and the
    /// route end. One instance per vehicle; it keeps the vehicle's progress between calls. <see cref="Update"/>
    /// allocates nothing.
    /// <para>
    /// Lane: the follower aims at the centre of the kerb-side lane for the direction of travel, i.e.
    /// (<see cref="RoutePoint.Lanes"/> − 0.5) × <see cref="RoutePoint.LaneWidth"/> metres from the centreline, to the
    /// right of travel with right-hand traffic and to the left when <see cref="Route.LeftHandTraffic"/>, then adds
    /// <see cref="LateralOffset"/>.
    /// </para>
    /// <para>
    /// Speed: the target is the minimum of (a) the current point's speed limit × <see cref="SpeedFactor"/>,
    /// (b) <see cref="MaxSpeed"/>, (c) for every route point within the braking scan ahead
    /// (v² / (2 × <see cref="ComfortDecel"/>) + 10 m) a cap c at distance d giving √(c² + 2 × ComfortDecel × d),
    /// where c is √(<see cref="LateralAccel"/> × r) for the local curve radius r and, at a junction point whose
    /// heading change exceeds <see cref="JunctionTurnAngleDeg"/>, at most <see cref="JunctionTurnSpeed"/>,
    /// (d) min(√(2 × ComfortDecel × d), d ÷ <see cref="StopApproachTime"/>) for the distance d to the stop point
    /// and to the route end (the linear term makes the final approach a slow creep).
    /// </para>
    /// <para>
    /// Pedals: below the target, throttle = clamp((target − speed) × <see cref="ThrottleGain"/>, 0, 1) and brake 0;
    /// above target + <see cref="SpeedTolerance"/>, brake = clamp((speed − target) × <see cref="BrakeGain"/>, 0, 1)
    /// and throttle 0; in between both 0. Arrived or at the stop point: throttle 0, brake 1.
    /// </para>
    /// </summary>
    public sealed class RouteFollower
    {
        private Route _route;
        private int _revision;
        private bool _tracking;

        /// <summary>Distance between axles in metres used for pure pursuit. Default 2.7.</summary>
        public float Wheelbase = 2.7f;

        /// <summary>Road-wheel angle in degrees that <see cref="DriveCommand.Steer"/> = ±1 represents. Default 35.</summary>
        public float MaxSteerAngleDeg = 35f;

        /// <summary>Lookahead distance = speed × this (seconds), clamped to [<see cref="MinLookahead"/>, <see cref="MaxLookahead"/>]. Default 0.8.</summary>
        public float LookaheadTime = 0.8f;

        /// <summary>Minimum lookahead in metres. Default 5.</summary>
        public float MinLookahead = 5f;

        /// <summary>Maximum lookahead in metres. Default 30.</summary>
        public float MaxLookahead = 30f;

        /// <summary>Lateral acceleration in m/s² allowed in curves. Default 4.</summary>
        public float LateralAccel = 4f;

        /// <summary>Deceleration in m/s² used to plan slowing and stopping. Default 5.</summary>
        public float ComfortDecel = 5f;

        /// <summary>Speed cap in m/s at a junction where the route turns. Default 7.</summary>
        public float JunctionTurnSpeed = 7f;

        /// <summary>Heading change in degrees at a junction above which <see cref="JunctionTurnSpeed"/> applies. Default 25.</summary>
        public float JunctionTurnAngleDeg = 25f;

        /// <summary>Speed cap in m/s while the lookahead point is more than 90° off the vehicle's heading (turning round). Default 4.</summary>
        public float TurnaroundSpeed = 4f;

        /// <summary>Remaining distance in metres at or below which the route counts as arrived. Default 3.</summary>
        public float ArrivalDistance = 3f;

        /// <summary>Distance in metres before the stop point at or within which the vehicle counts as stopped there. Default 1.</summary>
        public float StopTolerance = 1f;

        /// <summary>Near a stop point or the route end the target is at most the remaining distance ÷ this (seconds). Default 3.</summary>
        public float StopApproachTime = 3f;

        /// <summary>Speed error in m/s tolerated above the target before braking. Default 0.5.</summary>
        public float SpeedTolerance = 0.5f;

        /// <summary>Throttle per m/s below target. Default 0.25.</summary>
        public float ThrottleGain = 0.25f;

        /// <summary>Brake per m/s above target. Default 1.</summary>
        public float BrakeGain = 1f;

        /// <summary>Progress is searched in [previous − this, previous + <see cref="ProgressSearchAhead"/>] metres. Default 5.</summary>
        public float ProgressSearchBehind = 5f;

        /// <summary>See <see cref="ProgressSearchBehind"/>. Default 50.</summary>
        public float ProgressSearchAhead = 50f;

        /// <summary>
        /// Extra lateral offset in metres added to the lane target, positive to the right of the direction of travel
        /// (whatever the traffic side); e.g. −3 to pull out to overtake with right-hand traffic. Default 0.
        /// </summary>
        public float LateralOffset;

        /// <summary>Multiplier on the speed limit (police in pursuit use more than 1). Default 1.</summary>
        public float SpeedFactor = 1f;

        /// <summary>Absolute speed cap in m/s. Default <see cref="float.MaxValue"/>.</summary>
        public float MaxSpeed = float.MaxValue;

        /// <summary>True while a stop point is set.</summary>
        public bool HasStop { get; private set; }

        /// <summary>Distance in metres along the route of the stop point; meaningful only while <see cref="HasStop"/>.</summary>
        public float StopDistance { get; private set; }

        /// <summary>Last computed progress in metres along the route (0 after <see cref="Reset"/>).</summary>
        public float Progress { get; private set; }

        /// <summary>
        /// Sets a point, as a distance along the current route in metres, at which to come to rest. Cleared by
        /// <see cref="ClearStop"/>, <see cref="Reset"/> and when the route's <see cref="Route.Revision"/> changes.
        /// </summary>
        public void SetStop(float distanceAlongRoute)
        {
            HasStop = true;
            StopDistance = distanceAlongRoute;
        }

        /// <summary>Removes the stop point.</summary>
        public void ClearStop()
        {
            HasStop = false;
            StopDistance = 0f;
        }

        /// <summary>Forgets progress and the stop point; the next <see cref="Update"/> projects onto the whole route.</summary>
        public void Reset()
        {
            ClearStop();
            Progress = 0f;
            _tracking = false;
            _route = null;
        }

        /// <summary>
        /// Computes this step's controls. <paramref name="speed"/> is the forward speed in m/s (negative is treated as
        /// 0 for lookahead). On the first call, after <see cref="Reset"/> or when the route's
        /// <see cref="Route.Revision"/> differs from the last call, progress is found by projecting onto the whole
        /// route; otherwise within the progress search window. A vehicle far from the route still steers toward its
        /// lookahead point; no reversing is ever commanded. A null route or one with fewer than two points gives
        /// steer 0, throttle 0, brake 1, not arrived.
        /// </summary>
        public DriveCommand Update(Route route, Vec3 position, float headingDeg, float speed)
        {
            var cmd = new DriveCommand();
            if (route == null || route.Count < 2)
            {
                cmd.Brake = 1f;
                return cmd;
            }
            float v = Math.Max(0f, speed);
            float off;
            if (!_tracking || route != _route || route.Revision != _revision)
            {
                if (_tracking) ClearStop();
                _tracking = true;
                _route = route;
                _revision = route.Revision;
                Progress = route.Project(position, out off);
            }
            else
            {
                Progress = route.Project(position, Progress - ProgressSearchBehind, Progress + ProgressSearchAhead, out off);
            }
            float length = route.Length;
            cmd.Progress = Progress;
            cmd.Remaining = Math.Max(0f, length - Progress);

            // Steering: pure pursuit toward the lane point one lookahead ahead.
            float look = MathUtil.Clamp(v * LookaheadTime, MinLookahead, MaxLookahead);
            Vec3 target = LanePoint(route, Progress + look);
            Vec3 to = (target - position).Flat;
            float dist = to.Length;
            float alpha = dist > 1e-4f ? MathUtil.DeltaAngleDeg(headingDeg, Vec3.YawDegOf(to)) : 0f;
            bool turnaround = Math.Abs(alpha) > 90f;
            if (turnaround) cmd.Steer = Math.Sign(alpha);
            else if (dist > 1e-4f)
            {
                double curvature = 2.0 * Math.Sin(alpha * MathUtil.Deg2Rad) / dist;
                double wheel = Math.Atan(Wheelbase * curvature) * MathUtil.Rad2Deg;
                cmd.Steer = MathUtil.Clamp((float)(wheel / MaxSteerAngleDeg), -1f, 1f);
            }

            // Target speed.
            float vt = Math.Min(route.Sample(Progress).SpeedLimit * SpeedFactor, MaxSpeed);
            float a = Math.Max(0.1f, ComfortDecel);
            float scan = v * v / (2f * a) + 10f;
            for (int i = FirstPointAtOrBefore(route, Progress); i < route.Count - 1; i++)
            {
                RoutePoint p = route[i], q = route[i + 1];
                float d = Math.Max(0f, p.Distance - Progress);
                if (d > scan) break;
                float cap = float.MaxValue;
                float span = q.Distance - p.Distance;
                float turn = Math.Abs(MathUtil.DeltaAngleDeg(p.HeadingDeg, q.HeadingDeg));
                if (span > 1e-3f && turn > 1e-3f)
                    cap = (float)Math.Sqrt(LateralAccel * span / (turn * MathUtil.Deg2Rad));
                if (p.JunctionNodeId >= 0 && i > 0 && Math.Abs(MathUtil.DeltaAngleDeg(route[i - 1].HeadingDeg, q.HeadingDeg)) > JunctionTurnAngleDeg)
                    cap = Math.Min(cap, JunctionTurnSpeed);
                if (cap < float.MaxValue) vt = Math.Min(vt, (float)Math.Sqrt(cap * cap + 2f * a * d));
            }
            vt = Math.Min(vt, ApproachSpeed(cmd.Remaining, a));
            if (HasStop)
            {
                float ds = StopDistance - Progress;
                if (ds <= StopTolerance) cmd.AtStopPoint = true;
                else vt = Math.Min(vt, ApproachSpeed(ds, a));
            }
            if (turnaround) vt = Math.Min(vt, TurnaroundSpeed);
            cmd.Arrived = cmd.Remaining <= ArrivalDistance;
            if (cmd.Arrived || cmd.AtStopPoint) vt = 0f;
            cmd.TargetSpeed = Math.Max(0f, vt);

            if (cmd.Arrived || cmd.AtStopPoint)
            {
                cmd.Brake = 1f;
            }
            else if (v < cmd.TargetSpeed)
            {
                cmd.Throttle = MathUtil.Clamp((cmd.TargetSpeed - v) * ThrottleGain, 0f, 1f);
            }
            else if (v > cmd.TargetSpeed + SpeedTolerance)
            {
                cmd.Brake = MathUtil.Clamp((v - cmd.TargetSpeed) * BrakeGain, 0f, 1f);
            }
            return cmd;
        }
    
        private float ApproachSpeed(float distance, float decel)
        {
            if (distance <= 0f) return 0f;
            float linear = StopApproachTime > 0f ? distance / StopApproachTime : float.MaxValue;
            return Math.Min((float)Math.Sqrt(2f * decel * distance), linear);
        }

        // Centre of the target lane at a distance along the route; beyond the end, extended along the final heading.
        private Vec3 LanePoint(Route route, float distance)
        {
            RoutePoint p = route.Sample(distance);
            Vec3 pos = p.Position;
            float beyond = distance - route.Length;
            if (beyond > 0f) pos = pos + Vec3.FromYawDeg(p.HeadingDeg) * beyond;
            float side = (p.Lanes - 0.5f) * p.LaneWidth;
            if (route.LeftHandTraffic) side = -side;
            side += LateralOffset;
            double h = p.HeadingDeg * MathUtil.Deg2Rad;
            return pos + new Vec3((float)Math.Cos(h), 0f, (float)-Math.Sin(h)) * side;
        }

        private static int FirstPointAtOrBefore(Route route, float distance)
        {
            int lo = 0, hi = route.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (route[mid].Distance <= distance) lo = mid; else hi = mid - 1;
            }
            return lo;
        }
    }
}
