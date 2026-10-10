using System;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Combat
{
    /// <summary>What dealt the damage.</summary>
    public enum DamageKind
    {
        /// <summary>A firearm ray.</summary>
        Bullet,
        /// <summary>A fist or bat.</summary>
        Melee,
        /// <summary>A car hitting a person.</summary>
        Vehicle,
        /// <summary>An exploding car.</summary>
        Explosion,
        /// <summary>Anything else (scripted).</summary>
        Other,
    }

    /// <summary>What one <see cref="Damage.ApplyToPed"/> call did.</summary>
    public struct DamageResult
    {
        /// <summary>Health removed (0..previous health).</summary>
        public float HealthLost;
        /// <summary>Armour removed.</summary>
        public float ArmourLost;
        /// <summary>The ped died from this call (it was alive before).</summary>
        public bool Killed;
        /// <summary>The ped became Injured in this call (first time below 35, not killed).</summary>
        public bool BecameInjured;
        /// <summary>The ped was knocked down by this call.</summary>
        public bool KnockedDown;
    }

    /// <summary>
    /// The damage model (G7 contract "Damage" and "Consequences"). Every member is deterministic; nothing allocates.
    /// <para>
    /// Consequence rules shared by every damage path (<see cref="ApplyToPed"/>):
    /// <list type="bullet">
    /// <item>Death (health reaches 0): Health = 0; for a non-player ped also flag Dead (KnockedDown, HandsUp, OnPhone,
    /// Panicking, Hostile, Aiming cleared), Anim LieDown, Move zero; a running <c>CallPoliceBrain</c> is interrupted first
    /// (CallInterrupted). Emits <see cref="GameEventType.PedKilled"/> (A = cause, B = ped tag, Position = ped feet) for
    /// every ped including the player (G6's Respawn system then runs the player's death flow and owns the player's Dead
    /// flag). A non-player ped with <c>Citizen</c> ≠ 0 emits <c>CityEffect.KillCitizen(citizen, 0)</c> exactly once
    /// (building 0 = their home); the Dead flag guarantees once, since dead peds take no more damage.</item>
    /// <item>Injury: alive, health &lt; <see cref="InjuryHealth"/> after the hit and not yet Injured → flag Injured (never
    /// cleared), <see cref="GameEventType.PedInjured"/> (A = cause, B = ped), and for a non-player ped with a Citizen
    /// <c>CityEffect.InjureCitizen(citizen, 0)</c> exactly once.</item>
    /// <item>Knock-down (requested by the caller, ped alive and not already down): flag KnockedDown,
    /// <see cref="GameEventType.PedKnockedDown"/> (A = cause, B = ped, Value = impact speed for vehicles else 0). A
    /// non-player ped whose brain implements <c>Peds.IKnockDownHandler</c> gets <c>OnKnockedDown</c>; otherwise (null or a
    /// G7 brain) its brain becomes <c>Peds.KnockedDownBrain</c> (a running CallPoliceBrain is interrupted first, traits
    /// kept via <c>PedBrainFactory.TraitsOf</c>); the player keeps no brain and <see cref="CombatSystem"/> clears his flag
    /// after <see cref="PlayerKnockDownSeconds"/>.</item>
    /// </list>
    /// Order within one call: armour split, health, LastDamageTime/LastAttackerTag, death or else (injury, then knock-down).
    /// </para>
    /// </summary>
    public static class Damage
    {
        /// <summary>Share of each hit armour absorbs while it lasts (0.66).</summary>
        public const float ArmourAbsorb = 0.66f;
        /// <summary>Headshot multiplier (2.5).</summary>
        public const float HeadshotMultiplier = 2.5f;
        /// <summary>Hit height above the feet (m) above which a hit on a standing ped is a headshot (1.5).</summary>
        public const float HeadshotHeight = 1.5f;
        /// <summary>Health below which a ped is injured (35).</summary>
        public const float InjuryHealth = 35f;
        /// <summary>The player regenerates up to this health (50).</summary>
        public const float RegenCap = 50f;
        /// <summary>Seconds without damage before regeneration starts (5).</summary>
        public const float RegenDelaySeconds = 5f;
        /// <summary>Regeneration rate, health per second (2).</summary>
        public const float RegenPerSecond = 2f;
        /// <summary>Relative speed (m/s) above which a car knocks a person down (4).</summary>
        public const float VehicleKnockDownSpeed = 4f;
        /// <summary>Health damage per m/s above <see cref="VehicleKnockDownSpeed"/> (7).</summary>
        public const float VehicleDamagePerSpeed = 7f;
        /// <summary>Relative speed (m/s) above which a car kills outright, armour ignored (14).</summary>
        public const float VehicleKillSpeed = 14f;
        /// <summary>Explosion damage radius, metres (8).</summary>
        public const float ExplosionDamageRadius = 8f;
        /// <summary>Explosion knock-down radius, metres (12).</summary>
        public const float ExplosionKnockDownRadius = 12f;
        /// <summary>Explosion damage at the centre; falls off linearly to 0 at <see cref="ExplosionDamageRadius"/> (100).</summary>
        public const float ExplosionMaxDamage = 100f;
        /// <summary>Car health lost per point of bullet damage (car health is 0..100): 0.25.</summary>
        public const float CarBulletFactor = 0.25f;
        /// <summary>Seconds the player stays knocked down (1.5).</summary>
        public const float PlayerKnockDownSeconds = 1.5f;

        /// <summary>
        /// Armour split: absorbed = min(<paramref name="amount"/> × 0.66, <paramref name="armour"/>);
        /// <paramref name="armourLoss"/> = absorbed; returns the health loss = amount − absorbed. Amount ≤ 0 → 0, 0.
        /// Example: 30 with 100 armour → health 10.2, armour 19.8; 30 with 5 armour → health 25, armour 5.
        /// </summary>
        public static float SplitArmour(float amount, float armour, out float armourLoss)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// True when a hit at <paramref name="hitPoint"/> is a headshot: the victim is on foot, not KnockedDown and not
        /// Dead, and hitPoint.Y − victim feet Y &gt; <see cref="HeadshotHeight"/>.
        /// </summary>
        public static bool IsHeadshot(Ped victim, Vec3 hitPoint)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Damages a Game ped: no-op (default result) when <paramref name="victim"/> is null or Dead or
        /// <paramref name="amount"/> ≤ 0. Applies <see cref="SplitArmour"/>, subtracts the health loss (clamped at 0), sets
        /// <c>LastDamageTime</c> = <c>Clock.GameSeconds</c> and <c>LastAttackerTag</c> = <paramref name="causeTag"/>, then the
        /// consequences on the class. <paramref name="knockDown"/> requests a knock-down; <paramref name="impactSpeed"/> is
        /// the Value of PedKnockedDown. <paramref name="kind"/> is informational (it does not change the maths).
        /// </summary>
        public static DamageResult ApplyToPed(GameWorld world, Ped victim, float amount, int causeTag, DamageKind kind,
            bool knockDown, float impactSpeed)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Kills a living ped outright (armour ignored): health → 0 and the death consequences of the class. Used for
        /// vehicle hits above <see cref="VehicleKillSpeed"/>. No-op on null or Dead.
        /// </summary>
        public static DamageResult Kill(GameWorld world, Ped victim, int causeTag, DamageKind kind)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// A car (<paramref name="carTag"/>) hit a person (<paramref name="personTag"/>: Game ped or ambient citizen) at
        /// relative speed <paramref name="speed"/> m/s (G6's PedHitByVehicle). speed ≤ 4 → nothing (default result, no
        /// promotion). Otherwise the ambient citizen is promoted (<c>PedFromTag(tag, true, carTag)</c>); InVehicle or Dead
        /// peds are skipped; speed &gt; 14 → <see cref="Kill"/> plus a PedKnockedDown event (Value = speed) before
        /// PedKilled; 4 &lt; speed ≤ 14 → <see cref="ApplyToPed"/>((speed − 4) × 7, Vehicle, knockDown true, speed).
        /// Examples: 6 m/s → 14 damage and down; 14 m/s → 70 damage and down; 14.5 m/s → dead.
        /// </summary>
        public static DamageResult HitByVehicle(GameWorld world, int personTag, int carTag, float speed)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// An explosion at <paramref name="centre"/>: every Game ped on foot (not InVehicle, Invisible or Dead) and every
        /// ambient snapshot citizen whose feet are within <see cref="ExplosionKnockDownRadius"/> (3D distance d) is affected
        /// in <c>world.Peds</c> order, then snapshot order. Ambient ones are promoted first (cause =
        /// <paramref name="causeTag"/>). d &lt; 8 → <see cref="ApplyToPed"/>(100 × (1 − d / 8), Explosion, knockDown true);
        /// 8 ≤ d &lt; 12 → knock-down only (no damage). No line-of-sight test. Peds inside cars are G6's concern.
        /// Returns the number of persons affected.
        /// </summary>
        public static int Explode(GameWorld world, Vec3 centre, int causeTag)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Player regeneration for one step: when 0 &lt; Health &lt; 50 and (LastDamageTime &lt; 0 or
        /// <paramref name="now"/> − LastDamageTime ≥ 5) → Health = min(50, Health + 2 × <paramref name="dt"/>). Never
        /// lowers health above 50. Example: damaged to 20 at t = 0 → still 20 at t = 4.99, 22 at t = 6, 50 at t = 20.
        /// </summary>
        public static void Regenerate(Ped player, double now, float dt)
        {
            throw new NotImplementedException();
        }
    }
}
