using GtaSkylines.Game.Combat;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Peds;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Tests.Combat;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.Peds
{
    /// <summary>Each brain's transitions, driven by stimuli and time through PedsSystem.</summary>
    public class BrainTests
    {
        internal static PedTraits Adult { get { return new PedTraits { FleeSpeed = 5.5f }; } }

        internal static Ped Idle(Arena a, float x, float z, PedTraits traits, float yaw = 0f)
        {
            var p = a.Spawn(x, z, yaw);
            p.Brain = new IdleBrain(a.World, traits);
            return p;
        }

        internal static Stimulus Stim(StimulusKind k, int source = 0, Vec3 at = default(Vec3), float value = 0f)
        {
            return new Stimulus { Kind = k, SourceTag = source, SourcePosition = at, Value = value };
        }

        [Fact]
        public void IdleStandsStillAndIsNotActive()
        {
            var a = new Arena();
            var p = Idle(a, 0f, 0f, Adult);
            var start = p.Position;
            a.Run(new PedsSystem(), 5f);
            Assert.Equal(PedAnim.Idle, p.Anim);
            Assert.True(Vec3.Distance(start, p.Position) < 0.05f);
            Assert.False(p.Brain.IsActive);
        }

        [Fact]
        public void SeatedOccupantSitsFacingItsHomeYaw()
        {
            var a = new Arena();
            var t = Adult;
            t.HasHome = true;
            t.Seated = true;
            t.Home = Vec3.Zero;
            t.HomeYawDeg = 135f;
            var p = Idle(a, 0f, 0f, t);
            a.Run(new PedsSystem(), 12f);
            Assert.Equal(PedAnim.Sit, p.Anim);
            Assert.Equal(135f, p.YawDeg, 1);
        }

        [Fact]
        public void IdleTurnsStayWithinThirtyDegreesOfTheStart()
        {
            var a = new Arena();
            var p = Idle(a, 0f, 0f, Adult, 90f);
            var sys = new PedsSystem();
            for (int i = 0; i < 60 * 40; i++)
            {
                sys.Step(a.World, Arena.Dt);
                Assert.InRange(p.YawDeg, 90f - 31f, 90f + 31f);
            }
        }

        [Fact]
        public void BumpedWalkerStumblesThenWalksAway()
        {
            var a = new Arena();
            var p = Idle(a, 0f, 0f, Adult);
            Assert.True(PedsSystem.Deliver(a.World, p, Stim(StimulusKind.Bumped, a.Player.Tag, new Vec3(0f, 0f, -1f), 2f)));
            Assert.IsType<StumbleBrain>(p.Brain);
            var sys = new PedsSystem();
            a.Run(sys, 0.3f);
            Assert.IsType<StumbleBrain>(p.Brain);
            Assert.True(p.Brain.IsActive);
            Assert.True(p.Position.Z > 0.05f, "pushed away from the bumper");
            a.Run(sys, 0.5f);
            Assert.IsType<ReturnBrain>(p.Brain);
            Assert.False(p.Brain.IsActive);
        }

        private sealed class Complaint : IDialogueHook
        {
            public string Line(GameWorld world, DialogueContext context, uint citizen, Ped ped)
            {
                return context == DialogueContext.Bumped ? "Watch it!" : null;
            }
        }

        [Fact]
        public void StumbleQueuesTheComplaintOnlyWhenThePlayerIsWithinFifteenMetres()
        {
            var a = new Arena();
            a.World.Hooks.Dialogue = new Complaint();
            a.Player.Motor.Teleport(new Vec3(0f, 0f, -5f));
            var p = Idle(a, 0f, 0f, Adult);
            PedsSystem.Deliver(a.World, p, Stim(StimulusKind.Bumped, a.Player.Tag, a.Player.Position, 2f));
            a.Run(new PedsSystem(), 0.1f);
            Subtitle s;
            Assert.True(a.World.Messages.TryGetSubtitle(out s));
            Assert.Equal("Watch it!", s.Text);

            var far = new Arena();
            far.World.Hooks.Dialogue = new Complaint();
            var q = Idle(far, 0f, 0f, Adult);
            PedsSystem.Deliver(far.World, q, Stim(StimulusKind.Bumped, far.Player.Tag, far.Player.Position, 2f));
            far.Run(new PedsSystem(), 0.1f);
            Assert.False(far.World.Messages.TryGetSubtitle(out s));
        }

        [Fact]
        public void GunshotMakesAnAdultInTheOpenFleeAtFiveToSixMetresPerSecond()
        {
            var a = new Arena();
            var p = Idle(a, 0f, 0f, Adult);
            PedsSystem.Deliver(a.World, p, Stim(StimulusKind.GunshotHeard, 0, new Vec3(0f, 0f, -10f)));
            var flee = Assert.IsType<FleeBrain>(p.Brain);
            Assert.InRange(flee.Duration, 6f, 15f);
            a.Run(new PedsSystem(), 1.0f);
            Assert.True(p.Position.Z > 3f, "ran away from the threat");
            Assert.True(p.Has(PedFlags.Panicking));
            Assert.Equal(PedAnim.Panic, p.Anim);
            Assert.InRange(p.Move.DesiredVelocity.LengthXZ, 5f, 6.01f);
        }

        [Fact]
        public void FleeEndsInReturnAndClearsPanic()
        {
            var a = new Arena();
            var p = Idle(a, 0f, 0f, Adult);
            PedsSystem.Deliver(a.World, p, Stim(StimulusKind.GunshotHeard, 0, new Vec3(0f, 0f, -10f)));
            a.Run(new PedsSystem(), 15.2f);
            Assert.IsType<ReturnBrain>(p.Brain);
            Assert.False(p.Has(PedFlags.Panicking));
        }

        [Fact]
        public void FleeingPedIsStoppedByAWall()
        {
            var a = new Arena();
            var p = Idle(a, 0f, 0f, Adult);
            a.Wall(-30f, 6f, 30f, 6f);
            a.Rebuild();
            PedsSystem.Deliver(a.World, p, Stim(StimulusKind.GunshotHeard, 0, new Vec3(0f, 0f, -10f)));
            a.Run(new PedsSystem(), 5f);
            Assert.True(p.Position.Z < 6f, "blocked by the wall, not through it");
        }

        [Fact]
        public void SeniorsAndAdultsAgainstAWallCowerInsteadOfFleeing()
        {
            var a = new Arena();
            var senior = Adult;
            senior.Senior = true;
            var s = Idle(a, 0f, 0f, senior);
            PedsSystem.Deliver(a.World, s, Stim(StimulusKind.GunshotHeard, 0, new Vec3(0f, 0f, -10f)));
            Assert.IsType<CowerBrain>(s.Brain);

            a.Wall(20f, -5f, 20f, 5f);
            var nearWall = Idle(a, 19.2f, 0f, Adult);
            a.Rebuild();
            PedsSystem.Deliver(a.World, nearWall, Stim(StimulusKind.ShotAt, 0, new Vec3(0f, 0f, -10f)));
            Assert.IsType<CowerBrain>(nearWall.Brain);

            var open = Idle(a, -20f, 0f, Adult);
            PedsSystem.Deliver(a.World, open, Stim(StimulusKind.ExplosionHeard, 0, new Vec3(0f, 0f, -10f)));
            Assert.IsType<FleeBrain>(open.Brain);
        }

        [Fact]
        public void CowerCrouchesInPlaceThenFlees()
        {
            var a = new Arena();
            var t = Adult;
            t.Senior = true;
            var p = Idle(a, 0f, 0f, t);
            PedsSystem.Deliver(a.World, p, Stim(StimulusKind.GunshotHeard, 0, new Vec3(0f, 0f, -10f)));
            var cower = Assert.IsType<CowerBrain>(p.Brain);
            Assert.InRange(cower.Duration, 6f, 12f);
            var sys = new PedsSystem();
            a.Run(sys, 2f);
            Assert.Equal(PedAnim.Sit, p.Anim);
            Assert.True(p.Has(PedFlags.Panicking));
            Assert.True(Vec3.Distance(p.Position, Vec3.Zero) < 0.1f);
            a.Run(sys, 10.2f);
            Assert.IsType<FleeBrain>(p.Brain);
        }

        [Theory]
        [InlineData(10f, "HandsUp")]
        [InlineData(20f, "Flee")]
        public void AimedAtReactsByDistance(float distance, string brain)
        {
            var a = new Arena();
            var p = Idle(a, 0f, 0f, Adult);
            PedsSystem.Deliver(a.World, p, Stim(StimulusKind.AimedAt, a.Player.Tag, new Vec3(0f, 0f, -distance), distance));
            Assert.Equal(brain, ((PedBrain)p.Brain).Name);
        }

        [Fact]
        public void AimedAtBeyondTwentyFiveMetresIsIgnored()
        {
            var a = new Arena();
            var p = Idle(a, 0f, 0f, Adult);
            PedsSystem.Deliver(a.World, p, Stim(StimulusKind.AimedAt, a.Player.Tag, new Vec3(0f, 0f, -30f), 30f));
            Assert.IsType<IdleBrain>(p.Brain);
        }

        [Fact]
        public void HandsUpWhileAimedAtThenFleesHalfASecondAfterTheAimLeaves()
        {
            var a = new Arena();
            a.Player.Motor.Teleport(new Vec3(0f, 0f, -8f));
            a.Player.Flags |= PedFlags.Aiming;
            var p = Idle(a, 0f, 0f, Adult);
            PedsSystem.Deliver(a.World, p, Stim(StimulusKind.AimedAt, a.Player.Tag, a.Player.Position, 8f));
            a.World.PlayerState.AimTargetTag = p.Tag;
            var sys = new PedsSystem();
            long seq = a.World.Events.NextSeq;
            a.Run(sys, 2f);
            Assert.True(p.Has(PedFlags.HandsUp));
            Assert.Equal(PedAnim.HandsUp, p.Anim);
            Assert.Equal(1, Arena.Count(a.EventsSince(seq), GameEventType.HandsUp));
            a.World.PlayerState.AimTargetTag = 0;
            a.Run(sys, 0.4f);
            Assert.IsType<HandsUpBrain>(p.Brain);
            a.Run(sys, 0.3f);
            Assert.IsType<FleeBrain>(p.Brain);
            Assert.False(p.Has(PedFlags.HandsUp));
        }

        [Fact]
        public void ClerkHeldUpRaisesRobberyOnce()
        {
            var a = new Arena();
            var t = Adult;
            t.Clerk = true;
            a.Player.Motor.Teleport(new Vec3(0f, 0f, -3f));
            a.Player.Flags |= PedFlags.Aiming;
            var clerk = Idle(a, 0f, 0f, t);
            PedsSystem.Deliver(a.World, clerk, Stim(StimulusKind.AimedAt, a.Player.Tag, a.Player.Position, 3f));
            a.World.PlayerState.AimTargetTag = clerk.Tag;
            long seq = a.World.Events.NextSeq;
            a.Run(new PedsSystem(), 2f);
            Assert.Equal(1, Arena.Count(a.EventsSince(seq), GameEventType.Robbery));
            Assert.True(((HandsUpBrain)clerk.Brain).Robbed);
        }

        [Fact]
        public void PunchedFighterWithoutFirearmFightsBackOthersFlee()
        {
            var a = new Arena();
            var fighter = Adult;
            fighter.Fighter = true;
            var f = Idle(a, 0f, 0f, fighter);
            var m = Idle(a, 10f, 0f, Adult);
            var low = Idle(a, 20f, 0f, fighter);
            low.Health = 30f;
            var gun = Idle(a, 30f, 0f, fighter);
            var puncher = a.Spawn(0f, -1f);
            var armed = a.Spawn(30f, -1f);
            armed.CurrentWeapon = (int)WeaponId.Pistol;
            PedsSystem.Deliver(a.World, f, Stim(StimulusKind.Punched, puncher.Tag, puncher.Position));
            PedsSystem.Deliver(a.World, m, Stim(StimulusKind.Punched, puncher.Tag, puncher.Position));
            PedsSystem.Deliver(a.World, low, Stim(StimulusKind.Punched, puncher.Tag, puncher.Position));
            PedsSystem.Deliver(a.World, gun, Stim(StimulusKind.Punched, armed.Tag, armed.Position));
            Assert.IsType<FightBrain>(f.Brain);
            Assert.IsType<FleeBrain>(m.Brain);
            Assert.IsType<FleeBrain>(low.Brain);
            Assert.IsType<FleeBrain>(gun.Brain);
        }

        [Fact]
        public void FightBrainApproachesAndSwingsWithHostileFlag()
        {
            var a = new Arena();
            var target = a.Spawn(0f, 6f);
            var p = a.Spawn(0f, 0f);
            p.Brain = new FightBrain(Adult, target.Tag);
            var sys = new PedsSystem();
            a.Run(sys, 0.5f);
            Assert.True(p.Has(PedFlags.Hostile));
            Assert.True(p.Position.Z > 1f);
            a.Run(sys, 4f);
            Assert.True(target.Health < 100f, "got hit by fists");
        }

        [Fact]
        public void FightBrainGivesUpWhenHurtBelowFortyOrWhenTheTargetDrawsAGun()
        {
            var a = new Arena();
            var target = a.Spawn(0f, 3f);
            var p = a.Spawn(0f, 0f);
            p.Brain = new FightBrain(Adult, target.Tag);
            p.Health = 39f;
            a.Run(new PedsSystem(), 0.2f);
            Assert.IsType<FleeBrain>(p.Brain);

            var q = a.Spawn(10f, 0f);
            q.Brain = new FightBrain(Adult, target.Tag);
            target.CurrentWeapon = (int)WeaponId.Pistol;
            a.Run(new PedsSystem(), 0.2f);
            Assert.IsType<FleeBrain>(q.Brain);
        }

        [Fact]
        public void FightBrainReturnsWhenTheTargetDies()
        {
            var a = new Arena();
            var target = a.Spawn(0f, 3f);
            var p = a.Spawn(0f, 0f);
            p.Brain = new FightBrain(Adult, target.Tag);
            target.Flags |= PedFlags.Dead;
            a.Run(new PedsSystem(), 0.2f);
            Assert.IsType<ReturnBrain>(p.Brain);
            Assert.False(p.Has(PedFlags.Hostile));
        }

        [Fact]
        public void CallPoliceAssignStartsAPhoneCallThatCompletesAfterFourToTenSeconds()
        {
            var a = new Arena();
            var w = a.Spawn(0f, 0f);
            long seq = a.World.Events.NextSeq;
            Ped caller = CallPoliceBrain.Assign(a.World, new WitnessCandidate { Tag = w.Tag, Ped = w, Position = w.Position }, 42, a.Player.Tag);
            Assert.Same(w, caller);
            var call = Assert.IsType<CallPoliceBrain>(w.Brain);
            Assert.InRange(call.Duration, 4f, 10f);
            var sys = new PedsSystem();
            a.Run(sys, 0.2f);
            Assert.True(w.Has(PedFlags.OnPhone));
            Assert.Equal(PedAnim.Phone, w.Anim);
            Assert.Equal(1, Arena.Count(a.EventsSince(seq), GameEventType.CallStarted));
            a.Run(sys, call.Duration - 0.5f);
            Assert.Equal(0, Arena.Count(a.EventsSince(seq), GameEventType.CallCompleted));
            a.Run(sys, 0.7f);
            var done = a.EventsSince(seq).FindAll(e => e.Type == GameEventType.CallCompleted);
            Assert.Single(done);
            Assert.Equal(42f, done[0].Value);
            Assert.Equal(w.Tag, done[0].ActorA);
            Assert.False(w.Has(PedFlags.OnPhone));
            Assert.IsType<ReturnBrain>(w.Brain);
        }

        [Fact]
        public void CallDurationsAcrossSeedsStayInRangeAndVary()
        {
            float lo = 99f, hi = 0f;
            for (uint seed = 1; seed <= 40; seed++)
            {
                var a = new Arena(seed);
                var b = new CallPoliceBrain(a.World, Adult, 1, 0);
                Assert.InRange(b.Duration, 4f, 10f);
                if (b.Duration < lo) lo = b.Duration;
                if (b.Duration > hi) hi = b.Duration;
            }
            Assert.True(hi - lo > 2f);
        }

        [Fact]
        public void CallPoliceOnAnAmbientWitnessPromotesIt()
        {
            var a = new Arena();
            var amb = a.AddAmbient(9, 900u, 0f, 5f);
            Ped caller = CallPoliceBrain.Assign(a.World, new WitnessCandidate { Tag = WorldTag.ForAmbientCitizen(9), Ambient = amb, Citizen = 900u }, 7, 0);
            Assert.NotNull(caller);
            Assert.Equal(900u, caller.Citizen);
            Assert.IsType<CallPoliceBrain>(caller.Brain);
        }

        [Theory]
        [InlineData(StimulusKind.AimedAt, 90f, true)]
        [InlineData(StimulusKind.Punched, 0f, true)]
        [InlineData(StimulusKind.Hurt, 0f, true)]
        [InlineData(StimulusKind.ShotAt, 0f, true)]
        [InlineData(StimulusKind.VehicleThreat, 0f, true)]
        [InlineData(StimulusKind.GunshotHeard, 10f, true)]
        [InlineData(StimulusKind.GunshotHeard, 40f, false)]
        public void ThreatsInterruptACallAndTheCallerFlees(StimulusKind kind, float distance, bool interrupts)
        {
            var a = new Arena();
            var w = a.Spawn(0f, 0f);
            var call = new CallPoliceBrain(a.World, Adult, 5, 0);
            w.Brain = call;
            var sys = new PedsSystem();
            a.Run(sys, 0.2f);
            long seq = a.World.Events.NextSeq;
            PedsSystem.Deliver(a.World, w, Stim(kind, a.Player.Tag, new Vec3(0f, 0f, -distance), distance));
            Assert.Equal(interrupts, call.Interrupted);
            a.Run(sys, 12f);
            var ev = a.EventsSince(seq);
            Assert.Equal(interrupts ? 1 : 0, Arena.Count(ev, GameEventType.CallInterrupted));
            Assert.Equal(interrupts ? 0 : 1, Arena.Count(ev, GameEventType.CallCompleted));
        }

        [Fact]
        public void KnockedDownBrainLiesTwoToFourSecondsThenGetsUpAndFlees()
        {
            var a = new Arena();
            var p = a.Spawn(0f, 0f);
            Damage.ApplyToPed(a.World, p, 5f, a.Player.Tag, DamageKind.Melee, true, 0f);
            var down = Assert.IsType<KnockedDownBrain>(p.Brain);
            Assert.InRange(down.Duration, 2f, 4f);
            var sys = new PedsSystem();
            a.Run(sys, 1f);
            Assert.True(p.Has(PedFlags.KnockedDown));
            Assert.Equal(PedAnim.LieDown, p.Anim);
            PedsSystem.Deliver(a.World, p, Stim(StimulusKind.GunshotHeard, 0, new Vec3(0f, 0f, -5f)));
            Assert.IsType<KnockedDownBrain>(p.Brain);
            a.Run(sys, 3.2f);
            Assert.False(p.Has(PedFlags.KnockedDown));
            Assert.IsType<FleeBrain>(p.Brain);
        }

        [Fact]
        public void InjuredPedStaysDownSixSecondsAndDeadPedsStayDown()
        {
            var a = new Arena();
            var p = a.Spawn(0f, 0f);
            Damage.ApplyToPed(a.World, p, 70f, a.Player.Tag, DamageKind.Melee, true, 0f);
            Assert.Equal(6f, ((KnockedDownBrain)p.Brain).Duration);
            var sys = new PedsSystem();
            a.Run(sys, 5.5f);
            Assert.True(p.Has(PedFlags.KnockedDown));
            a.Run(sys, 1f);
            Assert.False(p.Has(PedFlags.KnockedDown));

            var d = a.Spawn(10f, 0f);
            d.Brain = new IdleBrain(a.World, Adult);
            Damage.ApplyToPed(a.World, d, 500f, 0, DamageKind.Bullet, false, 0f);
            Vec3 where = d.Position;
            a.Run(sys, 10f);
            Assert.True(d.IsDead);
            Assert.True(Vec3.Distance(where, d.Position) < 0.05f);
        }

        [Fact]
        public void ReturnBrainWalksAwayFromThePlayerAtWalkingSpeed()
        {
            var a = new Arena();
            a.Player.Motor.Teleport(new Vec3(0f, 0f, -4f));
            var p = a.Spawn(0f, 0f);
            p.Brain = new ReturnBrain(Adult);
            a.Run(new PedsSystem(), 3f);
            Assert.Equal(PedAnim.Walk, p.Anim);
            Assert.InRange(p.Position.Z, 2.5f, 5f);
        }

        [Fact]
        public void SidestepDodgesThenFlees()
        {
            var a = new Arena();
            var p = Idle(a, 0f, 0f, Adult);
            Car car = a.World.SpawnCar(0, new Vec3(0f, 0f, -15f), 0f, CarOwner.Civilian, 0);
            PedsSystem.Deliver(a.World, p, Stim(StimulusKind.VehicleThreat, car.Tag, car.Position, 15f));
            Assert.IsType<SidestepBrain>(p.Brain);
            var sys = new PedsSystem();
            a.Run(sys, 0.4f);
            Assert.Equal(PedAnim.Sprint, p.Anim);
            a.Run(sys, 0.5f);
            Assert.IsType<FleeBrain>(p.Brain);
        }

        [Fact]
        public void ShockedDriverIsStunnedThenFightsOrStaysSeated()
        {
            var a = new Arena();
            Car car = a.World.SpawnCar(0, new Vec3(0f, 0f, 0f), 0f, CarOwner.Civilian, 0);
            var d = a.Spawn(40f, 40f);
            a.World.PutInCar(d, car, 0);
            d.Brain = new ShockedDriverBrain(a.World, Adult);
            Assert.True(d.Brain.IsActive);
            a.Run(new PedsSystem(), 4.2f);
            bool fights = d.Brain is FightBrain && !d.Has(PedFlags.InVehicle);
            bool stays = d.Brain is ShockedDriverBrain && d.Has(PedFlags.InVehicle) && !d.Brain.IsActive;
            Assert.True(fights ^ stays);
        }

        [Fact]
        public void ShockedDriverGetsOutAndFleesFromGunfire()
        {
            var a = new Arena();
            Car car = a.World.SpawnCar(0, new Vec3(30f, 0f, 0f), 0f, CarOwner.Civilian, 0);
            var e = a.Spawn(50f, 50f);
            a.World.PutInCar(e, car, 0);
            var sb = new ShockedDriverBrain(a.World, Adult);
            e.Brain = sb;
            sb.React(a.World, e, Stim(StimulusKind.GunshotHeard, 0, Vec3.Zero));
            Assert.IsType<FleeBrain>(e.Brain);
            Assert.False(e.Has(PedFlags.InVehicle));
        }
    }
}
