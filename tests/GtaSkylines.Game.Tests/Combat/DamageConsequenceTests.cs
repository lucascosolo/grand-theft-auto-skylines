using GtaSkylines.Game.City;
using GtaSkylines.Game.Combat;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Peds;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.Combat
{
    /// <summary>Damage.ApplyToPed, vehicle impacts and explosions with their events and city effects.</summary>
    public class DamageConsequenceTests
    {
        private static readonly int SomeCar = WorldTag.ForCar(99);

        [Fact]
        public void LethalDamageKillsOnceAndFurtherDamageIsIgnored()
        {
            var a = new Arena();
            var p = a.Spawn(0f, 0f);
            p.Citizen = 55u;
            long seq = a.World.Events.NextSeq;
            var r = Damage.ApplyToPed(a.World, p, 150f, a.Player.Tag, DamageKind.Bullet, false, 0f);
            Assert.True(r.Killed);
            Assert.True(p.IsDead);
            Assert.Equal(0f, p.Health);
            Assert.Equal(PedAnim.LieDown, p.Anim);
            var again = Damage.ApplyToPed(a.World, p, 50f, a.Player.Tag, DamageKind.Bullet, true, 0f);
            Assert.False(again.Killed);
            Assert.Equal(1, Arena.Count(a.EventsSince(seq), GameEventType.PedKilled));
            Assert.Equal(1, a.CountEffects(CityEffectKind.KillCitizen));
            Assert.Equal(0, a.CountEffects(CityEffectKind.InjureCitizen));
        }

        [Fact]
        public void DeathRecordsAttackerAndTimeAndClearsTransientFlags()
        {
            var a = new Arena();
            var p = a.Spawn(0f, 0f);
            p.Flags |= PedFlags.HandsUp | PedFlags.Panicking | PedFlags.OnPhone;
            Damage.ApplyToPed(a.World, p, 500f, a.Player.Tag, DamageKind.Melee, false, 0f);
            Assert.Equal(a.Player.Tag, p.LastAttackerTag);
            Assert.Equal(a.World.Clock.GameSeconds, p.LastDamageTime);
            Assert.False(p.Has(PedFlags.HandsUp));
            Assert.False(p.Has(PedFlags.Panicking));
            Assert.False(p.Has(PedFlags.OnPhone));
        }

        [Fact]
        public void InjuryBelowThirtyFiveFlagsOncePerPedAndRaisesOneInjureCitizen()
        {
            var a = new Arena();
            var p = a.Spawn(0f, 0f);
            p.Citizen = 66u;
            long seq = a.World.Events.NextSeq;
            var first = Damage.ApplyToPed(a.World, p, 70f, a.Player.Tag, DamageKind.Bullet, false, 0f);
            Assert.True(first.BecameInjured);
            Assert.True(p.Has(PedFlags.Injured));
            var second = Damage.ApplyToPed(a.World, p, 5f, a.Player.Tag, DamageKind.Bullet, false, 0f);
            Assert.False(second.BecameInjured);
            Assert.Equal(1, a.CountEffects(CityEffectKind.InjureCitizen));
            Assert.Equal(1, Arena.Count(a.EventsSince(seq), GameEventType.PedInjured));
        }

        [Fact]
        public void AHitThatLeavesThirtyFiveOrMoreDoesNotInjure()
        {
            var a = new Arena();
            var p = a.Spawn(0f, 0f);
            var r = Damage.ApplyToPed(a.World, p, 65f, a.Player.Tag, DamageKind.Bullet, false, 0f);
            Assert.False(r.BecameInjured);
            Assert.False(p.Has(PedFlags.Injured));
        }

        [Fact]
        public void KnockDownSetsFlagEventAndKnockedDownBrain()
        {
            var a = new Arena();
            var p = a.Spawn(0f, 0f);
            p.Brain = new IdleBrain(a.World, default(PedTraits));
            long seq = a.World.Events.NextSeq;
            var r = Damage.ApplyToPed(a.World, p, 5f, a.Player.Tag, DamageKind.Melee, true, 0f);
            Assert.True(r.KnockedDown);
            Assert.True(p.Has(PedFlags.KnockedDown));
            Assert.IsType<KnockedDownBrain>(p.Brain);
            Assert.Equal(1, Arena.Count(a.EventsSince(seq), GameEventType.PedKnockedDown));
        }

        [Fact]
        public void NullDeadAndZeroDamageAreNoOps()
        {
            var a = new Arena();
            var p = a.Spawn(0f, 0f);
            Assert.Equal(0f, Damage.ApplyToPed(a.World, null, 10f, 0, DamageKind.Other, false, 0f).HealthLost);
            Assert.Equal(0f, Damage.ApplyToPed(a.World, p, 0f, 0, DamageKind.Other, false, 0f).HealthLost);
            Assert.Equal(100f, p.Health);
        }

        [Theory]
        [InlineData(4f, 100f, false)]
        [InlineData(3f, 100f, false)]
        [InlineData(6f, 86f, true)]
        [InlineData(14f, 30f, true)]
        public void VehicleImpactSpeedMapsToDamageAndKnockDown(float speed, float healthLeft, bool down)
        {
            var a = new Arena();
            var p = a.Spawn(0f, 0f);
            Damage.HitByVehicle(a.World, p.Tag, SomeCar, speed);
            Assert.Equal(healthLeft, p.Health, 2);
            Assert.Equal(down, p.Has(PedFlags.KnockedDown));
        }

        [Fact]
        public void VehicleImpactAboveFourteenKillsOutright()
        {
            var a = new Arena();
            var p = a.Spawn(0f, 0f);
            p.Armour = 100f;
            long seq = a.World.Events.NextSeq;
            Damage.HitByVehicle(a.World, p.Tag, SomeCar, 14.5f);
            Assert.True(p.IsDead);
            var ev = a.EventsSince(seq);
            Assert.Equal(1, Arena.Count(ev, GameEventType.PedKnockedDown));
            Assert.Equal(1, Arena.Count(ev, GameEventType.PedKilled));
            Assert.True(ev.FindIndex(e => e.Type == GameEventType.PedKnockedDown) < ev.FindIndex(e => e.Type == GameEventType.PedKilled));
        }

        [Fact]
        public void SlowVehicleBumpDoesNotPromoteAnAmbientCitizen()
        {
            var a = new Arena();
            var c = a.AddAmbient(12, 321u, 0f, 5f);
            Damage.HitByVehicle(a.World, WorldTag.ForAmbientCitizen(c.Instance), SomeCar, 3f);
            Assert.Null(a.World.FindPedByCitizen(321u));
        }

        [Fact]
        public void FastVehicleHitOnAnAmbientCitizenPromotesInjuresAndKills()
        {
            var a = new Arena();
            var c = a.AddAmbient(13, 322u, 0f, 5f);
            Damage.HitByVehicle(a.World, WorldTag.ForAmbientCitizen(c.Instance), SomeCar, 11f);
            Ped p = a.World.FindPedByCitizen(322u);
            Assert.NotNull(p);
            Assert.Equal(51f, p.Health, 2);
            Assert.Equal(0, a.CountEffects(CityEffectKind.KillCitizen));
            var c2 = a.AddAmbient(14, 323u, 5f, 5f);
            Damage.HitByVehicle(a.World, WorldTag.ForAmbientCitizen(c2.Instance), SomeCar, 20f);
            Assert.True(a.World.FindPedByCitizen(323u).IsDead);
            Assert.Equal(1, a.CountEffects(CityEffectKind.KillCitizen));
        }

        [Theory]
        [InlineData(0f, 100f, true)] // full damage at the centre: 100 x (1 - 0/8)
        [InlineData(4f, 50f, true)]
        [InlineData(7.9f, 1.25f, true)]
        public void ExplosionDamageFallsOffLinearlyToZeroAtEightMetres(float d, float damage, bool down)
        {
            var a = new Arena();
            var p = a.Spawn(d, 0f);
            p.Health = 200f;
            Assert.Equal(1, Damage.Explode(a.World, Vec3.Zero, 0));
            Assert.Equal(200f - damage, p.Health, 0.6);
            Assert.Equal(down, p.Has(PedFlags.KnockedDown));
        }

        [Fact]
        public void ExplosionAtTheCentreKillsAFullHealthPed()
        {
            var a = new Arena();
            var p = a.Spawn(0f, 0f);
            Damage.Explode(a.World, Vec3.Zero, 0);
            Assert.True(p.IsDead);
        }

        [Theory]
        [InlineData(8f, true)]
        [InlineData(10f, true)]
        [InlineData(11.9f, true)]
        [InlineData(12.5f, false)]
        public void BetweenEightAndTwelveMetresOnlyKnockDown(float d, bool down)
        {
            var a = new Arena();
            var p = a.Spawn(d, 0f);
            Damage.Explode(a.World, Vec3.Zero, 0);
            Assert.Equal(down, p.Has(PedFlags.KnockedDown));
            Assert.Equal(100f, p.Health);
        }

        [Fact]
        public void ExplosionSkipsPedsInCarsAndDeadPedsAndCountsTheRest()
        {
            var a = new Arena();
            a.Spawn(2f, 0f);
            var dead = a.Spawn(3f, 0f);
            dead.Flags |= PedFlags.Dead;
            var hidden = a.Spawn(4f, 0f);
            hidden.Flags |= PedFlags.InVehicle;
            Assert.Equal(1, Damage.Explode(a.World, Vec3.Zero, 0));
        }

        [Fact]
        public void ExplosionPromotesAmbientCitizensInRange()
        {
            var a = new Arena();
            a.AddAmbient(21, 700u, 5f, 0f);
            a.AddAmbient(22, 701u, 30f, 0f);
            Assert.Equal(1, Damage.Explode(a.World, Vec3.Zero, 0));
            Assert.NotNull(a.World.FindPedByCitizen(700u));
            Assert.Null(a.World.FindPedByCitizen(701u));
        }
    }
}
