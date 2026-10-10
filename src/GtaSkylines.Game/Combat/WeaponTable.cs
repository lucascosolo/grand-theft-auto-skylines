using System;
using GtaSkylines.Game.Profile;

namespace GtaSkylines.Game.Combat
{
    /// <summary>How a weapon attacks.</summary>
    public enum WeaponClass
    {
        /// <summary>Swings at the nearest person in a 70° arc within <see cref="WeaponStats.Range"/> (the reach).</summary>
        Melee,
        /// <summary>Hitscan: <see cref="WeaponStats.Pellets"/> rays per trigger pull, each up to <see cref="WeaponStats.Range"/>.</summary>
        Firearm,
    }

    /// <summary>One weapon's constants (DESIGN F-CMB-1, G7 contract). Immutable.</summary>
    public sealed class WeaponStats
    {
        internal WeaponStats(WeaponId id, string name, WeaponClass cls, float damage, int pellets, float range,
            float ratePerSecond, int magazine, float reloadSeconds, float spreadDeg, float knockDownChance)
        {
            Id = id;
            Name = name;
            Class = cls;
            Damage = damage;
            Pellets = pellets;
            Range = range;
            RatePerSecond = ratePerSecond;
            Magazine = magazine;
            ReloadSeconds = reloadSeconds;
            SpreadDeg = spreadDeg;
            KnockDownChance = knockDownChance;
        }

        /// <summary>Which weapon.</summary>
        public readonly WeaponId Id;
        /// <summary>Display name ("Pistol"); also the <c>Text</c> of Punched events. A constant string.</summary>
        public readonly string Name;
        /// <summary>Melee or firearm.</summary>
        public readonly WeaponClass Class;
        /// <summary>Health damage per hit (per pellet for the shotgun), before armour and the headshot multiplier.</summary>
        public readonly float Damage;
        /// <summary>Rays per trigger pull: 8 for the shotgun, 1 for other firearms, 0 for melee.</summary>
        public readonly int Pellets;
        /// <summary>Hitscan range in metres (firearms) or reach in metres (melee).</summary>
        public readonly float Range;
        /// <summary>Attacks per second (shots or swings); the minimum interval between attacks is 1 / rate.</summary>
        public readonly float RatePerSecond;
        /// <summary>Rounds per magazine; 0 for melee.</summary>
        public readonly int Magazine;
        /// <summary>Seconds to reload a magazine; 0 for melee.</summary>
        public readonly float ReloadSeconds;
        /// <summary>Base spread: half-angle in degrees of the cone each ray is drawn from (uniform over the cone's solid angle); 0 for melee.</summary>
        public readonly float SpreadDeg;
        /// <summary>Probability 0..1 that a melee hit knocks the victim down (fists 0.25, bat 0.70); 0 for firearms.</summary>
        public readonly float KnockDownChance;

        /// <summary>Seconds between attacks: 1 / <see cref="RatePerSecond"/>.</summary>
        public float Interval { get { return 1f / RatePerSecond; } }

        /// <summary>True for firearms.</summary>
        public bool IsFirearm { get { return Class == WeaponClass.Firearm; } }
    }

    /// <summary>
    /// The real weapon values of the G7 contract, indexed by <see cref="WeaponId"/>:
    /// <list type="table">
    /// <item><term>Fists</term><description>melee 8 dmg, reach 1.2 m, 2.2 swings/s, knock-down 25 %</description></item>
    /// <item><term>Bat</term><description>melee 26 dmg, reach 1.7 m, 1.4 swings/s, knock-down 70 %</description></item>
    /// <item><term>Pistol</term><description>26 dmg, 70 m, 3.5/s, magazine 12, reload 1.4 s, spread 1.2°</description></item>
    /// <item><term>Smg</term><description>14 dmg, 45 m, 12/s, magazine 30, reload 2.0 s, spread 3.0°</description></item>
    /// <item><term>Shotgun</term><description>8 pellets × 11 dmg, 22 m, 1.1/s, magazine 8, reload 2.8 s, spread 6°</description></item>
    /// <item><term>Carbine</term><description>30 dmg, 100 m, 8/s, magazine 30, reload 2.2 s, spread 1.0°</description></item>
    /// </list>
    /// </summary>
    public static class WeaponTable
    {
        private static readonly WeaponStats[] All =
        {
            new WeaponStats(WeaponId.Fists, "Fists", WeaponClass.Melee, 8f, 0, 1.2f, 2.2f, 0, 0f, 0f, 0.25f),
            new WeaponStats(WeaponId.Bat, "Bat", WeaponClass.Melee, 26f, 0, 1.7f, 1.4f, 0, 0f, 0f, 0.70f),
            new WeaponStats(WeaponId.Pistol, "Pistol", WeaponClass.Firearm, 26f, 1, 70f, 3.5f, 12, 1.4f, 1.2f, 0f),
            new WeaponStats(WeaponId.Smg, "SMG", WeaponClass.Firearm, 14f, 1, 45f, 12f, 30, 2.0f, 3.0f, 0f),
            new WeaponStats(WeaponId.Shotgun, "Shotgun", WeaponClass.Firearm, 11f, 8, 22f, 1.1f, 8, 2.8f, 6f, 0f),
            new WeaponStats(WeaponId.Carbine, "Carbine", WeaponClass.Firearm, 30f, 1, 100f, 8f, 30, 2.2f, 1.0f, 0f),
        };

        /// <summary>Spread multiplier while the shooter moves faster than <see cref="MovingSpeed"/>.</summary>
        public const float MovingSpreadFactor = 2f;
        /// <summary>Spread multiplier while aiming (Aiming flag) and not moving.</summary>
        public const float AimingStillSpreadFactor = 0.5f;
        /// <summary>Horizontal speed (m/s) above which a shooter counts as moving.</summary>
        public const float MovingSpeed = 0.5f;

        /// <summary>The stats of <paramref name="id"/>.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is not a defined weapon.</exception>
        public static WeaponStats Get(WeaponId id)
        {
            int i = (int)id;
            if (i < 0 || i >= All.Length) throw new ArgumentOutOfRangeException("id");
            return All[i];
        }

        /// <summary>
        /// Effective spread half-angle in degrees: base × 2 when <paramref name="moving"/>, else base × 0.5 when
        /// <paramref name="aiming"/>, else base. Moving wins over aiming. 0 for melee.
        /// </summary>
        public static float EffectiveSpreadDeg(WeaponId id, bool moving, bool aiming)
        {
            float s = Get(id).SpreadDeg;
            if (moving) return s * MovingSpreadFactor;
            return aiming ? s * AimingStillSpreadFactor : s;
        }
    }
}
