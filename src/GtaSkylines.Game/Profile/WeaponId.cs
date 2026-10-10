namespace GtaSkylines.Game.Profile
{
    /// <summary>The six weapons of DESIGN F-CMB-1. Numeric values are persisted in the profile record and never change.</summary>
    public enum WeaponId
    {
        /// <summary>Bare hands. Always owned, no ammo, cannot be bought or removed.</summary>
        Fists = 0,
        /// <summary>Melee, no ammo.</summary>
        Bat = 1,
        /// <summary>Pistol.</summary>
        Pistol = 2,
        /// <summary>Submachine gun.</summary>
        Smg = 3,
        /// <summary>Shotgun.</summary>
        Shotgun = 4,
        /// <summary>Carbine rifle.</summary>
        Carbine = 5,
    }

    /// <summary>Per-weapon constants. All arrays are indexed by <see cref="WeaponId"/>.</summary>
    public static class WeaponInfo
    {
        /// <summary>Number of weapons (6).</summary>
        public const int Count = 6;

        /// <summary>Maximum carried rounds: Fists 0, Bat 0, Pistol 250, Smg 400, Shotgun 80, Carbine 300.</summary>
        public static readonly int[] MaxAmmo = { 0, 0, 250, 400, 80, 300 };

        /// <summary>Rounds granted with the weapon when it is bought: 0, 0, 36, 90, 16, 90.</summary>
        public static readonly int[] StartAmmo = { 0, 0, 36, 90, 16, 90 };

        /// <summary>True for weapons that use ammo (Pistol, Smg, Shotgun, Carbine).</summary>
        public static bool UsesAmmo(WeaponId w) { return MaxAmmo[(int)w] > 0; }
    }
}
