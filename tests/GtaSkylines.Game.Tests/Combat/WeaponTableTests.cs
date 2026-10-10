using System;
using GtaSkylines.Game.Combat;
using GtaSkylines.Game.Profile;
using Xunit;

namespace GtaSkylines.Game.Tests.Combat
{
    /// <summary>The contract's weapon constants and the spread rules.</summary>
    public class WeaponTableTests
    {
        [Theory]
        [InlineData(WeaponId.Fists, 8f, 1.2f, 2.2f, 0, 0f, 0f, 1, false)]
        [InlineData(WeaponId.Bat, 26f, 1.7f, 1.4f, 0, 0f, 0f, 1, false)]
        [InlineData(WeaponId.Pistol, 26f, 70f, 3.5f, 12, 1.4f, 1.2f, 1, true)]
        [InlineData(WeaponId.Smg, 14f, 45f, 12f, 30, 2.0f, 3.0f, 1, true)]
        [InlineData(WeaponId.Shotgun, 11f, 22f, 1.1f, 8, 2.8f, 6f, 8, true)]
        [InlineData(WeaponId.Carbine, 30f, 100f, 8f, 30, 2.2f, 1.0f, 1, true)]
        public void ValuesMatchTheContract(WeaponId id, float dmg, float range, float rate, int mag, float reload, float spread,
            int pellets, bool firearm)
        {
            var w = WeaponTable.Get(id);
            Assert.Equal(dmg, w.Damage);
            Assert.Equal(range, w.Range);
            Assert.Equal(rate, w.RatePerSecond);
            Assert.Equal(mag, w.Magazine);
            Assert.Equal(reload, w.ReloadSeconds);
            Assert.Equal(spread, w.SpreadDeg);
            Assert.Equal(firearm, w.IsFirearm);
            if (firearm) Assert.Equal(pellets, w.Pellets);
        }

        [Fact]
        public void MeleeKnockDownChancesAreQuarterAndSeventyPercent()
        {
            Assert.Equal(0.25f, WeaponTable.Get(WeaponId.Fists).KnockDownChance);
            Assert.Equal(0.70f, WeaponTable.Get(WeaponId.Bat).KnockDownChance);
        }

        [Fact]
        public void UndefinedWeaponThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => WeaponTable.Get((WeaponId)99));
        }

        [Theory]
        [InlineData(WeaponId.Pistol, 1.2f)]
        [InlineData(WeaponId.Smg, 3.0f)]
        [InlineData(WeaponId.Shotgun, 6f)]
        [InlineData(WeaponId.Carbine, 1.0f)]
        public void SpreadDoublesMovingHalvesAimingStillAndMovingWins(WeaponId id, float baseSpread)
        {
            Assert.Equal(baseSpread, WeaponTable.EffectiveSpreadDeg(id, false, false), 4);
            Assert.Equal(baseSpread * 2f, WeaponTable.EffectiveSpreadDeg(id, true, false), 4);
            Assert.Equal(baseSpread * 0.5f, WeaponTable.EffectiveSpreadDeg(id, false, true), 4);
            Assert.Equal(baseSpread * 2f, WeaponTable.EffectiveSpreadDeg(id, true, true), 4);
        }

        [Fact]
        public void MeleeHasNoSpread()
        {
            Assert.Equal(0f, WeaponTable.EffectiveSpreadDeg(WeaponId.Fists, true, false));
            Assert.Equal(0f, WeaponTable.EffectiveSpreadDeg(WeaponId.Bat, false, true));
        }
    }
}
