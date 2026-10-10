using System.Collections.Generic;
using GtaSkylines.Game.Profile;
using Xunit;

namespace GtaSkylines.Game.Tests.Profile
{
    public class WalletTests
    {
        private static List<WalletTransaction> Drained(Wallet w)
        {
            var l = new List<WalletTransaction>();
            w.Drain(l);
            return l;
        }

        [Fact]
        public void Balance_reflects_profile_cash()
        {
            var w = new Wallet(new PlayerProfile());
            Assert.Equal(500L, w.Balance);
        }

        [Fact]
        public void Earn_adds_cash_records_positive_delta_and_counts_money_earned()
        {
            var p = new PlayerProfile();
            var w = new Wallet(p);
            Assert.Equal(250L, w.Earn(250, "Mission reward"));
            Assert.Equal(750L, w.Balance);
            Assert.Equal(250L, p.GetStat(StatNames.MoneyEarned));
            var t = Drained(w);
            Assert.Single(t);
            Assert.Equal(250L, t[0].Delta);
            Assert.Equal("Mission reward", t[0].Reason);
        }

        [Fact]
        public void Earn_zero_or_negative_does_nothing()
        {
            var w = new Wallet(new PlayerProfile());
            Assert.Equal(0L, w.Earn(0, "x"));
            Assert.Equal(0L, w.Earn(-50, "x"));
            Assert.Equal(500L, w.Balance);
            Assert.Empty(Drained(w));
        }

        [Fact]
        public void Earn_at_the_cap_records_only_what_was_added()
        {
            var p = new PlayerProfile();
            var w = new Wallet(p);
            long room = PlayerProfile.MaxCash - 500;
            Assert.Equal(room, w.Earn(room + 1000, "Jackpot"));
            Assert.Equal(PlayerProfile.MaxCash, w.Balance);
            Assert.Equal(room, p.GetStat(StatNames.MoneyEarned));
            Assert.Equal(room, Drained(w)[0].Delta);
            Assert.Equal(0L, w.Earn(10, "More"));
            Assert.Empty(Drained(w));
        }

        [Fact]
        public void TrySpend_charges_exactly_and_records_negative_delta()
        {
            var w = new Wallet(new PlayerProfile());
            Assert.True(w.TrySpend(100, "Bat"));
            Assert.Equal(400L, w.Balance);
            var t = Drained(w);
            Assert.Single(t);
            Assert.Equal(-100L, t[0].Delta);
            Assert.Equal("Bat", t[0].Reason);
        }

        [Fact]
        public void TrySpend_unaffordable_or_non_positive_changes_and_records_nothing()
        {
            var w = new Wallet(new PlayerProfile());
            Assert.False(w.TrySpend(501, "Too much"));
            Assert.False(w.TrySpend(0, "Zero"));
            Assert.False(w.TrySpend(-5, "Neg"));
            Assert.Equal(500L, w.Balance);
            Assert.Empty(Drained(w));
        }

        [Fact]
        public void TrySpend_exact_balance_leaves_zero()
        {
            var w = new Wallet(new PlayerProfile());
            Assert.True(w.TrySpend(500, "All"));
            Assert.Equal(0L, w.Balance);
        }

        [Fact]
        public void Fine_takes_what_exists_and_never_goes_negative()
        {
            var w = new Wallet(new PlayerProfile());
            Assert.Equal(200L, w.Fine(200, "Fine"));
            Assert.Equal(300L, w.Fine(1000, "Big fine"));
            Assert.Equal(0L, w.Balance);
            var t = Drained(w);
            Assert.Equal(2, t.Count);
            Assert.Equal(-200L, t[0].Delta);
            Assert.Equal(-300L, t[1].Delta);
            Assert.Equal("Big fine", t[1].Reason);
        }

        [Fact]
        public void Fine_with_no_cash_or_bad_amount_records_nothing()
        {
            var p = new PlayerProfile();
            var w = new Wallet(p);
            w.Fine(500, "drain");
            Drained(w);
            Assert.Equal(0L, w.Fine(100, "Broke"));
            Assert.Equal(0L, w.Fine(-100, "Neg"));
            Assert.Empty(Drained(w));
            Assert.Equal(0L, w.Balance);
        }

        [Fact]
        public void Drain_returns_oldest_first_appends_and_clears()
        {
            var w = new Wallet(new PlayerProfile());
            w.Earn(10, "a");
            w.TrySpend(5, "b");
            w.Fine(1, "c");
            var into = new List<WalletTransaction> { new WalletTransaction { Delta = 99, Reason = "pre" } };
            w.Drain(into);
            Assert.Equal(4, into.Count);
            Assert.Equal("pre", into[0].Reason);
            Assert.Equal("a", into[1].Reason);
            Assert.Equal("b", into[2].Reason);
            Assert.Equal("c", into[3].Reason);
            Assert.Empty(Drained(w));
        }

        [Fact]
        public void Pending_transactions_are_capped_dropping_the_oldest()
        {
            var w = new Wallet(new PlayerProfile());
            for (int i = 1; i <= Wallet.MaxPending + 4; i++) w.Earn(i, "t" + i);
            var t = Drained(w);
            Assert.Equal(Wallet.MaxPending, t.Count);
            Assert.Equal("t5", t[0].Reason);
            Assert.Equal("t" + (Wallet.MaxPending + 4), t[t.Count - 1].Reason);
        }

        [Fact]
        public void Wallet_exposes_its_profile()
        {
            var p = new PlayerProfile();
            Assert.Same(p, new Wallet(p).Profile);
        }
    }
}
