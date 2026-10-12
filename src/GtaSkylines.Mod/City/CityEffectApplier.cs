using System;
using System.Collections.Generic;
using System.Reflection;
using ColossalFramework;
using GtaSkylines.Game.City;
using Skylines.Host;
using Skylines.Host.Citizens;
using UnityEngine;

namespace GtaSkylines.Mod.City
{
    /// <summary>
    /// Applies the Game's <see cref="CityEffect"/>s to CS1 on the simulation thread (<c>SimulationManager.AddAction</c>),
    /// in order, each guarded and logged. The CS1 paths and their sources are in docs/CS1-API-NOTES.md (M2, effects).
    /// <see cref="Apply(CityEffectQueue)"/> runs on the main thread and marks takes in <see cref="Taken"/> at once, so the next
    /// snapshot already skips them.
    /// </summary>
    public sealed class CityEffectApplier
    {
        private const byte InjuredHealth = 10;
        private const int ChainLimit = 64;

        private static readonly MethodInfo s_die = typeof(ResidentAI).GetMethod("Die", BindingFlags.NonPublic | BindingFlags.Instance,
            null, new[] { typeof(uint), typeof(Citizen).MakeByRefType() }, null);

        private readonly HostLog _log;
        private readonly TakenIds _taken = new TakenIds();
        private readonly List<CityEffect> _drain = new List<CityEffect>();
        private readonly List<uint> _heldMain = new List<uint>();
        private readonly List<uint> _heldSim = new List<uint>();
        private readonly Action _rehold;
        private volatile bool _reholdPending;

        // F-VEH-8 lane blockers (docs/contracts/C2-abandoned-cars.md).
        private const int TrafficPerFrame = 256;
        private const int TrafficCeiling = 65534;
        private readonly Catalogues _catalogues;
        private readonly LaneBlockers _blockers = new LaneBlockers();
        private readonly List<LaneBlocker> _blockSim = new List<LaneBlocker>();
        private readonly List<ushort> _blockIds = new List<ushort>();
        private readonly Action _holdBlockers;
        private readonly Action _detachBlockers;
        private volatile bool _blockPending;
        private volatile bool _blockSuspended;
        private bool _detachedForSave;
        private VehicleInfo _fallbackCar;

        /// <summary>Logs to <paramref name="log"/>; vehicle prefabs for lane blockers come from <paramref name="catalogues"/>.</summary>
        public CityEffectApplier(HostLog log, Catalogues catalogues)
        {
            _log = log;
            _catalogues = catalogues;
            _rehold = Rehold;
            _holdBlockers = HoldBlockers;
            _detachBlockers = DetachBlockers;
        }

        /// <summary>Ids the snapshot must skip.</summary>
        public TakenIds Taken { get { return _taken; } }

        /// <summary>CS1 vehicles standing in for abandoned Game cars (F-VEH-8); the snapshot must skip them.</summary>
        public LaneBlockers Blockers { get { return _blockers; } }

        /// <summary>Main thread: drains <paramref name="queue"/> and schedules its effects on the simulation thread.</summary>
        public void Apply(CityEffectQueue queue)
        {
            if (queue == null || queue.Count == 0) return;
            _drain.Clear();
            queue.DrainTo(_drain);
            Schedule();
        }

        /// <summary>
        /// Main thread, every frame: schedules <paramref name="effects"/> (a <c>GameSession.Effects</c> list; not modified),
        /// then puts back any held citizen CS1 has sent out again (<see cref="CheckHeld"/>).
        /// </summary>
        public void Apply(IList<CityEffect> effects)
        {
            if (effects != null && effects.Count > 0)
            {
                _drain.Clear();
                for (int i = 0; i < effects.Count; i++) _drain.Add(effects[i]);
                Schedule();
            }
            CheckHeld();
            KeepBlockers();
        }

        // F-PED-7 holding: CS1 sends a citizen we placed in a building out again with a new instance (HumanAI.StartMoving,
        // HumanAI.cs:103-133). The main thread only reads Citizen.m_instance; the release runs on the simulation thread.
        // Allocates (one AddAction) only when such an instance exists.
        private void CheckHeld()
        {
            if (_reholdPending || !CitizenManager.exists || !SimulationManager.exists) return;
            _taken.HeldCitizens(_heldMain);
            if (_heldMain.Count == 0) return;
            Citizen[] people = Singleton<CitizenManager>.instance.m_citizens.m_buffer;
            for (int i = 0; i < _heldMain.Count; i++)
            {
                uint c = _heldMain[i];
                ushort inst = c < people.Length ? people[c].m_instance : (ushort)0;
                if (inst == 0 || _taken.Instance(inst)) continue;
                _reholdPending = true;
                Singleton<SimulationManager>.instance.AddAction(_rehold);
                return;
            }
        }

        private void Rehold()
        {
            try
            {
                CitizenManager cm = Singleton<CitizenManager>.instance;
                Citizen[] people = cm.m_citizens.m_buffer;
                CitizenInstance[] inst = cm.m_instances.m_buffer;
                _taken.HeldCitizens(_heldSim);
                for (int i = 0; i < _heldSim.Count; i++)
                {
                    uint c = _heldSim[i];
                    if (c >= people.Length || (people[c].m_flags & Citizen.Flags.Created) == 0 || people[c].Dead) continue;
                    ushort id = people[c].m_instance;
                    if (id == 0 || _taken.Instance(id)) continue;
                    ushort from = inst[id].m_sourceBuilding;
                    bool placed = Place(c, from, people);
                    _log.Info("held citizen " + c + ": CS1 instance " + id + " released, " + (placed ? "back in building " + people[c].GetBuildingByLocation() : "nowhere to put it"));
                }
            }
            catch (Exception ex)
            {
                _log.Error("holding citizens failed", ex);
            }
            finally
            {
                _reholdPending = false;
            }
        }

        /// <summary>Where a <c>Chirp(0, text)</c> looks for a walking citizen to send it (the player's position; main thread writes it).</summary>
        public UnityEngine.Vector3 ChirpNear;

        private void Schedule()
        {
            CityEffect[] batch = _drain.ToArray();
            _drain.Clear();
            for (int i = 0; i < batch.Length; i++) MarkTaken(batch[i]);
            if (!SimulationManager.exists)
            {
                _log.Warn("effects: no simulation, " + batch.Length + " dropped");
                return;
            }
            Singleton<SimulationManager>.instance.AddAction(() =>
            {
                for (int i = 0; i < batch.Length; i++) Run(batch[i]);
            });
        }

        private void MarkTaken(CityEffect e)
        {
            switch (e.Kind)
            {
                case CityEffectKind.TakeCitizen:
                    if (e.Instance != 0) _taken.SetInstance(e.Instance, true);
                    _taken.SetCitizen(e.Citizen, true);
                    break;
                case CityEffectKind.ReturnCitizen:
                    _taken.SetCitizen(e.Citizen, false);
                    break;
                case CityEffectKind.TakeVehicle:
                    _taken.SetVehicle(e.Vehicle, false, true);
                    break;
                case CityEffectKind.TakeParkedVehicle:
                    _taken.SetVehicle(e.Vehicle, true, true);
                    break;
            }
        }

        private void Run(CityEffect e)
        {
            try
            {
                string what;
                switch (e.Kind)
                {
                    case CityEffectKind.TakeCitizen: what = TakeCitizen(e.Citizen, e.Instance); break;
                    case CityEffectKind.ReturnCitizen: what = ReturnCitizen(e); break;
                    case CityEffectKind.TakeVehicle: what = TakeVehicle(e.Vehicle); break;
                    case CityEffectKind.TakeParkedVehicle: what = TakeParked(e.Vehicle); break;
                    case CityEffectKind.InjureCitizen: what = Injure(e.Citizen, e.Building); break;
                    case CityEffectKind.KillCitizen: what = Kill(e.Citizen, e.Building); break;
                    case CityEffectKind.PanicCitizen: what = CitizenFate.Panic(e.Instance); break;
                    case CityEffectKind.DouseFire: what = Douse(e.Building, e.Amount); break;
                    case CityEffectKind.Chirp: what = Chirp(e.Citizen != 0 ? e.Citizen : NearbyWalker(ChirpNear), e.Text); break;
                    case CityEffectKind.AdvanceTime: what = AdvanceTime(e.Amount); break;
                    case CityEffectKind.BlockLane: what = Block(e); break;
                    case CityEffectKind.UnblockLane: what = Unblock(e.Tag); break;
                    default: what = "unknown effect"; break;
                }
                _log.Info("effect " + e.Kind + ": " + what);
            }
            catch (Exception ex)
            {
                _log.Error("effect " + e.Kind + " failed", ex);
            }
            finally
            {
                if (e.Kind == CityEffectKind.TakeCitizen && e.Instance != 0) _taken.SetInstance(e.Instance, false);
                else if (e.Kind == CityEffectKind.TakeVehicle) _taken.SetVehicle(e.Vehicle, false, false);
                else if (e.Kind == CityEffectKind.TakeParkedVehicle) _taken.SetVehicle(e.Vehicle, true, false);
            }
        }

        // Releasing the instance leaves the citizen Moving with no instance and no vehicle; ResidentAI would then put it
        // at home (ResidentAI.cs:2003-2011) and TouristAI at its hotel or out of the city (TouristAI.cs:725-736). A
        // resident heading to work or a visit is placed there instead, as if it had arrived. Instance 0 (a carjacked
        // driver): whatever instance the citizen has (a driver keeps one while it drives, ResidentAI.cs:2909-2912).
        private static string TakeCitizen(uint citizen, ushort instance)
        {
            CitizenManager cm = Singleton<CitizenManager>.instance;
            CitizenInstance[] inst = cm.m_instances.m_buffer;
            Citizen[] people = cm.m_citizens.m_buffer;
            if (instance == 0 && citizen != 0 && citizen < people.Length) instance = people[citizen].m_instance;
            if (instance == 0 || instance >= inst.Length || (inst[instance].m_flags & CitizenInstance.Flags.Created) == 0
                || inst[instance].m_citizen != citizen || citizen == 0)
                return "instance " + instance + " no longer holds citizen " + citizen;
            ushort target = (inst[instance].m_flags & CitizenInstance.Flags.TargetIsNode) == 0 ? inst[instance].m_targetBuilding : (ushort)0;
            cm.ReleaseCitizenInstance(instance);
            if ((people[citizen].m_flags & Citizen.Flags.Tourist) != 0 || people[citizen].Dead)
                return "instance released; tourist or dead citizen left to CS1";
            if (target != 0 && target == people[citizen].m_workBuilding)
            {
                people[citizen].CurrentLocation = Citizen.Location.Work;
                return "instance released; citizen at work building " + target;
            }
            if (target != 0 && target == people[citizen].m_visitBuilding)
            {
                people[citizen].CurrentLocation = Citizen.Location.Visit;
                return "instance released; citizen visiting building " + target;
            }
            if (people[citizen].m_homeBuilding != 0)
            {
                if (people[citizen].m_visitBuilding != 0) people[citizen].SetVisitplace(citizen, 0, 0u);
                people[citizen].CurrentLocation = Citizen.Location.Home;
                return "instance released; citizen at home building " + people[citizen].m_homeBuilding;
            }
            return "instance released; homeless citizen left to CS1";
        }

        // F-PED-7. Arrived: in the errand building. Otherwise the journey continues from the ped's spot as CS1 continues
        // one (HumanAI.StartMoving, HumanAI.cs:127-130, with the spot in place of SetSource's spawn point,
        // ResidentAI.cs:2563-2570): a new instance (CitizenManager.cs:1354) with frames and m_targetPos at the spot,
        // Moving, then CitizenAI.SetTarget (CitizenAI.cs:190), which path-finds from m_targetPos (ResidentAI.cs:3105) and
        // spawns the instance once the path is found (HumanAI.cs:205-213). A path that cannot start (no WaitingPath,
        // CitizenAI.cs:1130) falls back to placing the citizen in the errand building.
        private static string ReturnCitizen(CityEffect e)
        {
            uint citizen = e.Citizen;
            Citizen[] people;
            if (!Alive(citizen, out people)) return "citizen " + citizen + " missing or dead";
            if (e.Building == 0) return "citizen " + citizen + " back in CS1's hands at " + people[citizen].CurrentLocation + " (no errand)";
            if (e.Arrived)
                return Place(citizen, e.Building, people) ? "arrived at " + people[citizen].CurrentLocation + " building " + e.Building : "arrived, but nowhere to put it";
            string why = Continue(citizen, e, people);
            if (why == null) return "continues to building " + e.Building + " (" + e.Errand + ") from " + e.Position;
            return why + "; " + (Place(citizen, e.Building, people) ? "placed at " + people[citizen].CurrentLocation + " building " + e.Building : "nowhere to put it");
        }

        // Null on success, else why the journey could not continue (no instance is left behind).
        private static string Continue(uint citizen, CityEffect e, Citizen[] people)
        {
            CitizenManager cm = Singleton<CitizenManager>.instance;
            Building[] buildings = Singleton<BuildingManager>.instance.m_buildings.m_buffer;
            if (e.Building >= buildings.Length || (buildings[e.Building].m_flags & Building.Flags.Created) == 0) return "errand building gone";
            if (people[citizen].m_instance != 0) cm.ReleaseCitizenInstance(people[citizen].m_instance);
            CitizenInfo info = people[citizen].GetCitizenInfo(citizen);
            ushort id;
            if (info == null || !cm.CreateCitizenInstance(out id, ref Singleton<SimulationManager>.instance.m_randomizer, info, citizen)) return "no citizen instance";
            CitizenInstance[] inst = cm.m_instances.m_buffer;
            Vector3 pos = new Vector3(e.Position.X, e.Position.Y, e.Position.Z);
            CitizenInstance.Frame frame = inst[id].m_frame0;
            frame.m_position = pos;
            frame.m_velocity = Vector3.zero;
            frame.m_rotation = Quaternion.Euler(0f, e.YawDeg, 0f);
            inst[id].m_frame0 = frame;
            inst[id].m_frame1 = frame;
            inst[id].m_frame2 = frame;
            inst[id].m_frame3 = frame;
            inst[id].m_targetPos = new Vector4(pos.x, pos.y, pos.z, 1f);
            people[citizen].CurrentLocation = Citizen.Location.Moving;
            info.m_citizenAI.SetTarget(id, ref inst[id], e.Building);
            if ((inst[id].m_flags & CitizenInstance.Flags.WaitingPath) != 0) return null;
            cm.ReleaseCitizenInstance(id);
            return "no path from the spot";
        }

        // VehicleManager.ReleaseVehicle (VehicleManager.cs:1647-1743), as CS1 despawns a congested car
        // (PassengerCarAI.cs:196-199): passengers' units are released (CitizenManager.cs:1268-1271 clears their vehicle).
        // Trailers are released with their tractor.
        private static string TakeVehicle(ushort vehicle)
        {
            VehicleManager vm = Singleton<VehicleManager>.instance;
            Vehicle[] buf = vm.m_vehicles.m_buffer;
            if (vehicle == 0 || vehicle >= buf.Length || (buf[vehicle].m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created)
                return "vehicle " + vehicle + " no longer exists";
            ushort id = buf[vehicle].GetFirstVehicle(vehicle);
            int n = 0;
            while (id != 0 && n < ChainLimit)
            {
                ushort next = buf[id].m_trailingVehicle;
                vm.ReleaseVehicle(id);
                id = next;
                n++;
            }
            return "released " + n + " vehicle(s) starting at the chain of " + vehicle;
        }

        private static string TakeParked(ushort parked)
        {
            VehicleManager vm = Singleton<VehicleManager>.instance;
            VehicleParked[] buf = vm.m_parkedVehicles.m_buffer;
            if (parked == 0 || parked >= buf.Length || (buf[parked].m_flags & (ushort)VehicleParked.Flags.Created) == 0)
                return "parked vehicle " + parked + " no longer exists";
            vm.ReleaseParkedVehicle(parked);
            return "parked vehicle " + parked + " released";
        }

        // Sick with low health at a building: ResidentAI's Home/Work/Visit branches then call FindHospital(.., Sick)
        // (ResidentAI.cs:1684-1686, :1766-1772, :1869-1897), which offers the citizen to an ambulance. Health below 40
        // keeps children and seniors off the childcare/eldercare path (ResidentAI.cs:2298).
        private string Injure(uint citizen, ushort building)
        {
            Citizen[] people;
            if (!Alive(citizen, out people)) return "citizen " + citizen + " missing or dead";
            _taken.SetCitizen(citizen, false);
            people[citizen].Sick = true;
            people[citizen].m_health = InjuredHealth;
            return Place(citizen, building, people) ? "sick at " + people[citizen].CurrentLocation + " building " + people[citizen].GetBuildingByLocation() : "sick, but no building to wait in";
        }

        // ResidentAI.Die then a building location: the Dead branches call FindHospital(.., Dead), a hearse
        // (ResidentAI.cs:1669, :1755, :1851). Without a building the citizen is released, as CS1 does (ResidentAI.cs:1985-1987).
        private string Kill(uint citizen, ushort building)
        {
            Citizen[] people;
            if (!Alive(citizen, out people)) return "citizen " + citizen + " missing or already dead";
            _taken.SetCitizen(citizen, false);
            ushort instance = people[citizen].m_instance;
            if (building == 0 && instance != 0) return CitizenFate.Kill(instance);
            CitizenInfo info = people[citizen].GetCitizenInfo(citizen);
            var ai = info != null ? info.m_citizenAI as ResidentAI : null;
            if (instance != 0) Singleton<CitizenManager>.instance.ReleaseCitizenInstance(instance);
            if (ai == null || s_die == null || (people[citizen].m_flags & Citizen.Flags.Tourist) != 0)
            {
                Singleton<CitizenManager>.instance.ReleaseCitizen(citizen);
                return "released (not a resident" + (s_die == null ? "; ResidentAI.Die not found" : "") + ")";
            }
            object[] args = { citizen, people[citizen] };
            s_die.Invoke(ai, args);
            people[citizen] = (Citizen)args[1];
            if (!Place(citizen, building, people))
            {
                Singleton<CitizenManager>.instance.ReleaseCitizen(citizen);
                return "died with no building for the body; released";
            }
            return "died; body at " + people[citizen].CurrentLocation + " building " + people[citizen].GetBuildingByLocation() + " for a hearse";
        }

        private static bool Alive(uint citizen, out Citizen[] people)
        {
            people = Singleton<CitizenManager>.instance.m_citizens.m_buffer;
            return citizen != 0 && citizen < people.Length && (people[citizen].m_flags & Citizen.Flags.Created) != 0 && !people[citizen].Dead;
        }

        // Puts a citizen with no instance in a building: home (building 0 or its home), work, or a visit place
        // (Citizen.SetVisitplace, Citizen.cs:770). False when there is nowhere to put it.
        private static bool Place(uint citizen, ushort building, Citizen[] people)
        {
            ushort instance = people[citizen].m_instance;
            if (instance != 0) Singleton<CitizenManager>.instance.ReleaseCitizenInstance(instance);
            ushort home = people[citizen].m_homeBuilding;
            if (building != 0 && building != home)
            {
                if (building == people[citizen].m_workBuilding)
                {
                    people[citizen].CurrentLocation = Citizen.Location.Work;
                    return true;
                }
                people[citizen].SetVisitplace(citizen, building, 0u);
                if (people[citizen].m_visitBuilding == building)
                {
                    people[citizen].CurrentLocation = Citizen.Location.Visit;
                    return true;
                }
            }
            if (home == 0) return false;
            people[citizen].CurrentLocation = Citizen.Location.Home;
            return true;
        }

        // FireTruckAI.ExtinguishFire (FireTruckAI.cs:415-475) with the reduction given by the Game.
        private static string Douse(ushort building, float amount)
        {
            BuildingManager bm = Singleton<BuildingManager>.instance;
            Building[] buf = bm.m_buildings.m_buffer;
            if (building == 0 || building >= buf.Length || (buf[building].m_flags & Building.Flags.Created) == 0) return "building " + building + " missing";
            int before = buf[building].m_fireIntensity;
            if (before == 0) return "not burning";
            float a = amount > 1f ? 1f : (amount > 0f ? amount : 0f);
            int after = Math.Max(0, before - (int)Math.Ceiling(a * 255f));
            buf[building].m_fireIntensity = (byte)after;
            if (after == 0) PutOut(bm, building, buf);
            ushort sub = buf[building].m_parentBuilding == 0 ? buf[building].m_subBuilding : (ushort)0;
            for (int n = 0; sub != 0 && n < ChainLimit; n++)
            {
                buf[sub].m_fireIntensity = (byte)Math.Min(after, buf[sub].m_fireIntensity);
                if (after == 0)
                {
                    Singleton<InstanceManager>.instance.SetGroup(new InstanceID { Building = sub }, null);
                    bm.UpdateBuildingRenderer(sub, true);
                }
                sub = buf[sub].m_subBuilding;
            }
            return "fire " + before + " -> " + after;
        }

        private static void PutOut(BuildingManager bm, ushort building, Building[] buf)
        {
            Singleton<InstanceManager>.instance.SetGroup(new InstanceID { Building = building }, null);
            Building.Flags flags = buf[building].m_flags;
            if (buf[building].m_productionRate != 0 && (buf[building].m_flags & Building.Flags.Evacuating) == 0)
                buf[building].m_flags |= Building.Flags.Active;
            Building.Flags flags2 = buf[building].m_flags;
            bm.UpdateBuildingRenderer(building, buf[building].GetLastFrameData().m_fireDamage == 0
                || (buf[building].m_flags & (Building.Flags.Abandoned | Building.Flags.Collapsed)) != 0);
            bm.UpdateBuildingColors(building);
            if (flags2 != flags) bm.UpdateFlags(building, flags2 ^ flags);
            if (Singleton<GuideManager>.instance.m_properties != null) bm.m_buildingOnFire.Deactivate(building, false);
        }

        // CustomCitizenMessage (a game type, so saved chirps load without this mod) returns the text as given when
        // its tag is null (CustomCitizenMessage.cs:3-16); MessageManager.QueueMessage (MessageManager.cs:535).
        // Chirp(0): the nearest created citizen instance within 200 m of the player (CitizenInstance.cs:12, :78, :441),
        // else 0 (CS1 then shows the post without a sender).
        private static uint NearbyWalker(UnityEngine.Vector3 near)
        {
            CitizenInstance[] inst = Singleton<CitizenManager>.instance.m_instances.m_buffer;
            uint best = 0;
            float bestSq = 200f * 200f;
            for (int i = 1; i < inst.Length; i++)
            {
                if ((inst[i].m_flags & CitizenInstance.Flags.Created) == 0 || inst[i].m_citizen == 0) continue;
                float d = (inst[i].GetLastFramePosition() - near).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = inst[i].m_citizen; }
            }
            return best;
        }

        private static string Chirp(uint sender, string text)
        {
            if (string.IsNullOrEmpty(text)) return "empty text";
            Singleton<MessageManager>.instance.QueueMessage(new CustomCitizenMessage(sender, text, null));
            return "queued from citizen " + sender;
        }

        // m_dayTimeOffsetFrames moves the sun (SimulationManager.cs:352, read at :668-675 and :928-931); while
        // day/night is off the simulation rewrites it each frame (:924-927), so nothing is done then.
        private static string AdvanceTime(float hours)
        {
            SimulationManager sim = Singleton<SimulationManager>.instance;
            if (!sim.m_enableDayNight) return "ignored, the city has no day/night cycle";
            uint frames = CityMath.HoursToDayFrames(hours);
            sim.m_dayTimeOffsetFrames += frames;
            return "sun moved " + frames + " frames (" + hours.ToString("0.00") + " h)";
        }

        // ---- F-VEH-8 lane blockers -------------------------------------------------------------------------------
        // A blocker is a PassengerCarAI vehicle with no path, Spawned (in m_vehicleGrid, which CarAI.CheckOtherVehicles
        // walks, CarAI.cs:646-680) and Stopped (maxSpeed 0, CarAI.cs:428-431). Every frame we reset m_waitCounter (Stopped
        // is dropped at 2, PassengerCarAI.cs:208-215, :499-506) and m_blockCounter (released at 150, CarAI.cs:88-96) and
        // re-pose it. Frames with m_insideBuilding are never drawn (Vehicle.cs:299-305). Notes: docs/CS1-API-NOTES.md (C2).

        // Main thread, every frame. While CS1 saves (SavePanel.isSaving, SavePanel.cs:85) the vehicles are released once,
        // ahead of the save's own simulation action (LoadingManager.cs:1137), and recreated when it is done.
        private void KeepBlockers()
        {
            if (_blockPending || _blockers.Count == 0 || !SimulationManager.exists) return;
            bool saving = SavePanel.isSaving;
            if (saving && _detachedForSave) return;
            _detachedForSave = saving;
            _blockPending = true;
            Singleton<SimulationManager>.instance.AddAction(saving ? _detachBlockers : _holdBlockers);
        }

        private string Block(CityEffect e)
        {
            var b = new LaneBlocker { Tag = e.Tag, Pivot = e.Position, YawDeg = e.YawDeg, Model = e.Model, Segment = e.Segment };
            _blockers.Add(b);
            string where = "car " + e.Tag + " on segment " + e.Segment + " at " + e.Position;
            if (_blockSuspended) return where + ": blocker waits for the save to finish";
            string why;
            ushort id = CreateBlocker(b, out why);
            if (id == 0)
            {
                _blockers.SetVehicle(e.Tag, 0, true);
                return where + ": " + why + "; traffic pressure only (reroute, no queue)";
            }
            return where + ": invisible CS1 vehicle " + id + " (" + why + ") holds traffic";
        }

        private string Unblock(int tag)
        {
            LaneBlocker b;
            if (!_blockers.Remove(tag, out b)) return "car " + tag + " was not blocking";
            if (b.Vehicle == 0) return "car " + tag + ": traffic pressure ended";
            if (!IsBlockerVehicle(b)) return "car " + tag + ": CS1 vehicle " + b.Vehicle + " already gone";
            Singleton<VehicleManager>.instance.ReleaseVehicle(b.Vehicle);
            return "car " + tag + ": CS1 vehicle " + b.Vehicle + " released";
        }

        private void HoldBlockers()
        {
            try
            {
                _blockSuspended = false;
                _blockers.CopyTo(_blockSim);
                Vehicle[] buf = Singleton<VehicleManager>.instance.m_vehicles.m_buffer;
                for (int i = 0; i < _blockSim.Count; i++)
                {
                    LaneBlocker b = _blockSim[i];
                    if (b.Vehicle != 0 && !IsBlockerVehicle(b))
                    {
                        _log.Warn("lane block car " + b.Tag + ": CS1 vehicle " + b.Vehicle + " gone; traffic pressure only (reroute, no queue)");
                        _blockers.SetVehicle(b.Tag, 0, true);
                        b.TrafficOnly = true;
                        b.Vehicle = 0;
                    }
                    if (b.Vehicle == 0 && !b.TrafficOnly)
                    {
                        string why;
                        b.Vehicle = CreateBlocker(b, out why);
                        if (b.Vehicle == 0)
                        {
                            _blockers.SetVehicle(b.Tag, 0, true);
                            _log.Warn("lane block car " + b.Tag + ": " + why + "; traffic pressure only (reroute, no queue)");
                        }
                    }
                    if (b.Vehicle != 0) Pin(b.Vehicle, ref buf[b.Vehicle], b);
                    Pressure(b.Segment);
                }
            }
            catch (Exception ex)
            {
                _log.Error("lane blockers failed", ex);
            }
            finally
            {
                _blockPending = false;
            }
        }

        private void DetachBlockers()
        {
            try
            {
                _blockSuspended = true;
                _blockers.CopyTo(_blockSim);
                _blockers.DetachAll(_blockIds);
                _log.Info("lane blockers: " + ReleaseBlockerVehicles(_blockSim) + " CS1 vehicle(s) released for the save");
            }
            catch (Exception ex)
            {
                _log.Error("lane blockers before save failed", ex);
            }
            finally
            {
                _blockPending = false;
            }
        }

        /// <summary>Main thread, play-mode end or city unload: forgets every lane block and releases its CS1 vehicle.</summary>
        public void ReleaseBlockers()
        {
            _detachedForSave = false;
            if (_blockers.Count == 0) return;
            var all = new List<LaneBlocker>();
            _blockers.CopyTo(all);
            _blockers.Clear(_blockIds);
            if (!SimulationManager.exists) return;
            Singleton<SimulationManager>.instance.AddAction(() =>
            {
                try { _log.Info("lane blockers: " + ReleaseBlockerVehicles(all) + " CS1 vehicle(s) released at play end"); }
                catch (Exception ex) { _log.Error("releasing lane blockers failed", ex); }
            });
        }

        private static int ReleaseBlockerVehicles(List<LaneBlocker> blockers)
        {
            int n = 0;
            for (int i = 0; i < blockers.Count; i++)
            {
                if (!IsBlockerVehicle(blockers[i])) continue;
                Singleton<VehicleManager>.instance.ReleaseVehicle(blockers[i].Vehicle);
                n++;
            }
            return n;
        }

        // Still ours: created, spawned, no path, a PassengerCarAI standing at the pivot (CS1 may have released it and
        // reused the id).
        private static bool IsBlockerVehicle(LaneBlocker b)
        {
            Vehicle[] buf = Singleton<VehicleManager>.instance.m_vehicles.m_buffer;
            if (b.Vehicle == 0 || b.Vehicle >= buf.Length) return false;
            const Vehicle.Flags live = Vehicle.Flags.Created | Vehicle.Flags.Spawned;
            if ((buf[b.Vehicle].m_flags & (live | Vehicle.Flags.Deleted)) != live || buf[b.Vehicle].m_path != 0) return false;
            VehicleInfo info = buf[b.Vehicle].Info;
            if (info == null || !(info.m_vehicleAI is PassengerCarAI)) return false;
            return (buf[b.Vehicle].GetLastFramePosition() - new Vector3(b.Pivot.X, b.Pivot.Y, b.Pivot.Z)).sqrMagnitude < 1f;
        }

        // VehicleManager.CreateVehicle (VehicleManager.cs:1585) of the car's prefab when it is a PassengerCarAI, else the
        // first catalogued PassengerCarAI prefab (the behaviour above is only verified for that AI); 0 and why on failure.
        private ushort CreateBlocker(LaneBlocker b, out string why)
        {
            VehicleInfo info = BlockerInfo(b.Model);
            if (info == null)
            {
                why = "no passenger-car prefab";
                return 0;
            }
            VehicleManager vm = Singleton<VehicleManager>.instance;
            ushort id;
            if (!vm.CreateVehicle(out id, ref Singleton<SimulationManager>.instance.m_randomizer, info, new Vector3(b.Pivot.X, b.Pivot.Y, b.Pivot.Z),
                    TransferManager.TransferReason.None, false, false))
            {
                why = "vehicle buffer full";
                return 0;
            }
            Vehicle[] buf = vm.m_vehicles.m_buffer;
            Pin(id, ref buf[id], b);
            _blockers.SetVehicle(b.Tag, id, false);
            buf[id].Spawn(id);
            why = info.name;
            return id;
        }

        private VehicleInfo BlockerInfo(int model)
        {
            VehicleInfo info = _catalogues.VehicleInfoOf(model);
            if (info != null && info.m_vehicleAI is PassengerCarAI) return info;
            if (_fallbackCar != null) return _fallbackCar;
            for (int i = 0; (info = _catalogues.VehicleInfoOf(i)) != null; i++)
                if (info.m_vehicleAI is PassengerCarAI) return _fallbackCar = info;
            return null;
        }

        // All four frames at the pivot, hidden (m_insideBuilding) and still; Stopped; counters reset; m_segment rebuilt by
        // CarAI.FrameDataUpdated (CarAI.cs:637-644), which is what other cars brake for (CarAI.cs:778-790).
        private static void Pin(ushort id, ref Vehicle v, LaneBlocker b)
        {
            var pos = new Vector3(b.Pivot.X, b.Pivot.Y, b.Pivot.Z);
            var frame = new Vehicle.Frame(pos, Quaternion.Euler(0f, b.YawDeg, 0f));
            frame.m_insideBuilding = true;
            v.m_frame0 = frame;
            v.m_frame1 = frame;
            v.m_frame2 = frame;
            v.m_frame3 = frame;
            var target = new Vector4(pos.x, pos.y, pos.z, 0f);
            v.m_targetPos0 = target;
            v.m_targetPos1 = target;
            v.m_targetPos2 = target;
            v.m_targetPos3 = target;
            v.m_flags |= Vehicle.Flags.Stopped;
            v.m_waitCounter = 0;
            v.m_blockCounter = 0;
            v.Info.m_vehicleAI.FrameDataUpdated(id, ref v, ref v.m_frame0);
        }

        // NetSegment.AddTraffic (NetSegment.cs:1905-1909), kept below 65535 so RoadBaseAI never flags the segment Blocked
        // (RoadBaseAI.cs:1411-1418).
        private static void Pressure(int segment)
        {
            NetSegment[] segs = Singleton<NetManager>.instance.m_segments.m_buffer;
            if (segment <= 0 || segment >= segs.Length || (segs[segment].m_flags & NetSegment.Flags.Created) == 0) return;
            int room = TrafficCeiling - segs[segment].m_trafficBuffer;
            if (room > 0) segs[segment].AddTraffic(Math.Min(TrafficPerFrame, room), 0);
        }
    }
}
