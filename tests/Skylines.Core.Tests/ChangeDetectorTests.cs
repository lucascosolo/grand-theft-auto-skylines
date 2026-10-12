using Skylines.Core.Geometry;
using Skylines.Core.Streaming;
using Xunit;

namespace Skylines.Core.Tests
{
    public class ChangeDetectorTests
    {
        private struct B
        {
            public ushort Info;
            public uint Flags;
            public byte State;
        }

        private static B[] Scene()
        {
            var a = new B[4];
            for (int i = 0; i < a.Length; i++)
            {
                a[i].Info = (ushort)(10 + i);
                a[i].Flags = BuildingCollision.FlagCreated;
                a[i].State = 0;
            }
            return a;
        }

        private static bool[] Sweep(ChangeDetector d, B[] a)
        {
            var changed = new bool[a.Length];
            for (int i = 0; i < a.Length; i++)
                changed[i] = d.Observe(i, BuildingCollision.Signature(a[i].Info, a[i].Flags, a[i].State));
            return changed;
        }

        private static void AssertOnly(bool[] changed, int expected)
        {
            for (int i = 0; i < changed.Length; i++)
                Assert.Equal(i == expected, changed[i]);
        }

        [Fact]
        public void FirstObservationReportsNothing()
        {
            var d = new ChangeDetector(4);
            AssertOnly(Sweep(d, Scene()), -1);
        }

        [Fact]
        public void UnchangedBuildingsReportNothingOverSeveralSweeps()
        {
            var d = new ChangeDetector(4);
            var a = Scene();
            for (int sweep = 0; sweep < 5; sweep++)
                AssertOnly(Sweep(d, a), -1);
        }

        [Fact]
        public void InfoIndexChangeIsReportedExactlyOnce()
        {
            var d = new ChangeDetector(4);
            var a = Scene();
            Sweep(d, a);
            a[2].Info = 99;
            AssertOnly(Sweep(d, a), 2);
            AssertOnly(Sweep(d, a), -1);
            AssertOnly(Sweep(d, a), -1);
        }

        [Fact]
        public void CompletedFlagChangeIsReportedExactlyOnce()
        {
            var d = new ChangeDetector(4);
            var a = Scene();
            Sweep(d, a);
            a[1].Flags |= BuildingCollision.FlagCompleted;
            AssertOnly(Sweep(d, a), 1);
            AssertOnly(Sweep(d, a), -1);
        }

        [Fact]
        public void ResetForgetsEverything()
        {
            var d = new ChangeDetector(4);
            var a = Scene();
            Sweep(d, a);
            d.Reset();
            a[0].Info = 77;
            AssertOnly(Sweep(d, a), -1);
            AssertOnly(Sweep(d, a), -1);
        }
    }
}
