using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Police;
using GtaSkylines.Game.Tests.CityTests;
using GtaSkylines.Game.Tests.CrimeTests;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.PoliceTests
{
    public class CopModelTests
    {
        private static readonly int[] PoliceIds = { 1, 6 };
        private static readonly int[] ServiceIds = { 0, 2, 5 };

        private static Scene Town(uint seed, bool withPolice)
        {
            var s = new Scene(seed: seed);
            s.UseGrid(21, 100f, -1000f, -1000f);
            s.City.AddBuilding(new BuildingDescriptor
            {
                Id = 9, Service = ServiceKind.Police, Position = new Vec3(300f, 0f, 0f), Doors = new[] { new Vec3(300f, 0f, 0f) },
            });
            // Without police models the service models (0, 2, 5) must still be avoided; 4 is a senior, 3 and 7 are adults.
            Roles.Apply(s.City, withPolice ? PoliceIds : new int[0], ServiceIds);
            return s;
        }

        private static List<Ped> Cops(Scene s, bool inCars)
        {
            var l = new List<Ped>();
            foreach (Ped p in s.World.Peds)
                if (p.Kind == PedKind.Cop && (p.VehicleId != 0) == inCars) l.Add(p);
            return l;
        }

        private static List<Ped> InsideCops(Scene s)
        {
            var l = new List<Ped>();
            foreach (Ped p in s.World.Peds) if (p.Kind == PedKind.Cop && p.Position.Y < -1000f) l.Add(p);
            return l;
        }

        private static List<Ped> CarCops(Scene s)
        {
            s.MakeWanted(2);
            s.RunUntil(() => s.Police.ActiveUnitCount > 0, 30f);
            return Cops(s, true);
        }

        [Theory]
        [InlineData(1u)] [InlineData(2u)] [InlineData(3u)] [InlineData(4u)] [InlineData(5u)]
        public void Cops_in_police_cars_wear_police_models_when_the_city_has_any(uint seed)
        {
            Scene s = Town(seed, withPolice: true);
            List<Ped> cops = CarCops(s);
            Assert.NotEmpty(cops);
            foreach (Ped p in cops) Assert.True(Roles.IsPolice(s.City, p.Model), "model " + p.Model + " is not a police model");
        }

        [Theory]
        [InlineData(1u)] [InlineData(2u)] [InlineData(3u)] [InlineData(4u)] [InlineData(5u)]
        public void Without_police_models_cops_wear_adult_civilian_models_never_service_ones(uint seed)
        {
            Scene s = Town(seed, withPolice: false);
            List<Ped> cops = CarCops(s);
            Assert.NotEmpty(cops);
            foreach (Ped p in cops)
            {
                CitizenModel m = s.City.GetCitizenModel(p.Model);
                Assert.Equal(CitizenModelRole.Civilian, m.Role);
                Assert.Contains(m.AgeGroup, new[] { 2, 3 });
            }
        }

        [Theory]
        [InlineData(1u, true)] [InlineData(2u, true)] [InlineData(1u, false)] [InlineData(2u, false)]
        public void The_cops_that_enter_an_interior_wear_the_same_kind_of_models(uint seed, bool withPolice)
        {
            Scene s = Town(seed, withPolice);
            BuildingDescriptor b = SessionTests.Sim.FindBuilding(InteriorArchetype.ConvenienceStore, ServiceKind.Commercial, "", new Vec3(60f, 0f, 60f), 90f);
            InteriorState it = s.World.Interior;
            it.Building = b;
            it.Layout = InteriorGenerator.Generate(b);
            it.Origin = InteriorState.OriginFor(b);
            it.Phase = InteriorPhase.Inside;
            s.MakeWanted(PoliceSystem.InteriorEntryStars);
            s.RunUntil(() => InsideCops(s).Count > 0, 45f);

            List<Ped> inside = InsideCops(s);
            Assert.NotEmpty(inside);
            foreach (Ped p in inside)
            {
                CitizenModel m = s.City.GetCitizenModel(p.Model);
                if (withPolice) Assert.Equal(CitizenModelRole.Police, m.Role);
                else
                {
                    Assert.Equal(CitizenModelRole.Civilian, m.Role);
                    Assert.Contains(m.AgeGroup, new[] { 2, 3 });
                }
            }
        }
    }
}
