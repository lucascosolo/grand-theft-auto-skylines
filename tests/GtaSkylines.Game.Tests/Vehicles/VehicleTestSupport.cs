using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Vehicles;
using Xunit;

namespace GtaSkylines.Game.Tests.Vehicles
{
    /// <summary>Triangle-soup builders for vehicle scenes. Triangles are double-sided, so winding is irrelevant.</summary>
    internal sealed class Terrain
    {
        private readonly List<float> _p = new List<float>();
        private readonly List<ushort> _f = new List<ushort>();

        public Terrain Tri(Vec3 a, Vec3 b, Vec3 c, SurfaceFlags flags)
        {
            _p.AddRange(new[] { a.X, a.Y, a.Z, b.X, b.Y, b.Z, c.X, c.Y, c.Z });
            _f.Add((ushort)flags);
            return this;
        }

        public Terrain Quad(Vec3 a, Vec3 b, Vec3 c, Vec3 d, SurfaceFlags flags)
        {
            Tri(a, b, c, flags);
            return Tri(a, c, d, flags);
        }

        /// <summary>Two big triangles at height y covering [-half, half] on x and z.</summary>
        public Terrain Flat(float half, float y = 0f)
        {
            return Quad(new Vec3(-half, y, -half), new Vec3(half, y, -half), new Vec3(half, y, half), new Vec3(-half, y, half), SurfaceFlags.Road);
        }

        /// <summary>Horizontal rectangle at height y.</summary>
        public Terrain Floor(float x0, float z0, float x1, float z1, float y)
        {
            return Quad(new Vec3(x0, y, z0), new Vec3(x1, y, z0), new Vec3(x1, y, z1), new Vec3(x0, y, z1), SurfaceFlags.Road);
        }

        /// <summary>Slope rising along +z from (zStart, y0) over a horizontal run at the given angle; returns the end height.</summary>
        public Terrain Ramp(float x0, float x1, float zStart, float run, float angleDeg, float y0, out float topY)
        {
            topY = y0 + run * (float)Math.Tan(angleDeg * Math.PI / 180.0);
            return Quad(new Vec3(x0, y0, zStart), new Vec3(x1, y0, zStart), new Vec3(x1, topY, zStart + run), new Vec3(x0, topY, zStart + run), SurfaceFlags.Road);
        }

        /// <summary>Vertical wall across x in [x0, x1] at depth z, from y0 to y1.</summary>
        public Terrain WallAcross(float x0, float x1, float z, float y0, float y1)
        {
            return Quad(new Vec3(x0, y0, z), new Vec3(x1, y0, z), new Vec3(x1, y1, z), new Vec3(x0, y1, z), SurfaceFlags.Building);
        }

        /// <summary>Closed thin slab spanning x in [x0, x1], z in [z, z + thickness], y in [y0, y1] (12 triangles).</summary>
        public Terrain Slab(float x0, float x1, float z, float thickness, float y0, float y1)
        {
            float z1 = z + thickness;
            WallAcross(x0, x1, z, y0, y1);
            WallAcross(x0, x1, z1, y0, y1);
            Floor(x0, z, x1, z1, y0);
            Floor(x0, z, x1, z1, y1);
            Quad(new Vec3(x0, y0, z), new Vec3(x0, y0, z1), new Vec3(x0, y1, z1), new Vec3(x0, y1, z), SurfaceFlags.Building);
            return Quad(new Vec3(x1, y0, z), new Vec3(x1, y0, z1), new Vec3(x1, y1, z1), new Vec3(x1, y1, z), SurfaceFlags.Building);
        }

        /// <summary>Gently bumpy square patch of the given half size, 2 m cells, heights from a fixed formula (about 0.15 m amplitude).</summary>
        public Terrain Bumpy(float half)
        {
            int n = (int)(2f * half / 2f);
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    float x0 = -half + 2f * i, z0 = -half + 2f * j, x1 = x0 + 2f, z1 = z0 + 2f;
                    Quad(new Vec3(x0, H(x0, z0), z0), new Vec3(x1, H(x1, z0), z0), new Vec3(x1, H(x1, z1), z1), new Vec3(x0, H(x0, z1), z1), SurfaceFlags.Terrain);
                }
            }
            return this;
        }

        public static float H(float x, float z)
        {
            return 0.15f * (float)(Math.Sin(x * 0.7) * Math.Cos(z * 0.9));
        }

        public Terrain Apply(CollisionWorld world, long key)
        {
            world.SetRegion(key, _p.ToArray(), _f.ToArray(), _f.Count);
            return this;
        }

        public CollisionWorld ToWorld(long key = 1)
        {
            var w = new CollisionWorld();
            Apply(w, key);
            return w;
        }
    }

    /// <summary>Models and assertion helpers.</summary>
    internal static class V
    {
        public const float Dt = 1f / 60f;
        public const int Tag = 1;

        /// <summary>A sedan: 0.9 x 0.75 x 2.2 half extents; mass about 1400 kg; top speed 1.4 x maxSpeed.</summary>
        public static VehicleModel Car(float maxSpeed = 20f, VehicleKind kind = VehicleKind.Car)
        {
            return new VehicleModel { Id = 100, Name = "Test car", Kind = kind, MaxSpeed = maxSpeed, HalfExtents = new Vec3(0.9f, 0.75f, 2.2f) };
        }

        public static VehicleModel Truck()
        {
            return new VehicleModel { Id = 101, Name = "Test truck", Kind = VehicleKind.Truck, MaxSpeed = 22f, HalfExtents = new Vec3(1.2f, 1.7f, 4f) };
        }

        public static void Near(float expected, float actual, float tol)
        {
            Assert.True(Math.Abs(expected - actual) <= tol, "expected " + expected + " +/- " + tol + " but was " + actual);
        }

        public static bool Finite(Vec3 v)
        {
            return !float.IsNaN(v.X + v.Y + v.Z) && !float.IsInfinity(v.X + v.Y + v.Z);
        }

        public static bool Finite(Quat q)
        {
            float s = q.X + q.Y + q.Z + q.W;
            return !float.IsNaN(s) && !float.IsInfinity(s);
        }

        /// <summary>Largest world z over the corners of the collision box (bottom clearance excluded).</summary>
        public static float MaxCornerZ(VehicleBody b)
        {
            VehicleTuning t = b.Tuning;
            float max = float.MinValue;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    for (int k = 0; k < 2; k++)
                    {
                        float ly = k == 0 ? -t.HalfExtents.Y + t.BodyClearance : t.HalfExtents.Y;
                        Vec3 p = b.Position + b.Rotation * new Vec3(sx * t.HalfExtents.X, ly, sz * t.HalfExtents.Z);
                        max = Math.Max(max, p.Z);
                    }
                }
            }
            return max;
        }

        public static float Clamp01(float v)
        {
            return v < 0f ? 0f : v > 1f ? 1f : v;
        }
    }

    /// <summary>A body, its world and a fixed 1/60 s driver loop.</summary>
    internal sealed class Sim
    {
        public readonly VehicleBody Body;
        public readonly CollisionWorld World;
        public readonly VehicleTuning Tuning;
        public VehicleStepResult Last;
        public int Steps;
        public float Time { get { return Steps * V.Dt; } }

        public Sim(VehicleModel model, CollisionWorld world, Vec3? position = null, Quat? rotation = null)
        {
            Tuning = VehicleTuning.FromModel(model);
            World = world;
            Body = new VehicleBody(Tuning);
            Body.Teleport(position ?? new Vec3(0f, Tuning.HalfExtents.Y + 0.02f, 0f), rotation ?? Quat.Identity);
        }

        public VehicleStepResult Step(VehicleControls c)
        {
            Last = Body.Step(V.Dt, c, World, V.Tag);
            Steps++;
            return Last;
        }

        public void Run(VehicleControls c, float seconds)
        {
            int n = (int)Math.Round(seconds / V.Dt);
            for (int i = 0; i < n; i++)
            {
                Step(c);
            }
        }

        /// <summary>Throttle that holds a forward speed (proportional controller, brakes only far above it).</summary>
        public VehicleControls Hold(float targetSpeed, float steer = 0f)
        {
            float err = targetSpeed - Body.Speed;
            var c = new VehicleControls { Steer = steer };
            if (err > 0f)
            {
                c.Throttle = V.Clamp01(err * 0.5f);
            }
            else if (err < -2f)
            {
                c.Brake = V.Clamp01((-err - 2f) * 0.3f);
            }
            return c;
        }

        /// <summary>Drives straight with <see cref="Hold"/> until Speed reaches the target (fails after maxSeconds).</summary>
        public void DriveTo(float speed, float maxSeconds = 40f, float steer = 0f)
        {
            float limit = Time + maxSeconds;
            while (Body.Speed < speed && Time < limit)
            {
                Step(new VehicleControls { Throttle = 1f, Steer = steer });
            }
            Assert.True(Body.Speed >= speed, "never reached " + speed + " m/s, got " + Body.Speed);
        }
    }
}
