using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Profile;

namespace GtaSkylines.Game.Session
{
    /// <summary>Where a play session starts when the player asks for "the last safehouse" (F-MODE-1).</summary>
    public static class SpawnPoint
    {
        /// <summary>
        /// The ground position at the first door of <c>profile.LastSafehouse</c> (the building's position when it has no
        /// door) and the yaw facing out of the building. False when the profile has no safehouse or the city no longer
        /// has that building; the outputs are then zero.
        /// </summary>
        public static bool TryLastSafehouse(PlayerProfile profile, ICity city, out Vec3 feet, out float yawDeg)
        {
            feet = default(Vec3);
            yawDeg = 0f;
            if (profile == null || city == null) return false;
            uint id = profile.LastSafehouse;
            BuildingDescriptor b;
            if (id == 0 || id > ushort.MaxValue || !city.TryGetBuilding((ushort)id, out b)) return false;
            bool hasDoor = b.Doors != null && b.Doors.Length > 0;
            feet = hasDoor ? b.Doors[0] : b.Position;
            yawDeg = b.YawDeg;
            return true;
        }
    }
}
