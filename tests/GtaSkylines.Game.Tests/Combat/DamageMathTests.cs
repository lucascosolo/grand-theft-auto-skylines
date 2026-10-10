using GtaSkylines.Game.Combat;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.Combat
{
    /// <summary>Pure damage maths: armour split, headshot test, regeneration.</summary>
    public class DamageMathTests
    {
        [Fact]
        public void ArmourAbsorbsSixtySixPercentWhileItLasts()
        {
            float armourLoss;
            float health = Damage.SplitArmour(30f, 100f, out armourLoss);
            Assert.Equal(10.2f, health, 3);
            Assert.Equal(19.8f, armourLoss, 3);
        }

        [Fact]
        public void ArmourThatRunsOutAbsorbsOnlyWhatItHas()
        {
            float armourLoss;
            float health = Damage.SplitArmour(30f, 5f, out armourLoss);
            Assert.Equal(25f, health, 3);
            Assert.Equal(5f, armourLoss, 3);
        }

        [Fact]
        public void NoArmourMeansFullHealthLoss()
        {
            float armourLoss;
            Assert.Equal(26f, Damage.SplitArmour(26f, 0f, out armourLoss), 3);
            Assert.Equal(0f, armourLoss);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-5f)]
        public void NonPositiveAmountDoesNothing(float amount)
        {
            float armourLoss;
            Assert.Equal(0f, Damage.SplitArmour(amount, 50f, out armourLoss));
            Assert.Equal(0f, armourLoss);
        }

        [Fact]
        public void HitAboveOnePointFiveMetresOnAStandingPedIsAHeadshot()
        {
            var p = Bare.Ped(2f, 3f, 4f);
            Assert.True(Damage.IsHeadshot(p, new Vec3(2f, 4.6f, 4f)));
            Assert.False(Damage.IsHeadshot(p, new Vec3(2f, 4.4f, 4f)));
            Assert.False(Damage.IsHeadshot(p, new Vec3(2f, 4.5f, 4f)), "exactly 1.5 m is not above 1.5");
        }

        [Fact]
        public void NoHeadshotOnAKnockedDownOrDeadPed()
        {
            var down = Bare.Ped();
            down.Flags |= PedFlags.KnockedDown;
            Assert.False(Damage.IsHeadshot(down, new Vec3(0f, 1.7f, 0f)));
            var dead = Bare.Ped();
            dead.Flags |= PedFlags.Dead;
            Assert.False(Damage.IsHeadshot(dead, new Vec3(0f, 1.7f, 0f)));
        }

        private static float Regen(Ped p, double damagedAt, double until)
        {
            p.LastDamageTime = damagedAt;
            double now = damagedAt;
            while (now < until - 1e-9)
            {
                now += 1.0 / 60.0;
                Damage.Regenerate(p, now, 1f / 60f);
            }
            return p.Health;
        }

        [Fact]
        public void RegenerationWaitsFiveSecondsThenGivesTwoPerSecond()
        {
            var p = Bare.Ped(kind: PedKind.Player);
            p.Health = 20f;
            Assert.Equal(20f, Regen(p, 0.0, 4.99), 3);
            Assert.Equal(22f, Regen(Health(20f), 0.0, 6.0), 1);
        }

        [Fact]
        public void RegenerationStopsAtFifty()
        {
            Assert.Equal(50f, Regen(Health(20f), 0.0, 20.0), 2);
            Assert.Equal(50f, Regen(Health(20f), 0.0, 40.0), 2);
        }

        [Fact]
        public void RegenerationNeverLowersOrTouchesHealthAboveFiftyOrAtZero()
        {
            Assert.Equal(80f, Regen(Health(80f), 0.0, 30.0), 3);
            Assert.Equal(0f, Regen(Health(0f), 0.0, 30.0), 3);
        }

        [Fact]
        public void NeverDamagedRegeneratesAtOnce()
        {
            var p = Health(10f);
            p.LastDamageTime = -1.0;
            Damage.Regenerate(p, 0.5, 1f);
            Assert.Equal(12f, p.Health, 3);
        }

        private static Ped Health(float h)
        {
            var p = Bare.Ped(kind: PedKind.Player);
            p.Health = h;
            return p;
        }
    }
}
