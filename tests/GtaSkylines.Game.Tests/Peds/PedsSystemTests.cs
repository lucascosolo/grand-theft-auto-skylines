using System;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Combat;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Peds;
using GtaSkylines.Game.Tests.Combat;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.Peds
{
    /// <summary>PedsSystem: events become stimuli, ambient panic effects, determinism, allocation.</summary>
    public class PedsSystemTests
    {
        private static Ped Calm(Arena a, float x, float z, float yaw = 0f)
        {
            return BrainTests.Idle(a, x, z, BrainTests.Adult, yaw);
        }

        private static void Emit(Arena a, GameEventType t, Vec3 at, int actorA, int actorB, float value)
        {
            a.World.Emit(t, at, actorA, actorB, value, null);
        }

        [Fact]
        public void RunsAtOrderThirty()
        {
            Assert.Equal(30, new PedsSystem().Order);
        }

        [Fact]
        public void BumpAtOrAboveClosingSpeedOnePointFiveMakesTheBumpedPedStumble()
        {
            var a = new Arena();
            var slow = Calm(a, 0f, 0f);
            var fast = Calm(a, 10f, 0f);
            var sys = new PedsSystem();
            sys.Step(a.World, Arena.Dt);
            Emit(a, GameEventType.Bumped, slow.Position, a.Player.Tag, slow.Tag, 1.0f);
            Emit(a, GameEventType.Bumped, fast.Position, a.Player.Tag, fast.Tag, 2.0f);
            sys.Step(a.World, Arena.Dt);
            Assert.IsType<IdleBrain>(slow.Brain);
            Assert.IsType<StumbleBrain>(fast.Brain);
        }

        [Fact]
        public void EventsBeforeTheFirstStepAreIgnored()
        {
            var a = new Arena();
            var p = Calm(a, 0f, 0f);
            Emit(a, GameEventType.Bumped, p.Position, a.Player.Tag, p.Tag, 3f);
            var sys = new PedsSystem();
            sys.Step(a.World, Arena.Dt);
            sys.Step(a.World, Arena.Dt);
            Assert.IsType<IdleBrain>(p.Brain);
        }

        [Fact]
        public void PunchSendsViolenceToSeeingWitnessesWithinFifteenMetresOnly()
        {
            var a = new Arena();
            var victim = Calm(a, 0f, 0f);
            var sees = Calm(a, 0f, 10f, 180f);
            var turnedAway = Calm(a, 5f, 8f, 0f);
            var tooFar = Calm(a, 0f, 25f, 180f);
            var sys = new PedsSystem();
            sys.Step(a.World, Arena.Dt);
            Emit(a, GameEventType.Punched, victim.Position, a.Player.Tag, victim.Tag, 8f);
            sys.Step(a.World, Arena.Dt);
            Assert.IsType<FleeBrain>(sees.Brain);
            Assert.IsType<IdleBrain>(turnedAway.Brain);
            Assert.IsType<IdleBrain>(tooFar.Brain);
        }

        [Fact]
        public void ShotHurtsASurvivingVictimAndShotsAtBystandersNearTheLine()
        {
            var a = new Arena();
            var victim = Calm(a, 0f, 20f, 180f);
            var near = Calm(a, 1.5f, 10f, 180f);
            var far = Calm(a, 30f, 10f, 180f);
            var sys = new PedsSystem();
            sys.Step(a.World, Arena.Dt);
            a.World.Emit(GameEventType.Shot, new Vec3(0f, 1.45f, 0f), new Vec3(0f, 1.2f, 20f), a.Player.Tag, victim.Tag, 26f);
            sys.Step(a.World, Arena.Dt);
            Assert.NotNull(victim.Brain as FleeBrain);
            Assert.NotNull(near.Brain as FleeBrain);
            Assert.IsType<IdleBrain>(far.Brain);
        }

        [Fact]
        public void GunshotIsHeardWithinSixtyMetresButNotByCopsOrThePlayer()
        {
            var a = new Arena();
            var hears = Calm(a, 50f, 0f);
            var deaf = Calm(a, 70f, 0f);
            var cop = a.Spawn(10f, 0f, 0f, PedKind.Cop);
            cop.Brain = new IdleBrain(a.World, BrainTests.Adult);
            var sys = new PedsSystem();
            sys.Step(a.World, Arena.Dt);
            Emit(a, GameEventType.Gunshot, Vec3.Zero, 0, 0, 60f);
            sys.Step(a.World, Arena.Dt);
            Assert.IsType<FleeBrain>(hears.Brain);
            Assert.IsType<IdleBrain>(deaf.Brain);
            Assert.IsType<IdleBrain>(cop.Brain);
        }

        [Fact]
        public void AimedAtEventCarriesTheDistanceAndTriggersHandsUpWithinFifteenMetres()
        {
            var a = new Arena();
            a.Player.Motor.Teleport(new Vec3(0f, 0f, -10f));
            var p = Calm(a, 0f, 0f);
            var sys = new PedsSystem();
            sys.Step(a.World, Arena.Dt);
            Emit(a, GameEventType.AimedAt, a.Player.Position, a.Player.Tag, p.Tag, 0f);
            sys.Step(a.World, Arena.Dt);
            Assert.IsType<HandsUpBrain>(p.Brain);
        }

        [Fact]
        public void GunshotPanicsAmbientCitizensWithin30mOnceEveryTenSeconds()
        {
            var a = new Arena();
            a.AddAmbient(1, 101u, 20f, 0f);
            a.AddAmbient(2, 102u, 35f, 0f);
            var sys = new PedsSystem();
            sys.Step(a.World, Arena.Dt);
            a.World.Effects.Items.Clear();
            Emit(a, GameEventType.Gunshot, Vec3.Zero, 0, 0, 60f);
            sys.Step(a.World, Arena.Dt);
            Assert.Equal(1, a.CountEffects(CityEffectKind.PanicCitizen));
            Assert.Equal((ushort)1, a.World.Effects.Items[0].Instance);

            Emit(a, GameEventType.Gunshot, Vec3.Zero, 0, 0, 60f);
            sys.Step(a.World, Arena.Dt);
            Assert.Equal(1, a.CountEffects(CityEffectKind.PanicCitizen));

            a.Run(sys, 10.5f);
            Emit(a, GameEventType.Gunshot, Vec3.Zero, 0, 0, 60f);
            sys.Step(a.World, Arena.Dt);
            Assert.Equal(2, a.CountEffects(CityEffectKind.PanicCitizen));
        }

        [Fact]
        public void DeadPedsAreNotSteppedAndHaveNoMovement()
        {
            var a = new Arena();
            var p = a.Spawn(0f, 0f);
            p.Flags |= PedFlags.Dead;
            p.Move.DesiredVelocity = new Vec3(3f, 0f, 0f);
            a.Run(new PedsSystem(), 0.5f);
            Assert.Equal(0f, p.Move.DesiredVelocity.LengthXZ);
        }

        private static float Scenario(uint seed)
        {
            var a = new Arena(seed);
            var sys = new PedsSystem();
            Ped[] ps = new Ped[10];
            for (int i = 0; i < ps.Length; i++) ps[i] = Calm(a, i * 4f, i * 2f);
            sys.Step(a.World, Arena.Dt);
            for (int step = 0; step < 600; step++)
            {
                if (step == 30) Emit(a, GameEventType.Gunshot, new Vec3(10f, 0f, 0f), 0, 0, 60f);
                if (step == 200) Emit(a, GameEventType.Bumped, ps[3].Position, a.Player.Tag, ps[3].Tag, 3f);
                sys.Step(a.World, Arena.Dt);
            }
            float h = 0f;
            for (int i = 0; i < ps.Length; i++) h += ps[i].Position.X * (i + 1) + ps[i].Position.Z * (i + 3);
            return h;
        }

        [Fact]
        public void SameSeedSameOutcomeDifferentSeedDiffers()
        {
            Assert.Equal(Scenario(7u), Scenario(7u));
            Assert.NotEqual(Scenario(7u), Scenario(8u));
        }

        [Fact]
        public void ThirtyIdlePedsAndAnIdlePlayerAllocateNothingPerStep()
        {
            var a = new Arena();
            var peds = new PedsSystem();
            var combat = new CombatSystem();
            for (int i = 0; i < 30; i++) Calm(a, (i % 6) * 5f, (i / 6) * 5f);
            for (int i = 0; i < 120; i++) { peds.Step(a.World, Arena.Dt); combat.Step(a.World, Arena.Dt); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 300; i++) { peds.Step(a.World, Arena.Dt); combat.Step(a.World, Arena.Dt); }
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.Equal(0L, after - before);
        }
    }
}
