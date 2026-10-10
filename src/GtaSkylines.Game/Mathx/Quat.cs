using System;

namespace GtaSkylines.Game.Mathx
{
    /// <summary>
    /// A rotation quaternion with <c>UnityEngine.Quaternion</c>'s semantics (x, y, z, w; left-handed; Euler order
    /// z, then x, then y as Unity applies it), so the Mod converts by copying fields.
    /// </summary>
    [Serializable]
    public struct Quat
    {
        /// <summary>Components.</summary>
        public float X, Y, Z, W;

        /// <summary>Creates a quaternion from raw components (not normalised).</summary>
        public Quat(float x, float y, float z, float w)
        {
            X = x; Y = y; Z = z; W = w;
        }

        /// <summary>No rotation.</summary>
        public static readonly Quat Identity = new Quat(0f, 0f, 0f, 1f);

        /// <summary>Rotation of <paramref name="angleDeg"/> degrees about unit axis <paramref name="axis"/>.</summary>
        public static Quat AngleAxis(float angleDeg, Vec3 axis)
        {
            Vec3 n = axis.Normalized;
            double h = angleDeg * MathUtil.Deg2Rad * 0.5;
            float s = (float)Math.Sin(h);
            return new Quat(n.X * s, n.Y * s, n.Z * s, (float)Math.Cos(h));
        }

        /// <summary>Unity's <c>Quaternion.Euler(pitch, yaw, roll)</c>: roll about z, then pitch about x, then yaw about y.</summary>
        public static Quat Euler(float pitchDeg, float yawDeg, float rollDeg)
        {
            return AngleAxis(yawDeg, Vec3.Up) * AngleAxis(pitchDeg, Vec3.Right) * AngleAxis(rollDeg, Vec3.Forward);
        }

        /// <summary>Rotation about +y only.</summary>
        public static Quat Yaw(float yawDeg)
        {
            return AngleAxis(yawDeg, Vec3.Up);
        }

        /// <summary>Composition: (a * b) rotates by b first, then by a (Unity's convention).</summary>
        public static Quat operator *(Quat a, Quat b)
        {
            return new Quat(
                a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
                a.W * b.Y + a.Y * b.W + a.Z * b.X - a.X * b.Z,
                a.W * b.Z + a.Z * b.W + a.X * b.Y - a.Y * b.X,
                a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);
        }

        /// <summary>Rotates a vector.</summary>
        public static Vec3 operator *(Quat q, Vec3 v)
        {
            float x2 = q.X * 2f, y2 = q.Y * 2f, z2 = q.Z * 2f;
            float xx = q.X * x2, yy = q.Y * y2, zz = q.Z * z2;
            float xy = q.X * y2, xz = q.X * z2, yz = q.Y * z2;
            float wx = q.W * x2, wy = q.W * y2, wz = q.W * z2;
            return new Vec3(
                (1f - (yy + zz)) * v.X + (xy - wz) * v.Y + (xz + wy) * v.Z,
                (xy + wz) * v.X + (1f - (xx + zz)) * v.Y + (yz - wx) * v.Z,
                (xz - wy) * v.X + (yz + wx) * v.Y + (1f - (xx + yy)) * v.Z);
        }

        /// <summary>The inverse of a unit quaternion.</summary>
        public Quat Inverse { get { return new Quat(-X, -Y, -Z, W); } }

        /// <summary>The same rotation scaled to unit length (identity when degenerate).</summary>
        public Quat Normalized
        {
            get
            {
                float l = (float)Math.Sqrt(X * X + Y * Y + Z * Z + W * W);
                return l > 1e-8f ? new Quat(X / l, Y / l, Z / l, W / l) : Identity;
            }
        }

        /// <summary>The rotated +z axis.</summary>
        public Vec3 Forward { get { return this * Vec3.Forward; } }

        /// <summary>The rotated +y axis.</summary>
        public Vec3 Up { get { return this * Vec3.Up; } }

        /// <summary>The rotated +x axis.</summary>
        public Vec3 Right { get { return this * Vec3.Right; } }

        /// <summary>Heading in degrees of the rotated forward axis on the x, z plane.</summary>
        public float YawDeg { get { return Vec3.YawDegOf(Forward); } }

        /// <summary>Dot product of two quaternions.</summary>
        public static float Dot(Quat a, Quat b) { return a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W; }

        /// <summary>Spherical interpolation along the shorter arc, t clamped to [0, 1].</summary>
        public static Quat Slerp(Quat a, Quat b, float t)
        {
            t = MathUtil.Clamp01(t);
            float d = Dot(a, b);
            if (d < 0f) { b = new Quat(-b.X, -b.Y, -b.Z, -b.W); d = -d; }
            if (d > 0.9995f)
            {
                return new Quat(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t, a.W + (b.W - a.W) * t).Normalized;
            }
            double theta = Math.Acos(d);
            double s = Math.Sin(theta);
            float wa = (float)(Math.Sin((1 - t) * theta) / s), wb = (float)(Math.Sin(t * theta) / s);
            return new Quat(a.X * wa + b.X * wb, a.Y * wa + b.Y * wb, a.Z * wa + b.Z * wb, a.W * wa + b.W * wb);
        }

        /// <summary>
        /// Rotation whose forward is <paramref name="forward"/> and whose up is as close to <paramref name="up"/> as
        /// possible (Unity's <c>Quaternion.LookRotation</c>). Identity for a zero forward.
        /// </summary>
        public static Quat LookRotation(Vec3 forward, Vec3 up)
        {
            Vec3 f = forward.Normalized;
            if (f.LengthSquared < 1e-12f) return Identity;
            Vec3 r = Vec3.Cross(up, f);
            if (r.LengthSquared < 1e-12f) r = Vec3.Cross(Math.Abs(f.Y) < 0.99f ? Vec3.Up : Vec3.Right, f);
            r = r.Normalized;
            Vec3 u = Vec3.Cross(f, r);
            // Rotation matrix columns: r, u, f.
            float m00 = r.X, m01 = u.X, m02 = f.X;
            float m10 = r.Y, m11 = u.Y, m12 = f.Y;
            float m20 = r.Z, m21 = u.Z, m22 = f.Z;
            float trace = m00 + m11 + m22;
            Quat q;
            if (trace > 0f)
            {
                float s = (float)Math.Sqrt(trace + 1f) * 2f;
                q = new Quat((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25f * s);
            }
            else if (m00 > m11 && m00 > m22)
            {
                float s = (float)Math.Sqrt(1f + m00 - m11 - m22) * 2f;
                q = new Quat(0.25f * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s);
            }
            else if (m11 > m22)
            {
                float s = (float)Math.Sqrt(1f + m11 - m00 - m22) * 2f;
                q = new Quat((m01 + m10) / s, 0.25f * s, (m12 + m21) / s, (m02 - m20) / s);
            }
            else
            {
                float s = (float)Math.Sqrt(1f + m22 - m00 - m11) * 2f;
                q = new Quat((m02 + m20) / s, (m12 + m21) / s, 0.25f * s, (m10 - m01) / s);
            }
            return q.Normalized;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return "(" + X.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + ", " + Y.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + ", "
                + Z.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + ", " + W.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + ")";
        }
    }
}
