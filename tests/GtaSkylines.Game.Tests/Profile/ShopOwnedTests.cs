using GtaSkylines.Game.Profile;
using Xunit;

namespace GtaSkylines.Game.Tests.Profile
{
    public class ShopOwnedTests
    {
        private static PlayerProfile Profile()
        {
            var p = new PlayerProfile();
            p.GiveWeapon(WeaponId.Pistol);
            p.OutfitId = 3;
            p.TakeCash(p.Cash);
            p.AddCash(1000000);
            return p;
        }

        [Fact]
        public void Null_item_is_not_owned()
        {
            Assert.False(Shop.IsOwned(null, new PlayerProfile()));
        }

        [Fact]
        public void Owned_weapon_and_worn_outfit_are_owned_others_are_not()
        {
            PlayerProfile p = Profile();
            Assert.True(Shop.IsOwned(Shop.Find("pistol"), p));
            Assert.False(Shop.IsOwned(Shop.Find("smg"), p));
            Assert.True(Shop.IsOwned(Shop.Find("outfit_3"), p));
            Assert.False(Shop.IsOwned(Shop.Find("outfit_4"), p));
        }

        [Fact]
        public void Other_effects_are_never_owned()
        {
            PlayerProfile p = Profile();
            Assert.False(Shop.IsOwned(Shop.Find("ammo_pistol"), p));
            Assert.False(Shop.IsOwned(Shop.Find("armour"), p));
            Assert.False(Shop.IsOwned(Shop.Find("respray"), p));
        }

        [Fact]
        public void IsOwned_agrees_with_Buy_AlreadyOwned_for_every_item()
        {
            foreach (ShopItem item in Shop.Items)
            {
                bool owned = Shop.IsOwned(item, Profile());
                PurchaseResult r = Shop.Buy(new Wallet(Profile()), item.Shop, item.Id);
                Assert.True(owned == (r == PurchaseResult.AlreadyOwned), item.Id);
            }
        }
    }
}
