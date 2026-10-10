using System;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Peds
{
    /// <summary>
    /// A brain that is not a <see cref="PedBrain"/> (G8's cop brain, G9's actors) implements this to handle its own
    /// knock-down; without it <c>Combat.Damage</c> replaces the brain with a <see cref="KnockedDownBrain"/>.
    /// </summary>
    public interface IKnockDownHandler
    {
        /// <summary>The ped was just knocked down (flag KnockedDown already set). The brain must clear the flag when the ped gets up.</summary>
        void OnKnockedDown(GameWorld world, Ped ped, int causeTag);
    }

    /// <summary>
    /// Answers with fists: flag Hostile (cleared on exit only if this brain set it), approaches the target
    /// (<see cref="PedSteering.Seek"/> at <see cref="ApproachSpeed"/>, Anim Run) to within <see cref="StrikeDistance"/>,
    /// faces it and swings fists through its own <c>WeaponUser</c> (<c>Combat.Weapons.Melee</c>, rate 2.2/s).
    /// Stops: health &lt; <see cref="GiveUpHealth"/> or the target holds a firearm (<c>CurrentWeapon</c> ≥ Pistol) →
    /// <see cref="FleeBrain"/>; the target dead, gone, in a vehicle or farther than <see cref="GiveUpDistance"/> →
    /// <see cref="ReturnBrain"/>. Active. Reactions: Punched/Hurt by the target ignored (the stop rules decide); a
    /// gunshot, ShotAt or AimedAt → FleeBrain; others ignored.
    /// </summary>
    public sealed class FightBrain : PedBrain
    {
        /// <summary>Below this health (40) the ped stops fighting and flees.</summary>
        public const float GiveUpHealth = 40f;
        /// <summary>Approach speed, m/s (3.6).</summary>
        public const float ApproachSpeed = 3.6f;
        /// <summary>Swing when this close (feet XZ), metres (1.0, inside the fists' 1.2 m reach).</summary>
        public const float StrikeDistance = 1f;
        /// <summary>The fight ends when the target gets this far, metres (20).</summary>
        public const float GiveUpDistance = 20f;

        /// <summary>Creates the brain against <paramref name="targetTag"/> (a Game ped tag).</summary>
        public FightBrain(PedTraits traits, int targetTag)
            : base(traits)
        {
            TargetTag = targetTag;
        }

        /// <summary>Who it fights.</summary>
        public int TargetTag { get; private set; }

        /// <inheritdoc />
        public override string Name { get { return "Fight"; } }

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
    /// A witness phoning the police (requested by G8). On its first step: stops, flag OnPhone, Anim Phone, emits
    /// <see cref="GameEventType.CallStarted"/> (A = ped tag, Value = crime id), and queues the
    /// <c>DialogueContext.WitnessCalling</c> line as a subtitle when the player is within
    /// <see cref="StumbleBrain.SubtitleRange"/>. It faces the perpetrator while on the phone. After <see cref="Duration"/>
    /// (seeded 4-10 s, uniform, drawn from <c>world.Rng</c> at construction) emits
    /// <see cref="GameEventType.CallCompleted"/> (A = ped tag, Value = crime id), clears OnPhone and becomes a
    /// <see cref="ReturnBrain"/>. Active.
    /// <para>Interruption (<see cref="Interrupt"/>, at most once, never after completion): emits
    /// <see cref="GameEventType.CallInterrupted"/> (A = ped tag, Value = crime id), clears OnPhone and flees from the threat
    /// (unless dead or knocked down, then the brain is just left). Threat stimuli: AimedAt (any distance), Punched, Hurt,
    /// ShotAt (a shot within 20 m), VehicleThreat, and GunshotHeard / ExplosionHeard within <see cref="ThreatRadius"/>.
    /// <c>Combat.Damage</c> interrupts on knock-down and death; G8 calls Interrupt when the caller loses the player and is
    /// &gt; 60 m away.</para>
    /// </summary>
    public sealed class CallPoliceBrain : PedBrain
    {
        /// <summary>Shortest call, seconds (4).</summary>
        public const float MinSeconds = 4f;
        /// <summary>Longest call, seconds (10).</summary>
        public const float MaxSeconds = 10f;
        /// <summary>Noise this close (m) interrupts the call (20).</summary>
        public const float ThreatRadius = 20f;

        /// <summary>Creates the call for <paramref name="crimeId"/> about <paramref name="perpetratorTag"/>; the duration is drawn from <c>world.Rng</c>.</summary>
        public CallPoliceBrain(GameWorld world, PedTraits traits, int crimeId, int perpetratorTag)
            : base(traits)
        {
            CrimeId = crimeId;
            PerpetratorTag = perpetratorTag;
        }

        /// <summary>G8's crime id (the Value of the call events).</summary>
        public int CrimeId { get; private set; }

        /// <summary>Who the call is about.</summary>
        public int PerpetratorTag { get; private set; }

        /// <summary>Call length, seconds (4-10).</summary>
        public float Duration { get; private set; }

        /// <summary>True once CallCompleted was emitted.</summary>
        public bool Completed { get; private set; }

        /// <summary>True once CallInterrupted was emitted.</summary>
        public bool Interrupted { get; private set; }

        /// <inheritdoc />
        public override string Name { get { return "CallPolice"; } }

        /// <inheritdoc />
        public override bool IsActive { get { return !Completed && !Interrupted; } }

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

        /// <summary>Interrupts the call (see the class); <paramref name="threatTag"/> is what the caller flees from. No-op when already completed or interrupted.</summary>
        public void Interrupt(GameWorld world, Ped ped, int threatTag)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// G8's entry point: makes <paramref name="witness"/> call. An ambient candidate is promoted first
        /// (<c>PromoteCitizen(Ambient, PedOrigin.PromotedCitizen, perpetratorTag)</c>). The ped's traits are kept when its
        /// brain is a <see cref="PedBrain"/>, else rolled. An existing CallPoliceBrain that is still active is kept (returns
        /// that ped unchanged). Returns the calling ped, or null when the candidate's ped is dead or gone.
        /// </summary>
        public static Ped Assign(GameWorld world, WitnessCandidate witness, int crimeId, int perpetratorTag)
        {
            throw new NotImplementedException();
        }
    }
}
