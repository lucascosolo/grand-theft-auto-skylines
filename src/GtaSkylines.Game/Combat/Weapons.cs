using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Combat
{
    /// <summary>What a ray hit.</summary>
    public enum ShotHitKind
    {
        /// <summary>Nothing within range.</summary>
        Nothing,
        /// <summary>Static geometry (impact only).</summary>
        Static,
        /// <summary>A person (Game ped, or an ambient citizen that was promoted by the hit).</summary>
        Person,
        /// <summary>A vehicle (Game car, or an ambient vehicle that was promoted by the hit).</summary>
        Vehicle,
    }

    /// <summary>Summary of one trigger pull (all pellets).</summary>
    public struct ShotResult
    {
        /// <summary>Rays cast (1, or 8 for the shotgun).</summary>
        public int Rays;
        /// <summary>What the first ray hit.</summary>
        public ShotHitKind FirstHitKind;
        /// <summary>Tag of the first person or vehicle hit by any ray (after promotion), 0 none.</summary>
        public int FirstHitTag;
        /// <summary>End point of the first ray (hit point or the range limit).</summary>
        public Vec3 FirstEndPoint;
        /// <summary>Person damage dealt by all rays (health + armour lost).</summary>
        public float PersonDamage;
        /// <summary>Car health removed by all rays.</summary>
        public float CarDamage;
        /// <summary>Rays that were headshots.</summary>
        public int Headshots;
        /// <summary>True when some ray killed a person.</summary>
        public bool Killed;
    }

    /// <summary>Result of one melee swing.</summary>
    public struct MeleeResult
    {
        /// <summary>True when someone was hit.</summary>
        public bool Hit;
        /// <summary>Victim ped tag (after promotion), 0 on a miss.</summary>
        public int VictimTag;
        /// <summary>Damage before armour (the weapon's).</summary>
        public float Damage;
        /// <summary>What the damage did.</summary>
        public DamageResult Outcome;
    }

    /// <summary>
    /// Attacks for anyone (player, G7 brains, G8 cops). Stateless: rate of fire, magazines and reloads belong to
    /// <see cref="WeaponUser"/>. Randomness (spread directions, knock-down rolls) comes from <c>world.Rng</c> in a fixed
    /// order, so a seed reproduces every result. Nothing allocates (internal scratch buffers; main thread only).
    /// </summary>
    public static class Weapons
    {
        /// <summary>Height of a ped's chest above its feet, metres: the aim point on a person (1.2).</summary>
        public const float ChestHeight = 1.2f;
        /// <summary>Height of the muzzle above the feet, metres (1.45).</summary>
        public const float MuzzleHeight = 1.45f;
        /// <summary>Muzzle offset along the facing, metres (0.4).</summary>
        public const float MuzzleForward = 0.4f;
        /// <summary>Melee arc: full angle in degrees centred on the facing (70, i.e. ±35°).</summary>
        public const float MeleeArcDeg = 70f;
        /// <summary>Largest vertical gap between attacker and victim feet for a melee hit, metres (1.0).</summary>
        public const float MeleeMaxHeightGap = 1f;
        /// <summary>Hearing radius stored in the Value of every Gunshot event, metres (60, = <c>Perception.GunshotHearingRadius</c>).</summary>
        public const float GunshotNoiseRadius = 60f;

        /// <summary>World muzzle point of a ped: feet + (0, 1.45, 0) + facing × 0.4.</summary>
        public static Vec3 MuzzleOf(Ped ped)
        {
            return ped.Position + new Vec3(0f, MuzzleHeight, 0f) + Vec3.FromYawDeg(ped.YawDeg) * MuzzleForward;
        }

        /// <summary>
        /// Contract form <c>Fire(shooter, origin, direction)</c>: fires <c>shooter.CurrentWeapon</c> with
        /// <see cref="WeaponTable.EffectiveSpreadDeg"/>(weapon, horizontal motor speed &gt; 0.5, Aiming flag). Same as the
        /// full overload otherwise. A melee weapon in hand → default result, nothing emitted.
        /// </summary>
        public static ShotResult Fire(GameWorld world, Ped shooter, Vec3 origin, Vec3 direction)
        {
            WeaponId weapon = (WeaponId)shooter.CurrentWeapon;
            if (!WeaponTable.Get(weapon).IsFirearm) return default(ShotResult);
            bool moving = shooter.Motor.Velocity.LengthXZ > WeaponTable.MovingSpeed;
            float spread = WeaponTable.EffectiveSpreadDeg(weapon, moving, shooter.Has(PedFlags.Aiming));
            return Fire(world, shooter, weapon, origin, direction, spread);
        }

        /// <summary>
        /// One trigger pull of firearm <paramref name="weapon"/> from <paramref name="origin"/> along
        /// <paramref name="direction"/> (need not be normalised; zero → default result). Does not check ammo or rate.
        /// <para>For each of <c>Pellets</c> rays, in order: a direction drawn uniformly from the cone of half-angle
        /// <paramref name="spreadDeg"/> around <paramref name="direction"/> (two <c>world.Rng.NextFloat</c> calls per ray,
        /// even when spread is 0); <c>Collision.Raycast(origin, dir, Range, QueryMask.All.Ignoring(shooter.Tag))</c>; then by
        /// <c>WorldTag.KindOf(hit.Tag)</c>:
        /// Ped → <see cref="Damage.ApplyToPed"/>(Damage × (2.5 if <see cref="Damage.IsHeadshot"/>), Bullet, no knock-down);
        /// AmbientCitizen → <c>PedFromTag(tag, promote: true, cause: shooter tag)</c> then the same;
        /// Car → <c>world.DamageCar(car, Damage × 0.25, shooter tag, point)</c>;
        /// AmbientVehicle/AmbientParkedVehicle → <c>CarFromTag(tag, promote: true, shooter tag)</c> then the same;
        /// static or a tag that resolves to nothing → impact only. Every ray emits <see cref="GameEventType.Shot"/>
        /// (Position = origin, Position2 = hit point or origin + dir × Range, A = shooter tag, B = person/vehicle tag after
        /// promotion or 0, Value = damage dealt by that ray: person health + armour lost, or car health lost).</para>
        /// <para>After the rays: one <see cref="GameEventType.Gunshot"/> (Position = origin, A = shooter tag, Value =
        /// <see cref="GunshotNoiseRadius"/>). <paramref name="shooter"/> may be null for a scripted shot (tags 0, no
        /// ignore). Sets the shooter's Anim to Shoot.</para>
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="weapon"/> is a melee weapon.</exception>
        public static ShotResult Fire(GameWorld world, Ped shooter, WeaponId weapon, Vec3 origin, Vec3 direction, float spreadDeg)
        {
            WeaponStats stats = WeaponTable.Get(weapon);
            if (!stats.IsFirearm) throw new ArgumentException("not a firearm", "weapon");
            var r = default(ShotResult);
            if (direction.LengthSquared < 1e-12f) return r;
            Vec3 fwd = direction.Normalized;
            int shooterTag = shooter == null ? 0 : shooter.Tag;
            QueryMask mask = shooter == null ? QueryMask.All : QueryMask.All.Ignoring(shooterTag);
            Vec3 side = Vec3.Cross(Math.Abs(fwd.Y) < 0.99f ? Vec3.Up : Vec3.Right, fwd).Normalized;
            Vec3 up = Vec3.Cross(fwd, side);
            float cosMax = (float)Math.Cos(Math.Max(0f, spreadDeg) * MathUtil.Deg2Rad);
            r.Rays = stats.Pellets;
            for (int i = 0; i < stats.Pellets; i++)
            {
                float u = world.Rng.NextFloat(), v = world.Rng.NextFloat();
                float cosT = 1f - u * (1f - cosMax);
                float sinT = (float)Math.Sqrt(Math.Max(0f, 1f - cosT * cosT));
                double phi = 2.0 * Math.PI * v;
                Vec3 dir = fwd * cosT + (side * (float)Math.Cos(phi) + up * (float)Math.Sin(phi)) * sinT;
                Ray(world, shooter, shooterTag, stats, origin, dir, mask, i == 0, ref r);
            }
            world.Emit(GameEventType.Gunshot, origin, shooterTag, 0, GunshotNoiseRadius, null);
            if (shooter != null) shooter.Anim = PedAnim.Shoot;
            return r;
        }

        /// <summary>
        /// One swing of melee <paramref name="weapon"/> by <paramref name="attacker"/>. Candidates: Game peds (not the
        /// attacker, not Dead, InVehicle or Invisible) and ambient snapshot citizens (not while inside an interior) whose feet
        /// are within the weapon's reach horizontally (XZ distance ≤ Range), within <see cref="MeleeMaxHeightGap"/>
        /// vertically, and within ±35° of the attacker's <c>YawDeg</c>. The nearest wins (ties: lower tag). An ambient
        /// victim is promoted (<c>PromoteCitizen(entry, PromotedCitizen, attacker tag)</c>). Damage = the weapon's, knock-down
        /// when <c>world.Rng.Chance(KnockDownChance)</c> (drawn only on a hit), through <see cref="Damage.ApplyToPed"/>
        /// (Melee). Emits <see cref="GameEventType.Punched"/> (A = attacker tag, B = victim ped tag, Value = weapon damage,
        /// Text = weapon name, Position = victim feet). A miss emits nothing. Sets the attacker's Anim to Punch.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="weapon"/> is a firearm.</exception>
        public static MeleeResult Melee(GameWorld world, Ped attacker, WeaponId weapon)
        {
            WeaponStats stats = WeaponTable.Get(weapon);
            if (stats.IsFirearm) throw new ArgumentException("not a melee weapon", "weapon");
            attacker.Anim = PedAnim.Punch;
            var r = default(MeleeResult);
            float best = float.MaxValue;
            int bestTag = 0;
            Ped bestPed = null;
            AmbientCitizen bestCitizen = default(AmbientCitizen);
            IList<Ped> peds = world.Peds;
            for (int i = 0; i < peds.Count; i++)
            {
                Ped p = peds[i];
                if (p == attacker || p.IsDead || p.Has(PedFlags.InVehicle) || p.Has(PedFlags.Invisible)) continue;
                float d;
                if (!InArc(attacker, p.Position, stats.Range, out d)) continue;
                if (d < best || (d == best && p.Tag < bestTag)) { best = d; bestTag = p.Tag; bestPed = p; }
            }
            if (!world.Interior.IsInside)
            {
                List<AmbientCitizen> cs = world.Ambient.Citizens;
                for (int i = 0; i < cs.Count; i++)
                {
                    AmbientCitizen c = cs[i];
                    float d;
                    if (!InArc(attacker, c.Position, stats.Range, out d) || world.FindPedByCitizen(c.Citizen) != null) continue;
                    int tag = WorldTag.ForAmbientCitizen(c.Instance);
                    if (d < best || (d == best && tag < bestTag)) { best = d; bestTag = tag; bestPed = null; bestCitizen = c; }
                }
            }
            if (bestTag == 0) return r;
            Ped victim = bestPed ?? world.PromoteCitizen(bestCitizen, PedOrigin.PromotedCitizen, attacker.Tag);
            if (victim == null) return r;
            bool knock = world.Rng.Chance(stats.KnockDownChance);
            world.Emit(GameEventType.Punched, victim.Position, attacker.Tag, victim.Tag, stats.Damage, stats.Name);
            r.Hit = true;
            r.VictimTag = victim.Tag;
            r.Damage = stats.Damage;
            r.Outcome = Damage.ApplyToPed(world, victim, stats.Damage, attacker.Tag, DamageKind.Melee, knock, 0f);
            return r;
        }

        private static bool InArc(Ped attacker, Vec3 feet, float reach, out float d)
        {
            Vec3 delta = feet - attacker.Position;
            d = delta.LengthXZ;
            if (d > reach || Math.Abs(delta.Y) > MeleeMaxHeightGap) return false;
            if (d < 1e-4f) return true;
            return Math.Abs(MathUtil.DeltaAngleDeg(attacker.YawDeg, Vec3.YawDegOf(delta))) <= MeleeArcDeg * 0.5f;
        }

        private static void Ray(GameWorld world, Ped shooter, int shooterTag, WeaponStats stats, Vec3 origin, Vec3 dir,
            QueryMask mask, bool first, ref ShotResult r)
        {
            PhysicsHit hit;
            Vec3 end = origin + dir * stats.Range;
            ShotHitKind kind = ShotHitKind.Nothing;
            int hitTag = 0;
            float dealt = 0f;
            if (world.Collision.Raycast(origin, dir, stats.Range, mask, out hit))
            {
                end = hit.Point;
                kind = ShotHitKind.Static;
                if (WorldTag.IsPerson(hit.Tag))
                {
                    Ped p = world.PedFromTag(hit.Tag, true, shooterTag);
                    if (p != null)
                    {
                        kind = ShotHitKind.Person;
                        hitTag = p.Tag;
                        float dmg = stats.Damage;
                        if (Damage.IsHeadshot(p, hit.Point))
                        {
                            dmg *= Damage.HeadshotMultiplier;
                            r.Headshots++;
                        }
                        DamageResult o = Damage.ApplyToPed(world, p, dmg, shooterTag, DamageKind.Bullet, false, 0f);
                        dealt = o.HealthLost + o.ArmourLost;
                        r.PersonDamage += dealt;
                        if (o.Killed) r.Killed = true;
                    }
                }
                else if (WorldTag.IsVehicle(hit.Tag))
                {
                    Car c = world.CarFromTag(hit.Tag, true, shooterTag);
                    if (c != null)
                    {
                        kind = ShotHitKind.Vehicle;
                        hitTag = c.Tag;
                        float before = c.Body.Health;
                        world.DamageCar(c, stats.Damage * Damage.CarBulletFactor, shooterTag, hit.Point);
                        dealt = before - c.Body.Health;
                        r.CarDamage += dealt;
                    }
                }
            }
            if (first)
            {
                r.FirstHitKind = kind;
                r.FirstEndPoint = end;
            }
            if (r.FirstHitTag == 0) r.FirstHitTag = hitTag;
            world.Emit(GameEventType.Shot, origin, end, shooterTag, hitTag, dealt);
        }
    }
}
