using System;
using GtaSkylines.Game.Camera;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Tests.PhysicsTests;
using GtaSkylines.Game.Tests.SessionTests;
using GtaSkylines.Game.World;
using Xunit;
using Sim = GtaSkylines.Game.Tests.SessionTests.Sim;

namespace GtaSkylines.Game.Tests.CameraTests
{
    public class CameraFootTests
    {
        private static Sim At(int footView, float yaw = 0f)
        {
            var sim = new Sim(settings: s => s.FootView = footView, yaw: yaw);
            sim.Run(3.0, In.Idle);
            return sim;
        }

        [Fact]
        public void FirstPerson_SitsAtEyeHeight_WithShortNearClip_AndLooksAlongTheView()
        {
            Sim sim = At(0, 90f);
            CameraRig c = sim.S.Camera;
            c.Present(1f);
            Assert.Equal(CameraMode.FootFirstPerson, c.Mode);
            Sim.Near(sim.Player.Position + new Vec3(0f, CameraRig.EyeHeight, 0f), c.Position, 0.03f, "eye position");
            Sim.Near(90f, (c.Rotation * Vec3.Forward).Length > 0 ? Vec3.YawDegOf(c.Rotation * Vec3.Forward) : 0f, 1f, "yaw");
            Sim.Near(0.05f, c.NearClip, 1e-4f);
            Sim.Near(sim.Settings.FieldOfViewDeg, c.FovDeg, 0.1f, "fov");
            Sim.Near(c.Position, sim.W.ViewPosition, 0.05f, "world view position written");
        }

        [Fact]
        public void FirstPerson_BobsWhileWalking_ButNotWhenStill()
        {
            Sim sim = At(0);
            float min = 9f, max = -9f;
            for (int i = 0; i < 90; i++)
            {
                sim.Tick(In.Forward);
                sim.S.Camera.Present(1f);
                float y = sim.S.Camera.Position.Y - sim.Player.Position.Y;
                min = Math.Min(min, y); max = Math.Max(max, y);
            }
            Assert.InRange(max - min, 0.05f, 0.1f);
            Assert.InRange(min, CameraRig.EyeHeight - 0.06f, CameraRig.EyeHeight);
            sim.Run(1.0, In.Idle);
            sim.S.Camera.Present(1f);
            Sim.Near(CameraRig.EyeHeight, sim.S.Camera.Position.Y - sim.Player.Position.Y, 0.005f, "still");
        }

        [Theory]
        [InlineData(1, 2.2f)]
        [InlineData(2, 3.5f)]
        [InlineData(3, 5f)]
        public void ThirdPerson_OrbitsAtTheViewDistanceBehindThePlayer(int view, float dist)
        {
            Sim sim = At(view);
            CameraRig c = sim.S.Camera;
            c.Present(1f);
            Vec3 pivot = sim.Player.Position + new Vec3(0f, CameraRig.PivotHeight, 0f);
            Sim.Near(dist, (c.Position - pivot).Length, 0.08f, "distance");
            Assert.True(c.Position.Z < pivot.Z - dist * 0.8f, "behind the player");
            Assert.Equal(CameraMode.FootOrbit, c.Mode);
            Sim.Near(0.1f, c.NearClip, 1e-4f);
        }

        [Fact]
        public void Aiming_GoesOverTheShoulder_Fov50()
        {
            Sim sim = At(2);
            sim.Run(2.0, In.Hold(GameButton.Aim));
            CameraRig c = sim.S.Camera;
            c.Present(1f);
            Assert.Equal(CameraMode.Aim, c.Mode);
            Sim.Near(50f, c.FovDeg, 1f, "aim fov");
            Vec3 pivot = sim.Player.Position + new Vec3(0f, CameraRig.PivotHeight, 0f) + new Vec3(CameraRig.AimRightOffset, 0f, 0f);
            Sim.Near(CameraRig.AimDistance, (c.Position - pivot).Length, 0.08f, "arm 1.2 m");
        }

        [Fact]
        public void Aiming_InFirstPerson_StaysFirstPerson()
        {
            Sim sim = At(0);
            sim.Run(0.5, In.Hold(GameButton.Aim));
            Assert.Equal(CameraMode.FootFirstPerson, sim.S.Camera.Mode);
        }

        [Fact]
        public void CameraView_CyclesFootViewsAndWraps_LeavingTheVehicleViewAlone()
        {
            Sim sim = At(0);
            VehicleView veh = sim.S.Camera.VehicleView;
            var seen = new System.Collections.Generic.List<FootView>();
            for (int i = 0; i < 4; i++) { sim.Tick(In.Press(GameButton.CameraView)); seen.Add(sim.S.Camera.FootView); }
            Assert.Equal(new[] { FootView.ThirdNear, FootView.ThirdMid, FootView.ThirdFar, FootView.FirstPerson }, seen.ToArray());
            Assert.Equal(veh, sim.S.Camera.VehicleView);
        }

        [Fact]
        public void Sprint_RaisesTheFovByUpTo8Degrees_Eased()
        {
            Sim sim = At(2);
            sim.S.Camera.Present(1f);
            float basefov = sim.S.Camera.FovDeg;
            sim.Run(0.1, In.Move(0f, 1f, GameButton.Sprint));
            sim.S.Camera.Present(1f);
            Assert.True(sim.S.Camera.FovDeg < basefov + 7f, "not instant");
            sim.Run(2.0, In.Move(0f, 1f, GameButton.Sprint));
            sim.S.Camera.Present(1f);
            Sim.Near(basefov + 8f, sim.S.Camera.FovDeg, 0.8f, "sprint fov");
        }

        [Fact]
        public void LookBehind_FirstPersonLooksBackward_OrbitSwapsSides()
        {
            Sim fp = At(0);
            fp.Run(0.2, In.Hold(GameButton.LookBehind));
            fp.S.Camera.Present(1f);
            Sim.Near(180f, Math.Abs(Vec3.YawDegOf(fp.S.Camera.Rotation * Vec3.Forward)), 3f, "looks back");
            Sim orb = At(2);
            orb.Run(2.0, In.Hold(GameButton.LookBehind));
            orb.S.Camera.Present(1f);
            Assert.True(orb.S.Camera.Position.Z > orb.Player.Position.Z + 2.5f, "camera in front of the player");
        }

        [Fact]
        public void Present_InterpolatesBetweenTheLastTwoSteps_AndClampsAlpha()
        {
            Sim sim = At(0);
            sim.Run(0.5, In.Forward);
            CameraRig c = sim.S.Camera;
            c.Present(0f); Vec3 a = c.Position;
            c.Present(1f); Vec3 b = c.Position;
            c.Present(0.5f); Vec3 mid = c.Position;
            Sim.Near((a + b) * 0.5f, mid, 0.01f, "midpoint");
            c.Present(-3f); Sim.Near(a, c.Position, 1e-4f);
            c.Present(9f); Sim.Near(b, c.Position, 1e-4f);
            Assert.True((b - a).Length > 0.02f);
        }
    }

    public class CameraSpringArmTests
    {
        private static Sim Walled(SurfaceFlags flags)
        {
            var sim = new Sim(settings: s => s.FootView = 3);
            new MeshBuilder().Wall(-6f, -2f, 6f, -2f, 0f, 8f, flags).Apply(sim.W.Collision, 11);
            return sim;
        }

        [Fact]
        public void AWallBehindThePlayer_PullsTheCameraInFront_NeverInsideIt()
        {
            Sim sim = Walled(SurfaceFlags.Building);
            sim.Run(2.0, In.Idle);
            sim.S.Camera.Present(1f);
            Assert.True(sim.S.Camera.Position.Z > -2f, "camera z " + sim.S.Camera.Position.Z);
            Assert.True(sim.S.Camera.ArmLength < 2.3f, "arm " + sim.S.Camera.ArmLength);
        }

        [Fact]
        public void ArmPullsInImmediately_AndEasesOutSlowly()
        {
            var sim = new Sim(settings: s => s.FootView = 3);
            sim.Run(3.0, In.Idle);
            Assert.True(sim.S.Camera.ArmLength > 4.9f);
            new MeshBuilder().Wall(-6f, -2f, 6f, -2f, 0f, 8f).Apply(sim.W.Collision, 11);
            sim.Tick(In.Idle);
            Assert.True(sim.S.Camera.ArmLength < 2.3f, "pulled in within one step");
            sim.W.Collision.RemoveRegion(11);
            sim.Run(0.1, In.Idle);
            Assert.InRange(sim.S.Camera.ArmLength, 1.8f, 3.4f);
            sim.Run(4.0, In.Idle);
            Assert.True(sim.S.Camera.ArmLength > 4.9f);
        }

        [Fact]
        public void NoCameraSurfaces_AreIgnored_InteriorSurfacesBlock()
        {
            Sim ghost = Walled(SurfaceFlags.Building | SurfaceFlags.NoCamera);
            ghost.Run(3.0, In.Idle);
            Assert.True(ghost.S.Camera.ArmLength > 4.9f);
            Sim inner = Walled(SurfaceFlags.Interior);
            inner.Run(2.0, In.Idle);
            Assert.True(inner.S.Camera.ArmLength < 2.3f);
        }

        [Fact]
        public void OrbitingNextToABox_TheCameraIsNeverInsideIt()
        {
            var sim = new Sim(settings: s => s.FootView = 3);
            Vec3 min = new Vec3(1.5f, 0f, -1f), max = new Vec3(4f, 4f, 3f);
            new MeshBuilder().Box(min, max).Apply(sim.W.Collision, 12);
            for (int i = 0; i < 360; i++)
            {
                sim.Tick(In.Look(2f, 0f));
                sim.S.Camera.Present(1f);
                Vec3 p = sim.S.Camera.Position;
                bool inside = p.X > min.X && p.X < max.X && p.Y > min.Y && p.Y < max.Y && p.Z > min.Z && p.Z < max.Z;
                Assert.False(inside, "camera inside the box at step " + i + ": " + p);
            }
        }
    }

    public class CameraLimitsAndDeathTests
    {
        [Fact]
        public void Death_OrbitsTheBodyAtFourMetres_About12DegreesPerSecond()
        {
            var sim = new Sim(settings: s => s.FootView = 0);
            sim.Run(0.5, In.Idle);
            sim.Player.Health = 0f;
            sim.Run(1.0, In.Idle);
            CameraRig c = sim.S.Camera;
            Assert.Equal(CameraMode.Death, c.Mode);
            c.Present(1f);
            Vec3 centre = sim.Player.Position + new Vec3(0f, 1.5f, 0f);
            Sim.Near(CameraRig.DeathOrbitDistance, (c.Position - centre).Length, 0.4f, "radius");
            float a0 = Vec3.YawDegOf((c.Position - centre).Flat);
            sim.Run(2.0, In.Idle);
            c.Present(1f);
            float a1 = Vec3.YawDegOf((c.Position - centre).Flat);
            Sim.Near(24f, Sim.AngleDiff(a0, a1), 5f, "orbit rate over 2 s");
        }

        [Fact]
        public void StaticHelpers_MatchTheContract()
        {
            Assert.Equal(0f, CameraRig.OrbitDistance(FootView.FirstPerson));
            Assert.Equal(3.5f, CameraRig.OrbitDistance(FootView.ThirdMid));
            Assert.Equal(2f * 4.4f + 1f, CameraRig.ChaseDistance(VehicleView.ChaseNear, 4.4f), 3);
            Assert.Equal(3f * 4.4f + 2f, CameraRig.ChaseDistance(VehicleView.ChaseFar, 4.4f), 3);
            Sim.Near(new Vec3(-0.35f, 0.55f * 1.5f - 0.75f, 0.44f), CameraRig.SeatLocal(new Vec3(0.9f, 0.75f, 2.2f)), 1e-4f);
        }
    }

    public class CameraVehicleTests
    {
        private static Sim InCar(int vehicleView, out Car car)
        {
            var sim = new Sim(settings: s => s.VehicleView = vehicleView);
            car = sim.W.SpawnCar(0, new Vec3(0f, 0f, 10f), 0f, CarOwner.Player, 0xFFFFFF);
            sim.W.PutInCar(sim.Player, car, 0);
            sim.Run(1.0, In.Idle);
            return sim;
        }

        [Fact]
        public void FirstPerson_SitsAtTheDriverSeat()
        {
            Car car;
            Sim sim = InCar(0, out car);
            CameraRig c = sim.S.Camera;
            c.Present(1f);
            Assert.Equal(CameraMode.VehicleFirstPerson, c.Mode);
            Vec3 want = car.Position + car.Rotation * CameraRig.SeatLocal(car.Model.HalfExtents);
            Sim.Near(want, c.Position, 0.12f, "seat");
        }

        [Fact]
        public void Hood_SitsOnTheBonnet()
        {
            Car car;
            Sim sim = InCar(1, out car);
            sim.S.Camera.Present(1f);
            Assert.Equal(CameraMode.Hood, sim.S.Camera.Mode);
            Vec3 h = car.Model.HalfExtents;
            Vec3 want = car.Position + car.Rotation * new Vec3(0f, h.Y + 0.15f, 0.25f * 2f * h.Z);
            Sim.Near(want, sim.S.Camera.Position, 0.12f, "bonnet");
        }

        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        public void Chase_SitsBehindTheCarAtTheModelDistance(int view)
        {
            Car car;
            Sim sim = InCar(view, out car);
            sim.Run(3.0, In.Idle);
            sim.S.Camera.Present(1f);
            Assert.Equal(CameraMode.Chase, sim.S.Camera.Mode);
            float len = 2f * car.Model.HalfExtents.Z;
            Vec3 pivot = car.Position + new Vec3(0f, car.Model.HalfExtents.Y + 0.4f, 0f);
            float want = CameraRig.ChaseDistance((VehicleView)view, len);
            Sim.Near(want, (sim.S.Camera.Position - pivot).Length, 0.4f, "chase distance");
            Assert.True(Vec3.Dot(sim.S.Camera.Position - car.Position, car.Rotation.Forward) < -len * 0.5f, "behind the car");
        }

        [Fact]
        public void CameraView_InACar_CyclesVehicleViewsOnly()
        {
            Car car;
            Sim sim = InCar(0, out car);
            FootView foot = sim.S.Camera.FootView;
            sim.Tick(In.Press(GameButton.CameraView));
            Assert.Equal(VehicleView.Hood, sim.S.Camera.VehicleView);
            Assert.Equal(foot, sim.S.Camera.FootView);
            for (int i = 0; i < 3; i++) sim.Tick(In.Press(GameButton.CameraView));
            Assert.Equal(VehicleView.FirstPerson, sim.S.Camera.VehicleView);
        }

        [Fact]
        public void ChasePitchIsLimited_To_Minus30_Plus45_AndRecentresAfter1_5s()
        {
            Car car;
            Sim sim = InCar(2, out car);
            sim.Tick(In.Look(0f, 200f));
            Sim.Near(45f, sim.PS.ViewPitchDeg, 0.01f);
            sim.Tick(In.Look(0f, -400f));
            Sim.Near(-30f, sim.PS.ViewPitchDeg, 0.01f);
            sim.Tick(In.Look(40f, 0f));
            sim.Run(1.3, In.Idle);
            Assert.True(Math.Abs(sim.PS.ViewYawDeg) > 30f, "still looking away before 1.5 s");
            sim.Run(4.0, In.Idle);
            Assert.True(Sim.AngleDiff(0f, sim.PS.ViewYawDeg) < 6f, "recentred behind the car");
        }
    }
}
