using System;
using GtaSkylines.Game.Combat;
using GtaSkylines.Game.Profile;
using Xunit;

namespace GtaSkylines.Game.Tests.Combat
{
    /// <summary>WeaponWheelModel sectors, pointer, selection, and CombatSystem.Cycle.</summary>
    public class WeaponWheelTests
    {
        private static PlayerProfile Profile(params WeaponId[] owned)
        {
            var p = new PlayerProfile();
            foreach (var w in owned)
            {
                p.GiveWeapon(w);
                if (WeaponInfo.UsesAmmo(w)) p.SetAmmo(w, 20);
            }
            return p;
        }

        [Theory]
        [InlineData(0f, 1f, 0)]
        [InlineData(0.87f, 0.5f, 1)]
        [InlineData(1f, 0f, 2)]
        [InlineData(0f, -1f, 3)]
        [InlineData(-0.87f, -0.5f, 4)]
        [InlineData(-1f, 0f, 5)]
        [InlineData(-0.1f, 1f, 0)]
        [InlineData(0.1f, 1f, 0)]
        public void SectorIsChosenByDirectionClockwiseFromUp(float x, float y, int sector)
        {
            Assert.Equal(sector, WeaponWheelModel.SectorOf(x, y));
        }

        [Fact]
        public void OpeningHighlightsTheWeaponInHandWithTheStickCentred()
        {
            var w = new WeaponWheelModel();
            w.Open(Profile(WeaponId.Pistol), WeaponId.Pistol);
            Assert.True(w.IsOpen);
            Assert.Equal((int)WeaponId.Pistol, w.Highlighted);
            Assert.Equal(0f, w.PointerX);
            Assert.Equal(0f, w.PointerY);
            Assert.Equal(WeaponWheelModel.OpenTimeScale, w.DesiredTimeScale);
        }

        [Fact]
        public void ClosedWheelRunsAtNormalSpeed()
        {
            Assert.Equal(1f, new WeaponWheelModel().DesiredTimeScale);
        }

        [Fact]
        public void LookDeltasMoveTheStickAndPickTheSectorUnderIt()
        {
            var w = new WeaponWheelModel();
            w.Open(Profile(WeaponId.Pistol), WeaponId.Fists);
            w.MovePointer(15f, 15f);
            Assert.Equal(0.5f, w.PointerX, 3);
            Assert.Equal(0.5f, w.PointerY, 3);
            Assert.Equal((int)WeaponId.Bat, w.Highlighted);
            w.MovePointer(0f, -30f);
            Assert.Equal((int)WeaponId.Pistol, w.Highlighted);
        }

        [Fact]
        public void InsideTheDeadZoneTheHighlightStays()
        {
            var w = new WeaponWheelModel();
            w.Open(Profile(WeaponId.Pistol), WeaponId.Pistol);
            w.MovePointer(3f, 0f);
            Assert.Equal((int)WeaponId.Pistol, w.Highlighted);
        }

        [Fact]
        public void TheStickIsClampedToUnitLength()
        {
            var w = new WeaponWheelModel();
            w.Open(Profile(), WeaponId.Fists);
            w.MovePointer(300f, 0f);
            Assert.Equal(1f, w.PointerX, 3);
            w.MovePointer(300f, 300f);
            Assert.True(w.PointerX * w.PointerX + w.PointerY * w.PointerY <= 1.0001f);
        }

        [Fact]
        public void PointerIsIgnoredWhileClosed()
        {
            var w = new WeaponWheelModel();
            w.MovePointer(30f, 30f);
            Assert.Equal(0f, w.PointerX);
        }

        [Fact]
        public void CloseSelectsASelectableHighlightAndNothingOtherwise()
        {
            var profile = Profile(WeaponId.Pistol);
            var w = new WeaponWheelModel();
            w.Open(profile, WeaponId.Fists);
            w.MovePointer(0f, -60f);
            WeaponId sel;
            Assert.False(w.Close(out sel), "Smg sector is not owned");
            Assert.False(w.IsOpen);
            w.Open(profile, WeaponId.Fists);
            w.MovePointer(30f, -30f);
            Assert.True(w.Close(out sel));
            Assert.Equal(WeaponId.Pistol, sel);
        }

        [Fact]
        public void SlotsReflectOwnershipAmmoAndSelectability()
        {
            var profile = Profile(WeaponId.Pistol, WeaponId.Smg);
            profile.SetAmmo(WeaponId.Smg, 0);
            var w = new WeaponWheelModel();
            w.Refresh(profile);
            Assert.True(w.GetSlot(0).Owned);
            Assert.True(w.GetSlot(0).Selectable);
            Assert.False(w.GetSlot(1).Owned);
            Assert.False(w.GetSlot(1).Selectable);
            Assert.Equal(20, w.GetSlot(2).Ammo);
            Assert.True(w.GetSlot(2).Selectable);
            Assert.True(w.GetSlot(3).Owned);
            Assert.False(w.GetSlot(3).Selectable, "owned but no ammo");
            Assert.Equal(WeaponId.Pistol, w.GetSlot(2).Weapon);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(6)]
        public void SlotOutOfRangeThrows(int i)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponWheelModel().GetSlot(i));
        }

        [Fact]
        public void CycleSkipsUnownedAndEmptyWeaponsAndWraps()
        {
            var profile = Profile(WeaponId.Pistol, WeaponId.Carbine);
            profile.SetAmmo(WeaponId.Carbine, 0);
            Assert.Equal(WeaponId.Pistol, CombatSystem.Cycle(profile, WeaponId.Fists, 1));
            Assert.Equal(WeaponId.Fists, CombatSystem.Cycle(profile, WeaponId.Pistol, 1));
            Assert.Equal(WeaponId.Pistol, CombatSystem.Cycle(profile, WeaponId.Fists, -1));
            Assert.Equal(WeaponId.Fists, CombatSystem.Cycle(profile, WeaponId.Pistol, -1));
        }

        [Fact]
        public void CycleWithOnlyFistsReturnsFists()
        {
            Assert.Equal(WeaponId.Fists, CombatSystem.Cycle(Profile(), WeaponId.Fists, 1));
        }
    }
}
