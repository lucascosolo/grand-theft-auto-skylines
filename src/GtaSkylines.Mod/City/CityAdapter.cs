using System;
using System.Collections.Generic;
using ColossalFramework;
using ColossalFramework.Math;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using Skylines.Host;
using UnityEngine;

namespace GtaSkylines.Mod.City
{
    /// <summary>
    /// <see cref="ICity"/> over CS1's managers. Main thread only; never throws for unknown ids. Sources:
    /// docs/CS1-API-NOTES.md (M2).
    /// </summary>
    public sealed class CityAdapter : ICity
    {
        private const float MapHalf = 8640f;
        private const float BuildingCell = 64f;
        private const int BuildingGrid = 270;
        private const float SegmentCell = 64f;
        private const int SegmentGrid = 270;
        private const float StreetCell = 8f;
        private const int StreetCacheLimit = 4096;
        private const float ServiceRefreshSeconds = 10f;
        private const int ChainLimit = 65536;

        private readonly HostLog _log;
        private readonly Catalogues _catalogues;
        private readonly Dictionary<ushort, BuildingDescriptor> _buildings = new Dictionary<ushort, BuildingDescriptor>();
        private readonly Dictionary<long, string> _streets = new Dictionary<long, string>();
        private readonly List<ushort>[] _services;
        private readonly List<float> _dist = new List<float>();
        private readonly List<ushort> _near = new List<ushort>();
        private readonly List<Vec3> _doors = new List<Vec3>();
        private float _servicesAt = float.NegativeInfinity;

        /// <summary>Reads the city through CS1's managers; models from <paramref name="catalogues"/>.</summary>
        public CityAdapter(HostLog log, Catalogues catalogues)
        {
            _log = log;
            _catalogues = catalogues;
            _services = new List<ushort>[Enum.GetValues(typeof(ServiceKind)).Length];
            for (int i = 0; i < _services.Length; i++) _services[i] = new List<ushort>();
        }

        /// <inheritdoc/>
        public float GroundHeight(float x, float z)
        {
            if (!TerrainManager.exists || Math.Abs(x) > MapHalf || Math.Abs(z) > MapHalf) return 0f;
            return Singleton<TerrainManager>.instance.SampleDetailHeightSmooth(new Vector3(x, 0f, z));
        }

        /// <inheritdoc/>
        public bool TryWaterSurface(float x, float z, out float surfaceY)
        {
            surfaceY = 0f;
            if (!TerrainManager.exists || Math.Abs(x) > MapHalf || Math.Abs(z) > MapHalf) return false;
            var p = new Vector3(x, 0f, z);
            TerrainManager tm = Singleton<TerrainManager>.instance;
            surfaceY = tm.SampleRawHeightSmoothWithWater(p, true, 0f);
            return surfaceY > tm.SampleRawHeightSmooth(p) + 0.05f;
        }

        /// <inheritdoc/>
        public float TimeOfDayHours
        {
            get
            {
                if (!SimulationManager.exists) return 12f;
                float h = Singleton<SimulationManager>.instance.m_currentDayTimeHour;
                return h >= 0f && h < 24f ? h : 0f;
            }
        }

        /// <inheritdoc/>
        public byte DistrictAt(float x, float z)
        {
            return DistrictManager.exists ? Singleton<DistrictManager>.instance.GetDistrict(new Vector3(x, 0f, z)) : (byte)0;
        }

        /// <inheritdoc/>
        public string DistrictName(byte district)
        {
            if (district == 0 || !DistrictManager.exists) return CityName;
            return Singleton<DistrictManager>.instance.GetDistrictName(district) ?? "";
        }

        /// <inheritdoc/>
        public string CityName
        {
            get
            {
                SimulationMetaData meta = SimulationManager.exists ? Singleton<SimulationManager>.instance.m_metaData : null;
                return meta != null && meta.m_CityName != null ? meta.m_CityName : "";
            }
        }

        /// <inheritdoc/>
        public string StreetNameNear(Vec3 point, float radius)
        {
            if (!NetManager.exists) return null;
            int cx = (int)Math.Floor(point.X / StreetCell), cz = (int)Math.Floor(point.Z / StreetCell);
            long key = ((long)(cx & 0xFFFFF) << 40) | ((long)(cz & 0xFFFFF) << 20) | (long)(Math.Min(radius, 1000f) * 10f);
            string name;
            if (_streets.TryGetValue(key, out name)) return name;
            name = NearestStreet(new Vector3((cx + 0.5f) * StreetCell, point.Y, (cz + 0.5f) * StreetCell), radius);
            if (_streets.Count >= StreetCacheLimit) _streets.Clear();
            _streets[key] = name;
            return name;
        }

        /// <inheritdoc/>
        public bool TryGetBuilding(ushort id, out BuildingDescriptor building)
        {
            building = null;
            if (!BuildingManager.exists) return false;
            Building[] buf = Singleton<BuildingManager>.instance.m_buildings.m_buffer;
            if (id == 0 || id >= buf.Length || (buf[id].m_flags & (Building.Flags.Created | Building.Flags.Deleted)) != Building.Flags.Created) return false;
            BuildingInfo info = buf[id].Info;
            if (info == null) return false;
            BuildingDescriptor d;
            Vec3 pos = new Vec3(buf[id].m_position.x, buf[id].m_position.y, buf[id].m_position.z);
            if (!_buildings.TryGetValue(id, out d) || d.InfoName != info.name || d.Position != pos)
            {
                d = Describe(id, ref buf[id], info, pos);
                _buildings[id] = d;
            }
            d.Burning = buf[id].m_fireIntensity > 0;
            d.Abandoned = (buf[id].m_flags & (Building.Flags.Abandoned | Building.Flags.Collapsed)) != 0
                || (buf[id].m_flags & Building.Flags.Completed) == 0;
            building = d;
            return true;
        }

        /// <inheritdoc/>
        public void BuildingsNear(Vec3 point, float radius, List<ushort> into)
        {
            into.Clear();
            _dist.Clear();
            if (!BuildingManager.exists) return;
            BuildingManager bm = Singleton<BuildingManager>.instance;
            Building[] buf = bm.m_buildings.m_buffer;
            int x0, z0, x1, z1;
            Cells(point, radius + 64f, BuildingCell, BuildingGrid, out x0, out z0, out x1, out z1);
            for (int gz = z0; gz <= z1; gz++)
                for (int gx = x0; gx <= x1; gx++)
                {
                    ushort id = bm.m_buildingGrid[gz * BuildingGrid + gx];
                    for (int guard = 0; id != 0 && guard < ChainLimit; guard++)
                    {
                        if ((buf[id].m_flags & (Building.Flags.Created | Building.Flags.Deleted)) == Building.Flags.Created && buf[id].m_parentBuilding == 0)
                        {
                            float d = DistanceXZ(buf[id].m_position, point) - Math.Max(buf[id].Width, buf[id].Length) * 4f;
                            if (d <= radius) InsertSorted(into, id, Math.Max(0f, d));
                        }
                        id = buf[id].m_nextGridBuilding;
                    }
                }
        }

        /// <inheritdoc/>
        public void ServiceBuildings(ServiceKind service, List<ushort> into)
        {
            into.Clear();
            if (!BuildingManager.exists) return;
            if (Time.realtimeSinceStartup - _servicesAt > ServiceRefreshSeconds) RefreshServices();
            int k = (int)service;
            if (k >= 0 && k < _services.Length) into.AddRange(_services[k]);
        }

        /// <inheritdoc/>
        public void Occupants(ushort building, List<Occupant> into)
        {
            into.Clear();
            if (!BuildingManager.exists || !CitizenManager.exists) return;
            Building[] buf = Singleton<BuildingManager>.instance.m_buildings.m_buffer;
            if (building == 0 || building >= buf.Length || (buf[building].m_flags & Building.Flags.Created) == 0) return;
            CitizenManager cm = Singleton<CitizenManager>.instance;
            CitizenUnit[] units = cm.m_units.m_buffer;
            Citizen[] people = cm.m_citizens.m_buffer;
            uint unit = buf[building].m_citizenUnits;
            for (int guard = 0; unit != 0 && guard < ChainLimit; guard++)
            {
                bool home = (units[unit].m_flags & CitizenUnit.Flags.Home) != 0;
                for (int i = 0; i < 5; i++)
                {
                    uint c = units[unit].GetCitizen(i);
                    if (c == 0 || people[c].Dead || people[c].GetBuildingByLocation() != building || people[c].m_instance != 0) continue;
                    if (home != (people[c].CurrentLocation == global::Citizen.Location.Home)) continue;
                    into.Add(new Occupant { Citizen = c, Model = Math.Max(0, _catalogues.CitizenId(people[c].GetCitizenInfo(c))), Resident = home });
                }
                unit = units[unit].m_nextUnit;
            }
        }

        /// <inheritdoc/>
        public bool TryGetCitizenProfile(uint citizen, out CitizenProfile profile)
        {
            profile = new CitizenProfile();
            if (!CitizenManager.exists) return false;
            CitizenManager cm = Singleton<CitizenManager>.instance;
            Citizen[] people = cm.m_citizens.m_buffer;
            if (citizen == 0 || citizen >= people.Length || (people[citizen].m_flags & global::Citizen.Flags.Created) == 0) return false;
            bool student = (people[citizen].m_flags & global::Citizen.Flags.Student) != 0;
            profile = new CitizenProfile
            {
                Citizen = citizen,
                Name = cm.GetCitizenName(citizen) ?? cm.GetDefaultCitizenName(citizen) ?? "",
                Age = CityMath.AgeYears(people[citizen].Age),
                Female = global::Citizen.GetGender(citizen) == global::Citizen.Gender.Female,
                WorkBuilding = people[citizen].m_workBuilding,
                HomeBuilding = people[citizen].m_homeBuilding,
                Happiness = Mathf.Clamp(global::Citizen.GetHappiness(people[citizen].m_health, people[citizen].m_wellbeing), 0, 100),
                Wealth = (int)people[citizen].WealthLevel,
                Education = (int)people[citizen].EducationLevel,
                Health = Mathf.Clamp(people[citizen].m_health, 0, 100),
                Sick = people[citizen].Sick,
                Tourist = (people[citizen].m_flags & global::Citizen.Flags.Tourist) != 0,
                Employed = people[citizen].m_workBuilding != 0,
                Student = student,
            };
            return true;
        }

        /// <inheritdoc/>
        public void Patients(Vec3 point, float radius, List<Patient> into)
        {
            into.Clear();
            if (!CitizenManager.exists) return;
            BuildingsNear(point, radius, _near);
            Building[] buf = Singleton<BuildingManager>.instance.m_buildings.m_buffer;
            CitizenManager cm = Singleton<CitizenManager>.instance;
            CitizenUnit[] units = cm.m_units.m_buffer;
            Citizen[] people = cm.m_citizens.m_buffer;
            for (int b = 0; b < _near.Count; b++)
            {
                ushort building = _near[b];
                Vec3 door = Vec3.Zero;
                bool hasDoor = false;
                uint unit = buf[building].m_citizenUnits;
                for (int guard = 0; unit != 0 && guard < ChainLimit; guard++)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        uint c = units[unit].GetCitizen(i);
                        if (c == 0 || !people[c].Sick || people[c].Dead || people[c].m_vehicle != 0 || people[c].m_instance != 0
                            || people[c].GetBuildingByLocation() != building) continue;
                        if (!hasDoor)
                        {
                            BuildingDescriptor d;
                            if (!TryGetBuilding(building, out d)) break;
                            door = d.Doors[0];
                            hasDoor = true;
                        }
                        into.Add(new Patient { Citizen = c, Building = building, Pickup = door });
                    }
                    unit = units[unit].m_nextUnit;
                }
            }
        }

        /// <inheritdoc/>
        public void BurningBuildings(Vec3 point, float radius, List<ushort> into)
        {
            BuildingsNear(point, radius, into);
            if (into.Count == 0) return;
            Building[] buf = Singleton<BuildingManager>.instance.m_buildings.m_buffer;
            int w = 0;
            for (int i = 0; i < into.Count; i++)
                if (buf[into[i]].m_fireIntensity > 0) into[w++] = into[i];
            into.RemoveRange(w, into.Count - w);
        }

        /// <inheritdoc/>
        public VehicleModel GetVehicleModel(int id) { return _catalogues.Vehicle(id); }

        /// <inheritdoc/>
        public int FindVehicleModel(VehicleKind kind, uint seed) { return _catalogues.Find(kind, seed); }

        /// <inheritdoc/>
        public CitizenModel GetCitizenModel(int id) { return _catalogues.Citizen(id); }

        /// <inheritdoc/>
        public int CitizenModelCount { get { return _catalogues.CitizenCount; } }

        private BuildingDescriptor Describe(ushort id, ref Building b, BuildingInfo info, Vec3 pos)
        {
            ItemClass cls = info.m_class;
            string ai = info.m_buildingAI != null ? info.m_buildingAI.GetType().Name : null;
            float angle = b.m_angle;
            _doors.Clear();
            if (info.m_enterDoors != null)
            {
                float offset = BuildingFrame.LocalMeshOffset(b.Length, info.m_cellLength, info.m_expandFrontYard);
                for (int i = 0; i < info.m_enterDoors.Length; i++)
                {
                    Vector3 p = info.m_enterDoors[i].m_position;
                    _doors.Add(BuildingFrame.DoorWorld(pos, angle, offset, new Vec3(p.x, p.y, p.z)));
                }
            }
            if (_doors.Count == 0) _doors.Add(BuildingFrame.FrontCenter(pos, angle, b.Length));
            float height = info.m_mesh != null && info.m_mesh.bounds.size.y > 0.1f ? info.m_mesh.bounds.size.y : info.m_size.y;
            string name = null;
            try { name = Singleton<BuildingManager>.instance.GetBuildingName(id, InstanceID.Empty); }
            catch (Exception e) { _log.Error("city: building name " + id, e); }
            return new BuildingDescriptor
            {
                Id = id,
                Name = name ?? "",
                InfoName = info.name ?? "",
                Service = cls != null ? ServiceMap.Map(cls.m_service.ToString(), cls.m_subService.ToString(), ai) : ServiceKind.Other,
                SubService = cls != null ? ServiceMap.SubServiceName(cls.m_subService.ToString()) : "",
                Level = cls != null ? ServiceMap.Level((int)cls.m_level) : 1,
                Position = pos,
                YawDeg = BuildingFrame.YawDeg(angle),
                Width = b.Width * 8f,
                Length = b.Length * 8f,
                Height = height,
                Doors = _doors.ToArray(),
                District = DistrictAt(pos.X, pos.Z),
            };
        }

        private void RefreshServices()
        {
            _servicesAt = Time.realtimeSinceStartup;
            for (int i = 0; i < _services.Length; i++) _services[i].Clear();
            Building[] buf = Singleton<BuildingManager>.instance.m_buildings.m_buffer;
            const Building.Flags bad = Building.Flags.Deleted | Building.Flags.Abandoned | Building.Flags.Collapsed;
            for (int i = 1; i < buf.Length; i++)
            {
                if ((buf[i].m_flags & Building.Flags.Created) == 0 || (buf[i].m_flags & bad) != 0 || (buf[i].m_flags & Building.Flags.Completed) == 0
                    || buf[i].m_parentBuilding != 0) continue;
                BuildingInfo info = buf[i].Info;
                if (info == null || info.m_class == null) continue;
                string ai = info.m_buildingAI != null ? info.m_buildingAI.GetType().Name : null;
                _services[(int)ServiceMap.Map(info.m_class.m_service.ToString(), null, ai)].Add((ushort)i);
            }
        }

        private string NearestStreet(Vector3 p, float radius)
        {
            NetManager nm = Singleton<NetManager>.instance;
            NetSegment[] segs = nm.m_segments.m_buffer;
            NetNode[] nodes = nm.m_nodes.m_buffer;
            int x0, z0, x1, z1;
            Cells(new Vec3(p.x, p.y, p.z), radius + 64f, SegmentCell, SegmentGrid, out x0, out z0, out x1, out z1);
            float best = radius;
            ushort bestId = 0;
            for (int gz = z0; gz <= z1; gz++)
                for (int gx = x0; gx <= x1; gx++)
                {
                    ushort id = nm.m_segmentGrid[gz * SegmentGrid + gx];
                    for (int guard = 0; id != 0 && guard < ChainLimit; guard++)
                    {
                        NetInfo info = segs[id].Info;
                        if ((segs[id].m_flags & (NetSegment.Flags.Created | NetSegment.Flags.Deleted)) == NetSegment.Flags.Created
                            && info != null && info.m_netAI is RoadBaseAI)
                        {
                            Vector3 a = nodes[segs[id].m_startNode].m_position, d = nodes[segs[id].m_endNode].m_position, b, c;
                            NetSegment.CalculateMiddlePoints(a, segs[id].m_startDirection, d, segs[id].m_endDirection, true, true, out b, out c);
                            var curve = new Bezier3(a, b, c, d);
                            for (int s = 0; s <= 8; s++)
                            {
                                float dist = Vector3.Distance(curve.Position(s * 0.125f), p);
                                if (dist < best)
                                {
                                    best = dist;
                                    bestId = id;
                                }
                            }
                        }
                        id = segs[id].m_nextGridSegment;
                    }
                }
            if (bestId == 0) return null;
            string name = nm.GetSegmentName(bestId);
            return string.IsNullOrEmpty(name) ? null : name;
        }

        private void InsertSorted(List<ushort> into, ushort id, float d)
        {
            int i = _dist.Count;
            while (i > 0 && _dist[i - 1] > d) i--;
            _dist.Insert(i, d);
            into.Insert(i, id);
        }

        private static float DistanceXZ(Vector3 a, Vec3 b)
        {
            float dx = a.x - b.X, dz = a.z - b.Z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static void Cells(Vec3 c, float r, float cell, int res, out int x0, out int z0, out int x1, out int z1)
        {
            x0 = Mathf.Clamp((int)((c.X - r) / cell + res * 0.5f), 0, res - 1);
            z0 = Mathf.Clamp((int)((c.Z - r) / cell + res * 0.5f), 0, res - 1);
            x1 = Mathf.Clamp((int)((c.X + r) / cell + res * 0.5f), 0, res - 1);
            z1 = Mathf.Clamp((int)((c.Z + r) / cell + res * 0.5f), 0, res - 1);
        }
    }
}
