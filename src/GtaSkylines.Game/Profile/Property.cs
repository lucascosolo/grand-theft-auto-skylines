using System.Collections.Generic;

namespace GtaSkylines.Game.Profile
{
    /// <summary>Residential property as safehouses (DESIGN F-ECO-2).</summary>
    public static class Property
    {
        /// <summary>Lowest CS1 building level (1).</summary>
        public const int MinLevel = 1;
        /// <summary>Highest CS1 building level (5).</summary>
        public const int MaxLevel = 5;
        /// <summary>Footprint area is clamped to at least this many square metres before pricing (64).</summary>
        public const float MinAreaM2 = 64f;
        /// <summary>Footprint area is clamped to at most this many square metres before pricing (2500).</summary>
        public const float MaxAreaM2 = 2500f;
        /// <summary>Price per square metre at level 1 (150).</summary>
        public const int PerM2AtLevel1 = 150;
        /// <summary>Price per square metre added per further level (100).</summary>
        public const int PerM2PerLevel = 100;

        /// <summary>
        /// Price in dollars: area = clamp(areaM2, 64, 2500); perM2 = 150 + 100 * (level - 1); price = round(area * perM2 / 500) * 500
        /// (nearest $500, halves up). Level 1, 64 m2 gives 9500; level 5, 2500 m2 gives 1 375 000.
        /// Returns -1 if level is outside 1..5 or areaM2 is NaN or &lt;= 0.
        /// </summary>
        public static long PriceFor(int level, float areaM2) { throw new System.NotImplementedException(); }

        /// <summary>
        /// Buys a building. buildingId 0 or invalid level/area gives InvalidProperty; already owned gives AlreadyOwned;
        /// cash below the price gives InsufficientCash; otherwise charges the wallet (reason "Property"), appends the id to
        /// <see cref="PlayerProfile.Properties"/>, sets <see cref="PlayerProfile.LastSafehouse"/> if it was 0, returns Ok.
        /// Nothing changes on a non-Ok result.
        /// </summary>
        public static PurchaseResult Buy(Wallet wallet, uint buildingId, int level, float areaM2) { throw new System.NotImplementedException(); }

        /// <summary>Sets LastSafehouse if the building is owned; returns false (and changes nothing) otherwise.</summary>
        public static bool SetLastSafehouse(PlayerProfile profile, uint buildingId) { throw new System.NotImplementedException(); }

        /// <summary>The owned safehouse ids in purchase order (new list).</summary>
        public static List<uint> Safehouses(PlayerProfile profile) { throw new System.NotImplementedException(); }
    }
}
