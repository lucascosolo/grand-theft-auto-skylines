using System;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Roads;
using GtaSkylines.Game.Vehicles;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Police
{
    /// <summary>How a <see cref="PoliceDriver"/> drives.</summary>
    public enum PoliceDriveMode
    {
        /// <summary>Brake 1, handbrake on (roadblocks, box-in once in its slot, cops out).</summary>
        Park = 0,
        /// <summary>Road route to <see cref="PoliceDriver.Target"/> with <see cref="RouteFollower"/>.</summary>
        Route = 1,
        /// <summary>Patrol/search wander route (<c>Roads.FindWanderRoute</c>, 600 m) inside a circle; a new route on arrival.</summary>
        Wander = 2,
        /// <summary>Direct steering at the target (pursuit, ramming, box-in approach): no road graph.</summary>
        Chase = 3,
        /// <summary>Direct steering at the player car's rear quarter (<see cref="PoliceDriver.PitAimPoint"/>).</summary>
        Pit = 4,
    }

    /// <summary>
    /// The police car AI (<see cref="IDriver"/>). One instance per unit slot, reused; the Police system sets the mode and
    /// target each step and the Vehicles system (order 20) calls <see cref="Drive"/> on the next step.
    /// <list type="bullet">
    /// <item>Route: the route is recomputed with <c>Roads.FindRoute</c> when the target moved more than
    /// <see cref="RerouteDistance"/> since the last search, or every <see cref="RerouteSeconds"/>; controls from
    /// <c>RouteFollower.Update</c> with SpeedFactor 1.25 when the siren is on (police ignore limits), 1 otherwise.</item>
    /// <item>Chase: steer = clamp(signed horizontal angle to the target / 35°, −1, 1); target speed = min(<see cref="MaxChaseSpeed"/>,
    /// distance × 1.2 + target speed), capped at 12 m/s while the angle exceeds 45°; throttle = clamp(0.25 × speed error),
    /// brake = clamp(−0.1 × speed error); handbrake when the angle exceeds 100° and speed &gt; 8 m/s. With
    /// <see cref="Ram"/> the target speed gets +6 m/s inside 10 m. The chase point leads the target by its velocity ×
    /// min(1.5 s, distance / 25 m/s).</item>
    /// <item>Falls back from Chase to Route when the target is &gt; <see cref="ChaseMaxDistance"/> away.</item>
    /// <item>Siren and LightBar follow <see cref="Siren"/>; Headlights at night.</item>
    /// </list>
    /// Zero allocation (one Route and one RouteFollower per instance, refilled).
    /// </summary>
    public sealed class PoliceDriver : IDriver
    {
        /// <summary>Recompute the route when the target moves this far, metres.</summary>
        public const float RerouteDistance = 25f;
        /// <summary>Recompute the route at least this often, seconds.</summary>
        public const float RerouteSeconds = 3f;
        /// <summary>Chase steering cap, m/s.</summary>
        public const float MaxChaseSpeed = 40f;
        /// <summary>Beyond this the Chase mode follows roads instead, metres.</summary>
        public const float ChaseMaxDistance = 80f;

        /// <summary>Creates an idle (Park) driver.</summary>
        public PoliceDriver()
        {
            Route = new Route();
            Follower = new RouteFollower();
        }

        /// <summary>The route (refilled in place).</summary>
        public Route Route { get; private set; }
        /// <summary>The route follower.</summary>
        public RouteFollower Follower { get; private set; }
        /// <summary>Mode.</summary>
        public PoliceDriveMode Mode;
        /// <summary>Target point (route destination, chase point, wander centre).</summary>
        public Vec3 Target;
        /// <summary>Target velocity for Chase/Pit lead, m/s.</summary>
        public Vec3 TargetVelocity;
        /// <summary>Wander radius for <see cref="PoliceDriveMode.Wander"/>, metres.</summary>
        public float WanderRadius;
        /// <summary>Speed multiplier (Search 0.6, others 1).</summary>
        public float SpeedFactor = 1f;
        /// <summary>Siren and light bar on.</summary>
        public bool Siren;
        /// <summary>Accept contact: speed up into the target (3★+ or Aggressive).</summary>
        public bool Ram;
        /// <summary>Seed for wander routes.</summary>
        public uint Seed;

        private bool _hasRoute;
        private Vec3 _routeTarget;
        private float _sinceRoute;
        private Rng _rng;
        private uint _rngSeed;

        /// <summary>Forgets the current route (call when the unit slot is reused).</summary>
        public void Reset()
        {
            _hasRoute = false;
            _sinceRoute = 0f;
            Route.Clear();
            Follower.Reset();
            Mode = PoliceDriveMode.Park;
            SpeedFactor = 1f;
            Siren = false;
            Ram = false;
            TargetVelocity = Vec3.Zero;
        }

        /// <summary>
        /// The PIT aim point: the player car's rear corner on the side the cop is on, local (±halfX, 0, −0.7 × halfZ),
        /// led by the player's velocity × 0.3 s.
        /// </summary>
        public static Vec3 PitAimPoint(Car player, Vec3 copPosition)
        {
            Quat r = player.Rotation;
            Vec3 right = r * Vec3.Right, fwd = r * Vec3.Forward;
            Vec3 h = player.Model.HalfExtents;
            float side = Vec3.Dot(copPosition - player.Position, right) >= 0f ? 1f : -1f;
            return player.Position + right * (side * h.X) - fwd * (0.7f * h.Z) + player.Body.Velocity * 0.3f;
        }

        /// <summary>
        /// Direct steering controls toward a point (the Chase law above), exposed for tests: <paramref name="position"/>,
        /// <paramref name="headingDeg"/> and <paramref name="speed"/> describe the cop car.
        /// </summary>
        public static VehicleControls SteerTowards(Vec3 position, float headingDeg, float speed, Vec3 target, float targetSpeed, bool ram)
        {
            Vec3 d = target - position;
            float dist = d.LengthXZ;
            float angle = dist > 1e-3f ? MathUtil.DeltaAngleDeg(headingDeg, Vec3.YawDegOf(d)) : 0f;
            VehicleControls c = new VehicleControls();
            c.Steer = MathUtil.Clamp(angle / 35f, -1f, 1f);
            float desired = Math.Min(MaxChaseSpeed, dist * 1.2f + targetSpeed);
            if (ram && dist < 10f) desired += 6f;
            if (Math.Abs(angle) > 45f) desired = Math.Min(desired, 12f);
            float err = desired - speed;
            c.Throttle = MathUtil.Clamp(0.25f * err, 0f, 1f);
            c.Brake = MathUtil.Clamp(-0.1f * err, 0f, 1f);
            c.Handbrake = Math.Abs(angle) > 100f && speed > 8f;
            return c;
        }

        /// <inheritdoc />
        public string Name { get { return "Police"; } }

        /// <inheritdoc />
        public bool Driverless { get { return false; } }

        /// <inheritdoc />
        public VehicleControls Drive(GameWorld world, Car car, float dt)
        {
            car.Siren = Siren && car.Model.HasSiren;
            car.LightBar = car.Siren;
            car.Headlights = world.Clock.IsNight;
            Vec3 pos = car.Position;
            float heading = car.Rotation.YawDeg;
            float speed = Vec3.Dot(car.Body.Velocity, car.Rotation * Vec3.Forward);
            _sinceRoute += dt;
            switch (Mode)
            {
                case PoliceDriveMode.Chase:
                case PoliceDriveMode.Pit:
                {
                    float dist = Vec3.DistanceXZ(pos, Target);
                    if (dist > ChaseMaxDistance && world.Roads.SegmentCount > 0) return FollowRoute(world, Target, pos, heading, speed);
                    Vec3 aim = Mode == PoliceDriveMode.Pit ? Target : Target + TargetVelocity * Math.Min(1.5f, dist / 25f);
                    return SteerTowards(pos, heading, speed, aim, TargetVelocity.Length, Ram || Mode == PoliceDriveMode.Pit);
                }
                case PoliceDriveMode.Route:
                    return FollowRoute(world, Target, pos, heading, speed);
                case PoliceDriveMode.Wander:
                    return Wander(world, pos, heading, speed);
                default:
                {
                    VehicleControls c = new VehicleControls();
                    c.Brake = 1f;
                    c.Handbrake = true;
                    return c;
                }
            }
        }

        private VehicleControls FollowRoute(GameWorld world, Vec3 target, Vec3 pos, float heading, float speed)
        {
            if (!_hasRoute || _sinceRoute >= RerouteSeconds || Vec3.DistanceXZ(target, _routeTarget) > RerouteDistance
                || Route.GraphVersion != world.Roads.Version)
            {
                world.Roads.FindRoute(pos, target, Route);
                Follower.Reset();
                _hasRoute = true;
                _routeTarget = target;
                _sinceRoute = 0f;
            }
            if (Route.Status != RouteStatus.Ok || Route.Count < 2)
                return SteerTowards(pos, heading, speed, target, 0f, false);
            return Follow(pos, heading, speed);
        }

        private VehicleControls Wander(GameWorld world, Vec3 pos, float heading, float speed)
        {
            if (WanderRadius > 0f && Vec3.DistanceXZ(pos, Target) > WanderRadius) return FollowRoute(world, Target, pos, heading, speed);
            if (_rng == null || _rngSeed != Seed)
            {
                _rng = new Rng(Seed);
                _rngSeed = Seed;
            }
            bool done = !_hasRoute || Route.Status != RouteStatus.Ok || Follower.Progress >= Route.Length - Follower.ArrivalDistance
                || Route.GraphVersion != world.Roads.Version;
            if (done)
            {
                world.Roads.FindWanderRoute(pos, heading, 600f, _rng, Route);
                Follower.Reset();
                _hasRoute = true;
                _routeTarget = Target;
                _sinceRoute = 0f;
            }
            if (Route.Status != RouteStatus.Ok || Route.Count < 2)
            {
                VehicleControls c = new VehicleControls();
                c.Brake = 1f;
                return c;
            }
            return Follow(pos, heading, speed);
        }

        private VehicleControls Follow(Vec3 pos, float heading, float speed)
        {
            Follower.SpeedFactor = (Siren ? 1.25f : 1f) * SpeedFactor;
            DriveCommand cmd = Follower.Update(Route, pos, heading, speed);
            VehicleControls c = new VehicleControls();
            c.Steer = cmd.Steer;
            c.Throttle = cmd.Throttle;
            c.Brake = cmd.Arrived ? 1f : cmd.Brake;
            return c;
        }
    }
}
