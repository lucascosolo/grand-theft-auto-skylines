using System;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Peds
{
    /// <summary>
    /// Interior occupants and calm promoted peds: stand still (Anim Idle) and turn by a seeded ±30° every 3-8 s (turning at
    /// <see cref="PedSteering.TurnRateDegPerSecond"/>); a Seated occupant sits (Anim Sit) at its home spot facing
    /// HomeYawDeg and never turns. An occupant pushed more than 1 m from home walks back (<see cref="PedPath"/>, walk
    /// speed). Not active (G6 may demote it). Default reactions.
    /// </summary>
    public sealed class IdleBrain : PedBrain
    {
        /// <summary>Shortest seconds between idle turns (3).</summary>
        public const float MinTurnSeconds = 3f;
        /// <summary>Longest seconds between idle turns (8).</summary>
        public const float MaxTurnSeconds = 8f;
        /// <summary>Largest idle turn, degrees (30).</summary>
        public const float MaxTurnDeg = 30f;

        /// <summary>Creates the brain; the first turn time is drawn from <c>world.Rng</c>.</summary>
        public IdleBrain(GameWorld world, PedTraits traits)
            : base(traits)
        {
        }

        /// <inheritdoc />
        public override string Name { get { return "Idle"; } }

        /// <inheritdoc />
        public override bool IsActive { get { return false; } }

        /// <inheritdoc />
        public override void Step(GameWorld world, Ped ped, float dt)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Calm walk-off after an incident: walks (<see cref="PedSteering.WalkSpeed"/>, Anim Walk) directly away from the
    /// player with avoidance. Indoors, an occupant walks back to its home spot instead and then becomes an
    /// <see cref="IdleBrain"/>. Not active, so G6 demotes the ped once out of the player's sight and far enough. Default
    /// reactions.
    /// </summary>
    public sealed class ReturnBrain : PedBrain
    {
        /// <summary>Creates the brain.</summary>
        public ReturnBrain(PedTraits traits)
            : base(traits)
        {
        }

        /// <inheritdoc />
        public override string Name { get { return "Return"; } }

        /// <inheritdoc />
        public override bool IsActive { get { return false; } }

        /// <inheritdoc />
        public override void Step(GameWorld world, Ped ped, float dt)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Bumped while walking: staggers for <see cref="StaggerSeconds"/> (pushed away from the bumper at
    /// <see cref="StaggerSpeed"/>, Anim Walk), and on the first step queues a complaint subtitle: the line from
    /// <c>Hooks.Dialogue.Line(world, DialogueContext.Bumped, ped.Citizen, ped)</c> (skipped when the hook is null or
    /// returns null) via <c>Messages.QueueSubtitle(null, line, 2.5)</c>, only when the player is within
    /// <see cref="SubtitleRange"/>. Then <see cref="ReturnBrain"/>. Active. Further bumps are ignored; other stimuli
    /// default.
    /// </summary>
    public sealed class StumbleBrain : PedBrain
    {
        /// <summary>Stagger duration, seconds (0.6).</summary>
        public const float StaggerSeconds = 0.6f;
        /// <summary>Stagger push speed, m/s (1.2).</summary>
        public const float StaggerSpeed = 1.2f;
        /// <summary>Complaint subtitle shown only when the player is this close, metres (15).</summary>
        public const float SubtitleRange = 15f;
        /// <summary>Subtitle duration, seconds (2.5).</summary>
        public const float SubtitleSeconds = 2.5f;

        /// <summary>Creates the brain staggering away from <paramref name="bumperTag"/> (its position is read on the first step).</summary>
        public StumbleBrain(PedTraits traits, int bumperTag)
            : base(traits)
        {
            BumperTag = bumperTag;
        }

        /// <summary>Who bumped.</summary>
        public int BumperTag { get; private set; }

        /// <inheritdoc />
        public override string Name { get { return "Stumble"; } }

        /// <inheritdoc />
        public override bool IsActive { get { return true; } }

        /// <inheritdoc />
        public override void Step(GameWorld world, Ped ped, float dt)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public override void React(GameWorld world, Ped ped, Stimulus stimulus)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// The driver of a CS1 vehicle the player crashed into (<see cref="PedOrigin.ShockedDriver"/>), seated in seat 0:
    /// sits stunned (Anim Sit) for a seeded 2-4 s (active), then with Traits.Fighter or a seeded
    /// <see cref="PedTraits.CarjackedFightChance"/> roll gets out at the driver door (<c>TakeOutOfCar(ped,
    /// DoorPosition(car, left), car yaw − 90)</c>) and becomes a <see cref="FightBrain"/> against the player; otherwise it
    /// stays seated and becomes inactive (G6 demotes it with the usual rules). Any stimulus while seated is ignored except
    /// that the ped gets out and flees (<see cref="FleeBrain"/>) on GunshotHeard, ShotAt, AimedAt or Hurt.
    /// </summary>
    public sealed class ShockedDriverBrain : PedBrain
    {
        /// <summary>Creates the brain; the stun time and fight roll are drawn from <c>world.Rng</c>.</summary>
        public ShockedDriverBrain(GameWorld world, PedTraits traits)
            : base(traits)
        {
        }

        /// <inheritdoc />
        public override string Name { get { return "ShockedDriver"; } }

        /// <inheritdoc />
        public override bool IsActive { get { throw new NotImplementedException(); } }

        /// <inheritdoc />
        public override void Step(GameWorld world, Ped ped, float dt)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public override void React(GameWorld world, Ped ped, Stimulus stimulus)
        {
            throw new NotImplementedException();
        }
    }
}
