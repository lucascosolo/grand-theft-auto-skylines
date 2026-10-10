using System;
using GtaSkylines.Game.Camera;
using GtaSkylines.Game.Combat;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Vehicles;
using GtaSkylines.Game.World;
using GtaSkylines.Mod.Play;
using GtaSkylines.Mod.Rendering;
using Xunit;
using DrawAnim = GtaSkylines.Mod.Rendering.PedAnim;
using GameAnim = GtaSkylines.Game.World.PedAnim;

namespace GtaSkylines.Mod.Tests.Play
{
    public class WorldFrameBuilderTests
    {
        // ---- pure mappings -------------------------------------------------------------------------------------

        [Theory]
        [InlineData(GameAnim.Idle, PedFlags.None, DrawAnim.Idle)]
        [InlineData(GameAnim.Walk, PedFlags.None, DrawAnim.Walk)]
        [InlineData(GameAnim.Run, PedFlags.None, DrawAnim.Run)]
        [InlineData(GameAnim.Sprint, PedFlags.None, DrawAnim.Sprint)]
        [InlineData(GameAnim.Swim, PedFlags.None, DrawAnim.Walk)]
        [InlineData(GameAnim.Sit, PedFlags.None, DrawAnim.Sitting)]
        [InlineData(GameAnim.LieDown, PedFlags.None, DrawAnim.Lying)]
        [InlineData(GameAnim.Idle, PedFlags.Dead, DrawAnim.Lying)]
        [InlineData(GameAnim.Walk, PedFlags.KnockedDown, DrawAnim.Lying)]
        [InlineData(GameAnim.Panic, PedFlags.None, DrawAnim.Panic)]
        [InlineData(GameAnim.Idle, PedFlags.Panicking, DrawAnim.Panic)]
        [InlineData(GameAnim.Phone, PedFlags.None, DrawAnim.Phone)]
        [InlineData(GameAnim.Idle, PedFlags.OnPhone, DrawAnim.Phone)]
        [InlineData(GameAnim.HandsUp, PedFlags.None, DrawAnim.HandsUp)]
        [InlineData(GameAnim.Idle, PedFlags.HandsUp, DrawAnim.HandsUp)]
        [InlineData(GameAnim.Fall, PedFlags.None, DrawAnim.Idle)]
        [InlineData(GameAnim.Climb, PedFlags.None, DrawAnim.Idle)]
        public void MapAnim_follows_the_documented_table(GameAnim anim, PedFlags flags, DrawAnim expected)
        {
            Assert.Equal(expected, WorldFrameBuilder.MapAnim(anim, flags));
        }

        [Theory]
        [InlineData(VehicleDamageState.Intact, CarDamage.Intact)]
        [InlineData(VehicleDamageState.Smoking, CarDamage.Smoking)]
        [InlineData(VehicleDamageState.OnFire, CarDamage.Burning)]
        [InlineData(VehicleDamageState.Exploded, CarDamage.Exploded)]
        public void MapDamage_maps_each_state(VehicleDamageState state, CarDamage expected)
        {
            Assert.Equal(expected, WorldFrameBuilder.MapDamage(state));
        }

        [Fact]
        public void ViewWeaponOf_keeps_the_order_and_sends_out_of_range_to_fists()
        {
            Assert.Equal(ViewWeapon.Pistol, WorldFrameBuilder.ViewWeaponOf(WeaponId.Pistol));
            Assert.Equal(ViewWeapon.Bat, WorldFrameBuilder.ViewWeaponOf(WeaponId.Bat));
            Assert.Equal(ViewWeapon.Fists, WorldFrameBuilder.ViewWeaponOf((WeaponId)99));
            Assert.Equal(ViewWeapon.Fists, WorldFrameBuilder.ViewWeaponOf((WeaponId)(-1)));
        }

        [Theory]
        [InlineData(CameraMode.FootFirstPerson, true)]
        [InlineData(CameraMode.VehicleFirstPerson, true)]
        [InlineData(CameraMode.Hood, true)]
        [InlineData(CameraMode.FootOrbit, false)]
        [InlineData(CameraMode.Aim, false)]
        [InlineData(CameraMode.Chase, false)]
        [InlineData(CameraMode.Death, false)]
        public void IsFirstPerson_covers_the_three_first_person_views(CameraMode mode, bool expected)
        {
            Assert.Equal(expected, WorldFrameBuilder.IsFirstPerson(mode));
        }

        // ---- peds ----------------------------------------------------------------------------------------------

        private static RenderFrame Fill(WorldFrameBuilder b, PlayGame g, float dt = 0.1f)
        {
            var f = new RenderFrame();
            b.Fill(f, g.Parts, dt);
            return f;
        }

        [Fact]
        public void Fill_sets_alpha_to_the_session_interpolation_and_delta_time()
        {
            var g = new PlayGame();
            RenderFrame f = Fill(new WorldFrameBuilder(), g, 0.25f);
            Assert.Equal(g.Parts.Session.Interpolation, f.Alpha);
            Assert.Equal(0.25f, f.DeltaTime);
        }

        [Fact]
        public void Player_is_drawn_in_third_person_but_hidden_in_first_person_on_foot()
        {
            var g = new PlayGame();
            g.Parts.Session.Camera.FootView = FootView.ThirdMid;
            g.Tick();
            Assert.Contains(Fill(new WorldFrameBuilder(), g).Peds, p => p.Key == g.Player.Id);

            g.Parts.Session.Camera.FootView = FootView.FirstPerson;
            g.Tick();
            Assert.Equal(CameraMode.FootFirstPerson, g.Parts.Session.Camera.Mode);
            Assert.DoesNotContain(Fill(new WorldFrameBuilder(), g).Peds, p => p.Key == g.Player.Id);
        }

        [Fact]
        public void Other_peds_stay_visible_in_first_person()
        {
            var g = new PlayGame();
            Ped npc = g.W.SpawnPed(PedKind.Civilian, 2, new Vec3(353, 0, 353), 0f);
            g.Parts.Session.Camera.FootView = FootView.FirstPerson;
            g.Tick();
            Assert.Contains(Fill(new WorldFrameBuilder(), g).Peds, p => p.Key == npc.Id && p.ModelId == 2);
        }

        [Fact]
        public void Peds_in_a_vehicle_or_invisible_are_not_drawn()
        {
            var g = new PlayGame();
            Ped a = g.W.SpawnPed(PedKind.Civilian, 1, new Vec3(5, 0, 5), 0f);
            Ped b = g.W.SpawnPed(PedKind.Civilian, 1, new Vec3(6, 0, 5), 0f);
            a.Flags |= PedFlags.InVehicle;
            b.Flags |= PedFlags.Invisible;
            RenderFrame f = Fill(new WorldFrameBuilder(), g);
            Assert.DoesNotContain(f.Peds, p => p.Key == a.Id || p.Key == b.Id);
        }

        [Fact]
        public void Ped_draw_carries_position_yaw_and_mapped_anim()
        {
            var g = new PlayGame();
            Ped npc = g.W.SpawnPed(PedKind.Civilian, 3, new Vec3(7, 0, 8), 90f);
            npc.Anim = GameAnim.Run;
            PedDraw d = Assert.Single(Fill(new WorldFrameBuilder(), g).Peds, p => p.Key == npc.Id);
            Assert.Equal(npc.Position, d.Position);
            Assert.Equal(90f, d.YawDeg);
            Assert.Equal(DrawAnim.Run, d.Anim);
            Assert.True(d.Visible);
        }

        [Fact]
        public void Gate_hides_peds_and_cars_that_are_not_ready()
        {
            var g = new PlayGame();
            Ped npc = g.W.SpawnPed(PedKind.Civilian, 1, new Vec3(5, 0, 5), 0f);
            Car car = g.SpawnCar(0, new Vec3(10, 0, 10));
            var gate = new FakeGate { Cars = false, Peds = false };
            var b = new WorldFrameBuilder { Gate = gate };
            RenderFrame hidden = Fill(b, g);
            Assert.DoesNotContain(hidden.Peds, p => p.Key == npc.Id);
            Assert.Empty(hidden.Cars);

            gate.Cars = gate.Peds = true;
            RenderFrame shown = Fill(b, g);
            Assert.Contains(shown.Peds, p => p.Key == npc.Id);
            Assert.Single(shown.Cars);
            Assert.Equal(car.Position, shown.Cars[0].Centre);
        }

        // ---- cars ----------------------------------------------------------------------------------------------

        [Fact]
        public void Car_draw_carries_model_colour_and_damage()
        {
            var g = new PlayGame();
            Car car = g.SpawnCar(1, new Vec3(10, 0, 10), 0xAB1234);
            CarDraw d = Assert.Single(Fill(new WorldFrameBuilder(), g).Cars);
            Assert.Equal(car.Model.Id, d.ModelId);
            Assert.Equal(0xAB1234, d.ColourRgb);
            Assert.Equal(CarDamage.Intact, d.Damage);
        }

        [Fact]
        public void Wheel_travel_accumulates_with_forward_speed()
        {
            var g = new PlayGame();
            Car car = g.SpawnCar(0, new Vec3(10, 0, 10));
            car.Body.SetVelocity(car.Rotation * Vec3.Forward * 10f, Vec3.Zero);
            var b = new WorldFrameBuilder();
            float t1 = Fill(b, g).Cars[0].WheelTravelMetres;
            float t2 = Fill(b, g).Cars[0].WheelTravelMetres;
            float t3 = Fill(b, g).Cars[0].WheelTravelMetres;
            Assert.True(t2 > t1, "t1=" + t1 + " t2=" + t2);
            Assert.True(t3 > t2, "t2=" + t2 + " t3=" + t3);
        }

        [Fact]
        public void Wheel_travel_stays_put_for_a_stationary_car()
        {
            var g = new PlayGame();
            g.SpawnCar(0, new Vec3(10, 0, 10));
            var b = new WorldFrameBuilder();
            Fill(b, g);
            Assert.Equal(0f, Fill(b, g).Cars[0].WheelTravelMetres, 3);
        }

        [Fact]
        public void Reset_forgets_wheel_travel()
        {
            var g = new PlayGame();
            Car car = g.SpawnCar(0, new Vec3(10, 0, 10));
            car.Body.SetVelocity(car.Rotation * Vec3.Forward * 10f, Vec3.Zero);
            var b = new WorldFrameBuilder();
            Fill(b, g);
            float grown = Fill(b, g).Cars[0].WheelTravelMetres;
            b.Reset();
            float fresh = Fill(b, g).Cars[0].WheelTravelMetres;
            Assert.True(fresh < grown, "fresh=" + fresh + " grown=" + grown);
        }

        [Fact]
        public void Seconds_since_explosion_counts_up_from_zero_once_the_car_has_exploded()
        {
            var g = new PlayGame();
            Car car = g.SpawnCar(0, new Vec3(10, 0, 10));
            var b = new WorldFrameBuilder();
            car.Body.ApplyDamage(1e6f);
            for (int i = 0; i < 1200 && car.Body.DamageState != VehicleDamageState.Exploded; i++) g.Tick();
            Assert.Equal(VehicleDamageState.Exploded, car.Body.DamageState);
            CarDraw first = Fill(b, g, 0.5f).Cars[0];
            CarDraw second = Fill(b, g, 0.5f).Cars[0];
            Assert.Equal(CarDamage.Exploded, first.Damage);
            Assert.Equal(0f, first.SecondsSinceExplosion, 3);
            Assert.Equal(0.5f, second.SecondsSinceExplosion, 3);
        }

        [Fact]
        public void Brake_and_siren_flags_follow_the_car()
        {
            var g = new PlayGame();
            Car car = g.SpawnCar(1, new Vec3(10, 0, 10));
            car.Siren = true;
            car.Controls.Handbrake = true;
            CarDraw d = Fill(new WorldFrameBuilder(), g).Cars[0];
            Assert.True(d.Siren);
            Assert.True(d.Brake);
        }

        // ---- effects -------------------------------------------------------------------------------------------

        [Fact]
        public void First_call_does_not_replay_old_events()
        {
            var g = new PlayGame();
            g.W.Emit(GameEventType.Explosion, new Vec3(1, 0, 1), 0, 0, 0f, null);
            g.W.Emit(GameEventType.Shot, new Vec3(1, 1, 1), new Vec3(5, 1, 1), 0, 0, 0f);
            var f = Fill(new WorldFrameBuilder(), g);
            Assert.Empty(f.Explosions);
            Assert.Empty(f.Shots);
        }

        [Fact]
        public void Shot_without_a_victim_gives_shot_and_flash_but_no_impact()
        {
            var g = new PlayGame();
            var b = new WorldFrameBuilder();
            Fill(b, g);
            g.W.Emit(GameEventType.Shot, new Vec3(1, 1, 1), new Vec3(9, 1, 1), 7, 0, 0f);
            RenderFrame f = Fill(b, g);
            ShotDraw s = Assert.Single(f.Shots);
            Assert.Equal(new Vec3(1, 1, 1), s.From);
            Assert.Equal(new Vec3(9, 1, 1), s.To);
            Assert.Equal(new Vec3(1, 1, 1), Assert.Single(f.MuzzleFlashes).Position);
            Assert.Empty(f.Impacts);
        }

        [Fact]
        public void Shot_that_hits_an_actor_adds_an_impact_at_the_end_point()
        {
            var g = new PlayGame();
            var b = new WorldFrameBuilder();
            Fill(b, g);
            g.W.Emit(GameEventType.Shot, new Vec3(1, 1, 1), new Vec3(9, 1, 1), 7, 8, 0f);
            Assert.Equal(new Vec3(9, 1, 1), Assert.Single(Fill(b, g).Impacts).Position);
        }

        [Fact]
        public void Explosion_event_adds_an_explosion_once()
        {
            var g = new PlayGame();
            var b = new WorldFrameBuilder();
            Fill(b, g);
            g.W.Emit(GameEventType.Explosion, new Vec3(3, 0, 4), 0, 0, 0f, null);
            Assert.Equal(new Vec3(3, 0, 4), Assert.Single(Fill(b, g).Explosions).Position);
            Assert.Empty(Fill(b, g).Explosions);
        }

        // ---- markers, view model, camera -----------------------------------------------------------------------

        [Fact]
        public void Mission_markers_become_cylinders()
        {
            var g = new PlayGame();
            g.W.Mission.Markers.Clear();
            g.W.Mission.Markers.Add(new WorldMarker { Id = 1, Position = new Vec3(20, 0, 30), Radius = 2.5f, ColourRgb = 0x00FF00 });
            RenderFrame f = Fill(new WorldFrameBuilder(), g);
            MarkerDraw m = Assert.Single(f.Markers);
            Assert.Equal(MarkerKind.Cylinder, m.Kind);
            Assert.Equal(new Vec3(20, 0, 30), m.Position);
            Assert.Equal(2.5f, m.Radius);
            Assert.Equal(0x00FF00, m.ColourRgb);
            Assert.Equal(WorldFrameBuilder.MarkerAlpha, m.Alpha);
        }

        [Fact]
        public void Building_door_target_adds_a_yellow_door_marker()
        {
            var g = new PlayGame();
            g.W.Mission.Markers.Clear();
            g.W.PlayerState.Interaction = new InteractionTarget { Kind = InteractionTargetKind.BuildingDoor, Position = new Vec3(4, 0, 6) };
            MarkerDraw m = Assert.Single(Fill(new WorldFrameBuilder(), g).Markers);
            Assert.Equal(MarkerKind.Door, m.Kind);
            Assert.Equal(new Vec3(4, 0, 6), m.Position);
            Assert.Equal(WorldFrameBuilder.DoorMarkerRadius, m.Radius);
            Assert.Equal(WorldFrameBuilder.DoorMarkerColour, m.ColourRgb);
        }

        [Fact]
        public void Other_interaction_targets_add_no_door_marker()
        {
            var g = new PlayGame();
            g.W.Mission.Markers.Clear();
            g.W.PlayerState.Interaction = new InteractionTarget { Kind = InteractionTargetKind.EnterVehicle, Position = new Vec3(4, 0, 6) };
            Assert.Empty(Fill(new WorldFrameBuilder(), g).Markers);
        }

        [Fact]
        public void Camera_comes_from_the_world_view_with_the_near_plane_of_the_mode()
        {
            var g = new PlayGame();
            g.Parts.Session.Camera.FootView = FootView.ThirdMid;
            g.Tick();
            g.W.ViewPosition = new Vec3(1, 2, 3);
            g.W.ViewRotation = Quat.Yaw(45f);
            g.W.ViewFovDeg = 63f;
            RenderFrame f = Fill(new WorldFrameBuilder(), g);
            Assert.True(f.HasCamera);
            Assert.Equal(new Vec3(1, 2, 3), f.Camera.Position);
            Assert.Equal(Quat.Yaw(45f), f.Camera.Rotation);
            Assert.Equal(63f, f.Camera.FovDeg);
            Assert.Equal(WorldFrameBuilder.ThirdPersonNear, f.Camera.Near);

            g.Parts.Session.Camera.FootView = FootView.FirstPerson;
            g.Tick();
            Assert.Equal(WorldFrameBuilder.FirstPersonNear, Fill(new WorldFrameBuilder(), g).Camera.Near);
        }

        [Fact]
        public void View_model_shows_the_current_weapon_in_first_person_only()
        {
            var g = new PlayGame();
            g.Parts.Session.Camera.FootView = FootView.FirstPerson;
            g.Tick();
            g.Player.CurrentWeapon = (int)WeaponId.Pistol;
            ViewModelDraw vm = Fill(new WorldFrameBuilder(), g).ViewModel;
            Assert.Equal(ViewWeapon.Pistol, vm.Weapon);
            Assert.True(vm.Visible);

            g.Parts.Session.Camera.FootView = FootView.ThirdMid;
            g.Tick();
            Assert.False(Fill(new WorldFrameBuilder(), g).ViewModel.Visible);
        }

        // ---- reload progress -----------------------------------------------------------------------------------

        [Fact]
        public void ReloadProgress_is_minus_one_without_a_user_or_when_not_reloading_or_without_reload_time()
        {
            WeaponStats pistol = WeaponTable.Get(WeaponId.Pistol);
            Assert.Equal(-1f, WorldFrameBuilder.ReloadProgress(null, pistol));
            Assert.Equal(-1f, WorldFrameBuilder.ReloadProgress(new WeaponUser(null), pistol));
            Assert.Equal(-1f, WorldFrameBuilder.ReloadProgress(new WeaponUser(null), WeaponTable.Get(WeaponId.Fists)));
        }

        private static PlayGame ArmedFirstPerson()
        {
            var g = new PlayGame();
            g.Profile.GiveWeapon(WeaponId.Pistol);
            g.Profile.SetAmmo(WeaponId.Pistol, 100);
            g.Player.CurrentWeapon = (int)WeaponId.Pistol;
            g.Parts.Session.Camera.FootView = FootView.FirstPerson;
            g.Tick(3);
            g.Player.MagazineAmmo = 4;
            return g;
        }

        [Fact]
        public void View_model_reload_progress_is_negative_when_not_reloading()
        {
            PlayGame g = ArmedFirstPerson();
            Assert.True(Fill(new WorldFrameBuilder(), g).ViewModel.ReloadProgress < 0f);
        }

        [Fact]
        public void View_model_reload_progress_rises_from_zero_during_a_reload()
        {
            PlayGame g = ArmedFirstPerson();
            Assert.True(g.Parts.Combat.PlayerWeapon.StartReload(g.Player));
            g.Tick(10);
            float first = Fill(new WorldFrameBuilder(), g).ViewModel.ReloadProgress;
            g.Tick(10);
            float second = Fill(new WorldFrameBuilder(), g).ViewModel.ReloadProgress;
            Assert.InRange(first, 0f, 0.999f);
            Assert.InRange(second, 0f, 0.999f);
            Assert.True(second > first);
            Assert.Equal(WorldFrameBuilder.ReloadProgress(g.Parts.Combat.PlayerWeapon, WeaponTable.Get(WeaponId.Pistol)), second);
        }
    }
}
