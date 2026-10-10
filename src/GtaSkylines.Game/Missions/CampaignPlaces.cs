using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Roads;

namespace GtaSkylines.Game.Missions
{
    /// <summary>A real place in the city as missions name it.</summary>
    public sealed class CampaignPlace
    {
        /// <summary>Building id, 0 when the place is a road point (no building matched at all).</summary>
        public ushort Building;
        /// <summary>The building's CS1 name, or <see cref="PlaceFinder.NowhereName"/> for a road point.</summary>
        public string Name = PlaceFinder.NowhereName;
        /// <summary>Ground point missions use: the building's first door, or the road point.</summary>
        public Vec3 Door;
        /// <summary>Street near the door (<c>ICity.StreetNameNear(Door, 60)</c>), or <see cref="PlaceFinder.UnnamedStreet"/>.</summary>
        public string Street = PlaceFinder.UnnamedStreet;
        /// <summary>District id at the door.</summary>
        public byte DistrictId;
        /// <summary><c>ICity.DistrictName(DistrictId)</c> (the city's name for district 0).</summary>
        public string District = "";
        /// <summary>The building's service (Other for a road point).</summary>
        public ServiceKind Service;
        /// <summary>The building's interior archetype (<c>InteriorGenerator.ArchetypeFor</c>), None for a road point.</summary>
        public InteriorArchetype Archetype;
        /// <summary>True when the preferred kind was missing and a fallback rule chose this place.</summary>
        public bool FallbackUsed;
    }

    /// <summary>The fixed places of a campaign, chosen once per city by <see cref="PlaceFinder.FindCampaignPlaces"/>.</summary>
    public sealed class CampaignPlaces
    {
        /// <summary>Vince's garage (contact building of V1-V3).</summary>
        public CampaignPlace Garage;
        /// <summary>The police station (contact building of R1-R3).</summary>
        public CampaignPlace PoliceStation;
        /// <summary>City hall (contact building of C1-C3).</summary>
        public CampaignPlace CityHall;
        /// <summary>Mean position of every building in the city (the "centre" fallbacks measure from).</summary>
        public Vec3 CityCentre;
    }

    /// <summary>
    /// Chooses real buildings for missions and jobs, with the fallbacks of the G9 contract. Deterministic: every choice
    /// depends only on the city's answers and the given point or <see cref="Rng"/>. Ties are broken by lower building id.
    /// Allocates (one <see cref="CampaignPlace"/> per answer): call on mission or round start, never per step.
    /// <para>
    /// "Nearest" means nearest door (XZ distance) to the reference point. The candidate set of a kind is
    /// <c>ICity.ServiceBuildings(kind)</c> minus Abandoned buildings and minus excluded ids where a method takes them.
    /// When every rule of a method finds nothing, the answer is the most central building of the city, and when the
    /// city has no buildings at all, a road point: the road graph point nearest the reference (<c>TryNearestPoint</c>,
    /// 2 km), else the reference point itself, named <see cref="NowhereName"/>.
    /// </para>
    /// </summary>
    public sealed class PlaceFinder
    {
        /// <summary>Name of a place that is not a building.</summary>
        public const string NowhereName = "the empty lot";
        /// <summary>Street name used when the city has none near a place.</summary>
        public const string UnnamedStreet = "an unnamed road";
        /// <summary>Street search radius around a door, metres.</summary>
        public const float StreetRadius = 60f;

        /// <summary>Creates a finder over a city and its road graph (graph may be empty, not null).</summary>
        /// <exception cref="ArgumentNullException">city or roads is null.</exception>
        public PlaceFinder(ICity city, RoadGraph roads)
        {
            if (city == null) throw new ArgumentNullException("city");
            if (roads == null) throw new ArgumentNullException("roads");
            City = city;
            Roads = roads;
        }

        /// <summary>The city.</summary>
        public ICity City { get; private set; }

        /// <summary>The road graph.</summary>
        public RoadGraph Roads { get; private set; }

        private static readonly ServiceKind[] AllServices = (ServiceKind[])Enum.GetValues(typeof(ServiceKind));
        private readonly List<ushort> _ids = new List<ushort>();

        private static Vec3 DoorOf(BuildingDescriptor b)
        {
            return b.Doors != null && b.Doors.Length > 0 ? b.Doors[0] : b.Position;
        }

        private int DistrictOf(BuildingDescriptor b)
        {
            Vec3 d = DoorOf(b);
            return City.DistrictAt(d.X, d.Z);
        }

        private bool DistrictHasBuildings(byte district)
        {
            for (int k = 0; k < AllServices.Length; k++)
            {
                Collect(AllServices[k], _ids);
                for (int i = 0; i < _ids.Count; i++)
                {
                    BuildingDescriptor b;
                    if (City.TryGetBuilding(_ids[i], out b) && DistrictOf(b) == district) return true;
                }
            }
            return false;
        }

        private void Collect(ServiceKind service, List<ushort> into)
        {
            City.ServiceBuildings(service, into);
            for (int i = into.Count - 1; i >= 0; i--)
            {
                BuildingDescriptor b;
                if (!City.TryGetBuilding(into[i], out b) || b.Abandoned) into.RemoveAt(i);
            }
        }

        private void Locate(CampaignPlace p)
        {
            p.Street = City.StreetNameNear(p.Door, StreetRadius) ?? UnnamedStreet;
            p.DistrictId = City.DistrictAt(p.Door.X, p.Door.Z);
            p.District = City.DistrictName(p.DistrictId) ?? "";
        }

        private CampaignPlace CentralOr(Vec3 point)
        {
            CampaignPlace p = MostCentral(null);
            return p.Building == 0 ? RoadPlace(point) : p;
        }

        private static CampaignPlace Fallback(CampaignPlace p)
        {
            p.FallbackUsed = true;
            return p;
        }

        private static void Use(List<ushort> used, CampaignPlace p)
        {
            if (p.Building != 0) used.Add(p.Building);
        }

        /// <summary>A road-point place named <see cref="NowhereName"/> near <paramref name="point"/>.</summary>
        public CampaignPlace RoadPlace(Vec3 point)
        {
            var p = new CampaignPlace { Door = NearestRoadPoint(point), FallbackUsed = true };
            Locate(p);
            return p;
        }

        private CampaignPlace NearestExcluding(ServiceKind service, InteriorArchetype archetype, Vec3 point, float minDistance, ICollection<ushort> exclude)
        {
            Collect(service, _ids);
            ushort best = 0, bestAny = 0;
            float bestD = float.MaxValue, bestAnyD = float.MaxValue;
            for (int i = 0; i < _ids.Count; i++)
            {
                ushort id = _ids[i];
                if (exclude != null && exclude.Contains(id)) continue;
                BuildingDescriptor b;
                if (!City.TryGetBuilding(id, out b)) continue;
                float d = Vec3.DistanceXZ(DoorOf(b), point);
                if (d < minDistance) continue;
                if (d < bestAnyD) { bestAnyD = d; bestAny = id; }
                if (archetype != InteriorArchetype.None && d < bestD && InteriorGenerator.ArchetypeFor(b) == archetype) { bestD = d; best = id; }
            }
            ushort pick = best != 0 ? best : bestAny;
            return pick == 0 ? null : Describe(pick);
        }

        private CampaignPlace NearestOf(ServiceKind[] services, Vec3 point, ICollection<ushort> exclude)
        {
            CampaignPlace best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < services.Length; i++)
            {
                CampaignPlace p = NearestExcluding(services[i], InteriorArchetype.None, point, 0f, exclude);
                if (p == null) continue;
                float d = Vec3.DistanceXZ(p.Door, point);
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        private CampaignPlace BiggestOffice(ICollection<ushort> exclude)
        {
            Collect(ServiceKind.Office, _ids);
            ushort best = 0;
            float bestV = -1f;
            for (int i = 0; i < _ids.Count; i++)
            {
                BuildingDescriptor b;
                if (exclude.Contains(_ids[i]) || !City.TryGetBuilding(_ids[i], out b)) continue;
                float v = b.Width * b.Length * b.Height;
                if (v > bestV) { bestV = v; best = b.Id; }
            }
            return best == 0 ? null : Describe(best);
        }

        private CampaignPlace PickInBand(ServiceKind[] services, Vec3 point, float minDistance, float maxDistance, Rng rng)
        {
            var inBand = new List<ushort>();
            ushort closest = 0;
            float closestGap = float.MaxValue;
            for (int k = 0; k < services.Length; k++)
            {
                Collect(services[k], _ids);
                for (int i = 0; i < _ids.Count; i++)
                {
                    BuildingDescriptor b;
                    if (!City.TryGetBuilding(_ids[i], out b)) continue;
                    float d = Vec3.DistanceXZ(DoorOf(b), point);
                    if (d >= minDistance && d <= maxDistance) { if (!inBand.Contains(b.Id)) inBand.Add(b.Id); continue; }
                    float gap = d < minDistance ? minDistance - d : d - maxDistance;
                    if (gap < closestGap || (gap == closestGap && b.Id < closest)) { closestGap = gap; closest = b.Id; }
                }
            }
            if (inBand.Count > 0)
            {
                inBand.Sort();
                return Describe(inBand[rng.Range(0, inBand.Count)]);
            }
            return closest == 0 ? null : Describe(closest);
        }

        /// <summary>
        /// The three contact buildings, each distinct from the others while the city has another candidate:
        /// <list type="bullet">
        /// <item>Garage: the Industrial building with archetype Garage nearest the city centre, else any Industrial
        /// building nearest the centre, else the most central building.</item>
        /// <item>PoliceStation: the Police building nearest the centre, else the most central building (excluding the
        /// garage).</item>
        /// <item>CityHall: the Government or Monument building nearest the centre, else the Office building with the
        /// largest Width × Length × Height, else the most central building (excluding the other two).</item>
        /// </list>
        /// </summary>
        public CampaignPlaces FindCampaignPlaces()
        {
            Vec3 centre = CityCentre();
            var used = new List<ushort>();
            CampaignPlace garage = NearestExcluding(ServiceKind.Industrial, InteriorArchetype.Garage, centre, 0f, used);
            if (garage == null) garage = Fallback(MostCentral(used));
            else if (garage.Archetype != InteriorArchetype.Garage) garage.FallbackUsed = true;
            Use(used, garage);
            CampaignPlace police = NearestExcluding(ServiceKind.Police, InteriorArchetype.None, centre, 0f, used);
            if (police == null) police = Fallback(MostCentral(used));
            Use(used, police);
            CampaignPlace hall = NearestOf(new[] { ServiceKind.Government, ServiceKind.Monument }, centre, used);
            if (hall == null)
            {
                hall = BiggestOffice(used);
                if (hall == null) hall = MostCentral(used);
                hall.FallbackUsed = true;
            }
            return new CampaignPlaces { Garage = garage, PoliceStation = police, CityHall = hall, CityCentre = centre };
        }

        /// <summary>Mean ground position of the buildings of every <see cref="ServiceKind"/>; Vec3.Zero when there are none.</summary>
        public Vec3 CityCentre()
        {
            double x = 0, z = 0;
            int n = 0;
            for (int k = 0; k < AllServices.Length; k++)
            {
                Collect(AllServices[k], _ids);
                for (int i = 0; i < _ids.Count; i++)
                {
                    BuildingDescriptor b;
                    if (!City.TryGetBuilding(_ids[i], out b)) continue;
                    x += b.Position.X;
                    z += b.Position.Z;
                    n++;
                }
            }
            return n == 0 ? Vec3.Zero : new Vec3((float)(x / n), 0f, (float)(z / n));
        }

        /// <summary>The building whose door is nearest <see cref="CityCentre"/>, skipping <paramref name="exclude"/> (may be null).</summary>
        public CampaignPlace MostCentral(ICollection<ushort> exclude)
        {
            Vec3 centre = CityCentre();
            ushort best = 0;
            float bestD = float.MaxValue;
            for (int k = 0; k < AllServices.Length; k++)
            {
                Collect(AllServices[k], _ids);
                for (int i = 0; i < _ids.Count; i++)
                {
                    ushort id = _ids[i];
                    if (exclude != null && exclude.Contains(id)) continue;
                    BuildingDescriptor b;
                    if (!City.TryGetBuilding(id, out b)) continue;
                    float d = Vec3.DistanceXZ(DoorOf(b), centre);
                    if (d < bestD || (d == bestD && id < best)) { bestD = d; best = id; }
                }
            }
            return best == 0 ? RoadPlace(centre) : Describe(best);
        }

        /// <summary>Describes one building as a place (Name, first door, street, district, service, archetype); null when it does not exist.</summary>
        public CampaignPlace Describe(ushort building)
        {
            BuildingDescriptor b;
            if (building == 0 || !City.TryGetBuilding(building, out b)) return null;
            var p = new CampaignPlace
            {
                Building = b.Id,
                Name = string.IsNullOrEmpty(b.Name) ? NowhereName : b.Name,
                Door = DoorOf(b),
                Service = b.Service,
                Archetype = InteriorGenerator.ArchetypeFor(b),
            };
            Locate(p);
            return p;
        }

        /// <summary>
        /// Nearest building of <paramref name="service"/> to <paramref name="point"/> whose door is at least
        /// <paramref name="minDistance"/> m away, preferring archetype <paramref name="archetype"/> when it is not None
        /// (any archetype of the service when none matches); null when the service has no candidate at that distance.
        /// </summary>
        public CampaignPlace Nearest(ServiceKind service, InteriorArchetype archetype, Vec3 point, float minDistance)
        {
            return NearestExcluding(service, archetype, point, minDistance, null);
        }

        /// <summary><see cref="Nearest(ServiceKind, InteriorArchetype, Vec3, float)"/> skipping the <paramref name="exclude"/> ids (may be null).</summary>
        public CampaignPlace Nearest(ServiceKind service, InteriorArchetype archetype, Vec3 point, float minDistance, ICollection<ushort> exclude)
        {
            return NearestExcluding(service, archetype, point, minDistance, exclude);
        }

        /// <summary>Nearest convenience store (Commercial, archetype ConvenienceStore), else nearest Commercial, else the most central building.</summary>
        public CampaignPlace ConvenienceStore(Vec3 point)
        {
            CampaignPlace p = Nearest(ServiceKind.Commercial, InteriorArchetype.ConvenienceStore, point, 0f);
            if (p != null && p.Archetype != InteriorArchetype.ConvenienceStore) p.FallbackUsed = true;
            return p ?? Fallback(CentralOr(point));
        }

        /// <summary>Nearest Health building, else nearest Commercial building, else the most central building.</summary>
        public CampaignPlace Hospital(Vec3 point)
        {
            CampaignPlace p = Nearest(ServiceKind.Health, InteriorArchetype.None, point, 0f);
            if (p != null) return p;
            p = Nearest(ServiceKind.Commercial, InteriorArchetype.None, point, 0f);
            return Fallback(p ?? CentralOr(point));
        }

        /// <summary>
        /// Nearest Industrial building with archetype Warehouse at least <paramref name="minDistance"/> m away, else any
        /// Industrial, else Commercial at that distance, else the most central building.
        /// </summary>
        public CampaignPlace Warehouse(Vec3 point, float minDistance)
        {
            CampaignPlace p = Nearest(ServiceKind.Industrial, InteriorArchetype.Warehouse, point, minDistance);
            if (p != null)
            {
                if (p.Archetype != InteriorArchetype.Warehouse) p.FallbackUsed = true;
                return p;
            }
            p = Nearest(ServiceKind.Commercial, InteriorArchetype.None, point, minDistance);
            return Fallback(p ?? CentralOr(point));
        }

        /// <summary>
        /// Nearest Industrial building at least <paramref name="minDistance"/> m away (R3's hideout, delivery pick-ups),
        /// else nearest Office, else the most central building.
        /// </summary>
        public CampaignPlace Industrial(Vec3 point, float minDistance)
        {
            CampaignPlace p = Nearest(ServiceKind.Industrial, InteriorArchetype.None, point, minDistance);
            if (p != null) return p;
            p = Nearest(ServiceKind.Office, InteriorArchetype.None, point, minDistance);
            return Fallback(p ?? CentralOr(point));
        }

        /// <summary>
        /// A building of any of <paramref name="services"/> whose door is between <paramref name="minDistance"/> and
        /// <paramref name="maxDistance"/> m from <paramref name="point"/>, chosen uniformly with <paramref name="rng"/>
        /// among candidates sorted by id; when none is in the band, the candidate whose distance is closest to the band;
        /// when the services have no buildings, any building by the same rule; else a road point.
        /// </summary>
        public CampaignPlace InBand(ServiceKind[] services, Vec3 point, float minDistance, float maxDistance, Rng rng)
        {
            CampaignPlace p = PickInBand(services, point, minDistance, maxDistance, rng);
            if (p != null) return p;
            p = PickInBand(AllServices, point, minDistance, maxDistance, rng);
            if (p != null)
            {
                p.FallbackUsed = true;
                return p;
            }
            return RoadPlace(point);
        }

        /// <summary>
        /// A point in the hottest district (largest <c>PlayerProfile.DistrictHeat</c> among districts that have buildings;
        /// ties → lower id): the door of the Residential or Commercial building of that district nearest its buildings'
        /// mean position. With no district heat at all, <see cref="InBand"/>(Residential/Commercial, point, 500, 1200).
        /// </summary>
        public CampaignPlace HottestDistrict(Profile.PlayerProfile profile, Vec3 point, Rng rng)
        {
            int best = -1;
            float bestHeat = 0f;
            foreach (KeyValuePair<int, float> kv in profile.DistrictHeat)
            {
                if (kv.Value <= 0f || kv.Key < 0 || kv.Key > 255) continue;
                if (kv.Value > bestHeat || (kv.Value == bestHeat && kv.Key < best))
                {
                    if (!DistrictHasBuildings((byte)kv.Key)) continue;
                    best = kv.Key;
                    bestHeat = kv.Value;
                }
            }
            if (best >= 0)
            {
                double x = 0, z = 0;
                int n = 0;
                var ids = new List<ushort>();
                foreach (ServiceKind s in new[] { ServiceKind.Residential, ServiceKind.Commercial })
                {
                    Collect(s, _ids);
                    for (int i = 0; i < _ids.Count; i++)
                    {
                        BuildingDescriptor b;
                        if (City.TryGetBuilding(_ids[i], out b) && DistrictOf(b) == best)
                        {
                            ids.Add(b.Id);
                            x += b.Position.X;
                            z += b.Position.Z;
                            n++;
                        }
                    }
                }
                if (n > 0)
                {
                    var mean = new Vec3((float)(x / n), 0f, (float)(z / n));
                    ushort pick = 0;
                    float bestD = float.MaxValue;
                    for (int i = 0; i < ids.Count; i++)
                    {
                        BuildingDescriptor b;
                        City.TryGetBuilding(ids[i], out b);
                        float d = Vec3.DistanceXZ(DoorOf(b), mean);
                        if (d < bestD || (d == bestD && ids[i] < pick)) { bestD = d; pick = ids[i]; }
                    }
                    return Describe(pick);
                }
            }
            return InBand(new[] { ServiceKind.Residential, ServiceKind.Commercial }, point, 500f, 1200f, rng);
        }

        /// <summary>The road point nearest <paramref name="point"/> within 2 km (<c>RoadGraph.TryNearestPoint</c>), else the point itself.</summary>
        public Vec3 NearestRoadPoint(Vec3 point)
        {
            RoadPoint rp;
            return Roads.TryNearestPoint(point, 2000f, out rp) ? rp.Position : point;
        }

        /// <summary>
        /// A road point <paramref name="distance"/> m (±15 %) from <paramref name="point"/> on a seeded bearing (up to 12
        /// tries, then the nearest road point to the last try). Used for chase starts and race targets.
        /// </summary>
        public Vec3 RoadPointAt(Vec3 point, float distance, Rng rng)
        {
            Vec3 last = point;
            for (int i = 0; i < 12; i++)
            {
                double bearing = rng.Range(0f, 360f) * MathUtil.Deg2Rad;
                float d = distance * rng.Range(0.85f, 1.15f);
                last = new Vec3(point.X + (float)Math.Sin(bearing) * d, point.Y, point.Z + (float)Math.Cos(bearing) * d);
                RoadPoint rp;
                if (!Roads.TryNearestPoint(last, 0.3f * distance, out rp)) continue;
                float got = Vec3.DistanceXZ(rp.Position, point);
                if (got >= 0.5f * distance && got <= 1.5f * distance) return rp.Position;
            }
            return NearestRoadPoint(last);
        }
    }
}
