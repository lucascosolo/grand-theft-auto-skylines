using GtaSkylines.Game.City;
using GtaSkylines.Game.Tests.Fakes;
using Xunit;

namespace GtaSkylines.Game.Tests.CityTests
{
    /// <summary>Test-only helper: gives a <see cref="FakeCity"/>'s eight citizen models explicit roles.</summary>
    internal static class Roles
    {
        /// <summary>Sets every model to Civilian, then the listed ids to Police and Service.</summary>
        public static FakeCity Apply(FakeCity city, int[] police, int[] service)
        {
            foreach (CitizenModel m in city.CitizenModels) m.Role = CitizenModelRole.Civilian;
            foreach (int id in police) city.CitizenModels[id].Role = CitizenModelRole.Police;
            foreach (int id in service) city.CitizenModels[id].Role = CitizenModelRole.Service;
            return city;
        }

        public static bool IsCivilian(ICity city, int id)
        {
            CitizenModel m = city.GetCitizenModel(id);
            return m != null && m.Role == CitizenModelRole.Civilian;
        }

        public static bool IsPolice(ICity city, int id)
        {
            CitizenModel m = city.GetCitizenModel(id);
            return m != null && m.Role == CitizenModelRole.Police;
        }
    }

    public class CitizenModelsTests
    {
        [Fact]
        public void A_model_is_a_civilian_by_default()
        {
            Assert.Equal(CitizenModelRole.Civilian, new CitizenModel().Role);
        }

        [Fact]
        public void Civilian_returns_the_pick_when_it_is_already_a_civilian()
        {
            FakeCity c = Roles.Apply(new FakeCity(), new[] { 1 }, new[] { 2 });
            Assert.Equal(0, CitizenModels.Civilian(c, 0));
            Assert.Equal(5, CitizenModels.Civilian(c, 5));
        }

        [Fact]
        public void Civilian_moves_to_the_next_civilian_id_after_a_police_or_service_pick()
        {
            FakeCity c = Roles.Apply(new FakeCity(), new[] { 1 }, new[] { 2 });
            Assert.Equal(3, CitizenModels.Civilian(c, 1));
            Assert.Equal(3, CitizenModels.Civilian(c, 2));
        }

        [Fact]
        public void Civilian_wraps_around_past_the_last_id()
        {
            FakeCity c = Roles.Apply(new FakeCity(), new[] { 0, 7 }, new[] { 6 });
            Assert.Equal(1, CitizenModels.Civilian(c, 6));
            Assert.Equal(1, CitizenModels.Civilian(c, 7));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(8)]
        [InlineData(500)]
        public void Civilian_returns_an_out_of_range_pick_unchanged(int pick)
        {
            FakeCity c = Roles.Apply(new FakeCity(), new[] { 1 }, new int[0]);
            Assert.Equal(pick, CitizenModels.Civilian(c, pick));
        }

        [Fact]
        public void Civilian_returns_the_pick_unchanged_when_the_city_has_no_civilian_model()
        {
            FakeCity c = Roles.Apply(new FakeCity(), new[] { 0, 1, 2, 3 }, new[] { 4, 5, 6, 7 });
            Assert.Equal(3, CitizenModels.Civilian(c, 3));
            Assert.Equal(6, CitizenModels.Civilian(c, 6));
        }

        [Fact]
        public void Police_is_minus_one_when_the_city_has_no_police_model()
        {
            FakeCity c = Roles.Apply(new FakeCity(), new int[0], new[] { 2, 3 });
            Assert.Equal(-1, CitizenModels.Police(c, 0u));
            Assert.Equal(-1, CitizenModels.Police(c, 12345u));
        }

        [Fact]
        public void Police_picks_the_salt_modulo_count_th_police_model_in_id_order()
        {
            FakeCity c = Roles.Apply(new FakeCity(), new[] { 6, 1, 4 }, new[] { 2 });
            Assert.Equal(1, CitizenModels.Police(c, 0u));
            Assert.Equal(4, CitizenModels.Police(c, 1u));
            Assert.Equal(6, CitizenModels.Police(c, 2u));
            Assert.Equal(1, CitizenModels.Police(c, 3u));
            Assert.Equal(6, CitizenModels.Police(c, uint.MaxValue - 1u));   // 4294967294 % 3 == 2
        }

        [Fact]
        public void Police_never_returns_a_service_model()
        {
            FakeCity c = Roles.Apply(new FakeCity(), new[] { 3 }, new[] { 0, 1, 2, 4, 5, 6, 7 });
            for (uint salt = 0; salt < 20; salt++) Assert.Equal(3, CitizenModels.Police(c, salt));
        }
    }
}
