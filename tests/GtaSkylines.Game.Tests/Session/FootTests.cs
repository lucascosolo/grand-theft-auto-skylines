using System;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using Xunit;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Session;
using GtaSkylines.Game.Tests.PhysicsTests;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Tests.SessionTests
{
    public class FootMovementTests
    {
        private static float Speed(Sim sim) { return sim.Player.Motor.Velocity.Flat.Length; }

        [Fact]
        public void W_MovesForwardRelativeToTheViewYaw_AtJogSpeed()
        {
            var sim = new Sim(yaw: 90f);
            Vec3 start = sim.Player.Position;
            sim.Run(1.0, In.Forward);
            Vec3 d = sim.Player.Position - start;
            Sim.Near(PlayerRules.JogSpeed, d.X, 0.35f, "x travelled in 1 s facing +x");
            Sim.Near(0f, d.Z, 0.1f, "z drift");
            Sim.Near(PlayerRules.JogSpeed, Speed(sim), 0.15f, "steady speed");
        }

        [Fact]
        public void MoveIsCameraRelative_AfterTurningTheView()
        {
            var sim = new Sim();
            sim.Tick(In.Look(90f, 0f));
            Vec3 start = sim.Player.Position;
            sim.Run(1.0, In.Move(0f, 1f));
            Assert.True(sim.Player.Position.X - start.X > 3.0f, "forward after a 90 degree view turn goes +x");
            Sim.Near(0f, sim.Player.Position.Z - start.Z, 0.3f);
        }

        [Fact]
        public void Strafe_MovesToTheRightOfTheView()
        {
            var sim = new Sim();
            Vec3 start = sim.Player.Position;
            sim.Run(1.0, In.Move(1f, 0f));
            Assert.True(sim.Player.Position.X - start.X > 3.0f, "D moves toward +x when the view faces +z");
        }

        [Fact]
        public void DiagonalInput_IsClampedToUnitLength()
        {
            var sim = new Sim();
            sim.Run(1.0, In.Move(1f, 1f));
            Sim.Near(PlayerRules.JogSpeed, Speed(sim), 0.2f, "diagonal speed");
        }

        [Fact]
        public void NoInput_StandsStill_AndAnimIsIdle()
        {
            var sim = new Sim();
            Vec3 p = sim.Player.Position;
            sim.Run(1.0, In.Idle);
            Sim.Near(p, sim.Player.Position, 0.01f);
            Assert.Equal(PedAnim.Idle, sim.Player.Anim);
        }

        [Fact]
        public void Anim_RunWhileJogging_SprintWhileSprinting()
        {
            var sim = new Sim();
            sim.Run(0.6, In.Forward);
            Assert.Equal(PedAnim.Run, sim.Player.Anim);
            sim.Run(0.6, In.Move(0f, 1f, GameButton.Sprint));
            Assert.Equal(PedAnim.Sprint, sim.Player.Anim);
        }

        [Fact]
        public void Sneak_TogglesWalkSpeedAndTheFlag()
        {
            var sim = new Sim();
            sim.Tick(In.Press(GameButton.Sneak));
            Assert.True(sim.Player.Has(PedFlags.Sneaking));
            sim.Run(1.0, In.Forward);
            Sim.Near(PlayerRules.WalkSpeed, Speed(sim), 0.15f, "sneak speed");
            Assert.Equal(PedAnim.Walk, sim.Player.Anim);
            sim.Tick(In.Press(GameButton.Sneak));
            Assert.False(sim.Player.Has(PedFlags.Sneaking));
            sim.Run(1.0, In.Forward);
            Sim.Near(PlayerRules.JogSpeed, Speed(sim), 0.15f, "jog again");
        }

        [Fact]
        public void ThirdPerson_TurnsTowardTheMoveDirectionAtBoundedRate()
        {
            var sim = new Sim(settings: s => s.FootView = 2);
            sim.Run(0.2, In.Idle);
            Assert.True(Sim.AngleDiff(0f, sim.Player.YawDeg) < 1f);
            // Strafe right: the body turns toward +x (yaw 90) but not instantly (720 deg/s).
            sim.Run(0.05, In.Move(1f, 0f));
            float early = sim.Player.YawDeg;
            Assert.InRange(early, 5f, 80f);
            sim.Run(0.5, In.Move(1f, 0f));
            Sim.Near(90f, sim.Player.YawDeg, 3f, "yaw after settling");
        }

        [Fact]
        public void FirstPerson_BodyFacesTheViewYaw()
        {
            var sim = new Sim(settings: s => s.FootView = 0);
            sim.Run(0.2, In.Look(50f, 0f));
            sim.Run(0.5, In.Move(1f, 0f));
            Sim.Near(sim.PS.ViewYawDeg, sim.Player.YawDeg, 2f, "body yaw follows the view in first person");
        }

        [Fact]
        public void Aiming_FacesTheViewYawEvenInThirdPerson()
        {
            var sim = new Sim(settings: s => s.FootView = 2);
            sim.Run(0.2, In.Look(70f, 0f));
            sim.Run(0.6, In.Move(-1f, 0f, GameButton.Aim));
            Sim.Near(sim.PS.ViewYawDeg, sim.Player.YawDeg, 2f, "aiming locks the body to the view");
            Assert.True(sim.Player.Has(PedFlags.Aiming));
            sim.Run(0.2, In.Idle);
            Assert.False(sim.Player.Has(PedFlags.Aiming));
        }

        [Fact]
        public void HandsUp_SetsFlagAndEmitsOneEventWhileHeld()
        {
            var sim = new Sim();
            sim.Tick(In.Press(GameButton.HandsUp));
            sim.Run(1.0, In.Hold(GameButton.HandsUp));
            Assert.True(sim.Player.Has(PedFlags.HandsUp));
            Assert.Equal(1, sim.Count(GameEventType.HandsUp));
            Assert.Equal(sim.Player.Tag, sim.Last(GameEventType.HandsUp).ActorB);
            sim.Run(0.3, In.Idle);
            Assert.False(sim.Player.Has(PedFlags.HandsUp));
        }

        [Fact]
        public void WalkedDistance_FeedsTheStats()
        {
            var sim = new Sim();
            sim.Run(5.0, In.Forward);
            sim.S.End();
            Assert.InRange(sim.Profile.GetStat(PlayerRules.StatDistanceWalked), 15, 19);
        }
    }

    public class FootSprintTests
    {
        [Fact]
        public void Sprint_RunsAt6_8AndDrainsStaminaAt18PerSecond()
        {
            var sim = new Sim();
            sim.Run(0.5, In.Move(0f, 1f, GameButton.Sprint));
            Sim.Near(PlayerRules.SprintSpeed, sim.Player.Motor.Velocity.Flat.Length, 0.3f, "sprint speed");
            sim.Run(1.5, In.Move(0f, 1f, GameButton.Sprint));
            Sim.Near(100f - 18f * 2f, sim.PS.Stamina, 3f, "stamina after 2 s");
            Assert.True(sim.PS.Sprinting);
        }

        [Fact]
        public void Sprint_NeedsForwardInput()
        {
            var sim = new Sim();
            sim.Run(1.0, GtaSkylines.Game.Tests.SessionTests.In.Move(1f, 0f, GameButton.Sprint));
            Sim.Near(PlayerRules.JogSpeed, sim.Player.Motor.Velocity.Flat.Length, 0.25f, "strafe sprint is only a jog");
            Sim.Near(100f, sim.PS.Stamina, 0.5f, "no drain");
        }

        [Fact]
        public void Stamina_RefillsAt25PerSecondAfterAOneSecondDelay()
        {
            var sim = new Sim();
            sim.Run(3.0, In.Move(0f, 1f, GameButton.Sprint));
            float low = sim.PS.Stamina;
            Assert.InRange(low, 40f, 50f);
            sim.Run(0.8, In.Idle);
            Sim.Near(low, sim.PS.Stamina, 1f, "no refill inside the 1 s delay");
            sim.Run(1.2, In.Idle);
            Sim.Near(low + 25f * 1.0f, sim.PS.Stamina, 5f, "refilled for roughly 1 s");
            sim.Run(5.0, In.Idle);
            Sim.Near(100f, sim.PS.Stamina, 0.01f, "full again");
        }

        [Fact]
        public void EmptyStamina_StopsTheSprintUntilItIsPressedAgain()
        {
            var sim = new Sim();
            sim.Run(7.0, In.Move(0f, 1f, GameButton.Sprint));
            Assert.InRange(sim.PS.Stamina, 0f, 3f);
            Sim.Near(PlayerRules.JogSpeed, sim.Player.Motor.Velocity.Flat.Length, 0.3f, "back to a jog while Sprint is still held");
            Assert.False(sim.PS.Sprinting);
        }
    }

    public class FootAirTests
    {
        private static Sim OnABox(float height)
        {
            var sim = new Sim(begin: false);
            new MeshBuilder().Box(new Vec3(40f, 0f, -5f), new Vec3(50f, height, 5f)).Apply(sim.W.Collision, 5);
            sim.S.Begin(new Vec3(45f, height, 0f), 0f);
            sim.Run(0.3, In.Idle);
            return sim;
        }

        [Fact]
        public void FallFromSixMetres_CostsAbout16Health_AndEmitsFell()
        {
            Sim sim = OnABox(6f);
            Sim.Near(6f, sim.Player.Position.Y, 0.1f, "starts on top");
            sim.Run(0.3, In.Move(0f, 1f));
            sim.Run(2.0, In.Move(0f, 1f));
            Assert.True(sim.Player.Position.Y < 0.2f, "ended on the ground");
            // v = sqrt(2 * 9.81 * 6) = 10.85 m/s, damage (v - 9) * 9 = 16.6
            Sim.Near(100f - 16.6f, sim.Player.Health, 6f, "health after the fall");
            Assert.Equal(1, sim.Count(GameEventType.Fell));
            Sim.Near(100f - sim.Player.Health, sim.Last(GameEventType.Fell).Value, 0.5f, "event value equals health lost");
            Assert.Equal(sim.Player.Tag, sim.Last(GameEventType.Fell).ActorA);
        }

        [Fact]
        public void FallFromThreeMetres_IsHarmless()
        {
            Sim sim = OnABox(3f);
            sim.Run(2.5, In.Move(0f, 1f));
            Assert.True(sim.Player.Position.Y < 0.2f);
            Sim.Near(100f, sim.Player.Health, 0.01f);
            Assert.Equal(0, sim.Count(GameEventType.Fell));
        }

        [Fact]
        public void FallDamage_IgnoresArmour()
        {
            var sim = new Sim(begin: false);
            new MeshBuilder().Box(new Vec3(40f, 0f, -5f), new Vec3(50f, 8f, 5f)).Apply(sim.W.Collision, 5);
            sim.S.Begin(new Vec3(45f, 8f, 0f), 0f);
            sim.Player.Armour = 100f;
            sim.Run(0.3, In.Move(0f, 1f));
            sim.Run(2.5, In.Move(0f, 1f));
            Sim.Near(100f, sim.Player.Armour, 0.01f, "armour untouched");
            Assert.True(sim.Player.Health < 85f);
        }

        [Fact]
        public void Jump_RisesAboutOneMetre_AndLands()
        {
            var sim = new Sim();
            sim.Tick(In.Press(GameButton.Jump));
            float apex = 0f;
            for (int i = 0; i < 90; i++) { sim.Tick(In.Idle); apex = Math.Max(apex, sim.Player.Position.Y); }
            Sim.Near(1.1f, apex, 0.3f, "jump apex");
            Sim.Near(0f, sim.Player.Position.Y, 0.06f, "back on the floor");
            Sim.Near(100f, sim.Player.Health, 0.01f);
        }

        [Fact]
        public void Jump_IntoALowWall_ClimbsTheLedge()
        {
            var sim = new Sim(begin: false);
            new MeshBuilder().Box(new Vec3(3f, 0f, -5f), new Vec3(9f, 1.5f, 5f)).Apply(sim.W.Collision, 5);
            sim.S.Begin(new Vec3(2.2f, 0f, 0f), 90f);
            sim.Run(0.2, In.Move(0f, 1f));
            sim.Tick(In.Move(0f, 1f).WithPress(GameButton.Jump));
            sim.Run(2.5, In.Move(0f, 1f));
            Sim.Near(1.5f, sim.Player.Position.Y, 0.15f, "on top of the wall");
            Assert.True(sim.Player.Position.X > 3f);
        }
    }
}
