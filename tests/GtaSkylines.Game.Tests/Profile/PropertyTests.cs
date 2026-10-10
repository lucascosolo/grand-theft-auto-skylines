using System.Collections.Generic;
using GtaSkylines.Game.Profile;
using Xunit;

namespace GtaSkylines.Game.Tests.Profile
{
    public class PropertyTests
    {
        private static Wallet WalletWith(long cash)
        {
            var p = new PlayerProfile();
            p.TakeCash(p.Cash);
            p.AddCash(cash);
            return new Wallet(p);
        }

        [Theory]
        [InlineData(1, 64f, 9500L)]          // 64*150 = 9600 -> nearest 500 = 9500
        [InlineData(5, 2500f, 1375000L)]     // 2500*550
        [InlineData(1, 100f, 15000L)]        // 15000 exact
        [InlineData(2, 100f, 25000L)]        // 100*250
        [InlineData(3, 100f, 35000L)]        // 100*350
        [InlineData(4, 100f, 45000L)]        // 100*450
        [InlineData(1, 10f, 9500L)]          // area clamped up to 64
        [InlineData(1, 5000f, 375000L)]      // area clamped down to 2500: 2500*150
        [InlineData(1, 70f, 10500L)]         // 10500 exact
        [InlineData(2, 101f, 25500L)]        // 101*250/500 = 50.5, halves up to 51
        public void PriceFor_follows_the_formula(int level, float area, long expected)
        {
            Assert.Equal(expected, Property.PriceFor(level, area));
        }

        [Theory]
        [InlineData(0, 100f)]
        [InlineData(6, 100f)]
        [InlineData(-1, 100f)]
        [InlineData(1, 0f)]
        [InlineData(1, -10f)]
        [InlineData(1, float.NaN)]
        public void PriceFor_invalid_input_is_minus_one(int level, float area)
        {
            Assert.Equal(-1L, Property.PriceFor(level, area));
        }

        [Fact]
        public void Buy_charges_records_owns_and_sets_first_safehouse()
        {
            var w = WalletWith(20000);
            Assert.Equal(PurchaseResult.Ok, Property.Buy(w, 42u, 1, 100f)); // 15000
            Assert.Equal(5000L, w.Balance);
            Assert.True(w.Profile.OwnsProperty(42u));
            Assert.Equal(42u, w.Profile.LastSafehouse);
            var t = new List<WalletTransaction>();
            w.Drain(t);
            Assert.Single(t);
            Assert.Equal(-15000L, t[0].Delta);
            Assert.Equal("Property", t[0].Reason);
        }

        [Fact]
        public void Second_purchase_keeps_the_existing_last_safehouse()
        {
            var w = WalletWith(100000);
            Property.Buy(w, 42u, 1, 100f);
            Assert.Equal(PurchaseResult.Ok, Property.Buy(w, 43u, 1, 100f));
            Assert.Equal(42u, w.Profile.LastSafehouse);
            Assert.Equal(new List<uint> { 42u, 43u }, Property.Safehouses(w.Profile));
        }

        [Fact]
        public void Buy_rejections_change_nothing()
        {
            var w = WalletWith(20000);
            Assert.Equal(PurchaseResult.InvalidProperty, Property.Buy(w, 0u, 1, 100f));
            Assert.Equal(PurchaseResult.InvalidProperty, Property.Buy(w, 5u, 0, 100f));
            Assert.Equal(PurchaseResult.InvalidProperty, Property.Buy(w, 5u, 6, 100f));
            Assert.Equal(PurchaseResult.InvalidProperty, Property.Buy(w, 5u, 1, 0f));
            Assert.Equal(PurchaseResult.InsufficientCash, Property.Buy(w, 5u, 5, 1000f));
            Assert.Equal(20000L, w.Balance);
            Assert.Empty(w.Profile.Properties);
            Assert.Equal(0u, w.Profile.LastSafehouse);
        }

        [Fact]
        public void Buying_an_owned_building_is_AlreadyOwned()
        {
            var w = WalletWith(100000);
            Property.Buy(w, 42u, 1, 100f);
            long before = w.Balance;
            Assert.Equal(PurchaseResult.AlreadyOwned, Property.Buy(w, 42u, 1, 100f));
            Assert.Equal(before, w.Balance);
            Assert.Single(w.Profile.Properties);
        }

        [Fact]
        public void SetLastSafehouse_requires_ownership()
        {
            var w = WalletWith(100000);
            Property.Buy(w, 42u, 1, 100f);
            Property.Buy(w, 43u, 1, 100f);
            Assert.True(Property.SetLastSafehouse(w.Profile, 43u));
            Assert.Equal(43u, w.Profile.LastSafehouse);
            Assert.False(Property.SetLastSafehouse(w.Profile, 99u));
            Assert.Equal(43u, w.Profile.LastSafehouse);
        }

        [Fact]
        public void Safehouses_returns_a_copy_in_purchase_order()
        {
            var p = new PlayerProfile();
            p.Properties.Add(9u); p.Properties.Add(3u);
            var l = Property.Safehouses(p);
            Assert.Equal(new List<uint> { 9u, 3u }, l);
            l.Add(100u);
            Assert.Equal(2, p.Properties.Count);
        }
    }
}
