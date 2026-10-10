using System;
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
        /// <summary>Total dollars spent or taken as fines (incremented by <see cref="Wallet.TrySpend"/> and <see cref="Wallet.Fine"/>).</summary>
        public const string MoneySpent = "money_spent";
    }

    /// <summary>
    /// Everything persisted about the player (DESIGN F-SAVE-1). Plain data plus invariant-keeping mutators; no
    /// randomness, no time source. A new profile has the defaults documented on each member.
    /// </summary>
    public sealed class PlayerProfile : IVitals
    {
        /// <summary>Cash a brand-new profile starts with (dollars).</summary>
        public const long StartCash = 500;
        /// <summary>Maximum cash; <see cref="AddCash"/> saturates here.</summary>
        public const long MaxCash = 999999999L;
        /// <summary>Maximum health and armour.</summary>
        public const int MaxVital = 100;
        /// <summary>Maximum heat value. Project convention: heat runs 0-100 and 20 heat is one star-equivalent.</summary>
        public const float MaxHeat = 100f;
        /// <summary>Heat per star-equivalent (20), the project-wide heat scale.</summary>
        public const float HeatPerStar = 20f;

        private int health = MaxVital;
        private int armour;
        private float heat;

        /// <summary>Cash in dollars, 0..<see cref="MaxCash"/>. Default <see cref="StartCash"/>. Never negative.</summary>
        public long Cash { get; private set; }
        /// <summary>Health 0..100, default 100. Setter clamps.</summary>
        public int Health { get { return health; } set { health = ClampInt(value, 0, MaxVital); } }
        /// <summary>Armour 0..100, default 0. Setter clamps.</summary>
        public int Armour { get { return armour; } set { armour = ClampInt(value, 0, MaxVital); } }

        float IVitals.Health { get { return Health; } set { Health = (int)Math.Round(value); } }

        float IVitals.Armour { get { return Armour; } set { Armour = (int)Math.Round(value); } }
        /// <summary>Outfit as a citizen model id; 0 (default) is the starting outfit. Must be &gt;= 0.</summary>
        public int OutfitId { get; set; }
        /// <summary>Building id of the last safehouse (spawn/save point); 0 (default) means none. Must be 0 or in <see cref="Properties"/>.</summary>
        public uint LastSafehouse { get; set; }
        /// <summary>Position in the main campaign, &gt;= 0, default 0.</summary>
        public int CampaignStep { get; set; }
        /// <summary>Player heat 0..<see cref="MaxHeat"/> (20 per star-equivalent), default 0. Setter clamps; NaN becomes 0.</summary>
        public float Heat { get { return heat; } set { heat = ClampHeat(value); } }
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
            Properties = new List<uint>();
            CompletedMissions = new List<string>();
            SideJobLevels = new Dictionary<string, int>();
            Stats = new Dictionary<string, long>();
            DistrictHeat = new Dictionary<int, float>();
            OwnedWeapons = new bool[WeaponInfo.Count];
            OwnedWeapons[0] = true;
            AmmoByWeapon = new int[WeaponInfo.Count];
        }

        private static int ClampInt(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }

        private static float ClampHeat(float v)
        {
            if (float.IsNaN(v) || v < 0f) return 0f;
            return v > MaxHeat ? MaxHeat : v;
        }

        /// <summary>Sets cash directly, clamped to 0..<see cref="MaxCash"/>. For the codec.</summary>
        internal void SetCash(long value) { Cash = value < 0 ? 0 : (value > MaxCash ? MaxCash : value); }

        /// <summary>Adds <paramref name="amount"/> dollars (negative is ignored), saturating at <see cref="MaxCash"/>. Returns the amount actually added.</summary>
        public long AddCash(long amount)
        {
            if (amount <= 0) return 0;
            long added = Math.Min(amount, MaxCash - Cash);
            Cash += added;
            return added;
        }

        /// <summary>
        /// Removes up to <paramref name="amount"/> dollars (a fine): takes everything if cash is lower, never goes negative.
        /// Negative amount is ignored. Returns the amount actually taken.
        /// </summary>
        public long TakeCash(long amount)
        {
            if (amount <= 0) return 0;
            long taken = Math.Min(amount, Cash);
            Cash -= taken;
            return taken;
        }

        /// <summary>Removes exactly <paramref name="amount"/> dollars if cash &gt;= amount and returns true; otherwise changes nothing and returns false. Amount &lt;= 0 returns false.</summary>
        public bool TrySpend(long amount)
        {
            if (amount <= 0 || amount > Cash) return false;
            Cash -= amount;
            return true;
        }

        /// <summary>True if the weapon is owned. <see cref="WeaponId.Fists"/> is always owned.</summary>
        public bool OwnsWeapon(WeaponId w) { return OwnedWeapons[(int)w]; }

        /// <summary>Marks the weapon owned (no ammo change). Idempotent.</summary>
        public void GiveWeapon(WeaponId w) { OwnedWeapons[(int)w] = true; }

        /// <summary>Rounds carried for the weapon; 0 if not owned or it uses no ammo.</summary>
        public int GetAmmo(WeaponId w) { return OwnedWeapons[(int)w] ? AmmoByWeapon[(int)w] : 0; }

        /// <summary>Sets ammo, clamped to 0..<see cref="WeaponInfo.MaxAmmo"/>. Ignored if the weapon is not owned.</summary>
        public void SetAmmo(WeaponId w, int rounds)
        {
            int i = (int)w;
            if (!OwnedWeapons[i]) return;
            AmmoByWeapon[i] = ClampInt(rounds, 0, WeaponInfo.MaxAmmo[i]);
        }

        /// <summary>Adds rounds up to the maximum. Returns rounds actually added (0 if not owned, no ammo type, full, or <paramref name="rounds"/> &lt;= 0).</summary>
        public int AddAmmo(WeaponId w, int rounds)
        {
            int i = (int)w;
            if (!OwnedWeapons[i] || rounds <= 0) return 0;
            int added = Math.Min(rounds, WeaponInfo.MaxAmmo[i] - AmmoByWeapon[i]);
            AmmoByWeapon[i] += added;
            return added;
        }

        /// <summary>Removes all weapons except Fists and zeroes all ammo (BUSTED).</summary>
        public void StripWeapons()
        {
            for (int i = 1; i < WeaponInfo.Count; i++) OwnedWeapons[i] = false;
            ClearAmmo();
        }

        /// <summary>Sets ammo of every owned weapon to 0 but keeps the weapons (WASTED, F-CMB-4).</summary>
        public void ClearAmmo()
        {
            for (int i = 0; i < WeaponInfo.Count; i++) AmmoByWeapon[i] = 0;
        }

        /// <summary>True if the building id is an owned property.</summary>
        public bool OwnsProperty(uint buildingId) { return Properties.Contains(buildingId); }

        /// <summary>True if <see cref="CompletedMissions"/> contains the id (ordinal, case-sensitive).</summary>
        public bool IsMissionComplete(string missionId) { return missionId != null && CompletedMissions.Contains(missionId); }

        /// <summary>Adds the id to <see cref="CompletedMissions"/> if absent and non-empty. Returns true if it was added.</summary>
        public bool CompleteMission(string missionId)
        {
            if (string.IsNullOrEmpty(missionId) || CompletedMissions.Contains(missionId)) return false;
            CompletedMissions.Add(missionId);
            return true;
        }

        /// <summary>Side-job level, 0 if absent.</summary>
        public int GetSideJobLevel(string jobId)
        {
            int v;
            return jobId != null && SideJobLevels.TryGetValue(jobId, out v) ? v : 0;
        }

        /// <summary>Stat counter, 0 if absent.</summary>
        public long GetStat(string name)
        {
            long v;
            return name != null && Stats.TryGetValue(name, out v) ? v : 0;
        }

        /// <summary>Adds <paramref name="delta"/> to a stat; the counter never drops below 0 and saturates at long.MaxValue.</summary>
        public void AddStat(string name, long delta)
        {
            if (name == null) return;
            long cur = GetStat(name);
            long next;
            if (delta > 0) next = cur > long.MaxValue - delta ? long.MaxValue : cur + delta;
            else next = cur + delta < 0 ? 0 : cur + delta;
            Stats[name] = next;
        }

        /// <summary>Heat of a district, 0 if absent.</summary>
        public float GetDistrictHeat(int districtId)
        {
            float v;
            return DistrictHeat.TryGetValue(districtId, out v) ? v : 0f;
        }

        /// <summary>Star-equivalents of <see cref="Heat"/>: ceil(Heat / <see cref="HeatPerStar"/>), 0 when Heat is 0, max 5.</summary>
        public int HeatStars
        {
            get
            {
                if (heat <= 0f) return 0;
                return Math.Min(5, (int)Math.Ceiling(heat / (double)HeatPerStar));
            }
        }

        /// <summary>Restores every invariant documented on the members (clamps ranges, drops duplicates and empty ids, removes non-positive levels and negative stats, drops LastSafehouse if not owned, Fists owned, no ammo for unowned weapons). Used after decoding.</summary>
        public void Normalise()
        {
            health = ClampInt(health, 0, MaxVital);
            armour = ClampInt(armour, 0, MaxVital);
            heat = ClampHeat(heat);
            if (Cash < 0) Cash = 0;
            if (Cash > MaxCash) Cash = MaxCash;
            if (OutfitId < 0) OutfitId = 0;
            if (CampaignStep < 0) CampaignStep = 0;
            if (double.IsNaN(PlayTimeSeconds) || PlayTimeSeconds < 0) PlayTimeSeconds = 0;

            OwnedWeapons[0] = true;
            for (int i = 0; i < WeaponInfo.Count; i++)
                AmmoByWeapon[i] = OwnedWeapons[i] ? ClampInt(AmmoByWeapon[i], 0, WeaponInfo.MaxAmmo[i]) : 0;

            var props = new List<uint>();
            for (int i = 0; i < Properties.Count; i++)
                if (Properties[i] != 0 && !props.Contains(Properties[i])) props.Add(Properties[i]);
            Properties.Clear();
            for (int i = 0; i < props.Count; i++) Properties.Add(props[i]);
            if (LastSafehouse != 0 && !Properties.Contains(LastSafehouse)) LastSafehouse = 0;

            var missions = new List<string>();
            for (int i = 0; i < CompletedMissions.Count; i++)
                if (!string.IsNullOrEmpty(CompletedMissions[i]) && !missions.Contains(CompletedMissions[i])) missions.Add(CompletedMissions[i]);
            CompletedMissions.Clear();
            for (int i = 0; i < missions.Count; i++) CompletedMissions.Add(missions[i]);

            foreach (string k in new List<string>(SideJobLevels.Keys))
                if (SideJobLevels[k] < 1) SideJobLevels.Remove(k);
            foreach (string k in new List<string>(Stats.Keys))
                if (Stats[k] < 0) Stats.Remove(k);
            foreach (int k in new List<int>(DistrictHeat.Keys))
                DistrictHeat[k] = ClampHeat(DistrictHeat[k]);
        }
    }
}
