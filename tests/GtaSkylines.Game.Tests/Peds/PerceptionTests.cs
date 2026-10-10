using System.Collections.Generic;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Peds;
using GtaSkylines.Game.Tests.Combat;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.Peds
{
    /// <summary>Perception: cone, range by day and night, occlusion, hearing, witnesses.</summary>
    public class PerceptionTests
    {
        private static Vec3 At(float x, float z) { return new Vec3(x, 0f, z); }

        [Theory]
        [InlineData(0f, 30f, true)]
        [InlineData(0f, 39f, true)]
        [InlineData(0f, 41f, false)]
        [InlineData(0f, -10f, false)]
        [InlineData(10f, 10f, true)]
        [InlineData(-30f, 10f, false)]
        public void ConeAndDayRange(float x, float z, bool seen)
        {
            var a = new Arena();
            a.World.Clock.Hour = 12f;
            var ped = a.Spawn(0f, 0f, 0f);
            Assert.Equal(seen, Perception.CanSee(a.World, ped, At(x, z)));
        }

        [Fact]
        public void ConeEdgeIsSixtyDegreesEitherSide()
        {
            var a = new Arena();
            var ped = a.Spawn(0f, 0f, 0f);
            float r = 20f;
            Assert.True(Perception.CanSee(a.World, ped, At(r * (float)System.Math.Sin(0.99 * System.Math.PI / 3), r * (float)System.Math.Cos(0.99 * System.Math.PI / 3))));
            Assert.False(Perception.CanSee(a.World, ped, At(r * (float)System.Math.Sin(1.02 * System.Math.PI / 3), r * (float)System.Math.Cos(1.02 * System.Math.PI / 3))));
        }

        [Fact]
        public void SomethingRightBehindWithinTwoMetresIsStillNoticed()
        {
            var a = new Arena();
            var ped = a.Spawn(0f, 0f, 0f);
            Assert.True(Perception.CanSee(a.World, ped, At(0f, -1.5f)));
            Assert.False(Perception.CanSee(a.World, ped, At(0f, -3f)));
        }

        [Fact]
        public void NightShortensSightFromFortyToTwentyFiveMetres()
        {
            var a = new Arena();
            var ped = a.Spawn(0f, 0f, 0f);
            a.World.Clock.Hour = 23f;
            Assert.Equal(25f, Perception.SightRange(a.World));
            Assert.True(Perception.CanSee(a.World, ped, At(0f, 24f)));
            Assert.False(Perception.CanSee(a.World, ped, At(0f, 26f)));
            a.World.Clock.Hour = 12f;
            Assert.Equal(40f, Perception.SightRange(a.World));
            Assert.True(Perception.CanSee(a.World, ped, At(0f, 26f)));
        }

        [Fact]
        public void AWallBetweenBlocksSight()
        {
            var a = new Arena();
            var ped = a.Spawn(0f, 0f, 0f);
            Assert.True(Perception.CanSee(a.World, ped, At(0f, 15f)));
            a.Wall(-5f, 7f, 5f, 7f);
            Assert.False(Perception.CanSee(a.World, ped, At(0f, 15f)));
            Assert.True(Perception.CanSee(a.World, ped, At(0f, 5f)));
        }

        [Fact]
        public void DeadAndKnockedDownPedsSeeNothing()
        {
            var a = new Arena();
            var dead = a.Spawn(0f, 0f);
            dead.Flags |= PedFlags.Dead;
            var down = a.Spawn(5f, 0f);
            down.Flags |= PedFlags.KnockedDown;
            Assert.False(Perception.CanSee(a.World, dead, At(0f, 5f)));
            Assert.False(Perception.CanSee(a.World, down, At(5f, 5f)));
        }

        [Fact]
        public void GeneralOverloadUsesTheGivenEyeFacingAndRange()
        {
            var a = new Arena();
            var eye = new Vec3(0f, 1.6f, 0f);
            Assert.True(Perception.CanSee(a.World, eye, 90f, 10f, At(8f, 0f)));
            Assert.False(Perception.CanSee(a.World, eye, 90f, 10f, At(12f, 0f)));
            Assert.False(Perception.CanSee(a.World, eye, 90f, 10f, At(-8f, 0f)));
        }

        [Theory]
        [InlineData(GameEventType.Gunshot, 59f, 0f, true)]
        [InlineData(GameEventType.Gunshot, 61f, 0f, false)]
        [InlineData(GameEventType.Gunshot, 30f, 25f, false)]
        [InlineData(GameEventType.Gunshot, 30f, 0f, true)]
        [InlineData(GameEventType.Explosion, 99f, 0f, true)]
        [InlineData(GameEventType.Explosion, 101f, 0f, false)]
        [InlineData(GameEventType.Punched, 1f, 0f, false)]
        public void HearingRadii(GameEventType type, float distance, float valueRadius, bool heard)
        {
            var a = new Arena();
            var ped = a.Spawn(0f, 0f);
            var e = new GameEvent { Type = type, Position = At(0f, -distance), Value = valueRadius };
            Assert.Equal(heard, Perception.Hears(a.World, ped, e));
        }

        [Fact]
        public void DeadPedsHearNothingAndWallsDoNotBlockSound()
        {
            var a = new Arena();
            var ped = a.Spawn(0f, 0f);
            a.Wall(-9f, -10f, 9f, -10f);
            var e = new GameEvent { Type = GameEventType.Gunshot, Position = At(0f, -20f), Value = 60f };
            Assert.True(Perception.Hears(a.World, ped, e));
            ped.Flags |= PedFlags.Dead;
            Assert.False(Perception.Hears(a.World, ped, e));
        }

        private static GameEvent Punch(Vec3 where, int actorA, int actorB)
        {
            return new GameEvent { Type = GameEventType.Punched, Position = where, ActorA = actorA, ActorB = actorB };
        }

        [Fact]
        public void WitnessesAreOnlyThoseWhoCouldSeeTheEventNearestFirst()
        {
            var a = new Arena();
            var facing = a.Spawn(0f, 20f, 180f);
            var nearer = a.Spawn(5f, 8f, 270f);
            var turnedAway = a.Spawn(0f, 12f, 0f);
            var behindWall = a.Spawn(-6f, 20f, 180f);
            a.Wall(-9f, 10f, -3f, 10f);
            var perpetrator = a.Spawn(0f, 0f);
            var list = new List<WitnessCandidate>();
            int n = Perception.WitnessesOf(a.World, Punch(At(0f, 0f), perpetrator.Tag, 0), list);
            Assert.Equal(list.Count, n);
            Assert.Equal(new[] { nearer.Tag, facing.Tag }, list.ConvertAll(w => w.Tag).ToArray());
            Assert.DoesNotContain(list, w => w.Tag == turnedAway.Tag || w.Tag == behindWall.Tag || w.Tag == perpetrator.Tag);
            Assert.True(list[0].Distance <= list[1].Distance);
        }

        [Fact]
        public void PlayerAndCopsAreNeverWitnessesButTheVictimMayBe()
        {
            var a = new Arena();
            a.Player.Motor.Teleport(At(0f, 5f));
            a.Player.YawDeg = 180f;
            var cop = a.Spawn(0f, 6f, 180f, PedKind.Cop);
            var victim = a.Spawn(1f, 4f, 180f);
            var list = new List<WitnessCandidate>();
            Perception.WitnessesOf(a.World, Punch(At(0f, 0f), 0, victim.Tag), list);
            Assert.DoesNotContain(list, w => w.Tag == a.Player.Tag || w.Tag == cop.Tag);
            Assert.Contains(list, w => w.Tag == victim.Tag);
        }

        [Fact]
        public void AmbientCitizensWitnessWithoutBeingPromoted()
        {
            var a = new Arena();
            var amb = a.AddAmbient(5, 800u, 0f, 15f, 180f);
            var list = new List<WitnessCandidate>();
            Perception.WitnessesOf(a.World, Punch(At(0f, 0f), 0, 0), list);
            var w = Assert.Single(list);
            Assert.Null(w.Ped);
            Assert.Equal(800u, w.Citizen);
            Assert.Equal(amb.Instance, w.Ambient.Instance);
            Assert.Null(a.World.FindPedByCitizen(800u));
        }

        [Fact]
        public void APromotedAmbientCitizenIsListedOnceAsAGamePed()
        {
            var a = new Arena();
            var amb = a.AddAmbient(6, 801u, 0f, 15f, 180f);
            Ped promoted = a.World.PromoteCitizen(amb, PedOrigin.PromotedCitizen, 0);
            a.Rebuild();
            var list = new List<WitnessCandidate>();
            Perception.WitnessesOf(a.World, Punch(At(0f, 0f), 0, 0), list);
            Assert.Single(list);
            Assert.Equal(promoted.Tag, list[0].Tag);
        }

        [Fact]
        public void SneakingPlayerHalvesTheRangeAtWhichHeIsSeen()
        {
            var a = new Arena();
            a.Spawn(0f, 30f, 180f);
            var list = new List<WitnessCandidate>();
            var e = Punch(At(0f, 0f), a.Player.Tag, 0);
            Assert.Equal(1, Perception.WitnessesOf(a.World, e, list));
            a.Player.Flags |= PedFlags.Sneaking;
            Assert.Equal(0, Perception.WitnessesOf(a.World, e, list));
        }

        [Fact]
        public void WitnessesOfClearsTheListFirst()
        {
            var a = new Arena();
            var list = new List<WitnessCandidate> { new WitnessCandidate { Tag = 5 } };
            Assert.Equal(0, Perception.WitnessesOf(a.World, Punch(At(0f, 0f), 0, 0), list));
            Assert.Empty(list);
        }
    }
}
