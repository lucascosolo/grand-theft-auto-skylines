using System;
using System.Collections.Generic;
using GtaSkylines.Game.Crime;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Settings;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Police
{
    /// <summary>How many units a wanted level calls for (F-HEAT-7).</summary>
    public struct UnitBudget
    {
        /// <summary>Police cars that chase, intercept, search, box in, PIT or go on foot.</summary>
        public int Cars;
        /// <summary>Roadblocks; each is one more car (two per junction are made of two roadblock units).</summary>
        public int Roadblocks;
        /// <summary>True when a helicopter flies.</summary>
        public bool Helicopter;
        /// <summary>5★: units ram (cars always choose Pursue/PIT over BoxIn) and cops shoot on sight.</summary>
        public bool Aggressive;

        /// <summary>Cars + Roadblocks.</summary>
        public int TotalCars { get { return Cars + Roadblocks; } }
    }

    /// <summary>The player as the dispatcher sees it this second (built by the Police system).</summary>
    public struct DispatchContext
    {
        /// <summary>Stars.</summary>
        public int Stars;
        /// <summary>Player feet (on foot) or car centre.</summary>
        public Vec3 PlayerPosition;
        /// <summary>Player velocity, m/s.</summary>
        public Vec3 PlayerVelocity;
        /// <summary>Heading of the player (car yaw in a car, ped yaw on foot), degrees.</summary>
        public float PlayerHeadingDeg;
        /// <summary>True when the player drives a car.</summary>
        public bool InCar;
        /// <summary>Seconds the player's car has been slower than <see cref="Dispatcher.BoxInSpeed"/> (0 on foot).</summary>
        public float SlowSeconds;
        /// <summary>True while any unit sees the player.</summary>
        public bool Seen;
        /// <summary>The search zone (Evading).</summary>
        public Vec3 SearchCentre;
        /// <summary>Search radius, 0 when not evading.</summary>
        public float SearchRadius;
    }

    /// <summary>
    /// Budgets, spawn timing and role assignment (F-HEAT-7). Role assignment runs every
    /// <see cref="AssignInterval"/> seconds and is deterministic: units are considered in slot order, ties broken by
    /// lower unit id, and every random choice uses <c>new Rng(Rng.Seed(seed, assignmentCount, unitId))</c>.
    /// </summary>
    public sealed class Dispatcher
    {
        /// <summary>Seconds between role assignments.</summary>
        public const float AssignInterval = 1f;
        /// <summary>Player car speed under which BoxIn may start, m/s.</summary>
        public const float BoxInSpeed = 3f;
        /// <summary>Seconds under <see cref="BoxInSpeed"/> before BoxIn.</summary>
        public const float BoxInSeconds = 2f;
        /// <summary>Most units boxing in.</summary>
        public const int MaxBoxIn = 3;
        /// <summary>Units within this distance go on foot when the player is on foot or stopped, metres.</summary>
        public const float OnFootRange = 25f;
        /// <summary>PIT: the cop car must be within this distance behind-left/right of the player's car, metres.</summary>
        public const float PitRange = 15f;
        /// <summary>PIT: the player's car must be faster than this, m/s.</summary>
        public const float PitMinSpeed = 12f;
        /// <summary>Intercept look-ahead range, seconds (picked per unit, seeded).</summary>
        public const float InterceptMinSeconds = 6f, InterceptMaxSeconds = 10f;
        /// <summary>Roadblock junction distance ahead on the predicted route, metres.</summary>
        public const float RoadblockMinDistance = 200f, RoadblockMaxDistance = 500f;
        /// <summary>At least one Intercept or Search unit per this many mobile units, when available.</summary>
        public const int UnitsPerNonPursuer = 3;
        /// <summary>Response delay bounds before scaling by difficulty, seconds.</summary>
        public const float MinResponseDelay = 6f, MaxResponseDelay = 20f;
        /// <summary>Seconds between successive reinforcements once the response delay has passed.</summary>
        public const float ReinforcementInterval = 1.5f;

        private static readonly int[] BaseCars = { 0, 1, 3, 4, 6, 8 };
        private static readonly int[] BaseRoadblocks = { 0, 0, 0, 1, 2, 3 };

        /// <summary>Creates a dispatcher.</summary>
        public Dispatcher(uint seed)
        {
            Seed = seed;
        }

        /// <summary>Seed of every role choice.</summary>
        public uint Seed { get; private set; }

        /// <summary>
        /// Budget for a wanted level (stars clamped 0-5): base cars 0/1/3/4/6/8 and roadblocks 0/0/0/1/2/3, each ×
        /// <see cref="WantedRules.BudgetScale"/> rounded half away from zero, cars at least 1 when stars ≥ 1.
        /// Helicopter at 4★+ when <paramref name="helicopterModelAvailable"/>; otherwise 4★+ adds 2 cars (after scaling).
        /// Aggressive at 5★. Implemented (data).
        /// </summary>
        public static UnitBudget BudgetFor(int stars, PoliceDifficulty difficulty, bool helicopterModelAvailable)
        {
            if (stars < 0) stars = 0;
            if (stars > 5) stars = 5;
            float k = WantedRules.BudgetScale(difficulty);
            UnitBudget b = new UnitBudget();
            b.Cars = (int)Math.Round(BaseCars[stars] * k, MidpointRounding.AwayFromZero);
            if (stars >= 1 && b.Cars < 1) b.Cars = 1;
            b.Roadblocks = (int)Math.Round(BaseRoadblocks[stars] * k, MidpointRounding.AwayFromZero);
            if (stars >= 4)
            {
                if (helicopterModelAvailable) b.Helicopter = true;
                else b.Cars += 2;
            }
            b.Aggressive = stars >= 5;
            return b;
        }

        /// <summary>
        /// Patrol cars while not wanted for the district heat at the player: 0 below 20; else 1 + floor(2 × (heat − 20) /
        /// 80) (20 → 1, 60 → 2, 100 → 3), × <see cref="WantedRules.BudgetScale"/> rounded half away from zero, at least
        /// 1 when heat ≥ 20. Implemented (data).
        /// </summary>
        public static int PatrolCount(float districtHeat, PoliceDifficulty difficulty)
        {
            if (districtHeat < 20f) return 0;
            int n = 1 + (int)Math.Floor(2f * (Math.Min(districtHeat, 100f) - 20f) / 80f);
            n = (int)Math.Round(n * WantedRules.BudgetScale(difficulty), MidpointRounding.AwayFromZero);
            return n < 1 ? 1 : n;
        }

        /// <summary>
        /// Seconds from the first star to the first spawned unit: base = 20 − 14 × h / 100 with h = max(player heat,
        /// district heat at the player), clamped to [6, 20], × <see cref="WantedRules.ReactionScale"/>. A cop that saw the
        /// crime is already there, so units already alive (patrols) join at once. Implemented (data).
        /// </summary>
        public static float ResponseDelay(float heat, PoliceDifficulty difficulty)
        {
            float d = MaxResponseDelay - (MaxResponseDelay - MinResponseDelay) * MathUtil.Clamp(heat, 0f, 100f) / 100f;
            return MathUtil.Clamp(d, MinResponseDelay, MaxResponseDelay) * WantedRules.ReactionScale(difficulty);
        }

        /// <summary>Role assignments made so far (part of the seed of each choice).</summary>
        public int AssignmentCount { get; private set; }

        /// <summary>
        /// Assigns roles to the units in use (helicopter and roadblock units keep theirs). Rules in priority order:
        /// <list type="number">
        /// <item>Not wanted: Patrol (or Leave for units left over from an episode).</item>
        /// <item>BoxIn: in a car with SlowSeconds ≥ 2 → the up-to-3 nearest car units within 60 m; slots front, back, side by nearest first. Not when Aggressive.</item>
        /// <item>OnFoot: the player on foot, or in a car with speed &lt; 1 m/s for 2 s and not boxed by this unit, → car units within 25 m.</item>
        /// <item>Pit: 3★+, player car speed &gt; 12 m/s, unit sees the player, unit's car within 15 m and behind the
        /// player's rear (local z &lt; −half length) with |local x| ≤ 4 m.</item>
        /// <item>Pursue: units that see the player, nearest first.</item>
        /// <item>Intercept: units that do not see the player while the player drives and is seen by someone, target
        /// <c>Roads.Predict(player, heading, speed, seeded 6-10 s)</c>.</item>
        /// <item>Search: everything else while Evading, sectors 0..n−1 by unit order (sector count = number of searching units).</item>
        /// <item>Balance: while ≥ 3 mobile car units exist, at least floor(n / 3) are Intercept or Search: the farthest
        /// Pursue units are converted (Intercept in a car chase, else Search).</item>
        /// </list>
        /// Returns the number of units whose role changed (RoleSeconds reset for them).
        /// </summary>
        public int AssignRoles(GameWorld world, DispatchContext context, IList<PoliceUnit> units)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Up to <paramref name="count"/> roadblock junctions: <c>Roads.FindJunctionsAhead(player, heading, 500)</c>
        /// filtered to distance ≥ 200 m, nearest first, one per junction. Writes their positions into
        /// <paramref name="into"/> (cleared first); returns how many.
        /// </summary>
        public int FindRoadblockJunctions(GameWorld world, DispatchContext context, int count, List<Vec3> into)
        {
            throw new NotImplementedException();
        }
    }
}
