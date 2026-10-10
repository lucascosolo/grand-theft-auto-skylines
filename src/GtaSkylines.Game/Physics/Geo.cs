using System;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.Physics
{
    /// <summary>
    /// Narrow-phase primitives. Capsules are vertical: a segment from a bottom centre up by <c>len</c> (0 for a sphere)
    /// with a radius. Callers pass coordinates relative to the query origin to keep float precision at city scale.
    /// </summary>
    internal static class Geo
    {
        /// <summary>Penetration at or below this is touching, not overlap.</summary>
        internal const float PenEps = 1e-4f;

        /// <summary>A contact counts only when the motion closes on it faster than this (filters grazing seams).</summary>
        internal const float ApproachEps = 1e-4f;

        internal static readonly Vec3 Up = new Vec3(0f, 1f, 0f);

        internal static bool InsideTri(Vec3 p, Vec3 a, Vec3 b, Vec3 c)
        {
            double v0x = b.X - a.X, v0y = b.Y - a.Y, v0z = b.Z - a.Z;
            double v1x = c.X - a.X, v1y = c.Y - a.Y, v1z = c.Z - a.Z;
            double v2x = p.X - a.X, v2y = p.Y - a.Y, v2z = p.Z - a.Z;
            double d00 = v0x * v0x + v0y * v0y + v0z * v0z;
            double d01 = v0x * v1x + v0y * v1y + v0z * v1z;
            double d11 = v1x * v1x + v1y * v1y + v1z * v1z;
            double d20 = v2x * v0x + v2y * v0y + v2z * v0z;
            double d21 = v2x * v1x + v2y * v1y + v2z * v1z;
            double den = d00 * d11 - d01 * d01;
            if (den <= 0.0) return false;
            double v = (d11 * d20 - d01 * d21) / den, w = (d00 * d21 - d01 * d20) / den;
            const double Tol = 1e-5;
            return v >= -Tol && w >= -Tol && v + w <= 1.0 + Tol;
        }

        internal static Vec3 ClosestPtTriangle(Vec3 p, Vec3 a, Vec3 b, Vec3 c)
        {
            Vec3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = Vec3.Dot(ab, ap), d2 = Vec3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return a;
            Vec3 bp = p - b;
            float d3 = Vec3.Dot(ab, bp), d4 = Vec3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return b;
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f) return a + ab * (d1 / (d1 - d3));
            Vec3 cp = p - c;
            float d5 = Vec3.Dot(ab, cp), d6 = Vec3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return c;
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f) return a + ac * (d2 / (d2 - d6));
            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f) return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
            float inv = 1f / (va + vb + vc);
            return a + ab * (vb * inv) + ac * (vc * inv);
        }

        /// <summary>Closest points of segments p1q1 and p2q2; returns the squared distance.</summary>
        internal static float ClosestSegSeg(Vec3 p1, Vec3 q1, Vec3 p2, Vec3 q2, out float s, out float t, out Vec3 c1, out Vec3 c2)
        {
            Vec3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vec3.Dot(d1, d1), e = Vec3.Dot(d2, d2), f = Vec3.Dot(d2, r);
            const float Eps = 1e-12f;
            if (a <= Eps && e <= Eps)
            {
                s = t = 0f;
            }
            else if (a <= Eps)
            {
                s = 0f;
                t = MathUtil.Clamp01(f / e);
            }
            else
            {
                float c = Vec3.Dot(d1, r);
                if (e <= Eps)
                {
                    t = 0f;
                    s = MathUtil.Clamp01(-c / a);
                }
                else
                {
                    float b = Vec3.Dot(d1, d2);
                    float denom = a * e - b * b;
                    s = denom > 1e-9f * a * e ? MathUtil.Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f) { t = 0f; s = MathUtil.Clamp01(-c / a); }
                    else if (t > 1f) { t = 1f; s = MathUtil.Clamp01((b - c) / a); }
                }
            }
            c1 = p1 + d1 * s;
            c2 = p2 + d2 * t;
            return (c1 - c2).LengthSquared;
        }

        /// <summary>First t ≥ 0 at which o + D·t is within R of c (0 when it starts inside and is not leaving).</summary>
        internal static bool RaySphere(Vec3 o, Vec3 d, Vec3 c, float r, out float t)
        {
            t = 0f;
            Vec3 m = o - c;
            float b = Vec3.Dot(m, d), cc = Vec3.Dot(m, m) - r * r;
            if (cc > 0f && b > 0f) return false;
            if (cc <= 0f) return b < 0f;
            float disc = b * b - cc;
            if (disc < 0f) return false;
            t = -b - (float)Math.Sqrt(disc);
            if (t < 0f) t = 0f;
            return true;
        }

        /// <summary>Ray against the infinite cylinder of radius r around segment pq; u is the axis parameter at contact.</summary>
        internal static bool RayCylinder(Vec3 o, Vec3 dir, Vec3 p, Vec3 q, float r, out float t, out float u)
        {
            t = u = 0f;
            Vec3 d = q - p, m = o - p;
            float dd = Vec3.Dot(d, d);
            if (dd < 1e-12f) return false;
            float md = Vec3.Dot(m, d), nd = Vec3.Dot(dir, d), mn = Vec3.Dot(m, dir);
            float a = dd - nd * nd;
            if (a < 1e-9f * dd) return false;
            float k = Vec3.Dot(m, m) - r * r;
            float c = dd * k - md * md;
            float b = dd * mn - nd * md;
            if (c < 0f)
            {
                if (b >= 0f) return false;
                t = 0f;
            }
            else
            {
                if (b >= 0f) return false;
                float disc = b * b - a * c;
                if (disc < 0f) return false;
                t = (-b - (float)Math.Sqrt(disc)) / a;
                if (t < 0f) t = 0f;
            }
            u = (md + t * nd) / dd;
            return u >= 0f && u <= 1f;
        }

        /// <summary>Ray against the vertical infinite cylinder through (cx, cz).</summary>
        internal static bool RayCylinderY(Vec3 o, Vec3 d, float cx, float cz, float r, out float t)
        {
            t = 0f;
            float wx = o.X - cx, wz = o.Z - cz;
            float a = d.X * d.X + d.Z * d.Z;
            if (a < 1e-12f) return false;
            float b = wx * d.X + wz * d.Z, c = wx * wx + wz * wz - r * r;
            if (c > 0f && b > 0f) return false;
            if (c <= 0f) return b < 0f;
            float disc = b * b - a * c;
            if (disc < 0f) return false;
            t = (-b - (float)Math.Sqrt(disc)) / a;
            if (t < 0f) t = 0f;
            return true;
        }

        /// <summary>First t at which o + D·t is within r of the vertical segment (cx, y0..y1, cz).</summary>
        internal static bool RayVerticalCapsule(Vec3 o, Vec3 d, float cx, float y0, float y1, float cz, float r, out float t)
        {
            t = float.MaxValue;
            float tt;
            if (RayCylinderY(o, d, cx, cz, r, out tt))
            {
                float y = o.Y + d.Y * tt;
                if (y >= y0 && y <= y1) t = tt;
            }
            if (RaySphere(o, d, new Vec3(cx, y0, cz), r, out tt) && tt < t) t = tt;
            if (y1 > y0 && RaySphere(o, d, new Vec3(cx, y1, cz), r, out tt) && tt < t) t = tt;
            return t != float.MaxValue;
        }

        /// <summary>
        /// Penetration of the vertical capsule (bottom centre s0, length len, radius r) into triangle abc with unit
        /// normal n. Returns false when the depth is not above <see cref="PenEps"/>.
        /// </summary>
        internal static bool OverlapTri(Vec3 s0, float len, float r, Vec3 a, Vec3 b, Vec3 c, Vec3 n, out Vec3 point, out Vec3 normal, out float depth)
        {
            Vec3 s1 = new Vec3(s0.X, s0.Y + len, s0.Z);
            float d0 = Vec3.Dot(s0 - a, n), d1 = d0 + len * n.Y;
            if (len > 0f && ((d0 < 0f && d1 > 0f) || (d0 > 0f && d1 < 0f)))
            {
                Vec3 x = s0 + (s1 - s0) * (d0 / (d0 - d1));
                if (InsideTri(x, a, b, c))
                {
                    float sg = d0 + d1 >= 0f ? 1f : -1f;
                    normal = n * sg;
                    depth = r + Math.Max(-sg * d0, -sg * d1);
                    point = x;
                    return true;
                }
            }
            Vec3 ps = s0, pt = ClosestPtTriangle(s0, a, b, c);
            float best = (ps - pt).LengthSquared;
            if (len > 0f)
            {
                Vec3 q = ClosestPtTriangle(s1, a, b, c);
                float d = (s1 - q).LengthSquared;
                if (d < best) { best = d; ps = s1; pt = q; }
                float s, t;
                Vec3 c1, c2;
                d = ClosestSegSeg(s0, s1, a, b, out s, out t, out c1, out c2);
                if (d < best) { best = d; ps = c1; pt = c2; }
                d = ClosestSegSeg(s0, s1, b, c, out s, out t, out c1, out c2);
                if (d < best) { best = d; ps = c1; pt = c2; }
                d = ClosestSegSeg(s0, s1, c, a, out s, out t, out c1, out c2);
                if (d < best) { best = d; ps = c1; pt = c2; }
            }
            float dist = (float)Math.Sqrt(best);
            point = pt;
            depth = r - dist;
            if (depth <= PenEps)
            {
                normal = Vec3.Zero;
                return false;
            }
            normal = dist > 1e-6f ? (ps - pt) / dist : (d0 + d1 < 0f ? -n : n);
            return true;
        }

        /// <summary>
        /// Sweeps the vertical capsule whose bottom centre is at the origin (length len, radius r) along unit d against
        /// triangle abc (unit normal n). Improves bestT/bestP/bestN when it finds an earlier approaching contact within
        /// maxT. Assumes no penetration deeper than <see cref="PenEps"/> at the start.
        /// </summary>
        internal static bool SweepTri(float len, float r, Vec3 d, float maxT, Vec3 a, Vec3 b, Vec3 c, Vec3 n,
            ref float bestT, ref Vec3 bestP, ref Vec3 bestN)
        {
            float limit = Math.Min(maxT, bestT);
            bool found = false;
            Vec3 top = new Vec3(0f, len, 0f);

            float d0 = -Vec3.Dot(a, n), d1 = d0 + len * n.Y;
            if (!((d0 > 0f && d1 < 0f) || (d0 < 0f && d1 > 0f)))
            {
                float sg = d0 + d1 >= 0f ? 1f : -1f;
                Vec3 nn = n * sg;
                float dn = Vec3.Dot(d, nn);
                if (dn < -ApproachEps)
                {
                    float e0 = d0 * sg, e1 = d1 * sg, dm = Math.Min(e0, e1);
                    float t = (dm - r) / -dn;
                    if (t < 0f) t = 0f;
                    if (t <= limit)
                    {
                        if (e0 <= dm + 1e-5f)
                        {
                            Vec3 p = d * t - nn * r;
                            if (InsideTri(p, a, b, c)) { Take(t, p, nn, ref limit, ref bestT, ref bestP, ref bestN); found = true; }
                        }
                        if (len > 0f && e1 <= dm + 1e-5f && t <= limit)
                        {
                            Vec3 p = top + d * t - nn * r;
                            if (InsideTri(p, a, b, c)) { Take(t, p, nn, ref limit, ref bestT, ref bestP, ref bestN); found = true; }
                        }
                    }
                }
            }

            found |= Edge(Vec3.Zero, d, a, b, r, ref limit, ref bestT, ref bestP, ref bestN);
            found |= Edge(Vec3.Zero, d, b, c, r, ref limit, ref bestT, ref bestP, ref bestN);
            found |= Edge(Vec3.Zero, d, c, a, r, ref limit, ref bestT, ref bestP, ref bestN);
            found |= Vertex(Vec3.Zero, d, a, r, ref limit, ref bestT, ref bestP, ref bestN);
            found |= Vertex(Vec3.Zero, d, b, r, ref limit, ref bestT, ref bestP, ref bestN);
            found |= Vertex(Vec3.Zero, d, c, r, ref limit, ref bestT, ref bestP, ref bestN);
            if (len > 0f)
            {
                found |= Edge(top, d, a, b, r, ref limit, ref bestT, ref bestP, ref bestN);
                found |= Edge(top, d, b, c, r, ref limit, ref bestT, ref bestP, ref bestN);
                found |= Edge(top, d, c, a, r, ref limit, ref bestT, ref bestP, ref bestN);
                found |= Vertex(top, d, a, r, ref limit, ref bestT, ref bestP, ref bestN);
                found |= Vertex(top, d, b, r, ref limit, ref bestT, ref bestP, ref bestN);
                found |= Vertex(top, d, c, r, ref limit, ref bestT, ref bestP, ref bestN);
                found |= AxisEdge(len, d, a, b, r, ref limit, ref bestT, ref bestP, ref bestN);
                found |= AxisEdge(len, d, b, c, r, ref limit, ref bestT, ref bestP, ref bestN);
                found |= AxisEdge(len, d, c, a, r, ref limit, ref bestT, ref bestP, ref bestN);
                found |= AxisVertex(len, d, a, r, ref limit, ref bestT, ref bestP, ref bestN);
                found |= AxisVertex(len, d, b, r, ref limit, ref bestT, ref bestP, ref bestN);
                found |= AxisVertex(len, d, c, r, ref limit, ref bestT, ref bestP, ref bestN);
            }
            return found;
        }

        private static void Take(float t, Vec3 p, Vec3 n, ref float limit, ref float bestT, ref Vec3 bestP, ref Vec3 bestN)
        {
            bestT = t;
            bestP = p;
            bestN = n;
            limit = t;
        }

        /// <summary>End sphere (centre e) against the edge pq.</summary>
        private static bool Edge(Vec3 e, Vec3 d, Vec3 p, Vec3 q, float r, ref float limit, ref float bestT, ref Vec3 bestP, ref Vec3 bestN)
        {
            float t, u;
            if (!RayCylinder(e, d, p, q, r, out t, out u) || t >= limit) return false;
            Vec3 at = p + (q - p) * u;
            Vec3 n = (e + d * t - at).Normalized;
            if (Vec3.Dot(d, n) >= -ApproachEps) return false;
            Take(t, at, n, ref limit, ref bestT, ref bestP, ref bestN);
            return true;
        }

        /// <summary>End sphere (centre e) against vertex v.</summary>
        private static bool Vertex(Vec3 e, Vec3 d, Vec3 v, float r, ref float limit, ref float bestT, ref Vec3 bestP, ref Vec3 bestN)
        {
            float t;
            if (!RaySphere(e, d, v, r, out t) || t >= limit) return false;
            Vec3 n = (e + d * t - v).Normalized;
            if (Vec3.Dot(d, n) >= -ApproachEps) return false;
            Take(t, v, n, ref limit, ref bestT, ref bestP, ref bestN);
            return true;
        }

        /// <summary>Capsule axis interior against edge pq interior.</summary>
        private static bool AxisEdge(float len, Vec3 d, Vec3 p, Vec3 q, float r, ref float limit, ref float bestT, ref Vec3 bestP, ref Vec3 bestN)
        {
            Vec3 e = q - p;
            Vec3 nc = Vec3.Cross(Up, e);
            float l2 = nc.LengthSquared;
            if (l2 < 1e-10f * e.LengthSquared || l2 < 1e-14f) return false;
            nc = nc / (float)Math.Sqrt(l2);
            float d0 = -Vec3.Dot(p, nc);
            float sg = d0 >= 0f ? 1f : -1f;
            float ad = d0 * sg, dd = Vec3.Dot(d, nc) * sg;
            if (dd >= -ApproachEps || ad < r - PenEps) return false;
            float t = (ad - r) / -dd;
            if (t < 0f) t = 0f;
            if (t >= limit) return false;
            Vec3 b0 = d * t;
            float s, u;
            Vec3 c1, c2;
            ClosestSegSeg(b0, b0 + new Vec3(0f, len, 0f), p, q, out s, out u, out c1, out c2);
            if (s <= 0f || s >= 1f || u <= 0f || u >= 1f) return false;
            Take(t, c2, nc * sg, ref limit, ref bestT, ref bestP, ref bestN);
            return true;
        }

        /// <summary>Capsule cylinder side against vertex v.</summary>
        private static bool AxisVertex(float len, Vec3 d, Vec3 v, float r, ref float limit, ref float bestT, ref Vec3 bestP, ref Vec3 bestN)
        {
            float t;
            if (!RayCylinderY(v, -d, 0f, 0f, r, out t) || t >= limit) return false;
            float y = v.Y - d.Y * t;
            if (y < 0f || y > len) return false;
            Vec3 n = new Vec3(d.X * t - v.X, 0f, d.Z * t - v.Z).Normalized;
            if (Vec3.Dot(d, n) >= -ApproachEps) return false;
            Take(t, v, n, ref limit, ref bestT, ref bestP, ref bestN);
            return true;
        }
    }
}
