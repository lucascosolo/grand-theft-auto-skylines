using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Interiors;
using Xunit;
using static GtaSkylines.Game.Tests.Interiors.InteriorTestSupport;

namespace GtaSkylines.Game.Tests.Interiors
{
    public class ArchetypeForTests
    {
        [Theory]
        [MemberData(nameof(AllServices))]
        public void AbandonedBuildingsHaveNoInterior(ServiceKind service)
        {
            foreach (string sub in new[] { "", "ResidentialLow", "CommercialLeisure", "IndustrialOil" })
            {
                var b = Building(5, service, sub, abandoned: true);
                Assert.Equal(InteriorArchetype.None, InteriorGenerator.ArchetypeFor(b));
                Assert.Null(InteriorGenerator.Generate(b));
            }
        }

        [Fact]
        public void ParksHaveNoInterior()
        {
            for (int id = 0; id < 50; id++)
            {
                var b = Building(id, ServiceKind.Park, "", 30f, 30f, 3f);
                Assert.Equal(InteriorArchetype.None, InteriorGenerator.ArchetypeFor(b));
                Assert.Null(InteriorGenerator.Generate(b));
            }
        }

        [Fact]
        public void GenerateWithArchetypeNoneIsNull()
        {
            Assert.Null(InteriorGenerator.Generate(Building(9, ServiceKind.Residential), InteriorArchetype.None, 0));
        }

        [Theory]
        [InlineData("ResidentialLow", 100f, 100f, 50f, InteriorArchetype.House)]
        [InlineData("ResidentialLowEco", 8f, 8f, 5f, InteriorArchetype.House)]
        [InlineData("ResidentialHigh", 5f, 5f, 3f, InteriorArchetype.ApartmentLobby)]
        [InlineData("ResidentialHighEco", 30f, 30f, 60f, InteriorArchetype.ApartmentLobby)]
        [InlineData("", 20f, 20f, 12f, InteriorArchetype.House)]
        [InlineData("", 10f, 40f, 12f, InteriorArchetype.House)]
        [InlineData("", 0f, 0f, 0f, InteriorArchetype.House)]
        [InlineData("", 20f, 20f, 12.1f, InteriorArchetype.ApartmentLobby)]
        [InlineData("", 20.1f, 20f, 5f, InteriorArchetype.ApartmentLobby)]
        [InlineData("", 40f, 40f, 40f, InteriorArchetype.ApartmentLobby)]
        public void ResidentialRule(string sub, float w, float l, float h, InteriorArchetype expected)
        {
            for (int id = 0; id < 20; id++)
                Assert.Equal(expected, InteriorGenerator.ArchetypeFor(Building(id, ServiceKind.Residential, sub, w, l, h)));
        }

        [Theory]
        [InlineData("CommercialLeisure")]
        [InlineData("CommercialTourist")]
        public void LeisureAndTouristCommercialAreRestaurants(string sub)
        {
            for (int id = 0; id < 300; id++)
                Assert.Equal(InteriorArchetype.Restaurant, InteriorGenerator.ArchetypeFor(Building(id, ServiceKind.Commercial, sub)));
        }

        [Fact]
        public void CommercialDistributionMatchesTenTwentyFiveTwentyFortyFive()
        {
            foreach (string sub in new[] { "", "CommercialHigh", "CommercialLow", "CommercialEco" })
            {
                var counts = new Dictionary<InteriorArchetype, int>();
                const int n = 2000;
                for (int id = 0; id < n; id++)
                {
                    var b = Building(id, ServiceKind.Commercial, sub, name: "Shop" + id);
                    InteriorArchetype a = InteriorGenerator.ArchetypeFor(b);
                    Assert.Equal(a, InteriorGenerator.ArchetypeFor(b));
                    int c;
                    counts.TryGetValue(a, out c);
                    counts[a] = c + 1;
                }
                Assert.Equal(4, counts.Count);
                AssertShare(counts, InteriorArchetype.GunStore, n, 0.10);
                AssertShare(counts, InteriorArchetype.ClothesShop, n, 0.25);
                AssertShare(counts, InteriorArchetype.Restaurant, n, 0.20);
                AssertShare(counts, InteriorArchetype.ConvenienceStore, n, 0.45);
            }
        }

        [Fact]
        public void CommercialChoiceDependsOnPrefabNameToo()
        {
            var seen = new HashSet<InteriorArchetype>();
            for (int i = 0; i < 400; i++)
                seen.Add(InteriorGenerator.ArchetypeFor(Building(77, ServiceKind.Commercial, "", name: "Prefab" + i)));
            Assert.True(seen.Count >= 3, "same id with many prefab names should give several shop kinds");
        }

        [Fact]
        public void IndustrialIsAboutTwentyPercentGarage()
        {
            foreach (string sub in new[] { "", "IndustrialGeneric", "IndustrialFarming", "IndustrialOil", "IndustrialOre" })
            {
                int garages = 0;
                const int n = 2000;
                for (int id = 0; id < n; id++)
                {
                    InteriorArchetype a = InteriorGenerator.ArchetypeFor(Building(id, ServiceKind.Industrial, sub, name: "Factory" + id));
                    Assert.True(a == InteriorArchetype.Garage || a == InteriorArchetype.Warehouse, a.ToString());
                    if (a == InteriorArchetype.Garage) garages++;
                }
                double share = garages / (double)n;
                Assert.InRange(share, 0.15, 0.25);
            }
        }

        [Theory]
        [InlineData(ServiceKind.Office, "", InteriorArchetype.Office)]
        [InlineData(ServiceKind.Office, "OfficeGeneric", InteriorArchetype.Office)]
        [InlineData(ServiceKind.Police, "", InteriorArchetype.PoliceStation)]
        [InlineData(ServiceKind.Police, "Low", InteriorArchetype.PoliceStation)]
        [InlineData(ServiceKind.Health, "", InteriorArchetype.Hospital)]
        [InlineData(ServiceKind.Health, "HealthCare", InteriorArchetype.Hospital)]
        [InlineData(ServiceKind.Fire, "", InteriorArchetype.FireStation)]
        [InlineData(ServiceKind.Education, "", InteriorArchetype.School)]
        [InlineData(ServiceKind.Education, "University", InteriorArchetype.School)]
        [InlineData(ServiceKind.Garbage, "", InteriorArchetype.ServiceHall)]
        [InlineData(ServiceKind.Electricity, "", InteriorArchetype.ServiceHall)]
        [InlineData(ServiceKind.Water, "", InteriorArchetype.ServiceHall)]
        [InlineData(ServiceKind.Transport, "PublicTransportBus", InteriorArchetype.ServiceHall)]
        [InlineData(ServiceKind.Deathcare, "", InteriorArchetype.ServiceHall)]
        [InlineData(ServiceKind.Government, "", InteriorArchetype.ServiceHall)]
        [InlineData(ServiceKind.Monument, "", InteriorArchetype.ServiceHall)]
        [InlineData(ServiceKind.Other, "", InteriorArchetype.Generic)]
        [InlineData(ServiceKind.Other, "Leisure", InteriorArchetype.Generic)]
        public void ServiceMapping(ServiceKind service, string sub, InteriorArchetype expected)
        {
            for (int id = 0; id < 40; id++)
                Assert.Equal(expected, InteriorGenerator.ArchetypeFor(Building(id * 101, service, sub, 5f + id, 90f - id, 3f * id)));
        }

        [Theory]
        [MemberData(nameof(AllServices))]
        public void GenerateDefaultsToArchetypeForVariantZero(ServiceKind service)
        {
            for (int id = 1; id <= 12; id++)
            {
                var b = Building(id * 311, service, id % 2 == 0 ? "ResidentialHigh" : "", 20f + id, 18f, 14f);
                InteriorArchetype expected = InteriorGenerator.ArchetypeFor(b);
                InteriorLayout l = InteriorGenerator.Generate(b);
                if (expected == InteriorArchetype.None)
                {
                    Assert.Null(l);
                    continue;
                }
                Assert.NotNull(l);
                Assert.Equal(expected, l.Archetype);
                Assert.Equal(0, l.Variant);
                Assert.Equal(b.Id, l.BuildingId);
            }
        }

        public static IEnumerable<object[]> AllServices()
        {
            foreach (ServiceKind s in Enum.GetValues(typeof(ServiceKind))) yield return new object[] { s };
        }

        private static void AssertShare(Dictionary<InteriorArchetype, int> counts, InteriorArchetype a, int n, double expected)
        {
            int c;
            counts.TryGetValue(a, out c);
            double share = c / (double)n;
            Assert.True(Math.Abs(share - expected) <= 0.05, a + " share " + share + " expected " + expected);
        }
    }
}
