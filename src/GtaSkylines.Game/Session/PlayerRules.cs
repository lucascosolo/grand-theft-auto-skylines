namespace GtaSkylines.Game.Session
{
    /// <summary>Player and session constants (DESIGN F-FOOT, F-VEH, F-INT; contract G6). Speeds m/s, times s, distances m.</summary>
    public static class PlayerRules
    {
        /// <summary>Default on-foot speed.</summary>
        public const float JogSpeed = 3.6f;
        /// <summary>Speed while sneaking.</summary>
        public const float WalkSpeed = 1.6f;
        /// <summary>Speed while Sprint is held with stamina &gt; 0 and moving forward-ish.</summary>
        public const float SprintSpeed = 6.8f;
        /// <summary>Stamina drained per second of sprinting.</summary>
        public const float StaminaDrainPerSecond = 18f;
        /// <summary>Stamina refilled per second once not sprinting for <see cref="StaminaRefillDelay"/>.</summary>
        public const float StaminaRefillPerSecond = 25f;
        /// <summary>Delay before stamina refills.</summary>
        public const float StaminaRefillDelay = 1f;
        /// <summary>Maximum stamina.</summary>
        public const float MaxStamina = 100f;
        /// <summary>Third-person turn rate toward the move direction, degrees per second.</summary>
        public const float TurnRateDegPerSecond = 720f;
        /// <summary>Landing speed below which a fall does no damage.</summary>
        public const float FallDamageMinSpeed = 9f;
        /// <summary>Health lost per m/s of landing speed above <see cref="FallDamageMinSpeed"/>.</summary>
        public const float FallDamagePerSpeed = 9f;

        /// <summary>Reach to a car door for "enter vehicle", measured from the player's feet to the door point, in front (within ±60° of facing or view).</summary>
        public const float EnterVehicleReach = 3f;
        /// <summary>Ambient vehicles moving faster than this cannot be entered.</summary>
        public const float EnterMovingMaxSpeed = 8f;
        /// <summary>Reach to an interior interaction point.</summary>
        public const float InteriorPointReach = 1.2f;
        /// <summary>Reach to a building door outside.</summary>
        public const float BuildingDoorReach = 2.5f;
        /// <summary>Reach to a person for "talk", in the front half (±90°).</summary>
        public const float TalkReach = 2.5f;
        /// <summary>Longest walk-to-door phase.</summary>
        public const float WalkToDoorMaxSeconds = 1.2f;
        /// <summary>Pull-out time when carjacking.</summary>
        public const float PullOutSeconds = 1.6f;
        /// <summary>Sit-down time after reaching the door.</summary>
        public const float GetInSeconds = 0.6f;
        /// <summary>Step-out time.</summary>
        public const float GetOutSeconds = 0.6f;
        /// <summary>The player exits normally only below this car speed.</summary>
        public const float ExitMaxSpeed = 3f;
        /// <summary>Above this car speed, F bails out instead of waiting.</summary>
        public const float BailMinSpeed = 8f;
        /// <summary>Health lost by bailing out.</summary>
        public const float BailDamage = 5f;
        /// <summary>Bail roll duration.</summary>
        public const float BailSeconds = 0.8f;

        /// <summary>Car-vs-ambient-traffic closing speed above which the ambient vehicle is promoted.</summary>
        public const float PromoteOnImpactSpeed = 6f;
        /// <summary>Speed above which passing within 1.5 m of traffic or a ped counts as a near miss (RecklessDriving).</summary>
        public const float NearMissSpeed = 12f;
        /// <summary>Lateral distance of a near miss.</summary>
        public const float NearMissDistance = 1.5f;

        /// <summary>Demote calm peds out of sight beyond this distance.</summary>
        public const float DemoteUnseenDistance = 60f;
        /// <summary>Demote calm peds beyond this distance even in sight.</summary>
        public const float DemoteAlwaysDistance = 150f;
        /// <summary>Remove dead or injured peds beyond this distance.</summary>
        public const float RemoveBodiesDistance = 120f;
        /// <summary>Abandoned cars farther than this, unseen for <see cref="AbandonedUnseenSeconds"/>, are removed.</summary>
        public const float AbandonedRemoveDistance = 300f;
        /// <summary>Seconds unseen before an abandoned car is removed.</summary>
        public const float AbandonedUnseenSeconds = 10f;

        /// <summary>Seconds of WASTED / BUSTED before respawn.</summary>
        public const float DeathScreenSeconds = 4f;
        /// <summary>Hospital fee after WASTED (or all cash when less).</summary>
        public const long HospitalFee = 500;
        /// <summary>Fine after BUSTED (or all cash when less).</summary>
        public const long BustedFine = 500;
        /// <summary>Hours skipped by sleeping.</summary>
        public const float SleepHours = 6f;
        /// <summary>Radius from the player's last car to a garage door within which a respray applies to that car.</summary>
        public const float RespraySearchRadius = 40f;

        /// <summary>Profile stat: metres walked (in addition to <c>StatNames.DistanceM</c>, which counts both).</summary>
        public const string StatDistanceWalked = "distance_walked_m";
        /// <summary>Profile stat: metres driven.</summary>
        public const string StatDistanceDriven = "distance_driven_m";
        /// <summary>Profile stat: interiors entered.</summary>
        public const string StatInteriorsVisited = "interiors_visited";
    }
}
