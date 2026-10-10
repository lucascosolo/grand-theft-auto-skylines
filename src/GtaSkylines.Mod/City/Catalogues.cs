using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using Skylines.Host;
using UnityEngine;

namespace GtaSkylines.Mod.City
{
    /// <summary>
    /// The vehicle and citizen models of the loaded city, built once per load from <c>PrefabCollection</c>: drivable road
    /// vehicles (and police helicopters) and human citizens, with stable ids for the session and reverse maps from
    /// prefab to id. Main thread only.
    /// </summary>
    public sealed class Catalogues
    {
        /// <summary>m/s per CS1 vehicle speed unit: lane limit 1.0 (50 km/h) is vehicle speed 8 (VehicleAI.cs:916).</summary>
        public const float VehicleUnitMs = CityMath.Cs1SpeedUnitMs / 8f;
        // Frame quantities are per 1/3.75 s, as Vehicle.GetSmoothVelocity scales them (Vehicle.cs:903).
        private const float FrameRate = 3.75f;

        private readonly List<VehicleModel> _vehicles = new List<VehicleModel>();
        private readonly List<CitizenModel> _citizens = new List<CitizenModel>();
        private readonly Dictionary<VehicleInfo, int> _vehicleIds = new Dictionary<VehicleInfo, int>();
        private readonly Dictionary<CitizenInfo, int> _citizenIds = new Dictionary<CitizenInfo, int>();
        private readonly List<int>[] _byKind;

        /// <summary>Reads every loaded vehicle and citizen prefab.</summary>
        public Catalogues(HostLog log)
        {
            _byKind = new List<int>[Enum.GetValues(typeof(VehicleKind)).Length];
            for (int i = 0; i < _byKind.Length; i++) _byKind[i] = new List<int>();
            int n = PrefabCollection<VehicleInfo>.LoadedCount();
            for (int i = 0; i < n; i++) AddVehicle(PrefabCollection<VehicleInfo>.GetLoaded((uint)i));
            n = PrefabCollection<CitizenInfo>.LoadedCount();
            for (int i = 0; i < n; i++) AddCitizen(PrefabCollection<CitizenInfo>.GetLoaded((uint)i));
            log.Info("catalogues: " + _vehicles.Count + " vehicle models, " + _citizens.Count + " citizen models");
        }

        /// <summary>Vehicle model count; ids are 0 .. count − 1.</summary>
        public int VehicleCount { get { return _vehicles.Count; } }

        /// <summary>Citizen model count; ids are 0 .. count − 1.</summary>
        public int CitizenCount { get { return _citizens.Count; } }

        /// <summary>A vehicle model, or null.</summary>
        public VehicleModel Vehicle(int id) { return id >= 0 && id < _vehicles.Count ? _vehicles[id] : null; }

        /// <summary>A citizen model, or null.</summary>
        public CitizenModel Citizen(int id) { return id >= 0 && id < _citizens.Count ? _citizens[id] : null; }

        /// <summary>Model id of a prefab, -1 when it is not a catalogued road vehicle.</summary>
        public int VehicleId(VehicleInfo info)
        {
            int id;
            return info != null && _vehicleIds.TryGetValue(info, out id) ? id : -1;
        }

        /// <summary>Model id of a prefab, -1 when it is not a catalogued human.</summary>
        public int CitizenId(CitizenInfo info)
        {
            int id;
            return info != null && _citizenIds.TryGetValue(info, out id) ? id : -1;
        }

        /// <summary>A seeded model of a kind, or -1.</summary>
        public int Find(VehicleKind kind, uint seed)
        {
            int k = (int)kind;
            if (k < 0 || k >= _byKind.Length || _byKind[k].Count == 0) return -1;
            return _byKind[k][(int)(seed % (uint)_byKind[k].Count)];
        }

        private void AddVehicle(VehicleInfo info)
        {
            if (info == null || info.m_vehicleAI == null || _vehicleIds.ContainsKey(info)) return;
            bool road = info.m_vehicleType == VehicleInfo.VehicleType.Car && !info.m_isLargeVehicle;
            Bounds b = info.m_mesh != null ? info.m_mesh.bounds : new Bounds();
            bool hasBounds = b.size.x > 0.1f && b.size.y > 0.1f && b.size.z > 0.1f;
            Vector3 size = hasBounds ? b.size : (info.m_generatedInfo != null ? info.m_generatedInfo.m_size : new Vector3(1.8f, 1.5f, 4.4f));
            VehicleKind kind = VehicleKind.Car;
            bool found = false;
            for (Type t = info.m_vehicleAI.GetType(); t != null && !found; t = t.BaseType) found = VehicleKinds.TryClassify(t.Name, size.z, out kind);
            if (!found || (!road && kind != VehicleKind.PoliceHelicopter)) return;
            var m = new VehicleModel
            {
                Id = _vehicles.Count,
                Name = info.name ?? "",
                Kind = kind,
                HalfExtents = new Vec3(size.x * 0.5f, size.y * 0.5f, size.z * 0.5f),
                CenterOffset = hasBounds ? new Vec3(b.center.x, b.center.y, b.center.z) : new Vec3(0f, size.y * 0.5f, 0f),
                MaxSpeed = info.m_maxSpeed * VehicleUnitMs,
                Acceleration = info.m_acceleration * VehicleUnitMs * FrameRate,
                Braking = info.m_braking * VehicleUnitMs * FrameRate,
                HasSiren = VehicleKinds.HasSiren(kind),
                Seats = kind == VehicleKind.Bus ? 30 : (kind == VehicleKind.Truck || kind == VehicleKind.FireTruck || kind == VehicleKind.Garbage ? 2 : 4),
            };
            _vehicleIds[info] = m.Id;
            _vehicles.Add(m);
            _byKind[(int)kind].Add(m.Id);
        }

        private void AddCitizen(CitizenInfo info)
        {
            if (info == null || _citizenIds.ContainsKey(info)) return;
            CitizenAI ai = info.m_citizenAI;
            if (!(ai is ResidentAI || ai is TouristAI || ai is ServicePersonAI)) return;
            var m = new CitizenModel
            {
                Id = _citizens.Count,
                Name = info.name ?? "",
                Female = info.m_gender == global::Citizen.Gender.Female,
                AgeGroup = CityMath.AgeGroupOfPhase(info.m_agePhase.ToString()),
            };
            _citizenIds[info] = m.Id;
            _citizens.Add(m);
        }
    }
}
