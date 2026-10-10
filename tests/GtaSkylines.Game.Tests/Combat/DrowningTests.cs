using GtaSkylines.Game.Combat;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Peds;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Session;
using GtaSkylines.Game.Tests.Fakes;
using GtaSkylines.Game.Tests.Peds;
using GtaSkylines.Game.Tests.PhysicsTests;
using GtaSkylines.Game.Tests.SessionTests;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.Combat
{
    /// <summary>F-FOOT-3: ten health per second while the motor is Swimming with no breath, armour ignored.</summary>
    public class DrowningTests
    {
        /// <summary>A session whose city is 3 m deep in water everywhere (the floor stays at y = 0).</summary>
        private static SessionTests.Sim DeepWater()
        {
            return new SessionTests.Sim(city: c => c.WaterLevel = 3f);
        }

        private static void RunUntilDrowning(SessionTests.Sim sim)
        {
            Assert.True(sim.Until(() => sim.Player.Motor.IsDrowning, 30.0, In.Idle), "never ran out of breath");
        }

        private static Ped DrowningPed(Arena a)
        {
            a.City.WaterLevel = 3f;
            Ped p = a.Spawn(0f, 0f);
            for (int i = 0; i < 1500 && !p.Motor.IsDrowning; i++) a.World.MovePed(p, Arena.Dt);
            Assert.True(p.Motor.IsDrowning, "setup: the ped never ran out of breath");
            return p;
        }

        [Fact]
        public void ConstantsAndKind()
        {
            Assert.Equal(10f, Damage.DrownDamagePerSecond);
            Assert.Equal("Drown", DamageKind.Drown.ToString());
            Assert.Equal("You are drowning", PlayerRules.DrowningHelp);
        }

        [Fact]
        public void DrownAppliesTenHealthPerSecond_IgnoresArmour_AndStampsTheDamage()
        {
            var a = new Arena();
            Ped p = DrowningPed(a);
            p.Health = 100f; p.Armour = 50f;
            DamageResult r = Damage.Drown(a.World, p, 0.5f);
            Assert.Equal(5f, r.HealthLost, 3);
            Assert.Equal(95f, p.Health, 3);
            Assert.Equal(50f, p.Armour, 3);
            Assert.Equal(a.World.Clock.GameSeconds, p.LastDamageTime, 3);
            Assert.Equal(0, p.LastAttackerTag);
        }

        [Fact]
        public void DrownIsANoOpWhenNotDrowningOrDtIsNotPositive()
        {
            var a = new Arena();
            Ped dry = a.Spawn(10f, 10f);
            float h = dry.Health;
            DamageResult r = Damage.Drown(a.World, dry, 1f);
            Assert.Equal(0f, r.HealthLost);
            Assert.Equal(h, dry.Health);
            Assert.Equal(-1.0, dry.LastDamageTime);
            var b = new Arena();
            Ped wet = DrowningPed(b);
            float wh = wet.Health;
            Damage.Drown(b.World, wet, 0f);
            Damage.Drown(b.World, wet, -1f);
            Assert.Equal(wh, wet.Health);
        }

        [Fact]
        public void APlayerWithoutBreathLosesAboutTenHealthPerSecondAndKeepsHisArmour()
        {
            SessionTests.Sim sim = DeepWater();
            RunUntilDrowning(sim);
            sim.Player.Health = 100f; sim.Player.Armour = 40f;
            sim.Run(3.0, In.Idle);
            Assert.True(sim.Player.Motor.IsDrowning);
            Assert.InRange(sim.Player.Health, 68.5f, 71.5f);
            Assert.Equal(40f, sim.Player.Armour, 2);
        }

        [Fact]
        public void NoDamageWhileSwimmingWithBreathLeft()
        {
            SessionTests.Sim sim = DeepWater();
            sim.Run(5.0, In.Idle);
            Assert.Equal(MotorState.Swimming, sim.Player.Motor.State);
            Assert.False(sim.Player.Motor.IsDrowning);
            Assert.Equal(100f, sim.Player.Health, 3);
        }

        [Fact]
        public void NoDamageOnLand()
        {
            var sim = new SessionTests.Sim();
            sim.Run(3.0, In.Idle);
            Assert.Equal(MotorState.Grounded, sim.Player.Motor.State);
            Assert.Equal(100f, sim.Player.Health, 3);
        }

        [Fact]
        public void DrowningToZeroIsWasted()
        {
            SessionTests.Sim sim = DeepWater();
            RunUntilDrowning(sim);
            sim.Player.Health = 5f;
            Assert.True(sim.Until(() => sim.PS.Life == LifeState.Wasted, 2.0, In.Idle), "never Wasted");
            Assert.True(sim.Player.IsDead);
            Assert.Equal(BigScreenKind.Wasted, sim.W.Messages.BigScreen);
        }

        [Fact]
        public void TheHelpTextShowsWhileDrowning()
        {
            SessionTests.Sim sim = DeepWater();
            Assert.NotEqual(PlayerRules.DrowningHelp, sim.S.Hud.HelpText);
            RunUntilDrowning(sim);
            sim.Run(0.2, In.Idle);
            Assert.Equal(PlayerRules.DrowningHelp, sim.S.Hud.HelpText);
        }

        [Fact]
        public void BreathRefillsAfterLeavingTheWater()
        {
            var city = new FakeCity { Ground = -5f, WaterLevel = 0f };
            var s = new PhysicsTests.Sim(T.BigFloor(300, -5f).ToWorld(), new Vec3(0, -1.3f, 0), city);
            s.Run(300, Vec3.Zero); // 5 s swimming
            Assert.Equal(MotorState.Swimming, s.Motor.State);
            float low = s.Motor.Breath;
            Assert.True(low < s.Motor.Settings.BreathSeconds - 1f, "breath " + low);
            city.WaterLevel = null;
            s.Run(120, Vec3.Zero); // 2 s out of the water
            Assert.NotEqual(MotorState.Swimming, s.Motor.State);
            Assert.True(s.Motor.Breath > low + 4f, "breath " + s.Motor.Breath + " from " + low);
        }

        [Fact]
        public void ANonPlayerPedWithoutBreathLosesHealthInThePedsSystemAndDiesAtZero()
        {
            var a = new Arena();
            Ped p = DrowningPed(a);
            p.Brain = new IdleBrain(a.World, BrainTests.Adult);
            p.Health = 100f; p.Armour = 0f;
            var sys = new PedsSystem();
            for (int i = 0; i < 60; i++) sys.Step(a.World, Arena.Dt);
            Assert.InRange(p.Health, 85f, 93f);
            p.Health = 3f;
            for (int i = 0; i < 60 && !p.IsDead; i++) sys.Step(a.World, Arena.Dt);
            Assert.True(p.IsDead);
        }
    }
}
