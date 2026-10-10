using System;
using System.Globalization;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Police;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Settings;
using GtaSkylines.Game.Tests.Fakes;
using GtaSkylines.Game.Vehicles;
using GtaSkylines.Game.World;
using GtaSkylines.Mod.Debug;
using Xunit;

namespace GtaSkylines.Mod.Tests.DebugTests
{
    public class DebugTextTests
    {
        private sealed class Brain : IPedBrain
        {
            private readonly string _name;
            public Brain(string name) { _name = name; }
            public string Name { get { return _name; } }
            public bool IsActive { get { return false; } }
            public void Step(GameWorld world, Ped ped, float dt) { }
        }

        private static GameWorld World()
        {
            return new GameWorld(new FakeCity(), new GameSettings(), new PlayerProfile(), 1u);
        }

        [Theory]
        [InlineData(UnitRole.Patrol, "Patrol")]
        [InlineData(UnitRole.Respond, "Respond")]
        [InlineData(UnitRole.Pursue, "Pursue")]
        [InlineData(UnitRole.Intercept, "Intercept")]
        [InlineData(UnitRole.Search, "Search")]
        [InlineData(UnitRole.Roadblock, "Roadblock")]
        [InlineData(UnitRole.BoxIn, "BoxIn")]
        [InlineData(UnitRole.Pit, "PIT")]
        [InlineData(UnitRole.OnFoot, "OnFoot")]
        [InlineData(UnitRole.Helicopter, "Helicopter")]
        [InlineData(UnitRole.Leave, "Leave")]
        public void RoleName_names_every_role(UnitRole role, string expected)
        {
            Assert.Equal(expected, DebugText.RoleName(role));
        }

        [Fact]
        public void RoleName_undefined_is_question_mark_and_names_are_cached()
        {
            Assert.Equal("?", DebugText.RoleName((UnitRole)99));
            Assert.Same(DebugText.RoleName(UnitRole.Pursue), DebugText.RoleName(UnitRole.Pursue));
        }

        [Fact]
        public void Ped_basic_line_without_brain()
        {
            GameWorld w = World();
            Ped p = w.SpawnPed(PedKind.Civilian, 0, Vec3.Zero, 0f);
            p.Anim = PedAnim.Idle;
            p.Health = 87.4f;
            Assert.Equal("Civ #" + p.Id + " -/Idle hp 87", DebugText.Ped(p));
        }

        [Fact]
        public void Ped_kind_names()
        {
            GameWorld w = World();
            Assert.StartsWith("Player #", DebugText.Ped(w.SpawnPed(PedKind.Player, 0, Vec3.Zero, 0f)));
            Assert.StartsWith("Cop #", DebugText.Ped(w.SpawnPed(PedKind.Cop, 0, Vec3.Zero, 0f)));
            Assert.StartsWith("Npc #", DebugText.Ped(w.SpawnPed(PedKind.Npc, 0, Vec3.Zero, 0f)));
        }

        [Fact]
        public void Ped_appends_armour_vehicle_and_dead_in_order()
        {
            GameWorld w = World();
            Ped p = w.SpawnPed(PedKind.Cop, 0, Vec3.Zero, 0f);
            p.Anim = PedAnim.Idle;
            p.Brain = new Brain("Flee");
            p.Health = 50f;
            p.Armour = 25f;
            p.VehicleId = 12;
            p.Flags |= PedFlags.Dead;
            Assert.Equal("Cop #" + p.Id + " Flee/Idle hp 50 ar 25 in #12 DEAD", DebugText.Ped(p));
        }

        [Fact]
        public void Ped_omits_zero_armour_zero_vehicle_and_alive()
        {
            GameWorld w = World();
            Ped p = w.SpawnPed(PedKind.Civilian, 0, Vec3.Zero, 0f);
            p.Anim = PedAnim.Idle;
            string s = DebugText.Ped(p);
            Assert.DoesNotContain(" ar ", s);
            Assert.DoesNotContain(" in #", s);
            Assert.DoesNotContain("DEAD", s);
        }

        [Fact]
        public void Car_line_uses_absolute_kmh_and_damage_percent()
        {
            GameWorld w = World();
            Car c = w.SpawnCar(0, new Vec3(20f, 0f, 0f), 0f, CarOwner.Police, 0);
            Vec3 f = c.Body.Rotation.Forward;
            c.Body.SetVelocity(new Vec3(-10f * f.X, -10f * f.Y, -10f * f.Z), Vec3.Zero);
            Assert.True(c.Body.Speed < 0f);
            c.Body.ApplyDamage(30f);
            string kmh = (Math.Abs(c.Body.Speed) * 3.6).ToString("0", CultureInfo.InvariantCulture);
            Assert.NotEqual("0", kmh);
            Assert.Equal("Police car #" + c.Id + " drv - " + kmh + " km/h dmg 30%", DebugText.Car(c));
        }

        [Fact]
        public void Car_shows_driver_id_and_owner_names()
        {
            GameWorld w = World();
            Car c = w.SpawnCar(0, Vec3.Zero, 0f, CarOwner.Civilian, 0);
            c.Seats[0] = 7;
            Assert.StartsWith("Civ car #" + c.Id + " drv #7 0 km/h dmg 0%", DebugText.Car(c));
            c.Owner = CarOwner.Player;
            Assert.StartsWith("Player car #", DebugText.Car(c));
            c.Owner = CarOwner.Mission;
            Assert.StartsWith("Mission car #", DebugText.Car(c));
            c.Owner = CarOwner.Abandoned;
            Assert.StartsWith("Abandoned car #", DebugText.Car(c));
        }

        [Fact]
        public void Car_appends_damage_state_words()
        {
            GameWorld w = World();
            Car c = w.SpawnCar(0, Vec3.Zero, 0f, CarOwner.Civilian, 0);
            Assert.EndsWith("dmg 0%", DebugText.Car(c));
            c.Body.ApplyDamage(70f);
            Assert.Equal(VehicleDamageState.Smoking, c.Body.DamageState);
            Assert.EndsWith("dmg 70% smoking", DebugText.Car(c));
            c.Body.ApplyDamage(20f);
            Assert.Equal(VehicleDamageState.OnFire, c.Body.DamageState);
            Assert.EndsWith("dmg 90% fire", DebugText.Car(c));
        }

        [Fact]
        public void Unit_sees_player()
        {
            var u = new PoliceUnit { Id = 3, Role = UnitRole.Pursue, SeesPlayer = true, Target = new Vec3(10.4f, 0f, -20.6f) };
            Assert.Equal("U3 Pursue sees -> (10, -21)", DebugText.Unit(u));
        }

        [Fact]
        public void Unit_lost_player_shows_seconds()
        {
            var u = new PoliceUnit { Id = 12, Role = UnitRole.Pit, SeesPlayer = false, SecondsSinceSeen = 7.4f, Target = new Vec3(1f, 0f, 2f) };
            Assert.Equal("U12 PIT lost 7s -> (1, 2)", DebugText.Unit(u));
        }

        private static WantedStatus Wanted() { return new WantedStatus(new PlayerProfile()); }

        [Fact]
        public void Wanted_clear()
        {
            WantedStatus w = Wanted();
            w.Stars = 0;
            w.PlayerHeat = 12.4f;
            Assert.Equal("Wanted 0/5 clear\nLKP none\nSearch none\nHeat 12", DebugText.Wanted(w));
        }

        [Fact]
        public void Wanted_seen_with_lkp_and_search()
        {
            WantedStatus w = Wanted();
            w.Stars = 3;
            w.HasLastKnownPosition = true;
            w.LastKnownPosition = new Vec3(100.4f, 0f, 200.6f);
            w.SecondsSinceSeen = 2.55f;
            w.SearchRadius = 150.4f;
            w.SearchCentre = new Vec3(5f, 0f, -6f);
            w.PlayerHeat = 60f;
            Assert.Equal("Wanted 3/5 seen\nLKP (100, 201) 2.6s ago\nSearch r 150 m at (5, -6)\nHeat 60", DebugText.Wanted(w));
        }

        [Fact]
        public void Wanted_evading_and_search_none_when_radius_zero()
        {
            WantedStatus w = Wanted();
            w.Stars = 2;
            w.Evading = true;
            w.SearchRadius = 0f;
            string[] lines = DebugText.Wanted(w).Split('\n');
            Assert.Equal(4, lines.Length);
            Assert.Equal("Wanted 2/5 evading", lines[0]);
            Assert.Equal("LKP none", lines[1]);
            Assert.Equal("Search none", lines[2]);
        }

        [Fact]
        public void Wanted_search_none_when_stars_zero_even_with_radius()
        {
            WantedStatus w = Wanted();
            w.Stars = 0;
            w.SearchRadius = 80f;
            Assert.Equal("Search none", DebugText.Wanted(w).Split('\n')[2]);
        }

        [Fact]
        public void Budget_within_budget()
        {
            Assert.Equal("Sim 1.50 ms / 4.00", DebugText.Budget("Sim", 1.5, 4.0));
        }

        [Fact]
        public void Budget_over_budget_is_flagged_but_equal_is_not()
        {
            Assert.Equal("Sim 4.50 ms / 4.00 OVER", DebugText.Budget("Sim", 4.5, 4.0));
            Assert.Equal("Sim 4.00 ms / 4.00", DebugText.Budget("Sim", 4.0, 4.0));
        }

        [Fact]
        public void Budget_without_budget_omits_limit()
        {
            Assert.Equal("Render 2.25 ms", DebugText.Budget("Render", 2.25, 0));
        }

        [Fact]
        public void Fps_within_budget()
        {
            var w = new PerfWindow(4);
            w.Add(20.0, 1, 1, 1);
            w.Add(20.0, 1, 1, 1);
            Assert.Equal("FPS 50.0  frame 20.0 ms (max 20.0) / 33.3", DebugText.Fps(w));
        }

        [Fact]
        public void Fps_over_budget_flagged()
        {
            var w = new PerfWindow(4);
            w.Add(40.0, 1, 1, 1);
            Assert.Equal("FPS 25.0  frame 40.0 ms (max 40.0) / 33.3 OVER", DebugText.Fps(w));
        }
    }
}
