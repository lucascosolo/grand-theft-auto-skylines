using System.Collections.Generic;
using GtaSkylines.Game.Profile;
using Xunit;

namespace GtaSkylines.Game.Tests.Profile
{
    public class PlayerProfileTests
    {
        [Fact]
        public void New_profile_has_documented_defaults()
        {
            var p = new PlayerProfile();
            Assert.Equal(500L, p.Cash);
            Assert.Equal(100, p.Health);
            Assert.Equal(0, p.Armour);
            Assert.Equal(0, p.OutfitId);
            Assert.Equal(0u, p.LastSafehouse);
            Assert.Equal(0f, p.Heat);
            Assert.Equal(0, p.HeatStars);
            Assert.True(p.OwnsWeapon(WeaponId.Fists));
            Assert.False(p.OwnsWeapon(WeaponId.Pistol));
        }

        [Fact]
        public void AddCash_adds_and_ignores_negative()
        {
            var p = new PlayerProfile();
            Assert.Equal(250L, p.AddCash(250));
            Assert.Equal(750L, p.Cash);
            Assert.Equal(0L, p.AddCash(-100));
            Assert.Equal(750L, p.Cash);
        }

        [Fact]
        public void AddCash_saturates_at_max_and_returns_amount_actually_added()
        {
            var p = new PlayerProfile();
            long added = p.AddCash(long.MaxValue);
            Assert.Equal(PlayerProfile.MaxCash, p.Cash);
            Assert.Equal(PlayerProfile.MaxCash - 500, added);
            Assert.Equal(0L, p.AddCash(1));
        }

        [Fact]
        public void TakeCash_takes_what_exists_and_never_goes_negative()
        {
            var p = new PlayerProfile();
            Assert.Equal(200L, p.TakeCash(200));
            Assert.Equal(300L, p.Cash);
            Assert.Equal(300L, p.TakeCash(10000));
            Assert.Equal(0L, p.Cash);
            Assert.Equal(0L, p.TakeCash(5));
            Assert.Equal(0L, p.TakeCash(-5));
            Assert.Equal(0L, p.Cash);
        }

        [Fact]
        public void TrySpend_is_all_or_nothing()
        {
            var p = new PlayerProfile();
            Assert.False(p.TrySpend(501));
            Assert.Equal(500L, p.Cash);
            Assert.True(p.TrySpend(500));
            Assert.Equal(0L, p.Cash);
            Assert.False(p.TrySpend(0));
            Assert.False(p.TrySpend(-1));
        }

        [Fact]
        public void Health_armour_and_heat_setters_clamp()
        {
            var p = new PlayerProfile();
            p.Health = 500; Assert.Equal(100, p.Health);
            p.Health = -5; Assert.Equal(0, p.Health);
            p.Armour = 101; Assert.Equal(100, p.Armour);
            p.Armour = -1; Assert.Equal(0, p.Armour);
            p.Heat = 250f; Assert.Equal(100f, p.Heat);
            p.Heat = -3f; Assert.Equal(0f, p.Heat);
            p.Heat = float.NaN; Assert.Equal(0f, p.Heat);
        }

        [Theory]
        [InlineData(0f, 0)]
        [InlineData(0.1f, 1)]
        [InlineData(20f, 1)]
        [InlineData(20.5f, 2)]
        [InlineData(60f, 3)]
        [InlineData(80f, 4)]
        [InlineData(81f, 5)]
        [InlineData(100f, 5)]
        public void HeatStars_is_ceiling_of_heat_over_20(float heat, int stars)
        {
            var p = new PlayerProfile { Heat = heat };
            Assert.Equal(stars, p.HeatStars);
        }

        [Fact]
        public void Weapon_ownership_and_ammo_clamping()
        {
            var p = new PlayerProfile();
            p.SetAmmo(WeaponId.Pistol, 10);
            Assert.Equal(0, p.GetAmmo(WeaponId.Pistol)); // not owned: ignored
            p.GiveWeapon(WeaponId.Pistol);
            p.GiveWeapon(WeaponId.Pistol); // idempotent
            Assert.True(p.OwnsWeapon(WeaponId.Pistol));
            p.SetAmmo(WeaponId.Pistol, 9999);
            Assert.Equal(250, p.GetAmmo(WeaponId.Pistol));
            p.SetAmmo(WeaponId.Pistol, -4);
            Assert.Equal(0, p.GetAmmo(WeaponId.Pistol));
        }

        [Fact]
        public void AddAmmo_caps_at_max_and_returns_added()
        {
            var p = new PlayerProfile();
            Assert.Equal(0, p.AddAmmo(WeaponId.Smg, 10)); // not owned
            p.GiveWeapon(WeaponId.Smg);
            Assert.Equal(390, p.AddAmmo(WeaponId.Smg, 390));
            Assert.Equal(10, p.AddAmmo(WeaponId.Smg, 50)); // 400 max
            Assert.Equal(400, p.GetAmmo(WeaponId.Smg));
            Assert.Equal(0, p.AddAmmo(WeaponId.Smg, 5)); // full
            Assert.Equal(0, p.AddAmmo(WeaponId.Smg, -5));
            p.GiveWeapon(WeaponId.Bat);
            Assert.Equal(0, p.AddAmmo(WeaponId.Bat, 5)); // no ammo type
            Assert.Equal(0, p.GetAmmo(WeaponId.Bat));
        }

        [Fact]
        public void StripWeapons_keeps_only_fists_and_zeroes_ammo()
        {
            var p = new PlayerProfile();
            p.GiveWeapon(WeaponId.Bat); p.GiveWeapon(WeaponId.Carbine); p.SetAmmo(WeaponId.Carbine, 50);
            p.StripWeapons();
            Assert.True(p.OwnsWeapon(WeaponId.Fists));
            Assert.False(p.OwnsWeapon(WeaponId.Bat));
            Assert.False(p.OwnsWeapon(WeaponId.Carbine));
            Assert.Equal(0, p.GetAmmo(WeaponId.Carbine));
        }

        [Fact]
        public void ClearAmmo_keeps_weapons()
        {
            var p = new PlayerProfile();
            p.GiveWeapon(WeaponId.Pistol); p.SetAmmo(WeaponId.Pistol, 50);
            p.ClearAmmo();
            Assert.True(p.OwnsWeapon(WeaponId.Pistol));
            Assert.Equal(0, p.GetAmmo(WeaponId.Pistol));
        }

        [Fact]
        public void Missions_are_unique_case_sensitive_and_non_empty()
        {
            var p = new PlayerProfile();
            Assert.True(p.CompleteMission("m1"));
            Assert.False(p.CompleteMission("m1"));
            Assert.True(p.CompleteMission("M1"));
            Assert.False(p.CompleteMission(""));
            Assert.False(p.CompleteMission(null));
            Assert.True(p.IsMissionComplete("m1"));
            Assert.False(p.IsMissionComplete("m2"));
            Assert.Equal(2, p.CompletedMissions.Count);
        }

        [Fact]
        public void Stats_default_to_zero_never_drop_below_zero_and_saturate()
        {
            var p = new PlayerProfile();
            Assert.Equal(0L, p.GetStat("crimes"));
            p.AddStat("crimes", 5);
            p.AddStat("crimes", -2);
            Assert.Equal(3L, p.GetStat("crimes"));
            p.AddStat("crimes", -100);
            Assert.Equal(0L, p.GetStat("crimes"));
            p.AddStat("big", long.MaxValue);
            p.AddStat("big", 10);
            Assert.Equal(long.MaxValue, p.GetStat("big"));
        }

        [Fact]
        public void Side_job_and_district_heat_lookups()
        {
            var p = new PlayerProfile();
            Assert.Equal(0, p.GetSideJobLevel("taxi"));
            Assert.Equal(0f, p.GetDistrictHeat(4));
            p.SideJobLevels["taxi"] = 2; p.DistrictHeat[4] = 30f;
            Assert.Equal(2, p.GetSideJobLevel("taxi"));
            Assert.Equal(30f, p.GetDistrictHeat(4));
        }

        [Fact]
        public void OwnsProperty_checks_the_list()
        {
            var p = new PlayerProfile();
            p.Properties.Add(12u);
            Assert.True(p.OwnsProperty(12u));
            Assert.False(p.OwnsProperty(13u));
        }

        [Fact]
        public void Normalise_restores_every_invariant()
        {
            var p = new PlayerProfile();
            p.Properties.Add(5u); p.Properties.Add(5u); p.Properties.Add(0u); p.Properties.Add(6u);
            p.LastSafehouse = 99u; // not owned
            p.CompletedMissions.Add("a"); p.CompletedMissions.Add("a"); p.CompletedMissions.Add("");
            p.SideJobLevels["ok"] = 2; p.SideJobLevels["zero"] = 0; p.SideJobLevels["neg"] = -3;
            p.Stats["ok"] = 4; p.Stats["neg"] = -1;
            p.DistrictHeat[1] = 500f; p.DistrictHeat[2] = -5f;
            p.OwnedWeapons[0] = false;
            p.OwnedWeapons[(int)WeaponId.Pistol] = true;
            p.AmmoByWeapon[(int)WeaponId.Pistol] = 9000;
            p.AmmoByWeapon[(int)WeaponId.Smg] = 20; // not owned
            p.CampaignStep = -4;
            p.OutfitId = -2;
            p.PlayTimeSeconds = -1;

            p.Normalise();

            Assert.Equal(new List<uint> { 5u, 6u }, new List<uint>(p.Properties));
            Assert.Equal(0u, p.LastSafehouse);
            Assert.Equal(new List<string> { "a" }, new List<string>(p.CompletedMissions));
            Assert.Equal(2, p.SideJobLevels["ok"]);
            Assert.False(p.SideJobLevels.ContainsKey("zero"));
            Assert.False(p.SideJobLevels.ContainsKey("neg"));
            Assert.Equal(4L, p.Stats["ok"]);
            Assert.False(p.Stats.ContainsKey("neg"));
            Assert.Equal(100f, p.GetDistrictHeat(1));
            Assert.Equal(0f, p.GetDistrictHeat(2));
            Assert.True(p.OwnedWeapons[0]);
            Assert.Equal(250, p.AmmoByWeapon[(int)WeaponId.Pistol]);
            Assert.Equal(0, p.AmmoByWeapon[(int)WeaponId.Smg]);
            Assert.Equal(0, p.CampaignStep);
            Assert.Equal(0, p.OutfitId);
            Assert.Equal(0.0, p.PlayTimeSeconds);
        }

        [Fact]
        public void Normalise_keeps_owned_last_safehouse()
        {
            var p = new PlayerProfile();
            p.Properties.Add(8u);
            p.LastSafehouse = 8u;
            p.Normalise();
            Assert.Equal(8u, p.LastSafehouse);
        }

        [Fact]
        public void Weapon_constants_match_design()
        {
            Assert.Equal(new[] { 0, 0, 250, 400, 80, 300 }, WeaponInfo.MaxAmmo);
            Assert.Equal(new[] { 0, 0, 36, 90, 16, 90 }, WeaponInfo.StartAmmo);
            Assert.Equal(6, WeaponInfo.Count);
        }
    }
}
