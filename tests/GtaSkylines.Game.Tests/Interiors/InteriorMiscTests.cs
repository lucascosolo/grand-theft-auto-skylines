using System;
using System.Diagnostics;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;
using Xunit;
using static GtaSkylines.Game.Tests.Interiors.InteriorTestSupport;

namespace GtaSkylines.Game.Tests.Interiors
{
    public class InteriorMiscTests
    {
        [Theory, MemberData(nameof(InteriorTestSupport.AllArchetypes), MemberType = typeof(InteriorTestSupport))]
        public void RoomAtMatchesRoomBoundsAndIsMinusOneOutside(InteriorArchetype a)
        {
            foreach (InteriorLayout l in Layouts(a))
            {
                for (int i = 0; i < l.Rooms.Count; i++)
                {
                    Vec3 c = l.Rooms[i].Bounds.Center;
                    Assert.Equal(i, l.RoomAt(c.X, c.Z));
                }
                for (float x = -l.Width / 2f + 0.13f; x < l.Width / 2f; x += 0.97f)
                    for (float z = 0.11f; z < l.Depth; z += 0.89f)
                    {
                        int expected = -1;
                        for (int i = 0; i < l.Rooms.Count; i++) if (l.Rooms[i].Bounds.Contains(x, z)) expected = i;
                        Assert.NotEqual(-1, expected);
                        Assert.Equal(expected, l.RoomAt(x, z));
                    }
                Assert.Equal(-1, l.RoomAt(-l.Width / 2f - 1f, l.Depth / 2f));
                Assert.Equal(-1, l.RoomAt(l.Width / 2f + 1f, l.Depth / 2f));
                Assert.Equal(-1, l.RoomAt(0f, -1f));
                Assert.Equal(-1, l.RoomAt(0f, l.Depth + 1f));
            }
        }

        [Fact]
        public void YawNinetyMapsLocalPlusZToWorldPlusX()
        {
            Vec3 w = InteriorLayout.ToWorld(new Vec3(0f, 0f, 1f), Vec3.Zero, 90f);
            Assert.Equal(1f, w.X, 4);
            Assert.Equal(0f, w.Y, 4);
            Assert.Equal(0f, w.Z, 4);
            Vec3 e = InteriorLayout.ToWorld(new Vec3(0f, 0f, 1f), new Vec3(100f, 5f, -40f), 0f);
            Assert.Equal(100f, e.X, 4);
            Assert.Equal(5f, e.Y, 4);
            Assert.Equal(-39f, e.Z, 4);
        }

        [Fact]
        public void ToWorldAndToLocalRoundTrip()
        {
            var origin = new Vec3(123.5f, 17f, -456.25f);
            foreach (float yaw in new[] { 0f, 90f, 180f, 270f, 33.3f, -75f, 400f })
                foreach (var p in new[] { new Vec3(0f, 0f, 0f), new Vec3(3.5f, 1.2f, 7.75f), new Vec3(-19f, 3f, 39f) })
                {
                    Vec3 back = InteriorLayout.ToLocal(InteriorLayout.ToWorld(p, origin, yaw), origin, yaw);
                    Assert.True(Vec3.Distance(back, p) < 1e-3f, "yaw " + yaw + " " + p + " -> " + back);
                    Vec3 forth = InteriorLayout.ToWorld(InteriorLayout.ToLocal(p, origin, yaw), origin, yaw);
                    Assert.True(Vec3.Distance(forth, p) < 1e-3f);
                }
        }

        [Fact]
        public void GenerateNeverThrowsForOddSizes()
        {
            foreach (float s in new[] { 0f, -1f, -1000f, 0.001f, 4f, 7.99f, 1000f, 100000f })
                foreach (InteriorArchetype a in AllArchetypeValues())
                {
                    InteriorLayout l = InteriorGenerator.Generate(Building(1234, ServiceFor(a), "", s, s, s), a, 2);
                    Assert.NotNull(l);
                    Assert.InRange(l.Width, 8f, 40f);
                    Assert.InRange(l.Depth, 8f, 40f);
                }
        }

        [Fact]
        public void WarehouseOfFortyByFortyGeneratesUnder200Ms()
        {
            AssertFast(InteriorArchetype.Warehouse, ServiceKind.Industrial);
        }

        [Fact]
        public void OfficeOfFortyByFortyGeneratesUnder200Ms()
        {
            AssertFast(InteriorArchetype.Office, ServiceKind.Office);
        }

        private static void AssertFast(InteriorArchetype a, ServiceKind s)
        {
            var b = Building(4242, s, "", 40f, 40f, 30f);
            InteriorGenerator.Generate(b, a, 0);
            long best = long.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                var sw = Stopwatch.StartNew();
                InteriorGenerator.Generate(b, a, 0);
                sw.Stop();
                best = Math.Min(best, sw.ElapsedMilliseconds);
            }
            Assert.True(best < 200, a + " took " + best + " ms");
        }
    }
}
