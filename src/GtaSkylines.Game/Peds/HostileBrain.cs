using GtaSkylines.Game.Combat;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Peds
{
    /// <summary>
    /// An armed enemy (G9 mission hostiles: <c>ped.Brain = new HostileBrain(world, traits, WeaponId.Pistol, player.Tag)</c>).
    /// Keeps flag Hostile set (so the player's lock-on prefers it) and fights <see cref="TargetTag"/> with its own
    /// <see cref="WeaponUser"/> (unlimited reserve, real magazines and reloads):
    /// <list type="bullet">
    /// <item>Target missing, dead: stands (Idle), Aiming cleared.</item>
    /// <item>Firearm: when the target's chest is out of sight (<see cref="Perception.CanSee(GameWorld, Vec3, float, float, Vec3)"/>
    /// facing the target, range = <see cref="EngageFraction"/> × weapon range) it runs toward the target
    /// (<see cref="ApproachSpeed"/>, Anim Run). In sight: flag Aiming, faces the target, and after
    /// <see cref="ReactionSeconds"/> of continuous sight fires at the target's chest through
    /// <see cref="WeaponUser.TryAttack"/> with spread = weapon spread + <see cref="SpreadPerMetre"/> × distance (× 2 when the
    /// target moves faster than 3 m/s). Every seeded 2-4 s, and at once after losing health, it strafes for
    /// <see cref="StrafeSeconds"/> perpendicular to the target at <see cref="StrafeSpeed"/> (alternating sides) while still
    /// shooting — its "take cover" move.</item>
    /// <item>Melee weapon: approaches and swings like <see cref="FightBrain"/>.</item>
    /// </list>
    /// Knock-downs (<see cref="IKnockDownHandler"/>): lies <see cref="DownSeconds"/>, then resumes. Ignores stimuli. Active.
    /// Randomness from <c>world.Rng</c>.
    /// </summary>
    public sealed class HostileBrain : PedBrain, IKnockDownHandler
    {
        /// <summary>Approach speed, m/s (3.6).</summary>
        public const float ApproachSpeed = 3.6f;
        /// <summary>Engages within this fraction of the weapon's range (0.8).</summary>
        public const float EngageFraction = 0.8f;
        /// <summary>Seconds of continuous sight before the first shot (0.6).</summary>
        public const float ReactionSeconds = 0.6f;
        /// <summary>Extra spread per metre of distance, degrees (0.08).</summary>
        public const float SpreadPerMetre = 0.08f;
        /// <summary>Strafe duration, seconds (1).</summary>
        public const float StrafeSeconds = 1f;
        /// <summary>Strafe speed, m/s (2.5).</summary>
        public const float StrafeSpeed = 2.5f;
        /// <summary>Seconds on the ground after a knock-down (2).</summary>
        public const float DownSeconds = 2f;

        private readonly WeaponUser _gun = new WeaponUser(null);
        private bool _armed;
        private float _seen, _nextStrafe, _strafeLeft, _down, _lastHealth = -1f;
        private int _side = 1;

        /// <summary>Creates the brain; <paramref name="weapon"/> is put in hand on the first step.</summary>
        public HostileBrain(GameWorld world, PedTraits traits, WeaponId weapon, int targetTag)
            : base(traits)
        {
            Weapon = weapon;
            TargetTag = targetTag;
            _nextStrafe = world.Rng.Range(2f, 4f);
        }

        /// <summary>The weapon used.</summary>
        public WeaponId Weapon { get; private set; }

        /// <summary>Who it fights.</summary>
        public int TargetTag { get; set; }

        /// <inheritdoc />
        public override string Name { get { return "Hostile"; } }

        /// <inheritdoc />
        public override bool IsActive { get { return true; } }

        /// <inheritdoc />
        public void OnKnockedDown(GameWorld world, Ped ped, int causeTag)
        {
            _down = DownSeconds;
        }

        /// <inheritdoc />
        public override void React(GameWorld world, Ped ped, Stimulus stimulus)
        {
        }

        /// <inheritdoc />
        public override void Step(GameWorld world, Ped ped, float dt)
        {
            Seconds += dt;
            ped.Flags |= PedFlags.Hostile;
            if (!_armed)
            {
                _armed = true;
                _gun.Load(ped, Weapon);
            }
            _gun.Step(ped, dt);
            if (_down > 0f)
            {
                _down -= dt;
                ped.Flags &= ~PedFlags.Aiming;
                PedSteering.Stop(ped, PedAnim.LieDown);
                if (_down <= 0f) ped.Flags &= ~PedFlags.KnockedDown;
                return;
            }
            Ped target = world.PedFromTag(TargetTag, false, 0);
            if (target == null || target.IsDead)
            {
                ped.Flags &= ~PedFlags.Aiming;
                PedSteering.Stop(ped, PedAnim.Idle);
                return;
            }
            WeaponStats stats = WeaponTable.Get(Weapon);
            Vec3 to = (target.Position - ped.Position).Flat;
            float dist = to.Length;
            if (!stats.IsFirearm)
            {
                if (dist > FightBrain.StrikeDistance)
                {
                    PedSteering.Seek(world, ped, target.Position, ApproachSpeed, PedAnim.Run, dt);
                    return;
                }
                PedSteering.Stop(ped, PedAnim.Idle);
                if (dist > 1e-3f) ped.YawDeg = Vec3.YawDegOf(to);
                Attack(world, ped, ped.Position, to, 0f);
                return;
            }

            Vec3 chest = target.Position + new Vec3(0f, Weapons.ChestHeight, 0f);
            Vec3 eye = ped.Position + new Vec3(0f, Perception.EyeHeight, 0f);
            float yaw = dist > 1e-3f ? Vec3.YawDegOf(to) : ped.YawDeg;
            if (!Perception.CanSee(world, eye, yaw, stats.Range * EngageFraction, chest))
            {
                _seen = 0f;
                ped.Flags &= ~PedFlags.Aiming;
                PedSteering.Seek(world, ped, target.Position, ApproachSpeed, PedAnim.Run, dt);
                return;
            }
            _seen += dt;
            ped.Flags |= PedFlags.Aiming;
            if (_lastHealth >= 0f && ped.Health < _lastHealth && _strafeLeft <= 0f) _nextStrafe = Seconds;
            _lastHealth = ped.Health;
            if (_strafeLeft <= 0f && Seconds >= _nextStrafe)
            {
                _strafeLeft = StrafeSeconds;
                _side = -_side;
                _nextStrafe = Seconds + StrafeSeconds + world.Rng.Range(2f, 4f);
            }
            if (_strafeLeft > 0f && dist > 1e-3f)
            {
                _strafeLeft -= dt;
                Vec3 side = new Vec3(to.Z, 0f, -to.X) / dist * _side;
                Vec3 dir;
                if (PedSteering.ChooseDirection(world, ped, side, StrafeSpeed, out dir)) ped.Move.DesiredVelocity = dir * StrafeSpeed;
                else ped.Move.DesiredVelocity = Vec3.Zero;
                ped.Move.Jump = false;
            }
            else ped.Move.DesiredVelocity = Vec3.Zero;
            ped.YawDeg = yaw;
            ped.Move.FacingYawDeg = yaw;
            ped.Anim = PedAnim.Aim;
            if (_seen < ReactionSeconds) return;
            float spread = stats.SpreadDeg + SpreadPerMetre * dist;
            if (target.Motor.Velocity.LengthXZ > 3f) spread *= 2f;
            Vec3 muzzle = Weapons.MuzzleOf(ped);
            Attack(world, ped, muzzle, chest - muzzle, spread);
        }

        private void Attack(GameWorld world, Ped ped, Vec3 origin, Vec3 direction, float spread)
        {
            ShotResult shot;
            MeleeResult melee;
            _gun.TryAttack(world, ped, origin, direction, spread, out shot, out melee);
        }
    }
}
