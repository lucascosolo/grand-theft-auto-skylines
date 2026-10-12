using System.Collections.Generic;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Mod.City;
using Xunit;

namespace GtaSkylines.Mod.Tests.City
{
    public class LaneBlockersTests
    {
        private static LaneBlocker Rec(int tag, int segment = 7, ushort vehicle = 0)
        {
            return new LaneBlocker { Tag = tag, Pivot = new Vec3(1f, 2f, 3f), YawDeg = 20f, Model = 4, Segment = segment, Vehicle = vehicle };
        }

        [Fact]
        public void Add_CountsRecords()
        {
            var b = new LaneBlockers();
            Assert.Equal(0, b.Count);
            b.Add(Rec(1));
            b.Add(Rec(2));
            Assert.Equal(2, b.Count);
        }

        [Fact]
        public void Add_SameTag_Replaces()
        {
            var b = new LaneBlockers();
            b.Add(Rec(1, segment: 7));
            b.Add(Rec(1, segment: 9));
            Assert.Equal(1, b.Count);
            LaneBlocker r;
            Assert.True(b.Remove(1, out r));
            Assert.Equal(9, r.Segment);
        }

        [Fact]
        public void Remove_ReturnsTheRecord_AndFalseForAnUnknownTag()
        {
            var b = new LaneBlockers();
            b.Add(Rec(5));
            LaneBlocker r;
            Assert.False(b.Remove(6, out r));
            Assert.True(b.Remove(5, out r));
            Assert.Equal(5, r.Tag);
            Assert.Equal(7, r.Segment);
            Assert.Equal(20f, r.YawDeg);
            Assert.Equal(0, b.Count);
        }

        [Fact]
        public void IsBlocker_TrueAfterSetVehicle_FalseForZeroAndAfterRemove()
        {
            var b = new LaneBlockers();
            b.Add(Rec(1));
            Assert.False(b.IsBlocker(0));
            Assert.False(b.IsBlocker(55));
            b.SetVehicle(1, 55, true);
            Assert.True(b.IsBlocker(55));
            Assert.False(b.IsBlocker(0));
            LaneBlocker r;
            Assert.True(b.Remove(1, out r));
            Assert.Equal((ushort)55, r.Vehicle);
            Assert.True(r.TrafficOnly);
            Assert.False(b.IsBlocker(55));
        }

        [Fact]
        public void SetVehicle_UnknownTag_IsANoOp()
        {
            var b = new LaneBlockers();
            b.SetVehicle(3, 40, false);
            Assert.Equal(0, b.Count);
            Assert.False(b.IsBlocker(40));
        }

        [Fact]
        public void CopyTo_ClearsTheListAndFillsIt()
        {
            var b = new LaneBlockers();
            b.Add(Rec(1));
            b.Add(Rec(2));
            var list = new List<LaneBlocker> { Rec(99) };
            b.CopyTo(list);
            Assert.Equal(2, list.Count);
            Assert.DoesNotContain(list, r => r.Tag == 99);
            Assert.Contains(list, r => r.Tag == 1);
            Assert.Contains(list, r => r.Tag == 2);
        }

        [Fact]
        public void DetachAll_ListsVehicles_KeepsRecordsWithVehicleZero()
        {
            var b = new LaneBlockers();
            b.Add(Rec(1)); b.Add(Rec(2)); b.Add(Rec(3));
            b.SetVehicle(1, 10, false);
            b.SetVehicle(2, 20, false);
            var vehicles = new List<ushort> { 77 };
            b.DetachAll(vehicles);
            Assert.Equal(2, vehicles.Count);
            Assert.Contains((ushort)10, vehicles);
            Assert.Contains((ushort)20, vehicles);
            Assert.Equal(3, b.Count);
            Assert.False(b.IsBlocker(10));
            var recs = new List<LaneBlocker>();
            b.CopyTo(recs);
            Assert.All(recs, r => Assert.Equal((ushort)0, r.Vehicle));
        }

        [Fact]
        public void Clear_ListsVehicles_AndEmpties()
        {
            var b = new LaneBlockers();
            b.Add(Rec(1)); b.Add(Rec(2));
            b.SetVehicle(1, 10, false);
            var vehicles = new List<ushort> { 77 };
            b.Clear(vehicles);
            Assert.Equal(new List<ushort> { 10 }, vehicles);
            Assert.Equal(0, b.Count);
            Assert.False(b.IsBlocker(10));
        }
    }
}
