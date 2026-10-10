using GtaSkylines.Game.Crime;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.CrimeTests
{
    public class CrimeClassifierTests
    {
        private static GameEvent Ev(GameEventType t, int a, int b, double time = 0.0, float value = 0f)
        {
            return new GameEvent { Type = t, ActorA = a, ActorB = b, GameTime = time, Value = value, Position = Vec3.Zero };
        }

        private static CrimeType Classify(CrimeClassifier c, Scene s, GameEvent e, out int victim, out bool loud)
        {
            return c.Classify(s.World, e, out victim, out loud);
        }

        [Theory]
        [InlineData(CrimeType.CarStolen, 1f)] [InlineData(CrimeType.CarJacked, 2f)] [InlineData(CrimeType.Assault, 1f)]
        [InlineData(CrimeType.PedKnockedDown, 1f)] [InlineData(CrimeType.HitAndRun, 2f)] [InlineData(CrimeType.PedInjured, 2f)]
        [InlineData(CrimeType.PedKilled, 3f)] [InlineData(CrimeType.Gunshot, 1f)] [InlineData(CrimeType.ShotAtPed, 2f)]
        [InlineData(CrimeType.Explosion, 3f)] [InlineData(CrimeType.Trespass, 1f)] [InlineData(CrimeType.Robbery, 3f)]
        [InlineData(CrimeType.RecklessDriving, 1f)] [InlineData(CrimeType.AssaultOnCop, 3f)] [InlineData(CrimeType.CopKilled, 4f)]
        [InlineData(CrimeType.ResistingArrest, 2f)] [InlineData(CrimeType.PoliceCarStolen, 3f)] [InlineData(CrimeType.Threatening, 1f)]
        [InlineData(CrimeType.None, 0f)]
        public void Severity_table_matches_the_contract(CrimeType type, float severity)
        {
            Assert.Equal(severity, CrimeClassifier.SeverityOf(type), 3);
        }

        [Fact]
        public void Only_gunfire_and_explosions_are_loud()
        {
            Assert.True(CrimeClassifier.IsLoud(CrimeType.Gunshot));
            Assert.True(CrimeClassifier.IsLoud(CrimeType.ShotAtPed));
            Assert.True(CrimeClassifier.IsLoud(CrimeType.Explosion));
            Assert.False(CrimeClassifier.IsLoud(CrimeType.Assault));
            Assert.False(CrimeClassifier.IsLoud(CrimeType.CarJacked));
        }

        [Fact]
        public void Punching_a_civilian_is_assault_and_the_victim_is_recorded()
        {
            var s = new Scene(policeCars: false);
            Ped civ = s.Civilian(5, 0);
            CrimeType t = Classify(new CrimeClassifier(), s, Ev(GameEventType.Punched, s.Player.Tag, civ.Tag), out int victim, out bool loud);
            Assert.Equal(CrimeType.Assault, t);
            Assert.Equal(civ.Tag, victim);
            Assert.False(loud);
        }

        [Fact]
        public void Shoving_a_civilian_is_assault_but_a_cop_is_assault_on_a_cop()
        {
            var s = new Scene(policeCars: false);
            Ped civ = s.Civilian(5, 0), cop = s.Cop(8, 0);
            var c = new CrimeClassifier();
            Assert.Equal(CrimeType.Assault, Classify(c, s, Ev(GameEventType.Shove, s.Player.Tag, civ.Tag), out _, out _));
            Assert.Equal(CrimeType.AssaultOnCop, Classify(c, s, Ev(GameEventType.Shove, s.Player.Tag, cop.Tag), out _, out _));
        }

        [Fact]
        public void Not_the_player_acting_is_not_a_crime()
        {
            var s = new Scene(policeCars: false);
            Ped a = s.Civilian(5, 0), b = s.Civilian(8, 0);
            Assert.Equal(CrimeType.None, Classify(new CrimeClassifier(), s, Ev(GameEventType.Punched, a.Tag, b.Tag), out _, out _));
        }

        [Fact]
        public void Shooting_at_a_civilian_is_loud_shot_at_ped_and_at_a_cop_assault_on_cop()
        {
            var s = new Scene(policeCars: false);
            Ped civ = s.Civilian(5, 0), cop = s.Cop(8, 0);
            var c = new CrimeClassifier();
            Assert.Equal(CrimeType.ShotAtPed, Classify(c, s, Ev(GameEventType.Shot, s.Player.Tag, civ.Tag), out _, out bool loud1));
            Assert.True(loud1);
            Assert.Equal(CrimeType.AssaultOnCop, Classify(c, s, Ev(GameEventType.Shot, s.Player.Tag, cop.Tag), out _, out bool loud2));
            Assert.True(loud2);
        }

        [Fact]
        public void A_shot_that_hits_no_person_is_nothing_but_the_gunshot_event_is_a_loud_crime()
        {
            var s = new Scene(policeCars: false);
            var c = new CrimeClassifier();
            Assert.Equal(CrimeType.None, Classify(c, s, Ev(GameEventType.Shot, s.Player.Tag, 0), out _, out _));
            Assert.Equal(CrimeType.Gunshot, Classify(c, s, Ev(GameEventType.Gunshot, s.Player.Tag, 0), out int victim, out bool loud));
            Assert.True(loud);
            Assert.Equal(0, victim);
        }

        [Fact]
        public void Explosion_by_the_player_or_a_player_owned_car_is_loud_explosion()
        {
            var s = new Scene(policeCars: false);
            Car mine = s.World.SpawnCar(0, new Vec3(20, 0, 0), 0f, CarOwner.Player, 0xFFFFFF);
            Car other = s.World.SpawnCar(0, new Vec3(30, 0, 0), 0f, CarOwner.Civilian, 0xFFFFFF);
            var c = new CrimeClassifier();
            Assert.Equal(CrimeType.Explosion, Classify(c, s, Ev(GameEventType.Explosion, s.Player.Tag, 0), out _, out bool l1));
            Assert.Equal(CrimeType.Explosion, Classify(c, s, Ev(GameEventType.Explosion, mine.Tag, 0), out _, out bool l2));
            Assert.Equal(CrimeType.None, Classify(c, s, Ev(GameEventType.Explosion, other.Tag, 0), out _, out _));
            Assert.True(l1 && l2);
        }

        [Fact]
        public void Stealing_a_parked_car_is_car_stolen_and_a_police_car_police_car_stolen()
        {
            var s = new Scene(policeCars: false);
            Car parked = s.World.SpawnCar(0, new Vec3(20, 0, 0), 0f, CarOwner.Civilian, 0xFFFFFF);
            var c = new CrimeClassifier();
            Assert.Equal(CrimeType.CarStolen, Classify(c, s, Ev(GameEventType.CarStolen, s.Player.Tag, parked.Tag, 0.0, 0f), out int victim, out _));
            Assert.Equal(parked.Tag, victim);
            Assert.Equal(CrimeType.PoliceCarStolen, Classify(c, s, Ev(GameEventType.CarStolen, s.Player.Tag, parked.Tag, 0.0, 1f), out _, out _));
        }

        [Fact]
        public void Jacking_a_civilian_car_is_car_jacked_and_a_police_car_police_car_stolen()
        {
            var s = new Scene(policeCars: false);
            s.City.AddVehicleModel(new GtaSkylines.Game.City.VehicleModel { Id = 1, Name = "Police Car", Kind = GtaSkylines.Game.City.VehicleKind.Police, HasSiren = true });
            Car civ = s.World.SpawnCar(0, new Vec3(20, 0, 0), 0f, CarOwner.Civilian, 0xFFFFFF);
            Car cop = s.World.SpawnCar(1, new Vec3(30, 0, 0), 0f, CarOwner.Police, 0xFFFFFF);
            var c = new CrimeClassifier();
            Assert.Equal(CrimeType.CarJacked, Classify(c, s, Ev(GameEventType.CarJacked, s.Player.Tag, civ.Tag), out _, out _));
            Assert.Equal(CrimeType.PoliceCarStolen, Classify(c, s, Ev(GameEventType.CarJacked, s.Player.Tag, cop.Tag), out _, out _));
        }

        [Fact]
        public void Knocking_a_civilian_down_with_the_players_car_is_a_minor_crime_and_injuring_one_is_hit_and_run()
        {
            var s = new Scene(policeCars: false);
            Car car = s.PlayerInCar(0, 0);
            Ped civ = s.Civilian(5, 0);
            var c = new CrimeClassifier();
            Assert.Equal(CrimeType.PedKnockedDown, Classify(c, s, Ev(GameEventType.PedKnockedDown, car.Tag, civ.Tag), out _, out _));
            Assert.Equal(CrimeType.HitAndRun, Classify(c, s, Ev(GameEventType.PedInjured, car.Tag, civ.Tag), out _, out _));
        }

        [Fact]
        public void Hurting_a_cop_by_any_means_is_assault_on_a_cop()
        {
            var s = new Scene(policeCars: false);
            Car car = s.PlayerInCar(0, 0);
            Ped cop = s.Cop(5, 0);
            var c = new CrimeClassifier();
            Assert.Equal(CrimeType.AssaultOnCop, Classify(c, s, Ev(GameEventType.PedKnockedDown, car.Tag, cop.Tag), out _, out _));
            Assert.Equal(CrimeType.AssaultOnCop, Classify(c, s, Ev(GameEventType.PedInjured, car.Tag, cop.Tag), out _, out _));
        }

        [Fact]
        public void Injuring_a_civilian_on_foot_is_ped_injured_and_knocking_one_down_on_foot_is_assault()
        {
            var s = new Scene(policeCars: false);
            Ped civ = s.Civilian(5, 0);
            var c = new CrimeClassifier();
            Assert.Equal(CrimeType.PedInjured, Classify(c, s, Ev(GameEventType.PedInjured, s.Player.Tag, civ.Tag), out _, out _));
            Assert.Equal(CrimeType.Assault, Classify(c, s, Ev(GameEventType.PedKnockedDown, s.Player.Tag, civ.Tag), out _, out _));
        }

        [Fact]
        public void Killing_a_civilian_is_ped_killed_and_a_cop_cop_killed_also_from_the_players_car()
        {
            var s = new Scene(policeCars: false);
            Car car = s.PlayerInCar(0, 0);
            Ped civ = s.Civilian(5, 0), cop = s.Cop(8, 0);
            var c = new CrimeClassifier();
            Assert.Equal(CrimeType.PedKilled, Classify(c, s, Ev(GameEventType.PedKilled, s.Player.Tag, civ.Tag), out _, out _));
            Assert.Equal(CrimeType.PedKilled, Classify(c, s, Ev(GameEventType.PedKilled, car.Tag, civ.Tag), out _, out _));
            Assert.Equal(CrimeType.CopKilled, Classify(c, s, Ev(GameEventType.PedKilled, s.Player.Tag, cop.Tag), out _, out _));
            Assert.Equal(CrimeType.CopKilled, Classify(c, s, Ev(GameEventType.PedKilled, car.Tag, cop.Tag), out _, out _));
        }

        [Fact]
        public void Trespass_and_robbery_are_crimes()
        {
            var s = new Scene(policeCars: false);
            var c = new CrimeClassifier();
            Assert.Equal(CrimeType.Trespass, Classify(c, s, Ev(GameEventType.Trespass, s.Player.Tag, 0), out _, out _));
            Assert.Equal(CrimeType.Robbery, Classify(c, s, Ev(GameEventType.Robbery, s.Player.Tag, s.Civilian(5, 0).Tag), out _, out _));
        }

        [Fact]
        public void Aiming_a_weapon_at_a_civilian_is_threatening_but_fists_or_a_cop_target_are_not()
        {
            var s = new Scene(policeCars: false);
            Ped civ = s.Civilian(5, 0), cop = s.Cop(8, 0);
            var c = new CrimeClassifier();
            s.Player.CurrentWeapon = (int)WeaponId.Fists;
            Assert.Equal(CrimeType.None, Classify(c, s, Ev(GameEventType.AimedAt, s.Player.Tag, civ.Tag), out _, out _));
            s.Player.CurrentWeapon = (int)WeaponId.Pistol;
            Assert.Equal(CrimeType.Threatening, Classify(c, s, Ev(GameEventType.AimedAt, s.Player.Tag, civ.Tag), out _, out _));
            Assert.Equal(CrimeType.None, Classify(c, s, Ev(GameEventType.AimedAt, s.Player.Tag, cop.Tag), out _, out _));
        }

        [Fact]
        public void Three_reckless_events_within_twenty_seconds_make_one_reckless_driving_crime()
        {
            var s = new Scene(policeCars: false);
            Car car = s.PlayerInCar(0, 0);
            var c = new CrimeClassifier();
            Assert.Equal(CrimeType.None, Classify(c, s, Ev(GameEventType.RecklessDriving, car.Tag, 0, 100.0), out _, out _));
            Assert.Equal(CrimeType.None, Classify(c, s, Ev(GameEventType.RecklessDriving, car.Tag, 0, 110.0), out _, out _));
            Assert.Equal(CrimeType.RecklessDriving, Classify(c, s, Ev(GameEventType.RecklessDriving, car.Tag, 0, 119.9), out _, out _));
        }

        [Fact]
        public void A_third_reckless_event_after_the_window_is_not_a_crime()
        {
            var s = new Scene(policeCars: false);
            Car car = s.PlayerInCar(0, 0);
            var c = new CrimeClassifier();
            Classify(c, s, Ev(GameEventType.RecklessDriving, car.Tag, 0, 100.0), out _, out _);
            Classify(c, s, Ev(GameEventType.RecklessDriving, car.Tag, 0, 110.0), out _, out _);
            Assert.Equal(CrimeType.None, Classify(c, s, Ev(GameEventType.RecklessDriving, car.Tag, 0, 121.0), out _, out _));
        }

        [Fact]
        public void The_reckless_memory_is_forgotten_after_a_crime_and_on_reset()
        {
            var s = new Scene(policeCars: false);
            Car car = s.PlayerInCar(0, 0);
            var c = new CrimeClassifier();
            for (int i = 0; i < 3; i++) Classify(c, s, Ev(GameEventType.RecklessDriving, car.Tag, 0, 100.0 + i), out _, out _);
            // the next two events alone must not complete a triple
            Assert.Equal(CrimeType.None, Classify(c, s, Ev(GameEventType.RecklessDriving, car.Tag, 0, 104.0), out _, out _));
            Assert.Equal(CrimeType.None, Classify(c, s, Ev(GameEventType.RecklessDriving, car.Tag, 0, 105.0), out _, out _));
            c.Reset();
            Assert.Equal(CrimeType.None, Classify(c, s, Ev(GameEventType.RecklessDriving, car.Tag, 0, 106.0), out _, out _));
            Assert.Equal(CrimeType.None, Classify(c, s, Ev(GameEventType.RecklessDriving, car.Tag, 0, 107.0), out _, out _));
        }
    }
}
