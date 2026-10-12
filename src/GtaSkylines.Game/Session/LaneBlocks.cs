using System;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Roads;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Session
{
    /// <summary>
    /// F-VEH-8 (docs/contracts/C2-abandoned-cars.md): an abandoned, stationary car standing in a car lane near the player
    /// emits one <c>CityEffect.BlockLane</c>, and one <c>CityEffect.UnblockLane</c> when that ends.
    /// </summary>
    public static class LaneBlocks
    {
        /// <summary>Longest a car blocks over one abandonment, seconds.</summary>
        public const float MaxSeconds = 180f;
        /// <summary>A blocking car pushed farther than this (XZ, metres) from where it blocked stops blocking.</summary>
        public const float MoveTolerance = 2f;
        /// <summary>Horizontal speed below which a car counts as standing, m/s.</summary>
        public const float StandingSpeed = 0.5f;
        /// <summary>Nearest-road search distance, metres.</summary>
        public const float SearchDistance = 25f;
        /// <summary>Greatest height difference between car centre and road centreline, metres.</summary>
        public const float MaxHeightGap = 3f;

        /// <summary>
        /// True when <paramref name="position"/> lies within half a lane width of a car lane of the nearest drivable segment:
        /// |lateral offset| ≤ (forward + backward lanes) × lane width ÷ 2, and within <see cref="MaxHeightGap"/> vertically.
        /// </summary>
        public static bool TryFindLane(RoadGraph roads, Vec3 position, out RoadPoint point)
        {
            RoadSegment s;
            if (roads != null && roads.TryNearestPoint(position, SearchDistance, out point) && roads.TryGetSegment(point.SegmentId, out s))
            {
                float width = s.LaneWidth > 0f && !float.IsInfinity(s.LaneWidth) ? s.LaneWidth : RoadDefaults.LaneWidth;
                if (Math.Abs(point.LateralOffset) <= (s.ForwardLanes + s.BackwardLanes) * width * 0.5f
                    && Math.Abs(position.Y - point.Position.Y) <= MaxHeightGap)
                    return true;
            }
            point = default(RoadPoint);
            return false;
        }

        /// <summary>One step for <paramref name="car"/>: block or unblock (Ambient system; the player stands at <paramref name="player"/>).</summary>
        internal static void Step(GameWorld w, Car car, Vec3 player, float dt)
        {
            if (car.Owner != CarOwner.Abandoned)
            {
                w.EndLaneBlock(car);
                car.LaneBlockSeconds = 0f;
                return;
            }
            bool near = Vec3.DistanceXZ(car.Position, player) <= w.Ambient.Radius;
            if (car.LaneBlocking)
            {
                car.LaneBlockSeconds += dt;
                if (!near || Occupied(car) || car.LaneBlockSeconds >= MaxSeconds
                    || Vec3.DistanceXZ(car.Position, car.LaneBlockAnchor) > MoveTolerance)
                    w.EndLaneBlock(car);
                return;
            }
            if (!near || car.LaneBlockSeconds >= MaxSeconds || car.Destroyed || Occupied(car)) return;
            Vec3 v = car.Body.Velocity;
            if (v.X * v.X + v.Z * v.Z >= StandingSpeed * StandingSpeed) return;
            RoadPoint p;
            if (!TryFindLane(w.Roads, car.Position, out p)) return;
            car.LaneBlocking = true;
            car.LaneBlockAnchor = car.Position;
            Vec3 pivot = car.Position - car.Rotation * car.Model.CenterOffset;
            w.Effects.Add(CityEffect.BlockLane(car.Tag, pivot, car.Rotation.YawDeg, car.Model.Id, p.SegmentId));
        }

        private static bool Occupied(Car car)
        {
            for (int i = 0; i < Car.MaxSeats; i++) if (car.Seats[i] != 0) return true;
            return false;
        }
    }
}
