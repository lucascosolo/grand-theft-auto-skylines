using GtaSkylines.Game.Roads;
using GtaSkylines.Mod.World;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class LaneCounterTests
    {
        private static LaneSpec Lane(bool car, byte dir, float width = 3f, float speed = 1f)
        {
            return new LaneSpec { Car = car, Direction = dir, Width = width, SpeedLimit = speed };
        }

        [Fact]
        public void Count_two_way_road()
        {
            var lanes = new[] { Lane(true, 1, 3f, 1f), Lane(true, 2, 4f, 2f), Lane(true, 1, 5f, 1.5f) };
            var s = LaneCounter.Count(lanes, 3, false);
            Assert.Equal(2, s.Forward);
            Assert.Equal(1, s.Backward);
            Assert.Equal(4f, s.LaneWidth, 3);
            Assert.Equal(2f, s.SpeedLimit, 3);
        }

        [Fact]
        public void Count_ignores_non_car_lanes()
        {
            var lanes = new[] { Lane(false, 1, 10f, 9f), Lane(true, 2, 3f, 1f) };
            var s = LaneCounter.Count(lanes, 2, false);
            Assert.Equal(0, s.Forward);
            Assert.Equal(1, s.Backward);
            Assert.Equal(3f, s.LaneWidth, 3);
            Assert.Equal(1f, s.SpeedLimit, 3);
        }

        [Theory]
        [InlineData((byte)3)]
        [InlineData((byte)7)]
        [InlineData((byte)0xB)]
        public void Count_both_direction_lane_adds_to_both(byte dir)
        {
            var s = LaneCounter.Count(new[] { Lane(true, dir) }, 1, false);
            Assert.Equal(1, s.Forward);
            Assert.Equal(1, s.Backward);
        }

        [Fact]
        public void Count_inverted_swaps_totals()
        {
            var lanes = new[] { Lane(true, 1), Lane(true, 1), Lane(true, 2) };
            var s = LaneCounter.Count(lanes, 3, true);
            Assert.Equal(1, s.Forward);
            Assert.Equal(2, s.Backward);
        }

        [Fact]
        public void Count_reads_only_first_count_entries()
        {
            var lanes = new[] { Lane(true, 1, 3f, 1f), Lane(true, 2, 9f, 9f) };
            var s = LaneCounter.Count(lanes, 1, false);
            Assert.Equal(1, s.Forward);
            Assert.Equal(0, s.Backward);
            Assert.Equal(3f, s.LaneWidth, 3);
            Assert.Equal(1f, s.SpeedLimit, 3);
        }

        [Fact]
        public void Count_no_car_lanes_is_zero()
        {
            var s = LaneCounter.Count(new[] { Lane(false, 1) }, 1, false);
            Assert.Equal(0, s.Forward);
            Assert.Equal(0, s.Backward);
            Assert.Equal(0f, s.LaneWidth);
            Assert.Equal(0f, s.SpeedLimit);
        }

        [Theory]
        [InlineData(false, 0)]
        [InlineData(false, -1)]
        [InlineData(true, 0)]
        public void Count_null_or_empty_is_zero(bool nonNull, int count)
        {
            var s = LaneCounter.Count(nonNull ? new[] { Lane(true, 1) } : null, count, false);
            Assert.Equal(0, s.Forward);
            Assert.Equal(0, s.Backward);
            Assert.Equal(0f, s.LaneWidth);
            Assert.Equal(0f, s.SpeedLimit);
        }

        [Theory]
        [InlineData(false, 2, 2, false, false, false, RoadFlags.None)]
        [InlineData(false, 2, 0, false, false, false, RoadFlags.OneWay)]
        [InlineData(false, 0, 3, false, false, false, RoadFlags.OneWay)]
        [InlineData(false, 0, 0, false, false, false, RoadFlags.None)]
        [InlineData(true, 3, 0, false, false, false, RoadFlags.Highway | RoadFlags.OneWay)]
        [InlineData(false, 1, 1, true, false, false, RoadFlags.Bridge)]
        [InlineData(false, 1, 1, false, true, false, RoadFlags.Tunnel)]
        [InlineData(false, 1, 1, false, false, true, RoadFlags.PedestrianOnly)]
        [InlineData(true, 0, 2, true, true, true, RoadFlags.Highway | RoadFlags.OneWay | RoadFlags.Bridge | RoadFlags.Tunnel | RoadFlags.PedestrianOnly)]
        public void Flags_combines_inputs(bool highway, int fwd, int bwd, bool bridge, bool tunnel, bool ped, RoadFlags expected)
        {
            Assert.Equal(expected, LaneCounter.Flags(highway, fwd, bwd, bridge, tunnel, ped));
        }
    }
}
