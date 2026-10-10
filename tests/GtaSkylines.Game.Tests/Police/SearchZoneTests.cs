using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Police;
using GtaSkylines.Game.Tests.CrimeTests;
using Xunit;

namespace GtaSkylines.Game.Tests.PoliceTests
{
    public class SearchZoneTests
    {
        [Fact]
        public void Sector_centres_sit_at_sixty_percent_of_the_radius_at_the_wedge_bearing()
        {
            Vec3 c = new Vec3(10f, 2f, 20f);
            // 4 wedges: sector 0 centre bearing 45 degrees (north-east), sector 1 135 (south-east)
            Vec3 s0 = SearchZone.SectorCentre(c, 100f, 0, 4);
            Vec3 s1 = SearchZone.SectorCentre(c, 100f, 1, 4);
            float k = 60f * 0.70710678f;
            Near.Equal(c.X + k, s0.X, 0.05f); Near.Equal(c.Z + k, s0.Z, 0.05f); Near.Equal(2f, s0.Y, 0.001f);
            Near.Equal(c.X + k, s1.X, 0.05f); Near.Equal(c.Z - k, s1.Z, 0.05f);
        }

        [Fact]
        public void A_single_sector_is_the_centre_itself()
        {
            Vec3 c = new Vec3(5f, 0f, 5f);
            Assert.Equal(c, SearchZone.SectorCentre(c, 100f, 0, 1));
        }

        [Fact]
        public void SectorOf_inverts_SectorCentre()
        {
            Vec3 c = new Vec3(100f, 0f, -50f);
            for (int n = 2; n <= 6; n++)
                for (int i = 0; i < n; i++)
                    Assert.Equal(i, SearchZone.SectorOf(c, SearchZone.SectorCentre(c, 120f, i, n), n));
        }

        [Fact]
        public void SectorOf_the_centre_and_a_single_wedge_is_zero()
        {
            Vec3 c = new Vec3(1f, 0f, 1f);
            Assert.Equal(0, SearchZone.SectorOf(c, c, 4));
            Assert.Equal(0, SearchZone.SectorOf(c, new Vec3(50f, 0f, 50f), 1));
        }

        // Pins of the implemented data helpers (these pass on the skeleton and guard against regressions).
        [Fact]
        public void Radius_grows_six_metres_per_second_from_forty_and_caps_at_400()
        {
            Assert.Equal(40f, SearchZone.Radius(0f), 3);
            Assert.Equal(100f, SearchZone.Radius(10f), 3);
            Assert.Equal(400f, SearchZone.Radius(1000f), 3);
        }

        [Theory]
        [InlineData(1, 20f)] [InlineData(2, 30f)] [InlineData(3, 45f)] [InlineData(4, 60f)] [InlineData(5, 90f)]
        public void Search_time_per_star_and_longer_in_a_hot_district(int stars, float seconds)
        {
            Assert.Equal(seconds, SearchZone.SearchSeconds(stars, 0f), 3);
            Assert.Equal(seconds * 1.5f, SearchZone.SearchSeconds(stars, 61f), 3);
            Assert.Equal(seconds, SearchZone.SearchSeconds(stars, 60f), 3);
        }
    }
}
