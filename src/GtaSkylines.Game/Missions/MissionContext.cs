using System;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Missions
{
    /// <summary>What a step reports each <see cref="MissionStep.Step"/>.</summary>
    public enum StepState
    {
        /// <summary>Keep stepping.</summary>
        Running,
        /// <summary>Completed: the runner ends it and begins the next step (in the same fixed step).</summary>
        Done,
        /// <summary>Failed: the mission (or job round) fails with <see cref="MissionContext.FailReason"/>.</summary>
        Failed,
    }

    /// <summary>
    /// Runtime state shared by the steps of the running mission or side-job round. Owned and reused by
    /// <see cref="MissionRunner"/>: created once with fixed capacities, reset per run, never reallocated.
    /// </summary>
    public sealed class MissionContext
    {
        /// <summary>Actor slots per run.</summary>
        public const int MaxActors = 16;
        /// <summary>Events copied per fixed step; later ones in the same step are dropped (and counted in <see cref="DroppedEvents"/>).</summary>
        public const int EventCapacity = 64;

        /// <summary>Creates an empty context.</summary>
        public MissionContext()
        {
            ActorIds = new int[MaxActors];
            Events = new GameEvent[EventCapacity];
        }

        /// <summary>The world (set by the runner before every step).</summary>
        public GameWorld World;
        /// <summary>The running definition (null for a side-job round).</summary>
        public MissionDefinition Definition;
        /// <summary>Game ids of spawned actors by slot (0 = not spawned or removed).</summary>
        public readonly int[] ActorIds;
        /// <summary>Index of the current step.</summary>
        public int StepIndex;
        /// <summary>Game seconds since the current step began.</summary>
        public float StepSeconds;
        /// <summary>Game seconds since the run began.</summary>
        public float RunSeconds;
        /// <summary>This fixed step's events (from the runner's single cursor), valid indices [0, <see cref="EventCount"/>).</summary>
        public readonly GameEvent[] Events;
        /// <summary>Number of valid <see cref="Events"/>.</summary>
        public int EventCount;
        /// <summary>Events dropped because a step produced more than <see cref="EventCapacity"/> (debug).</summary>
        public long DroppedEvents;
        /// <summary>Reason set by <see cref="Fail"/>.</summary>
        public string FailReason;
        /// <summary>Id the runner hands out to blips and markers of this run (blip ids 1000-1999 belong to missions).</summary>
        public int NextBlipId = 1000;

        /// <summary>Sets <see cref="FailReason"/> and returns <see cref="StepState.Failed"/>.</summary>
        public StepState Fail(string reason)
        {
            FailReason = reason;
            return StepState.Failed;
        }

        /// <summary>The live ped in a slot, or null (unspawned, removed, out of range).</summary>
        public Ped ActorPed(int slot)
        {
            throw new NotImplementedException();
        }

        /// <summary>The live car in a slot, or null.</summary>
        public Car ActorCar(int slot)
        {
            throw new NotImplementedException();
        }

        /// <summary>The car the player is sitting in (any seat), or null.</summary>
        public Car PlayerCar
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>True when the player is in seat 0 of <paramref name="car"/>.</summary>
        public bool PlayerDrives(Car car)
        {
            throw new NotImplementedException();
        }

        /// <summary>True when any of this step's events has the type and (when not 0) the given actors.</summary>
        public bool HasEvent(GameEventType type, int actorA, int actorB)
        {
            throw new NotImplementedException();
        }

        /// <summary>Clears per-run state (slots, step counters, events, fail reason) for a new run.</summary>
        public void Reset(GameWorld world, MissionDefinition definition)
        {
            throw new NotImplementedException();
        }
    }
}
