using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Peds
{
    /// <summary>
    /// Runs away from a threat at Traits.FleeSpeed (5-6 m/s; 5.5 when the traits carry none) for a seeded 6-15 s with
    /// Anim Panic and flag Panicking. The threat position follows <see cref="ThreatTag"/> while it resolves
    /// (<c>TryGetTagPosition</c>), else stays at the last known point. Outdoors: <see cref="PedSteering.Flee"/>; indoors
    /// (<c>InteriorId</c> ≥ 0 with an interior loaded): <see cref="PedPath"/> toward the interior's entrance spawn if it is
    /// farther from the threat than the ped is, else toward the NpcSpot farthest from the threat. When the time is up →
    /// <see cref="ReturnBrain"/> (Panicking cleared). Active.
    /// Reactions: VehicleThreat → <see cref="SidestepBrain"/>; Bumped ignored; any other stimulus re-targets to the new
    /// source and restarts the timer with a fresh draw.
    /// </summary>
    public sealed class FleeBrain : PedBrain
    {
        /// <summary>Shortest flee, seconds (6).</summary>
        public const float MinSeconds = 6f;
        /// <summary>Longest flee, seconds (15).</summary>
        public const float MaxSeconds = 15f;
        /// <summary>Flee speed used when the traits carry none, m/s (5.5).</summary>
        public const float DefaultSpeed = 5.5f;

        private PedPath _path;
        private bool _hasGoal;
        private Vec3 _goal;

        /// <summary>Creates the brain; the duration is drawn from <c>world.Rng</c>.</summary>
        public FleeBrain(GameWorld world, PedTraits traits, int threatTag, Vec3 threatPosition)
            : base(traits)
        {
            ThreatTag = threatTag;
            ThreatPosition = threatPosition;
            Duration = world.Rng.Range(MinSeconds, MaxSeconds);
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
            Seconds += dt;
            if (Seconds >= Duration)
            {
                Assign(ped, new ReturnBrain(Traits));
                return;
            }
            ThreatPosition = PositionOf(world, ThreatTag, ThreatPosition);
            ped.Flags |= PedFlags.Panicking;
            float speed = Traits.FleeSpeed > 0f ? Traits.FleeSpeed : DefaultSpeed;
            InteriorState interior = world.Interior;
            if (ped.InteriorId >= 0 && interior.IsInside && interior.Layout != null)
            {
                if (!_hasGoal) _goal = IndoorGoal(interior, ped);
                _hasGoal = true;
                if (_path == null) _path = new PedPath();
                _path.Follow(world, ped, _goal, speed, PedAnim.Panic, dt);
                return;
            }
            PedSteering.Flee(world, ped, ThreatPosition, speed, PedAnim.Panic, dt);
        }

        /// <inheritdoc />
        public override void React(GameWorld world, Ped ped, Stimulus stimulus)
        {
            if (stimulus.Kind == StimulusKind.Bumped) return;
            if (stimulus.Kind == StimulusKind.VehicleThreat)
            {
                DefaultReact(world, ped, stimulus);
                return;
            }
            ThreatTag = stimulus.SourceTag;
            ThreatPosition = stimulus.SourcePosition;
            Duration = world.Rng.Range(MinSeconds, MaxSeconds);
            Seconds = 0f;
            _hasGoal = false;
        }

        private Vec3 IndoorGoal(InteriorState interior, Ped ped)
        {
            Vec3 exit = interior.ToWorld(interior.Layout.EntranceSpawn);
            float mine = Vec3.DistanceXZ(ped.Position, ThreatPosition);
            if (Vec3.DistanceXZ(exit, ThreatPosition) > mine) return exit;
            Vec3 best = ped.Position;
            float bestD = mine;
            List<NpcSpot> spots = interior.Layout.NpcSpots;
            for (int i = 0; i < spots.Count; i++)
            {
                Vec3 p = interior.ToWorld(spots[i].Position);
                float d = Vec3.DistanceXZ(p, ThreatPosition);
                if (d > bestD)
                {
                    bestD = d;
                    best = p;
                }
            }
            return best;
        }
    }

    /// <summary>
    /// Dodges a car coming at the ped: for <see cref="DodgeSeconds"/> moves at <see cref="DodgeSpeed"/> perpendicular to
    /// the car's velocity, to the side of the car's path the ped already stands on (exactly on the path → the car's
    /// right; a car with no velocity counts as moving toward the ped), Anim Sprint, then <see cref="FleeBrain"/> from the
    /// car. Active. A further VehicleThreat restarts the dodge; other stimuli are ignored while dodging.
    /// </summary>
    public sealed class SidestepBrain : PedBrain
    {
        /// <summary>Dodge duration, seconds (0.8).</summary>
        public const float DodgeSeconds = 0.8f;
        /// <summary>Dodge speed, m/s (5.5).</summary>
        public const float DodgeSpeed = 5.5f;

        private bool _started;
        private Vec3 _dir;

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
            Seconds += dt;
            Vec3 carPos = PositionOf(world, CarTag, ped.Position - Vec3.FromYawDeg(ped.YawDeg));
            if (!_started)
            {
                _started = true;
                Vec3 toPed = (ped.Position - carPos).Flat;
                Vec3 v = CarVelocity(world).Flat;
                if (v.LengthSquared < 0.01f) v = toPed;
                if (v.LengthSquared < 1e-6f) v = Vec3.FromYawDeg(ped.YawDeg);
                Vec3 right = new Vec3(v.Z, 0f, -v.X).Normalized;
                _dir = Vec3.Dot(toPed, right) >= 0f ? right : -right;
            }
            if (Seconds >= DodgeSeconds)
            {
                Assign(ped, new FleeBrain(world, Traits, CarTag, carPos));
                return;
            }
            PedSteering.Go(ped, _dir, DodgeSpeed, PedAnim.Sprint, dt);
        }

        /// <inheritdoc />
        public override void React(GameWorld world, Ped ped, Stimulus stimulus)
        {
            if (stimulus.Kind != StimulusKind.VehicleThreat) return;
            CarTag = stimulus.SourceTag;
            Seconds = 0f;
            _started = false;
        }

        private Vec3 CarVelocity(GameWorld world)
        {
            Car car = world.CarFromTag(CarTag, false, 0);
            if (car != null) return car.Body.Velocity;
            AmbientVehicle av;
            if (WorldTag.KindOf(CarTag) == TagKind.AmbientVehicle && world.TryGetAmbientVehicle((ushort)WorldTag.IdOf(CarTag), false, out av))
                return av.Velocity;
            return Vec3.Zero;
        }
    }

    /// <summary>
    /// Crouches in place (Anim Sit — the closest animation G6 offers; flag Panicking) facing away from the threat for a
    /// seeded 6-12 s, then <see cref="FleeBrain"/> from the threat. Active. Reactions: Punched, Hurt, or AimedAt within
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
            Duration = world.Rng.Range(MinSeconds, MaxSeconds);
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
            Seconds += dt;
            if (Seconds >= Duration)
            {
                Assign(ped, new FleeBrain(world, Traits, ThreatTag, PositionOf(world, ThreatTag, ThreatPosition)));
                return;
            }
            ped.Flags |= PedFlags.Panicking;
            PedSteering.Stop(ped, PedAnim.Sit);
            Vec3 away = (ped.Position - ThreatPosition).Flat;
            if (away.LengthSquared > 1e-6f) PedSteering.TurnToward(ped, Vec3.YawDegOf(away), dt);
        }

        /// <inheritdoc />
        public override void React(GameWorld world, Ped ped, Stimulus stimulus)
        {
            if (stimulus.Kind == StimulusKind.Punched || stimulus.Kind == StimulusKind.Hurt
                || (stimulus.Kind == StimulusKind.AimedAt && stimulus.Value <= HandsUpBrain.Range))
                DefaultReact(world, ped, stimulus);
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

        private bool _started;
        private float _lapse;

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
            Seconds += dt;
            Ped aimer = world.PedFromTag(AimerTag, false, 0);
            if (!_started)
            {
                _started = true;
                world.Emit(GameEventType.HandsUp, ped.Position, AimerTag, ped.Tag, 0f, null);
                if (Traits.Clerk && !Robbed)
                {
                    Robbed = true;
                    float building = world.Interior.Building != null ? world.Interior.Building.Id : 0f;
                    world.Emit(GameEventType.Robbery, ped.Position, AimerTag, ped.Tag, building, null);
                }
            }
            bool held = aimer != null && !aimer.IsDead && aimer.Has(PedFlags.Aiming)
                && (aimer.Kind == PedKind.Player ? world.PlayerState.AimTargetTag == ped.Tag : Vec3.Distance(aimer.Position, ped.Position) <= Range);
            _lapse = held ? 0f : _lapse + dt;
            if (_lapse >= AimLostGraceSeconds - 1e-4f)
            {
                Assign(ped, new FleeBrain(world, Traits, AimerTag, PositionOf(world, AimerTag, ped.Position)));
                return;
            }
            ped.Flags |= PedFlags.HandsUp;
            PedSteering.Stop(ped, PedAnim.HandsUp);
            if (aimer != null)
            {
                Vec3 to = (aimer.Position - ped.Position).Flat;
                if (to.LengthSquared > 1e-6f) PedSteering.TurnToward(ped, Vec3.YawDegOf(to), dt);
            }
        }

        /// <inheritdoc />
        public override void React(GameWorld world, Ped ped, Stimulus stimulus)
        {
            switch (stimulus.Kind)
            {
                case StimulusKind.AimedAt:
                    _lapse = 0f;
                    break;
                case StimulusKind.Punched:
                case StimulusKind.Hurt:
                case StimulusKind.ShotAt:
                    Flee(world, ped, stimulus);
                    break;
            }
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
            float d = world.Rng.Range(MinSeconds, MaxSeconds);
            Duration = ped.Has(PedFlags.Injured) ? InjuredSeconds : d;
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
            Seconds += dt;
            if (Seconds >= Duration)
            {
                ped.Flags &= ~PedFlags.KnockedDown;
                Assign(ped, new FleeBrain(world, Traits, ped.LastAttackerTag,
                    PositionOf(world, ped.LastAttackerTag, ped.Position - Vec3.FromYawDeg(ped.YawDeg))));
                return;
            }
            ped.Flags |= PedFlags.KnockedDown;
            PedSteering.Stop(ped, PedAnim.LieDown);
        }

        /// <inheritdoc />
        public override void React(GameWorld world, Ped ped, Stimulus stimulus)
        {
        }
    }
}
