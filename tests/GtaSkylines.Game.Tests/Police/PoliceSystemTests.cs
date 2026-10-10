using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Police;
using GtaSkylines.Game.Settings;
using GtaSkylines.Game.Tests.CrimeTests;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.PoliceTests
{
    public class PoliceSystemTests
    {
        private static Scene Town(PoliceDifficulty d = PoliceDifficulty.Normal, bool helicopter = false)
        {
            var s = new Scene(difficulty: d, helicopter: helicopter);
            s.UseGrid(21, 100f, -1000f, -1000f);
            s.City.AddBuilding(new BuildingDescriptor
            {
                Id = 9, Service = ServiceKind.Police, Position = new Vec3(300f, 0f, 0f), Doors = new[] { new Vec3(300f, 0f, 0f) },
            });
            return s;
        }

        private static List<PoliceUnit> InUse(Scene s)
        {
            var l = new List<PoliceUnit>();
            foreach (PoliceUnit u in s.Police.Units) if (u.InUse) l.Add(u);
            return l;
        }

        // ---- evasion and the search zone ----------------------------------------------------------------------------

        [Fact]
        public void Unseen_for_two_seconds_the_player_is_evading_and_the_search_zone_is_published()
        {
            var s = new Scene(policeCars: false);
            s.MakeWanted(2);
            s.Run(1.5f);
            Assert.False(s.Wanted.Evading);
            Assert.Equal(0f, s.Wanted.SearchRadius, 3);
            s.Run(1f);
            Assert.True(s.Wanted.Evading);
            Near.Equal(s.Wanted.LastKnownPosition, s.Wanted.SearchCentre, 0.01f);
            Near.Equal(SearchZone.Radius(s.Wanted.SecondsSinceSeen), s.Wanted.SearchRadius, 0.5f);
        }

        [Fact]
        public void The_search_zone_grows_six_metres_per_second()
        {
            var s = new Scene(policeCars: false);
            s.MakeWanted(3);
            s.Run(10f);
            Near.Equal(40f + 6f * 10f, s.Wanted.SearchRadius, 1.5f);
            s.Run(10f);
            Near.Equal(40f + 6f * 20f, s.Wanted.SearchRadius, 1.5f);
        }

        [Theory]
        [InlineData(1, 20f)] [InlineData(2, 30f)] [InlineData(3, 45f)] [InlineData(4, 60f)] [InlineData(5, 90f)]
        public void Stars_clear_after_the_search_time_for_the_level_and_heat_stays(int stars, float seconds)
        {
            var s = new Scene(policeCars: false);
            s.Wanted.PlayerHeat = 50f;
            s.MakeWanted(stars);
            s.Run(seconds - 1.5f);
            Assert.Equal(stars, s.Wanted.Stars);
            s.Run(3f);
            Assert.Equal(0, s.Wanted.Stars);
            Assert.False(s.Wanted.Evading);
            Assert.Equal(0f, s.Wanted.SearchRadius, 3);
            Assert.False(s.Wanted.HasLastKnownPosition);
            Assert.Equal(-1, s.Wanted.Description.OutfitModel);
            Assert.True(s.Wanted.PlayerHeat > 49f);
        }

        [Fact]
        public void A_hot_district_makes_the_search_last_one_and_a_half_times_longer()
        {
            var s = new Scene(policeCars: false);
            s.Wanted.SetDistrictHeat(0, 90f);
            s.MakeWanted(1);
            s.Run(25f);
            Assert.Equal(1, s.Wanted.Stars);                    // a cool district would have cleared at 20 s
            s.Run(7f);
            Assert.Equal(0, s.Wanted.Stars);                    // 30 s
        }

        // ---- spawning, budgets and roles ------------------------------------------------------------------------------

        [Fact]
        public void No_unit_appears_before_the_response_delay_and_the_first_one_leaves_the_station()
        {
            var s = Town();
            s.Wanted.PlayerHeat = 100f;                          // delay 6 s
            s.MakeWanted(1);
            s.Step();
            Assert.InRange(s.Police.ResponseSecondsRemaining, 4f, 6.1f);
            s.Run(4.5f);
            Assert.Equal(0, s.Police.ActiveUnitCount);
            float t = s.RunUntil(() => s.Police.ActiveUnitCount > 0, 8f);
            Assert.True(t > 0f);
            PoliceUnit u = InUse(s)[0];
            Car car = s.World.FindCar(u.CarId);
            Assert.NotNull(car);
            Assert.Equal(CarOwner.Police, car.Owner);
            Assert.Equal(1, car.Model.Id);
            Assert.True(car.Siren);
            Assert.InRange(Vec3.DistanceXZ(car.Position, new Vec3(300f, 0f, 0f)), 0f, 90f);
            Assert.NotEqual(0, car.Seats[0]);
            Assert.NotEqual(0, car.Seats[1]);
            Assert.Equal(PedKind.Cop, s.World.FindPed(car.Seats[0]).Kind);
            Assert.Same(u.Driver, car.Driver);
        }

        [Fact]
        public void A_station_farther_than_one_and_a_half_kilometres_means_an_off_screen_road_spawn()
        {
            var s = Town();
            s.City.Buildings[9].Position = new Vec3(5000f, 0f, 0f);
            s.City.Buildings[9].Doors = new[] { new Vec3(5000f, 0f, 0f) };
            s.Wanted.PlayerHeat = 100f;
            s.MakeWanted(1);
            Assert.True(s.RunUntil(() => s.Police.ActiveUnitCount > 0, 10f) > 0f);
            Car car = s.World.FindCar(InUse(s)[0].CarId);
            Assert.InRange(Vec3.DistanceXZ(car.Position, s.Player.Position), 230f, 420f);
            Assert.False(s.World.CanPlayerSee(car.Position));
        }

        [Theory]
        [InlineData(PoliceDifficulty.Easy)] [InlineData(PoliceDifficulty.Normal)] [InlineData(PoliceDifficulty.Hard)]
        public void Three_stars_fill_the_budget_of_the_difficulty(PoliceDifficulty d)
        {
            var s = Town(d);
            s.Wanted.PlayerHeat = 100f;
            s.MakeWanted(3);
            s.Run(40f);
            UnitBudget b = Dispatcher.BudgetFor(3, d, false);
            Assert.Equal(b.Cars, s.Police.Budget.Cars);
            int blocks = 0, cars = 0;
            foreach (PoliceUnit u in InUse(s)) { if (u.IsRoadblock) blocks++; else cars++; }
            Assert.Equal(b.Cars, cars);
            Assert.True(blocks >= b.Roadblocks && blocks <= 2 * b.Roadblocks, "roadblock units " + blocks);
        }

        [Fact]
        public void At_four_stars_a_helicopter_flies_when_the_city_has_one_else_two_more_cars()
        {
            var with = Town(helicopter: true);
            with.Wanted.PlayerHeat = 100f;
            with.MakeWanted(4);
            with.Run(40f);
            Assert.True(with.Police.Budget.Helicopter);
            Assert.True(with.Police.Helicopter.Active);

            var without = Town();
            without.Wanted.PlayerHeat = 100f;
            without.MakeWanted(4);
            without.Run(40f);
            Assert.False(without.Police.Helicopter.Active);
            Assert.Equal(Dispatcher.BudgetFor(4, PoliceDifficulty.Normal, false).Cars, without.Police.Budget.Cars);
        }

        [Fact]
        public void Units_that_see_the_player_keep_the_last_known_position_fresh()
        {
            var s = Town();
            s.PlacePlayer(300f, 8f);                              // beside the station: units spawn in sight
            s.Wanted.PlayerHeat = 100f;
            s.MakeWanted(1);
            s.Wanted.LastKnownPosition = new Vec3(800f, 0f, 800f);   // stale
            s.Run(40f);
            Assert.True(s.Wanted.Stars > 0, "units that keep seeing the player must not let the stars clear");
            Near.Equal(s.Player.Position, s.Wanted.LastKnownPosition, 3f);
        }

        [Fact]
        public void The_player_dying_ends_the_pursuit_and_removes_every_unit()
        {
            var s = Town();
            s.Wanted.PlayerHeat = 100f;
            s.MakeWanted(2);
            Assert.True(s.RunUntil(() => s.Police.ActiveUnitCount > 0, 12f) > 0f);
            s.World.Emit(GameEventType.PlayerDied, s.Player.Position, s.Player.Tag, 0, 0f, null);
            s.Wanted.Reset();
            s.Run(1f);
            Assert.Equal(0, s.Police.ActiveUnitCount);
            foreach (Car c in s.World.Cars) Assert.NotEqual(CarOwner.Police, c.Owner);
        }

        // ---- patrols --------------------------------------------------------------------------------------------------

        [Fact]
        public void Patrols_appear_only_when_the_district_heat_is_twenty_or_more()
        {
            var cool = Town();
            cool.Wanted.SetDistrictHeat(0, 10f);
            cool.Run(60f);
            Assert.Equal(0, cool.Police.ActiveUnitCount);

            var hot = Town();
            hot.Wanted.SetDistrictHeat(0, 60f);
            hot.Run(60f);
            Assert.True(hot.Police.ActiveUnitCount >= 1);
            foreach (PoliceUnit u in InUse(hot)) Assert.Equal(UnitRole.Patrol, u.Role);
            Assert.Equal(0, hot.Wanted.Stars);
        }

        [Fact]
        public void A_patrol_that_recognises_a_hot_player_sets_one_star_without_adding_heat()
        {
            var s = Town();
            s.PlacePlayer(300f, 8f);                              // beside the station
            s.Wanted.PlayerHeat = 60f;
            s.Wanted.SetDistrictHeat(0, 60f);
            float t = s.RunUntil(() => s.Wanted.Stars > 0, 90f);
            Assert.True(t > 0f, "no patrol recognised the player");
            Assert.Equal(1, s.Wanted.Stars);
            Assert.InRange(s.Wanted.PlayerHeat, 59f, 60.01f);
        }

        // ---- determinism ----------------------------------------------------------------------------------------------

        [Fact]
        public void The_same_seed_and_inputs_give_the_same_units_in_the_same_places()
        {
            List<Vec3> Run(uint seed)
            {
                var s = new Scene(seed);
                s.UseGrid(21, 100f, -1000f, -1000f);
                s.Wanted.PlayerHeat = 100f;
                s.MakeWanted(3);
                s.Run(30f);
                var l = new List<Vec3>();
                foreach (PoliceUnit u in InUse(s)) { Car c = s.World.FindCar(u.CarId); l.Add(c == null ? Vec3.Zero : c.Position); }
                return l;
            }
            Assert.Equal(Run(31u), Run(31u));
        }
    }
}
