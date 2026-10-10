using System;
using System.Collections.Generic;
using System.Reflection;
using ColossalFramework;
using GtaSkylines.Game.City;
using Skylines.Host;
using Skylines.Host.Citizens;

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

        /// <summary>Logs to <paramref name="log"/>.</summary>
        public CityEffectApplier(HostLog log)
        {
            _log = log;
        }

        /// <summary>Ids the snapshot must skip.</summary>
        public TakenIds Taken { get { return _taken; } }

        /// <summary>Main thread: drains <paramref name="queue"/> and schedules its effects on the simulation thread.</summary>
        public void Apply(CityEffectQueue queue)
        {
            if (queue == null || queue.Count == 0) return;
            _drain.Clear();
            queue.DrainTo(_drain);
            Schedule();
        }

        /// <summary>Main thread: schedules <paramref name="effects"/> (a <c>GameSession.Effects</c> list; not modified).</summary>
        public void Apply(IList<CityEffect> effects)
        {
            if (effects == null || effects.Count == 0) return;
            _drain.Clear();
            for (int i = 0; i < effects.Count; i++) _drain.Add(effects[i]);
            Schedule();
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
                    _taken.SetInstance(e.Instance, true);
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
                    case CityEffectKind.ReturnCitizen: what = ReturnCitizen(e.Citizen); break;
                    case CityEffectKind.TakeVehicle: what = TakeVehicle(e.Vehicle); break;
                    case CityEffectKind.TakeParkedVehicle: what = TakeParked(e.Vehicle); break;
                    case CityEffectKind.InjureCitizen: what = Injure(e.Citizen, e.Building); break;
                    case CityEffectKind.KillCitizen: what = Kill(e.Citizen, e.Building); break;
                    case CityEffectKind.PanicCitizen: what = CitizenFate.Panic(e.Instance); break;
                    case CityEffectKind.DouseFire: what = Douse(e.Building, e.Amount); break;
                    case CityEffectKind.Chirp: what = Chirp(e.Citizen != 0 ? e.Citizen : NearbyWalker(ChirpNear), e.Text); break;
                    case CityEffectKind.AdvanceTime: what = AdvanceTime(e.Amount); break;
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
                if (e.Kind == CityEffectKind.TakeCitizen) _taken.SetInstance(e.Instance, false);
                else if (e.Kind == CityEffectKind.TakeVehicle) _taken.SetVehicle(e.Vehicle, false, false);
                else if (e.Kind == CityEffectKind.TakeParkedVehicle) _taken.SetVehicle(e.Vehicle, true, false);
            }
        }

        // Releasing the instance leaves the citizen Moving with no instance and no vehicle; ResidentAI would then put it
        // at home (ResidentAI.cs:2003-2011) and TouristAI at its hotel or out of the city (TouristAI.cs:725-736). A
        // resident heading to work or a visit is placed there instead, as if it had arrived.
        private static string TakeCitizen(uint citizen, ushort instance)
        {
            CitizenManager cm = Singleton<CitizenManager>.instance;
            CitizenInstance[] inst = cm.m_instances.m_buffer;
            Citizen[] people = cm.m_citizens.m_buffer;
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

        private static string ReturnCitizen(uint citizen)
        {
            Citizen[] people = Singleton<CitizenManager>.instance.m_citizens.m_buffer;
            if (citizen == 0 || citizen >= people.Length || (people[citizen].m_flags & Citizen.Flags.Created) == 0) return "citizen " + citizen + " no longer exists";
            return "citizen " + citizen + " back in CS1's hands at " + people[citizen].CurrentLocation;
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
    }
}
