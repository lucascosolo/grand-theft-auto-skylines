using System;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Police
{
    /// <summary>Why a cop recognised the player (F-HEAT-5); <see cref="None"/> = not recognised.</summary>
    public enum RecognitionReason
    {
        /// <summary>Not recognised (no sight, or sight without identification).</summary>
        None = 0,
        /// <summary>(a) The player matches <c>WantedStatus.Description</c>.</summary>
        Description = 1,
        /// <summary>(b) The player is within <see cref="Recognition.CloseRange"/>.</summary>
        Close = 2,
        /// <summary>(c) The cop saw the player commit a crime (<c>CrimeSystem.CopSawCrimeRecently</c>).</summary>
        CrimeInSight = 3,
        /// <summary>(d) The suspicion meter filled while the player was in the search zone.</summary>
        Suspicion = 4,
    }

    /// <summary>A police eye: a cop on foot, a police car's occupants, or the helicopter. Value type kept by the unit.</summary>
    public struct PoliceObserver
    {
        /// <summary>Eye position: cop feet + 1.6 m; car bounds centre + 0.6 m; helicopter position.</summary>
        public Vec3 Eye;
        /// <summary>Facing, degrees (cop yaw, car yaw; ignored for the helicopter, whose cone points straight down).</summary>
        public float YawDeg;
        /// <summary>True for the helicopter's spotlight: range × 1.3, cone of <see cref="Recognition.SpotlightConeDeg"/> around straight down.</summary>
        public bool Helicopter;
        /// <summary>Tag ignored by the sight raycast (the cop's or the car's own tag).</summary>
        public int SelfTag;
        /// <summary>Tag of the cop (or the car's driver) reported as the recogniser.</summary>
        public int CopTag;
        /// <summary>Suspicion meter 0..1; 1 = recognised by rule (d).</summary>
        public float Suspicion;
    }

    /// <summary>
    /// Police sight and identification (F-HEAT-5). Sight = line of sight AND within the view cone AND within range:
    /// <list type="bullet">
    /// <item>Line of sight: <c>Collision.Raycast</c> from <see cref="PoliceObserver.Eye"/> to the target point (the
    /// player's feet + 1.2 m on foot, the car's bounds centre in a car), mask All ignoring SelfTag, clear when nothing is
    /// hit before the target or the hit's tag is the player's or the player's car's.</item>
    /// <item>Cone: <see cref="ConeDeg"/> total (60° either side of the yaw, horizontal angle).</item>
    /// <item>Range: <see cref="DayRange"/> by day, <see cref="NightRange"/> at night (<c>GameClock.IsNight</c>);
    /// × <see cref="SneakFactor"/> when the player is on foot and Sneaking; × <see cref="SpotlightFactor"/> for the
    /// helicopter (3D distance).</item>
    /// <item>The player inside an interior is never seen from outside (and vice versa): InteriorId must match.</item>
    /// </list>
    /// Identification (any one, checked in order a, b, c, d): (a) description match — on foot:
    /// Description.OutfitModel == player.Model; in a car: Description.CarModel == car.Model.Id and Description.CarColour ==
    /// car.ColourRgb (an unknown −1 field never matches); (b) distance &lt; <see cref="CloseRange"/>; (c) the cop saw a
    /// crime within 1 s; (d) the player is inside the published search zone (SearchRadius &gt; 0) and the suspicion meter
    /// reaches 1. Suspicion fills at 1/<see cref="SuspicionNearSeconds"/> per second within <see cref="SuspicionNearRange"/>,
    /// 1/<see cref="SuspicionFarSeconds"/> within <see cref="SuspicionFarRange"/>, times (1 + PlayerHeat / 100), divided by
    /// <c>WantedRules.ReactionScale</c>; it drains at <see cref="SuspicionDrainPerSecond"/> while the player is not in sight
    /// or not in a fill range. Not wanted (patrols): rules (a) and (c) do not apply; (b) and the suspicion meter (without
    /// the search-zone condition) apply only when PlayerHeat ≥ <see cref="PatrolRecognitionHeat"/>.
    /// </summary>
    public static class Recognition
    {
        /// <summary>Total view cone, degrees.</summary>
        public const float ConeDeg = 120f;
        /// <summary>Range by day, metres.</summary>
        public const float DayRange = 60f;
        /// <summary>Range at night, metres.</summary>
        public const float NightRange = 35f;
        /// <summary>Range factor while the player sneaks on foot.</summary>
        public const float SneakFactor = 0.6f;
        /// <summary>Range factor for the helicopter spotlight.</summary>
        public const float SpotlightFactor = 1.3f;
        /// <summary>Spotlight cone (total) around straight down, degrees.</summary>
        public const float SpotlightConeDeg = 90f;
        /// <summary>Always recognised within this distance when seen, metres.</summary>
        public const float CloseRange = 12f;
        /// <summary>Suspicion fills in this many seconds within <see cref="SuspicionNearRange"/>.</summary>
        public const float SuspicionNearSeconds = 2f;
        /// <summary>Near suspicion range, metres.</summary>
        public const float SuspicionNearRange = 25f;
        /// <summary>Suspicion fills in this many seconds within <see cref="SuspicionFarRange"/>.</summary>
        public const float SuspicionFarSeconds = 4f;
        /// <summary>Far suspicion range, metres.</summary>
        public const float SuspicionFarRange = 45f;
        /// <summary>Suspicion drain per second when not filling.</summary>
        public const float SuspicionDrainPerSecond = 0.5f;
        /// <summary>Player heat from which patrols (not wanted) can recognise the player (2 star-equivalents).</summary>
        public const float PatrolRecognitionHeat = 40f;

        /// <summary>The view range for the conditions (see class), metres.</summary>
        public static float ViewRange(bool night, bool playerSneakingOnFoot, bool helicopter)
        {
            float r = night ? NightRange : DayRange;
            if (playerSneakingOnFoot) r *= SneakFactor;
            if (helicopter) r *= SpotlightFactor;
            return r;
        }

        /// <summary>The player's description now: OutfitModel = player.Model; in a car also CarModel, CarColour and CarId of that car, else −1/0/0.</summary>
        public static SuspectDescription Describe(GameWorld world)
        {
            throw new NotImplementedException();
        }

        /// <summary>Rule (a) for the player's current state against <paramref name="description"/>.</summary>
        public static bool Matches(GameWorld world, SuspectDescription description)
        {
            throw new NotImplementedException();
        }

        /// <summary>Line of sight + cone + range from an observer to the player (see class); no identification.</summary>
        public static bool CanSeePlayer(GameWorld world, PoliceObserver observer)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Cop sight of a world point (crime witnessing): line of sight from the cop's eye (feet + 1.6 m, or the seated
        /// car's centre + 0.6 m facing the car's yaw), within the cone and <see cref="ViewRange"/>(night, false, false) —
        /// day 60 m, night 35 m.
        /// </summary>
        public static bool CopSeesPoint(GameWorld world, Ped cop, Vec3 point)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sight plus identification for one observer over one step of <paramref name="dt"/> seconds, updating its
        /// suspicion meter. <paramref name="copSawCrime"/> is rule (c) for this observer (from the Crime system).
        /// </summary>
        public static RecognitionReason Sees(GameWorld world, ref PoliceObserver observer, bool copSawCrime, float dt)
        {
            throw new NotImplementedException();
        }
    }
}
