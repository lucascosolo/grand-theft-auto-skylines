using GtaSkylines.Game.Crime;
using Xunit;

namespace GtaSkylines.Game.Tests.CrimeTests
{
    public class WantedRulesTests
    {
        [Theory]
        [InlineData(0.5f, 1)]
        [InlineData(1f, 1)]
        [InlineData(2f, 2)]
        [InlineData(3f, 3)]
        [InlineData(4f, 4)]
        public void Unwanted_player_gets_ceil_of_severity_stars(float severity, int expected)
        {
            var ep = new WantedEpisode();
            Assert.Equal(expected, WantedRules.StarsAfterCrime(0, severity, false, ref ep));
        }

        [Fact]
        public void Lower_severity_never_lowers_stars()
        {
            var ep = new WantedEpisode();
            Assert.Equal(3, WantedRules.StarsAfterCrime(3, 1f, false, ref ep));
        }

        [Fact]
        public void Higher_severity_raises_stars_to_its_ceiling()
        {
            var ep = new WantedEpisode();
            Assert.Equal(3, WantedRules.StarsAfterCrime(1, 3f, false, ref ep));
        }

        [Fact]
        public void Four_more_severity_one_crimes_at_one_star_escalate_only_on_the_fourth()
        {
            var ep = new WantedEpisode();
            int stars = 1;
            stars = WantedRules.StarsAfterCrime(stars, 1f, false, ref ep);
            stars = WantedRules.StarsAfterCrime(stars, 1f, false, ref ep);
            stars = WantedRules.StarsAfterCrime(stars, 1f, false, ref ep);
            Assert.Equal(1, stars);          // sum 3 is not above 3
            stars = WantedRules.StarsAfterCrime(stars, 1f, false, ref ep);
            Assert.Equal(2, stars);          // sum 4 is
        }

        [Fact]
        public void Accumulated_severity_restarts_after_a_star_is_gained()
        {
            var ep = new WantedEpisode();
            int stars = 1;
            for (int i = 0; i < 4; i++) stars = WantedRules.StarsAfterCrime(stars, 1f, false, ref ep);
            Assert.Equal(2, stars);
            Assert.Equal(0f, ep.SeveritySinceStar);
            for (int i = 0; i < 3; i++) stars = WantedRules.StarsAfterCrime(stars, 1f, false, ref ep);
            Assert.Equal(2, stars);
        }

        [Fact]
        public void Stars_cap_at_five()
        {
            var ep = new WantedEpisode();
            Assert.Equal(5, WantedRules.StarsAfterCrime(5, 4f, false, ref ep));
            int stars = 4;
            stars = WantedRules.StarsAfterCrime(stars, 4f, false, ref ep);
            stars = WantedRules.StarsAfterCrime(stars, 4f, false, ref ep);
            Assert.Equal(5, stars);
        }

        [Fact]
        public void First_cop_kill_means_at_least_three_stars_and_the_second_at_least_four()
        {
            var ep = new WantedEpisode();
            int stars = WantedRules.StarsAfterCrime(0, 1f, true, ref ep);
            Assert.Equal(3, stars);
            Assert.Equal(1, ep.CopKills);
            stars = WantedRules.StarsAfterCrime(stars, 1f, true, ref ep);
            Assert.Equal(4, stars);
            Assert.Equal(2, ep.CopKills);
        }

        [Fact]
        public void Player_heat_rises_six_per_severity_and_caps_at_100()
        {
            Assert.Equal(18f, WantedRules.PlayerHeatAfterCrime(0f, 3f), 3);
            Assert.Equal(100f, WantedRules.PlayerHeatAfterCrime(95f, 3f), 3);
        }

        [Fact]
        public void District_heat_rises_ten_per_severity_and_caps_at_100()
        {
            Assert.Equal(30f, WantedRules.DistrictHeatAfterCrime(0f, 3f), 3);
            Assert.Equal(100f, WantedRules.DistrictHeatAfterCrime(80f, 3f), 3);
        }

        [Fact]
        public void Player_heat_does_not_decay_while_wanted()
        {
            Assert.Equal(40f, WantedRules.DecayPlayerHeat(40f, true, false, 600f), 3);
        }

        [Fact]
        public void Player_heat_decays_one_per_minute_when_not_wanted_and_three_in_a_safehouse()
        {
            Assert.Equal(39f, WantedRules.DecayPlayerHeat(40f, false, false, 60f), 3);
            Assert.Equal(37f, WantedRules.DecayPlayerHeat(40f, false, true, 60f), 3);
            Assert.Equal(39.5f, WantedRules.DecayPlayerHeat(40f, false, false, 30f), 3);
        }

        [Fact]
        public void Heat_decay_never_goes_below_zero()
        {
            Assert.Equal(0f, WantedRules.DecayPlayerHeat(0.5f, false, true, 600f), 3);
            Assert.Equal(0f, WantedRules.DecayDistrictHeat(1f, 600f), 3);
        }

        [Fact]
        public void District_heat_decays_two_per_minute()
        {
            Assert.Equal(38f, WantedRules.DecayDistrictHeat(40f, 60f), 3);
        }
    }
}
