using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Police;
using GtaSkylines.Game.Roads;
using GtaSkylines.Game.Settings;
using GtaSkylines.Game.Tests.CrimeTests;
using GtaSkylines.Game.Tests.Roads;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.PoliceTests
{
    public class DispatcherTests
    {
        // ---- budgets (implemented data; pins) -------------------------------------------------------------------------------

        [Theory]
        [InlineData(0, 0, 0, false)] [InlineData(1, 1, 0, false)] [InlineData(2, 3, 0, false)] [InlineData(3, 4, 1, false)]
        [InlineData(4, 6, 2, true)] [InlineData(5, 8, 3, true)]
        public void Normal_budget_per_star(int stars, int cars, int roadblocks, bool helicopter)
        {
            UnitBudget b = Dispatcher.BudgetFor(stars, PoliceDifficulty.Normal, true);
            Assert.Equal(cars, b.Cars);
            Assert.Equal(roadblocks, b.Roadblocks);
            Assert.Equal(helicopter, b.Helicopter);
            Assert.Equal(stars == 5, b.Aggressive);
        }

        [Fact]
        public void Without_a_helicopter_model_four_stars_and_up_add_two_cars()
        {
            Assert.Equal(8, Dispatcher.BudgetFor(4, PoliceDifficulty.Normal, false).Cars);
            Assert.False(Dispatcher.BudgetFor(4, PoliceDifficulty.Normal, false).Helicopter);
            Assert.Equal(10, Dispatcher.BudgetFor(5, PoliceDifficulty.Normal, false).Cars);
        }

        [Fact]
        public void Difficulty_scales_the_budget()
        {
            Assert.Equal(2, Dispatcher.BudgetFor(3, PoliceDifficulty.Easy, true).Cars);     // 4 x 0.6 = 2.4
            Assert.Equal(6, Dispatcher.BudgetFor(3, PoliceDifficulty.Hard, true).Cars);     // 4 x 1.4 = 5.6
            Assert.Equal(1, Dispatcher.BudgetFor(1, PoliceDifficulty.Easy, true).Cars);     // never below one car
        }

        [Fact]
        public void Patrols_exist_only_from_district_heat_twenty_up_to_three()
        {
            Assert.Equal(0, Dispatcher.PatrolCount(19.9f, PoliceDifficulty.Normal));
            Assert.Equal(1, Dispatcher.PatrolCount(20f, PoliceDifficulty.Normal));
            Assert.Equal(2, Dispatcher.PatrolCount(60f, PoliceDifficulty.Normal));
            Assert.Equal(3, Dispatcher.PatrolCount(100f, PoliceDifficulty.Normal));
        }

        [Fact]
        public void Response_delay_shrinks_with_heat_and_scales_with_difficulty()
        {
            Assert.Equal(20f, Dispatcher.ResponseDelay(0f, PoliceDifficulty.Normal), 3);
            Assert.Equal(6f, Dispatcher.ResponseDelay(100f, PoliceDifficulty.Normal), 3);
            Assert.Equal(28f, Dispatcher.ResponseDelay(0f, PoliceDifficulty.Easy), 3);
            Assert.Equal(4.2f, Dispatcher.ResponseDelay(100f, PoliceDifficulty.Hard), 3);
        }

        // ---- role assignment ----------------------------------------------------------------------------------------------

        private sealed class Fleet
        {
            public readonly Scene Scene = new Scene(policeCars: true);
            public readonly List<PoliceUnit> Units = new List<PoliceUnit>();
            private int nextId = 1;

            public PoliceUnit Add(float x, float z, bool sees, UnitRole role = UnitRole.Respond)
            {
                Car car = Scene.World.SpawnCar(1, new Vec3(x, 0f, z), 0f, CarOwner.Police, 0xFFFFFF);
                var u = new PoliceUnit { Id = nextId++, Kind = UnitKind.Car, Role = role, CarId = car.Id, SeesPlayer = sees };
                u.Observer.Eye = car.Position + new Vec3(0f, 0.6f, 0f);
                Units.Add(u);
                return u;
            }

            public int Assign(Dispatcher d, DispatchContext ctx) { return d.AssignRoles(Scene.World, ctx, Units); }
        }

        private static DispatchContext Driving(int stars = 3, float speed = 15f, bool seen = true)
        {
            return new DispatchContext
            {
                Stars = stars, PlayerPosition = new Vec3(0f, 0f, 100f), PlayerVelocity = new Vec3(0f, 0f, speed),
                PlayerHeadingDeg = 0f, InCar = true, Seen = seen,
            };
        }

        private static int Count(IList<PoliceUnit> units, UnitRole role)
        {
            int n = 0;
            foreach (PoliceUnit u in units) if (u.Role == role) n++;
            return n;
        }

        [Fact]
        public void Not_wanted_units_are_on_patrol()
        {
            var f = new Fleet();
            f.Add(50, 0, false); f.Add(80, 0, false);
            f.Assign(new Dispatcher(1u), new DispatchContext { Stars = 0, PlayerPosition = Vec3.Zero });
            Assert.Equal(2, Count(f.Units, UnitRole.Patrol));
        }

        [Fact]
        public void The_nearest_unit_that_sees_the_player_pursues()
        {
            var f = new Fleet();
            PoliceUnit nearest = f.Add(0, 70, true);
            f.Add(0, 40, true); f.Add(0, -20, true);
            f.Units.Reverse();                                   // slot order must not matter
            f.Assign(new Dispatcher(1u), Driving());
            Assert.Equal(UnitRole.Pursue, nearest.Role);
        }

        [Fact]
        public void Pursuers_never_get_every_unit_one_in_three_intercepts_or_searches()
        {
            var f = new Fleet();
            PoliceUnit p1 = f.Add(0, 70, true), p2 = f.Add(0, 60, true), p3 = f.Add(0, 50, true), far = f.Add(0, 0, true);
            f.Assign(new Dispatcher(1u), Driving());
            int nonPursue = Count(f.Units, UnitRole.Intercept) + Count(f.Units, UnitRole.Search);
            Assert.True(nonPursue >= 1);                          // floor(4 / 3)
            Assert.Equal(UnitRole.Pursue, p1.Role);               // the nearest keeps pursuing
            Assert.Equal(UnitRole.Intercept, far.Role);           // the farthest pursuer is converted (car chase)
            Assert.True(Count(f.Units, UnitRole.Pursue) >= 1);
        }

        [Fact]
        public void A_unit_that_cannot_see_a_driving_player_who_is_seen_intercepts_ahead_on_the_road()
        {
            var f = new Fleet();
            f.Scene.World.Roads.Rebuild(Nets.Straight(2000f, speed: 15f).Nodes, Nets.Straight(2000f, speed: 15f).Segments, false);
            PoliceUnit spotter = f.Add(10, 90, true);
            PoliceUnit other = f.Add(300, 600, false);
            f.Assign(new Dispatcher(5u), Driving(stars: 2, speed: 15f));
            Assert.Equal(UnitRole.Pursue, spotter.Role);
            Assert.Equal(UnitRole.Intercept, other.Role);
            Assert.InRange(other.Target.Z, 100f + 15f * 6f - 8f, 100f + 15f * 10f + 8f);
            Assert.InRange(other.Target.X, -8f, 8f);
        }

        [Fact]
        public void Evading_units_search_one_sector_of_the_zone_each()
        {
            var f = new Fleet();
            PoliceUnit a = f.Add(50, 0, false), b = f.Add(-50, 0, false), c = f.Add(0, -50, false);
            var ctx = new DispatchContext { Stars = 2, PlayerPosition = new Vec3(900, 0, 900), SearchCentre = Vec3.Zero, SearchRadius = 100f, Seen = false, InCar = true };
            f.Assign(new Dispatcher(3u), ctx);
            var slots = new HashSet<int>();
            foreach (PoliceUnit u in f.Units)
            {
                Assert.Equal(UnitRole.Search, u.Role);
                slots.Add(u.Slot);
                Near.Equal(SearchZone.SectorCentre(Vec3.Zero, 100f, u.Slot, 3), u.Target, 0.5f);
            }
            Assert.Equal(3, slots.Count);
        }

        [Fact]
        public void A_stopped_players_car_is_boxed_in_by_the_three_nearest_units_in_distinct_slots()
        {
            var f = new Fleet();
            PoliceUnit u1 = f.Add(0, 110, true), u2 = f.Add(0, 120, true), u3 = f.Add(0, 130, true), u4 = f.Add(0, 190, true);
            var ctx = Driving(stars: 3, speed: 0f);
            ctx.SlowSeconds = 2.5f;
            f.Assign(new Dispatcher(1u), ctx);
            Assert.Equal(UnitRole.BoxIn, u1.Role); Assert.Equal(UnitRole.BoxIn, u2.Role); Assert.Equal(UnitRole.BoxIn, u3.Role);
            Assert.NotEqual(UnitRole.BoxIn, u4.Role);
            Assert.Equal(3, new HashSet<int> { u1.Slot, u2.Slot, u3.Slot }.Count);
            foreach (int s in new[] { u1.Slot, u2.Slot, u3.Slot }) Assert.InRange(s, 0, 2);
        }

        [Fact]
        public void Boxing_in_needs_two_seconds_of_slowness_and_never_happens_at_five_stars()
        {
            var brief = new Fleet();
            brief.Add(0, 110, true);
            var c1 = Driving(stars: 3, speed: 0f); c1.SlowSeconds = 1.5f;
            brief.Assign(new Dispatcher(1u), c1);
            Assert.Equal(0, Count(brief.Units, UnitRole.BoxIn));

            var aggressive = new Fleet();
            aggressive.Add(0, 110, true);
            var c2 = Driving(stars: 5, speed: 0f); c2.SlowSeconds = 3f;
            aggressive.Assign(new Dispatcher(1u), c2);
            Assert.Equal(0, Count(aggressive.Units, UnitRole.BoxIn));
        }

        [Fact]
        public void Units_within_twenty_five_metres_of_a_player_on_foot_get_out()
        {
            var f = new Fleet();
            PoliceUnit near = f.Add(0, 20, true), far = f.Add(0, 60, true);
            var ctx = new DispatchContext { Stars = 2, PlayerPosition = Vec3.Zero, InCar = false, Seen = true };
            f.Assign(new Dispatcher(1u), ctx);
            Assert.Equal(UnitRole.OnFoot, near.Role);
            Assert.NotEqual(UnitRole.OnFoot, far.Role);
        }

        [Fact]
        public void A_fleeing_fast_car_is_hit_by_a_pit_from_behind_at_three_stars_and_up()
        {
            Fleet Make(out PoliceUnit behind)
            {
                var f = new Fleet();
                f.Scene.PlayerInCar(0, 100);
                behind = f.Add(-2, 92, true);
                f.Add(0, 40, true); f.Add(0, 20, true);
                return f;
            }
            var f3 = Make(out PoliceUnit pit);
            f3.Assign(new Dispatcher(1u), Driving(stars: 3, speed: 15f));
            Assert.Equal(UnitRole.Pit, pit.Role);

            var f2 = Make(out PoliceUnit noPit2);
            f2.Assign(new Dispatcher(1u), Driving(stars: 2, speed: 15f));
            Assert.NotEqual(UnitRole.Pit, noPit2.Role);

            var slow = Make(out PoliceUnit noPitSlow);
            slow.Assign(new Dispatcher(1u), Driving(stars: 3, speed: 10f));
            Assert.NotEqual(UnitRole.Pit, noPitSlow.Role);
        }

        [Fact]
        public void Roadblock_and_helicopter_units_keep_their_roles()
        {
            var f = new Fleet();
            PoliceUnit block = f.Add(0, 300, false, UnitRole.Roadblock);
            block.IsRoadblock = true;
            var heli = new PoliceUnit { Id = 50, Kind = UnitKind.Helicopter, Role = UnitRole.Helicopter };
            f.Units.Add(heli);
            f.Add(0, 50, true);
            f.Assign(new Dispatcher(1u), Driving());
            Assert.Equal(UnitRole.Roadblock, block.Role);
            Assert.Equal(UnitRole.Helicopter, heli.Role);
        }

        [Fact]
        public void AssignRoles_returns_the_number_of_changes_and_resets_the_role_clock_only_for_them()
        {
            var f = new Fleet();
            PoliceUnit a = f.Add(0, 50, true), b = f.Add(0, 60, true);
            a.RoleSeconds = 9f; b.RoleSeconds = 9f;
            var d = new Dispatcher(1u);
            int first = f.Assign(d, Driving(stars: 2));
            Assert.True(first >= 1);
            foreach (PoliceUnit u in f.Units) u.RoleSeconds = 5f;
            int second = f.Assign(d, Driving(stars: 2));
            Assert.Equal(0, second);
            Assert.Equal(5f, a.RoleSeconds, 3);
            Assert.Equal(2, d.AssignmentCount);
        }

        [Fact]
        public void The_same_seed_assigns_the_same_intercept_targets()
        {
            Vec3 Target(uint seed)
            {
                var f = new Fleet();
                var net = Nets.Straight(2000f, speed: 15f);
                f.Scene.World.Roads.Rebuild(net.Nodes, net.Segments, false);
                f.Add(0, 90, true);
                PoliceUnit other = f.Add(300, 600, false);
                f.Assign(new Dispatcher(seed), Driving(stars: 2));
                return other.Target;
            }
            Assert.Equal(Target(21u), Target(21u));
        }

        // ---- roadblock junctions ------------------------------------------------------------------------------------------

        private static Fleet RoadWithJunctions()
        {
            var f = new Fleet();
            var net = new Net()
                .Node(1, 0, 0).Node(2, 0, 100).Node(3, 0, 300).Node(4, 0, 450).Node(5, 0, 700)
                .Node(12, 200, 100).Node(13, 200, 300).Node(14, 200, 450)
                .Road(1, 1, 2).Road(2, 2, 3).Road(3, 3, 4).Road(4, 4, 5)
                .Road(12, 2, 12).Road(13, 3, 13).Road(14, 4, 14);
            f.Scene.World.Roads.Rebuild(net.Nodes, net.Segments, false);
            return f;
        }

        [Fact]
        public void Roadblocks_go_to_junctions_two_hundred_to_five_hundred_metres_ahead_nearest_first()
        {
            var f = RoadWithJunctions();
            var ctx = new DispatchContext { Stars = 3, PlayerPosition = Vec3.Zero, PlayerHeadingDeg = 0f, PlayerVelocity = new Vec3(0, 0, 15), InCar = true };
            var found = new List<Vec3> { new Vec3(9, 9, 9) };
            int n = new Dispatcher(1u).FindRoadblockJunctions(f.Scene.World, ctx, 5, found);
            Assert.Equal(2, n);                                  // 100 m is too close, 700 m too far
            Assert.Equal(2, found.Count);
            Near.Equal(new Vec3(0, 0, 300), found[0], 1f);
            Near.Equal(new Vec3(0, 0, 450), found[1], 1f);
        }

        [Fact]
        public void FindRoadblockJunctions_returns_at_most_the_requested_count()
        {
            var f = RoadWithJunctions();
            var ctx = new DispatchContext { Stars = 3, PlayerPosition = Vec3.Zero, PlayerHeadingDeg = 0f, PlayerVelocity = new Vec3(0, 0, 15), InCar = true };
            var found = new List<Vec3>();
            Assert.Equal(1, new Dispatcher(1u).FindRoadblockJunctions(f.Scene.World, ctx, 1, found));
            Near.Equal(new Vec3(0, 0, 300), found[0], 1f);
        }
    }
}
