using System;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Mod.Rendering
{
    /// <summary>Pure maths for the police helicopter: body tilt, rotor travel and the spotlight beam transform.</summary>
    public static class HeliMath
    {
        /// <summary>Half angle of the drawn spotlight cone, degrees.</summary>
        public const float BeamHalfAngleDeg = 12f;
        /// <summary>Rotor travel per second (the value CS1 feeds the vehicle shader as <c>_TyrePosition.y</c>; rate is a visual guess).</summary>
        public const float RotorRate = 60f;
        /// <summary>Radians of body tilt per m/s of speed.</summary>
        public const float TiltPerSpeed = 0.012f;
        /// <summary>Largest body tilt, radians.</summary>
        public const float MaxTilt = 0.3f;
        /// <summary>Beams shorter than this are not drawn, metres.</summary>
        public const float MinBeamLength = 1f;

        /// <summary>
        /// Body lean (about the forward axis, positive banks right) and nod (positive noses down), radians, from
        /// <paramref name="velocity"/> and <paramref name="yawDeg"/>: proportional to the speed along the right and forward axes, clamped.
        /// </summary>
        public static void Tilt(Vec3 velocity, float yawDeg, out float lean, out float nod)
        {
            Quat yaw = Quat.Yaw(yawDeg);
            lean = Clamp(Vec3.Dot(velocity, yaw * Vec3.Right) * TiltPerSpeed);
            nod = Clamp(Vec3.Dot(velocity, yaw * Vec3.Forward) * TiltPerSpeed);
        }

        /// <summary>
        /// The spotlight cone (<see cref="MeshShapes.Cone"/>: apex at the origin, opening along +z to radius 1 at z = 1) from
        /// <paramref name="from"/> to <paramref name="to"/>: its rotation, base radius and length. False when the beam is
        /// shorter than <see cref="MinBeamLength"/> or not finite.
        /// </summary>
        public static bool Beam(Vec3 from, Vec3 to, out Quat rotation, out float radius, out float length)
        {
            Vec3 d = to - from;
            length = d.Length;
            rotation = Quat.Identity;
            radius = 0f;
            if (!d.IsFinite || length < MinBeamLength) return false;
            rotation = Quat.LookRotation(d, Vec3.Forward);
            radius = length * (float)Math.Tan(BeamHalfAngleDeg * MathUtil.Deg2Rad);
            return true;
        }

        private static float Clamp(float v)
        {
            return v < -MaxTilt ? -MaxTilt : v > MaxTilt ? MaxTilt : v;
        }
    }
}
