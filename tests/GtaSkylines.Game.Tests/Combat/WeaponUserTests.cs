using GtaSkylines.Game.Combat;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.Combat
{
    /// <summary>Magazine and reload timing (pure) and rate of fire (through a world).</summary>
    public class WeaponUserTests
    {
        private const float Dt = 1f / 60f;

        private static PlayerProfile Owning(WeaponId w, int ammo)
        {
            var p = new PlayerProfile();
            p.GiveWeapon(w);
            p.SetAmmo(w, ammo);
            return p;
        }

        private static void Advance(WeaponUser u, Ped ped, float seconds)
        {
            int n = (int)(seconds / Dt + 0.5f);
            for (int i = 0; i < n; i++) u.Step(ped, Dt);
        }

        [Fact]
        public void EquipFillsTheMagazineFromCarriedAmmo()
        {
            var u = new WeaponUser(Owning(WeaponId.Pistol, 36));
            var ped = Bare.Ped();
            u.Equip(ped, WeaponId.Pistol);
            Assert.Equal((int)WeaponId.Pistol, ped.CurrentWeapon);
            Assert.Equal(12, ped.MagazineAmmo);
        }

        [Fact]
        public void EquipWithLittleAmmoGivesASmallMagazineAndMeleeGivesNone()
        {
            var u = new WeaponUser(Owning(WeaponId.Pistol, 5));
            var ped = Bare.Ped();
            u.Equip(ped, WeaponId.Pistol);
            Assert.Equal(5, ped.MagazineAmmo);
            u.Equip(ped, WeaponId.Fists);
            Assert.Equal(0, ped.MagazineAmmo);
        }

        [Fact]
        public void StepClampsTheMagazineToCarriedAmmo()
        {
            var profile = Owning(WeaponId.Smg, 30);
            var u = new WeaponUser(profile);
            var ped = Bare.Ped();
            u.Equip(ped, WeaponId.Smg);
            profile.SetAmmo(WeaponId.Smg, 4);
            u.Step(ped, Dt);
            Assert.Equal(4, ped.MagazineAmmo);
        }

        [Theory]
        [InlineData(WeaponId.Pistol, 1.4f, 12)]
        [InlineData(WeaponId.Smg, 2.0f, 30)]
        [InlineData(WeaponId.Shotgun, 2.8f, 8)]
        [InlineData(WeaponId.Carbine, 2.2f, 30)]
        public void ReloadTakesTheWeaponsTimeAndRefillsTheMagazine(WeaponId w, float seconds, int magazine)
        {
            var u = new WeaponUser(Owning(w, 60));
            var ped = Bare.Ped();
            u.Equip(ped, w);
            ped.MagazineAmmo = 0;
            Assert.True(u.StartReload(ped));
            Assert.True(u.IsReloading);
            Advance(u, ped, seconds - 0.1f);
            Assert.True(u.IsReloading);
            Assert.Equal(0, ped.MagazineAmmo);
            Advance(u, ped, 0.2f);
            Assert.False(u.IsReloading);
            Assert.Equal(magazine, ped.MagazineAmmo);
        }

        [Fact]
        public void ReloadWithAShortReserveFillsOnlyWhatIsCarried()
        {
            var u = new WeaponUser(Owning(WeaponId.Pistol, 8));
            var ped = Bare.Ped();
            u.Equip(ped, WeaponId.Pistol);
            ped.MagazineAmmo = 3;
            Assert.True(u.StartReload(ped));
            Advance(u, ped, 1.6f);
            Assert.Equal(8, ped.MagazineAmmo);
        }

        [Fact]
        public void ReloadRefusedWhenFullWhenNoReserveOrForMelee()
        {
            var u = new WeaponUser(Owning(WeaponId.Pistol, 12));
            var ped = Bare.Ped();
            u.Equip(ped, WeaponId.Pistol);
            Assert.False(u.StartReload(ped), "magazine already full");
            ped.MagazineAmmo = 0;
            u.Equip(ped, WeaponId.Fists);
            Assert.False(u.StartReload(ped), "melee");
        }

        [Fact]
        public void SecondReloadWhileReloadingIsRefused()
        {
            var u = new WeaponUser(Owning(WeaponId.Pistol, 36));
            var ped = Bare.Ped();
            u.Equip(ped, WeaponId.Pistol);
            ped.MagazineAmmo = 2;
            Assert.True(u.StartReload(ped));
            Assert.False(u.StartReload(ped));
        }

        [Fact]
        public void EquippingAnotherWeaponCancelsTheReload()
        {
            var profile = Owning(WeaponId.Pistol, 36);
            profile.GiveWeapon(WeaponId.Smg);
            profile.SetAmmo(WeaponId.Smg, 30);
            var u = new WeaponUser(profile);
            var ped = Bare.Ped();
            u.Equip(ped, WeaponId.Pistol);
            ped.MagazineAmmo = 1;
            u.StartReload(ped);
            u.Equip(ped, WeaponId.Smg);
            Assert.False(u.IsReloading);
            Assert.Equal(0f, u.ReloadRemaining);
        }

        private static int Attacks(Arena a, WeaponUser u, Ped shooter, float seconds, out int outcomeCooldown)
        {
            int n = (int)(seconds / Dt + 0.5f), fired = 0;
            outcomeCooldown = 0;
            ShotResult shot;
            MeleeResult melee;
            for (int i = 0; i < n; i++)
            {
                u.Step(shooter, Dt);
                var o = u.TryAttack(a.World, shooter, Weapons.MuzzleOf(shooter), Vec3.Forward, 0f, out shot, out melee);
                if (o == AttackOutcome.Attacked) fired++;
                else if (o == AttackOutcome.Cooldown) outcomeCooldown++;
            }
            return fired;
        }

        [Theory]
        [InlineData(WeaponId.Smg, 12)]
        [InlineData(WeaponId.Carbine, 8)]
        public void HeldAttackFiresAtTheWeaponsRatePerSecond(WeaponId w, int perSecond)
        {
            var a = new Arena();
            var shooter = a.Spawn(0f, 0f);
            var u = new WeaponUser(null);
            u.Equip(shooter, w);
            shooter.MagazineAmmo = 30;
            int cooldowns;
            int fired = Attacks(a, u, shooter, 2f, out cooldowns);
            Assert.InRange(fired, perSecond * 2 - 1, perSecond * 2 + 1);
            Assert.True(cooldowns > 0);
        }

        [Fact]
        public void PistolFiresSevenShotsInTwoSecondsAndFistsFourOrFiveSwings()
        {
            var a = new Arena();
            var shooter = a.Spawn(0f, 0f);
            var u = new WeaponUser(null);
            u.Equip(shooter, WeaponId.Pistol);
            shooter.MagazineAmmo = 12;
            int cd;
            Assert.InRange(Attacks(a, u, shooter, 2f, out cd), 6, 8);
            var fists = new WeaponUser(null);
            fists.Equip(shooter, WeaponId.Fists);
            Assert.InRange(Attacks(a, fists, shooter, 2f, out cd), 4, 5);
        }

        [Fact]
        public void EachShotTakesOneRoundFromMagazineAndProfileAndTheShotgunOneShellForEightPellets()
        {
            var a = new Arena();
            var profile = Owning(WeaponId.Shotgun, 16);
            var shooter = a.Spawn(0f, 0f);
            var u = new WeaponUser(profile);
            u.Equip(shooter, WeaponId.Shotgun);
            ShotResult shot;
            MeleeResult melee;
            var o = u.TryAttack(a.World, shooter, Weapons.MuzzleOf(shooter), Vec3.Forward, 0f, out shot, out melee);
            Assert.Equal(AttackOutcome.Attacked, o);
            Assert.Equal(8, shot.Rays);
            Assert.Equal(7, shooter.MagazineAmmo);
            Assert.Equal(15, profile.GetAmmo(WeaponId.Shotgun));
        }

        [Fact]
        public void EmptyingTheMagazineStartsAnAutomaticReloadAndAttackDuringItIsRefused()
        {
            var a = new Arena();
            var profile = Owning(WeaponId.Pistol, 24);
            var shooter = a.Spawn(0f, 0f);
            var u = new WeaponUser(profile);
            u.Equip(shooter, WeaponId.Pistol);
            shooter.MagazineAmmo = 1;
            ShotResult shot;
            MeleeResult melee;
            Assert.Equal(AttackOutcome.Attacked, u.TryAttack(a.World, shooter, Weapons.MuzzleOf(shooter), Vec3.Forward, 0f, out shot, out melee));
            Assert.True(u.IsReloading);
            Advance(u, shooter, 0.5f);
            Assert.Equal(AttackOutcome.Reloading, u.TryAttack(a.World, shooter, Weapons.MuzzleOf(shooter), Vec3.Forward, 0f, out shot, out melee));
            Advance(u, shooter, 1.0f);
            Assert.Equal(12, shooter.MagazineAmmo);
        }

        [Fact]
        public void EmptyMagazineWithNoReserveReportsEmptyAndFiresNothing()
        {
            var a = new Arena();
            var shooter = a.Spawn(0f, 0f);
            var u = new WeaponUser(Owning(WeaponId.Pistol, 0));
            u.Equip(shooter, WeaponId.Pistol);
            ShotResult shot;
            MeleeResult melee;
            long seq = a.World.Events.NextSeq;
            Assert.Equal(AttackOutcome.Empty, u.TryAttack(a.World, shooter, Weapons.MuzzleOf(shooter), Vec3.Forward, 0f, out shot, out melee));
            Assert.Equal(0, Arena.Count(a.EventsSince(seq), GameEventType.Gunshot));
        }
    }
}
