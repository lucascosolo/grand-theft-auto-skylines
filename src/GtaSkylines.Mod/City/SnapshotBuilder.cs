using ColossalFramework;
using ColossalFramework.Math;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using UnityEngine;

namespace GtaSkylines.Mod.City
{
    /// <summary>
    /// Fills <see cref="CitySnapshot"/> each frame with CS1's walking citizens, moving road vehicles and parked vehicles
    /// around the player, placed as CS1 draws them (CitizenInstance.RenderInstance, Vehicle.RenderInstance; the
    /// reference's MovingObjects). Skips what the Game has taken. Main thread; allocates nothing in steady state.
    /// </summary>
    public sealed class SnapshotBuilder
    {
        private const float VehicleCell = 32f;
        private const int VehicleGrid = 540;
        private const float CitizenCell = 8f;
        private const int CitizenGrid = 2160;
        private const float GridSlack = 16f;
        private const int ChainLimit = 65536;
        private const float FrameRate = 3.75f;

        private readonly Catalogues _catalogues;
        private readonly TakenIds _taken;

        /// <summary>Uses model ids from <paramref name="catalogues"/> and skips <paramref name="taken"/>.</summary>
        public SnapshotBuilder(Catalogues catalogues, TakenIds taken)
        {
            _catalogues = catalogues;
            _taken = taken;
        }

        /// <summary>Clears and fills <paramref name="into"/> around <paramref name="center"/> within its radius.</summary>
        public void Fill(CitySnapshot into, Vector3 center)
        {
            into.Clear();
            into.Center = new Vec3(center.x, center.y, center.z);
            float radius = into.Radius;
            if (CitizenManager.exists) Citizens(into, center, radius);
            if (VehicleManager.exists)
            {
                Vehicles(into, center, radius);
                Parked(into, center, radius);
            }
        }

        private void Citizens(CitySnapshot into, Vector3 center, float radius)
        {
            CitizenManager cm = Singleton<CitizenManager>.instance;
            CitizenInstance[] buf = cm.m_instances.m_buffer;
            SimulationManager sim = Singleton<SimulationManager>.instance;
            int x0, z0, x1, z1;
            Cells(center, radius + GridSlack, CitizenCell, CitizenGrid, out x0, out z0, out x1, out z1);
            for (int gz = z0; gz <= z1; gz++)
                for (int gx = x0; gx <= x1; gx++)
                {
                    ushort id = cm.m_citizenGrid[gz * CitizenGrid + gx];
                    for (int guard = 0; id != 0 && guard < ChainLimit; guard++)
                    {
                        ushort next = buf[id].m_nextGridInstance;
                        const CitizenInstance.Flags want = CitizenInstance.Flags.Created | CitizenInstance.Flags.Character;
                        uint citizen = buf[id].m_citizen;
                        if ((buf[id].m_flags & (want | CitizenInstance.Flags.Deleted)) == want && citizen != 0
                            && !_taken.Instance(id) && !_taken.Citizen(citizen))
                        {
                            int model = _catalogues.CitizenId(buf[id].Info);
                            uint target = sim.m_referenceFrameIndex - ((uint)(id << 4) / 65536u);
                            CitizenInstance.Frame a = buf[id].GetFrameData(target - 32), b = buf[id].GetFrameData(target - 16);
                            bool visible = a.m_transition || b.m_transition || (buf[id].m_flags & CitizenInstance.Flags.HangAround) != 0
                                || !((a.m_underground && b.m_underground) || (a.m_insideBuilding && b.m_insideBuilding));
                            if (model >= 0 && visible)
                            {
                                float t = ((target & 15) + sim.m_referenceTimer) * 0.0625f;
                                Vector3 feet = Smooth(a.m_position, a.m_velocity, b.m_position, b.m_velocity, t);
                                if (Within(feet, center, radius))
                                {
                                    Vector3 fwd = Quaternion.Lerp(a.m_rotation, b.m_rotation, t) * Vector3.forward;
                                    Vector3 v = Vector3.Lerp(a.m_velocity, b.m_velocity, t) * FrameRate;
                                    ushort errand;
                                    ErrandKind kind = Errand(citizen, id, out errand);
                                    into.Citizens.Add(new AmbientCitizen
                                    {
                                        Instance = id,
                                        Citizen = citizen,
                                        Position = ToVec(feet),
                                        Velocity = ToVec(v),
                                        YawDeg = CityMath.NormalizeYawDeg(Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg),
                                        Model = model,
                                        ErrandBuilding = errand,
                                        Errand = kind,
                                    });
                                }
                            }
                        }
                        id = next;
                    }
                }
        }

        private void Vehicles(CitySnapshot into, Vector3 center, float radius)
        {
            VehicleManager vm = Singleton<VehicleManager>.instance;
            Vehicle[] buf = vm.m_vehicles.m_buffer;
            float timer = Singleton<SimulationManager>.instance.m_referenceTimer;
            int x0, z0, x1, z1;
            Cells(center, radius + GridSlack, VehicleCell, VehicleGrid, out x0, out z0, out x1, out z1);
            for (int gz = z0; gz <= z1; gz++)
                for (int gx = x0; gx <= x1; gx++)
                {
                    ushort id = vm.m_vehicleGrid[gz * VehicleGrid + gx];
                    for (int guard = 0; id != 0 && guard < ChainLimit; guard++)
                    {
                        ushort next = buf[id].m_nextGridVehicle;
                        VehicleInfo info = buf[id].Info;
                        int model = _catalogues.VehicleId(info);
                        if (model >= 0 && (buf[id].m_flags & (Vehicle.Flags.Spawned | Vehicle.Flags.Deleted)) == Vehicle.Flags.Spawned
                            && buf[id].m_leadingVehicle == 0 && !_taken.Vehicle(id, false))
                        {
                            uint target = buf[id].GetTargetFrame(info, id);
                            Vehicle.Frame a = buf[id].GetFrameData(target - 32), b = buf[id].GetFrameData(target - 16);
                            if (a.m_transition || b.m_transition || !((a.m_underground && b.m_underground) || (a.m_insideBuilding && b.m_insideBuilding)))
                            {
                                float t = ((target & 15) + timer) * 0.0625f;
                                Vector3 pivot = Smooth(a.m_position, a.m_velocity, b.m_position, b.m_velocity, t);
                                if (Within(pivot, center, radius))
                                {
                                    VehicleModel m = _catalogues.Vehicle(model);
                                    Quaternion rot = Quaternion.Lerp(a.m_rotation, b.m_rotation, t);
                                    Color c = info.m_vehicleAI.GetColor(id, ref buf[id], InfoManager.InfoMode.None, InfoManager.SubInfoMode.Default);
                                    uint driver = 0;
                                    if ((m.Kind == VehicleKind.Car || m.Kind == VehicleKind.Van) && buf[id].m_citizenUnits != 0)
                                        driver = Singleton<CitizenManager>.instance.m_units.m_buffer[buf[id].m_citizenUnits].m_citizen0;
                                    ushort driverErrand;
                                    ErrandKind driverKind = Errand(driver, 0, out driverErrand);
                                    into.Vehicles.Add(new AmbientVehicle
                                    {
                                        Id = id,
                                        Parked = false,
                                        Position = BoundsCenter(pivot, rot, m),
                                        Rotation = ToQuat(rot),
                                        Velocity = ToVec(Vector3.Lerp(a.m_velocity, b.m_velocity, t) * FrameRate),
                                        Model = model,
                                        Kind = m.Kind,
                                        DriverCitizen = driver,
                                        DriverErrandBuilding = driverErrand,
                                        DriverErrand = driverKind,
                                        ColorRgb = Rgb(c),
                                    });
                                }
                            }
                        }
                        id = next;
                    }
                }
        }

        private void Parked(CitySnapshot into, Vector3 center, float radius)
        {
            VehicleManager vm = Singleton<VehicleManager>.instance;
            VehicleParked[] buf = vm.m_parkedVehicles.m_buffer;
            int x0, z0, x1, z1;
            Cells(center, radius + GridSlack, VehicleCell, VehicleGrid, out x0, out z0, out x1, out z1);
            for (int gz = z0; gz <= z1; gz++)
                for (int gx = x0; gx <= x1; gx++)
                {
                    ushort id = vm.m_parkedGrid[gz * VehicleGrid + gx];
                    for (int guard = 0; id != 0 && guard < ChainLimit; guard++)
                    {
                        ushort next = buf[id].m_nextGridParked;
                        VehicleInfo info = buf[id].Info;
                        int model = _catalogues.VehicleId(info);
                        if (model >= 0 && (buf[id].m_flags & (ushort)VehicleParked.Flags.Created) != 0 && (buf[id].m_flags & (ushort)VehicleParked.Flags.Deleted) == 0
                            && Within(buf[id].m_position, center, radius) && !_taken.Vehicle(id, true))
                        {
                            VehicleModel m = _catalogues.Vehicle(model);
                            Quaternion rot = buf[id].m_rotation;
                            Color c = info.m_vehicleAI.GetColor(id, ref buf[id], InfoManager.InfoMode.None, InfoManager.SubInfoMode.Default);
                            into.Vehicles.Add(new AmbientVehicle
                            {
                                Id = id,
                                Parked = true,
                                Position = BoundsCenter(buf[id].m_position, rot, m),
                                Rotation = ToQuat(rot),
                                Model = model,
                                Kind = m.Kind,
                                ColorRgb = Rgb(c),
                            });
                        }
                        id = next;
                    }
                }
        }

        // F-PED-7: the building a citizen's instance is heading to (none for a node target: a stop or a tour point,
        // CitizenInstance.cs:39, :84) and what it is to the citizen (Citizen.cs:255-259). Instance 0: the citizen's own
        // (a driver keeps its instance while driving, ResidentAI.cs:2909-2912).
        private static ErrandKind Errand(uint citizen, ushort instance, out ushort building)
        {
            building = 0;
            CitizenManager cm = Singleton<CitizenManager>.instance;
            Citizen[] people = cm.m_citizens.m_buffer;
            if (citizen == 0 || citizen >= people.Length) return ErrandKind.None;
            if (instance == 0) instance = people[citizen].m_instance;
            CitizenInstance[] inst = cm.m_instances.m_buffer;
            if (instance == 0 || instance >= inst.Length || inst[instance].m_citizen != citizen
                || (inst[instance].m_flags & CitizenInstance.Flags.TargetIsNode) != 0) return ErrandKind.None;
            building = inst[instance].m_targetBuilding;
            ErrandKind kind = Errands.Classify(building, people[citizen].m_homeBuilding, people[citizen].m_workBuilding, people[citizen].m_visitBuilding);
            if (kind == ErrandKind.None) building = 0;
            return kind;
        }

        private static Vec3 BoundsCenter(Vector3 pivot, Quaternion rot, VehicleModel m)
        {
            Vector3 off = rot * new Vector3(m.CenterOffset.X, m.CenterOffset.Y, m.CenterOffset.Z);
            return new Vec3(pivot.x + off.x, pivot.y + off.y, pivot.z + off.z);
        }

        private static bool Within(Vector3 p, Vector3 c, float r)
        {
            float dx = p.x - c.x, dz = p.z - c.z;
            return dx * dx + dz * dz <= r * r && Mathf.Abs(p.y - c.y) <= r;
        }

        private static int Rgb(Color c)
        {
            Color32 k = c;
            return (k.r << 16) | (k.g << 8) | k.b;
        }

        private static Vec3 ToVec(Vector3 v) { return new Vec3(v.x, v.y, v.z); }

        private static Quat ToQuat(Quaternion q) { return new Quat(q.x, q.y, q.z, q.w); }

        private static Vector3 Smooth(Vector3 p0, Vector3 v0, Vector3 p1, Vector3 v1, float t)
        {
            return new Bezier3 { a = p0, b = p0 + v0 * 0.333f, c = p1 - v1 * 0.333f, d = p1 }.Position(t);
        }

        private static void Cells(Vector3 c, float r, float cell, int res, out int x0, out int z0, out int x1, out int z1)
        {
            x0 = Mathf.Clamp((int)((c.x - r) / cell + res * 0.5f), 0, res - 1);
            z0 = Mathf.Clamp((int)((c.z - r) / cell + res * 0.5f), 0, res - 1);
            x1 = Mathf.Clamp((int)((c.x + r) / cell + res * 0.5f), 0, res - 1);
            z1 = Mathf.Clamp((int)((c.z + r) / cell + res * 0.5f), 0, res - 1);
        }
    }
}
