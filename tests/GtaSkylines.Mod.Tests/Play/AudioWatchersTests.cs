using GtaSkylines.Mod.Audio;
using GtaSkylines.Mod.Play;
using Xunit;

namespace GtaSkylines.Mod.Tests.Play
{
    public class AudioWatchersTests
    {
        [Fact]
        public void Cash_fires_when_delta_appears_once()
        {
            var w = new CashDeltaWatcher();
            Assert.False(w.Update(0, float.MaxValue));
            Assert.True(w.Update(500, 0f));
            Assert.False(w.Update(500, 0.5f));
            Assert.False(w.Update(500, 2.9f));
        }

        [Fact]
        public void Cash_fires_again_when_amount_changes_or_age_resets()
        {
            var w = new CashDeltaWatcher();
            w.Update(500, 0f);
            w.Update(500, 1f);
            Assert.True(w.Update(700, 0f));
            w.Update(700, 1f);
            Assert.True(w.Update(700, 0f));
        }

        [Fact]
        public void Cash_silent_when_delta_expires()
        {
            var w = new CashDeltaWatcher();
            w.Update(-200, 0f);
            Assert.False(w.Update(0, float.MaxValue));
            Assert.True(w.Update(-200, 0f));
        }

        [Fact]
        public void Ring_fires_on_new_contact_notification_only()
        {
            var w = new PhoneRingWatcher();
            Assert.False(w.Update(0, 0f, false));
            Assert.True(w.Update(1, 0f, true));
            Assert.False(w.Update(1, 0.5f, true));
            Assert.False(w.Update(1, 6f, true));
        }

        [Fact]
        public void Ring_ignores_non_contact_notifications()
        {
            var w = new PhoneRingWatcher();
            Assert.False(w.Update(1, 0f, false));
        }

        [Fact]
        public void Ring_fires_when_feed_is_full_and_newest_age_resets()
        {
            var w = new PhoneRingWatcher();
            w.Update(4, 3f, false);
            Assert.True(w.Update(4, 0f, true));
        }

        [Fact]
        public void Ring_fires_again_after_feed_empties()
        {
            var w = new PhoneRingWatcher();
            w.Update(1, 0f, true);
            w.Update(0, 0f, false);
            Assert.True(w.Update(1, 0f, true));
        }

        [Theory]
        [InlineData(3, false, 0f, SirenKind.PoliceYelp)]
        [InlineData(5, true, 1f, SirenKind.PoliceYelp)]
        [InlineData(0, false, 4.5f, SirenKind.PoliceWail)]
        [InlineData(0, true, 4.5f, SirenKind.PoliceWail)]
        [InlineData(2, false, 4.5f, SirenKind.PoliceWail)]
        [InlineData(1, true, 0f, SirenKind.PoliceWail)]
        [InlineData(2, true, 3.9f, SirenKind.PoliceWail)]
        [InlineData(2, true, 4.0f, SirenKind.PoliceYelp)]
        [InlineData(2, true, 5.4f, SirenKind.PoliceYelp)]
        [InlineData(2, true, 5.6f, SirenKind.PoliceWail)]
        [InlineData(1, true, 10.0f, SirenKind.PoliceYelp)]
        public void PoliceSiren_rule(int stars, bool pursuing, float t, SirenKind expected)
        {
            Assert.Equal(expected, AudioCues.PoliceSiren(stars, pursuing, t));
        }
    }
}
