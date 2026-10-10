using System;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.Vehicles
{
    /// <summary>What a <see cref="VehicleImpact"/> hit.</summary>
    public enum ImpactKind
    {
        /// <summary>Static triangles of the CollisionWorld; <see cref="VehicleImpact.OtherTag"/> is −1.</summary>
        Static = 0,

        /// <summary>A dynamic box (a CS1 ambient vehicle), immovable for the body; the tag is the box's tag.</summary>
        Box = 1,

        /// <summary>A dynamic capsule (a pedestrian), a soft 80 kg obstacle; the tag is the capsule's tag.</summary>
        Capsule = 2,

        /// <summary>Another <see cref="VehicleBody"/>, from <see cref="VehicleBody.ResolveCollision"/>; the tag is the one passed for it.</summary>
        Vehicle = 3,
    }

    /// <summary>One collision the body took during a step. World space, metres.</summary>
    public struct VehicleImpact
    {
        /// <summary>Contact point.</summary>
        public Vec3 Point;

        /// <summary>Unit normal pointing from the other object into this body (the direction this body was pushed).</summary>
        public Vec3 Normal;

        /// <summary>
        /// Closing speed along <see cref="Normal"/> just before the impulse, m/s, always ≥
        /// <see cref="VehicleTuning.ImpactReportSpeed"/>: <c>−Dot(vSelf − vOther, Normal)</c> at the contact point, with
        /// vOther 0 for static geometry, boxes and capsules. A head-on hit into a wall at 20 m/s reports ≈ 20.
        /// </summary>
        public float RelativeNormalSpeed;

        /// <summary>Tag of the other object, or −1 for static geometry.</summary>
        public int OtherTag;

        /// <summary>What was hit.</summary>
        public ImpactKind Kind;

        /// <summary>Health this impact cost the body (0 below the damage threshold or once exploded).</summary>
        public float Damage;
    }

    /// <summary>
    /// Outcome of one <see cref="VehicleBody.Step"/>. A value type with inline storage for up to
    /// <see cref="MaxImpacts"/> impacts so stepping allocates nothing.
    /// </summary>
    public struct VehicleStepResult
    {
        /// <summary>Most impacts one step reports.</summary>
        public const int MaxImpacts = 4;

        private VehicleImpact _i0, _i1, _i2, _i3;

        /// <summary>
        /// Impacts recorded this step, 0..<see cref="MaxImpacts"/>. At most one per distinct (kind, tag) other, all static
        /// geometry counting as one other; each is that other's fastest contact over all sub-steps. When more others were
        /// hit, the fastest <see cref="MaxImpacts"/> are kept. Ordered by descending <see cref="VehicleImpact.RelativeNormalSpeed"/>.
        /// </summary>
        public int ImpactCount;

        /// <summary>True on the one step in which the body exploded (fire timer ran out).</summary>
        public bool Exploded;

        /// <summary>True on the step in which the last wheel lost contact (some wheel touched at the start of the step, none at its end).</summary>
        public bool LeftGround;

        /// <summary>
        /// True on the step in which a wheel regained contact after the body had been without any wheel contact for at
        /// least 0.25 s (so kerb hops do not count).
        /// </summary>
        public bool Landed;

        /// <summary>Impact <paramref name="index"/>, 0 ≤ index &lt; <see cref="ImpactCount"/>.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside [0, ImpactCount).</exception>
        public VehicleImpact GetImpact(int index)
        {
            if (index < 0 || index >= ImpactCount)
            {
                throw new ArgumentOutOfRangeException("index");
            }

            switch (index)
            {
                case 0: return _i0;
                case 1: return _i1;
                case 2: return _i2;
                default: return _i3;
            }
        }

        /// <summary>
        /// Records an impact: merges it with an existing one for the same (kind, tag) by keeping the faster, else adds it,
        /// replacing the slowest when full; keeps the descending order.
        /// </summary>
        internal void AddImpact(VehicleImpact impact)
        {
            throw new NotImplementedException();
        }

        internal void SetImpact(int index, VehicleImpact impact)
        {
            switch (index)
            {
                case 0: _i0 = impact; break;
                case 1: _i1 = impact; break;
                case 2: _i2 = impact; break;
                case 3: _i3 = impact; break;
                default: throw new ArgumentOutOfRangeException("index");
            }
        }
    }
}
