using GtaSkylines.Game.Combat;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.Combat
{
    /// <summary>The player's weapons and the event-driven damage, through CombatSystem.Step.</summary>
    public class CombatSystemTests
    {
        private static CombatSystem Start(Arena a)
        {
            var sys = new CombatSystem();
            sys.Step(a.World, Arena.Dt);
            return sys;
        }

        private static void GiveAndEquip(Arena a, WeaponId w, int ammo)
        {
            a.Profile.GiveWeapon(w);
            a.Profile.SetAmmo(w, ammo);
            a.Player.CurrentWeapon = (int)w;
        }

        [Fact]
        public void RunsAtOrderFifty()
        {
            Assert.Equal(50, new CombatSystem().Order);
        }

        [Fact]
        public void HeldAttackFiresThePistolAndSpendsAmmo()
        {
            var a = new Arena();
            GiveAndEquip(a, WeaponId.Pistol, 36);
            var sys = Start(a);
            a.World.Input = Arena.HeldAndPressed(GameButton.Attack);
            long seq = a.World.Events.NextSeq;
            sys.Step(a.World, Arena.Dt);
            Assert.Equal(1, Arena.Count(a.EventsSince(seq), GameEventType.Gunshot));
            Assert.Equal(35, a.Profile.GetAmmo(WeaponId.Pistol));
            Assert.Equal(PedAnim.Shoot, a.Player.Anim);
        }

        [Fact]
        public void HoldingAttackWithTheSmgKeepsFiringAtItsRate()
        {
            var a = new Arena();
            GiveAndEquip(a, WeaponId.Smg, 90);
            var sys = Start(a);
            a.World.Input = Arena.Held(GameButton.Attack);
            for (int i = 0; i < 60; i++) sys.Step(a.World, Arena.Dt);
            Assert.InRange(90 - a.Profile.GetAmmo(WeaponId.Smg), 11, 13);
        }

        [Fact]
        public void FistsSwingAndPunchAnimShows()
        {
            var a = new Arena();
            a.Player.Motor.Teleport(new Vec3(0f, 0f, 0f));
            var victim = a.Spawn(0f, 1f, 180f);
            var sys = Start(a);
            a.World.Input = Arena.HeldAndPressed(GameButton.Attack);
            sys.Step(a.World, Arena.Dt);
            Assert.Equal(PedAnim.Punch, a.Player.Anim);
            Assert.True(victim.Health < 100f);
        }

        [Fact]
        public void UnownedWeaponInHandFallsBackToFists()
        {
            var a = new Arena();
            a.Player.CurrentWeapon = (int)WeaponId.Carbine;
            Start(a);
            Assert.Equal((int)WeaponId.Fists, a.Player.CurrentWeapon);
        }

        [Fact]
        public void NextAndPrevWeaponCycleThroughSelectableWeapons()
        {
            var a = new Arena();
            GiveAndEquip(a, WeaponId.Pistol, 36);
            a.Player.CurrentWeapon = (int)WeaponId.Fists;
            var sys = Start(a);
            a.World.Input = Arena.HeldAndPressed(GameButton.NextWeapon);
            sys.Step(a.World, Arena.Dt);
            Assert.Equal((int)WeaponId.Pistol, a.Player.CurrentWeapon);
            a.World.Input = Arena.HeldAndPressed(GameButton.PrevWeapon);
            sys.Step(a.World, Arena.Dt);
            Assert.Equal((int)WeaponId.Fists, a.Player.CurrentWeapon);
        }

        [Fact]
        public void ReloadKeyStartsAReloadThatFillsTheMagazine()
        {
            var a = new Arena();
            GiveAndEquip(a, WeaponId.Pistol, 36);
            var sys = Start(a);
            a.Player.MagazineAmmo = 3;
            a.World.Input = Arena.HeldAndPressed(GameButton.Reload);
            sys.Step(a.World, Arena.Dt);
            Assert.True(sys.PlayerWeapon.IsReloading);
            a.World.Input = new InputFrame();
            for (int i = 0; i < 90; i++) sys.Step(a.World, Arena.Dt);
            Assert.Equal(12, a.Player.MagazineAmmo);
        }

        [Fact]
        public void WeaponWheelOpensWhileHeldAndEquipsTheHighlightOnRelease()
        {
            var a = new Arena();
            GiveAndEquip(a, WeaponId.Pistol, 36);
            a.Player.CurrentWeapon = (int)WeaponId.Fists;
            var sys = Start(a);
            a.World.Input = Arena.HeldAndPressed(GameButton.WeaponWheel);
            sys.Step(a.World, Arena.Dt);
            Assert.True(sys.Wheel.IsOpen);
            var look = Arena.Held(GameButton.WeaponWheel);
            look.LookYawDeg = 30f;
            look.LookPitchDeg = -30f;
            a.World.Input = look;
            sys.Step(a.World, Arena.Dt);
            var rel = new InputFrame();
            rel.Released = InputFrame.Bit(GameButton.WeaponWheel);
            a.World.Input = rel;
            sys.Step(a.World, Arena.Dt);
            Assert.False(sys.Wheel.IsOpen);
            Assert.Equal((int)WeaponId.Pistol, a.Player.CurrentWeapon);
        }

        [Fact]
        public void AttackIsIgnoredWhileTheWheelIsOpen()
        {
            var a = new Arena();
            GiveAndEquip(a, WeaponId.Pistol, 36);
            var sys = Start(a);
            a.World.Input = Arena.HeldAndPressed(GameButton.WeaponWheel, GameButton.Attack);
            sys.Step(a.World, Arena.Dt);
            Assert.Equal(36, a.Profile.GetAmmo(WeaponId.Pistol));
        }

        [Fact]
        public void AimingAtAHostilePedWithinSixDegreesLocksOnAndEmitsAimedAtOnce()
        {
            var a = new Arena();
            a.Player.Motor.Teleport(Vec3.Zero);
            GiveAndEquip(a, WeaponId.Pistol, 36);
            var target = a.Spawn(1.5f, 20f, 180f);
            target.Flags |= PedFlags.Hostile;
            a.Rebuild();
            a.Player.Flags |= PedFlags.Aiming;
            a.World.ViewPosition = new Vec3(0f, 1.6f, 0f);
            a.World.ViewRotation = Quat.Yaw(0f);
            var sys = Start(a);
            a.World.Input = Arena.Held(GameButton.Aim);
            long seq = a.World.Events.NextSeq;
            sys.Step(a.World, Arena.Dt);
            sys.Step(a.World, Arena.Dt);
            Assert.Equal(target.Tag, a.World.PlayerState.AimTargetTag);
            var aimed = a.EventsSince(seq).FindAll(e => e.Type == GameEventType.AimedAt);
            Assert.Single(aimed);
            Assert.Equal(a.Player.Tag, aimed[0].ActorA);
            Assert.Equal(target.Tag, aimed[0].ActorB);
        }

        [Fact]
        public void NotAimingClearsTheAimTarget()
        {
            var a = new Arena();
            var sys = Start(a);
            a.World.PlayerState.AimTargetTag = 12345;
            a.World.Input = new InputFrame();
            sys.Step(a.World, Arena.Dt);
            Assert.Equal(0, a.World.PlayerState.AimTargetTag);
        }

        [Fact]
        public void PedHitByVehicleEventDamagesTheVictimOnTheNextStep()
        {
            var a = new Arena();
            var victim = a.Spawn(0f, 0f);
            var sys = Start(a);
            a.World.Emit(GameEventType.PedHitByVehicle, victim.Position, WorldTag.ForCar(9), victim.Tag, 9f, null);
            sys.Step(a.World, Arena.Dt);
            Assert.Equal(65f, victim.Health, 2);
            Assert.True(victim.Has(PedFlags.KnockedDown));
        }

        [Fact]
        public void EventsBeforeTheFirstStepAreNotReplayed()
        {
            var a = new Arena();
            var victim = a.Spawn(0f, 0f);
            a.World.Emit(GameEventType.PedHitByVehicle, victim.Position, WorldTag.ForCar(9), victim.Tag, 9f, null);
            Start(a);
            Assert.Equal(100f, victim.Health);
        }

        [Fact]
        public void ExplosionEventDamagesNearbyPeds()
        {
            var a = new Arena();
            var victim = a.Spawn(4f, 0f);
            var sys = Start(a);
            a.World.Emit(GameEventType.Explosion, Vec3.Zero, 0, 0, 0f, null);
            sys.Step(a.World, Arena.Dt);
            Assert.Equal(50f, victim.Health, 1);
        }

        [Fact]
        public void PlayerRegeneratesTwoPerSecondFiveSecondsAfterTheLastHit()
        {
            var a = new Arena();
            var sys = Start(a);
            a.Player.Health = 20f;
            Damage.ApplyToPed(a.World, a.Player, 0.001f, 0, DamageKind.Other, false, 0f);
            a.Player.LastDamageTime = a.World.Clock.GameSeconds;
            a.World.Input = new InputFrame();
            for (int i = 0; i < 240; i++) { a.World.Clock.GameSeconds += Arena.Dt; sys.Step(a.World, Arena.Dt); }
            Assert.Equal(20f, a.Player.Health, 1);
            for (int i = 0; i < 360; i++) { a.World.Clock.GameSeconds += Arena.Dt; sys.Step(a.World, Arena.Dt); }
            Assert.InRange(a.Player.Health, 29f, 31f);
        }

        [Fact]
        public void DeadPlayerCannotFire()
        {
            var a = new Arena();
            GiveAndEquip(a, WeaponId.Pistol, 36);
            var sys = Start(a);
            a.Player.Health = 0f;
            a.World.Input = Arena.HeldAndPressed(GameButton.Attack);
            sys.Step(a.World, Arena.Dt);
            Assert.Equal(36, a.Profile.GetAmmo(WeaponId.Pistol));
        }
    }
}
