using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Peds
{
    /// <summary>
    /// Interior occupants and calm promoted peds: stand still (Anim Idle) and turn by a seeded ±30° (around the facing
    /// they had on their first step) every 3-8 s, turning at <see cref="PedSteering.TurnRateDegPerSecond"/>; a Seated
    /// occupant sits (Anim Sit) facing HomeYawDeg and never turns. An occupant more than 1 m from home walks back
    /// (<see cref="PedPath"/>, walk speed). Not active (G6 may demote it). Default reactions. The turn timer is drawn
    /// from <c>world.Rng</c> on the first step (the constructor accepts a null world).
    /// </summary>
    public sealed class IdleBrain : PedBrain
    {
        /// <summary>Shortest seconds between idle turns (3).</summary>
        public const float MinTurnSeconds = 3f;
        /// <summary>Longest seconds between idle turns (8).</summary>
        public const float MaxTurnSeconds = 8f;
        /// <summary>Largest idle turn, degrees (30).</summary>
        public const float MaxTurnDeg = 30f;
        /// <summary>An occupant farther than this from home (m) walks back (1).</summary>
        public const float HomeTolerance = 1f;

        private bool _init;
        private float _baseYaw, _targetYaw, _nextTurn;
        private PedPath _path;

        /// <summary>Creates the brain.</summary>
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
            Seconds += dt;
            if (Traits.HasHome && Vec3.DistanceXZ(ped.Position, Traits.Home) > HomeTolerance)
            {
                if (_path == null) _path = new PedPath();
                _path.Follow(world, ped, Traits.Home, PedSteering.WalkSpeed, PedAnim.Walk, dt);
                return;
            }
            if (Traits.HasHome && Traits.Seated)
            {
                ped.YawDeg = Traits.HomeYawDeg;
                PedSteering.Stop(ped, PedAnim.Sit);
                return;
            }
            if (!_init)
            {
                _init = true;
                _baseYaw = ped.YawDeg;
                _targetYaw = _baseYaw;
                _nextTurn = Seconds + world.Rng.Range(MinTurnSeconds, MaxTurnSeconds);
            }
            if (Seconds >= _nextTurn)
            {
                _targetYaw = _baseYaw + world.Rng.Range(-MaxTurnDeg, MaxTurnDeg);
                _nextTurn = Seconds + world.Rng.Range(MinTurnSeconds, MaxTurnSeconds);
            }
            PedSteering.Stop(ped, PedAnim.Idle);
            PedSteering.TurnToward(ped, _targetYaw, dt);
        }
    }

    /// <summary>
    /// Calm walk-off after an incident: walks (<see cref="PedSteering.WalkSpeed"/>, Anim Walk) directly away from the
    /// player with avoidance. Indoors (an occupant with a home and <c>InteriorId</c> ≥ 0), it walks back to its home spot
    /// instead and then becomes an <see cref="IdleBrain"/>. Not active, so G6 demotes the ped once out of the player's
    /// sight and far enough. Default reactions.
    /// </summary>
    public sealed class ReturnBrain : PedBrain
    {
        private PedPath _path;

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
            Seconds += dt;
            if (Traits.HasHome && ped.InteriorId >= 0)
            {
                if (_path == null) _path = new PedPath();
                if (_path.Follow(world, ped, Traits.Home, PedSteering.WalkSpeed, PedAnim.Walk, dt)) Assign(ped, new IdleBrain(world, Traits));
                return;
            }
            Ped player = world.Player;
            Vec3 from = player != null ? player.Position : ped.Position - Vec3.FromYawDeg(ped.YawDeg);
            PedSteering.Flee(world, ped, from, PedSteering.WalkSpeed, PedAnim.Walk, dt);
        }
    }

    /// <summary>
    /// Bumped while walking: staggers for <see cref="StaggerSeconds"/> (pushed away from the bumper at
    /// <see cref="StaggerSpeed"/>, Anim Walk), and on the first step queues a complaint subtitle: the line from
    /// <c>Hooks.Dialogue.Line(world, DialogueContext.Bumped, ped.Citizen, ped)</c> (skipped when the hook is null or
    /// returns null) via <c>Messages.QueueSubtitle(null, line, 2.5)</c>, only when the player is within
    /// <see cref="SubtitleRange"/>. Then <see cref="ReturnBrain"/>. Active. Further bumps are ignored; other stimuli
    /// default. The bumper's position is the stimulus position when made by a reaction, else the bumper tag's position.
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

        private bool _started;
        private Vec3 _push;

        /// <summary>Creates the brain staggering away from <paramref name="bumperTag"/> (its position is read on the first step).</summary>
        public StumbleBrain(PedTraits traits, int bumperTag)
            : base(traits)
        {
            BumperTag = bumperTag;
        }

        /// <summary>Who bumped.</summary>
        public int BumperTag { get; private set; }

        internal Vec3 From;
        internal bool HasFrom;

        /// <inheritdoc />
        public override string Name { get { return "Stumble"; } }

        /// <inheritdoc />
        public override bool IsActive { get { return true; } }

        /// <inheritdoc />
        public override void Step(GameWorld world, Ped ped, float dt)
        {
            Seconds += dt;
            if (!_started)
            {
                _started = true;
                Vec3 from = HasFrom ? From : PositionOf(world, BumperTag, ped.Position - Vec3.FromYawDeg(ped.YawDeg));
                Vec3 away = (ped.Position - from).Flat;
                _push = away.LengthSquared > 1e-8f ? away.Normalized : -Vec3.FromYawDeg(ped.YawDeg);
                Say(world, ped, DialogueContext.Bumped);
            }
            if (Seconds >= StaggerSeconds)
            {
                Assign(ped, new ReturnBrain(Traits));
                return;
            }
            ped.Move.DesiredVelocity = _push * StaggerSpeed;
            ped.Move.FacingYawDeg = ped.YawDeg;
            ped.Anim = PedAnim.Walk;
        }

        /// <inheritdoc />
        public override void React(GameWorld world, Ped ped, Stimulus stimulus)
        {
            if (stimulus.Kind != StimulusKind.Bumped) DefaultReact(world, ped, stimulus);
        }

        internal static void Say(GameWorld world, Ped ped, DialogueContext context)
        {
            IDialogueHook hook = world.Hooks.Dialogue;
            Ped player = world.Player;
            if (hook == null || player == null || Vec3.Distance(player.Position, ped.Position) > SubtitleRange) return;
            string line = hook.Line(world, context, ped.Citizen, ped);
            if (line != null) world.Messages.QueueSubtitle(null, line, SubtitleSeconds);
        }
    }

    /// <summary>
    /// The driver of a CS1 vehicle the player crashed into (<see cref="PedOrigin.ShockedDriver"/>), seated in seat 0:
    /// sits stunned (Anim Sit) for a seeded 2-4 s (active), then — when Traits.Fighter or a seeded
    /// <see cref="PedTraits.CarjackedFightChance"/> roll says fight, and the player is on foot within
    /// <see cref="FightBrain.GiveUpDistance"/> — gets out at the driver door (<c>TakeOutOfCar(ped, DoorPosition(car,
    /// left), car yaw − 90)</c>) and becomes a <see cref="FightBrain"/> against the player; otherwise it stays seated and
    /// becomes inactive (G6 demotes it with the usual rules). While seated, GunshotHeard, ShotAt, AimedAt or Hurt make it
    /// get out and flee (<see cref="FleeBrain"/>); other stimuli are ignored. Once out, default reactions.
    /// </summary>
    public sealed class ShockedDriverBrain : PedBrain
    {
        private readonly float _stun;
        private readonly bool _fight;
        private bool _done;

        /// <summary>Creates the brain; the stun time and fight roll are drawn from <c>world.Rng</c>.</summary>
        public ShockedDriverBrain(GameWorld world, PedTraits traits)
            : base(traits)
        {
            _stun = world.Rng.Range(2f, 4f);
            _fight = world.Rng.NextFloat() < PedTraits.CarjackedFightChance || traits.Fighter;
        }

        /// <inheritdoc />
        public override string Name { get { return "ShockedDriver"; } }

        /// <inheritdoc />
        public override bool IsActive { get { return !_done; } }

        /// <inheritdoc />
        public override void Step(GameWorld world, Ped ped, float dt)
        {
            Seconds += dt;
            PedSteering.Stop(ped, ped.Has(PedFlags.InVehicle) ? PedAnim.Sit : PedAnim.Idle);
            if (_done || Seconds < _stun) return;
            _done = true;
            Ped player = world.Player;
            if (!_fight || player == null || player.Has(PedFlags.InVehicle)
                || Vec3.Distance(player.Position, ped.Position) > FightBrain.GiveUpDistance) return;
            GetOut(world, ped);
            Assign(ped, new FightBrain(Traits, player.Tag));
        }

        /// <inheritdoc />
        public override void React(GameWorld world, Ped ped, Stimulus stimulus)
        {
            if (!ped.Has(PedFlags.InVehicle))
            {
                DefaultReact(world, ped, stimulus);
                return;
            }
            switch (stimulus.Kind)
            {
                case StimulusKind.GunshotHeard:
                case StimulusKind.ShotAt:
                case StimulusKind.AimedAt:
                case StimulusKind.Hurt:
                    GetOut(world, ped);
                    Flee(world, ped, stimulus);
                    break;
            }
        }

        private static void GetOut(GameWorld world, Ped ped)
        {
            Car car = world.FindCar(ped.VehicleId);
            if (car != null) world.TakeOutOfCar(ped, world.DoorPosition(car, true), car.Rotation.YawDeg - 90f);
        }
    }
}
