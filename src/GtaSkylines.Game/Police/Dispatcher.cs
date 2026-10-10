using System;
using System.Collections.Generic;
using GtaSkylines.Game.Crime;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Roads;
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

        private const int Scratch = 64;
        private readonly int[] _order = new int[Scratch];
        private readonly float[] _dist = new float[Scratch];
        private readonly UnitRole[] _role = new UnitRole[Scratch];
        private readonly int[] _slot = new int[Scratch];
        private readonly Vec3[] _target = new Vec3[Scratch];
        private readonly bool[] _sectorTaken = new bool[Scratch];
        private readonly List<JunctionAhead> _junctions = new List<JunctionAhead>(16);

        /// <summary>
        /// Assigns roles to the units in use (helicopter, roadblock and Leave units keep theirs). Units are ranked by
        /// horizontal distance to the player (ties: lower id), so slot order does not matter. Rules in priority order:
        /// <list type="number">
        /// <item>Not wanted: Patrol.</item>
        /// <item>BoxIn: in a car with SlowSeconds ≥ 2 and not Aggressive (5★) → the up-to-3 nearest car units within
        /// 60 m; slots front (0), back (1), side (2) by nearest first; Target = that slot beside the player's car.</item>
        /// <item>OnFoot: the player on foot, or in a car slower than 1 m/s for 2 s → units within 25 m.</item>
        /// <item>Pit: 3★+, player car speed &gt; 12 m/s, unit sees the player, within 15 m and behind the player's
        /// rear (local z &lt; −half length) with |local x| ≤ 4 m.</item>
        /// <item>Pursue: units that see the player.</item>
        /// <item>Intercept: units that do not see a driving player who is seen by someone; Target =
        /// <c>Roads.Predict(player, heading, speed, t)</c> with t = clamp(unit distance / 20 m/s + seeded jitter
        /// [−1, 1), 6, 10) s, so far units aim farther ahead and each unit aims at a different point.</item>
        /// <item>Search: everything else while the search zone is published; each searcher, nearest first, takes the
        /// free sector (count = searchers) whose centre is closest to it; Target = that sector centre.</item>
        /// <item>Respond: the rest (drive to the last known position).</item>
        /// <item>Balance: with n ≥ 3 mobile units, at least floor(n / 3) Intercept or Search: the farthest Pursue units
        /// (never the last one) are converted, to Intercept in a car chase, else to Search.</item>
        /// </list>
        /// Returns the number of units whose role changed (RoleSeconds reset for them). Allocates nothing.
        /// </summary>
        public int AssignRoles(GameWorld world, DispatchContext context, IList<PoliceUnit> units)
        {
            AssignmentCount++;
            int n = 0;
            for (int i = 0; i < units.Count && n < Scratch; i++)
            {
                PoliceUnit u = units[i];
                if (u == null || !u.InUse || u.Kind != UnitKind.Car || u.IsRoadblock) continue;
                if (u.Role == UnitRole.Roadblock || u.Role == UnitRole.Helicopter || u.Role == UnitRole.Leave) continue;
                _order[n] = i;
                _dist[n] = Vec3.DistanceXZ(UnitPosition(world, u), context.PlayerPosition);
                _role[n] = UnitRole.Respond;
                _slot[n] = 0;
                _target[n] = u.Target;
                n++;
            }
            SortByDistance(units, n);

            if (context.Stars <= 0)
            {
                for (int k = 0; k < n; k++) _role[k] = UnitRole.Patrol;
                return Commit(units, n);
            }

            float speed = context.PlayerVelocity.LengthXZ;
            Car playerCar = Recognition.PlayerCar(world);
            float halfLen = playerCar != null ? playerCar.Model.HalfExtents.Z : 2.2f;
            float halfWid = playerCar != null ? playerCar.Model.HalfExtents.X : 0.9f;
            Quat frame = Quat.Yaw(context.PlayerHeadingDeg);
            Vec3 fwd = frame * Vec3.Forward, right = frame * Vec3.Right;
            bool assigned;

            // BoxIn
            if (context.InCar && context.SlowSeconds >= BoxInSeconds && context.Stars < 5)
            {
                int boxed = 0;
                for (int k = 0; k < n && boxed < MaxBoxIn; k++)
                {
                    if (_dist[k] > 60f) break;
                    _role[k] = UnitRole.BoxIn;
                    _slot[k] = boxed;
                    float gap = halfLen + 2.2f + 0.8f;
                    _target[k] = boxed == 0 ? context.PlayerPosition + fwd * gap
                        : boxed == 1 ? context.PlayerPosition - fwd * gap
                        : context.PlayerPosition + right * (halfWid + 0.9f + 0.8f);
                    boxed++;
                }
            }

            bool stopped = !context.InCar || (speed < 1f && context.SlowSeconds >= BoxInSeconds);
            for (int k = 0; k < n; k++)
            {
                if (_role[k] != UnitRole.Respond) continue;
                PoliceUnit u = units[_order[k]];
                assigned = false;
                if (stopped && _dist[k] <= OnFootRange) { _role[k] = UnitRole.OnFoot; assigned = true; }
                if (!assigned && context.Stars >= 3 && context.InCar && speed > PitMinSpeed && u.SeesPlayer)
                {
                    Vec3 rel = UnitPosition(world, u) - context.PlayerPosition;
                    float lz = Vec3.Dot(rel, fwd), lx = Vec3.Dot(rel, right);
                    if (rel.LengthXZ <= PitRange && lz < -halfLen && Math.Abs(lx) <= 4f) { _role[k] = UnitRole.Pit; assigned = true; }
                }
                if (!assigned && u.SeesPlayer) { _role[k] = UnitRole.Pursue; _target[k] = context.PlayerPosition; }
            }

            for (int k = 0; k < n; k++)
            {
                if (_role[k] != UnitRole.Respond) continue;
                if (context.InCar && context.Seen) SetIntercept(world, context, units[_order[k]], k, speed);
                else if (context.SearchRadius > 0f) _role[k] = UnitRole.Search;
                else _target[k] = world.Wanted.HasLastKnownPosition ? world.Wanted.LastKnownPosition : context.PlayerPosition;
            }

            // Balance: never all pursuers.
            if (n >= UnitsPerNonPursuer)
            {
                int required = n / UnitsPerNonPursuer, have = 0, pursuers = 0;
                for (int k = 0; k < n; k++)
                {
                    if (_role[k] == UnitRole.Intercept || _role[k] == UnitRole.Search) have++;
                    if (_role[k] == UnitRole.Pursue) pursuers++;
                }
                for (int k = n - 1; k >= 0 && have < required && pursuers > 1; k--)
                {
                    if (_role[k] != UnitRole.Pursue) continue;
                    if (context.InCar) SetIntercept(world, context, units[_order[k]], k, speed);
                    else _role[k] = UnitRole.Search;
                    have++;
                    pursuers--;
                }
            }

            AssignSectors(world, context, units, n);
            return Commit(units, n);
        }

        private void SetIntercept(GameWorld world, DispatchContext context, PoliceUnit u, int k, float speed)
        {
            float jitter = Seeded.Range(Seed, (uint)AssignmentCount, (uint)u.Id, -1f, 1f);
            float t = MathUtil.Clamp(_dist[k] / 20f + jitter, InterceptMinSeconds, InterceptMaxSeconds);
            RoadPrediction p = world.Roads.Predict(context.PlayerPosition, context.PlayerHeadingDeg, speed, t);
            _role[k] = UnitRole.Intercept;
            _target[k] = p.Position;
        }

        private void AssignSectors(GameWorld world, DispatchContext context, IList<PoliceUnit> units, int n)
        {
            int count = 0;
            for (int k = 0; k < n; k++) if (_role[k] == UnitRole.Search) count++;
            if (count == 0) return;
            Vec3 centre = context.SearchRadius > 0f ? context.SearchCentre : context.PlayerPosition;
            float radius = context.SearchRadius > 0f ? context.SearchRadius : SearchZone.BaseRadius;
            for (int s = 0; s < count; s++) _sectorTaken[s] = false;
            for (int k = 0; k < n; k++)
            {
                if (_role[k] != UnitRole.Search) continue;
                Vec3 pos = UnitPosition(world, units[_order[k]]);
                int best = -1;
                float bestD = float.MaxValue;
                for (int s = 0; s < count; s++)
                {
                    if (_sectorTaken[s]) continue;
                    float d = Vec3.DistanceXZ(pos, SearchZone.SectorCentre(centre, radius, s, count));
                    if (d < bestD) { bestD = d; best = s; }
                }
                _sectorTaken[best] = true;
                _slot[k] = best;
                _target[k] = SearchZone.SectorCentre(centre, radius, best, count);
            }
        }

        private int Commit(IList<PoliceUnit> units, int n)
        {
            int changed = 0;
            for (int k = 0; k < n; k++)
            {
                PoliceUnit u = units[_order[k]];
                if (u.Role != _role[k])
                {
                    u.Role = _role[k];
                    u.RoleSeconds = 0f;
                    changed++;
                }
                u.Slot = _slot[k];
                u.Target = _target[k];
            }
            return changed;
        }

        private void SortByDistance(IList<PoliceUnit> units, int n)
        {
            for (int i = 1; i < n; i++)
            {
                int o = _order[i];
                float d = _dist[i];
                int id = units[o].Id;
                int j = i - 1;
                while (j >= 0 && (_dist[j] > d || (_dist[j] == d && units[_order[j]].Id > id)))
                {
                    _order[j + 1] = _order[j];
                    _dist[j + 1] = _dist[j];
                    j--;
                }
                _order[j + 1] = o;
                _dist[j + 1] = d;
            }
        }

        /// <summary>Where a unit is: its car's centre, else its observer's eye (helicopter).</summary>
        internal static Vec3 UnitPosition(GameWorld world, PoliceUnit u)
        {
            if (u.CarId != 0)
            {
                Car car = world.FindCar(u.CarId);
                if (car != null) return car.Position;
            }
            return u.Observer.Eye;
        }

        /// <summary>
        /// Up to <paramref name="count"/> roadblock junctions: <c>Roads.FindJunctionsAhead(player, heading, 500)</c>
        /// filtered to distance ≥ 200 m, nearest first, one per junction. The heading is the velocity's when the player
        /// moves faster than 2 m/s, else <see cref="DispatchContext.PlayerHeadingDeg"/>. Writes their positions into
        /// <paramref name="into"/> (cleared first); returns how many.
        /// </summary>
        public int FindRoadblockJunctions(GameWorld world, DispatchContext context, int count, List<Vec3> into)
        {
            into.Clear();
            float heading = context.PlayerVelocity.LengthXZ > 2f ? Vec3.YawDegOf(context.PlayerVelocity) : context.PlayerHeadingDeg;
            world.Roads.FindJunctionsAhead(context.PlayerPosition, heading, RoadblockMaxDistance, _junctions);
            for (int i = 0; i < _junctions.Count && into.Count < count; i++)
                if (_junctions[i].Distance >= RoadblockMinDistance) into.Add(_junctions[i].Position);
            return into.Count;
        }
    }
}
