using System;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Vehicles;
using Xunit;

namespace GtaSkylines.Game.Tests.Vehicles
{
    /// <summary>
    /// Damage states reached through real impacts. DamageScale is raised on the tuning (a public field the body reads every
    /// step) so a single wall hit lands the health in the wanted band.
    /// </summary>
    public class VehicleDamageTests
    {
        private const float WallZ = 120f;

        private static Sim CarAtWall(float damageScale)
        {
            var w = new Terrain().Flat(2000f).ToWorld(1);
            new Terrain().WallAcross(-100f, 100f, WallZ, 0f, 12f).Apply(w, 2);
            var s = new Sim(V.Car(), w, new Vec3(0f, 0.77f, 30f), Quat.Identity);
            s.Tuning.DamageScale = damageScale;
            return s;
        }

        private static VehicleStepResult HitWall(Sim s, float speed)
        {
            for (int i = 0; i < 60 * 40; i++)
            {
                VehicleStepResult r = s.Step(s.Hold(speed));
                if (r.ImpactCount > 0)
                {
                    return r;
                }
            }
            Assert.Fail("never hit the wall");
            return default(VehicleStepResult);
        }

        [Fact]
        public void LightDamage_LeavesTheCarIntact()
        {
            Sim s = CarAtWall(0.15f);
            HitWall(s, 8f);
            Assert.True(s.Body.Health > 60f);
            Assert.Equal(VehicleDamageState.Intact, s.Body.DamageState);
            Assert.Equal(0f, s.Body.FireSeconds, 1e-4);
        }

        [Fact]
        public void HealthBelowForty_IsSmoking_NotYetOnFire()
        {
            Sim s = CarAtWall(0.73f);
            HitWall(s, 14f);
            Assert.True(s.Body.Health < 40f && s.Body.Health > 15f, "health " + s.Body.Health);
            Assert.Equal(VehicleDamageState.Smoking, s.Body.DamageState);
            Assert.Equal(0f, s.Body.FireSeconds, 1e-4);
            s.Run(VehicleControls.None, 8f);
            Assert.Equal(VehicleDamageState.Smoking, s.Body.DamageState);
        }

        [Fact]
        public void HealthAtOrBelowFifteen_IsOnFire_WithSixSecondTimerCountingDown()
        {
            Sim s = CarAtWall(5f);
            HitWall(s, 14f);
            Assert.True(s.Body.Health <= 15f, "health " + s.Body.Health);
            Assert.Equal(VehicleDamageState.OnFire, s.Body.DamageState);
            V.Near(6f, s.Body.FireSeconds, 0.1f);
            s.Run(VehicleControls.None, 1f);
            Assert.Equal(VehicleDamageState.OnFire, s.Body.DamageState);
            V.Near(5f, s.Body.FireSeconds, 0.15f);
        }

        [Fact]
        public void OnFire_ExplodesAfterSixSeconds_FlagRaisedOnceThenStateIsPermanent()
        {
            Sim s = CarAtWall(5f);
            HitWall(s, 14f);
            int explosions = 0;
            int explodedAt = -1;
            for (int i = 0; i < 60 * 9; i++)
            {
                VehicleStepResult r = s.Step(VehicleControls.None);
                if (r.Exploded)
                {
                    explosions++;
                    explodedAt = i;
                }
                if (s.Time < 5.5f)
                {
                    Assert.Equal(VehicleDamageState.OnFire, s.Body.DamageState);
                }
            }
            Assert.Equal(1, explosions);
            Assert.InRange(explodedAt, 60 * 5, 60 * 7);
            Assert.Equal(VehicleDamageState.Exploded, s.Body.DamageState);
            Assert.Equal(0f, s.Body.Health, 1e-4);
            Assert.Equal(0f, s.Body.FireSeconds, 1e-4);
            Assert.Equal(0, s.Body.Gear);
            Assert.Equal(0f, s.Body.EngineRpm, 1e-4);
        }

        [Fact]
        public void ExplodedBody_IgnoresControls_WheelsLocked_ButStillFalls()
        {
            Sim s = CarAtWall(5f);
            HitWall(s, 14f);
            s.Run(VehicleControls.None, 7f);
            Assert.Equal(VehicleDamageState.Exploded, s.Body.DamageState);
            s.Body.Teleport(new Vec3(0f, 0.77f, -200f), Quat.Identity);
            Assert.Equal(VehicleDamageState.Exploded, s.Body.DamageState);
            s.Run(new VehicleControls { Throttle = 1f, Steer = 1f }, 3f);
            Assert.True(Vec3.DistanceXZ(new Vec3(0f, 0f, -200f), s.Body.Position) < 0.5f, "an exploded car drove away");
            Assert.True(Math.Abs(s.Body.SmoothedSteer) < 1e-3f, "steering still smoothing: " + s.Body.SmoothedSteer);
            Assert.True(s.Body.Position.Y > 0.3f && s.Body.Position.Y < 1.5f, "rests on the ground");
            s.Body.Teleport(new Vec3(0f, 10f, -200f), Quat.Identity);
            s.Run(VehicleControls.None, 0.5f);
            Assert.True(s.Body.Position.Y < 9f, "still falls under gravity");
        }

        [Fact]
        public void ExplodedBody_TakesNoFurtherDamage_AndStaysExploded()
        {
            Sim s = CarAtWall(5f);
            HitWall(s, 14f);
            s.Run(VehicleControls.None, 7f);
            s.Body.Teleport(new Vec3(0f, 0.77f, 60f), Quat.Identity);
            s.Run(new VehicleControls { Throttle = 1f }, 0.2f);
            Assert.Equal(0f, s.Body.Health, 1e-4);
            Assert.Equal(VehicleDamageState.Exploded, s.Body.DamageState);
        }

        [Fact]
        public void Teleport_KeepsDamageStateAndFireTimerState()
        {
            Sim s = CarAtWall(5f);
            HitWall(s, 14f);
            s.Run(VehicleControls.None, 1f);
            float fire = s.Body.FireSeconds;
            s.Body.Teleport(new Vec3(0f, 0.77f, -50f), Quat.Identity);
            Assert.Equal(VehicleDamageState.OnFire, s.Body.DamageState);
            V.Near(fire, s.Body.FireSeconds, 0.02f);
        }
    }
}
