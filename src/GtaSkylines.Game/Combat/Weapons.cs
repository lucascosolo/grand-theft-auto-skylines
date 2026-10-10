using System;
using GtaSkylines.Game.Mathx;
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// Contract form <c>Fire(shooter, origin, direction)</c>: fires <c>shooter.CurrentWeapon</c> with
        /// <see cref="WeaponTable.EffectiveSpreadDeg"/>(weapon, horizontal motor speed &gt; 0.5, Aiming flag). Same as the
        /// full overload otherwise. A melee weapon in hand → default result, nothing emitted.
        /// </summary>
        public static ShotResult Fire(GameWorld world, Ped shooter, Vec3 origin, Vec3 direction)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }
    }
}
