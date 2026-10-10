using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.City
{
    /// <summary>What a road vehicle is, for taking it, police logic and jobs. The Mod never reports non-road vehicles.</summary>
    public enum VehicleKind
    {
        /// <summary>Private car.</summary>
        Car,
        /// <summary>Van or pickup.</summary>
        Van,
        /// <summary>Cargo truck.</summary>
        Truck,
        /// <summary>Bus.</summary>
        Bus,
        /// <summary>Taxi.</summary>
        Taxi,
        /// <summary>Police car.</summary>
        Police,
        /// <summary>Ambulance.</summary>
        Ambulance,
        /// <summary>Fire engine.</summary>
        FireTruck,
        /// <summary>Hearse.</summary>
        Hearse,
        /// <summary>Garbage truck.</summary>
        Garbage,
        /// <summary>Any other city service vehicle (maintenance, post, snow plough...).</summary>
        Service,
        /// <summary>Police helicopter: never an ambient road vehicle; only returned by <see cref="ICity.FindVehicleModel"/> when the game has one.</summary>
        PoliceHelicopter,
    }

    /// <summary>What a building is used for, from its CS1 service and sub-service.</summary>
    public enum ServiceKind
    {
        /// <summary>Unknown or not classified.</summary>
        Other,
        /// <summary>Homes (low or high density).</summary>
        Residential,
        /// <summary>Shops, restaurants, leisure, tourism.</summary>
        Commercial,
        /// <summary>Offices.</summary>
        Office,
        /// <summary>Industry (generic or specialised).</summary>
        Industrial,
        /// <summary>Police station or headquarters.</summary>
        Police,
        /// <summary>Clinic or hospital.</summary>
        Health,
        /// <summary>Fire station.</summary>
        Fire,
        /// <summary>Schools, universities.</summary>
        Education,
        /// <summary>Garbage, recycling, incineration.</summary>
        Garbage,
        /// <summary>Power plants.</summary>
        Electricity,
        /// <summary>Water and sewage.</summary>
        Water,
        /// <summary>Stations, depots, terminals.</summary>
        Transport,
        /// <summary>Parks and plazas (no interior).</summary>
        Park,
        /// <summary>Monuments, landmarks, unique buildings.</summary>
        Monument,
        /// <summary>City hall and other government buildings.</summary>
        Government,
        /// <summary>Cemetery, crematorium.</summary>
        Deathcare,
    }

    /// <summary>A walking CS1 citizen near the player this frame (a snapshot; CS1 still simulates it).</summary>
    public struct AmbientCitizen
    {
        /// <summary>CS1 citizen instance id (non-zero).</summary>
        public ushort Instance;
        /// <summary>CS1 citizen id (non-zero).</summary>
        public uint Citizen;
        /// <summary>Feet position.</summary>
        public Vec3 Position;
        /// <summary>Velocity in m/s.</summary>
        public Vec3 Velocity;
        /// <summary>Facing, degrees.</summary>
        public float YawDeg;
        /// <summary>Citizen model id (see <see cref="ICity.GetCitizenModel"/>).</summary>
        public int Model;
    }

    /// <summary>A CS1 road vehicle near the player this frame, moving or parked (a snapshot).</summary>
    public struct AmbientVehicle
    {
        /// <summary>CS1 vehicle id, or parked vehicle id when <see cref="Parked"/>.</summary>
        public ushort Id;
        /// <summary>True for a CS1 parked vehicle.</summary>
        public bool Parked;
        /// <summary>Centre of the model's bounds in world space.</summary>
        public Vec3 Position;
        /// <summary>Orientation.</summary>
        public Quat Rotation;
        /// <summary>Velocity in m/s.</summary>
        public Vec3 Velocity;
        /// <summary>Vehicle model id (see <see cref="ICity.GetVehicleModel"/>).</summary>
        public int Model;
        /// <summary>Kind of the model.</summary>
        public VehicleKind Kind;
        /// <summary>CS1 citizen driving it, 0 for none (parked, or a service vehicle without a modelled driver).</summary>
        public uint DriverCitizen;
        /// <summary>Paint colour as CS1 draws it, 0xRRGGBB.</summary>
        public int ColorRgb;
    }

    /// <summary>Everything the Game needs about a vehicle model. Sizes in metres, speeds in m/s.</summary>
    public sealed class VehicleModel
    {
        /// <summary>Model id.</summary>
        public int Id;
        /// <summary>Display name ("Sedan", "Police Car").</summary>
        public string Name = "";
        /// <summary>Kind.</summary>
        public VehicleKind Kind;
        /// <summary>Half the size of the model's bounding box (x width, y height, z length).</summary>
        public Vec3 HalfExtents = new Vec3(0.9f, 0.75f, 2.2f);
        /// <summary>Offset from the vehicle's origin (ground under its centre in CS1) to the bounds centre.</summary>
        public Vec3 CenterOffset = new Vec3(0f, 0.75f, 0f);
        /// <summary>CS1's own top speed for the model.</summary>
        public float MaxSpeed = 30f;
        /// <summary>CS1's own acceleration, m/s².</summary>
        public float Acceleration = 4f;
        /// <summary>CS1's own braking, m/s².</summary>
        public float Braking = 8f;
        /// <summary>True for police, ambulance and fire models (siren and light bar).</summary>
        public bool HasSiren;
        /// <summary>Seats including the driver.</summary>
        public int Seats = 4;
    }

    /// <summary>What a citizen model is dressed as.</summary>
    public enum CitizenModelRole
    {
        /// <summary>Ordinary resident or tourist.</summary>
        Civilian,
        /// <summary>CS1 police officer (PoliceOfficerAI): what cops on foot wear.</summary>
        Police,
        /// <summary>Another CS1 service person in uniform or costume (paramedic, park worker, mascot, ...).</summary>
        Service,
    }

    /// <summary>A citizen model (the player's possible outfits, and every ped's look).</summary>
    public sealed class CitizenModel
    {
        /// <summary>Model id.</summary>
        public int Id;
        /// <summary>CS1 prefab name.</summary>
        public string Name = "";
        /// <summary>True for a female model.</summary>
        public bool Female;
        /// <summary>Age group: 0 child, 1 teen, 2 young adult, 3 adult, 4 senior.</summary>
        public int AgeGroup = 3;
        /// <summary>What the model is dressed as; random civilians never wear police or service models.</summary>
        public CitizenModelRole Role;
    }

    /// <summary>A building as the Game sees it. Positions in world space; the building's local +z is its front.</summary>
    public sealed class BuildingDescriptor
    {
        /// <summary>CS1 building id.</summary>
        public ushort Id;
        /// <summary>Display name (CS1's own building name).</summary>
        public string Name = "";
        /// <summary>CS1 prefab name (seeds generation; same prefab, same family of layouts).</summary>
        public string InfoName = "";
        /// <summary>Use.</summary>
        public ServiceKind Service;
        /// <summary>CS1 sub-service name ("ResidentialHigh", "CommercialLeisure", "IndustrialOil", ...), empty if none.</summary>
        public string SubService = "";
        /// <summary>CS1 level, 1-5 (1 for buildings without levels).</summary>
        public int Level = 1;
        /// <summary>Ground position of the building's origin (centre of its lot).</summary>
        public Vec3 Position;
        /// <summary>Yaw of the building's front (+z local), degrees.</summary>
        public float YawDeg;
        /// <summary>Footprint across the front, metres.</summary>
        public float Width = 16f;
        /// <summary>Footprint front to back, metres.</summary>
        public float Length = 16f;
        /// <summary>Height of the model's bounds, metres.</summary>
        public float Height = 10f;
        /// <summary>Door positions on the ground in world space; never empty (the Mod falls back to the front centre).</summary>
        public Vec3[] Doors = new Vec3[0];
        /// <summary>CS1 district id at the building (0 = none).</summary>
        public byte District;
        /// <summary>True while the building is on fire.</summary>
        public bool Burning;
        /// <summary>True for abandoned, collapsed or otherwise unusable buildings (no interior).</summary>
        public bool Abandoned;
    }

    /// <summary>A CS1 citizen's identity and state, for the ped card, dialogue and jobs.</summary>
    public struct CitizenProfile
    {
        /// <summary>CS1 citizen id.</summary>
        public uint Citizen;
        /// <summary>CS1's generated name.</summary>
        public string Name;
        /// <summary>Age in years.</summary>
        public int Age;
        /// <summary>True for a female citizen.</summary>
        public bool Female;
        /// <summary>Workplace or school building, 0 for none.</summary>
        public ushort WorkBuilding;
        /// <summary>Home building, 0 for none (tourists, homeless).</summary>
        public ushort HomeBuilding;
        /// <summary>0-100.</summary>
        public int Happiness;
        /// <summary>0 low, 1 medium, 2 high.</summary>
        public int Wealth;
        /// <summary>0 uneducated .. 3 highly educated.</summary>
        public int Education;
        /// <summary>0-100.</summary>
        public int Health;
        /// <summary>True when CS1 marks the citizen sick.</summary>
        public bool Sick;
        /// <summary>True for tourists.</summary>
        public bool Tourist;
        /// <summary>True when the citizen has a job (workers) or school place (students).</summary>
        public bool Employed;
        /// <summary>True for students.</summary>
        public bool Student;
    }

    /// <summary>A citizen inside a building right now (for interior occupants).</summary>
    public struct Occupant
    {
        /// <summary>CS1 citizen id.</summary>
        public uint Citizen;
        /// <summary>Citizen model id.</summary>
        public int Model;
        /// <summary>True when the building is their home; false for workers, students, visitors.</summary>
        public bool Resident;
    }

    /// <summary>A sick or injured citizen waiting in a building (paramedic jobs).</summary>
    public struct Patient
    {
        /// <summary>CS1 citizen id.</summary>
        public uint Citizen;
        /// <summary>Building where they are.</summary>
        public ushort Building;
        /// <summary>Door of that building, world space.</summary>
        public Vec3 Pickup;
    }
}
