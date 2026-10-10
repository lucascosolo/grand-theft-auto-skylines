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
    /// distance × 1.2 + target speed); throttle/brake proportional to the speed error (gain 0.25 / 1); handbrake when the
    /// angle exceeds 100° and speed &gt; 8 m/s. With <see cref="Ram"/> the target speed gets +6 m/s inside 10 m.</item>
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

        /// <summary>
        /// The PIT aim point: the player car's rear corner on the side the cop is on, local (±halfX, 0, −0.7 × halfZ),
        /// led by the player's velocity × 0.3 s.
        /// </summary>
        public static Vec3 PitAimPoint(Car player, Vec3 copPosition)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Direct steering controls toward a point (the Chase law above), exposed for tests: <paramref name="position"/>,
        /// <paramref name="headingDeg"/> and <paramref name="speed"/> describe the cop car.
        /// </summary>
        public static VehicleControls SteerTowards(Vec3 position, float headingDeg, float speed, Vec3 target, float targetSpeed, bool ram)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public string Name { get { return "Police"; } }

        /// <inheritdoc />
        public bool Driverless { get { return false; } }

        /// <inheritdoc />
        public VehicleControls Drive(GameWorld world, Car car, float dt)
        {
            throw new NotImplementedException();
        }
    }
}
