using System.Collections.Generic;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.City
{
    /// <summary>
    /// Read-only queries the Game makes about the city. Implemented by the Mod over CS1's managers (main thread) and
    /// by fakes in tests. Methods that fill a list clear it first. Nothing here may throw for an unknown id: it returns
    /// false, null or an empty list instead.
    /// </summary>
    public interface ICity
    {
        /// <summary>Terrain height (not roads or buildings) at a point; 0 outside the map.</summary>
        float GroundHeight(float x, float z);

        /// <summary>True with the water surface height where CS1 has water above the terrain at the point.</summary>
        bool TryWaterSurface(float x, float z, out float surfaceY);

        /// <summary>CS1's time of day, hours in [0, 24).</summary>
        float TimeOfDayHours { get; }

        /// <summary>CS1 district id at a point (0 = no district).</summary>
        byte DistrictAt(float x, float z);

        /// <summary>District name; the city's name for district 0.</summary>
        string DistrictName(byte district);

        /// <summary>Name of the named road nearest to a point within <paramref name="radius"/> m, or null.</summary>
        string StreetNameNear(Vec3 point, float radius);

        /// <summary>A building by id; false when it does not exist.</summary>
        bool TryGetBuilding(ushort id, out BuildingDescriptor building);

        /// <summary>Ids of buildings whose lot is within <paramref name="radius"/> m of a point, nearest first.</summary>
        void BuildingsNear(Vec3 point, float radius, List<ushort> into);

        /// <summary>Ids of every working building of a service in the city.</summary>
        void ServiceBuildings(ServiceKind service, List<ushort> into);

        /// <summary>Citizens CS1 has inside a building right now.</summary>
        void Occupants(ushort building, List<Occupant> into);

        /// <summary>A citizen's identity and state; false when the citizen does not exist.</summary>
        bool TryGetCitizenProfile(uint citizen, out CitizenProfile profile);

        /// <summary>Sick or injured citizens waiting in buildings within <paramref name="radius"/> m, nearest first.</summary>
        void Patients(Vec3 point, float radius, List<Patient> into);

        /// <summary>Burning buildings within <paramref name="radius"/> m, nearest first.</summary>
        void BurningBuildings(Vec3 point, float radius, List<ushort> into);

        /// <summary>A vehicle model by id, or null.</summary>
        VehicleModel GetVehicleModel(int id);

        /// <summary>Some vehicle model of a kind that exists in this game (seeded choice), or -1 when there is none.</summary>
        int FindVehicleModel(VehicleKind kind, uint seed);

        /// <summary>A citizen model by id, or null.</summary>
        CitizenModel GetCitizenModel(int id);

        /// <summary>Number of citizen models; ids are 0 .. count − 1.</summary>
        int CitizenModelCount { get; }

        /// <summary>The city's name.</summary>
        string CityName { get; }
    }

    /// <summary>
    /// Per-frame snapshot of the CS1 actors near the player (within about 150 m), filled by the Mod before each
    /// <c>GameSession</c> tick. Lists are reused between frames.
    /// </summary>
    public sealed class CitySnapshot
    {
        /// <summary>Walking CS1 citizens near the player.</summary>
        public readonly List<AmbientCitizen> Citizens = new List<AmbientCitizen>();

        /// <summary>CS1 road vehicles (moving and parked) near the player.</summary>
        public readonly List<AmbientVehicle> Vehicles = new List<AmbientVehicle>();

        /// <summary>Centre the snapshot was taken around.</summary>
        public Vec3 Center;

        /// <summary>Radius covered, metres.</summary>
        public float Radius = 150f;

        /// <summary>Clears both lists.</summary>
        public void Clear()
        {
            Citizens.Clear();
            Vehicles.Clear();
        }
    }
}
