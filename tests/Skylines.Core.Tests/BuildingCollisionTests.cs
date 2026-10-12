using System;
using Skylines.Core.Geometry;
using Xunit;

namespace Skylines.Core.Tests
{
    public class BuildingCollisionTests
    {
        private const uint Created = BuildingCollision.FlagCreated;
        private const uint Completed = BuildingCollision.FlagCompleted;

        private static void Near(double expected, float actual)
        {
            Assert.True(Math.Abs(expected - actual) <= 1e-3, "expected " + expected + " but was " + actual);
        }

        [Fact]
        public void FlagConstantsMatchCs1()
        {
            Assert.Equal(1u, BuildingCollision.FlagCreated);
            Assert.Equal(2u, BuildingCollision.FlagDeleted);
            Assert.Equal(0x10000u, BuildingCollision.FlagCompleted);
            Assert.Equal(0x400000u, BuildingCollision.FlagCollapsed);
            Assert.Equal(0x800000u, BuildingCollision.FlagUpgrading);
            Assert.Equal(0x8000000u, BuildingCollision.FlagHidden);
        }

        [Fact]
        public void CreatedOnlyAtStateZeroDrawsNothing()
        {
            Near(0, BuildingCollision.HeightScale(Created, 0, 20f, 30));
        }

        [Theory]
        [InlineData((byte)255)]
        [InlineData((byte)0)]
        public void CompletedDrawsFullHeightWhateverTheConstructState(byte state)
        {
            Near(1, BuildingCollision.HeightScale(Created | Completed, state, 20f, 30));
        }

        [Fact]
        public void NotCreatedDrawsNothing()
        {
            Near(0, BuildingCollision.HeightScale(0, 128, 20f, 30));
        }

        [Theory]
        [InlineData((byte)16, 0.026471)]
        [InlineData((byte)32, 0.202941)]
        [InlineData((byte)64, 0.555882)]
        [InlineData((byte)96, 0.908824)]
        [InlineData((byte)128, 1.0)]
        public void RisingAndHeightScaleFollowCs1Formula(byte s, double expected)
        {
            Near(expected, BuildingCollision.RisingScale(s, 20f, 20f, 30));
            Near(expected, BuildingCollision.HeightScale(Created, s, 20f, 30));
        }

        [Fact]
        public void RisingScaleUsesTallestForStepSize()
        {
            Near(0.558824, BuildingCollision.RisingScale(64, 120f, 120f, 30));
        }

        [Theory]
        [InlineData((byte)0, 1.0)]
        [InlineData((byte)64, 1.0)]
        [InlineData((byte)96, 0.517647)]
        [InlineData((byte)128, 0.023529)]
        [InlineData((byte)160, 0.0)]
        public void FallingScaleFollowsCs1Formula(byte s, double expected)
        {
            Near(expected, BuildingCollision.FallingScale(s, 20f, 30f, 30));
        }

        [Fact]
        public void ZeroHeightDrawsNothing()
        {
            Near(0, BuildingCollision.HeightScale(Created, 128, 0f, 30));
            Near(0, BuildingCollision.RisingScale(128, 0f, 0f, 30));
            Near(0, BuildingCollision.FallingScale(128, 0f, 0f, 30));
        }

        [Fact]
        public void RisingIsNonDecreasingAndFallingNonIncreasing()
        {
            float prevUp = BuildingCollision.RisingScale(0, 20f, 20f, 30);
            float prevDown = BuildingCollision.FallingScale(0, 20f, 20f, 30);
            for (int s = 1; s <= 255; s++)
            {
                float up = BuildingCollision.RisingScale((byte)s, 20f, 20f, 30);
                float down = BuildingCollision.FallingScale((byte)s, 20f, 20f, 30);
                Assert.True(up >= prevUp, "rising dropped at " + s);
                Assert.True(down <= prevDown, "falling rose at " + s);
                prevUp = up;
                prevDown = down;
            }
        }

        [Fact]
        public void SignatureIsStableForEqualInputs()
        {
            Assert.Equal(BuildingCollision.Signature(7, Created | Completed, 255),
                BuildingCollision.Signature(7, Created | Completed, 255));
        }

        [Fact]
        public void SignatureChangesWithInfoIndex()
        {
            Assert.NotEqual(BuildingCollision.Signature(7, Created, 0), BuildingCollision.Signature(8, Created, 0));
        }

        [Fact]
        public void SignatureChangesWhenCompletedToggles()
        {
            Assert.NotEqual(BuildingCollision.Signature(7, Created, 0),
                BuildingCollision.Signature(7, Created | Completed, 0));
        }

        [Fact]
        public void SignatureChangesWhenUpgradingToggles()
        {
            Assert.NotEqual(BuildingCollision.Signature(7, Created, 0),
                BuildingCollision.Signature(7, Created | BuildingCollision.FlagUpgrading, 0));
        }

        [Fact]
        public void SignatureChangesWithConstructState()
        {
            Assert.NotEqual(BuildingCollision.Signature(7, Created, 10), BuildingCollision.Signature(7, Created, 11));
        }

        [Theory]
        [InlineData(0x20000u)]
        [InlineData(0x40000u)]
        public void SignatureIgnoresUnrelatedFlags(uint other)
        {
            Assert.Equal(BuildingCollision.Signature(7, Created | Completed, 255),
                BuildingCollision.Signature(7, Created | Completed | other, 255));
        }
    }
}
