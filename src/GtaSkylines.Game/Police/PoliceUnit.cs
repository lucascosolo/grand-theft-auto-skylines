using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.Police
{
    /// <summary>A unit's job (F-HEAT-7). Numeric values are stable.</summary>
    public enum UnitRole
    {
        /// <summary>Not wanted: wander a patrol route (<c>RoadGraph.FindWanderRoute</c>), siren off.</summary>
        Patrol = 0,
        /// <summary>Spawned, driving to the last known position (or the player) by road, siren on.</summary>
        Respond = 1,
        /// <summary>Can see the player: drive straight at the player's car (direct steering); on foot, get out and chase.</summary>
        Pursue = 2,
        /// <summary>Drive to <c>RoadGraph.Predict(player, 6-10 s)</c> ahead of the player's car.</summary>
        Intercept = 3,
        /// <summary>Wander inside one sector of the search zone, speed × 0.6, siren off at 1-2★.</summary>
        Search = 4,
        /// <summary>Parked across a junction ahead on the predicted route (3★+), cops beside it, car Locked.</summary>
        Roadblock = 5,
        /// <summary>Park against the stopped player car (front, back or side slot).</summary>
        BoxIn = 6,
        /// <summary>Steer into the rear quarter of the fleeing player car (3★+).</summary>
        Pit = 7,
        /// <summary>Cops left the car within 25 m of the player and act through <see cref="CopBrain"/>.</summary>
        OnFoot = 8,
        /// <summary>The helicopter: hover 40 m above the last known position, spotlight on (4★+).</summary>
        Helicopter = 9,
        /// <summary>Stars cleared or unit surplus: drive away from the player and despawn when &gt; 400 m and unseen.</summary>
        Leave = 10,
    }

    /// <summary>What a unit is physically.</summary>
    public enum UnitKind
    {
        /// <summary>A police car with two cops (driver seat 0, passenger seat 1).</summary>
        Car = 0,
        /// <summary>The kinematic helicopter (<see cref="PoliceHelicopter"/>), no Game car, no peds.</summary>
        Helicopter = 1,
    }

    /// <summary>
    /// One police unit. Pooled by the Police system (<see cref="PoliceSystem.MaxUnits"/> slots, created once); a slot is
    /// reused after its unit despawns, so ids, not references, identify units across steps.
    /// </summary>
    public sealed class PoliceUnit
    {
        /// <summary>Cops per car.</summary>
        public const int CopsPerCar = 2;

        /// <summary>Unit id, session-unique, from 1; 0 = free slot.</summary>
        public int Id;
        /// <summary>Kind.</summary>
        public UnitKind Kind;
        /// <summary>Current role.</summary>
        public UnitRole Role;
        /// <summary>Seconds in the current role.</summary>
        public float RoleSeconds;
        /// <summary>Game car id (0 for the helicopter or after the car was destroyed).</summary>
        public int CarId;
        /// <summary>Cop ped ids (0 = none/removed); index 0 is the driver.</summary>
        public readonly int[] CopIds = new int[CopsPerCar];
        /// <summary>Search sector index when Role is Search; BoxIn slot (0 front, 1 back, 2 side) when BoxIn.</summary>
        public int Slot;
        /// <summary>The point the role drives to (intercept point, roadblock junction, sector centre, box-in slot).</summary>
        public Vec3 Target;
        /// <summary>The car's driver (also assigned to <c>Car.Driver</c>).</summary>
        public readonly PoliceDriver Driver = new PoliceDriver();
        /// <summary>Sight and suspicion state of the car's occupants (or the helicopter).</summary>
        public PoliceObserver Observer;
        /// <summary>True when the unit saw (recognised) the player this step.</summary>
        public bool SeesPlayer;
        /// <summary>Seconds since the unit last recognised the player.</summary>
        public float SecondsSinceSeen;
        /// <summary>Spawned as a roadblock unit (counts against the roadblock budget, not the car budget).</summary>
        public bool IsRoadblock;

        /// <summary>True when the slot holds a unit.</summary>
        public bool InUse { get { return Id != 0; } }
    }
}
