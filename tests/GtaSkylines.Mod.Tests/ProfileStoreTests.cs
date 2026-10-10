using System.Collections.Generic;
using GtaSkylines.Game.Profile;
using GtaSkylines.Mod.Persistence;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class ProfileStoreTests
    {
        private static byte[] Valid() { return ProfileRecord.Encode(new PlayerProfile()); }
        private static readonly byte[] Kept = { 9, 9, 9 };

        private static ProfileStore New(List<string> log = null)
        {
            return new ProfileStore(m => { if (log != null) log.Add(m); });
        }

        private static List<KeyValuePair<string, byte[]>> SaveAll(ProfileStore s)
        {
            var w = new List<KeyValuePair<string, byte[]>>();
            s.Save((k, v) => w.Add(new KeyValuePair<string, byte[]>(k, v)));
            return w;
        }

        [Fact]
        public void Keys_are_fixed()
        {
            Assert.Equal("GtaSkylines.Profile", ProfileStore.Key);
            Assert.Equal("GtaSkylines.Profile.unreadable", ProfileStore.UnreadableKey);
        }

        [Fact]
        public void New_store_is_empty_and_may_not_write()
        {
            var s = New();
            Assert.Null(s.Record);
            Assert.Null(s.Unreadable);
            Assert.False(s.EnteredBefore);
            Assert.False(s.MayWrite);
        }

        [Fact]
        public void Load_null_record_keeps_unreadable_as_is()
        {
            var s = New();
            s.Load(null, Kept);
            Assert.Null(s.Record);
            Assert.Equal(Kept, s.Unreadable);
            Assert.False(s.EnteredBefore);
            Assert.False(s.MayWrite);
        }

        [Fact]
        public void Load_valid_record()
        {
            var s = New();
            byte[] rec = Valid();
            s.Load(rec, Kept);
            Assert.Equal(rec, s.Record);
            Assert.True(s.EnteredBefore);
            Assert.True(s.MayWrite);
            Assert.Equal(Kept, s.Unreadable);
        }

        [Fact]
        public void Load_corrupt_record_starts_fresh_and_keeps_the_raw_bytes()
        {
            var log = new List<string>();
            var s = New(log);
            byte[] bad = { 1, 2, 3 };
            s.Load(bad, Kept);
            Assert.Null(s.Record);
            Assert.Equal(bad, s.Unreadable);
            Assert.True(s.EnteredBefore);
            Assert.Contains(log, m => m.Contains("unreadable"));
        }

        [Fact]
        public void Load_record_with_a_flipped_byte_is_unreadable()
        {
            byte[] bad = Valid();
            bad[bad.Length - 1] ^= 0xFF;
            var s = New();
            s.Load(bad, null);
            Assert.Null(s.Record);
            Assert.Equal(bad, s.Unreadable);
        }

        [Fact]
        public void Load_replaces_all_earlier_state()
        {
            var s = New();
            s.Load(Valid(), Kept);
            s.Update(Valid());
            s.Load(null, null);
            Assert.Null(s.Record);
            Assert.Null(s.Unreadable);
            Assert.False(s.EnteredBefore);
            Assert.False(s.MayWrite);
        }

        [Fact]
        public void MarkEntered_allows_writing()
        {
            var s = New();
            s.MarkEntered();
            Assert.True(s.MayWrite);
            Assert.False(s.EnteredBefore);
        }

        [Fact]
        public void Update_sets_record_and_ignores_null()
        {
            var s = New();
            byte[] rec = Valid();
            s.Update(rec);
            Assert.Equal(rec, s.Record);
            s.Update(null);
            Assert.Equal(rec, s.Record);
        }

        [Fact]
        public void Save_writes_nothing_before_the_player_entered_play()
        {
            var s = New();
            s.Update(Valid());
            Assert.Empty(SaveAll(s));
        }

        [Fact]
        public void Save_writes_record_after_MarkEntered()
        {
            var s = New();
            byte[] rec = Valid();
            s.Update(rec);
            s.MarkEntered();
            var w = SaveAll(s);
            Assert.Single(w);
            Assert.Equal(ProfileStore.Key, w[0].Key);
            Assert.Equal(rec, w[0].Value);
        }

        [Fact]
        public void Save_writes_unreadable_bytes_alongside_the_record()
        {
            var s = New();
            byte[] bad = { 1, 2, 3 };
            s.Load(bad, null);
            s.Update(Valid());
            var w = SaveAll(s);
            Assert.Equal(2, w.Count);
            Assert.Contains(w, kv => kv.Key == ProfileStore.Key);
            Assert.Contains(w, kv => kv.Key == ProfileStore.UnreadableKey && kv.Value.Length == 3);
        }

        [Fact]
        public void Save_with_nothing_to_write_after_entering_writes_nothing()
        {
            var s = New();
            s.MarkEntered();
            Assert.Empty(SaveAll(s));
        }

        [Fact]
        public void Reset_returns_to_a_new_store()
        {
            var s = New();
            s.Load(Valid(), Kept);
            s.MarkEntered();
            s.Reset();
            Assert.Null(s.Record);
            Assert.Null(s.Unreadable);
            Assert.False(s.EnteredBefore);
            Assert.False(s.MayWrite);
            Assert.Empty(SaveAll(s));
        }
    }
}
