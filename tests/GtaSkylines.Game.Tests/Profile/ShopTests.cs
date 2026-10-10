using System.Collections.Generic;
using GtaSkylines.Game.Profile;
using Xunit;

namespace GtaSkylines.Game.Tests.Profile
{
    public class ShopTests
    {
        private static Wallet RichWallet(long cash = 100000)
        {
            var p = new PlayerProfile();
            p.TakeCash(p.Cash);
            p.AddCash(cash);
            return new Wallet(p);
        }

        private static List<WalletTransaction> Drained(Wallet w)
        {
            var l = new List<WalletTransaction>();
            w.Drain(l);
            return l;
        }

        [Fact]
        public void Find_returns_items_by_exact_id_and_null_otherwise()
        {
            var item = Shop.Find("pistol");
            Assert.NotNull(item);
            Assert.Equal(600L, item.Price);
            Assert.Equal(WeaponId.Pistol, item.Weapon);
            Assert.Null(Shop.Find("Pistol"));
            Assert.Null(Shop.Find("nope"));
            Assert.Null(Shop.Find(null));
        }

        [Fact]
        public void Catalogue_prices_match_design()
        {
            var expected = new Dictionary<string, long>
            {
                { "bat", 100 }, { "pistol", 600 }, { "smg", 2200 }, { "shotgun", 1800 }, { "carbine", 4500 },
                { "ammo_pistol", 50 }, { "ammo_smg", 90 }, { "ammo_shotgun", 70 }, { "ammo_carbine", 120 }, { "armour", 250 },
                { "snack", 5 }, { "meal", 20 },
                { "outfit_1", 80 }, { "outfit_2", 120 }, { "outfit_3", 200 }, { "outfit_4", 260 }, { "outfit_5", 320 }, { "outfit_6", 400 },
                { "respray", 300 }, { "heal", 200 }, { "payoff_heat", 500 },
            };
            Assert.Equal(expected.Count, Shop.Items.Length);
            foreach (var kv in expected)
            {
                var item = Shop.Find(kv.Key);
                Assert.NotNull(item);
                Assert.Equal(kv.Value, item.Price);
            }
        }

        [Fact]
        public void ItemsFor_lists_each_shop_in_catalogue_order()
        {
            var ammu = Shop.ItemsFor(ShopKind.Ammunation);
            Assert.Equal(10, ammu.Count);
            Assert.Equal("bat", ammu[0].Id);
            Assert.Equal("armour", ammu[9].Id);
            Assert.Equal(new[] { "snack", "meal" }, Ids(Shop.ItemsFor(ShopKind.Food)));
            Assert.Equal(new[] { "outfit_1", "outfit_2" }, Ids(Shop.ItemsFor(ShopKind.ClothesBudget)));
            Assert.Equal(new[] { "outfit_3", "outfit_4" }, Ids(Shop.ItemsFor(ShopKind.ClothesMid)));
            Assert.Equal(new[] { "outfit_5", "outfit_6" }, Ids(Shop.ItemsFor(ShopKind.ClothesDesigner)));
            Assert.Equal(new[] { "respray" }, Ids(Shop.ItemsFor(ShopKind.Respray)));
            Assert.Equal(new[] { "heal" }, Ids(Shop.ItemsFor(ShopKind.Hospital)));
            Assert.Equal(new[] { "payoff_heat" }, Ids(Shop.ItemsFor(ShopKind.PoliceStation)));
        }

        private static string[] Ids(List<ShopItem> items)
        {
            var r = new string[items.Count];
            for (int i = 0; i < r.Length; i++) r[i] = items[i].Id;
            return r;
        }

        [Fact]
        public void PriceOf_is_fixed_except_pay_off_heat_which_scales_with_stars()
        {
            var p = new PlayerProfile();
            Assert.Equal(600L, Shop.PriceOf(Shop.Find("pistol"), p));
            var payoff = Shop.Find("payoff_heat");
            Assert.Equal(0L, Shop.PriceOf(payoff, p));
            p.Heat = 20f;
            Assert.Equal(500L, Shop.PriceOf(payoff, p));
            p.Heat = 61f;
            Assert.Equal(2000L, Shop.PriceOf(payoff, p));
            p.Heat = 100f;
            Assert.Equal(2500L, Shop.PriceOf(payoff, p));
        }

        [Fact]
        public void Buying_a_weapon_adds_it_with_start_ammo_and_records_the_name()
        {
            var w = RichWallet(1000);
            Assert.Equal(PurchaseResult.Ok, Shop.Buy(w, ShopKind.Ammunation, "pistol"));
            Assert.Equal(400L, w.Balance);
            Assert.True(w.Profile.OwnsWeapon(WeaponId.Pistol));
            Assert.Equal(36, w.Profile.GetAmmo(WeaponId.Pistol));
            var t = Drained(w);
            Assert.Single(t);
            Assert.Equal(-600L, t[0].Delta);
            Assert.Equal("Pistol", t[0].Reason);
        }

        [Fact]
        public void Buying_the_bat_gives_a_weapon_without_ammo()
        {
            var w = RichWallet();
            Assert.Equal(PurchaseResult.Ok, Shop.Buy(w, ShopKind.Ammunation, "bat"));
            Assert.True(w.Profile.OwnsWeapon(WeaponId.Bat));
            Assert.Equal(0, w.Profile.GetAmmo(WeaponId.Bat));
        }

        [Fact]
        public void Buying_an_owned_weapon_is_AlreadyOwned_and_charges_nothing()
        {
            var w = RichWallet(5000);
            Shop.Buy(w, ShopKind.Ammunation, "smg");
            long before = w.Balance;
            Drained(w);
            Assert.Equal(PurchaseResult.AlreadyOwned, Shop.Buy(w, ShopKind.Ammunation, "smg"));
            Assert.Equal(before, w.Balance);
            Assert.Empty(Drained(w));
        }

        [Fact]
        public void Insufficient_cash_changes_nothing()
        {
            var w = RichWallet(599);
            Assert.Equal(PurchaseResult.InsufficientCash, Shop.Buy(w, ShopKind.Ammunation, "pistol"));
            Assert.Equal(599L, w.Balance);
            Assert.False(w.Profile.OwnsWeapon(WeaponId.Pistol));
            Assert.Empty(Drained(w));
        }

        [Fact]
        public void Exact_cash_is_enough()
        {
            var w = RichWallet(600);
            Assert.Equal(PurchaseResult.Ok, Shop.Buy(w, ShopKind.Ammunation, "pistol"));
            Assert.Equal(0L, w.Balance);
        }

        [Fact]
        public void Unknown_item_and_wrong_shop_are_rejected()
        {
            var w = RichWallet();
            Assert.Equal(PurchaseResult.UnknownItem, Shop.Buy(w, ShopKind.Ammunation, "rocket"));
            Assert.Equal(PurchaseResult.UnknownItem, Shop.Buy(w, ShopKind.Ammunation, null));
            Assert.Equal(PurchaseResult.WrongShop, Shop.Buy(w, ShopKind.Food, "pistol"));
            Assert.Equal(100000L, w.Balance);
        }

        [Fact]
        public void Ammo_pack_requires_the_weapon_and_adds_rounds()
        {
            var w = RichWallet();
            Assert.Equal(PurchaseResult.WeaponNotOwned, Shop.Buy(w, ShopKind.Ammunation, "ammo_pistol"));
            Assert.Equal(100000L, w.Balance);
            Shop.Buy(w, ShopKind.Ammunation, "pistol");
            long before = w.Balance;
            Assert.Equal(PurchaseResult.Ok, Shop.Buy(w, ShopKind.Ammunation, "ammo_pistol"));
            Assert.Equal(72, w.Profile.GetAmmo(WeaponId.Pistol));
            Assert.Equal(before - 50, w.Balance);
        }

        [Fact]
        public void Ammo_pack_that_overflows_is_bought_whole_and_capped()
        {
            var w = RichWallet();
            Shop.Buy(w, ShopKind.Ammunation, "shotgun");
            w.Profile.SetAmmo(WeaponId.Shotgun, 70); // max 80, pack gives 16
            long before = w.Balance;
            Assert.Equal(PurchaseResult.Ok, Shop.Buy(w, ShopKind.Ammunation, "ammo_shotgun"));
            Assert.Equal(80, w.Profile.GetAmmo(WeaponId.Shotgun));
            Assert.Equal(before - 70, w.Balance);
        }

        [Fact]
        public void Ammo_at_maximum_is_AmmoFull_and_free()
        {
            var w = RichWallet();
            Shop.Buy(w, ShopKind.Ammunation, "carbine");
            w.Profile.SetAmmo(WeaponId.Carbine, 300);
            long before = w.Balance;
            Assert.Equal(PurchaseResult.AmmoFull, Shop.Buy(w, ShopKind.Ammunation, "ammo_carbine"));
            Assert.Equal(before, w.Balance);
            Assert.Equal(300, w.Profile.GetAmmo(WeaponId.Carbine));
        }

        [Fact]
        public void Armour_sets_to_100_once()
        {
            var w = RichWallet();
            w.Profile.Armour = 30;
            Assert.Equal(PurchaseResult.Ok, Shop.Buy(w, ShopKind.Ammunation, "armour"));
            Assert.Equal(100, w.Profile.Armour);
            Assert.Equal(99750L, w.Balance);
            Assert.Equal(PurchaseResult.ArmourFull, Shop.Buy(w, ShopKind.Ammunation, "armour"));
            Assert.Equal(99750L, w.Balance);
        }

        [Fact]
        public void Food_adds_health_capped_at_100()
        {
            var w = RichWallet();
            w.Profile.Health = 50;
            Assert.Equal(PurchaseResult.Ok, Shop.Buy(w, ShopKind.Food, "snack"));
            Assert.Equal(75, w.Profile.Health);
            Assert.Equal(PurchaseResult.Ok, Shop.Buy(w, ShopKind.Food, "meal"));
            Assert.Equal(100, w.Profile.Health);
            Assert.Equal(100000L - 5 - 20, w.Balance);
        }

        [Fact]
        public void Food_at_full_health_is_HealthFull_and_free()
        {
            var w = RichWallet();
            Assert.Equal(PurchaseResult.HealthFull, Shop.Buy(w, ShopKind.Food, "meal"));
            Assert.Equal(100000L, w.Balance);
        }

        [Fact]
        public void Hospital_heal_sets_health_to_100_or_reports_full()
        {
            var w = RichWallet();
            Assert.Equal(PurchaseResult.HealthFull, Shop.Buy(w, ShopKind.Hospital, "heal"));
            w.Profile.Health = 1;
            Assert.Equal(PurchaseResult.Ok, Shop.Buy(w, ShopKind.Hospital, "heal"));
            Assert.Equal(100, w.Profile.Health);
            Assert.Equal(99800L, w.Balance);
        }

        [Fact]
        public void Outfit_sets_outfit_id_and_wearing_it_again_is_AlreadyOwned()
        {
            var w = RichWallet();
            Assert.Equal(PurchaseResult.Ok, Shop.Buy(w, ShopKind.ClothesDesigner, "outfit_6"));
            Assert.Equal(6, w.Profile.OutfitId);
            Assert.Equal(99600L, w.Balance);
            Assert.Equal(PurchaseResult.AlreadyOwned, Shop.Buy(w, ShopKind.ClothesDesigner, "outfit_6"));
            Assert.Equal(99600L, w.Balance);
            Assert.Equal(PurchaseResult.WrongShop, Shop.Buy(w, ShopKind.ClothesBudget, "outfit_6"));
            Assert.Equal(PurchaseResult.Ok, Shop.Buy(w, ShopKind.ClothesBudget, "outfit_1"));
            Assert.Equal(1, w.Profile.OutfitId);
        }

        [Fact]
        public void Respray_charges_without_touching_the_profile()
        {
            var w = RichWallet();
            Assert.Equal(PurchaseResult.Ok, Shop.Buy(w, ShopKind.Respray, "respray"));
            Assert.Equal(99700L, w.Balance);
            Assert.Equal(100, w.Profile.Health);
            Assert.Equal(0, w.Profile.OutfitId);
        }

        [Fact]
        public void Pay_off_heat_clears_player_and_district_heat_and_charges_per_star()
        {
            var w = RichWallet(5000);
            Assert.Equal(PurchaseResult.NoHeat, Shop.Buy(w, ShopKind.PoliceStation, "payoff_heat"));
            Assert.Equal(5000L, w.Balance);
            w.Profile.Heat = 45f; // 3 stars
            w.Profile.DistrictHeat[2] = 30f;
            w.Profile.DistrictHeat[9] = 80f;
            Assert.Equal(PurchaseResult.Ok, Shop.Buy(w, ShopKind.PoliceStation, "payoff_heat"));
            Assert.Equal(3500L, w.Balance);
            Assert.Equal(0f, w.Profile.Heat);
            Assert.Equal(0f, w.Profile.GetDistrictHeat(2));
            Assert.Equal(0f, w.Profile.GetDistrictHeat(9));
        }

        [Fact]
        public void Pay_off_heat_without_enough_cash_changes_nothing()
        {
            var w = RichWallet(1000);
            w.Profile.Heat = 100f; // 5 stars = 2500
            Assert.Equal(PurchaseResult.InsufficientCash, Shop.Buy(w, ShopKind.PoliceStation, "payoff_heat"));
            Assert.Equal(1000L, w.Balance);
            Assert.Equal(100f, w.Profile.Heat);
        }

        [Fact]
        public void Already_conditions_are_checked_before_cash()
        {
            var w = RichWallet(0);
            Assert.Equal(PurchaseResult.HealthFull, Shop.Buy(w, ShopKind.Food, "snack"));
            Assert.Equal(PurchaseResult.WeaponNotOwned, Shop.Buy(w, ShopKind.Ammunation, "ammo_smg"));
            Assert.Equal(PurchaseResult.NoHeat, Shop.Buy(w, ShopKind.PoliceStation, "payoff_heat"));
        }
    }
}
