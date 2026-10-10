using System;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Mod.Rendering;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class WeaponModelsTests
    {
        const float NoShot = 10f;
        const float NoReload = -1f;

        [Fact]
        public void Fists_have_no_geometry()
        {
            Assert.Equal(0, WeaponModels.Build(ViewWeapon.Fists).VertexCount);
        }

        [Theory]
        [InlineData(ViewWeapon.Bat, 0.75f, 0.95f)]
        [InlineData(ViewWeapon.Pistol, 0.15f, 0.30f)]
        [InlineData(ViewWeapon.Smg, 0.30f, 0.55f)]
        [InlineData(ViewWeapon.Shotgun, 0.80f, 1.10f)]
        [InlineData(ViewWeapon.Carbine, 0.70f, 1.00f)]
        public void Model_is_valid_and_sized_along_z(ViewWeapon w, float minLen, float maxLen)
        {
            var m = WeaponModels.Build(w);
            MeshAsserts.Invariants(m);
            Assert.InRange(m.TriangleCount, 1, 2000);
            Vec3 min, max;
            MeshAsserts.Bounds(m, out min, out max);
            Assert.InRange(max.Z - min.Z, minLen, maxLen);
        }

        [Theory]
        [InlineData(ViewWeapon.Pistol)]
        [InlineData(ViewWeapon.Smg)]
        [InlineData(ViewWeapon.Shotgun)]
        [InlineData(ViewWeapon.Carbine)]
        public void Muzzle_sits_at_the_front_of_the_barrel(ViewWeapon w)
        {
            var m = WeaponModels.Build(w);
            Vec3 min, max;
            MeshAsserts.Bounds(m, out min, out max);
            var mz = WeaponModels.Muzzle(w);
            Assert.InRange(mz.Z, max.Z - 0.02f, max.Z + 0.02f);
            Assert.InRange(mz.X, min.X, max.X);
            Assert.InRange(mz.Y, min.Y, max.Y);
        }

        [Theory]
        [InlineData(ViewWeapon.Fists)]
        [InlineData(ViewWeapon.Bat)]
        public void Melee_muzzle_is_zero(ViewWeapon w)
        {
            Assert.Equal(Vec3.Zero, WeaponModels.Muzzle(w));
        }

        [Theory]
        [InlineData(ViewWeapon.Pistol, 0.04f, 6f)]
        [InlineData(ViewWeapon.Smg, 0.025f, 3f)]
        [InlineData(ViewWeapon.Shotgun, 0.09f, 12f)]
        [InlineData(ViewWeapon.Carbine, 0.035f, 4f)]
        public void Recoil_decays_exponentially(ViewWeapon w, float kick, float climb)
        {
            var p0 = WeaponModels.Pose(w, 0f, NoReload);
            Assert.Equal(-kick, p0.Offset.Z, 4);
            Assert.Equal(climb, p0.PitchUpDeg, 3);
            float e = (float)Math.Exp(-0.1 / 0.08);
            var p = WeaponModels.Pose(w, 0.1f, NoReload);
            Assert.Equal(-kick * e, p.Offset.Z, 4);
            Assert.Equal(climb * e, p.PitchUpDeg, 3);
            Assert.Equal(0f, p.Offset.X, 4);
            Assert.Equal(0f, p.Offset.Y, 4);
            float prev = float.MaxValue;
            for (float t = 0f; t < 0.4f; t += 0.02f)
            {
                float mag = Math.Abs(WeaponModels.Pose(w, t, NoReload).Offset.Z);
                Assert.True(mag < prev);
                prev = mag;
            }
        }

        [Theory]
        [InlineData(0.4f)]
        [InlineData(-1f)]
        [InlineData(5f)]
        public void No_recoil_outside_the_shot_window(float since)
        {
            var p = WeaponModels.Pose(ViewWeapon.Pistol, since, NoReload);
            Assert.Equal(Vec3.Zero, p.Offset);
            Assert.Equal(0f, p.PitchUpDeg, 4);
        }

        [Fact]
        public void Fists_never_move()
        {
            var p = WeaponModels.Pose(ViewWeapon.Fists, 0.1f, 0.5f);
            Assert.Equal(Vec3.Zero, p.Offset);
            Assert.Equal(0f, p.PitchUpDeg, 4);
        }

        [Fact]
        public void Bat_swings_for_035_seconds()
        {
            Assert.Equal(-80f, WeaponModels.Pose(ViewWeapon.Bat, 0.175f, NoReload).PitchUpDeg, 3);
            Assert.Equal(0f, WeaponModels.Pose(ViewWeapon.Bat, 0f, NoReload).PitchUpDeg, 3);
            Assert.Equal(Vec3.Zero, WeaponModels.Pose(ViewWeapon.Bat, 0.175f, NoReload).Offset);
            var after = WeaponModels.Pose(ViewWeapon.Bat, 0.35f, NoReload);
            Assert.Equal(0f, after.PitchUpDeg, 4);
            Assert.Equal(0f, WeaponModels.Pose(ViewWeapon.Bat, 0.39f, NoReload).PitchUpDeg, 4);
        }

        [Fact]
        public void Bat_does_not_reload_move()
        {
            var p = WeaponModels.Pose(ViewWeapon.Bat, NoShot, 0.5f);
            Assert.Equal(Vec3.Zero, p.Offset);
            Assert.Equal(0f, p.PitchUpDeg, 4);
        }

        [Fact]
        public void Reload_peaks_at_half_progress()
        {
            var p = WeaponModels.Pose(ViewWeapon.Pistol, NoShot, 0.5f);
            Assert.Equal(-0.2f, p.Offset.Y, 4);
            Assert.Equal(-35f, p.PitchUpDeg, 3);
            var start = WeaponModels.Pose(ViewWeapon.Pistol, NoShot, 0f);
            Assert.Equal(0f, start.Offset.Y, 4);
            Assert.Equal(0f, WeaponModels.Pose(ViewWeapon.Pistol, NoShot, 1f).PitchUpDeg, 3);
            Assert.Equal(0f, WeaponModels.Pose(ViewWeapon.Pistol, NoShot, 1.5f).Offset.Y, 4);
            Assert.True(WeaponModels.Pose(ViewWeapon.Pistol, NoShot, 0.25f).Offset.Y > p.Offset.Y);
        }

        [Fact]
        public void Reload_adds_to_recoil()
        {
            var p = WeaponModels.Pose(ViewWeapon.Pistol, 0f, 0.5f);
            Assert.Equal(-0.04f, p.Offset.Z, 4);
            Assert.Equal(-0.2f, p.Offset.Y, 4);
            Assert.Equal(6f - 35f, p.PitchUpDeg, 3);
        }
    }
}
