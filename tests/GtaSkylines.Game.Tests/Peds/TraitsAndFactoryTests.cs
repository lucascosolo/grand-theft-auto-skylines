using GtaSkylines.Game.City;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Peds;
using GtaSkylines.Game.Tests.Combat;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.Peds
{
    /// <summary>PedTraits.Roll and PedBrainFactory (carjacked fight or flee by seed).</summary>
    public class TraitsAndFactoryTests
    {
        private static Ped WithModel(Arena a, int model, uint citizen)
        {
            var p = a.World.SpawnPed(PedKind.Civilian, model, new Vec3(0f, 0f, 0f), 0f);
            p.Citizen = citizen;
            return p;
        }

        private static float FighterShare(Arena a, int model)
        {
            int n = 0;
            for (uint c = 1; c <= 600; c++)
                if (PedTraits.Roll(a.World, WithModel(a, model, c), PedOrigin.PromotedCitizen, default(NpcSpot)).Fighter) n++;
            return n / 600f;
        }

        [Fact]
        public void FighterSharesFollowAgeAndSex()
        {
            var a = new Arena();
            a.City.CitizenModels.Add(new CitizenModel { Id = 8, AgeGroup = 0 });
            a.City.CitizenModels.Add(new CitizenModel { Id = 9, AgeGroup = 4 });
            a.City.CitizenModels.Add(new CitizenModel { Id = 10, AgeGroup = 3, Female = false });
            Assert.InRange(FighterShare(a, 10), 0.09f, 0.22f);
            Assert.InRange(FighterShare(a, 0), 0.27f, 0.43f);
            Assert.Equal(0f, FighterShare(a, 8));
            Assert.Equal(0f, FighterShare(a, 9));
        }

        [Fact]
        public void SeniorsAreFlaggedAndFleeSpeedIsFiveToSix()
        {
            var a = new Arena();
            int id = a.City.CitizenModels.Count; // FakeCity looks models up by list position
            a.City.CitizenModels.Add(new CitizenModel { Id = id, AgeGroup = 4 });
            var t = PedTraits.Roll(a.World, WithModel(a, id, 5u), PedOrigin.PromotedCitizen, default(NpcSpot));
            Assert.True(t.Senior);
            Assert.InRange(t.FleeSpeed, 5f, 6f);
        }

        [Fact]
        public void SameCitizenAndIdGiveTheSameTraitsAndTheSeedIsFromThem()
        {
            var a = new Arena();
            var p = WithModel(a, 2, 77u);
            var t1 = PedTraits.Roll(a.World, p, PedOrigin.PromotedCitizen, default(NpcSpot));
            var t2 = PedTraits.Roll(a.World, p, PedOrigin.PromotedCitizen, default(NpcSpot));
            Assert.Equal(t1.Fighter, t2.Fighter);
            Assert.Equal(t1.FleeSpeed, t2.FleeSpeed);
            Assert.Equal(Rng.Seed(77u, (uint)p.Id), t1.Seed);
        }

        [Fact]
        public void InteriorOccupantTraitsCarryTheSpot()
        {
            var a = new Arena();
            var p = WithModel(a, 2, 5u);
            var spot = new NpcSpot { Role = NpcRole.Clerk, Position = new Vec3(1f, 0f, 2f), YawDeg = 30f, Seated = false };
            var t = PedTraits.Roll(a.World, p, PedOrigin.InteriorOccupant, spot);
            Assert.True(t.HasHome);
            Assert.True(t.Clerk);
        }

        [Fact]
        public void CarjackedDriversFightOnASeededThirtyPercentRoll()
        {
            var a = new Arena();
            var f = new PedBrainFactory();
            int fights = 0;
            for (uint c = 1; c <= 400; c++)
            {
                var p = WithModel(a, 2, c);
                var traits = PedTraits.Roll(a.World, p, PedOrigin.CarjackedDriver, default(NpcSpot));
                bool expect = new Rng(Rng.Seed(traits.Seed, 1u)).NextFloat() < PedTraits.CarjackedFightChance;
                var brain = f.Create(a.World, p, PedOrigin.CarjackedDriver, a.Player.Tag, default(NpcSpot));
                Assert.Equal(expect, brain is FightBrain);
                Assert.True(expect ? brain is FightBrain : brain is FleeBrain);
                if (expect) fights++;
            }
            Assert.InRange(fights / 400f, 0.2f, 0.4f);
        }

        private sealed class MarkerFactory : IBrainFactory
        {
            public readonly IPedBrain Brain = new IdleBrain(null, default(PedTraits));
            public IPedBrain Create(GameWorld world, Ped ped, PedOrigin origin, int cause, NpcSpot spot) { return Brain; }
        }

        [Fact]
        public void FactoryMapsOriginsToBrains()
        {
            var a = new Arena();
            var f = new PedBrainFactory();
            var p = WithModel(a, 2, 1u);
            Assert.IsType<IdleBrain>(f.Create(a.World, p, PedOrigin.PromotedCitizen, 0, default(NpcSpot)));
            Assert.IsType<IdleBrain>(f.Create(a.World, p, PedOrigin.InteriorOccupant, 0, default(NpcSpot)));
            Assert.IsType<ShockedDriverBrain>(f.Create(a.World, p, PedOrigin.ShockedDriver, 0, default(NpcSpot)));
            Assert.Null(f.Create(a.World, p, PedOrigin.Scripted, 0, default(NpcSpot)));
        }

        [Fact]
        public void CopsGoToTheCopFactoryOrGetNothing()
        {
            var a = new Arena();
            var f = new PedBrainFactory();
            var cop = a.Spawn(0f, 0f, 0f, PedKind.Cop);
            Assert.Null(f.Create(a.World, cop, PedOrigin.Scripted, 0, default(NpcSpot)));
            var marker = new MarkerFactory();
            f.Cops = marker;
            Assert.Same(marker.Brain, f.Create(a.World, cop, PedOrigin.PromotedCitizen, 0, default(NpcSpot)));
        }
    }
}
