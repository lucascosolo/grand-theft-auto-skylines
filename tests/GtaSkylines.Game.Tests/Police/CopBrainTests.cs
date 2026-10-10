using GtaSkylines.Game.Crime;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Police;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Tests.CrimeTests;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.PoliceTests
{
    public class CopBrainTests
    {
        [Theory]
        [InlineData(3, false, false, false, true)]       // three stars and up always shoot
        [InlineData(5, false, false, false, true)]
        [InlineData(1, false, false, false, false)]      // unarmed at one star: arrest
        [InlineData(2, false, true, false, false)]       // aiming with fists is not armed
        [InlineData(1, true, true, false, true)]         // armed and aiming
        [InlineData(2, true, false, false, false)]       // armed but holstered and quiet
        [InlineData(1, false, false, true, true)]        // a cop was attacked this episode
        public void ShouldShoot_follows_the_contract(int stars, bool armed, bool aimingOrFiring, bool copAttacked, bool expected)
        {
            Assert.Equal(expected, CopBrain.ShouldShoot(stars, armed, aimingOrFiring, copAttacked));
        }

        [Fact]
        public void Aim_error_grows_with_distance_and_player_speed_and_scales_with_difficulty()
        {
            Assert.Equal(1.5f + 0.12f * 10f + 0.5f * 4f, CopBrain.AimErrorDeg(10f, 4f, 1f), 3);
            Assert.Equal(CopBrain.AimErrorDeg(10f, 4f, 1f) * 1.5f, CopBrain.AimErrorDeg(10f, 4f, 1.5f), 3);
            Assert.True(CopBrain.AimErrorDeg(40f, 0f, 1f) > CopBrain.AimErrorDeg(10f, 0f, 1f));
            Assert.True(CopBrain.AimErrorDeg(10f, 8f, 1f) > CopBrain.AimErrorDeg(10f, 0f, 1f));
        }

        // A cop standing outside (Exiting, so the brain picks its own mode) `distance` metres south of the player.
        private static Ped CopOnFoot(Scene s, float distance)
        {
            Ped cop = s.Cop(0f, -distance);
            cop.Brain = new CopBrain(s.Police) { Mode = CopMode.Exiting };
            return cop;
        }

        private static Scene Make(int stars)
        {
            var s = new Scene(policeCars: false);
            s.MakeWanted(stars);
            return s;
        }

        [Fact]
        public void At_one_star_with_an_unarmed_player_cops_approach_and_never_fire()
        {
            var s = Make(1);
            Ped cop = CopOnFoot(s, 20f);
            s.Run(12f);
            Assert.Empty(s.Peds.Fires);
            Assert.Equal(CopMode.Arrest, ((CopBrain)cop.Brain).Mode);
        }

        [Fact]
        public void At_three_stars_cops_shoot_with_a_pistol_toward_the_player()
        {
            var s = Make(3);
            Ped cop = CopOnFoot(s, 20f);
            s.Run(10f);
            Assert.InRange(s.Peds.Fires.Count, 5, 18);
            Assert.Equal((int)WeaponId.Pistol, cop.CurrentWeapon);
            Assert.True(cop.Has(PedFlags.Hostile));
            foreach (var shot in s.Peds.Fires)
            {
                Assert.Equal(cop.Id, shot.ShooterId);
                Vec3 toPlayer = new Vec3(0f, 1.2f, 0f) - shot.Origin;
                float cos = Vec3.Dot(toPlayer.Normalized, shot.Direction.Normalized);
                float angle = (float)(System.Math.Acos(System.Math.Min(1f, cos)) * 180.0 / System.Math.PI);
                Assert.InRange(angle, 0f, CopBrain.AimErrorDeg(25f, 1f, 1.5f) + 1f);
            }
        }

        [Fact]
        public void An_armed_aiming_player_makes_even_a_one_star_cop_shoot()
        {
            var s = Make(1);
            s.Player.CurrentWeapon = (int)WeaponId.Pistol;
            s.Player.Flags |= PedFlags.Aiming;
            CopOnFoot(s, 20f);
            s.Run(10f);
            Assert.NotEmpty(s.Peds.Fires);
        }

        [Fact]
        public void Standing_still_next_to_a_cop_with_hands_up_is_busted_after_two_seconds_with_the_order_shouted()
        {
            var s = Make(1);
            s.Player.Flags |= PedFlags.HandsUp;
            Ped cop = CopOnFoot(s, 12f);
            s.Run(1.5f);
            Assert.True(s.World.Messages.TryGetSubtitle(out Subtitle sub));
            Assert.Equal(CopBrain.HandsUpLine, sub.Text);
            Assert.Equal(CopBrain.Speaker, sub.Speaker);
            Assert.False(s.Wanted.BustedRequested);
            float t = s.RunUntil(() => s.Wanted.BustedRequested, 12f);
            Assert.True(t > 0f);
            Assert.Equal(cop.Tag, s.Wanted.BustedByTag);
            Assert.InRange(Vec3.DistanceXZ(cop.Position, s.Player.Position), 0f, CopBrain.ArrestRange + 0.3f);
        }

        [Fact]
        public void The_player_standing_still_without_hands_up_is_also_arrested()
        {
            var s = Make(2);
            CopOnFoot(s, 12f);
            Assert.True(s.RunUntil(() => s.Wanted.BustedRequested, 15f) > 0f);
        }

        [Fact]
        public void At_three_stars_nobody_arrests_a_player_holding_hands_up()
        {
            var s = Make(3);
            s.Player.Flags |= PedFlags.HandsUp;
            CopOnFoot(s, 3f);
            s.Run(8f);
            Assert.False(s.Wanted.BustedRequested);
        }

        [Fact]
        public void Running_away_after_the_order_reports_resisting_arrest_once()
        {
            var s = Make(1);
            CopOnFoot(s, 12f);
            Assert.True(s.RunUntil(() => s.World.Messages.TryGetSubtitle(out _), 8f) > 0f);
            s.Step();
            s.PlacePlayer(0f, 40f);                               // flees far from the cop
            s.Step(); s.Step();
            s.PlacePlayer(0f, 80f);
            s.Run(1f);
            int resisting = 0;
            for (int i = 0; i < s.Crime.CrimeCount; i++) if (s.Crime.GetCrime(i).Type == CrimeType.ResistingArrest) resisting++;
            Assert.Equal(1, resisting);
            Assert.Equal(2, s.Wanted.Stars);
        }

        [Fact]
        public void A_cop_shot_at_in_the_open_takes_cover_on_the_far_side_of_its_car()
        {
            var s = Make(3);
            s.City.AddVehicleModel(new GtaSkylines.Game.City.VehicleModel { Id = 1, Name = "Police Car", Kind = GtaSkylines.Game.City.VehicleKind.Police, HasSiren = true });
            Car car = s.World.SpawnCar(1, new Vec3(0f, 0f, -20f), 0f, CarOwner.Police, 0xFFFFFF);
            Ped cop = s.Cop(2f, -20f);
            cop.Brain = new CopBrain(s.Police) { Mode = CopMode.Exiting };
            s.Run(1.5f);
            cop.LastDamageTime = s.World.Clock.GameSeconds;
            s.Run(3f);
            Assert.Equal(CopMode.Cover, ((CopBrain)cop.Brain).Mode);
            Assert.True(cop.Position.Z < car.Position.Z + 0.5f, "cover is on the side away from the player");
        }
    }
}
