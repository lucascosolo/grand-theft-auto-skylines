using GtaSkylines.Game.Crime;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Police;
using GtaSkylines.Game.Tests.CrimeTests;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.Refactor
{
    /// <summary>The wanted episode lives on WantedStatus and is read by Crime, Police and the cop brains.</summary>
    public class WantedEpisodeTests
    {
        private static Scene Make(int stars)
        {
            var s = new Scene(policeCars: false);
            if (stars > 0) s.MakeWanted(stars);
            return s;
        }

        [Fact]
        public void Known_crimes_update_the_episode_on_the_wanted_status()
        {
            Scene s = Make(1);
            var expected = default(WantedEpisode);
            int stars = 1;
            stars = WantedRules.StarsAfterCrime(stars, CrimeClassifier.SeverityOf(CrimeType.Assault), false, ref expected);
            s.Crime.ReportKnownCrime(s.World, CrimeType.Assault, Vec3.Zero, 0);
            stars = WantedRules.StarsAfterCrime(stars, CrimeClassifier.SeverityOf(CrimeType.CopKilled), true, ref expected);
            s.Crime.ReportKnownCrime(s.World, CrimeType.CopKilled, Vec3.Zero, 0);

            Assert.Equal(stars, s.Wanted.Stars);
            Assert.Equal(expected.SeveritySinceStar, s.Wanted.Episode.SeveritySinceStar, 3);
            Assert.Equal(1, s.Wanted.Episode.CopKills);
            Assert.True(s.Wanted.Episode.CopAttacked);
        }

        [Fact]
        public void Episode_is_default_after_stars_return_to_zero_and_a_crime_step_runs()
        {
            Scene s = Make(1);
            s.Crime.ReportKnownCrime(s.World, CrimeType.CopKilled, Vec3.Zero, 0);
            Assert.True(s.Wanted.Episode.CopAttacked);
            s.Wanted.Stars = 0;
            s.Step();
            Assert.False(s.Wanted.Episode.CopAttacked);
            Assert.Equal(0, s.Wanted.Episode.CopKills);
            Assert.Equal(0f, s.Wanted.Episode.SeveritySinceStar);
        }

        [Fact]
        public void Reset_clears_the_episode()
        {
            Scene s = Make(1);
            s.Crime.ReportKnownCrime(s.World, CrimeType.CopKilled, Vec3.Zero, 0);
            Assert.True(s.Wanted.Episode.CopAttacked);
            s.Wanted.Reset();
            Assert.False(s.Wanted.Episode.CopAttacked);
            Assert.Equal(0, s.Wanted.Episode.CopKills);
        }

        [Fact]
        public void Reset_closes_the_episode_so_the_next_wanted_period_is_a_new_number()
        {
            Scene s = Make(1);
            s.Run(1f);
            Assert.Equal(1, s.Wanted.EpisodeNumber);
            s.Wanted.Reset();
            s.MakeWanted(1);
            s.Run(1f);
            Assert.Equal(2, s.Wanted.EpisodeNumber);
        }

        [Fact]
        public void EpisodeNumber_counts_one_per_continuous_wanted_period()
        {
            Scene s = Make(0);
            Assert.Equal(0, s.Wanted.EpisodeNumber);
            s.MakeWanted(1);
            s.Run(5f);
            Assert.Equal(1, s.Wanted.EpisodeNumber);
            s.Wanted.Stars = 2; // rising stars inside the same period do not open a new episode
            s.Run(3f);
            Assert.Equal(1, s.Wanted.EpisodeNumber);

            s.Wanted.Stars = 0;
            s.Run(2f);
            Assert.Equal(1, s.Wanted.EpisodeNumber);

            s.MakeWanted(1);
            s.Run(5f);
            Assert.Equal(2, s.Wanted.EpisodeNumber);
        }

        [Fact]
        public void Opening_an_episode_does_not_clear_what_was_already_recorded()
        {
            Scene s = Make(1);
            s.Wanted.Episode.CopAttacked = true;
            s.Run(1f);
            Assert.True(s.Wanted.Episode.CopAttacked);
        }

        [Fact]
        public void A_cop_that_would_arrest_shoots_once_the_wanted_episode_says_a_cop_was_attacked()
        {
            Scene calm = Make(1);
            AddCop(calm);
            calm.Run(10f);
            Assert.Empty(calm.Peds.Fires);

            Scene hot = Make(1);
            hot.Wanted.Episode.CopAttacked = true;
            AddCop(hot);
            hot.Run(10f);
            Assert.NotEmpty(hot.Peds.Fires);
        }

        private static void AddCop(Scene s)
        {
            Ped cop = s.Cop(0f, -20f);
            cop.Brain = new CopBrain(s.Police) { Mode = CopMode.Exiting };
        }
    }
}
