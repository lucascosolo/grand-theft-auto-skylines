using System;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Missions
{
    /// <summary>The campaign's mission ids, as stored in <c>PlayerProfile.CompletedMissions</c>.</summary>
    public static class MissionIds
    {
        /// <summary>Vince 1, "Fresh Start".</summary>
        public const string V1 = "V1";
        /// <summary>Vince 2, "Hot Wheels".</summary>
        public const string V2 = "V2";
        /// <summary>Vince 3, "Clean Getaway".</summary>
        public const string V3 = "V3";
        /// <summary>Reyes 1, "Ride Along".</summary>
        public const string R1 = "R1";
        /// <summary>Reyes 2, "Evidence Locker".</summary>
        public const string R2 = "R2";
        /// <summary>Reyes 3, "Takedown".</summary>
        public const string R3 = "R3";
        /// <summary>Mayor's Office 1, "Code Red".</summary>
        public const string C1 = "C1";
        /// <summary>Mayor's Office 2, "Five-Alarm".</summary>
        public const string C2 = "C2";
        /// <summary>Mayor's Office 3, "Gridlock" (finale).</summary>
        public const string C3 = "C3";

        /// <summary>All nine, in campaign order (V1..V3, R1..R3, C1..C3).</summary>
        public static readonly string[] All = { V1, V2, V3, R1, R2, R3, C1, C2, C3 };
    }

    /// <summary>A mission contact.</summary>
    public enum ContactId
    {
        /// <summary>Vince, a street hustler (garage).</summary>
        Vince,
        /// <summary>Sergeant Reyes (police station).</summary>
        Reyes,
        /// <summary>The Mayor's Office (city hall).</summary>
        MayorsOffice,
    }

    /// <summary>Mission-level fail conditions checked by the runner every step, in this order.</summary>
    [Flags]
    public enum MissionFail
    {
        /// <summary>None beyond the always-on ones.</summary>
        None = 0,
        /// <summary>The player died (<see cref="GameEventType.PlayerDied"/>). Always on.</summary>
        Wasted = 1,
        /// <summary>The player was busted (<see cref="GameEventType.PlayerArrested"/>). Always on.</summary>
        Busted = 2,
        /// <summary>The car in <see cref="MissionDefinition.VehicleSlot"/> exploded or was removed.</summary>
        VehicleDestroyed = 4,
        /// <summary>The actor in <see cref="MissionDefinition.TargetSlot"/> is farther than <see cref="MissionDefinition.EscapeDistance"/> from the player.</summary>
        TargetEscaped = 8,
        /// <summary>The ped in <see cref="MissionDefinition.PassengerSlot"/> died or was removed.</summary>
        PassengerKilled = 16,
    }

    /// <summary>Failure reasons shown on the MISSION FAILED screen (one cached string each).</summary>
    public static class FailReasons
    {
        /// <summary>Wasted.</summary>
        public const string Wasted = "You were wasted.";
        /// <summary>Busted.</summary>
        public const string Busted = "You got busted.";
        /// <summary>Mission vehicle destroyed.</summary>
        public const string VehicleDestroyed = "The vehicle was destroyed.";
        /// <summary>Target escaped.</summary>
        public const string TargetEscaped = "The target got away.";
        /// <summary>Passenger killed.</summary>
        public const string PassengerKilled = "Your passenger is dead.";
        /// <summary>Timed step ran out.</summary>
        public const string TimeOut = "You ran out of time.";
        /// <summary>Delivered car below the required health.</summary>
        public const string CarTooDamaged = "The car is too beat up.";
        /// <summary>A no-wanted step saw stars.</summary>
        public const string Spotted = "You were made by the cops.";
        /// <summary>The player abandoned the mission vehicle for too long.</summary>
        public const string VehicleAbandoned = "You abandoned the vehicle.";
        /// <summary>A side job's vehicle was left.</summary>
        public const string LeftJobVehicle = "You left the vehicle.";
        /// <summary>The player cancelled from the phone or interaction menu.</summary>
        public const string Cancelled = "Job cancelled.";
    }

    /// <summary>What a mission pays.</summary>
    public struct MissionReward
    {
        /// <summary>Cash added with <c>Wallet.Earn</c>.</summary>
        public long Cash;
        /// <summary>Wallet reason, "Mission: <i>Title</i>" (built once).</summary>
        public string Reason;
    }

    /// <summary>A subtitle line of a briefing or a Talk step.</summary>
    public struct DialogueLine
    {
        /// <summary>Speaker name (null for narration).</summary>
        public string Speaker;
        /// <summary>The line (placeholders already filled).</summary>
        public string Text;
        /// <summary>Seconds on screen.</summary>
        public float Seconds;

        /// <summary>Creates a line.</summary>
        public DialogueLine(string speaker, string text, float seconds)
        {
            Speaker = speaker;
            Text = text;
            Seconds = seconds;
        }
    }

    /// <summary>What kind of actor a <see cref="MissionActorSpec"/> spawns.</summary>
    public enum ActorKind
    {
        /// <summary>A Game car (<c>GameWorld.SpawnCar</c>, Owner Mission).</summary>
        Car,
        /// <summary>A Game ped (<c>GameWorld.SpawnPed</c>).</summary>
        Ped,
    }

    /// <summary>How a mission car is driven.</summary>
    public enum ActorDriving
    {
        /// <summary>Parked: no driver, no <see cref="IDriver"/>.</summary>
        Parked,
        /// <summary>A <see cref="FleeDriver"/> (chase targets): needs a ped in seat 0.</summary>
        Flee,
        /// <summary>A G6 <see cref="RouteDriver"/> towards <see cref="MissionActorSpec.DriveTo"/>.</summary>
        DriveTo,
    }

    /// <summary>
    /// A mission actor, spawned by the runner when step <see cref="SpawnAtStep"/> begins (before that step's
    /// <c>Begin</c>) and removed when the mission ends (pass or fail; a car the player is sitting in is set to Owner
    /// Abandoned instead of removed, and a ped seated in the player's car is removed with returnCitizen true).
    /// The spawned actor's id goes into <c>MissionContext.ActorIds[slot]</c>, where slot is the index of this spec in
    /// <see cref="MissionDefinition.Actors"/>.
    /// </summary>
    public sealed class MissionActorSpec
    {
        /// <summary>Car or ped.</summary>
        public ActorKind Kind;
        /// <summary>Step index whose start spawns the actor (0 = at mission start).</summary>
        public int SpawnAtStep;
        /// <summary>Ground point (car: under its centre; ped: feet).</summary>
        public Vec3 Position;
        /// <summary>Facing, degrees.</summary>
        public float YawDeg;
        /// <summary>Blip shown while the actor lives (follows it), or null for none.</summary>
        public BlipKind? Blip;

        /// <summary>Car: model kind (<c>ICity.FindVehicleModel(kind, Seed)</c>); when that is −1 the Car kind is tried, then the actor is skipped.</summary>
        public VehicleKind VehicleKind;
        /// <summary>Car: colour 0xRRGGBB, or −1 for the model's own random colour (seeded by <see cref="Seed"/>).</summary>
        public int ColourRgb = -1;
        /// <summary>Car: locked doors (the player must break in: G6 rules).</summary>
        public bool Locked;
        /// <summary>Car: how it is driven.</summary>
        public ActorDriving Driving;
        /// <summary>Car with <see cref="ActorDriving.DriveTo"/>: destination.</summary>
        public Vec3 DriveTo;
        /// <summary>Car: max speed factor of the AI (×RouteFollower limit), 1 default.</summary>
        public float SpeedFactor = 1f;

        /// <summary>Ped: kind (Civilian, Cop, Npc).</summary>
        public PedKind PedKind = PedKind.Civilian;
        /// <summary>Ped: citizen model id, or −1 for a seeded pick over <c>ICity.CitizenModelCount</c>.</summary>
        public int Model = -1;
        /// <summary>Ped: CS1 citizen this ped stands for (patients), 0 none.</summary>
        public uint Citizen;
        /// <summary>Ped: hostile to the player (armed brain from G7; Eliminate targets).</summary>
        public bool Hostile;
        /// <summary>Ped: weapon slot given to a hostile (G7 <see cref="WeaponId"/>).</summary>
        public WeaponId Weapon;
        /// <summary>Ped: armour 0-100.</summary>
        public float Armour;
        /// <summary>Ped: slot of a car actor to seat it in (−1 on foot). The car must spawn at the same step or earlier.</summary>
        public int InCarSlot = -1;
        /// <summary>Ped: seat in that car (0 = driver).</summary>
        public int Seat;

        /// <summary>Seed for model and colour picks.</summary>
        public uint Seed;
    }

    /// <summary>
    /// A mission: data plus a list of steps, built by code from templates and the city (<see cref="CampaignGenerator"/>).
    /// Immutable once built except for the steps' own runtime fields, which <c>MissionStep.Begin</c> resets, so a
    /// definition can be run again after a failure.
    /// </summary>
    public sealed class MissionDefinition
    {
        /// <summary>Id (<see cref="MissionIds"/>).</summary>
        public string Id;
        /// <summary>Title shown on the HUD and the passed screen.</summary>
        public string Title;
        /// <summary>Contact.</summary>
        public ContactId Contact;
        /// <summary>Missions that must all be complete before this one is available.</summary>
        public string[] Requires = new string[0];
        /// <summary>The contact building the mission starts from.</summary>
        public CampaignPlace StartPlace;
        /// <summary>Ground centre of the start marker (the building door).</summary>
        public Vec3 StartMarker;
        /// <summary>Subtitle lines queued when the mission starts.</summary>
        public DialogueLine[] Briefing = new DialogueLine[0];
        /// <summary>The steps, run in order.</summary>
        public MissionStep[] Steps = new MissionStep[0];
        /// <summary>Actors (slot = index).</summary>
        public MissionActorSpec[] Actors = new MissionActorSpec[0];
        /// <summary>Extra fail conditions (Wasted and Busted are always on).</summary>
        public MissionFail Fail;
        /// <summary>Car slot for <see cref="MissionFail.VehicleDestroyed"/>, −1 none.</summary>
        public int VehicleSlot = -1;
        /// <summary>Actor slot for <see cref="MissionFail.TargetEscaped"/>, −1 none.</summary>
        public int TargetSlot = -1;
        /// <summary>Distance for <see cref="MissionFail.TargetEscaped"/>, metres.</summary>
        public float EscapeDistance = 400f;
        /// <summary>Ped slot for <see cref="MissionFail.PassengerKilled"/>, −1 none.</summary>
        public int PassengerSlot = -1;
        /// <summary>Reward.</summary>
        public MissionReward Reward;
        /// <summary>Phone text the contact sends when the mission becomes available.</summary>
        public string UnlockText;
        /// <summary>Phone text the contact sends after the mission is passed, or null.</summary>
        public string PassedText;
        /// <summary>What the contact says when called while this is the next mission (one subtitle line).</summary>
        public string CallLine;

        /// <summary>True when every id in <see cref="Requires"/> is complete in the profile and this mission is not.</summary>
        public bool IsAvailable(PlayerProfile profile)
        {
            if (profile.IsMissionComplete(Id)) return false;
            for (int i = 0; i < Requires.Length; i++) if (!profile.IsMissionComplete(Requires[i])) return false;
            return true;
        }
    }

    /// <summary>Contact names and their phone/speaker identities.</summary>
    public static class Contacts
    {
        /// <summary>Phone and subtitle name of a contact: "Vince", "Sgt. Reyes", "Mayor's Office".</summary>
        public static string Name(ContactId id)
        {
            switch (id)
            {
                case ContactId.Vince: return CampaignText.VinceName;
                case ContactId.Reyes: return CampaignText.ReyesName;
                default: return CampaignText.MayorName;
            }
        }
    }
}
