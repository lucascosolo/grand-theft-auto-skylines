using System;

namespace GtaSkylines.Game.Mathx
{
    /// <summary>
    /// A 3D vector in CS1/Unity world space (metres, y up, left-handed). Same semantics as
    /// <c>UnityEngine.Vector3</c> so the Mod converts by copying fields.
    /// </summary>
    [Serializable]
    public struct Vec3 : IEquatable<Vec3>
    {
        /// <summary>Components.</summary>
        public float X, Y, Z;

        /// <summary>Creates a vector.</summary>
        public Vec3(float x, float y, float z)
        {
            X = x; Y = y; Z = z;
        }

        /// <summary>(0, 0, 0).</summary>
        public static readonly Vec3 Zero = new Vec3(0f, 0f, 0f);

        /// <summary>(1, 1, 1).</summary>
        public static readonly Vec3 One = new Vec3(1f, 1f, 1f);

        /// <summary>(0, 1, 0).</summary>
        public static readonly Vec3 Up = new Vec3(0f, 1f, 0f);

        /// <summary>(0, 0, 1): north on the CS1 map, and the forward of an unrotated object.</summary>
        public static readonly Vec3 Forward = new Vec3(0f, 0f, 1f);

        /// <summary>(1, 0, 0).</summary>
        public static readonly Vec3 Right = new Vec3(1f, 0f, 0f);

        /// <summary>Component sum.</summary>
        public static Vec3 operator +(Vec3 a, Vec3 b) { return new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }

        /// <summary>Component difference.</summary>
        public static Vec3 operator -(Vec3 a, Vec3 b) { return new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }

        /// <summary>Negation.</summary>
        public static Vec3 operator -(Vec3 a) { return new Vec3(-a.X, -a.Y, -a.Z); }

        /// <summary>Scale.</summary>
        public static Vec3 operator *(Vec3 a, float s) { return new Vec3(a.X * s, a.Y * s, a.Z * s); }

        /// <summary>Scale.</summary>
        public static Vec3 operator *(float s, Vec3 a) { return new Vec3(a.X * s, a.Y * s, a.Z * s); }

        /// <summary>Division by a scalar.</summary>
        public static Vec3 operator /(Vec3 a, float s) { float k = 1f / s; return new Vec3(a.X * k, a.Y * k, a.Z * k); }

        /// <summary>Exact component equality.</summary>
        public static bool operator ==(Vec3 a, Vec3 b) { return a.X == b.X && a.Y == b.Y && a.Z == b.Z; }

        /// <summary>Exact component inequality.</summary>
        public static bool operator !=(Vec3 a, Vec3 b) { return !(a == b); }

        /// <summary>Dot product.</summary>
        public static float Dot(Vec3 a, Vec3 b) { return a.X * b.X + a.Y * b.Y + a.Z * b.Z; }

        /// <summary>Cross product (Unity's convention: Cross(Right, Up) = Forward).</summary>
        public static Vec3 Cross(Vec3 a, Vec3 b)
        {
            return new Vec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        }

        /// <summary>Component-wise product.</summary>
        public static Vec3 Scale(Vec3 a, Vec3 b) { return new Vec3(a.X * b.X, a.Y * b.Y, a.Z * b.Z); }

        /// <summary>Component-wise minimum.</summary>
        public static Vec3 Min(Vec3 a, Vec3 b) { return new Vec3(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z)); }

        /// <summary>Component-wise maximum.</summary>
        public static Vec3 Max(Vec3 a, Vec3 b) { return new Vec3(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z)); }

        /// <summary>Linear interpolation, t not clamped.</summary>
        public static Vec3 Lerp(Vec3 a, Vec3 b, float t) { return new Vec3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t); }

        /// <summary>Euclidean distance.</summary>
        public static float Distance(Vec3 a, Vec3 b) { return (a - b).Length; }

        /// <summary>Distance on the x, z plane.</summary>
        public static float DistanceXZ(Vec3 a, Vec3 b)
        {
            float dx = a.X - b.X, dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Length.</summary>
        public float Length { get { return (float)Math.Sqrt(X * X + Y * Y + Z * Z); } }

        /// <summary>Squared length.</summary>
        public float LengthSquared { get { return X * X + Y * Y + Z * Z; } }

        /// <summary>Length on the x, z plane.</summary>
        public float LengthXZ { get { return (float)Math.Sqrt(X * X + Z * Z); } }

        /// <summary>Unit vector in the same direction, or zero for a (near) zero vector.</summary>
        public Vec3 Normalized
        {
            get
            {
                float l = Length;
                return l > 1e-6f ? new Vec3(X / l, Y / l, Z / l) : Zero;
            }
        }

        /// <summary>The vector with y set to 0.</summary>
        public Vec3 Flat { get { return new Vec3(X, 0f, Z); } }

        /// <summary>The vector with a different y.</summary>
        public Vec3 WithY(float y) { return new Vec3(X, y, Z); }

        /// <summary>Clamps the length to <paramref name="max"/>.</summary>
        public static Vec3 ClampLength(Vec3 v, float max)
        {
            float l2 = v.LengthSquared;
            if (l2 <= max * max || l2 <= 0f) return v;
            return v * (max / (float)Math.Sqrt(l2));
        }

        /// <summary>Removes the component along unit normal <paramref name="n"/>.</summary>
        public static Vec3 ProjectOnPlane(Vec3 v, Vec3 n) { return v - n * Dot(v, n); }

        /// <summary>Heading in degrees of a direction on the x, z plane (0 = +z, 90 = +x), matching Unity's yaw.</summary>
        public static float YawDegOf(Vec3 dir) { return (float)(Math.Atan2(dir.X, dir.Z) * MathUtil.Rad2Deg); }

        /// <summary>Unit direction on the x, z plane for a yaw in degrees (0 = +z, 90 = +x).</summary>
        public static Vec3 FromYawDeg(float yawDeg)
        {
            double r = yawDeg * MathUtil.Deg2Rad;
            return new Vec3((float)Math.Sin(r), 0f, (float)Math.Cos(r));
        }

        /// <summary>True when every component is finite.</summary>
        public bool IsFinite
        {
            get { return !(float.IsNaN(X) || float.IsInfinity(X) || float.IsNaN(Y) || float.IsInfinity(Y) || float.IsNaN(Z) || float.IsInfinity(Z)); }
        }

        /// <inheritdoc />
        public bool Equals(Vec3 other) { return this == other; }

        /// <inheritdoc />
        public override bool Equals(object obj) { return obj is Vec3 && this == (Vec3)obj; }

        /// <inheritdoc />
        public override int GetHashCode() { return X.GetHashCode() ^ (Y.GetHashCode() << 2) ^ (Z.GetHashCode() >> 2); }

        /// <inheritdoc />
        public override string ToString()
        {
            return "(" + X.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ", "
                + Y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ", "
                + Z.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ")";
        }
    }
}
