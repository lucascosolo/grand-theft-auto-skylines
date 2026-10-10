using System;
using System.Collections.Generic;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Peds;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Session;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Combat
{
    /// <summary>
    /// The player's weapons and every damage consequence that arrives as an event (G7 contract F-CMB). Order
    /// <see cref="SystemOrder.Combat"/> (50). Add once per session: <c>session.AddSystem(new CombatSystem())</c>.
    /// <para><b>Events</b> (own cursor, starting at <c>Events.NextSeq</c> on the first step, so only later events count):
    /// <see cref="GameEventType.PedHitByVehicle"/> → <see cref="Damage.HitByVehicle"/>(B, A, Value);
    /// <see cref="GameEventType.Explosion"/> → <see cref="Damage.Explode"/>(Position, A);
    /// <see cref="GameEventType.Fell"/> → the player's LastDamageTime = event time (G6 already applied the damage);
    /// <see cref="GameEventType.PlayerRespawned"/> → player weapon timers reset, KnockedDown cleared, the weapon in hand
    /// re-validated.</para>
    /// <para><b>Player weapons</b>, skipped entirely unless the player exists, is alive (Health &gt; 0,
    /// <c>PlayerState.Life</c> Alive), on foot, and no menu is open (then the wheel closes without selecting and
    /// AimTargetTag is 0):
    /// <list type="number">
    /// <item>The weapon in hand must be owned, else Fists are equipped (e.g. after Busted stripped weapons).</item>
    /// <item>Weapon wheel: WeaponWheel held opens <see cref="Wheel"/> and feeds it the look deltas; on release the
    /// highlighted selectable weapon is equipped. While open, Attack, Reload and Next/PrevWeapon are ignored.</item>
    /// <item>NextWeapon / PrevWeapon pressed: equip the next / previous selectable weapon in WeaponId order, wrapping
    /// (Fists always selectable; a firearm needs ammo &gt; 0).</item>
    /// <item>Reload pressed → <see cref="WeaponUser.StartReload"/>.</item>
    /// <item>Aim and lock-on, while the Aiming flag is set (G6 sets it while Aim is held on foot) and a firearm is in
    /// hand and Aim is held: the lock-on candidate is the Game ped (alive, on foot, visible: a static raycast from the muzzle to its chest
    /// is clear) with the smallest angle between the view forward (<c>world.ViewRotation</c>) and the direction from
    /// <c>world.ViewPosition</c> to its chest, within <see cref="LockOnAngleDeg"/> and <see cref="LockOnRange"/> of the
    /// player, that is Hostile or is the current AimTargetTag. Otherwise the aimed person is the first person hit by a
    /// ray from ViewPosition along the view forward within the weapon's range (an ambient citizen within
    /// <see cref="AimPromoteRange"/> is promoted, cause = player tag). <c>PlayerState.AimTargetTag</c> = that tag or 0.
    /// When it changes to a non-zero tag, <see cref="GameEventType.AimedAt"/> (A = player, B = target) is emitted once.
    /// Not aiming, or melee in hand → AimTargetTag 0.</item>
    /// <item>Attack held → <see cref="WeaponUser.TryAttack"/>. Firearm aim point: the lock-on target's chest, else the
    /// view ray's first hit, else ViewPosition + forward × Range; origin <see cref="Weapons.MuzzleOf"/>; spread
    /// <see cref="WeaponTable.EffectiveSpreadDeg"/>(moving: horizontal speed &gt; 0.5, aiming: Aiming flag). After an
    /// attack the player's Anim is Shoot (firearm) or Punch (melee) for <see cref="AttackAnimSeconds"/> (overriding G6's
    /// choice, which ran earlier in the step).</item>
    /// <item>The player's <see cref="WeaponUser"/> steps; <see cref="Damage.Regenerate"/> runs; a player KnockedDown flag
    /// set by damage is cleared after <see cref="Damage.PlayerKnockDownSeconds"/>.</item>
    /// </list></para>
    /// <para>Wasted: G6's Respawn system clears the current weapon's ammo (<c>Profile.SetAmmo(current, 0)</c>); the
    /// magazine invariant then empties the magazine. Steady state allocates nothing.</para>
    /// </summary>
    public sealed class CombatSystem : IGameSystem, ITimeScaleSource, IAmmoReadout
    {
        /// <summary>Soft lock-on cone half-angle, degrees (6).</summary>
        public const float LockOnAngleDeg = 6f;
        /// <summary>Soft lock-on range, metres (40).</summary>
        public const float LockOnRange = 40f;
        /// <summary>An ambient citizen under the crosshair within this distance (m) is promoted so it can react (25).</summary>
        public const float AimPromoteRange = 25f;
        /// <summary>Seconds the Shoot / Punch anim is held after an attack (0.25).</summary>
        public const float AttackAnimSeconds = 0.25f;

        /// <summary>Creates the system; the player's <see cref="WeaponUser"/> binds to the world's profile on the first step.</summary>
        public CombatSystem()
        {
            Wheel = new WeaponWheelModel();
        }

        /// <inheritdoc />
        public int Order { get { return SystemOrder.Combat; } }

        /// <summary>The weapon wheel (HUD, M4).</summary>
        public WeaponWheelModel Wheel { get; private set; }

        /// <summary>The player's weapon timing (null before the first step).</summary>
        public WeaponUser PlayerWeapon { get; private set; }

        /// <summary>The player's weapon stats (for the HUD's weapon name and ammo display).</summary>
        public WeaponStats PlayerWeaponStats(GameWorld world)
        {
            return world.Player == null ? WeaponTable.Get(WeaponId.Fists) : WeaponTable.Get((WeaponId)world.Player.CurrentWeapon);
        }

        /// <inheritdoc />
        public void Step(GameWorld world, float dt)
        {
            if (!_started)
            {
                _cursor = world.Events.NextSeq;
                _started = true;
            }
            Ped player = world.Player;
            ReadEvents(world, player);
            if (player == null) return;
            if (PlayerWeapon == null) PlayerWeapon = new WeaponUser(world.Profile);
            PlayerState ps = world.PlayerState;

            int cur = player.CurrentWeapon;
            if (cur < 0 || cur >= WeaponInfo.Count || !world.Profile.OwnsWeapon((WeaponId)cur)) PlayerWeapon.Load(player, WeaponId.Fists);
            else if (cur != _lastWeapon) PlayerWeapon.Load(player, (WeaponId)cur);
            _lastWeapon = player.CurrentWeapon;

            PlayerWeapon.Step(player, dt);
            Damage.Regenerate(player, world.Clock.GameSeconds, dt);
            if (player.Has(PedFlags.KnockedDown))
            {
                _knockSeconds += dt;
                if (_knockSeconds >= Damage.PlayerKnockDownSeconds)
                {
                    player.Flags &= ~PedFlags.KnockedDown;
                    _knockSeconds = 0f;
                }
            }
            else _knockSeconds = 0f;

            bool canAct = player.Health > 0f && !player.IsDead && ps.Life == LifeState.Alive && !player.Has(PedFlags.InVehicle)
                && !player.Has(PedFlags.KnockedDown) && !world.Menu.IsOpen;
            if (!canAct)
            {
                WeaponId ignored;
                if (Wheel.IsOpen) Wheel.Close(out ignored);
                ps.AimTargetTag = 0;
                _animSeconds = 0f;
                return;
            }

            InputFrame input = world.Input;
            if (input.IsHeld(GameButton.WeaponWheel))
            {
                if (!Wheel.IsOpen) Wheel.Open(world.Profile, (WeaponId)player.CurrentWeapon);
                Wheel.MovePointer(input.LookYawDeg, input.LookPitchDeg);
                ps.AimTargetTag = 0;
                return;
            }
            if (Wheel.IsOpen)
            {
                WeaponId selected;
                if (Wheel.Close(out selected)) Switch(player, selected);
            }
            if (input.WasPressed(GameButton.NextWeapon)) Switch(player, Cycle(world.Profile, (WeaponId)player.CurrentWeapon, 1));
            else if (input.WasPressed(GameButton.PrevWeapon)) Switch(player, Cycle(world.Profile, (WeaponId)player.CurrentWeapon, -1));
            if (input.WasPressed(GameButton.Reload)) PlayerWeapon.StartReload(player);

            WeaponStats stats = WeaponTable.Get((WeaponId)player.CurrentWeapon);
            Aim(world, player, ps, stats);

            if (input.IsHeld(GameButton.Attack))
            {
                Vec3 origin = Weapons.MuzzleOf(player);
                Vec3 direction = Vec3.Forward;
                float spread = 0f;
                if (stats.IsFirearm)
                {
                    direction = AimPoint(world, player, ps, stats) - origin;
                    spread = WeaponTable.EffectiveSpreadDeg(stats.Id, player.Motor.Velocity.LengthXZ > WeaponTable.MovingSpeed,
                        player.Has(PedFlags.Aiming));
                }
                ShotResult shot;
                MeleeResult melee;
                if (PlayerWeapon.TryAttack(world, player, origin, direction, spread, out shot, out melee) == AttackOutcome.Attacked)
                {
                    _animSeconds = AttackAnimSeconds;
                    _anim = stats.IsFirearm ? PedAnim.Shoot : PedAnim.Punch;
                }
            }
            if (_animSeconds > 0f)
            {
                player.Anim = _anim;
                _animSeconds -= dt;
            }
        }

        /// <summary>The next (<paramref name="direction"/> = +1) or previous (−1) selectable weapon after <paramref name="current"/>, wrapping; Fists when none.</summary>
        public static WeaponId Cycle(PlayerProfile profile, WeaponId current, int direction)
        {
            for (int k = 1; k <= WeaponInfo.Count; k++)
            {
                int i = (((int)current + direction * k) % WeaponInfo.Count + WeaponInfo.Count) % WeaponInfo.Count;
                var w = (WeaponId)i;
                if (profile.OwnsWeapon(w) && (!WeaponInfo.UsesAmmo(w) || profile.GetAmmo(w) > 0)) return w;
            }
            return WeaponId.Fists;
        }

        private bool _started;
        private long _cursor;
        private int _lastWeapon = -1;
        private float _knockSeconds;
        private float _animSeconds;
        private PedAnim _anim;

        /// <inheritdoc />
        public float DesiredTimeScale { get { return Wheel.DesiredTimeScale; } }

        /// <inheritdoc />
        public bool TryGetAmmo(GameWorld world, out int magazine, out int reserve)
        {
            magazine = 0;
            reserve = 0;
            Ped player = world.Player;
            if (player == null || !WeaponTable.Get((WeaponId)player.CurrentWeapon).IsFirearm) return false;
            magazine = player.MagazineAmmo;
            reserve = Math.Max(0, world.Profile.GetAmmo((WeaponId)player.CurrentWeapon) - magazine);
            return true;
        }

        /// <summary>
        /// Adds G7 to a session: a <see cref="PedsSystem"/> and a CombatSystem, and installs <see cref="PedBrainFactory"/>
        /// (keeping a previously installed factory's <c>Cops</c>), the time-scale source and the ammo readout on
        /// <c>world.Hooks</c>. Returns the combat system (its <see cref="Wheel"/> is the HUD's weapon wheel).
        /// </summary>
        public static CombatSystem Install(GameSession session)
        {
            if (session == null) throw new ArgumentNullException("session");
            GameHooks hooks = session.World.Hooks;
            var factory = new PedBrainFactory();
            var old = hooks.Brains as PedBrainFactory;
            if (old != null) factory.Cops = old.Cops;
            hooks.Brains = factory;
            var combat = new CombatSystem();
            hooks.TimeScale = combat;
            hooks.Ammo = combat;
            session.AddSystem(new PedsSystem());
            session.AddSystem(combat);
            return combat;
        }

        private void Switch(Ped player, WeaponId weapon)
        {
            PlayerWeapon.Equip(player, weapon);
            _lastWeapon = player.CurrentWeapon;
        }

        private void ReadEvents(GameWorld world, Ped player)
        {
            GameEvent e;
            while (world.Events.Read(ref _cursor, out e))
            {
                switch (e.Type)
                {
                    case GameEventType.PedHitByVehicle:
                        Damage.HitByVehicle(world, e.ActorB, e.ActorA, e.Value);
                        break;
                    case GameEventType.Explosion:
                        Damage.Explode(world, e.Position, e.ActorA);
                        break;
                    case GameEventType.Fell:
                        if (player != null) player.LastDamageTime = e.GameTime;
                        break;
                    case GameEventType.PlayerRespawned:
                        if (PlayerWeapon != null) PlayerWeapon.ResetTimers();
                        if (player != null) player.Flags &= ~PedFlags.KnockedDown;
                        _lastWeapon = -1;
                        _knockSeconds = 0f;
                        break;
                }
            }
        }

        private void Aim(GameWorld world, Ped player, PlayerState ps, WeaponStats stats)
        {
            int target = 0;
            if (player.Has(PedFlags.Aiming) && world.Input.IsHeld(GameButton.Aim) && stats.IsFirearm)
            {
                target = LockOn(world, player, ps.AimTargetTag);
                if (target == 0) target = UnderCrosshair(world, player, stats);
            }
            if (target != 0 && target != ps.AimTargetTag)
            {
                Vec3 at;
                float d = world.TryGetTagPosition(target, out at) ? Vec3.Distance(at, player.Position) : 0f;
                world.Emit(GameEventType.AimedAt, player.Position, player.Tag, target, d, null);
            }
            ps.AimTargetTag = target;
        }

        private static int LockOn(GameWorld world, Ped player, int current)
        {
            Vec3 view = world.ViewPosition;
            Vec3 fwd = world.ViewRotation.Forward;
            Vec3 muzzle = Weapons.MuzzleOf(player);
            float best = LockOnAngleDeg;
            int tag = 0;
            IList<Ped> peds = world.Peds;
            for (int i = 0; i < peds.Count; i++)
            {
                Ped p = peds[i];
                if (p == player || p.IsDead || p.Has(PedFlags.InVehicle) || p.Has(PedFlags.Invisible)) continue;
                if (!p.Has(PedFlags.Hostile) && p.Tag != current) continue;
                Vec3 chest = p.Position + new Vec3(0f, Weapons.ChestHeight, 0f);
                if (Vec3.Distance(chest, player.Position) > LockOnRange) continue;
                Vec3 to = chest - view;
                float len = to.Length;
                if (len < 1e-3f) continue;
                float cos = MathUtil.Clamp(Vec3.Dot(to / len, fwd), -1f, 1f);
                float ang = (float)(Math.Acos(cos) * MathUtil.Rad2Deg);
                if (ang > best) continue;
                Vec3 ray = chest - muzzle;
                PhysicsHit hit;
                if (world.Collision.Raycast(muzzle, ray, ray.Length, QueryMask.StaticOnly, out hit)) continue;
                best = ang;
                tag = p.Tag;
            }
            return tag;
        }

        private static int UnderCrosshair(GameWorld world, Ped player, WeaponStats stats)
        {
            PhysicsHit hit;
            if (!world.Collision.Raycast(world.ViewPosition, world.ViewRotation.Forward, stats.Range, QueryMask.All.Ignoring(player.Tag), out hit))
                return 0;
            TagKind kind = WorldTag.KindOf(hit.Tag);
            if (kind == TagKind.Ped)
            {
                Ped p = world.PedFromTag(hit.Tag, false, 0);
                return p != null && !p.IsDead ? p.Tag : 0;
            }
            if (kind != TagKind.AmbientCitizen || Vec3.Distance(hit.Point, player.Position) > AimPromoteRange) return 0;
            Ped promoted = world.PedFromTag(hit.Tag, true, player.Tag);
            return promoted != null && !promoted.IsDead ? promoted.Tag : 0;
        }

        private static Vec3 AimPoint(GameWorld world, Ped player, PlayerState ps, WeaponStats stats)
        {
            if (ps.AimTargetTag != 0)
            {
                Ped t = world.PedFromTag(ps.AimTargetTag, false, 0);
                if (t != null) return t.Position + new Vec3(0f, Weapons.ChestHeight, 0f);
            }
            Vec3 fwd = world.ViewRotation.Forward;
            PhysicsHit hit;
            if (world.Collision.Raycast(world.ViewPosition, fwd, stats.Range, QueryMask.All.Ignoring(player.Tag), out hit)) return hit.Point;
            return world.ViewPosition + fwd * stats.Range;
        }
    }
}
