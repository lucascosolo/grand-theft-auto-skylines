using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.World
{
    /// <summary>What the player's interaction key would do this step.</summary>
    public enum InteractionTargetKind
    {
        /// <summary>Nothing in reach.</summary>
        None,
        /// <summary>In a car: F exits (bails out above 8 m/s).</summary>
        ExitVehicle,
        /// <summary>F enters (or carjacks) the car named by <see cref="InteractionTarget.TargetTag"/> (Game car or ambient vehicle tag).</summary>
        EnterVehicle,
        /// <summary>E uses <c>Layout.Interactions[Index]</c> of the current interior.</summary>
        InteriorPoint,
        /// <summary>E uses <c>InteriorState.CustomInteractions[Index]</c>.</summary>
        CustomInteraction,
        /// <summary>E enters <see cref="InteractionTarget.Building"/> through the door at <see cref="InteractionTarget.Position"/>.</summary>
        BuildingDoor,
        /// <summary>E talks to the person named by <see cref="InteractionTarget.TargetTag"/> (Game ped or ambient citizen tag).</summary>
        Talk,
    }

    /// <summary>The interaction chosen this step by the targeting rules (see <c>GameSession</c>).</summary>
    public struct InteractionTarget
    {
        /// <summary>Kind.</summary>
        public InteractionTargetKind Kind;
        /// <summary>Help prompt with button tokens ("Press {EnterExit} to enter Sedan"); cached per target, not rebuilt per step.</summary>
        public string Prompt;
        /// <summary>WorldTag of the car or person, 0 otherwise.</summary>
        public int TargetTag;
        /// <summary>Building for BuildingDoor (0 otherwise).</summary>
        public ushort Building;
        /// <summary>Index for InteriorPoint / CustomInteraction, −1 otherwise.</summary>
        public int Index;
        /// <summary>World position of the door, point, car door or person.</summary>
        public Vec3 Position;
    }

    /// <summary>Where the player is in getting in or out of a car.</summary>
    public enum VehicleTransition
    {
        /// <summary>None.</summary>
        None,
        /// <summary>Moving to the driver door (≤ 1.2 s; cancelled by movement input).</summary>
        WalkToDoor,
        /// <summary>Pulling the driver out (1.6 s), then GetIn.</summary>
        PullOut,
        /// <summary>Sitting down (0.6 s), then the player is the driver.</summary>
        GetIn,
        /// <summary>Waiting for the car to slow below 3 m/s, then stepping out (0.6 s).</summary>
        GetOut,
        /// <summary>Bailing out at speed (roll 0.8 s).</summary>
        Bail,
    }

    /// <summary>The player's life cycle.</summary>
    public enum LifeState
    {
        /// <summary>Playing.</summary>
        Alive,
        /// <summary>WASTED screen running (4 s), then respawn.</summary>
        Wasted,
        /// <summary>BUSTED screen running (4 s), then respawn.</summary>
        Busted,
    }

    /// <summary>Player-only state shared by systems. G6 writes it unless a field says otherwise.</summary>
    public sealed class PlayerState
    {
        /// <summary>Stamina 0-100.</summary>
        public float Stamina = 100f;
        /// <summary>Seconds since sprinting last drained stamina (refill starts at 1 s).</summary>
        public float SecondsSinceSprint;
        /// <summary>Sprinting this step.</summary>
        public bool Sprinting;
        /// <summary>Sneak toggle (mirrors <c>PedFlags.Sneaking</c>).</summary>
        public bool Sneaking;
        /// <summary>View yaw in degrees (0 = +z): look input integrates into it; the camera rig may recentre it.</summary>
        public float ViewYawDeg;
        /// <summary>View pitch in degrees (+ up), clamped by the camera mode's limits.</summary>
        public float ViewPitchDeg;
        /// <summary>This step's interaction.</summary>
        public InteractionTarget Interaction;
        /// <summary>Car transition.</summary>
        public VehicleTransition Transition;
        /// <summary>Seconds in the current transition.</summary>
        public float TransitionSeconds;
        /// <summary>Car the transition is about (Game car id), 0 none.</summary>
        public int TransitionCarId;
        /// <summary>The Game car the player drove last (0 none); respray and description use it.</summary>
        public int LastCarId;
        /// <summary>Life cycle.</summary>
        public LifeState Life;
        /// <summary>Seconds in the current life state.</summary>
        public float LifeSeconds;
        /// <summary>Soft lock-on target WorldTag while aiming (G7 writes; the camera frames it), 0 none.</summary>
        public int AimTargetTag;
        /// <summary>Metres walked not yet added to the profile's <c>distance_m</c> stat (whole metres are moved over each step).</summary>
        public float PendingDistance;
    }
}
