using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Vehicles;

namespace GtaSkylines.Game.World
{
    /// <summary>Who a Game car belongs to.</summary>
    public enum CarOwner
    {
        /// <summary>Driven or last driven by the player and not yet abandoned.</summary>
        Player = 0,
        /// <summary>A police unit (G8).</summary>
        Police = 1,
        /// <summary>A promoted CS1 vehicle with its civilian driver, or a carjacked car nobody drives yet.</summary>
        Civilian = 2,
        /// <summary>Spawned by a mission or job (G9); never removed by distance while the owner keeps it.</summary>
        Mission = 3,
        /// <summary>Left empty by the player or a dead driver; removed when &gt; 300 m away and unseen for 10 s.</summary>
        Abandoned = 4,
    }

    /// <summary>
    /// A vehicle the Game simulates with a G5 <see cref="VehicleBody"/>. Created only through
    /// <c>GameWorld.SpawnCar</c>/<c>PromoteVehicle</c>. Its collision box (body half extents, tag
    /// <c>WorldTag.ForCar(Id)</c>) is in the collision world for every query outside the Vehicles system's own stepping.
    /// </summary>
    public sealed class Car
    {
        /// <summary>Maximum seats tracked (driver + 7 passengers).</summary>
        public const int MaxSeats = 8;

        /// <summary>Creates a car; <see cref="GameWorld"/> calls this.</summary>
        internal Car(int id, VehicleModel model, VehicleBody body)
        {
            Id = id;
            Model = model;
            Body = body;
            Seats = new int[MaxSeats];
            Plate = "";
        }

        /// <summary>Stable id, unique among peds and cars of the session, never reused.</summary>
        public readonly int Id;
        /// <summary>Model (size, kind, name, siren).</summary>
        public readonly VehicleModel Model;
        /// <summary>Physics body: pose (bounds centre), velocity, health, damage state.</summary>
        public readonly VehicleBody Body;
        /// <summary>Paint 0xRRGGBB (resprays change it).</summary>
        public int ColourRgb;
        /// <summary>Owner.</summary>
        public CarOwner Owner;
        /// <summary>Ped id per seat (index 0 driver), 0 empty; only the first <c>Model.Seats</c> (≤ <see cref="MaxSeats"/>) are used.</summary>
        public readonly int[] Seats;
        /// <summary>Driver ped id (= Seats[0]), 0 none.</summary>
        public int DriverId { get { return Seats[0]; } }
        /// <summary>Siren sounding (emergency models only).</summary>
        public bool Siren;
        /// <summary>Light bar flashing (emergency models only; on with the siren, may stay on alone).</summary>
        public bool LightBar;
        /// <summary>Headlights on.</summary>
        public bool Headlights;
        /// <summary>Horn sounding this step.</summary>
        public bool Horn;
        /// <summary>AI control for a car whose driver is not the player; null = the car brakes (handbrake on when nobody sits at the wheel).</summary>
        public IDriver Driver;
        /// <summary>Controls applied in the last step (the player's or the driver's), for lights and audio.</summary>
        public VehicleControls Controls;
        /// <summary>CS1 vehicle (or parked vehicle) id it was taken from; 0 when spawned.</summary>
        public ushort SourceCs1Vehicle;
        /// <summary>True when <see cref="SourceCs1Vehicle"/> was a parked vehicle.</summary>
        public bool SourceParked;
        /// <summary>Locked: the player cannot enter (mission cars, police cars at roadblocks).</summary>
        public bool Locked;
        /// <summary>Seconds this car has been Abandoned, &gt; 300 m away and not in view (removed at 10 s).</summary>
        public float AbandonedSeconds;
        /// <summary>F-VEH-8: a BlockLane is in force for this car.</summary>
        internal bool LaneBlocking;
        /// <summary>F-VEH-8: where the car stood when it started blocking.</summary>
        internal Vec3 LaneBlockAnchor;
        /// <summary>F-VEH-8: seconds blocked during this abandonment.</summary>
        internal float LaneBlockSeconds;
        /// <summary>Number plate text (seeded from the id, 7 characters).</summary>
        public string Plate;

        /// <summary>Bounds centre at the start of the latest simulation step (set by the session), for render interpolation.</summary>
        public Vec3 PrevPosition;
        /// <summary>Rotation at the start of the latest simulation step.</summary>
        public Quat PrevRotation = Quat.Identity;
        /// <summary>Bounds centre, world space (the body's).</summary>
        public Vec3 Position { get { return Body.Position; } }
        /// <summary>Orientation (the body's).</summary>
        public Quat Rotation { get { return Body.Rotation; } }
        /// <summary>This car's collision and event tag.</summary>
        public int Tag { get { return WorldTag.ForCar(Id); } }
        /// <summary>True once the body exploded.</summary>
        public bool Destroyed { get { return Body.DamageState == VehicleDamageState.Exploded; } }
    }
}
