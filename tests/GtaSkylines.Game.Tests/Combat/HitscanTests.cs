using GtaSkylines.Game.City;
using GtaSkylines.Game.Combat;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.Combat
{
    /// <summary>Weapons.Fire against peds, ambient citizens, cars, walls; determinism.</summary>
    public class HitscanTests
    {
        private static ShotResult Pistol(Arena a, Ped shooter, Vec3 aimAt)
        {
            Vec3 origin = Weapons.MuzzleOf(shooter);
            return Weapons.Fire(a.World, shooter, WeaponId.Pistol, origin, aimAt - origin, 0f);
        }

        [Fact]
        public void PistolKillsAPedAtTenMetresInExactlyFourBodyShots()
        {
            var a = new Arena();
            var shooter = a.Spawn(0f, 0f);
            var victim = a.Spawn(0f, 10f, 180f);
            a.Rebuild();
            long seq = a.World.Events.NextSeq;
            for (int i = 1; i <= 3; i++)
            {
                var r = Pistol(a, shooter, a.Chest(victim));
                Assert.Equal(ShotHitKind.Person, r.FirstHitKind);
                Assert.Equal(victim.Tag, r.FirstHitTag);
                Assert.Equal(100f - 26f * i, victim.Health, 3);
                Assert.False(victim.IsDead);
            }
            var last = Pistol(a, shooter, a.Chest(victim));
            Assert.True(last.Killed);
            Assert.True(victim.IsDead);
            Assert.Equal(0f, victim.Health);
            Assert.Equal(1, Arena.Count(a.EventsSince(seq), GameEventType.PedKilled));
        }

        [Fact]
        public void HeadshotDoesTwoPointFiveTimesDamage()
        {
            var a = new Arena();
            var shooter = a.Spawn(0f, 0f);
            var victim = a.Spawn(0f, 10f, 180f);
            a.Rebuild();
            var r = Pistol(a, shooter, victim.Position + new Vec3(0f, 1.7f, 0f));
            Assert.Equal(1, r.Headshots);
            Assert.Equal(100f - 65f, victim.Health, 3);
        }

        [Fact]
        public void ArmourTakesTwoThirdsOfABulletAndTheRestHurts()
        {
            var a = new Arena();
            var shooter = a.Spawn(0f, 0f);
            var victim = a.Spawn(0f, 10f, 180f);
            victim.Armour = 100f;
            a.Rebuild();
            Pistol(a, shooter, a.Chest(victim));
            Assert.Equal(100f - 26f * 0.34f, victim.Health, 2);
            Assert.Equal(100f - 26f * 0.66f, victim.Armour, 2);
        }

        [Fact]
        public void ShotEventCarriesOriginEndPointAndTagsAndGunshotEventFollows()
        {
            var a = new Arena();
            var shooter = a.Spawn(0f, 0f);
            var victim = a.Spawn(0f, 10f, 180f);
            a.Rebuild();
            long seq = a.World.Events.NextSeq;
            Pistol(a, shooter, a.Chest(victim));
            var ev = a.EventsSince(seq);
            Assert.Equal(1, Arena.Count(ev, GameEventType.Shot));
            Assert.Equal(1, Arena.Count(ev, GameEventType.Gunshot));
            GameEvent shot = ev.Find(e => e.Type == GameEventType.Shot);
            GameEvent noise = ev.Find(e => e.Type == GameEventType.Gunshot);
            Assert.Equal(shooter.Tag, shot.ActorA);
            Assert.Equal(victim.Tag, shot.ActorB);
            Assert.Equal(26f, shot.Value, 2);
            Assert.Equal(Weapons.MuzzleOf(shooter).Z, shot.Position.Z, 3);
            Assert.InRange(shot.Position2.Z, 9.5f, 10.1f);
            Assert.Equal(60f, noise.Value);
            Assert.Equal(shooter.Tag, noise.ActorA);
            Assert.Equal(PedAnim.Shoot, shooter.Anim);
        }

        [Fact]
        public void AWallBlocksTheShot()
        {
            var a = new Arena();
            var shooter = a.Spawn(0f, 0f);
            var victim = a.Spawn(0f, 10f, 180f);
            a.Wall(-5f, 5f, 5f, 5f);
            a.Rebuild();
            var r = Pistol(a, shooter, a.Chest(victim));
            Assert.Equal(ShotHitKind.Static, r.FirstHitKind);
            Assert.Equal(100f, victim.Health);
            Assert.Equal(5f, r.FirstEndPoint.Z, 2);
        }

        [Fact]
        public void ShotPastRangeEndsAtRangeLimitWithNothingHit()
        {
            var a = new Arena();
            var shooter = a.Spawn(0f, 0f);
            a.Rebuild();
            Vec3 origin = Weapons.MuzzleOf(shooter);
            var r = Weapons.Fire(a.World, shooter, WeaponId.Pistol, origin, Vec3.Forward, 0f);
            Assert.Equal(ShotHitKind.Nothing, r.FirstHitKind);
            Assert.Equal(origin.Z + 70f, r.FirstEndPoint.Z, 2);
        }

        [Fact]
        public void ShooterDoesNotHitHimself()
        {
            var a = new Arena();
            var shooter = a.Spawn(0f, 0f);
            a.Rebuild();
            var r = Weapons.Fire(a.World, shooter, WeaponId.Pistol, shooter.Position + new Vec3(0f, 1f, 0f), Vec3.Forward, 0f);
            Assert.NotEqual(shooter.Tag, r.FirstHitTag);
            Assert.Equal(100f, shooter.Health);
        }

        [Fact]
        public void ShotgunCastsEightRaysAndSpreadsThem()
        {
            var a = new Arena();
            var shooter = a.Spawn(0f, 0f);
            a.Rebuild();
            long seq = a.World.Events.NextSeq;
            Vec3 origin = Weapons.MuzzleOf(shooter);
            var r = Weapons.Fire(a.World, shooter, WeaponId.Shotgun, origin, Vec3.Forward, 6f);
            Assert.Equal(8, r.Rays);
            var ev = a.EventsSince(seq);
            Assert.Equal(8, Arena.Count(ev, GameEventType.Shot));
            Assert.Equal(1, Arena.Count(ev, GameEventType.Gunshot));
            float minX = float.MaxValue, maxX = float.MinValue;
            foreach (var e in ev)
            {
                if (e.Type != GameEventType.Shot) continue;
                if (e.Position2.X < minX) minX = e.Position2.X;
                if (e.Position2.X > maxX) maxX = e.Position2.X;
            }
            Assert.True(maxX - minX > 0.5f, "pellets must not share one line");
            Assert.True(maxX < 22f * 0.11f && minX > -22f * 0.11f, "stay inside the 6 degree cone");
        }

        [Fact]
        public void ShotgunPelletsEachDoElevenDamage()
        {
            var a = new Arena();
            var shooter = a.Spawn(0f, 0f);
            var victim = a.Spawn(0f, 3f, 180f);
            a.Rebuild();
            Vec3 origin = Weapons.MuzzleOf(shooter);
            var r = Weapons.Fire(a.World, shooter, WeaponId.Shotgun, origin, a.Chest(victim) - origin, 0f);
            Assert.True(r.PersonDamage >= 11f * 8f - 0.01f);
        }

        [Fact]
        public void MeleeWeaponThroughTheFullOverloadThrowsAndFireWithFistsDoesNothing()
        {
            var a = new Arena();
            var shooter = a.Spawn(0f, 0f);
            Assert.Throws<System.ArgumentException>(() => Weapons.Fire(a.World, shooter, WeaponId.Bat, Vec3.Zero, Vec3.Forward, 0f));
            shooter.CurrentWeapon = (int)WeaponId.Fists;
            long seq = a.World.Events.NextSeq;
            Weapons.Fire(a.World, shooter, Weapons.MuzzleOf(shooter), Vec3.Forward);
            Assert.Equal(seq, a.World.Events.NextSeq);
        }

        [Fact]
        public void ContractFireUsesTheShootersCurrentWeaponAndTagIgnore()
        {
            var a = new Arena();
            var shooter = a.Spawn(0f, 0f);
            var victim = a.Spawn(0f, 10f, 180f);
            shooter.CurrentWeapon = (int)WeaponId.Carbine;
            a.Rebuild();
            Vec3 origin = Weapons.MuzzleOf(shooter);
            Weapons.Fire(a.World, shooter, origin, a.Chest(victim) - origin);
            Assert.InRange(victim.Health, 100f - 30f * 1.05f, 100f - 30f * 0.95f + 2f);
        }

        [Fact]
        public void AmbientCitizenIsPromotedByTheHitAndKillCitizenIsEmittedOnce()
        {
            var a = new Arena();
            var shooter = a.Spawn(0f, 0f);
            a.AddAmbient(77, 4242u, 0f, 10f, 180f);
            long seq = a.World.Events.NextSeq;
            for (int i = 0; i < 6; i++)
            {
                Vec3 origin = Weapons.MuzzleOf(shooter);
                Weapons.Fire(a.World, shooter, WeaponId.Pistol, origin, new Vec3(0f, 1.2f, 10f) - origin, 0f);
            }
            Ped victim = a.World.FindPedByCitizen(4242u);
            Assert.NotNull(victim);
            Assert.True(victim.IsDead);
            Assert.Equal(1, a.CountEffects(CityEffectKind.KillCitizen));
            Assert.Equal(1, Arena.Count(a.EventsSince(seq), GameEventType.PedKilled));
            foreach (var e in a.World.Effects.Items)
                if (e.Kind == CityEffectKind.KillCitizen) { Assert.Equal(4242u, e.Citizen); Assert.Equal((ushort)0, e.Building); }
        }

        [Fact]
        public void InjuredBelowThirtyFiveEmitsInjureCitizenOnceNotTwice()
        {
            var a = new Arena();
            var shooter = a.Spawn(0f, 0f);
            a.AddAmbient(78, 4243u, 0f, 10f, 180f);
            long seq = a.World.Events.NextSeq;
            for (int i = 0; i < 3; i++)
            {
                Vec3 origin = Weapons.MuzzleOf(shooter);
                Weapons.Fire(a.World, shooter, WeaponId.Pistol, origin, new Vec3(0f, 1.2f, 10f) - origin, 0f);
            }
            Ped victim = a.World.FindPedByCitizen(4243u);
            Assert.Equal(22f, victim.Health, 2);
            Assert.True(victim.Has(PedFlags.Injured));
            Assert.Equal(1, a.CountEffects(CityEffectKind.InjureCitizen));
            Assert.Equal(1, Arena.Count(a.EventsSince(seq), GameEventType.PedInjured));
            Assert.Equal(0, a.CountEffects(CityEffectKind.KillCitizen));
        }

        [Fact]
        public void BulletsDoAQuarterDamageToACar()
        {
            var a = new Arena();
            var shooter = a.Spawn(0f, 0f);
            Car car = a.World.SpawnCar(0, new Vec3(0f, 0f, 12f), 0f, CarOwner.Civilian, 0);
            a.Rebuild();
            float before = car.Body.Health;
            Vec3 origin = Weapons.MuzzleOf(shooter);
            var r = Weapons.Fire(a.World, shooter, WeaponId.Pistol, origin, new Vec3(0f, 1f, 12f) - origin, 0f);
            Assert.Equal(ShotHitKind.Vehicle, r.FirstHitKind);
            Assert.Equal(car.Tag, r.FirstHitTag);
            Assert.Equal(6.5f, r.CarDamage, 2);
            Assert.Equal(before - 6.5f, car.Body.Health, 2);
        }

        [Fact]
        public void SameSeedGivesTheSameSpreadAndADifferentSeedDoesNot()
        {
            Vec3 e1 = ShotgunEnd(99u), e2 = ShotgunEnd(99u), e3 = ShotgunEnd(100u);
            Assert.Equal(e1, e2);
            Assert.NotEqual(e1, e3);
        }

        private static Vec3 ShotgunEnd(uint seed)
        {
            var a = new Arena(seed);
            var shooter = a.Spawn(0f, 0f);
            a.Rebuild();
            return Weapons.Fire(a.World, shooter, WeaponId.Shotgun, Weapons.MuzzleOf(shooter), Vec3.Forward, 6f).FirstEndPoint;
        }

        [Fact]
        public void MuzzleIsAtFeetPlusOnePointFourFiveAndFortyCentimetresForward()
        {
            var a = new Arena();
            var p = a.Spawn(3f, 4f, 90f);
            Vec3 m = Weapons.MuzzleOf(p);
            Assert.Equal(3.4f, m.X, 3);
            Assert.Equal(1.45f, m.Y, 3);
            Assert.Equal(4f, m.Z, 3);
        }
    }
}
