using System.Collections.Generic;

namespace GtaSkylines.Game.Profile
{
    /// <summary>One money movement, for the HUD ("+$250 Mission reward", "-$600 Pistol").</summary>
    public struct WalletTransaction
    {
        /// <summary>Signed dollars actually moved: positive earned, negative spent or fined. Never 0.</summary>
        public long Delta;
        /// <summary>Short reason shown on the HUD.</summary>
        public string Reason;
    }

    /// <summary>Cash transactions on a profile, recording each as a <see cref="WalletTransaction"/> for the HUD.</summary>
    public sealed class Wallet
    {
        /// <summary>Maximum transactions kept awaiting <see cref="Drain"/>; the oldest are dropped beyond this (16).</summary>
        public const int MaxPending = 16;

        private readonly List<WalletTransaction> pending = new List<WalletTransaction>(MaxPending + 1);

        /// <summary>The profile this wallet changes.</summary>
        public PlayerProfile Profile { get; private set; }

        /// <summary>Creates a wallet over a profile (not null).</summary>
        public Wallet(PlayerProfile profile) { Profile = profile; }

        /// <summary>Current cash in dollars.</summary>
        public long Balance { get { return Profile.Cash; } }

        /// <summary>Adds earnings (amount &gt; 0, otherwise nothing happens), adds the amount actually added to the money_earned stat, records +delta. Returns dollars added.</summary>
        public long Earn(long amount, string reason)
        {
            long added = Profile.AddCash(amount);
            if (added <= 0) return 0;
            Profile.AddStat(StatNames.MoneyEarned, added);
            Record(added, reason);
            return added;
        }

        /// <summary>Spends exactly the amount if affordable, records -amount, returns true. Otherwise changes and records nothing and returns false. Amount &lt;= 0 returns false.</summary>
        public bool TrySpend(long amount, string reason)
        {
            if (!Profile.TrySpend(amount)) return false;
            Profile.AddStat(StatNames.MoneySpent, amount);
            Record(-amount, reason);
            return true;
        }

        /// <summary>Takes up to the amount (a fine or fee): all cash if lower, never negative. Records -taken when taken &gt; 0. Returns dollars taken.</summary>
        public long Fine(long amount, string reason)
        {
            long taken = Profile.TakeCash(amount);
            if (taken > 0) { Profile.AddStat(StatNames.MoneySpent, taken); Record(-taken, reason); }
            return taken;
        }

        /// <summary>Appends pending transactions (oldest first) to <paramref name="into"/> and clears them.</summary>
        public void Drain(List<WalletTransaction> into)
        {
            into.AddRange(pending);
            pending.Clear();
        }

        private void Record(long delta, string reason)
        {
            if (pending.Count >= MaxPending) pending.RemoveAt(0);
            WalletTransaction t;
            t.Delta = delta;
            t.Reason = reason;
            pending.Add(t);
        }
    }
}
