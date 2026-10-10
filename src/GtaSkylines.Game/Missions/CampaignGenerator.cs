using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Roads;

namespace GtaSkylines.Game.Missions
{
    /// <summary>The nine generated missions and their places.</summary>
    public sealed class Campaign
    {
        /// <summary>Creates a campaign over generated parts.</summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public Campaign(CampaignPlaces places, IList<MissionDefinition> missions)
        {
            if (places == null) throw new ArgumentNullException("places");
            if (missions == null) throw new ArgumentNullException("missions");
            Places = places;
            Missions = missions;
        }

        /// <summary>The contact buildings.</summary>
        public CampaignPlaces Places { get; private set; }

        /// <summary>The missions in <see cref="MissionIds.All"/> order.</summary>
        public IList<MissionDefinition> Missions { get; private set; }

        /// <summary>The mission with an id, or null.</summary>
        public MissionDefinition Find(string id)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Unlock rules (<see cref="MissionDefinition.Requires"/>): V1 none; V2 V1; V3 V2; R1 V2; R2 R1; R3 R2; C1 R1;
        /// C2 C1; C3 C2, V3 and R3. Available missions = <see cref="MissionDefinition.IsAvailable"/>, in campaign order,
        /// appended to <paramref name="into"/> (cleared first).
        /// </summary>
        public void Available(PlayerProfile profile, List<MissionDefinition> into)
        {
            throw new NotImplementedException();
        }

        /// <summary>A contact's next available mission (first in order), or null.</summary>
        public MissionDefinition NextFor(ContactId contact, PlayerProfile profile)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Builds the campaign from the city. <see cref="Generate"/> runs once per session (all nine missions, so markers,
    /// phone texts and tests can see them); <see cref="Rebuild"/> rebuilds one mission when it starts, so dynamic
    /// choices (patients, fires, hot district, parked car spot) use the city as it is now. Both are deterministic for
    /// (city answers, seed, reference point). Every text is filled with <see cref="TextVars"/>: <c>{contact}</c> family
    /// from the contact building; <c>{place}</c>/<c>{place2}</c> as documented per mission; <c>{city}</c> =
    /// <c>ICity.CityName</c>. Rewards: V1 $250, V2 $600, V3 $900, R1 $700, R2 $1,000, R3 $1,500, C1 $800,
    /// C2 $900, C3 $5,000. Every mission has <see cref="MissionDefinition.StartMarker"/> = its contact building door and
    /// <see cref="MissionDefinition.StartPlace"/> = that building.
    /// <para>Per mission (steps in order; slot numbers are <see cref="MissionDefinition.Actors"/> indices):</para>
    /// <list type="number">
    /// <item>V1: place = <c>ConvenienceStore(garage door)</c>. GoTo(store door, 3 m, OnFoot) · BuyItem("snack",
    /// store) · StealVehicle(any kind, ParkedOnly, StoreSlot 0) · Deliver(CurrentCar → garage door, 6 m) · Talk(outro).
    /// No actors.</item>
    /// <item>V2: place = <c>InBand(Residential/Commercial, garage door, 600, 1500)</c> preferring a different district
    /// than the garage; actor 0 = a parked Car (VehicleKind.Car, colour 0xD01818, seed-picked model) at
    /// <c>NearestRoadPoint(place door)</c>; {vehicle} = its model name. StealVehicle(slot 0, GuaranteedWitness,
    /// WitnessStars 1) · Deliver(MissionVehicle 0 → garage, 6 m, MinHealth 60) · Talk. Fail VehicleDestroyed slot 0.</item>
    /// <item>V3: place = the nearest Garage-archetype Industrial building to the garage other than the garage itself
    /// (ResprayStep backyard = false), else the garage (backyard = true). SetWanted(2) · LoseWanted · Respray ·
    /// GoTo(garage, 6 m, AnyVehicle) · Talk.</item>
    /// <item>R1: place = <c>HottestDistrict</c>; place2 street = the street at the suspect start. Actor 0 = police Car
    /// (VehicleKind.Police) parked 12 m in front of the station door at the nearest road point; actor 1 = suspect Car
    /// (VehicleKind.Car, Flee, SpeedFactor 1.25) spawned at step 3 at <c>RoadPointAt(place door, 300)</c>; actor 2 =
    /// its driver (Civilian, InCarSlot 1, seat 0). EnterVehicle(slot 0) · GoTo(place door, 40 m, MissionVehicle 0) ·
    /// Talk(radio) · Chase(slot 1) · Talk. Fail VehicleDestroyed 0, TargetEscaped 1.</item>
    /// <item>R2: place2 = <c>Warehouse(station door, 400)</c>. Collect(station, CustomId 9000, SeenStars 2) ·
    /// Deliver(Player → place2 door, 5 m, FailIfWanted) · Talk.</item>
    /// <item>R3: place2 = <c>Industrial(station door, 500)</c>. Actors 0-2 = hostile Civilian peds with Pistol (armour
    /// 0, 25, 50) placed 4 m apart in front of place2's door, spawned at step 1. GoTo(place2 door, 25 m, Any) ·
    /// Eliminate(0, 1, 2) · SetWanted(2) · LoseWanted · Talk.</item>
    /// <item>C1: patient = first of <c>ICity.Patients(city hall door, 3000)</c>; place = its building, {person} = its
    /// profile name; when none, place = <c>InBand(Residential, city hall door, 300, 1200)</c> and the patient is a
    /// spawned ped with no citizen named <see cref="FallbackPatientName"/>. place2 = <c>Hospital(place door)</c>.
    /// Actor 0 = Ambulance car at the city hall road point; actor 1 = patient ped at place door (Citizen = the patient's
    /// citizen), spawned at step 1. EnterVehicle(slot 0) · PickUp(slot 1) · Timed(Deliver(Passenger 1 → hospital door,
    /// 8 m), 30 s + route length / 11 m/s, rounded up to 5 s; straight-line × 1.4 when no route). Fail
    /// VehicleDestroyed 0, PassengerKilled 1.</item>
    /// <item>C2: place = first of <c>ICity.BurningBuildings(city hall door, 3000)</c>; else the building nearest a
    /// seeded point 400-1200 m from city hall (reported fire). Actor 0 = FireTruck at the city hall road point.
    /// Burning: EnterVehicle(0) · GoTo(place door, 20 m, MissionVehicle 0) · HoseFire(place) · Talk(outro).
    /// Reported: EnterVehicle(0) · GoTo(place door, 20 m, MissionVehicle 0) · GoTo(place door, 4 m, OnFoot) ·
    /// Talk(false alarm). Fail VehicleDestroyed 0.</item>
    /// <item>C3: place2 = <c>InBand(any service, city hall door, 1500, 3000)</c>. Actor 0 = Theo (Civilian) 3 m from the
    /// city hall door. EnterVehicle(Car, Van, Taxi, Police) (done at once when already driving) · PickUp(0) ·
    /// Talk(ambush) · SetWanted(4) · Deliver(Passenger 0 → place2 door, 8 m) · Talk. Fail PassengerKilled 0.</item>
    /// </list>
    /// </summary>
    public static class CampaignGenerator
    {
        /// <summary>{person} for a spawned fallback patient.</summary>
        public const string FallbackPatientName = "a collapsed pedestrian";

        /// <summary>Custom interaction id of R2's evidence locker.</summary>
        public const int EvidenceInteractionId = 9000;

        /// <summary>Generates all nine missions with places chosen around the contact buildings.</summary>
        /// <exception cref="ArgumentNullException">city, roads or profile is null.</exception>
        public static Campaign Generate(ICity city, RoadGraph roads, PlayerProfile profile, uint seed)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Rebuilds one mission against the current city state with the same contact buildings and seed (same result as
        /// <see cref="Generate"/> when the city has not changed). Unknown id → null.
        /// </summary>
        public static MissionDefinition Rebuild(string id, Campaign campaign, ICity city, RoadGraph roads, PlayerProfile profile, uint seed)
        {
            throw new NotImplementedException();
        }

        /// <summary>The <see cref="TextVars"/> every mission starts from: city name and contact place.</summary>
        public static TextVars BaseVars(ICity city, CampaignPlace contact)
        {
            throw new NotImplementedException();
        }

        /// <summary>Per-mission seed: <c>Rng.Seed(seed, index + 1)</c> where index is the position in <see cref="MissionIds.All"/>.</summary>
        public static uint MissionSeed(uint seed, string id)
        {
            int index = Array.IndexOf(MissionIds.All, id);
            if (index < 0) throw new ArgumentException("unknown mission id", "id");
            return Rng.Seed(seed, (uint)(index + 1));
        }
    }
}
