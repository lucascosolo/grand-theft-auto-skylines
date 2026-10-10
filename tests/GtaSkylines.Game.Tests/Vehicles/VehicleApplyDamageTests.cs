// Compiled only once the implementer adds VehicleBody.ApplyDamage(float) and defines G5_APPLY_DAMAGE
// (<DefineConstants>$(DefineConstants);G5_APPLY_DAMAGE</DefineConstants> in the test project).
#if G5_APPLY_DAMAGE
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Vehicles;
using Xunit;

namespace GtaSkylines.Game.Tests.Vehicles
{
    public class VehicleApplyDamageTests
    {
        private static Sim OnFlat()
        {
            return new Sim(V.Car(), new Terrain().Flat(500f).ToWorld());
        }

        [Fact]
        public void ApplyDamage_LowersHealth_AndDrivesTheDamageStates()
        {
            Sim s = OnFlat();
            s.Body.ApplyDamage(30f);
            Assert.Equal(70f, s.Body.Health, 0.01);
            Assert.Equal(VehicleDamageState.Intact, s.Body.DamageState);

            s.Body.ApplyDamage(35f);
            Assert.Equal(35f, s.Body.Health, 0.01);
            Assert.Equal(VehicleDamageState.Smoking, s.Body.DamageState);

            s.Body.ApplyDamage(25f);
            Assert.Equal(10f, s.Body.Health, 0.01);
            Assert.Equal(VehicleDamageState.OnFire, s.Body.DamageState);
            Assert.True(s.Body.FireSeconds > 5.5f && s.Body.FireSeconds <= 6f, "fire timer " + s.Body.FireSeconds);

            int explosions = 0;
            for (int i = 0; i < 60 * 8; i++)
            {
                if (s.Step(VehicleControls.None).Exploded) explosions++;
            }
            Assert.Equal(1, explosions);
            Assert.Equal(VehicleDamageState.Exploded, s.Body.DamageState);
            Assert.Equal(0f, s.Body.Health, 1e-4);
        }

        [Fact]
        public void ApplyDamage_ZeroNegativeAndNaN_ChangeNothing()
        {
            Sim s = OnFlat();
            s.Body.ApplyDamage(0f);
            s.Body.ApplyDamage(-20f);
            s.Body.ApplyDamage(float.NaN);
            Assert.Equal(100f, s.Body.Health, 1e-4);
            Assert.Equal(VehicleDamageState.Intact, s.Body.DamageState);
        }

        [Fact]
        public void ApplyDamage_ClampsHealthAtZero_AndLargeDamageSetsTheCarAlight()
        {
            Sim s = OnFlat();
            s.Body.ApplyDamage(500f);
            Assert.True(s.Body.Health >= 0f && s.Body.Health <= 15f);
            Assert.True(s.Body.DamageState == VehicleDamageState.OnFire || s.Body.DamageState == VehicleDamageState.Exploded);
        }

        [Fact]
        public void ApplyDamage_OnAnExplodedBody_IsIgnored()
        {
            Sim s = OnFlat();
            s.Body.ApplyDamage(95f);
            s.Run(VehicleControls.None, 7f);
            Assert.Equal(VehicleDamageState.Exploded, s.Body.DamageState);
            s.Body.ApplyDamage(10f);
            Assert.Equal(0f, s.Body.Health, 1e-4);
            Assert.Equal(VehicleDamageState.Exploded, s.Body.DamageState);
        }
    }
}
#endif
