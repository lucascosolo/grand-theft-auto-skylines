using System;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.Roads
{
    /// <summary>
    /// The HUD's GPS line from the player to a target. Owns one <see cref="Route"/> that it refills in place.
    /// <see cref="Tick"/> recomputes:
    /// <list type="bullet">
    /// <item>immediately when a target was set while none was held, or when the graph's <see cref="RoadGraph.Version"/>
    /// differs from the route's <see cref="Route.GraphVersion"/>;</item>
    /// <item>rate-limited (only when at least <see cref="RoadDefaults.GpsMinRecomputeInterval"/> seconds have passed
    /// since the last recompute) when the player is strictly farther than <see cref="RoadDefaults.GpsOffRouteDistance"/>
    /// from the route, when the target has moved strictly farther than <see cref="RoadDefaults.GpsTargetMoveDistance"/>
    /// from the route's <see cref="Route.RequestedTarget"/>, or when the last attempt did not return
    /// <see cref="RouteStatus.Ok"/> (retry).</item>
    /// </list>
    /// Time is the sum of <c>dt</c> passed to <see cref="Tick"/>. Steady-state ticks allocate nothing.
    /// </summary>
    public sealed class GpsRoute
    {
        private readonly RoadGraph _graph;
        private readonly Route _route = new Route();
        private float _clock;
        private float _lastCompute;
        private bool _pending;

        /// <summary>Creates a GPS bound to a graph.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="graph"/> is null.</exception>
        public GpsRoute(RoadGraph graph)
        {
            if (graph == null) throw new ArgumentNullException("graph");
            _graph = graph;
            OffRouteDistance = float.PositiveInfinity;
        }

        /// <summary>The route shown on the HUD (empty when there is no target or no route).</summary>
        public Route Route
        {
            get { return _route; }
        }

        /// <summary>True while a target is set.</summary>
        public bool HasTarget { get; private set; }

        /// <summary>The current target position; meaningful only while <see cref="HasTarget"/>.</summary>
        public Vec3 Target { get; private set; }

        /// <summary>Status of the last recompute; <see cref="RouteStatus.None"/> before the first or after <see cref="ClearTarget"/>.</summary>
        public RouteStatus Status
        {
            get { return _route.Status; }
        }

        /// <summary>The player's distance in metres along the route at the last tick (0 when no route).</summary>
        public float Progress { get; private set; }

        /// <summary>Route length minus <see cref="Progress"/> in metres at the last tick (0 when no route).</summary>
        public float Remaining { get; private set; }

        /// <summary>3D distance in metres from the player to the route at the last tick (<see cref="float.PositiveInfinity"/> when no route).</summary>
        public float OffRouteDistance { get; private set; }

        /// <summary>Number of route computations so far (diagnostics and tests).</summary>
        public int RecomputeCount { get; private set; }

        /// <summary>
        /// Sets or moves the target. Call every frame for a moving target; small moves do not trigger recomputes
        /// (see the class rules). Takes effect at the next <see cref="Tick"/>.
        /// </summary>
        public void SetTarget(Vec3 target)
        {
            if (!HasTarget) _pending = true;
            HasTarget = true;
            Target = target;
        }

        /// <summary>Removes the target and clears the route (status <see cref="RouteStatus.None"/>).</summary>
        public void ClearTarget()
        {
            HasTarget = false;
            _pending = false;
            _route.Clear();
            Progress = 0f;
            Remaining = 0f;
            OffRouteDistance = float.PositiveInfinity;
        }

        /// <summary>
        /// Advances the clock by <paramref name="dt"/> seconds, recomputes if the class rules say so, then updates
        /// <see cref="Progress"/>, <see cref="Remaining"/> and <see cref="OffRouteDistance"/> for
        /// <paramref name="playerPosition"/>. Does nothing but advance the clock without a target.
        /// </summary>
        /// <returns>True when the route was recomputed during this call.</returns>
        public bool Tick(float dt, Vec3 playerPosition)
        {
            _clock += dt;
            if (!HasTarget) return false;
            float off;
            float along = _route.Project(playerPosition, out off);
            bool recompute = _pending || _route.GraphVersion != _graph.Version;
            if (!recompute && _clock - _lastCompute >= RoadDefaults.GpsMinRecomputeInterval)
            {
                recompute = _route.Status != RouteStatus.Ok
                    || off > RoadDefaults.GpsOffRouteDistance
                    || Vec3.Distance(Target, _route.RequestedTarget) > RoadDefaults.GpsTargetMoveDistance;
            }
            if (recompute)
            {
                _pending = false;
                _lastCompute = _clock;
                RecomputeCount++;
                _graph.FindRoute(playerPosition, Target, _route);
                along = _route.Project(playerPosition, out off);
            }
            Progress = _route.Count > 0 ? along : 0f;
            Remaining = _route.Count > 0 ? Math.Max(0f, _route.Length - along) : 0f;
            OffRouteDistance = off;
            return recompute;
        }
    }
}
