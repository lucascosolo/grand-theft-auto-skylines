using System;
using System.Collections.Generic;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Peds
{
    /// <summary>
    /// Outdoor local planner (G7 contract "Steering"): turns a wanted direction into <c>ped.Move</c> while avoiding
    /// obstacles. Writes only <c>ped.Move</c>, <c>ped.YawDeg</c> and <c>ped.Anim</c>; the Peds system moves the motor.
    /// Deterministic, no allocation.
    /// <para>
    /// Avoidance: the candidate directions are the wanted horizontal direction rotated by 0, +45, −45, +90, −90, +135,
    /// −135 and 180 degrees, tried in that order; the first whose capsule cast (the ped's motor radius and height, feet
    /// lifted by the motor's step height, length <see cref="ProbeDistance"/> + speed × <see cref="ProbeSecondsAhead"/>,
    /// <c>QueryMask.All.Ignoring(ped.Tag)</c>) hits nothing is used. When all eight are blocked the ped stands still.
    /// The facing turns toward the chosen direction at most <see cref="TurnRateDegPerSecond"/>.
    /// </para>
    /// </summary>
    public static class PedSteering
    {
        /// <summary>Minimum probe length, metres (1.0).</summary>
        public const float ProbeDistance = 1f;
        /// <summary>Extra probe length per m/s of speed, seconds (0.3).</summary>
        public const float ProbeSecondsAhead = 0.3f;
        /// <summary>Facing turn rate, degrees per second (540).</summary>
        public const float TurnRateDegPerSecond = 540f;
        /// <summary>Calm walking speed, m/s (1.4).</summary>
        public const float WalkSpeed = 1.4f;
        /// <summary>Within this distance (m) of a seek target the ped stops (0.5).</summary>
        public const float ArriveDistance = 0.5f;

        /// <summary>
        /// The avoidance pick for wanted horizontal direction <paramref name="direction"/> at <paramref name="speed"/>:
        /// true with the chosen unit direction, false when every candidate is blocked.
        /// </summary>
        public static bool ChooseDirection(GameWorld world, Ped ped, Vec3 direction, float speed, out Vec3 chosen)
        {
            Vec3 flat = direction.Flat;
            float len = flat.Length;
            if (len < 1e-5f)
            {
                chosen = Vec3.Zero;
                return false;
            }
            return Choose(world, ped, flat / len, ProbeDistance + speed * ProbeSecondsAhead, out chosen);
        }

        /// <summary>Moves toward <paramref name="target"/> at <paramref name="speed"/> with avoidance; stops within <see cref="ArriveDistance"/>. True once arrived.</summary>
        public static bool Seek(GameWorld world, Ped ped, Vec3 target, float speed, PedAnim moving, float dt)
        {
            Vec3 to = (target - ped.Position).Flat;
            float d = to.Length;
            if (d <= ArriveDistance)
            {
                Stop(ped, PedAnim.Idle);
                return true;
            }
            float probe = Math.Min(ProbeDistance + speed * ProbeSecondsAhead, Math.Max(0f, d - SeekClearance));
            Vec3 dir;
            if (!Choose(world, ped, to / d, probe, out dir))
            {
                Stop(ped, PedAnim.Idle);
                return false;
            }
            Go(ped, dir, speed, moving, dt);
            return false;
        }

        /// <summary>Moves directly away from <paramref name="threat"/> (horizontal) at <paramref name="speed"/> with avoidance.</summary>
        public static void Flee(GameWorld world, Ped ped, Vec3 threat, float speed, PedAnim moving, float dt)
        {
            Vec3 away = (ped.Position - threat).Flat;
            float len = away.Length;
            Vec3 want = len > 1e-4f ? away / len : Vec3.FromYawDeg(ped.YawDeg);
            Vec3 dir;
            if (Choose(world, ped, want, ProbeDistance + speed * ProbeSecondsAhead, out dir)) Go(ped, dir, speed, moving, dt);
            else Stop(ped, moving);
        }

        /// <summary>Stands still: Move.DesiredVelocity zero, facing kept, Anim = <paramref name="anim"/>.</summary>
        public static void Stop(Ped ped, PedAnim anim)
        {
            ped.Move.DesiredVelocity = Vec3.Zero;
            ped.Move.FacingYawDeg = ped.YawDeg;
            ped.Move.Jump = false;
            ped.Anim = anim;
        }

        /// <summary>Turns the facing toward <paramref name="yawDeg"/> at the turn rate without moving.</summary>
        public static void TurnToward(Ped ped, float yawDeg, float dt)
        {
            ped.YawDeg = MathUtil.MoveTowardsAngleDeg(ped.YawDeg, yawDeg, TurnRateDegPerSecond * dt);
            ped.Move.FacingYawDeg = ped.YawDeg;
        }

        private const float SeekClearance = 0.8f;
        private static readonly float[] Offsets = { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f };

        internal static bool Choose(GameWorld world, Ped ped, Vec3 want, float probe, out Vec3 chosen)
        {
            if (probe <= 0f)
            {
                chosen = want;
                return true;
            }
            MotorSettings m = ped.Motor.Settings;
            Vec3 feet = ped.Position + new Vec3(0f, m.StepHeight, 0f);
            float height = Math.Max(2f * m.Radius, m.Height - m.StepHeight);
            QueryMask mask = QueryMask.All.Ignoring(ped.Tag);
            float baseYaw = Vec3.YawDegOf(want);
            for (int i = 0; i < Offsets.Length; i++)
            {
                Vec3 dir = i == 0 ? want : Vec3.FromYawDeg(baseYaw + Offsets[i]);
                PhysicsHit hit;
                if (!world.Collision.CapsuleCast(feet, m.Radius, height, dir, probe, mask, out hit)
                    || (hit.Distance <= 0f && Vec3.Dot(hit.Normal.Flat, dir) > 0f))
                {
                    chosen = dir;
                    return true;
                }
            }
            chosen = Vec3.Zero;
            return false;
        }

        internal static void Go(Ped ped, Vec3 dir, float speed, PedAnim moving, float dt)
        {
            ped.Move.DesiredVelocity = dir * speed;
            ped.Move.Jump = false;
            TurnToward(ped, Vec3.YawDegOf(dir), dt);
            ped.Anim = moving;
        }
    }

    /// <summary>
    /// Indoor path following over the loaded interior's <c>InteriorNavGrid.FindPath</c> (layout-local coordinates via
    /// <c>InteriorState.ToLocal/ToWorld</c>). One per brain that walks indoors; its point list is allocated once
    /// (capacity <see cref="Capacity"/>). Replans when the goal moved more than <see cref="ReplanDistance"/> or every
    /// <see cref="ReplanSeconds"/>. Waypoints are reached within 0.3 m. Outside an interior (or with no nav grid) it falls
    /// back to <see cref="PedSteering.Seek"/>.
    /// </summary>
    public sealed class PedPath
    {
        /// <summary>Initial waypoint capacity (32).</summary>
        public const int Capacity = 32;
        /// <summary>Goal movement that forces a replan, metres (1).</summary>
        public const float ReplanDistance = 1f;
        /// <summary>Replan interval, seconds (1).</summary>
        public const float ReplanSeconds = 1f;

        private const float WaypointRadius = 0.3f;
        private readonly List<Vec3> _points = new List<Vec3>(Capacity);
        private bool _planned;
        private float _sinceReplan;
        private Vec3 _goal;
        private bool _found;

        /// <summary>Waypoints left (world space).</summary>
        public int Remaining { get { return _points.Count; } }

        /// <summary>Forgets the current path.</summary>
        public void Clear()
        {
            _points.Clear();
            _planned = false;
        }

        /// <summary>
        /// Walks <paramref name="ped"/> toward <paramref name="goal"/> at <paramref name="speed"/>; true once within
        /// <see cref="PedSteering.ArriveDistance"/> of the goal. When no path exists the ped stands still and false is returned.
        /// </summary>
        public bool Follow(GameWorld world, Ped ped, Vec3 goal, float speed, PedAnim moving, float dt)
        {
            InteriorState interior = world.Interior;
            if (!interior.IsInside || interior.Layout == null || interior.Layout.Nav == null)
                return PedSteering.Seek(world, ped, goal, speed, moving, dt);
            if (Vec3.DistanceXZ(ped.Position, goal) <= PedSteering.ArriveDistance)
            {
                PedSteering.Stop(ped, PedAnim.Idle);
                _points.Clear();
                return true;
            }
            _sinceReplan += dt;
            if (!_planned || _sinceReplan >= ReplanSeconds || Vec3.DistanceXZ(goal, _goal) > ReplanDistance)
            {
                _planned = true;
                _sinceReplan = 0f;
                _goal = goal;
                _found = interior.Layout.Nav.FindPath(interior.ToLocal(ped.Position), interior.ToLocal(goal), _points);
                for (int i = 0; i < _points.Count; i++) _points[i] = interior.ToWorld(_points[i]).WithY(ped.Position.Y);
            }
            while (_points.Count > 0 && Vec3.DistanceXZ(ped.Position, _points[0]) <= WaypointRadius) _points.RemoveAt(0);
            if (!_found)
            {
                PedSteering.Stop(ped, PedAnim.Idle);
                return false;
            }
            Vec3 next = _points.Count > 0 ? _points[0] : goal;
            Vec3 to = (next - ped.Position).Flat;
            float len = to.Length;
            if (len < 1e-4f)
            {
                PedSteering.Stop(ped, PedAnim.Idle);
                return false;
            }
            PedSteering.Go(ped, to / len, speed, moving, dt);
            return false;
        }
    }
}
