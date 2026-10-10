using GtaSkylines.Game.Crime;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Missions;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Session;
using GtaSkylines.Game.Settings;
using GtaSkylines.Game.Tests.CrimeTests;
using GtaSkylines.Game.Tests.Fakes;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.Refactor
{
    /// <summary>CrimeWantedControl routes mission wanted writes through CrimeSystem.RaiseStars; GameSession wires it.</summary>
    public class CrimeWantedControlTests
    {
        private static readonly Vec3 Spot = new Vec3(30f, 0f, 40f);

        private static Scene Make() { return new Scene(policeCars: false); }

        // ---- RaiseStars --------------------------------------------------------------------------------------------

        [Fact]
        public void RaiseStars_takes_the_maximum_capped_at_five()
        {
            Scene s = Make();
            s.Crime.RaiseStars(s.World, 3, Spot);
            Assert.Equal(3, s.Wanted.Stars);
            s.Crime.RaiseStars(s.World, 2, Spot);
            Assert.Equal(3, s.Wanted.Stars);
            s.Crime.RaiseStars(s.World, 9, Spot);
            Assert.Equal(5, s.Wanted.Stars);
        }

        [Fact]
        public void RaiseStars_sets_the_last_known_position_and_the_search_state()
        {
            Scene s = Make();
            s.Wanted.Evading = true;
            s.Wanted.SecondsSinceSeen = 12f;
            s.Crime.RaiseStars(s.World, 2, Spot);
            Assert.True(s.Wanted.HasLastKnownPosition);
            Assert.Equal(Spot, s.Wanted.LastKnownPosition);
            Assert.Equal(Spot, s.Wanted.SearchCentre);
            Assert.Equal(0f, s.Wanted.SecondsSinceSeen);
            Assert.False(s.Wanted.Evading);
        }

        [Fact]
        public void RaiseStars_resets_severity_since_star_when_stars_rose()
        {
            Scene s = Make();
            s.MakeWanted(1);
            s.Wanted.Episode.SeveritySinceStar = 2f;
            s.Crime.RaiseStars(s.World, 3, Spot);
            Assert.Equal(0f, s.Wanted.Episode.SeveritySinceStar);
        }

        [Fact]
        public void RaiseStars_lifts_player_and_district_heat_to_the_star_floor()
        {
            Scene s = Make();
            byte district = s.World.City.DistrictAt(Spot.X, Spot.Z);
            s.Crime.RaiseStars(s.World, 3, Spot);
            float floor = 3 * PlayerProfile.HeatPerStar;
            Assert.True(s.Wanted.PlayerHeat >= floor, "player heat " + s.Wanted.PlayerHeat);
            Assert.True(s.Wanted.GetDistrictHeat(district) >= floor, "district heat " + s.Wanted.GetDistrictHeat(district));
        }

        [Fact]
        public void RaiseStars_never_lowers_heat_that_is_already_above_the_floor()
        {
            Scene s = Make();
            byte district = s.World.City.DistrictAt(Spot.X, Spot.Z);
            s.Wanted.PlayerHeat = 95f;
            s.Wanted.SetDistrictHeat(district, 90f);
            s.Crime.RaiseStars(s.World, 1, Spot);
            Assert.Equal(95f, s.Wanted.PlayerHeat, 3);
            Assert.Equal(90f, s.Wanted.GetDistrictHeat(district), 3);
        }

        [Fact]
        public void RaiseStars_describes_the_visible_player()
        {
            Scene s = Make();
            s.Crime.RaiseStars(s.World, 1, Spot);
            Assert.NotEqual(SuspectDescription.Unknown.OutfitModel, s.Wanted.Description.OutfitModel);
            Assert.Equal(s.Player.Model, s.Wanted.Description.OutfitModel);
        }

        [Fact]
        public void CrimeWantedControl_RaiseStars_and_ReportCrime_both_go_through_the_crime_system()
        {
            Scene s = Make();
            IWantedControl control = new CrimeWantedControl(s.Crime);
            s.Wanted.PlayerHeat = 0f;
            control.RaiseStars(s.World, 2, Spot);
            Assert.Equal(2, s.Wanted.Stars);
            Assert.True(s.Wanted.PlayerHeat >= 2 * PlayerProfile.HeatPerStar);
            Assert.Equal(s.Player.Model, s.Wanted.Description.OutfitModel);

            s.Wanted.Reset();
            s.Wanted.PlayerHeat = 0f;
            control.ReportCrime(s.World, 3, Spot);
            Assert.Equal(3, s.Wanted.Stars);
            Assert.Equal(Spot, s.Wanted.LastKnownPosition);
            Assert.True(s.Wanted.PlayerHeat >= 3 * PlayerProfile.HeatPerStar);
            Assert.Equal(s.Player.Model, s.Wanted.Description.OutfitModel);
        }

        // ---- wiring ------------------------------------------------------------------------------------------------

        private static GameSession NewSession() { return new GameSession(new FakeCity(), new GameSettings(), new PlayerProfile(), 5u); }

        [Fact]
        public void Adding_the_crime_system_before_the_runner_wires_the_crime_control()
        {
            GameSession session = NewSession();
            session.AddSystem(new CrimeSystem(new FakePedServices(), 5u));
            var runner = new MissionRunner(session.World, 5u);
            Assert.Same(DirectWantedControl.Instance, runner.WantedControl);
            session.AddSystem(runner);
            Assert.IsType<CrimeWantedControl>(runner.WantedControl);
            Assert.Same(runner.WantedControl, runner.Context.Wanted);
        }

        [Fact]
        public void Adding_the_runner_before_the_crime_system_wires_the_crime_control()
        {
            GameSession session = NewSession();
            var runner = new MissionRunner(session.World, 5u);
            session.AddSystem(runner);
            Assert.Same(DirectWantedControl.Instance, runner.WantedControl);
            session.AddSystem(new CrimeSystem(new FakePedServices(), 5u));
            Assert.IsType<CrimeWantedControl>(runner.WantedControl);
            Assert.Same(runner.WantedControl, runner.Context.Wanted);
        }

        [Fact]
        public void A_runner_without_a_crime_system_keeps_the_direct_control()
        {
            GameSession session = NewSession();
            var runner = new MissionRunner(session.World, 5u);
            session.AddSystem(runner);
            Assert.Same(DirectWantedControl.Instance, runner.WantedControl);
            Assert.Same(DirectWantedControl.Instance, runner.Context.Wanted);
        }

        [Fact]
        public void A_custom_wanted_control_is_not_replaced_by_the_wiring()
        {
            GameSession session = NewSession();
            var runner = new MissionRunner(session.World, 5u);
            IWantedControl custom = new CrimeWantedControl(new CrimeSystem(new FakePedServices(), 1u));
            runner.WantedControl = custom;
            session.AddSystem(runner);
            session.AddSystem(new CrimeSystem(new FakePedServices(), 5u));
            Assert.Same(custom, runner.WantedControl);
        }
    }
}
