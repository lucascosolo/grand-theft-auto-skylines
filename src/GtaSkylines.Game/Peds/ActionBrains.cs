using GtaSkylines.Game.Combat;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Peds
{
    /// <summary>
    /// A brain that handles its own knock-down (G8's cop brain, <see cref="HostileBrain"/>); without it
    /// <c>Combat.Damage</c> replaces the brain with a <see cref="KnockedDownBrain"/>.
    /// </summary>
    public interface IKnockDownHandler
    {
        /// <summary>The ped was just knocked down (flag KnockedDown already set). The brain must clear the flag when the ped gets up.</summary>
        void OnKnockedDown(GameWorld world, Ped ped, int causeTag);
    }

    /// <summary>
    /// Answers with fists: flag Hostile (cleared on exit only if this brain set it), approaches the target
    /// (<see cref="PedSteering.Seek"/> at <see cref="ApproachSpeed"/>, Anim Run) to within <see cref="StrikeDistance"/>,
    /// faces it and swings fists through its own <see cref="WeaponUser"/> (<see cref="Weapons.Melee"/>, rate 2.2/s).
    /// Stops: health &lt; <see cref="GiveUpHealth"/> or the target holds a firearm (<c>CurrentWeapon</c> ≥ Pistol) →
    /// <see cref="FleeBrain"/>; the target dead, gone, in a vehicle or farther than <see cref="GiveUpDistance"/> →
    /// <see cref="ReturnBrain"/>. Active. Reactions: Punched/Hurt ignored (the stop rules decide); GunshotHeard, ShotAt
    /// or AimedAt → FleeBrain; others ignored.
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

        private readonly WeaponUser _fists = new WeaponUser(null);
        private bool _armed;
        private bool _setHostile;

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
            Seconds += dt;
            Ped target = world.PedFromTag(TargetTag, false, 0);
            if (ped.Health < GiveUpHealth || (target != null && HoldsFirearm(target)))
            {
                End(ped, new FleeBrain(world, Traits, TargetTag, PositionOf(world, TargetTag, ped.Position)));
                return;
            }
            if (target == null || target.IsDead || target.Has(PedFlags.InVehicle)
                || Vec3.DistanceXZ(target.Position, ped.Position) > GiveUpDistance)
            {
                End(ped, new ReturnBrain(Traits));
                return;
            }
            if (!ped.Has(PedFlags.Hostile))
            {
                ped.Flags |= PedFlags.Hostile;
                _setHostile = true;
            }
            if (!_armed)
            {
                _armed = true;
                _fists.Load(ped, WeaponId.Fists);
            }
            _fists.Step(ped, dt);
            Vec3 to = (target.Position - ped.Position).Flat;
            if (to.Length > StrikeDistance)
            {
                PedSteering.Seek(world, ped, target.Position, ApproachSpeed, PedAnim.Run, dt);
                return;
            }
            PedSteering.Stop(ped, PedAnim.Idle);
            if (to.LengthSquared > 1e-6f) ped.YawDeg = Vec3.YawDegOf(to);
            ShotResult shot;
            MeleeResult melee;
            _fists.TryAttack(world, ped, ped.Position, to, 0f, out shot, out melee);
        }

        /// <inheritdoc />
        public override void React(GameWorld world, Ped ped, Stimulus stimulus)
        {
            if (stimulus.Kind == StimulusKind.GunshotHeard || stimulus.Kind == StimulusKind.ShotAt || stimulus.Kind == StimulusKind.AimedAt)
                End(ped, new FleeBrain(world, Traits, stimulus.SourceTag, stimulus.SourcePosition));
        }

        private void End(Ped ped, IPedBrain next)
        {
            if (_setHostile) ped.Flags &= ~PedFlags.Hostile;
            _setHostile = false;
            Assign(ped, next);
        }
    }

    /// <summary>
    /// A witness phoning the police (requested by G8). On its first step: stops, flag OnPhone, Anim Phone, emits
    /// <see cref="GameEventType.CallStarted"/> (A = ped tag, Value = crime id), and queues the
    /// <c>DialogueContext.WitnessCalling</c> line as a subtitle when the player is within
    /// <see cref="StumbleBrain.SubtitleRange"/>. It faces the perpetrator while on the phone. After <see cref="Duration"/>
    /// on the phone (counted from the first step) emits <see cref="GameEventType.CallCompleted"/> (A = ped tag, Value =
    /// crime id), clears OnPhone and becomes a <see cref="ReturnBrain"/>. Active while the call runs.
    /// <para>Interruption (<see cref="Interrupt"/>, at most once, never after completion): emits
    /// <see cref="GameEventType.CallInterrupted"/> (A = ped tag, Value = crime id), clears OnPhone and flees from the threat
    /// (unless dead or knocked down, then the brain is just left). Threat stimuli: AimedAt (any distance), Punched, Hurt,
    /// ShotAt, VehicleThreat, and GunshotHeard / ExplosionHeard within <see cref="ThreatRadius"/> of the ped.
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

        private bool _started;

        /// <summary>Creates the call for <paramref name="crimeId"/> about <paramref name="perpetratorTag"/>; the duration (4-10 s, uniform) is drawn from <c>world.Rng</c>.</summary>
        public CallPoliceBrain(GameWorld world, PedTraits traits, int crimeId, int perpetratorTag)
            : this(traits, crimeId, perpetratorTag, world.Rng.Range(MinSeconds, MaxSeconds))
        {
        }

        /// <summary>Creates the call with an explicit length (G8's <c>ICrimePedServices.CreateCallPolice</c>), in seconds.</summary>
        public CallPoliceBrain(PedTraits traits, int crimeId, int perpetratorTag, float callSeconds)
            : base(traits)
        {
            CrimeId = crimeId;
            PerpetratorTag = perpetratorTag;
            Duration = callSeconds;
        }

        /// <summary>G8's crime id (the Value of the call events).</summary>
        public int CrimeId { get; private set; }

        /// <summary>Who the call is about.</summary>
        public int PerpetratorTag { get; private set; }

        /// <summary>Call length, seconds.</summary>
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
            if (Completed || Interrupted) return;
            if (!_started)
            {
                _started = true;
                world.Emit(GameEventType.CallStarted, ped.Position, ped.Tag, 0, CrimeId, null);
                StumbleBrain.Say(world, ped, DialogueContext.WitnessCalling);
            }
            else Seconds += dt;
            if (Seconds >= Duration - 1e-4f)
            {
                Completed = true;
                ped.Flags &= ~PedFlags.OnPhone;
                world.Emit(GameEventType.CallCompleted, ped.Position, ped.Tag, 0, CrimeId, null);
                Assign(ped, new ReturnBrain(Traits));
                return;
            }
            ped.Flags |= PedFlags.OnPhone;
            PedSteering.Stop(ped, PedAnim.Phone);
            Vec3 perp;
            if (PerpetratorTag != 0 && world.TryGetTagPosition(PerpetratorTag, out perp))
            {
                Vec3 to = (perp - ped.Position).Flat;
                if (to.LengthSquared > 1e-6f) PedSteering.TurnToward(ped, Vec3.YawDegOf(to), dt);
            }
        }

        /// <inheritdoc />
        public override void React(GameWorld world, Ped ped, Stimulus stimulus)
        {
            if (Completed || Interrupted) return;
            switch (stimulus.Kind)
            {
                case StimulusKind.AimedAt:
                case StimulusKind.Punched:
                case StimulusKind.Hurt:
                case StimulusKind.ShotAt:
                case StimulusKind.VehicleThreat:
                    InterruptAt(world, ped, stimulus.SourceTag, stimulus.SourcePosition);
                    break;
                case StimulusKind.GunshotHeard:
                case StimulusKind.ExplosionHeard:
                    if (Vec3.Distance(stimulus.SourcePosition, ped.Position) <= ThreatRadius)
                        InterruptAt(world, ped, stimulus.SourceTag, stimulus.SourcePosition);
                    break;
            }
        }

        /// <summary>Interrupts the call (see the class); <paramref name="threatTag"/> is what the caller flees from. No-op when already completed or interrupted.</summary>
        public void Interrupt(GameWorld world, Ped ped, int threatTag)
        {
            InterruptAt(world, ped, threatTag, PositionOf(world, threatTag, ped.Position - Vec3.FromYawDeg(ped.YawDeg)));
        }

        private void InterruptAt(GameWorld world, Ped ped, int threatTag, Vec3 threat)
        {
            if (Completed || Interrupted) return;
            Interrupted = true;
            ped.Flags &= ~PedFlags.OnPhone;
            world.Emit(GameEventType.CallInterrupted, ped.Position, ped.Tag, 0, CrimeId, null);
            if (ped.IsDead || ped.Has(PedFlags.KnockedDown)) return;
            Assign(ped, new FleeBrain(world, Traits, threatTag, threat));
        }

        /// <summary>
        /// G8's entry point: makes <paramref name="witness"/> call. An ambient candidate is promoted first
        /// (<c>PromoteCitizen(Ambient, PedOrigin.PromotedCitizen, perpetratorTag)</c>). The ped's traits are kept when its
        /// brain is a <see cref="PedBrain"/>, else rolled. An existing CallPoliceBrain that is still active is kept (returns
        /// that ped unchanged). Returns the calling ped, or null when the candidate's ped is dead or gone.
        /// </summary>
        public static Ped Assign(GameWorld world, WitnessCandidate witness, int crimeId, int perpetratorTag)
        {
            Ped ped = witness.Ped;
            if (ped == null && WorldTag.KindOf(witness.Tag) == TagKind.AmbientCitizen)
                ped = world.PromoteCitizen(witness.Ambient, PedOrigin.PromotedCitizen, perpetratorTag);
            if (ped == null || ped.IsDead) return null;
            var current = ped.Brain as CallPoliceBrain;
            if (current != null && current.IsActive) return ped;
            Assign(ped, new CallPoliceBrain(world, PedBrainFactory.TraitsOf(world, ped), crimeId, perpetratorTag));
            return ped;
        }
    }
}
