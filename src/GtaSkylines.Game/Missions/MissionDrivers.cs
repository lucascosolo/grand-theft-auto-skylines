using System;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Roads;
using GtaSkylines.Game.Vehicles;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Missions
{
    /// <summary>
    /// A fleeing getaway driver (chase targets). Follows <c>RoadGraph.FindWanderRoute</c> routes of at least 600 m that
    /// start on the heading pointing away from the player (bearing from player to car), replanning when fewer than
    /// 60 m of the route remain or the route search failed (retry every 1 s). Speed: <c>Follower.SpeedFactor</c> =
    /// <see cref="SpeedFactor"/> × the road limit. Headlights on at night; never sirens. Needs a living ped in seat 0.
    /// </summary>
    public sealed class FleeDriver : IDriver
    {
        /// <summary>Creates a driver with its own seeded random source for route picks.</summary>
        public FleeDriver(uint seed, float speedFactor)
        {
            Rng = new Rng(seed);
            SpeedFactor = speedFactor;
            Route = new Route();
            Follower = new RouteFollower();
        }

        /// <summary>Random source for wander routes.</summary>
        public Rng Rng { get; private set; }

        /// <summary>Speed factor over the limit (1.25 for chases).</summary>
        public float SpeedFactor { get; private set; }

        /// <summary>The current route (refilled in place).</summary>
        public Route Route { get; private set; }

        /// <summary>The follower.</summary>
        public RouteFollower Follower { get; private set; }

        /// <inheritdoc />
        public string Name { get { return "Flee"; } }

        /// <inheritdoc />
        public bool Driverless { get { return false; } }

        /// <inheritdoc />
        public VehicleControls Drive(GameWorld world, Car car, float dt)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// A street-race rival: drives the checkpoint list in order, one <c>RoadGraph.FindRoute</c> leg per checkpoint
    /// (next leg planned when within <see cref="RaceStep.CheckpointRadius"/> of the current target), at
    /// <see cref="SpeedFactor"/> × the road limit. Driverless (no ped needed). After the last checkpoint it brakes and
    /// <see cref="Finished"/> becomes true.
    /// </summary>
    public sealed class RivalDriver : IDriver
    {
        /// <summary>Creates a rival over a checkpoint list (shared, not copied).</summary>
        /// <exception cref="ArgumentNullException">checkpoints is null.</exception>
        public RivalDriver(Vec3[] checkpoints, float speedFactor)
        {
            if (checkpoints == null) throw new ArgumentNullException("checkpoints");
            Checkpoints = checkpoints;
            SpeedFactor = speedFactor;
            Route = new Route();
            Follower = new RouteFollower();
        }

        /// <summary>Default rival speed factor (1.1 × the limit).</summary>
        public const float DefaultSpeedFactor = 1.1f;

        /// <summary>Checkpoints in race order.</summary>
        public Vec3[] Checkpoints { get; private set; }

        /// <summary>Speed factor over the limit.</summary>
        public float SpeedFactor { get; private set; }

        /// <summary>Index of the checkpoint being driven to (= Checkpoints.Length when finished).</summary>
        public int NextCheckpoint { get; private set; }

        /// <summary>True after the last checkpoint.</summary>
        public bool Finished { get { return NextCheckpoint >= Checkpoints.Length; } }

        /// <summary>Game seconds at which it finished (−1 before).</summary>
        public double FinishTime { get; private set; }

        /// <summary>The current leg.</summary>
        public Route Route { get; private set; }

        /// <summary>The follower.</summary>
        public RouteFollower Follower { get; private set; }

        /// <inheritdoc />
        public string Name { get { return "Race"; } }

        /// <inheritdoc />
        public bool Driverless { get { return true; } }

        /// <inheritdoc />
        public VehicleControls Drive(GameWorld world, Car car, float dt)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// A street race. Begin shows the first checkpoint (Checkpoint blip + marker of <see cref="CheckpointRadius"/>, the
    /// following one as a second, non-GPS Checkpoint blip). The player passes a checkpoint by driving within the radius
    /// (XZ) of it in order. Rank: by checkpoints passed, then by XZ distance to the next checkpoint, among the player and
    /// the cars in <see cref="RivalSlots"/> (their <see cref="RivalDriver.NextCheckpoint"/>); written to
    /// <c>Mission.StatusLine</c> as <see cref="CampaignText.RacePosition"/>(rank, racers) (cached strings).
    /// Done when the player passes the last checkpoint; <see cref="FinishPosition"/> = the player's rank then (rivals
    /// that finished earlier rank ahead). Fails with <see cref="FailReasons.LeftJobVehicle"/> after 15 s out of a car.
    /// </summary>
    public sealed class RaceStep : MissionStep
    {
        /// <summary>Checkpoint radius, metres.</summary>
        public const float CheckpointRadius = 10f;

        /// <summary>Checkpoints in order (6-12).</summary>
        public readonly Vec3[] Checkpoints;
        /// <summary>Car slots of the rivals.</summary>
        public readonly int[] RivalSlots;
        /// <summary>Checkpoints the player has passed (output).</summary>
        public int Passed;
        /// <summary>The player's final position, 1-based (output, valid after Done).</summary>
        public int FinishPosition;

        /// <summary>Creates the step.</summary>
        /// <exception cref="ArgumentNullException">An array is null.</exception>
        public RaceStep(Vec3[] checkpoints, int[] rivalSlots)
        {
            if (checkpoints == null) throw new ArgumentNullException("checkpoints");
            if (rivalSlots == null) throw new ArgumentNullException("rivalSlots");
            Checkpoints = checkpoints;
            RivalSlots = rivalSlots;
            Gps = true;
        }

        /// <inheritdoc />
        public override StepKind Kind { get { return StepKind.Race; } }

        /// <inheritdoc />
        public override StepState Step(MissionContext ctx, float dt) { throw new NotImplementedException(); }
    }
}
