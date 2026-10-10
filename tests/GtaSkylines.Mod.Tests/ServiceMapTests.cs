using GtaSkylines.Game.City;
using GtaSkylines.Mod.City;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class ServiceMapTests
    {
        [Theory]
        [InlineData("Residential", ServiceKind.Residential)]
        [InlineData("Commercial", ServiceKind.Commercial)]
        [InlineData("Tourism", ServiceKind.Commercial)]
        [InlineData("Office", ServiceKind.Office)]
        [InlineData("Industrial", ServiceKind.Industrial)]
        [InlineData("PlayerIndustry", ServiceKind.Industrial)]
        [InlineData("Fishing", ServiceKind.Industrial)]
        [InlineData("PoliceDepartment", ServiceKind.Police)]
        [InlineData("HealthCare", ServiceKind.Health)]
        [InlineData("FireDepartment", ServiceKind.Fire)]
        [InlineData("Education", ServiceKind.Education)]
        [InlineData("PlayerEducation", ServiceKind.Education)]
        [InlineData("Garbage", ServiceKind.Garbage)]
        [InlineData("Electricity", ServiceKind.Electricity)]
        [InlineData("Water", ServiceKind.Water)]
        [InlineData("PublicTransport", ServiceKind.Transport)]
        [InlineData("Beautification", ServiceKind.Park)]
        [InlineData("VarsitySports", ServiceKind.Park)]
        [InlineData("Monument", ServiceKind.Monument)]
        [InlineData("Museums", ServiceKind.Monument)]
        [InlineData("Disaster", ServiceKind.Government)]
        [InlineData("None", ServiceKind.Other)]
        [InlineData("Banana", ServiceKind.Other)]
        [InlineData(null, ServiceKind.Other)]
        public void Map_by_service(string service, ServiceKind expected)
        {
            Assert.Equal(expected, ServiceMap.Map(service, null, null));
        }

        [Theory]
        [InlineData("HealthCare")]
        [InlineData("Garbage")]
        [InlineData("Residential")]
        [InlineData(null)]
        public void Map_cemetery_ai_is_deathcare(string service)
        {
            Assert.Equal(ServiceKind.Deathcare, ServiceMap.Map(service, "Whatever", "CemeteryAI"));
        }

        [Fact]
        public void Map_ignores_sub_service_and_other_ai()
        {
            Assert.Equal(ServiceKind.Health, ServiceMap.Map("HealthCare", "HealthCareHospital", "HospitalAI"));
            Assert.Equal(ServiceKind.Health, ServiceMap.Map("HealthCare", null, "ClinicAI"));
        }

        [Theory]
        [InlineData(null, "")]
        [InlineData("None", "")]
        [InlineData("", "")]
        [InlineData("ResidentialHigh", "ResidentialHigh")]
        public void SubServiceName_blanks_none(string input, string expected)
        {
            Assert.Equal(expected, ServiceMap.SubServiceName(input));
        }

        [Theory]
        [InlineData(-1, 1)]
        [InlineData(0, 1)]
        [InlineData(1, 2)]
        [InlineData(2, 3)]
        [InlineData(3, 4)]
        [InlineData(4, 5)]
        [InlineData(5, 1)]
        [InlineData(99, 1)]
        public void Level_maps_cs1_enum_to_one_to_five(int cs1, int expected)
        {
            Assert.Equal(expected, ServiceMap.Level(cs1));
        }
    }
}
