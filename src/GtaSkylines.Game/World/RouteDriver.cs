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
            Destination = destination;
            HasDestination = true;
            Arrived = false;
            _needRoute = true;
            Follower.Reset();
        }

        private bool _needRoute;
        private int _graphVersion = -1;
        private float _retry;

        /// <inheritdoc />
        public string Name { get { return "Route"; } }

        /// <inheritdoc />
        public bool Driverless { get { return false; } }

        /// <inheritdoc />
        public VehicleControls Drive(GameWorld world, Car car, float dt)
        {
            var stop = new VehicleControls { Brake = 1f };
            if (!HasDestination || world == null || car == null) return stop;
            _retry -= dt;
            if (_needRoute || world.Roads.Version != _graphVersion || (Status != RouteStatus.Ok && _retry <= 0f))
            {
                Status = world.Roads.FindRoute(car.Position, Destination, Route);
                _graphVersion = world.Roads.Version;
                _needRoute = false;
                _retry = 2f;
                Follower.Reset();
            }
            if (Status != RouteStatus.Ok) return stop;
            VehicleBody body = car.Body;
            float speed = Vec3.Dot(body.Velocity, body.Rotation.Forward);
            DriveCommand cmd = Follower.Update(Route, car.Position, body.Rotation.YawDeg, speed);
            if (cmd.Arrived) Arrived = true;
            if (Arrived) return stop;
            return new VehicleControls { Steer = cmd.Steer, Throttle = cmd.Throttle, Brake = cmd.Brake };
        }
    }
}
