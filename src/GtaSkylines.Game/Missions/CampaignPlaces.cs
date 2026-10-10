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
            throw new NotImplementedException();
        }

        /// <summary>Mean ground position of the buildings of every <see cref="ServiceKind"/>; Vec3.Zero when there are none.</summary>
        public Vec3 CityCentre()
        {
            throw new NotImplementedException();
        }

        /// <summary>The building whose door is nearest <see cref="CityCentre"/>, skipping <paramref name="exclude"/> (may be null).</summary>
        public CampaignPlace MostCentral(ICollection<ushort> exclude)
        {
            throw new NotImplementedException();
        }

        /// <summary>Describes one building as a place (Name, first door, street, district, service, archetype); null when it does not exist.</summary>
        public CampaignPlace Describe(ushort building)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Nearest building of <paramref name="service"/> to <paramref name="point"/> whose door is at least
        /// <paramref name="minDistance"/> m away, preferring archetype <paramref name="archetype"/> when it is not None
        /// (any archetype of the service when none matches); null when the service has no candidate at that distance.
        /// </summary>
        public CampaignPlace Nearest(ServiceKind service, InteriorArchetype archetype, Vec3 point, float minDistance)
        {
            throw new NotImplementedException();
        }

        /// <summary>Nearest convenience store (Commercial, archetype ConvenienceStore), else nearest Commercial, else the most central building.</summary>
        public CampaignPlace ConvenienceStore(Vec3 point)
        {
            throw new NotImplementedException();
        }

        /// <summary>Nearest Health building, else nearest Commercial building, else the most central building.</summary>
        public CampaignPlace Hospital(Vec3 point)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Nearest Industrial building with archetype Warehouse at least <paramref name="minDistance"/> m away, else any
        /// Industrial, else Commercial at that distance, else the most central building.
        /// </summary>
        public CampaignPlace Warehouse(Vec3 point, float minDistance)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Nearest Industrial building at least <paramref name="minDistance"/> m away (R3's hideout, delivery pick-ups),
        /// else nearest Office, else the most central building.
        /// </summary>
        public CampaignPlace Industrial(Vec3 point, float minDistance)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// A building of any of <paramref name="services"/> whose door is between <paramref name="minDistance"/> and
        /// <paramref name="maxDistance"/> m from <paramref name="point"/>, chosen uniformly with <paramref name="rng"/>
        /// among candidates sorted by id; when none is in the band, the candidate whose distance is closest to the band;
        /// when the services have no buildings, any building by the same rule; else a road point.
        /// </summary>
        public CampaignPlace InBand(ServiceKind[] services, Vec3 point, float minDistance, float maxDistance, Rng rng)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// A point in the hottest district (largest <c>PlayerProfile.DistrictHeat</c> among districts that have buildings;
        /// ties → lower id): the door of the Residential or Commercial building of that district nearest its buildings'
        /// mean position. With no district heat at all, <see cref="InBand"/>(Residential/Commercial, point, 500, 1200).
        /// </summary>
        public CampaignPlace HottestDistrict(Profile.PlayerProfile profile, Vec3 point, Rng rng)
        {
            throw new NotImplementedException();
        }

        /// <summary>The road point nearest <paramref name="point"/> within 2 km (<c>RoadGraph.TryNearestPoint</c>), else the point itself.</summary>
        public Vec3 NearestRoadPoint(Vec3 point)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// A road point <paramref name="distance"/> m (±15 %) from <paramref name="point"/> on a seeded bearing (up to 12
        /// tries, then the nearest road point to the last try). Used for chase starts and race targets.
        /// </summary>
        public Vec3 RoadPointAt(Vec3 point, float distance, Rng rng)
        {
            throw new NotImplementedException();
        }
    }
}
