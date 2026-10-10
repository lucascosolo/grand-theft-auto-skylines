using System.Collections.Generic;

namespace GtaSkylines.Game.Profile
{
    /// <summary>Names of the standard stat counters (DESIGN F-ECO-3). Any other name is allowed too.</summary>
    public static class StatNames
    {
        /// <summary>Distance travelled in metres.</summary>
        public const string DistanceM = "distance_m";
        /// <summary>Vehicles stolen.</summary>
        public const string VehiclesStolen = "vehicles_stolen";
        /// <summary>Crimes committed.</summary>
        public const string Crimes = "crimes";
        /// <summary>Times arrested (BUSTED).</summary>
        public const string Arrests = "arrests";
        /// <summary>Times killed (WASTED).</summary>
        public const string Deaths = "deaths";
        /// <summary>Jobs and missions completed.</summary>
        public const string JobsDone = "jobs_done";
        /// <summary>Total dollars earned (incremented by <see cref="Wallet.Earn"/>).</summary>
        public const string MoneyEarned = "money_earned";
    }

    /// <summary>
    /// Everything persisted about the player (DESIGN F-SAVE-1). Plain data plus invariant-keeping mutators; no
    /// randomness, no time source. A new profile has the defaults documented on each member.
    /// </summary>
    public sealed class PlayerProfile
    {
        /// <summary>Cash a brand-new profile starts with (dollars).</summary>
        public const long StartCash = 500;
        /// <summary>Maximum cash; <see cref="AddCash"/> saturates here.</summary>
        public const long MaxCash = 999999999L;
        /// <summary>Maximum health and armour.</summary>
        public const int MaxVital = 100;
        /// <summary>Maximum heat value.</summary>
        public const float MaxHeat = 100f;
        /// <summary>Heat per star-equivalent (20).</summary>
        public const float HeatPerStar = 20f;

        /// <summary>Cash in dollars, 0..<see cref="MaxCash"/>. Default <see cref="StartCash"/>. Never negative.</summary>
        public long Cash { get; private set; }
        /// <summary>Health 0..100, default 100. Setter clamps.</summary>
        public int Health { get; set; }
        /// <summary>Armour 0..100, default 0. Setter clamps.</summary>
        public int Armour { get; set; }
        /// <summary>Outfit as a citizen model id; 0 (default) is the starting outfit. Must be &gt;= 0.</summary>
        public int OutfitId { get; set; }
        /// <summary>Building id of the last safehouse (spawn/save point); 0 (default) means none. Must be 0 or in <see cref="Properties"/>.</summary>
        public uint LastSafehouse { get; set; }
        /// <summary>Position in the main campaign, &gt;= 0, default 0.</summary>
        public int CampaignStep { get; set; }
        /// <summary>Player heat 0..<see cref="MaxHeat"/>, default 0. Setter clamps; NaN becomes 0.</summary>
        public float Heat { get; set; }
        /// <summary>Seconds played, &gt;= 0, default 0.</summary>
        public double PlayTimeSeconds { get; set; }
        /// <summary>Owned building ids in purchase order, no duplicates, none 0.</summary>
        public IList<uint> Properties { get; private set; }
        /// <summary>Completed mission ids, no duplicates, none empty.</summary>
        public IList<string> CompletedMissions { get; private set; }
        /// <summary>Side-job id to level (&gt;= 1). Absent means level 0.</summary>
        public IDictionary<string, int> SideJobLevels { get; private set; }
        /// <summary>Stat name to counter (&gt;= 0).</summary>
        public IDictionary<string, long> Stats { get; private set; }
        /// <summary>CS1 district id to heat 0..<see cref="MaxHeat"/>.</summary>
        public IDictionary<int, float> DistrictHeat { get; private set; }

        /// <summary>Raw ownership flags for the codec, index = (int)<see cref="WeaponId"/>, length <see cref="WeaponInfo.Count"/>. Fists is always true. Elsewhere use <see cref="OwnsWeapon"/>.</summary>
        public bool[] OwnedWeapons { get; private set; }
        /// <summary>Raw ammo for the codec, same indexing and length as <see cref="OwnedWeapons"/>. Elsewhere use <see cref="GetAmmo"/>.</summary>
        public int[] AmmoByWeapon { get; private set; }

        /// <summary>Creates a profile with all defaults; owns only <see cref="WeaponId.Fists"/>.</summary>
        public PlayerProfile()
        {
            Cash = StartCash;
            Health = MaxVital;
            Properties = new List<uint>();
            CompletedMissions = new List<string>();
            SideJobLevels = new Dictionary<string, int>();
            Stats = new Dictionary<string, long>();
            DistrictHeat = new Dictionary<int, float>();
            OwnedWeapons = new bool[WeaponInfo.Count];
            OwnedWeapons[0] = true;
            AmmoByWeapon = new int[WeaponInfo.Count];
        }

        /// <summary>Adds <paramref name="amount"/> dollars (negative is ignored), saturating at <see cref="MaxCash"/>. Returns the amount actually added.</summary>
        public long AddCash(long amount) { throw new System.NotImplementedException(); }

        /// <summary>
        /// Removes up to <paramref name="amount"/> dollars (a fine): takes everything if cash is lower, never goes negative.
        /// Negative amount is ignored. Returns the amount actually taken.
        /// </summary>
        public long TakeCash(long amount) { throw new System.NotImplementedException(); }

        /// <summary>Removes exactly <paramref name="amount"/> dollars if cash &gt;= amount and returns true; otherwise changes nothing and returns false. Amount &lt;= 0 returns false.</summary>
        public bool TrySpend(long amount) { throw new System.NotImplementedException(); }

        /// <summary>True if the weapon is owned. <see cref="WeaponId.Fists"/> is always owned.</summary>
        public bool OwnsWeapon(WeaponId w) { throw new System.NotImplementedException(); }

        /// <summary>Marks the weapon owned (no ammo change). Idempotent.</summary>
        public void GiveWeapon(WeaponId w) { throw new System.NotImplementedException(); }

        /// <summary>Rounds carried for the weapon; 0 if not owned or it uses no ammo.</summary>
        public int GetAmmo(WeaponId w) { throw new System.NotImplementedException(); }

        /// <summary>Sets ammo, clamped to 0..<see cref="WeaponInfo.MaxAmmo"/>. Ignored if the weapon is not owned.</summary>
        public void SetAmmo(WeaponId w, int rounds) { throw new System.NotImplementedException(); }

        /// <summary>Adds rounds up to the maximum. Returns rounds actually added (0 if not owned, no ammo type, full, or <paramref name="rounds"/> &lt;= 0).</summary>
        public int AddAmmo(WeaponId w, int rounds) { throw new System.NotImplementedException(); }

        /// <summary>Removes all weapons except Fists and zeroes all ammo (BUSTED).</summary>
        public void StripWeapons() { throw new System.NotImplementedException(); }

        /// <summary>Sets ammo of every owned weapon to 0 but keeps the weapons (WASTED, F-CMB-4).</summary>
        public void ClearAmmo() { throw new System.NotImplementedException(); }

        /// <summary>True if the building id is an owned property.</summary>
        public bool OwnsProperty(uint buildingId) { throw new System.NotImplementedException(); }

        /// <summary>True if <see cref="CompletedMissions"/> contains the id (ordinal, case-sensitive).</summary>
        public bool IsMissionComplete(string missionId) { throw new System.NotImplementedException(); }

        /// <summary>Adds the id to <see cref="CompletedMissions"/> if absent and non-empty. Returns true if it was added.</summary>
        public bool CompleteMission(string missionId) { throw new System.NotImplementedException(); }

        /// <summary>Side-job level, 0 if absent.</summary>
        public int GetSideJobLevel(string jobId) { throw new System.NotImplementedException(); }

        /// <summary>Stat counter, 0 if absent.</summary>
        public long GetStat(string name) { throw new System.NotImplementedException(); }

        /// <summary>Adds <paramref name="delta"/> to a stat; the counter never drops below 0 and saturates at long.MaxValue.</summary>
        public void AddStat(string name, long delta) { throw new System.NotImplementedException(); }

        /// <summary>Heat of a district, 0 if absent.</summary>
        public float GetDistrictHeat(int districtId) { throw new System.NotImplementedException(); }

        /// <summary>Star-equivalents of <see cref="Heat"/>: ceil(Heat / <see cref="HeatPerStar"/>), 0 when Heat is 0, max 5.</summary>
        public int HeatStars { get { throw new System.NotImplementedException(); } }

        /// <summary>Restores every invariant documented on the members (clamps ranges, drops duplicates and empty ids, removes non-positive levels and negative stats, drops LastSafehouse if not owned, Fists owned, no ammo for unowned weapons). Used after decoding.</summary>
        public void Normalise() { throw new System.NotImplementedException(); }
    }
}
