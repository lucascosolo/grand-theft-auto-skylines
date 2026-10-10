using System;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Roads;
using GtaSkylines.Game.Vehicles;

namespace GtaSkylines.Game.World
{
    /// <summary>
    /// Simple "drive to" AI over <see cref="RouteFollower"/>: finds a road route from the car to a destination with
    /// <c>world.Roads.FindRoute</c> and follows it. Controls: Steer = command.Steer, Throttle = command.Throttle,
    /// Brake = command.Brake, Handbrake false; after arrival brake 1. Heading and forward speed are taken from the body
    /// (<c>Rotation.YawDeg</c>, Dot(Velocity, Forward)).
    /// </summary>
    public sealed class RouteDriver : IDriver
    {
        /// <summary>Creates a driver with no destination (it brakes until <see cref="SetDestination"/>).</summary>
        public RouteDriver()
        {
            Route = new Route();
            Follower = new RouteFollower();
        }

        /// <summary>The route being followed (refilled in place).</summary>
        public Route Route { get; private set; }

        /// <summary>The follower (tunable: SpeedFactor, MaxSpeed, LateralOffset...).</summary>
        public RouteFollower Follower { get; private set; }

        /// <summary>True once a destination is set.</summary>
        public bool HasDestination { get; private set; }

        /// <summary>The destination.</summary>
        public Vec3 Destination { get; private set; }

        /// <summary>Status of the last route search (<see cref="RouteStatus"/>); a failed search is retried every 2 s.</summary>
        public RouteStatus Status { get; private set; }

        /// <summary>True once the follower reported arrival at the destination.</summary>
        public bool Arrived { get; private set; }

        /// <summary>Sets a destination; the route is computed on the next <see cref="Drive"/> (and recomputed when the road graph version changes). Clears Arrived.</summary>
        public void SetDestination(Vec3 destination)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public string Name { get { return "Route"; } }

        /// <inheritdoc />
        public bool Driverless { get { return false; } }

        /// <inheritdoc />
        public VehicleControls Drive(GameWorld world, Car car, float dt)
        {
            throw new NotImplementedException();
        }
    }
}
