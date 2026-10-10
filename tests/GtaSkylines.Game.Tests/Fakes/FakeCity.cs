using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.Tests.Fakes
{
    /// <summary>
    /// Settable <see cref="ICity"/> for tests: flat ground at <see cref="Ground"/>, optional water, and whatever
    /// buildings, citizens and models a test adds. Shared by every test file; extend it, do not fork it.
    /// </summary>
    public sealed class FakeCity : ICity
    {
        public float Ground;
        public float? WaterLevel;
        public float Hours = 12f;
        public string Name = "Testville";
        public readonly Dictionary<ushort, BuildingDescriptor> Buildings = new Dictionary<ushort, BuildingDescriptor>();
        public readonly Dictionary<uint, CitizenProfile> Profiles = new Dictionary<uint, CitizenProfile>();
        public readonly Dictionary<ushort, List<Occupant>> OccupantsByBuilding = new Dictionary<ushort, List<Occupant>>();
        public readonly List<Patient> PatientList = new List<Patient>();
        public readonly Dictionary<int, VehicleModel> VehicleModels = new Dictionary<int, VehicleModel>();
        public readonly List<CitizenModel> CitizenModels = new List<CitizenModel>();
        public readonly Dictionary<byte, string> Districts = new Dictionary<byte, string>();
        /// <summary>Optional district function; null means district 0 everywhere.</summary>
        public System.Func<float, float, byte> DistrictFunc;
        /// <summary>Optional street name function; null means no streets.</summary>
        public System.Func<Vec3, string> StreetFunc;

        public FakeCity()
        {
            for (int i = 0; i < 8; i++) CitizenModels.Add(new CitizenModel { Id = i, Name = "Citizen" + i, Female = i % 2 == 1, AgeGroup = 2 + i % 3 });
            AddVehicleModel(new VehicleModel { Id = 0, Name = "Sedan", Kind = VehicleKind.Car });
            AddVehicleModel(new VehicleModel { Id = 1, Name = "Police Car", Kind = VehicleKind.Police, HasSiren = true, MaxSpeed = 40f });
            AddVehicleModel(new VehicleModel { Id = 2, Name = "Ambulance", Kind = VehicleKind.Ambulance, HasSiren = true, HalfExtents = new Vec3(1.1f, 1.3f, 3f) });
            AddVehicleModel(new VehicleModel { Id = 3, Name = "Fire Engine", Kind = VehicleKind.FireTruck, HasSiren = true, HalfExtents = new Vec3(1.2f, 1.6f, 4.5f) });
            AddVehicleModel(new VehicleModel { Id = 4, Name = "Taxi", Kind = VehicleKind.Taxi });
            AddVehicleModel(new VehicleModel { Id = 5, Name = "Bus", Kind = VehicleKind.Bus, HalfExtents = new Vec3(1.3f, 1.6f, 6f), MaxSpeed = 20f, Seats = 30 });
            AddVehicleModel(new VehicleModel { Id = 6, Name = "Truck", Kind = VehicleKind.Truck, HalfExtents = new Vec3(1.2f, 1.7f, 4f), MaxSpeed = 22f, Seats = 2 });
        }

        public void AddVehicleModel(VehicleModel m) { VehicleModels[m.Id] = m; }

        public void AddBuilding(BuildingDescriptor b) { Buildings[b.Id] = b; }

        public float GroundHeight(float x, float z) { return Ground; }

        public bool TryWaterSurface(float x, float z, out float surfaceY)
        {
            surfaceY = WaterLevel ?? 0f;
            return WaterLevel.HasValue && WaterLevel.Value > Ground;
        }

        public float TimeOfDayHours { get { return Hours; } }

        public byte DistrictAt(float x, float z) { return DistrictFunc == null ? (byte)0 : DistrictFunc(x, z); }

        public string DistrictName(byte district)
        {
            string n;
            return district == 0 ? Name : Districts.TryGetValue(district, out n) ? n : "District " + district;
        }

        public string StreetNameNear(Vec3 point, float radius) { return StreetFunc == null ? null : StreetFunc(point); }

        public bool TryGetBuilding(ushort id, out BuildingDescriptor building) { return Buildings.TryGetValue(id, out building); }

        public void BuildingsNear(Vec3 point, float radius, List<ushort> into)
        {
            into.Clear();
            var found = new List<KeyValuePair<float, ushort>>();
            foreach (var kv in Buildings)
            {
                float d = Vec3.DistanceXZ(kv.Value.Position, point);
                if (d <= radius) found.Add(new KeyValuePair<float, ushort>(d, kv.Key));
            }
            found.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : a.Value.CompareTo(b.Value));
            foreach (var f in found) into.Add(f.Value);
        }

        public void ServiceBuildings(ServiceKind service, List<ushort> into)
        {
            into.Clear();
            foreach (var kv in Buildings) if (kv.Value.Service == service && !kv.Value.Abandoned) into.Add(kv.Key);
            into.Sort();
        }

        public void Occupants(ushort building, List<Occupant> into)
        {
            into.Clear();
            List<Occupant> list;
            if (OccupantsByBuilding.TryGetValue(building, out list)) into.AddRange(list);
        }

        public bool TryGetCitizenProfile(uint citizen, out CitizenProfile profile) { return Profiles.TryGetValue(citizen, out profile); }

        public void Patients(Vec3 point, float radius, List<Patient> into)
        {
            into.Clear();
            foreach (var p in PatientList) if (Vec3.DistanceXZ(p.Pickup, point) <= radius) into.Add(p);
            into.Sort((a, b) => Vec3.DistanceXZ(a.Pickup, point).CompareTo(Vec3.DistanceXZ(b.Pickup, point)));
        }

        public void BurningBuildings(Vec3 point, float radius, List<ushort> into)
        {
            into.Clear();
            foreach (var kv in Buildings) if (kv.Value.Burning && Vec3.DistanceXZ(kv.Value.Position, point) <= radius) into.Add(kv.Key);
            into.Sort((a, b) => Vec3.DistanceXZ(Buildings[a].Position, point).CompareTo(Vec3.DistanceXZ(Buildings[b].Position, point)));
        }

        public VehicleModel GetVehicleModel(int id)
        {
            VehicleModel m;
            return VehicleModels.TryGetValue(id, out m) ? m : null;
        }

        public int FindVehicleModel(VehicleKind kind, uint seed)
        {
            var ids = new List<int>();
            foreach (var kv in VehicleModels) if (kv.Value.Kind == kind) ids.Add(kv.Key);
            if (ids.Count == 0) return -1;
            ids.Sort();
            return ids[(int)(seed % (uint)ids.Count)];
        }

        public CitizenModel GetCitizenModel(int id) { return id >= 0 && id < CitizenModels.Count ? CitizenModels[id] : null; }

        public int CitizenModelCount { get { return CitizenModels.Count; } }

        public string CityName { get { return Name; } }
    }
}
