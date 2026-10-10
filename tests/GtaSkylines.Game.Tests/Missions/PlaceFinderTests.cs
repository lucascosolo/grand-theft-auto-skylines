using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Missions;
using GtaSkylines.Game.Roads;
using Xunit;

namespace GtaSkylines.Game.Tests.Missions
{
    public class PlaceFinderTests
    {
        private static PlaceFinder Finder(TestCity t, bool roads = true)
        {
            RoadGraph g = roads ? TestCity.Grid().Build() : new RoadGraph();
            return new PlaceFinder(t.City, g);
        }

        [Fact]
        public void ConstructorRejectsNulls()
        {
            Assert.Throws<ArgumentNullException>(() => new PlaceFinder(null, new RoadGraph()));
            Assert.Throws<ArgumentNullException>(() => new PlaceFinder(new Fakes.FakeCity(), null));
        }

        [Fact]
        public void FullCityPicksTheRealContactBuildings()
        {
            var t = TestCity.Full();
            CampaignPlaces p = Finder(t).FindCampaignPlaces();
            Assert.Equal(InteriorArchetype.Garage, p.Garage.Archetype);
            Assert.Equal(ServiceKind.Industrial, p.Garage.Service);
            Assert.Equal(t.Police, p.PoliceStation.Building);
            Assert.Equal(t.CityHall, p.CityHall.Building);
            Assert.Equal("Central Precinct", p.PoliceStation.Name);
            Assert.False(p.Garage.FallbackUsed);
            Assert.False(p.PoliceStation.FallbackUsed);
            Assert.False(p.CityHall.FallbackUsed);
        }

        [Fact]
        public void GaragePrefersTheGarageNearestTheCityCentre()
        {
            var t = TestCity.Full();
            PlaceFinder f = Finder(t);
            Vec3 centre = f.CityCentre();
            float d1 = Vec3.DistanceXZ(t.City.Buildings[t.Garage].Position, centre);
            float d2 = Vec3.DistanceXZ(t.City.Buildings[t.Garage2].Position, centre);
            ushort expected = d1 <= d2 ? t.Garage : t.Garage2;
            Assert.Equal(expected, f.FindCampaignPlaces().Garage.Building);
        }

        [Fact]
        public void CampaignPlacesDescribeNameDoorStreetAndDistrict()
        {
            var t = TestCity.Full();
            CampaignPlace hall = Finder(t).FindCampaignPlaces().CityHall;
            Assert.Equal("Testville City Hall", hall.Name);
            Assert.Equal(t.Door(), hall.Door);
            Assert.Equal("Main Street", hall.Street);
            Assert.Equal(1, hall.DistrictId);
            Assert.Equal("Old Town", hall.District);
            Assert.Equal(ServiceKind.Government, hall.Service);
        }

        [Fact]
        public void CitiesWithoutThoseServicesFallBackToDistinctExistingBuildings()
        {
            var t = TestCity.Sparse();
            CampaignPlaces p = Finder(t).FindCampaignPlaces();
            var ids = new HashSet<ushort> { p.Garage.Building, p.PoliceStation.Building, p.CityHall.Building };
            Assert.Equal(3, ids.Count);
            foreach (ushort id in ids) Assert.True(t.City.Buildings.ContainsKey(id));
            Assert.True(p.Garage.FallbackUsed);
            Assert.True(p.PoliceStation.FallbackUsed);
            Assert.True(p.CityHall.FallbackUsed);
        }

        [Fact]
        public void IndustryWithoutAGarageArchetypeStillGivesAnIndustrialGarageContact()
        {
            var t = new TestCity();
            TestCity.Common(t.City);
            t.Warehouse = t.Add(ServiceKind.Industrial, InteriorArchetype.Warehouse, "Only Warehouse", 600, 600, 1);
            t.Home1 = t.Add(ServiceKind.Residential, InteriorArchetype.House, "Elm House", 400, 450, 1);
            CampaignPlace garage = Finder(t).FindCampaignPlaces().Garage;
            Assert.Equal(t.Warehouse, garage.Building);
            Assert.True(garage.FallbackUsed);
        }

        [Fact]
        public void CityHallFallsBackToTheBiggestOfficeBuilding()
        {
            var t = TestCity.Sparse();
            ushort small = t.Add(ServiceKind.Office, InteriorArchetype.Office, "Small Office", 600, 600, 1);
            ushort big = t.Add(ServiceKind.Office, InteriorArchetype.Office, "Big Office", 1200, 1200, 2);
            t.City.Buildings[big].Width = 60f;
            t.City.Buildings[big].Length = 60f;
            t.City.Buildings[big].Height = 90f;
            Assert.NotEqual(small, big);
            Assert.Equal(big, Finder(t).FindCampaignPlaces().CityHall.Building);
        }

        [Fact]
        public void AnEmptyCityGivesRoadPointPlacesNamedNowhere()
        {
            var t = TestCity.Empty();
            CampaignPlaces p = Finder(t).FindCampaignPlaces();
            foreach (CampaignPlace place in new[] { p.Garage, p.PoliceStation, p.CityHall })
            {
                Assert.NotNull(place);
                Assert.Equal(0, place.Building);
                Assert.Equal(PlaceFinder.NowhereName, place.Name);
            }
        }

        [Fact]
        public void EmptyCityAndEmptyRoadsStillAnswer()
        {
            var f = Finder(TestCity.Empty(), roads: false);
            CampaignPlace c = f.ConvenienceStore(new Vec3(10, 0, 10));
            Assert.Equal(PlaceFinder.NowhereName, c.Name);
            Assert.Equal(new Vec3(10, 0, 10), c.Door);
        }

        [Fact]
        public void CityCentreIsTheMeanOfAllBuildings()
        {
            var t = TestCity.Sparse();
            float x = 0, z = 0;
            foreach (BuildingDescriptor b in t.City.Buildings.Values) { x += b.Position.X; z += b.Position.Z; }
            int n = t.City.Buildings.Count;
            Vec3 c = Finder(t).CityCentre();
            Assert.Equal(x / n, c.X, 1);
            Assert.Equal(z / n, c.Z, 1);
            Assert.Equal(Vec3.Zero, Finder(TestCity.Empty()).CityCentre());
        }

        [Fact]
        public void MostCentralSkipsExcludedBuildings()
        {
            var t = TestCity.Sparse();
            PlaceFinder f = Finder(t);
            CampaignPlace first = f.MostCentral(null);
            CampaignPlace second = f.MostCentral(new List<ushort> { first.Building });
            Assert.NotEqual(first.Building, second.Building);
        }

        [Fact]
        public void DescribeKnownAndUnknownBuildings()
        {
            var t = TestCity.Full();
            PlaceFinder f = Finder(t);
            CampaignPlace p = f.Describe(t.Store);
            Assert.Equal("Quick Stop Market", p.Name);
            Assert.Equal(t.Store, p.Building);
            Assert.Equal(InteriorArchetype.ConvenienceStore, p.Archetype);
            Assert.Equal(ServiceKind.Commercial, p.Service);
            Assert.Null(f.Describe(60000));
        }

        [Fact]
        public void ConvenienceStoreIsTheNearestStoreOtherwiseCommercial()
        {
            var t = TestCity.Full();
            PlaceFinder f = Finder(t);
            Assert.Equal(t.Store, f.ConvenienceStore(new Vec3(520, 0, 700)).Building);
            Assert.Equal(t.Store2, f.ConvenienceStore(new Vec3(1450, 0, 900)).Building);
            var sparse = TestCity.Sparse();
            sparse.City.Buildings.Remove(sparse.Store);
            Assert.Equal(sparse.Shop2, Finder(sparse).ConvenienceStore(new Vec3(0, 0, 0)).Building);
        }

        [Fact]
        public void HospitalIsHealthThenCommercialThenCentral()
        {
            var full = TestCity.Full();
            Assert.Equal(full.Hospital, Finder(full).Hospital(new Vec3(1500, 0, 1500)).Building);
            var sparse = TestCity.Sparse();
            CampaignPlace h = Finder(sparse).Hospital(new Vec3(500, 0, 500));
            Assert.Equal(ServiceKind.Commercial, h.Service);
            var none = TestCity.Empty();
            Assert.Equal(PlaceFinder.NowhereName, Finder(none).Hospital(Vec3.Zero).Name);
        }

        [Fact]
        public void WarehouseHonoursTheMinimumDistanceAndPrefersWarehouseArchetype()
        {
            var t = TestCity.Full();
            PlaceFinder f = Finder(t);
            Vec3 near = t.City.Buildings[t.Warehouse].Doors[0];
            Assert.Equal(t.Warehouse, f.Warehouse(near, 0f).Building);
            CampaignPlace far = f.Warehouse(near, 100f);
            Assert.True(Vec3.DistanceXZ(far.Door, near) >= 100f);
            Assert.Equal(ServiceKind.Industrial, far.Service);
        }

        [Fact]
        public void IndustrialFallsBackToOfficeThenCentral()
        {
            var sparse = TestCity.Sparse();
            ushort office = sparse.Add(ServiceKind.Office, InteriorArchetype.Office, "Meridian Tower", 800, 800, 1);
            Assert.Equal(office, Finder(sparse).Industrial(Vec3.Zero, 0f).Building);
            var none = TestCity.Sparse();
            Assert.NotEqual(0, Finder(none).Industrial(Vec3.Zero, 0f).Building);
        }

        [Fact]
        public void NearestWithMinDistanceSkipsCloseBuildings()
        {
            var t = TestCity.Full();
            PlaceFinder f = Finder(t);
            Vec3 p = t.City.Buildings[t.Police].Doors[0];
            CampaignPlace near = f.Nearest(ServiceKind.Police, InteriorArchetype.None, p, 0f);
            Assert.Equal(t.Police, near.Building);
            Assert.Null(f.Nearest(ServiceKind.Police, InteriorArchetype.None, p, 50f));
            Assert.Null(f.Nearest(ServiceKind.Fire, InteriorArchetype.None, p, 0f));
        }

        [Fact]
        public void InBandPicksInsideTheBandDeterministically()
        {
            var t = TestCity.Full();
            PlaceFinder f = Finder(t);
            Vec3 origin = new Vec3(600, 0, 600);
            var services = new[] { ServiceKind.Residential, ServiceKind.Commercial };
            for (uint seed = 1; seed < 20; seed++)
            {
                CampaignPlace a = f.InBand(services, origin, 300f, 1000f, new Rng(seed));
                CampaignPlace b = f.InBand(services, origin, 300f, 1000f, new Rng(seed));
                Assert.Equal(a.Building, b.Building);
                float d = Vec3.DistanceXZ(a.Door, origin);
                Assert.InRange(d, 300f, 1000f);
                Assert.Contains(a.Service, services);
            }
        }

        [Fact]
        public void InBandWithNobodyInTheBandTakesTheClosestToIt()
        {
            var t = TestCity.Full();
            PlaceFinder f = Finder(t);
            // Only homes: Home1 (450,558) is ~160 m from (600,600); band is 2000-3000 m, so the farthest home is closest to it.
            var homes = new[] { ServiceKind.Residential };
            CampaignPlace p = f.InBand(homes, new Vec3(600, 0, 600), 2000f, 3000f, new Rng(1));
            float best = -1f;
            ushort expect = 0;
            foreach (ushort id in new[] { t.Home1, t.Home2, t.Home3 })
            {
                float d = Vec3.DistanceXZ(t.City.Buildings[id].Doors[0], new Vec3(600, 0, 600));
                if (d > best) { best = d; expect = id; }
            }
            Assert.Equal(expect, p.Building);
        }

        [Fact]
        public void InBandWithMissingServicesUsesAnyBuilding()
        {
            var t = TestCity.Sparse();
            CampaignPlace p = Finder(t).InBand(new[] { ServiceKind.Fire }, new Vec3(600, 0, 600), 0f, 5000f, new Rng(3));
            Assert.NotEqual(0, p.Building);
        }

        [Fact]
        public void HottestDistrictPicksAResidentialOrCommercialBuildingThere()
        {
            var t = TestCity.Full();
            var profile = new GtaSkylines.Game.Profile.PlayerProfile();
            profile.DistrictHeat[2] = 60f;
            profile.DistrictHeat[1] = 10f;
            CampaignPlace p = Finder(t).HottestDistrict(profile, new Vec3(600, 0, 600), new Rng(5));
            Assert.Equal(2, p.DistrictId);
            Assert.True(p.Service == ServiceKind.Residential || p.Service == ServiceKind.Commercial);
        }

        [Fact]
        public void HottestDistrictWithoutHeatUsesTheBand()
        {
            var t = TestCity.Full();
            var profile = new GtaSkylines.Game.Profile.PlayerProfile();
            Vec3 origin = new Vec3(600, 0, 600);
            CampaignPlace p = Finder(t).HottestDistrict(profile, origin, new Rng(5));
            Assert.InRange(Vec3.DistanceXZ(p.Door, origin), 500f, 1200f);
        }

        [Fact]
        public void NearestRoadPointSnapsToTheGraphOrReturnsThePoint()
        {
            var t = TestCity.Full();
            Vec3 snapped = Finder(t).NearestRoadPoint(new Vec3(310, 0, 195));
            RoadPoint rp;
            Assert.True(TestCity.Grid().Build().TryNearestPoint(snapped, 1f, out rp));
            Vec3 p = new Vec3(7, 0, 9);
            Assert.Equal(p, Finder(t, roads: false).NearestRoadPoint(p));
        }

        [Fact]
        public void RoadPointAtLiesOnTheRoadsAboutTheRequestedDistance()
        {
            var t = TestCity.Full();
            RoadGraph g = TestCity.Grid().Build();
            var f = new PlaceFinder(t.City, g);
            Vec3 origin = new Vec3(1400, 0, 1400);
            for (uint seed = 1; seed < 10; seed++)
            {
                Vec3 p = f.RoadPointAt(origin, 300f, new Rng(seed));
                RoadPoint rp;
                Assert.True(g.TryNearestPoint(p, 1f, out rp), "on a road");
                Assert.InRange(Vec3.DistanceXZ(p, origin), 150f, 450f);
            }
        }
    }

    internal static class TestCityExtensions
    {
        public static Vec3 Door(this TestCity t) { return t.City.Buildings[t.CityHall].Doors[0]; }
    }
}
