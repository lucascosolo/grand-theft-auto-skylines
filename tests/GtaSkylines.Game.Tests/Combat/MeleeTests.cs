using GtaSkylines.Game.Combat;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.Combat
{
    /// <summary>Weapons.Melee: arc, reach, nearest wins, knock-down chance, promotion.</summary>
    public class MeleeTests
    {
        [Fact]
        public void FistHitsAPedStraightAheadWithinReach()
        {
            var a = new Arena();
            var attacker = a.Spawn(0f, 0f);
            var victim = a.Spawn(0f, 1.0f, 180f);
            long seq = a.World.Events.NextSeq;
            var r = Weapons.Melee(a.World, attacker, WeaponId.Fists);
            Assert.True(r.Hit);
            Assert.Equal(victim.Tag, r.VictimTag);
            Assert.Equal(8f, r.Damage);
            Assert.Equal(92f, victim.Health, 2);
            Assert.Equal(PedAnim.Punch, attacker.Anim);
            var punched = a.EventsSince(seq).Find(e => e.Type == GameEventType.Punched);
            Assert.Equal(attacker.Tag, punched.ActorA);
            Assert.Equal(victim.Tag, punched.ActorB);
            Assert.Equal(8f, punched.Value);
            Assert.Equal("Fists", punched.Text);
        }

        [Fact]
        public void MissesAPedBehindAndEmitsNothing()
        {
            var a = new Arena();
            var attacker = a.Spawn(0f, 0f);
            var victim = a.Spawn(0f, -1.0f);
            long seq = a.World.Events.NextSeq;
            var r = Weapons.Melee(a.World, attacker, WeaponId.Fists);
            Assert.False(r.Hit);
            Assert.Equal(0, r.VictimTag);
            Assert.Equal(100f, victim.Health);
            Assert.Equal(seq, a.World.Events.NextSeq);
        }

        [Theory]
        [InlineData(0.55f, 1.0f, true)]
        [InlineData(0.8f, 1.0f, false)]
        [InlineData(-0.55f, 1.0f, true)]
        [InlineData(-0.8f, 1.0f, false)]
        public void ArcIsPlusMinusThirtyFiveDegrees(float x, float z, bool hit)
        {
            var a = new Arena();
            var attacker = a.Spawn(0f, 0f);
            a.Spawn(x, z);
            Assert.Equal(hit, Weapons.Melee(a.World, attacker, WeaponId.Fists).Hit);
        }

        [Fact]
        public void ArcFollowsTheAttackersYaw()
        {
            var a = new Arena();
            var attacker = a.Spawn(0f, 0f, 90f);
            a.Spawn(1f, 0f);
            Assert.True(Weapons.Melee(a.World, attacker, WeaponId.Fists).Hit);
        }

        [Fact]
        public void ReachIsOnePointTwoForFistsAndOnePointSevenForABat()
        {
            var a = new Arena();
            var attacker = a.Spawn(0f, 0f);
            var victim = a.Spawn(0f, 1.5f);
            Assert.False(Weapons.Melee(a.World, attacker, WeaponId.Fists).Hit);
            var r = Weapons.Melee(a.World, attacker, WeaponId.Bat);
            Assert.True(r.Hit);
            Assert.Equal(26f, r.Damage);
            Assert.Equal(74f, victim.Health, 2);
        }

        [Fact]
        public void TheNearestPersonInTheArcIsHit()
        {
            var a = new Arena();
            var attacker = a.Spawn(0f, 0f);
            a.Spawn(0f, 1.1f);
            var near = a.Spawn(0.1f, 0.6f);
            Assert.Equal(near.Tag, Weapons.Melee(a.World, attacker, WeaponId.Fists).VictimTag);
        }

        [Fact]
        public void DeadAndOtherFloorPedsAreIgnored()
        {
            var a = new Arena();
            var attacker = a.Spawn(0f, 0f);
            var dead = a.Spawn(0f, 0.8f);
            dead.Flags |= PedFlags.Dead;
            var upstairs = a.World.SpawnPed(PedKind.Civilian, 3, new Vec3(0f, 3f, 0.9f), 0f);
            Assert.NotNull(upstairs);
            Assert.False(Weapons.Melee(a.World, attacker, WeaponId.Fists).Hit);
        }

        [Fact]
        public void AnAmbientCitizenInFrontIsPromotedAndHurt()
        {
            var a = new Arena();
            var attacker = a.Spawn(0f, 0f);
            a.AddAmbient(31, 999u, 0f, 1.0f, 180f);
            var r = Weapons.Melee(a.World, attacker, WeaponId.Fists);
            Assert.True(r.Hit);
            Ped promoted = a.World.FindPedByCitizen(999u);
            Assert.NotNull(promoted);
            Assert.Equal(promoted.Tag, r.VictimTag);
            Assert.Equal(92f, promoted.Health, 2);
        }

        [Fact]
        public void FirearmThroughMeleeThrows()
        {
            var a = new Arena();
            var attacker = a.Spawn(0f, 0f);
            Assert.Throws<System.ArgumentException>(() => Weapons.Melee(a.World, attacker, WeaponId.Pistol));
        }

        private static int KnockDowns(WeaponId w, int swings)
        {
            var a = new Arena(55u);
            var attacker = a.Spawn(0f, 0f);
            var victim = a.Spawn(0f, 1f, 180f);
            int downs = 0;
            for (int i = 0; i < swings; i++)
            {
                victim.Health = 100f;
                victim.Flags &= ~(PedFlags.KnockedDown | PedFlags.Injured);
                victim.Brain = null;
                if (Weapons.Melee(a.World, attacker, w).Outcome.KnockedDown) downs++;
            }
            return downs;
        }

        [Fact]
        public void FistsKnockDownAboutAQuarterAndBatsAboutSeventyPercent()
        {
            Assert.InRange(KnockDowns(WeaponId.Fists, 400) / 400f, 0.18f, 0.32f);
            Assert.InRange(KnockDowns(WeaponId.Bat, 400) / 400f, 0.62f, 0.78f);
        }

        [Fact]
        public void KnockDownIsReproducibleForASeed()
        {
            Assert.Equal(KnockDowns(WeaponId.Bat, 60), KnockDowns(WeaponId.Bat, 60));
        }
    }
}
