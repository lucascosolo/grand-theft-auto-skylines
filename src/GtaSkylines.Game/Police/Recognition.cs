using System;
using GtaSkylines.Game.Crime;
using GtaSkylines.Game.Physics;
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
            SuspectDescription d = SuspectDescription.Unknown;
            Ped p = world.Player;
            if (p == null) return d;
            d.OutfitModel = p.Model;
            Car car = PlayerCar(world);
            if (car != null)
            {
                d.CarModel = car.Model.Id;
                d.CarColour = car.ColourRgb;
                d.CarId = car.Id;
            }
            return d;
        }

        /// <summary>Rule (a) for the player's current state against <paramref name="description"/>.</summary>
        public static bool Matches(GameWorld world, SuspectDescription description)
        {
            Ped p = world.Player;
            if (p == null) return false;
            Car car = PlayerCar(world);
            if (car != null)
                return description.CarModel >= 0 && description.CarModel == car.Model.Id && description.CarColour == car.ColourRgb;
            return description.OutfitModel >= 0 && description.OutfitModel == p.Model;
        }

        /// <summary>Line of sight + cone + range from an observer to the player (see class); no identification.</summary>
        public static bool CanSeePlayer(GameWorld world, PoliceObserver observer)
        {
            Ped p = world.Player;
            if (p == null) return false;
            Car car = PlayerCar(world);
            Vec3 target = car != null ? car.Position : p.Position + new Vec3(0f, 1.2f, 0f);
            bool sneaking = car == null && (p.Has(PedFlags.Sneaking) || (world.PlayerState != null && world.PlayerState.Sneaking));
            float range = ViewRange(world.Clock.IsNight, sneaking, observer.Helicopter);
            Vec3 d = target - observer.Eye;
            float dist = d.Length;
            if (dist > range) return false;
            if (!InCone(observer.YawDeg, observer.Helicopter, d, dist)) return false;
            return Clear(world, observer.Eye, target, observer.SelfTag, p.Tag, car != null ? car.Tag : p.Tag, 0f);
        }

        /// <summary>
        /// Cop sight of a world point (crime witnessing): line of sight from the cop's eye (feet + 1.6 m, or the seated
        /// car's centre + 0.6 m facing the car's yaw), within the cone and <see cref="ViewRange"/>(night, false, false) —
        /// day 60 m, night 35 m.
        /// </summary>
        public static bool CopSeesPoint(GameWorld world, Ped cop, Vec3 point)
        {
            Vec3 eye;
            float yaw;
            int self = cop.Tag;
            Car car = cop.VehicleId != 0 ? world.FindCar(cop.VehicleId) : null;
            if (car != null)
            {
                eye = car.Position + new Vec3(0f, 0.6f, 0f);
                yaw = car.Rotation.YawDeg;
                self = car.Tag;
            }
            else
            {
                eye = cop.Position + new Vec3(0f, 1.6f, 0f);
                yaw = cop.YawDeg;
            }
            Vec3 target = point + new Vec3(0f, 1f, 0f);
            Vec3 d = target - eye;
            float dist = d.Length;
            if (dist > ViewRange(world.Clock.IsNight, false, false)) return false;
            if (!InCone(yaw, false, d, dist)) return false;
            return Clear(world, eye, target, self, -1, -1, 3f);
        }

        /// <summary>
        /// Sight plus identification for one observer over one step of <paramref name="dt"/> seconds, updating its
        /// suspicion meter. <paramref name="copSawCrime"/> is rule (c) for this observer (from the Crime system).
        /// </summary>
        public static RecognitionReason Sees(GameWorld world, ref PoliceObserver observer, bool copSawCrime, float dt)
        {
            WantedStatus w = world.Wanted;
            bool wanted = w.IsWanted;
            if (!wanted && w.PlayerHeat < PatrolRecognitionHeat)
            {
                Drain(ref observer, dt);
                return RecognitionReason.None;
            }
            if (CanSeePlayer(world, observer))
            {
                if (wanted && Matches(world, w.Description)) return RecognitionReason.Description;
                Car car = PlayerCar(world);
                Vec3 target = car != null ? car.Position : world.Player.Position + new Vec3(0f, 1.2f, 0f);
                float dist = Vec3.Distance(observer.Eye, target);
                if (dist < CloseRange) return RecognitionReason.Close;
                if (wanted && copSawCrime) return RecognitionReason.CrimeInSight;
                bool zone = !wanted || (w.SearchRadius > 0f && SearchZone.Contains(w.SearchCentre, w.SearchRadius, world.Player.Position));
                if (zone && dist <= SuspicionFarRange)
                {
                    float fill = dist <= SuspicionNearRange ? 1f / SuspicionNearSeconds : 1f / SuspicionFarSeconds;
                    fill *= (1f + w.PlayerHeat / 100f) / WantedRules.ReactionScale(world.Settings.Police);
                    observer.Suspicion = Math.Min(1f, observer.Suspicion + fill * dt);
                    if (observer.Suspicion >= 0.999f)
                    {
                        observer.Suspicion = 1f;
                        return RecognitionReason.Suspicion;
                    }
                    return RecognitionReason.None;
                }
            }
            Drain(ref observer, dt);
            return RecognitionReason.None;
        }

        /// <summary>The car the player sits in, or null.</summary>
        internal static Car PlayerCar(GameWorld world)
        {
            Ped p = world.Player;
            return p != null && p.VehicleId != 0 ? world.FindCar(p.VehicleId) : null;
        }

        private static void Drain(ref PoliceObserver observer, float dt)
        {
            observer.Suspicion = Math.Max(0f, observer.Suspicion - SuspicionDrainPerSecond * dt);
        }

        private static bool InCone(float yawDeg, bool helicopter, Vec3 d, float dist)
        {
            if (dist < 1e-3f) return true;
            if (helicopter) return -d.Y / dist >= (float)Math.Cos(0.5 * SpotlightConeDeg * MathUtil.Deg2Rad);
            if (d.X * d.X + d.Z * d.Z < 1e-6f) return true;
            float delta = MathUtil.DeltaAngleDeg(yawDeg, Vec3.YawDegOf(d));
            return Math.Abs(delta) <= 0.5f * ConeDeg;
        }

        /// <summary>
        /// Line of sight from <paramref name="from"/> to <paramref name="to"/> ignoring <paramref name="selfTag"/>: hits on
        /// <paramref name="okA"/>/<paramref name="okB"/> or within <paramref name="slack"/> m of the target count as clear;
        /// other people's capsules are looked through (up to 4 of them).
        /// </summary>
        internal static bool Clear(GameWorld world, Vec3 from, Vec3 to, int selfTag, int okA, int okB, float slack)
        {
            Vec3 d = to - from;
            float dist = d.Length;
            if (dist < 1e-3f) return true;
            Vec3 dir = d / dist;
            QueryMask mask = QueryMask.All.Ignoring(selfTag);
            float travelled = 0f;
            for (int i = 0; i < 5; i++)
            {
                PhysicsHit hit;
                float remaining = dist - travelled - 0.05f;
                if (remaining <= 0f) return true;
                if (!world.Collision.Raycast(from + dir * travelled, dir, remaining, mask, out hit)) return true;
                if (hit.Tag != 0 && (hit.Tag == okA || hit.Tag == okB)) return true;
                if (travelled + hit.Distance >= dist - slack) return true;
                if (!WorldTag.IsPerson(hit.Tag)) return false;
                travelled += hit.Distance + 0.05f;
            }
            return false;
        }
    }
}
