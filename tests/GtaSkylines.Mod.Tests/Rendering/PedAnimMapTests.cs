using System;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Mod.Rendering;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class PedAnimMapTests
    {
        [Theory]
        [InlineData(PedAnim.Idle)]
        [InlineData(PedAnim.HandsUp)]
        [InlineData(PedAnim.Phone)]
        public void Static_poses_are_default_and_still(PedAnim a)
        {
            var p = PedAnimMap.Map(a, 5f);
            Assert.Equal(PedAnimMap.StateDefault, p.Cs1State);
            Assert.Equal(0f, p.Speed, 4);
            Assert.False(p.LieOnBack);
        }

        [Theory]
        [InlineData(0.1f, 0.5f)]
        [InlineData(1.5f, 1.5f)]
        [InlineData(9f, 3.0f)]
        [InlineData(-4f, 0.5f)]
        [InlineData(float.NaN, 0.5f)]
        public void Walk_clamps_speed(float speed, float expected)
        {
            var p = PedAnimMap.Map(PedAnim.Walk, speed);
            Assert.Equal(PedAnimMap.StateDefault, p.Cs1State);
            Assert.Equal(expected, p.Speed, 4);
        }

        [Theory]
        [InlineData(PedAnim.Run)]
        [InlineData(PedAnim.Sprint)]
        public void Run_and_sprint_have_minimum_speed(PedAnim a)
        {
            Assert.Equal(3.3f, PedAnimMap.Map(a, 1f).Speed, 4);
            Assert.Equal(6f, PedAnimMap.Map(a, 6f).Speed, 4);
            Assert.Equal(3.3f, PedAnimMap.Map(a, float.NaN).Speed, 4);
            Assert.Equal(PedAnimMap.StateDefault, PedAnimMap.Map(a, 6f).Cs1State);
        }

        [Fact]
        public void Panic_passes_speed_through()
        {
            var p = PedAnimMap.Map(PedAnim.Panic, 4.5f);
            Assert.Equal(PedAnimMap.StatePanic, p.Cs1State);
            Assert.Equal(4.5f, p.Speed, 4);
            Assert.Equal(0f, PedAnimMap.Map(PedAnim.Panic, -2f).Speed, 4);
            Assert.Equal(0f, PedAnimMap.Map(PedAnim.Panic, float.NaN).Speed, 4);
        }

        [Fact]
        public void Sitting_uses_sitting_state()
        {
            var p = PedAnimMap.Map(PedAnim.Sitting, 3f);
            Assert.Equal(PedAnimMap.StateSitting, p.Cs1State);
            Assert.Equal(0f, p.Speed, 4);
        }

        [Fact]
        public void Only_lying_lies_on_back()
        {
            foreach (PedAnim a in Enum.GetValues(typeof(PedAnim)))
                Assert.Equal(a == PedAnim.Lying, PedAnimMap.Map(a, 2f).LieOnBack);
            var p = PedAnimMap.Map(PedAnim.Lying, 2f);
            Assert.Equal(PedAnimMap.StateDefault, p.Cs1State);
            Assert.Equal(0f, p.Speed, 4);
        }

        [Fact]
        public void Every_anim_maps_to_a_known_state()
        {
            foreach (PedAnim a in Enum.GetValues(typeof(PedAnim)))
                Assert.InRange(PedAnimMap.Map(a, 1f).Cs1State, 0, 2);
        }

        [Fact]
        public void Thresholds_are_published()
        {
            Assert.Equal(0.1f, PedAnimMap.WalkThreshold, 4);
            Assert.Equal(3.2f, PedAnimMap.SprintThreshold, 4);
        }

        [Fact]
        public void LyingRotation_zero_yaw()
        {
            var q = PedAnimMap.LyingRotation(0f);
            MeshAsserts.Near(new Vec3(0, 0, -1), q * new Vec3(0, 1, 0));
            MeshAsserts.Near(new Vec3(0, 1, 0), q * new Vec3(0, 0, 1));
        }

        [Fact]
        public void LyingRotation_yaw_90_head_points_away_from_facing()
        {
            MeshAsserts.Near(new Vec3(-1, 0, 0), PedAnimMap.LyingRotation(90f) * new Vec3(0, 1, 0));
        }
    }

    public class PedInstanceIdsTests
    {
        [Fact]
        public void Type_byte_is_ours_and_above_cs1_types()
        {
            Assert.Equal(0xF0u, PedInstanceIds.OurType);
            Assert.Equal(0xF0u, PedInstanceIds.Raw(0) >> 24);
            Assert.True((PedInstanceIds.Raw(5) >> 24) > 23u);
        }

        [Fact]
        public void Index_bits_are_never_zero_and_ids_are_distinct()
        {
            var seen = new System.Collections.Generic.HashSet<uint>();
            for (int s = 0; s < PedInstanceIds.MaxSlots; s++)
            {
                uint raw = PedInstanceIds.Raw(s);
                Assert.NotEqual(0u, raw & 0xFFFFFFu);
                Assert.True(seen.Add(raw));
            }
            Assert.Equal(4096, PedInstanceIds.MaxSlots);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(4096)]
        [InlineData(int.MaxValue)]
        public void Raw_rejects_out_of_range_slots(int slot)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PedInstanceIds.Raw(slot));
        }
    }
}
