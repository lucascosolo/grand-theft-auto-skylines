using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Police;
using GtaSkylines.Game.Tests.CrimeTests;
using Xunit;

namespace GtaSkylines.Game.Tests.PoliceTests
{
    public class PoliceSpawnerTests
    {
        // A 21 x 21 grid with 100 m blocks: x, z from -1000 to 1000.
        private static Scene Town()
        {
            var s = new Scene(grid: false);
            s.UseGrid(21, 100f, -1000f, -1000f);
            return s;
        }

        private static void Station(Scene s, ushort id, float x, float z)
        {
            s.City.AddBuilding(new BuildingDescriptor
            {
                Id = id, Name = "Police HQ", Service = ServiceKind.Police, Position = new Vec3(x, 0f, z), Doors = new[] { new Vec3(x, 0f, z) },
            });
        }

        [Fact]
        public void A_station_within_one_and_a_half_kilometres_is_used_and_the_unit_appears_near_its_door()
        {
            var s = Town();
            Station(s, 9, 700f, 0f);
            var sp = new PoliceSpawner(5u);
            Assert.True(sp.TryPick(s.World, Vec3.Zero, out SpawnPoint p));
            Assert.True(p.FromStation);
            Assert.Equal(9, p.Station);
            Assert.InRange(Vec3.DistanceXZ(p.Ground, new Vec3(700f, 0f, 0f)), 0f, PoliceSpawner.StationRoadSnap);
        }

        [Fact]
        public void The_nearest_station_wins()
        {
            var s = Town();
            Station(s, 3, 900f, 0f);
            Station(s, 4, 400f, 0f);
            Assert.True(new PoliceSpawner(5u).TryPick(s.World, Vec3.Zero, out SpawnPoint p));
            Assert.Equal(4, p.Station);
        }

        [Fact]
        public void Without_a_station_in_range_the_unit_appears_two_fifty_to_four_hundred_metres_away_on_a_road_out_of_view()
        {
            var s = Town();
            Station(s, 9, 1900f, 0f);                            // more than 1.5 km away
            s.World.ViewPosition = new Vec3(0f, 2f, 0f);
            var sp = new PoliceSpawner(5u);
            for (int i = 0; i < 8; i++)
            {
                Assert.True(sp.TryPick(s.World, Vec3.Zero, out SpawnPoint p), "pick " + i);
                Assert.False(p.FromStation);
                Assert.Equal(0, p.Station);
                Assert.InRange(Vec3.DistanceXZ(p.Ground, Vec3.Zero), PoliceSpawner.MinDistance, PoliceSpawner.MaxDistance);
                Assert.False(s.World.CanPlayerSee(p.Ground + new Vec3(0f, 1f, 0f)));
                Assert.True(s.World.Roads.TryNearestPoint(p.Ground, 5f, out _));
            }
            Assert.Equal(8, sp.RequestCount);
        }

        [Fact]
        public void A_city_with_no_station_still_gets_off_screen_units()
        {
            var s = Town();
            Assert.True(new PoliceSpawner(2u).TryPick(s.World, Vec3.Zero, out SpawnPoint p));
            Assert.False(p.FromStation);
        }

        [Fact]
        public void No_roads_no_spawn_point()
        {
            var s = new Scene();
            Assert.False(new PoliceSpawner(2u).TryPick(s.World, Vec3.Zero, out _));
        }

        [Fact]
        public void The_same_seed_picks_the_same_points_and_other_seeds_differ()
        {
            List<Vec3> Picks(uint seed)
            {
                var s = Town();
                var sp = new PoliceSpawner(seed);
                var l = new List<Vec3>();
                for (int i = 0; i < 5; i++) { sp.TryPick(s.World, Vec3.Zero, out SpawnPoint p); l.Add(p.Ground); }
                return l;
            }
            Assert.Equal(Picks(9u), Picks(9u));
            Assert.NotEqual(Picks(9u), Picks(10u));
        }
    }
}
