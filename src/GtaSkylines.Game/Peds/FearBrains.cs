using System;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Peds
{
    /// <summary>
    /// Runs away from a threat at Traits.FleeSpeed (5-6 m/s) for a seeded 6-15 s with Anim Panic and flag Panicking.
    /// The threat position follows <see cref="ThreatTag"/> while it resolves (<c>TryGetTagPosition</c>), else stays at the
    /// last known point. Outdoors: <see cref="PedSteering.Flee"/>; indoors: <see cref="PedPath"/> toward the interior's
    /// entrance spawn if it is farther from the threat than the ped is, else toward the NpcSpot farthest from the threat.
    /// When the time is up → <see cref="ReturnBrain"/> (Panicking cleared). Active.
    /// Reactions: a new threat stimulus (anything but Bumped) re-targets to the new source and restarts the timer with a
    /// fresh draw; VehicleThreat → <see cref="SidestepBrain"/>; Bumped ignored.
    /// </summary>
    public sealed class FleeBrain : PedBrain
    {
        /// <summary>Shortest flee, seconds (6).</summary>
        public const float MinSeconds = 6f;
        /// <summary>Longest flee, seconds (15).</summary>
        public const float MaxSeconds = 15f;

        /// <summary>Creates the brain; the duration is drawn from <c>world.Rng</c>.</summary>
        public FleeBrain(GameWorld world, PedTraits traits, int threatTag, Vec3 threatPosition)
            : base(traits)
        {
            ThreatTag = threatTag;
            ThreatPosition = threatPosition;
        }

        /// <summary>What it flees from (WorldTag), 0 for a fixed point.</summary>
        public int ThreatTag { get; private set; }

        /// <summary>The latest threat position.</summary>
        public Vec3 ThreatPosition { get; private set; }

        /// <summary>Flee duration drawn at construction (or at the last re-target), seconds.</summary>
        public float Duration { get; private set; }

        /// <inheritdoc />
        public override string Name { get { return "Flee"; } }

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
    /// Dodges a car coming at the ped: for <see cref="DodgeSeconds"/> moves at <see cref="DodgeSpeed"/> perpendicular to
    /// the car's velocity, to the side of the car's path the ped already stands on (the sign of the cross product of the
    /// car's velocity and the car-to-ped vector; exactly on the path → the car's right), Anim Sprint, then
    /// <see cref="FleeBrain"/> from the car. Active. Stimuli other than VehicleThreat are deferred to the FleeBrain that
    /// follows (ignored while dodging).
    /// </summary>
    public sealed class SidestepBrain : PedBrain
    {
        /// <summary>Dodge duration, seconds (0.8).</summary>
        public const float DodgeSeconds = 0.8f;
        /// <summary>Dodge speed, m/s (5.5).</summary>
        public const float DodgeSpeed = 5.5f;

        /// <summary>Creates the brain dodging <paramref name="carTag"/>.</summary>
        public SidestepBrain(PedTraits traits, int carTag)
            : base(traits)
        {
            CarTag = carTag;
        }

        /// <summary>The car being dodged.</summary>
        public int CarTag { get; private set; }

        /// <inheritdoc />
        public override string Name { get { return "Sidestep"; } }

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
    /// Crouches in place (Anim Sit — the closest animation G6 offers; flag Panicking) facing away from the threat for a
    /// seeded 6-12 s, then <see cref="FleeBrain"/> from the threat. Active. Reactions: Punched, Hurt or AimedAt within
    /// <see cref="HandsUpBrain.Range"/> → the default reaction; other stimuli ignored.
    /// </summary>
    public sealed class CowerBrain : PedBrain
    {
        /// <summary>Shortest cower, seconds (6).</summary>
        public const float MinSeconds = 6f;
        /// <summary>Longest cower, seconds (12).</summary>
        public const float MaxSeconds = 12f;
        /// <summary>An adult this close (m) to static geometry cowers instead of fleeing (1.2).</summary>
        public const float WallDistance = 1.2f;

        /// <summary>Creates the brain; the duration is drawn from <c>world.Rng</c>.</summary>
        public CowerBrain(GameWorld world, PedTraits traits, int threatTag, Vec3 threatPosition)
            : base(traits)
        {
            ThreatTag = threatTag;
            ThreatPosition = threatPosition;
        }

        /// <summary>The threat.</summary>
        public int ThreatTag { get; private set; }

        /// <summary>The threat's position when cowering began.</summary>
        public Vec3 ThreatPosition { get; private set; }

        /// <summary>Duration drawn at construction, seconds.</summary>
        public float Duration { get; private set; }

        /// <inheritdoc />
        public override string Name { get { return "Cower"; } }

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
    /// Hands up while aimed at: flag HandsUp, Anim HandsUp, facing the aimer, no movement. On the first step emits
    /// <see cref="GameEventType.HandsUp"/> (A = aimer, B = ped); a clerk (Traits.Clerk) also emits
    /// <see cref="GameEventType.Robbery"/> once (A = aimer, B = ped, Value = <c>Interior.Building.Id</c>, 0 outside) — G9
    /// opens the till. Each step the aim counts as held while <c>PlayerState.AimTargetTag</c> = this ped's tag and the
    /// aimer has the Aiming flag (for a non-player aimer: Aiming flag and within <see cref="Range"/>); after
    /// <see cref="AimLostGraceSeconds"/> without it → <see cref="FleeBrain"/> from the aimer (HandsUp cleared). Active.
    /// Reactions: AimedAt refreshes; Punched, Hurt, ShotAt → FleeBrain; others ignored.
    /// </summary>
    public sealed class HandsUpBrain : PedBrain
    {
        /// <summary>Aim distance (m) within which a ped raises its hands (15); farther (to 25 m) it flees.</summary>
        public const float Range = 15f;
        /// <summary>Seconds the aim may lapse before the ped flees (0.5).</summary>
        public const float AimLostGraceSeconds = 0.5f;

        /// <summary>Creates the brain for the aimer <paramref name="aimerTag"/>.</summary>
        public HandsUpBrain(PedTraits traits, int aimerTag)
            : base(traits)
        {
            AimerTag = aimerTag;
        }

        /// <summary>Who aims.</summary>
        public int AimerTag { get; private set; }

        /// <summary>True once the Robbery event was emitted.</summary>
        public bool Robbed { get; private set; }

        /// <inheritdoc />
        public override string Name { get { return "HandsUp"; } }

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
    /// Lying on the ground after a hit (assigned by <c>Combat.Damage</c>): flag KnockedDown, Anim LieDown, no movement, for
    /// a seeded 2-4 s, or <see cref="InjuredSeconds"/> when the ped is Injured (checked at construction). Then KnockedDown
    /// cleared and <see cref="FleeBrain"/> from <c>ped.LastAttackerTag</c>. Ignores every stimulus. A dead ped stays down
    /// (the Peds system no longer steps it). Active.
    /// </summary>
    public sealed class KnockedDownBrain : PedBrain
    {
        /// <summary>Shortest time down, seconds (2).</summary>
        public const float MinSeconds = 2f;
        /// <summary>Longest time down, seconds (4).</summary>
        public const float MaxSeconds = 4f;
        /// <summary>Time down when injured, seconds (6).</summary>
        public const float InjuredSeconds = 6f;

        /// <summary>Creates the brain; the duration is drawn from <c>world.Rng</c> (always drawn, even when injured, to keep the sequence stable).</summary>
        public KnockedDownBrain(GameWorld world, PedTraits traits, Ped ped)
            : base(traits)
        {
        }

        /// <summary>Time down, seconds.</summary>
        public float Duration { get; private set; }

        /// <inheritdoc />
        public override string Name { get { return "KnockedDown"; } }

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
        }
    }
}
