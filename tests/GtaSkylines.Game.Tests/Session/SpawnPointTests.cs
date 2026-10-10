using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Session;
using GtaSkylines.Game.Tests.Fakes;
using Xunit;

namespace GtaSkylines.Game.Tests.SessionTests
{
    public class SpawnPointTests
    {
        private static BuildingDescriptor House(ushort id, params Vec3[] doors)
        {
            return new BuildingDescriptor { Id = id, Position = new Vec3(10f, 2f, 20f), YawDeg = 90f, Doors = doors };
        }

        [Fact]
        public void OwnedLastSafehouse_SpawnsAtItsFirstDoorFacingOut()
        {
            var city = new FakeCity();
            city.AddBuilding(House(7, new Vec3(1f, 0f, 2f), new Vec3(5f, 0f, 6f)));
            var p = new PlayerProfile();
            p.Properties.Add(7);
            p.LastSafehouse = 7;
            Vec3 feet; float yaw;
            Assert.True(SpawnPoint.TryLastSafehouse(p, city, out feet, out yaw));
            Assert.Equal(new Vec3(1f, 0f, 2f), feet);
            Assert.Equal(90f, yaw);
        }

        [Fact]
        public void SafehouseWithoutDoors_UsesItsPosition()
        {
            var city = new FakeCity();
            city.AddBuilding(House(7));
            var p = new PlayerProfile { LastSafehouse = 7 };
            Vec3 feet; float yaw;
            Assert.True(SpawnPoint.TryLastSafehouse(p, city, out feet, out yaw));
            Assert.Equal(new Vec3(10f, 2f, 20f), feet);
        }

        [Fact]
        public void NoSafehouse_FallsBack()
        {
            var city = new FakeCity();
            city.AddBuilding(House(7, new Vec3(1f, 0f, 2f)));
            Vec3 feet; float yaw;
            Assert.False(SpawnPoint.TryLastSafehouse(new PlayerProfile(), city, out feet, out yaw));
            Assert.False(SpawnPoint.TryLastSafehouse(null, city, out feet, out yaw));
        }

        [Fact]
        public void SafehouseMissingFromTheCity_FallsBack()
        {
            var p = new PlayerProfile { LastSafehouse = 9 };
            Vec3 feet; float yaw;
            Assert.False(SpawnPoint.TryLastSafehouse(p, new FakeCity(), out feet, out yaw));
        }
    }
}
