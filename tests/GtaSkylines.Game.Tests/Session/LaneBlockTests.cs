using GtaSkylines.Game.City;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Roads;
using GtaSkylines.Game.Session;
using GtaSkylines.Game.Tests.Roads;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.SessionTests
{
    /// <summary>F-VEH-8 (docs/contracts/C2-abandoned-cars.md): Game rules for BlockLane / UnblockLane effects.</summary>
    public class LaneBlockTests
    {
        private const int Segment = 7;
        private const float Yaw = 20f;
        private static readonly Vec3 OnLane = new Vec3(1.5f, 0f, 0f);
        private static readonly Vec3 PlayerAway = new Vec3(-20f, 0f, 20f);

        /// <summary>Two-way road, 1 + 1 lanes of 3 m, segment 7, along +z through the origin; player 28 m from the origin.</summary>
        private static Sim NewSim()
        {
            var sim = new Sim(road: false, feet: PlayerAway);
            Net n = new Net().Node(1, 0, -300).Node(2, 0, 300).Road(Segment, 1, 2, 1, 1, 10f, RoadFlags.None, 3f);
            sim.W.Roads.Rebuild(n.Nodes, n.Segments, false);
            return sim;
        }

        private static Car Spawn(Sim sim, Vec3 pivot, CarOwner owner = CarOwner.Abandoned)
        {
            Car car = sim.W.SpawnCar(0, pivot, Yaw, owner, 0x112233);
            sim.Run(0.5, In.Idle);
            return car;
        }

        private static int Count(Sim sim, CityEffectKind kind, int tag)
        {
            int c = 0;
            foreach (CityEffect e in sim.Effects) if (e.Kind == kind && e.Tag == tag) c++;
            return c;
        }

        private static int Blocks(Sim sim, Car car) { return Count(sim, CityEffectKind.BlockLane, car.Tag); }
        private static int Unblocks(Sim sim, Car car) { return Count(sim, CityEffectKind.UnblockLane, car.Tag); }

        private static Car Blocked(out Sim simOut)
        {
            Sim sim = NewSim();
            Car car = Spawn(sim, OnLane);
            sim.Run(1.0, In.Idle);
            Assert.Equal(1, Blocks(sim, car));
            simOut = sim;
            return car;
        }

        /// <summary>Stands the player beside the driver door of the car at <see cref="OnLane"/> and takes the car.</summary>
        private static void PlayerEnters(Sim sim)
        {
            sim.Place(new Vec3(-1.5f, 0f, 0.44f), 90f);
            sim.Run(0.2, In.Idle);
            sim.Tick(In.Press(GameButton.EnterExit));
            Assert.True(sim.Until(() => sim.Player.Has(PedFlags.InVehicle), 4.0, In.Idle), "player never got in");
        }

        [Fact]
        public void AbandonedStandingCarOnTheLane_BlocksOnce_WithItsPose()
        {
            Sim sim = NewSim();
            Car car = Spawn(sim, OnLane);
            sim.Run(2.0, In.Idle);
            Assert.Equal(1, Blocks(sim, car));
            CityEffect e = sim.FirstEffect(CityEffectKind.BlockLane);
            Assert.Equal(car.Tag, e.Tag);
            Assert.Equal(Segment, e.Segment);
            Assert.Equal(car.Model.Id, e.Model);
            Sim.Near(car.Position - car.Rotation * car.Model.CenterOffset, e.Position, 0.05f, "pivot");
            Sim.Near(0f, Sim.AngleDiff(Yaw, e.YawDeg), 1.5f, "yaw");
            sim.Run(5.0, In.Idle);
            Assert.Equal(1, Blocks(sim, car));
            Assert.Equal(0, Unblocks(sim, car));
        }

        [Fact]
        public void CarTenMetresOffTheRoad_DoesNotBlock()
        {
            Sim sim = NewSim();
            Car car = Spawn(sim, new Vec3(10f, 0f, 0f));
            sim.Run(3.0, In.Idle);
            Assert.Equal(0, Blocks(sim, car));
        }

        [Fact]
        public void PlayerLeavesTheirCarOnTheLane_BlocksOnceItStands()
        {
            Sim sim = NewSim();
            Car car = Spawn(sim, OnLane);
            PlayerEnters(sim);
            sim.Run(1.0, In.Idle);
            int whileDriving = Blocks(sim, car);
            sim.Tick(In.Press(GameButton.EnterExit));
            Assert.True(sim.Until(() => !sim.Player.Has(PedFlags.InVehicle), 3.0, In.Idle));
            sim.Run(2.0, In.Idle);
            Assert.Equal(CarOwner.Abandoned, car.Owner);
            Assert.Equal(whileDriving + 1, Blocks(sim, car));
            Assert.Equal(whileDriving, Unblocks(sim, car)); // the earlier block, if any, was released on boarding
        }

        [Fact]
        public void PlayerTakesTheBlockingCarAgain_UnblocksOnce()
        {
            Car car = Blocked(out Sim sim);
            PlayerEnters(sim);
            sim.Run(2.0, In.Idle);
            Assert.Equal(1, Unblocks(sim, car));
            Assert.Equal(1, Blocks(sim, car));
        }

        [Fact]
        public void CarMovedThreeMetres_UnblocksOnce()
        {
            Car car = Blocked(out Sim sim);
            car.Body.Teleport(car.Position + new Vec3(0f, 0f, 3f), car.Rotation);
            sim.Run(2.0, In.Idle);
            Assert.Equal(1, Unblocks(sim, car));
        }

        [Fact]
        public void CarRemoved_UnblocksOnce()
        {
            Car car = Blocked(out Sim sim);
            sim.W.RemoveCar(car);
            sim.Run(2.0, In.Idle);
            Assert.Equal(1, Unblocks(sim, car));
        }

        [Fact]
        public void PlayerBeyondTheSnapshotRadius_UnblocksOnce()
        {
            Car car = Blocked(out Sim sim);
            float far = sim.W.Ambient.Radius + 20f;
            sim.Place(new Vec3(-20f, 0f, far), 0f);
            sim.Run(2.0, In.Idle);
            Assert.Equal(1, Unblocks(sim, car));
            Assert.Equal(1, Blocks(sim, car));
        }

        [Fact]
        public void After180Seconds_UnblocksOnce_AndNeverBlocksAgain()
        {
            Car car = Blocked(out Sim sim);
            sim.Run(LaneBlocks.MaxSeconds + 5.0, In.Idle);
            Assert.Equal(1, Unblocks(sim, car));
            sim.Run(10.0, In.Idle);
            Assert.Equal(1, Blocks(sim, car));
            Assert.Equal(1, Unblocks(sim, car));
        }

        [Fact]
        public void StandingCarWithThePlayerAsDriver_EmitsNothing()
        {
            Sim sim = NewSim();
            Car car = Spawn(sim, OnLane);
            PlayerEnters(sim);
            int blocksSoFar = Blocks(sim, car);
            int unblocksSoFar = Unblocks(sim, car);
            sim.Run(3.0, In.Idle);
            Assert.Equal(blocksSoFar, Blocks(sim, car));
            Assert.Equal(unblocksSoFar, Unblocks(sim, car));
            Assert.Equal(blocksSoFar, unblocksSoFar); // whatever blocked before boarding was released by boarding
        }

        [Theory]
        [InlineData(CarOwner.Civilian)]
        [InlineData(CarOwner.Player)]
        public void StandingCarOfAnotherOwner_DoesNotBlock(CarOwner owner)
        {
            Sim sim = NewSim();
            Car car = Spawn(sim, OnLane, owner);
            sim.Run(3.0, In.Idle);
            Assert.Equal(0, Blocks(sim, car));
        }

        [Fact]
        public void MovingCar_BlocksOnlyOnceItStands()
        {
            Sim sim = NewSim();
            Car mover = sim.W.SpawnCar(0, new Vec3(1.5f, 0f, -20f), Yaw, CarOwner.Abandoned, 0x112233);
            mover.Body.SetVelocity(new Vec3(0f, 0f, 4f), Vec3.Zero);
            for (int i = 0; i < 60 * 60 && mover.Body.Speed >= LaneBlocks.StandingSpeed; i++)
            {
                sim.Tick(In.Idle);
                if (mover.Body.Speed >= LaneBlocks.StandingSpeed) Assert.Equal(0, Blocks(sim, mover));
            }
            Assert.True(mover.Body.Speed < LaneBlocks.StandingSpeed, "car never came to rest");
            sim.Run(1.0, In.Idle);
            Assert.Equal(1, Blocks(sim, mover));
        }
    }

    public class LaneFindTests
    {
        private static RoadGraph Road()
        {
            return new Net().Node(1, 0, 0).Node(2, 0, 200).Road(7, 1, 2, 1, 1, 10f, RoadFlags.None, 3f).Build();
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(2.9f)]
        [InlineData(-2.9f)]
        public void WithinHalfTheLanesWidth_IsALane(float x)
        {
            RoadPoint p;
            Assert.True(LaneBlocks.TryFindLane(Road(), new Vec3(x, 0f, 100f), out p));
            Assert.Equal(7, p.SegmentId);
        }

        [Fact]
        public void OnTheCentreline_ReturnsTheSegmentPoint()
        {
            RoadPoint p;
            Assert.True(LaneBlocks.TryFindLane(Road(), new Vec3(0f, 0f, 100f), out p));
            Assert.Equal(7, p.SegmentId);
            Assert.InRange(p.DistanceAlong, 99f, 101f);
        }

        [Fact]
        public void BesideTheRoad_ThreeAndAHalfMetres_IsNotALane()
        {
            RoadPoint p;
            Assert.False(LaneBlocks.TryFindLane(Road(), new Vec3(3.5f, 0f, 100f), out p));
        }

        [Fact]
        public void FiveMetresAboveTheRoad_IsNotALane()
        {
            RoadPoint p;
            Assert.False(LaneBlocks.TryFindLane(Road(), new Vec3(0f, 5f, 100f), out p));
        }

        [Fact]
        public void EmptyGraph_IsNotALane()
        {
            RoadPoint p;
            Assert.False(LaneBlocks.TryFindLane(new RoadGraph(), new Vec3(0f, 0f, 0f), out p));
        }
    }
}
