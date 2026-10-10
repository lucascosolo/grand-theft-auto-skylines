using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Police;
using GtaSkylines.Game.Settings;
using GtaSkylines.Game.Tests.CrimeTests;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.PoliceTests
{
    /// <summary>The player stands at the origin; the observer stands <c>d</c> metres south of them looking north (+z) unless stated.</summary>
    public class RecognitionTests
    {
        private const int Red = 0xC0392B, Blue = 0x2E5FA8;

        private static Scene Wanted(int stars = 1, PoliceDifficulty diff = PoliceDifficulty.Normal)
        {
            var s = new Scene(policeCars: false, difficulty: diff);
            s.MakeWanted(stars);
            return s;
        }

        private static PoliceObserver Obs(float distance, float yaw = 0f, float height = 1.6f)
        {
            return new PoliceObserver { Eye = new Vec3(0f, height, -distance), YawDeg = yaw, SelfTag = 0, CopTag = 0 };
        }

        private static void Describe(Scene s, int outfit)
        {
            s.Wanted.Description = new SuspectDescription { OutfitModel = outfit, CarModel = -1 };
        }

        private static RecognitionReason Sees(Scene s, ref PoliceObserver o, bool crime = false, float dt = 0.1f)
        {
            return Recognition.Sees(s.World, ref o, crime, dt);
        }

        // ---- description ---------------------------------------------------------------------------------------------

        [Fact]
        public void Describe_on_foot_records_the_outfit_and_no_car()
        {
            var s = Wanted();
            SuspectDescription d = Recognition.Describe(s.World);
            Assert.Equal(s.Player.Model, d.OutfitModel);
            Assert.Equal(-1, d.CarModel);
        }

        [Fact]
        public void Describe_in_a_car_records_its_model_colour_and_id()
        {
            var s = Wanted();
            Car car = s.PlayerInCar(0, 0, colour: Red);
            SuspectDescription d = Recognition.Describe(s.World);
            Assert.Equal(car.Model.Id, d.CarModel);
            Assert.Equal(Red, d.CarColour);
            Assert.Equal(car.Id, d.CarId);
        }

        [Fact]
        public void Matches_compares_outfit_on_foot_and_model_plus_colour_in_a_car_and_unknown_never_matches()
        {
            var s = Wanted();
            Assert.True(Recognition.Matches(s.World, new SuspectDescription { OutfitModel = 3, CarModel = -1 }));
            Assert.False(Recognition.Matches(s.World, new SuspectDescription { OutfitModel = 4, CarModel = -1 }));
            Assert.False(Recognition.Matches(s.World, SuspectDescription.Unknown));
            Car car = s.PlayerInCar(0, 0, colour: Red);
            Assert.True(Recognition.Matches(s.World, new SuspectDescription { OutfitModel = -1, CarModel = car.Model.Id, CarColour = Red }));
            Assert.False(Recognition.Matches(s.World, new SuspectDescription { OutfitModel = -1, CarModel = car.Model.Id, CarColour = Blue }));
            Assert.False(Recognition.Matches(s.World, new SuspectDescription { OutfitModel = -1, CarModel = 99, CarColour = Red }));
        }

        // ---- identification -------------------------------------------------------------------------------------------

        [Fact]
        public void A_cop_at_thirty_metres_recognises_a_matching_player()
        {
            var s = Wanted();
            PoliceObserver o = Obs(30f);
            Assert.Equal(RecognitionReason.Description, Sees(s, ref o));
        }

        [Fact]
        public void After_an_unseen_outfit_change_a_cop_at_thirty_metres_does_not_recognise_but_at_ten_metres_does()
        {
            var s = Wanted();
            s.Player.Model = 6;                              // the cops were told outfit 3
            PoliceObserver far = Obs(30f), near = Obs(10f);
            Assert.Equal(RecognitionReason.None, Sees(s, ref far));
            Assert.Equal(RecognitionReason.Close, Sees(s, ref near));
        }

        [Fact]
        public void After_an_unseen_car_change_a_cop_at_thirty_metres_does_not_recognise_but_at_ten_metres_does()
        {
            var s = Wanted();
            Car car = s.PlayerInCar(0, 0, colour: Blue);
            s.Wanted.Description = new SuspectDescription { OutfitModel = -1, CarModel = car.Model.Id, CarColour = Red, CarId = 77 };
            PoliceObserver far = Obs(30f), near = Obs(10f);
            Assert.Equal(RecognitionReason.None, Sees(s, ref far));
            Assert.Equal(RecognitionReason.Close, Sees(s, ref near));
        }

        [Fact]
        public void A_respray_to_the_described_colour_is_recognised_at_thirty_metres()
        {
            var s = Wanted();
            Car car = s.PlayerInCar(0, 0, colour: Red);
            s.Wanted.Description = new SuspectDescription { OutfitModel = -1, CarModel = car.Model.Id, CarColour = Red };
            PoliceObserver o = Obs(30f);
            Assert.Equal(RecognitionReason.Description, Sees(s, ref o));
        }

        [Fact]
        public void Seeing_a_crime_identifies_a_player_who_does_not_match()
        {
            var s = Wanted();
            s.Player.Model = 6;
            PoliceObserver o = Obs(30f);
            Assert.Equal(RecognitionReason.CrimeInSight, Sees(s, ref o, crime: true));
        }

        // ---- sight ---------------------------------------------------------------------------------------------------

        [Fact]
        public void Day_range_is_sixty_metres()
        {
            var s = Wanted();
            PoliceObserver ok = Obs(55f), no = Obs(65f);
            Assert.Equal(RecognitionReason.Description, Sees(s, ref ok));
            Assert.Equal(RecognitionReason.None, Sees(s, ref no));
        }

        [Fact]
        public void Night_range_is_thirty_five_metres()
        {
            var s = Wanted();
            s.World.Clock.Hour = 23f;
            PoliceObserver ok = Obs(30f), no = Obs(40f);
            Assert.Equal(RecognitionReason.Description, Sees(s, ref ok));
            Assert.Equal(RecognitionReason.None, Sees(s, ref no));
        }

        [Fact]
        public void Sneaking_on_foot_shrinks_the_range_to_sixty_percent()
        {
            var s = Wanted();
            PoliceObserver normal = Obs(40f);
            Assert.Equal(RecognitionReason.Description, Sees(s, ref normal));
            s.Player.Flags |= PedFlags.Sneaking;
            s.World.PlayerState.Sneaking = true;
            PoliceObserver sneaking = Obs(40f);
            Assert.Equal(RecognitionReason.None, Sees(s, ref sneaking));
        }

        [Fact]
        public void The_view_cone_is_one_hundred_twenty_degrees_wide()
        {
            var s = Wanted();
            PoliceObserver inside = Obs(30f, yaw: 55f), outside = Obs(30f, yaw: 65f), behind = Obs(30f, yaw: 180f);
            Assert.Equal(RecognitionReason.Description, Sees(s, ref inside));
            Assert.Equal(RecognitionReason.None, Sees(s, ref outside));
            Assert.Equal(RecognitionReason.None, Sees(s, ref behind));
        }

        [Fact]
        public void A_wall_between_the_cop_and_the_player_blocks_sight()
        {
            var s = Wanted();
            s.World.Collision.AddBox(new Vec3(0f, 2f, -15f), new Vec3(5f, 5f, 0.5f), Quat.Identity, 999);
            PoliceObserver o = Obs(30f);
            Assert.False(Recognition.CanSeePlayer(s.World, o));
            Assert.Equal(RecognitionReason.None, Sees(s, ref o, crime: true));
        }

        [Fact]
        public void CanSeePlayer_is_sight_without_identification()
        {
            var s = Wanted();
            s.Player.Model = 6;
            PoliceObserver o = Obs(30f);
            Assert.True(Recognition.CanSeePlayer(s.World, o));
        }

        [Fact]
        public void The_helicopter_spotlight_extends_range_by_thirty_percent_inside_a_downward_cone()
        {
            var s = Wanted();
            var heli = new PoliceObserver { Eye = new Vec3(0f, 70f, -20f), Helicopter = true };   // 72.8 m away, 16 degrees off straight down
            var tooFar = new PoliceObserver { Eye = new Vec3(0f, 100f, -20f), Helicopter = true };
            var offCone = new PoliceObserver { Eye = new Vec3(0f, 40f, -60f), Helicopter = true }; // 56 degrees off straight down
            Assert.True(Recognition.CanSeePlayer(s.World, heli));
            Assert.False(Recognition.CanSeePlayer(s.World, tooFar));
            Assert.False(Recognition.CanSeePlayer(s.World, offCone));
            var carLike = new PoliceObserver { Eye = new Vec3(0f, 70f, -20f), YawDeg = 0f };
            Assert.False(Recognition.CanSeePlayer(s.World, carLike));
        }

        // ---- cop sight of a world point (crime witnessing) --------------------------------------------------------------

        [Fact]
        public void A_cop_on_foot_sees_a_point_ahead_within_sixty_metres_by_day_only()
        {
            var s = Wanted();
            Ped cop = s.World.SpawnPed(PedKind.Cop, 2, new Vec3(200f, 0f, 200f), 0f);
            cop.YawDeg = 0f;
            Assert.True(Recognition.CopSeesPoint(s.World, cop, new Vec3(200f, 0f, 250f)));
            Assert.False(Recognition.CopSeesPoint(s.World, cop, new Vec3(200f, 0f, 270f)));
            Assert.False(Recognition.CopSeesPoint(s.World, cop, new Vec3(200f, 0f, 150f)));     // behind
        }

        [Fact]
        public void A_cop_at_night_sees_thirty_but_not_forty_metres()
        {
            var s = Wanted();
            s.World.Clock.Hour = 2f;
            Ped cop = s.World.SpawnPed(PedKind.Cop, 2, new Vec3(200f, 0f, 200f), 0f);
            cop.YawDeg = 0f;
            Assert.True(Recognition.CopSeesPoint(s.World, cop, new Vec3(200f, 0f, 230f)));
            Assert.False(Recognition.CopSeesPoint(s.World, cop, new Vec3(200f, 0f, 240f)));
        }

        [Fact]
        public void A_cop_seated_in_a_car_looks_along_the_cars_yaw()
        {
            var s = Wanted();
            s.City.AddVehicleModel(new GtaSkylines.Game.City.VehicleModel { Id = 1, Name = "Police Car", Kind = GtaSkylines.Game.City.VehicleKind.Police, HasSiren = true });
            Car car = s.World.SpawnCar(1, new Vec3(200f, 0f, 200f), 90f, CarOwner.Police, 0xFFFFFF);
            Ped cop = s.World.SpawnPed(PedKind.Cop, 2, new Vec3(200f, 0f, 200f), 0f);
            s.World.PutInCar(cop, car, 0);
            Assert.True(Recognition.CopSeesPoint(s.World, cop, new Vec3(230f, 0f, 200f)));      // east, the way the car faces
            Assert.False(Recognition.CopSeesPoint(s.World, cop, new Vec3(170f, 0f, 200f)));
        }

        // ---- suspicion (rule d) ------------------------------------------------------------------------------------------

        private static Scene InSearchZone(PoliceDifficulty diff = PoliceDifficulty.Normal, float heat = 0f)
        {
            var s = Wanted(2, diff);
            s.Player.Model = 6;                                  // does not match the description
            s.Wanted.Evading = true;
            s.Wanted.SearchCentre = Vec3.Zero;
            s.Wanted.SearchRadius = 100f;
            s.Wanted.PlayerHeat = heat;
            return s;
        }

        private static int StepsToSuspicion(Scene s, float distance, float dt, int max)
        {
            PoliceObserver o = Obs(distance);
            for (int i = 1; i <= max; i++)
                if (Sees(s, ref o, false, dt) == RecognitionReason.Suspicion) return i;
            return -1;
        }

        [Fact]
        public void Suspicion_fills_in_two_seconds_within_twenty_five_metres_in_the_search_zone()
        {
            var s = InSearchZone();
            PoliceObserver o = Obs(20f);
            for (int i = 0; i < 3; i++) Assert.Equal(RecognitionReason.None, Sees(s, ref o, false, 0.5f));
            Assert.Equal(RecognitionReason.Suspicion, Sees(s, ref o, false, 0.5f));
            Assert.Equal(1f, o.Suspicion, 2);
        }

        [Fact]
        public void Suspicion_takes_four_seconds_between_twenty_five_and_forty_five_metres()
        {
            var s = InSearchZone();
            Assert.Equal(8, StepsToSuspicion(s, 40f, 0.5f, 20));
        }

        [Fact]
        public void No_suspicion_beyond_forty_five_metres_or_outside_the_search_zone()
        {
            var s = InSearchZone();
            Assert.Equal(-1, StepsToSuspicion(s, 50f, 0.5f, 40));
            var outside = InSearchZone();
            outside.Wanted.SearchCentre = new Vec3(500f, 0f, 500f);
            outside.Wanted.SearchRadius = 50f;
            Assert.Equal(-1, StepsToSuspicion(outside, 20f, 0.5f, 40));
            var notEvading = InSearchZone();
            notEvading.Wanted.SearchRadius = 0f;
            Assert.Equal(-1, StepsToSuspicion(notEvading, 20f, 0.5f, 40));
        }

        [Fact]
        public void High_player_heat_fills_suspicion_faster()
        {
            var s = InSearchZone(heat: 100f);
            Assert.Equal(2, StepsToSuspicion(s, 20f, 0.5f, 10));   // 2 s / (1 + 100/100) = 1 s
        }

        [Fact]
        public void Hard_difficulty_fills_suspicion_faster_and_easy_slower()
        {
            // fill times 2 s x ReactionScale: Hard 1.4 s, Easy 2.8 s (dt 0.2)
            Assert.InRange(StepsToSuspicion(InSearchZone(PoliceDifficulty.Hard), 20f, 0.2f, 40), 7, 8);
            Assert.InRange(StepsToSuspicion(InSearchZone(PoliceDifficulty.Normal), 20f, 0.2f, 40), 10, 11);
            Assert.InRange(StepsToSuspicion(InSearchZone(PoliceDifficulty.Easy), 20f, 0.2f, 40), 14, 15);
        }

        [Fact]
        public void Suspicion_drains_at_half_a_unit_per_second_when_the_player_is_out_of_sight()
        {
            var s = InSearchZone();
            PoliceObserver o = Obs(20f);
            for (int i = 0; i < 3; i++) Sees(s, ref o, false, 0.5f);          // 1.5 s of 2 s = 0.75
            Assert.Equal(0.75f, o.Suspicion, 2);
            o.YawDeg = 180f;                                                  // looks away
            Sees(s, ref o, false, 0.5f);
            Sees(s, ref o, false, 0.5f);
            Assert.Equal(0.25f, o.Suspicion, 2);
        }

        // ---- patrols (not wanted) -------------------------------------------------------------------------------------------

        [Fact]
        public void A_patrol_ignores_a_low_heat_player_even_up_close()
        {
            var s = new Scene(policeCars: false);
            s.Wanted.PlayerHeat = 30f;
            PoliceObserver o = Obs(5f);
            Assert.Equal(RecognitionReason.None, Sees(s, ref o));
        }

        [Fact]
        public void A_patrol_recognises_a_player_with_heat_forty_up_close_but_not_by_description_or_crime()
        {
            var s = new Scene(policeCars: false);
            s.Wanted.PlayerHeat = 40f;
            PoliceObserver close = Obs(5f), mid = Obs(30f);
            Assert.Equal(RecognitionReason.Close, Sees(s, ref close));
            Assert.Equal(RecognitionReason.None, Sees(s, ref mid, crime: true));
        }

        [Fact]
        public void A_patrol_builds_suspicion_without_a_search_zone_when_heat_is_forty_or_more()
        {
            var hot = new Scene(policeCars: false);
            hot.Wanted.PlayerHeat = 40f;
            Assert.True(StepsToSuspicion(hot, 20f, 0.5f, 10) > 0);
            var cool = new Scene(policeCars: false);
            cool.Wanted.PlayerHeat = 20f;
            Assert.Equal(-1, StepsToSuspicion(cool, 20f, 0.5f, 20));
        }

        // Pins of implemented data (pass on the skeleton).
        [Fact]
        public void ViewRange_combines_day_night_sneak_and_spotlight()
        {
            Assert.Equal(60f, Recognition.ViewRange(false, false, false), 3);
            Assert.Equal(35f, Recognition.ViewRange(true, false, false), 3);
            Assert.Equal(36f, Recognition.ViewRange(false, true, false), 3);
            Assert.Equal(78f, Recognition.ViewRange(false, false, true), 3);
        }
    }
}
