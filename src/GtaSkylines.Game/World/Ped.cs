using System;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;

namespace GtaSkylines.Game.World
{
    /// <summary>Who a ped is.</summary>
    public enum PedKind
    {
        /// <summary>The player (exactly one per session, <see cref="GameWorld.Player"/>).</summary>
        Player = 0,
        /// <summary>A civilian: a promoted CS1 citizen, a carjacked driver, a mission actor.</summary>
        Civilian = 1,
        /// <summary>A police officer (G8).</summary>
        Cop = 2,
        /// <summary>An interior occupant created from an <c>NpcSpot</c>; exists only while the player is inside.</summary>
        Npc = 3,
    }

    /// <summary>State bits of a ped. Written by the owning system (see each), read by everyone.</summary>
    [Flags]
    public enum PedFlags
    {
        /// <summary>None.</summary>
        None = 0,
        /// <summary>Health reached 0 (G7; G6 for the player). Dead peds never move again.</summary>
        Dead = 1 << 0,
        /// <summary>Lying on the ground after a hit (G7).</summary>
        KnockedDown = 1 << 1,
        /// <summary>Hands raised (G7 brains; G6 for the player holding HandsUp).</summary>
        HandsUp = 1 << 2,
        /// <summary>On the phone (G7 CallPolice).</summary>
        OnPhone = 1 << 3,
        /// <summary>Panicking (G7).</summary>
        Panicking = 1 << 4,
        /// <summary>Sneaking: walk speed, quieter, shorter detection range (G6 for the player).</summary>
        Sneaking = 1 << 5,
        /// <summary>Aiming a weapon (G6 sets it for the player while Aim is held on foot; G7 for others).</summary>
        Aiming = 1 << 6,
        /// <summary>Arrested and in custody (G8).</summary>
        Arrested = 1 << 7,
        /// <summary>Seated in a car (<see cref="Ped.VehicleId"/> ≠ 0); not in the collision world, not drawn as a walker.</summary>
        InVehicle = 1 << 8,
        /// <summary>Not drawn and not in the collision world (taxi fares riding, scripted hiding).</summary>
        Invisible = 1 << 9,
        /// <summary>Hostile to the player: crosshair turns red, lock-on prefers it (G7, G8, G9).</summary>
        Hostile = 1 << 10,
        /// <summary>Injured below 35 health (G7; set once, never cleared).</summary>
        Injured = 1 << 11,
        /// <summary>A mission actor: never demoted by distance while set (G9).</summary>
        Persistent = 1 << 12,
    }

    /// <summary>The animation the renderer plays (M3 maps it to CS1's AnimationState).</summary>
    public enum PedAnim
    {
        /// <summary>Standing.</summary>
        Idle = 0,
        /// <summary>Walking (≤ 2 m/s).</summary>
        Walk = 1,
        /// <summary>Jogging.</summary>
        Run = 2,
        /// <summary>Sprinting.</summary>
        Sprint = 3,
        /// <summary>Running in panic.</summary>
        Panic = 4,
        /// <summary>Seated (furniture or a car seat).</summary>
        Sit = 5,
        /// <summary>Lying (knocked down, dead, in bed).</summary>
        LieDown = 6,
        /// <summary>On the phone.</summary>
        Phone = 7,
        /// <summary>Hands raised.</summary>
        HandsUp = 8,
        /// <summary>Airborne.</summary>
        Fall = 9,
        /// <summary>Ledge climb.</summary>
        Climb = 10,
        /// <summary>Swimming.</summary>
        Swim = 11,
        /// <summary>Getting into a car.</summary>
        EnterVehicle = 12,
        /// <summary>Getting out of a car (or bailing).</summary>
        ExitVehicle = 13,
        /// <summary>Melee swing.</summary>
        Punch = 14,
        /// <summary>Aiming a weapon.</summary>
        Aim = 15,
        /// <summary>Firing a weapon.</summary>
        Shoot = 16,
    }

    /// <summary>
    /// A person the Game simulates (player, promoted civilian, cop, interior NPC). Created only through
    /// <c>GameWorld.SpawnPed</c>/<c>PromoteCitizen</c>, which assign the id and the motor.
    /// Position is the motor's feet position; the ped's collision capsule (radius/height from the motor settings,
    /// tag <c>WorldTag.ForPed(Id)</c>) is put into the collision world each step unless InVehicle, Invisible or Dead
    /// (bodies are not obstacles). The player is a Ped like any other (<see cref="PedKind.Player"/>, id 1).
    /// </summary>
    public sealed class Ped
    {
        /// <summary>Creates a ped; <see cref="GameWorld"/> calls this, systems use <c>GameWorld.SpawnPed</c>.</summary>
        internal Ped(int id, PedKind kind, int model, CharacterMotor motor)
        {
            Id = id;
            Kind = kind;
            Model = model;
            Motor = motor;
            Health = 100f;
            InteriorId = -1;
            Seat = -1;
        }

        /// <summary>Stable id, unique among peds and cars of the session, never reused; 1 is the player.</summary>
        public readonly int Id;
        /// <summary>Kind.</summary>
        public PedKind Kind;
        /// <summary>Citizen model id (<c>ICity.GetCitizenModel</c>); for the player, the outfit.</summary>
        public int Model;
        /// <summary>CS1 citizen id this ped was promoted from, or the driver's citizen; 0 none.</summary>
        public uint Citizen;
        /// <summary>The ped's kinematic capsule. Its <c>Mask</c> ignores the ped's own tag.</summary>
        public readonly CharacterMotor Motor;
        /// <summary>Feet position (the motor's), world space.</summary>
        public Vec3 Position { get { return Motor.Position; } }
        /// <summary>Facing, degrees (0 = +z, 90 = +x).</summary>
        public float YawDeg;
        /// <summary>Feet position at the start of the latest simulation step (set by the session), for render interpolation.</summary>
        public Vec3 PrevPosition;
        /// <summary>Facing at the start of the latest simulation step.</summary>
        public float PrevYawDeg;
        /// <summary>Health 0-100; 0 means dead. Damage maths is G7's; G6 applies fall damage to the player.</summary>
        public float Health;
        /// <summary>Armour 0-100 (G7 absorbs 66 % of damage with it).</summary>
        public float Armour;
        /// <summary>State bits.</summary>
        public PedFlags Flags;
        /// <summary>Id of the car the ped sits in, 0 when on foot.</summary>
        public int VehicleId;
        /// <summary>Seat in that car: 0 driver, 1.. passengers; −1 on foot.</summary>
        public int Seat;
        /// <summary>Animation for the renderer.</summary>
        public PedAnim Anim;
        /// <summary>AI; null for the player and for peds nobody has given a brain (they stand still).</summary>
        public IPedBrain Brain;
        /// <summary>What the brain wants this step; the Peds system (G7, order 30) steps the motor with it through <c>GameWorld.MovePed</c>.</summary>
        public MotorInput Move;
        /// <summary>Weapon slot in hand, as <c>(int)WeaponId</c> (0 Fists). For the player G7 keeps it in step with the profile.</summary>
        public int CurrentWeapon;
        /// <summary>Rounds in the current magazine (G7).</summary>
        public int MagazineAmmo;
        /// <summary><c>GameClock.GameSeconds</c> of the last damage taken (G7 regeneration), −1 never.</summary>
        public double LastDamageTime = -1.0;
        /// <summary>Tag of whoever last damaged this ped, 0 none (G7).</summary>
        public int LastAttackerTag;
        /// <summary>Building id of the interior the ped is in, −1 outside.</summary>
        public int InteriorId;
        /// <summary>Seconds this ped has been demotable (no brain or <see cref="IPedBrain.IsActive"/> false, and not visible to the player), maintained by G6's Ambient system.</summary>
        public float IdleUnseenSeconds;
        /// <summary>CS1 building this citizen was heading to (its errand), 0 for none; handed back with it.</summary>
        public ushort ErrandBuilding;
        /// <summary>What <see cref="ErrandBuilding"/> is to the citizen.</summary>
        public ErrandKind Errand;

        /// <summary>This ped's collision and event tag.</summary>
        public int Tag { get { return WorldTag.ForPed(Id); } }

        /// <summary>True when every bit of <paramref name="f"/> is set.</summary>
        public bool Has(PedFlags f) { return (Flags & f) == f; }

        /// <summary>True when Dead.</summary>
        public bool IsDead { get { return (Flags & PedFlags.Dead) != 0; } }
    }
}
