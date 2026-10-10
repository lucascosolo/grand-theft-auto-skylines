using System;
using System.Collections.Generic;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Tests.Fakes;
using Xunit;

namespace GtaSkylines.Game.Tests.PhysicsTests
{
    /// <summary>Builds triangle soups for tests. Triangles are double-sided in the world, so winding is irrelevant.</summary>
    internal sealed class MeshBuilder
    {
        private readonly List<float> _p = new List<float>();
        private readonly List<ushort> _f = new List<ushort>();

        public int Count { get { return _f.Count; } }

        public MeshBuilder Tri(Vec3 a, Vec3 b, Vec3 c, SurfaceFlags flags = SurfaceFlags.Terrain)
        {
            _p.AddRange(new[] { a.X, a.Y, a.Z, b.X, b.Y, b.Z, c.X, c.Y, c.Z });
            _f.Add((ushort)flags);
            return this;
        }

        public MeshBuilder Quad(Vec3 a, Vec3 b, Vec3 c, Vec3 d, SurfaceFlags flags = SurfaceFlags.Terrain)
        {
            Tri(a, b, c, flags);
            return Tri(a, c, d, flags);
        }

        /// <summary>Horizontal rectangle at height y.</summary>
        public MeshBuilder Floor(float x0, float z0, float x1, float z1, float y, SurfaceFlags flags = SurfaceFlags.Terrain)
        {
            return Quad(new Vec3(x0, y, z0), new Vec3(x1, y, z0), new Vec3(x1, y, z1), new Vec3(x0, y, z1), flags);
        }

        /// <summary>Vertical rectangle above the segment (x0,z0)-(x1,z1), from y0 to y1.</summary>
        public MeshBuilder Wall(float x0, float z0, float x1, float z1, float y0, float y1, SurfaceFlags flags = SurfaceFlags.Building)
        {
            return Quad(new Vec3(x0, y0, z0), new Vec3(x1, y0, z1), new Vec3(x1, y1, z1), new Vec3(x0, y1, z0), flags);
        }

        /// <summary>Closed axis-aligned box (12 triangles).</summary>
        public MeshBuilder Box(Vec3 min, Vec3 max, SurfaceFlags flags = SurfaceFlags.Building)
        {
            Floor(min.X, min.Z, max.X, max.Z, min.Y, flags);
            Floor(min.X, min.Z, max.X, max.Z, max.Y, flags);
            Wall(min.X, min.Z, max.X, min.Z, min.Y, max.Y, flags);
            Wall(min.X, max.Z, max.X, max.Z, min.Y, max.Y, flags);
            Wall(min.X, min.Z, min.X, max.Z, min.Y, max.Y, flags);
            Wall(max.X, min.Z, max.X, max.Z, min.Y, max.Y, flags);
            return this;
        }

        /// <summary>Slab rising along +z at the given angle from z = zStart (y = 0 there) over a horizontal run, width x0..x1.</summary>
        public MeshBuilder Ramp(float x0, float x1, float zStart, float run, float angleDeg, SurfaceFlags flags = SurfaceFlags.Terrain)
        {
            float rise = run * (float)Math.Tan(angleDeg * Math.PI / 180.0);
            return Quad(new Vec3(x0, 0, zStart), new Vec3(x1, 0, zStart), new Vec3(x1, rise, zStart + run), new Vec3(x0, rise, zStart + run), flags);
        }

        public MeshBuilder Apply(CollisionWorld world, long key = 1)
        {
            world.SetRegion(key, _p.ToArray(), _f.ToArray(), Count);
            return this;
        }

        public CollisionWorld ToWorld(long key = 1)
        {
            var w = new CollisionWorld();
            Apply(w, key);
            return w;
        }
    }

    internal static class T
    {
        public const float Dt = 1f / 60f;

        public static void Near(float expected, float actual, float tol = 1e-3f)
        {
            Assert.True(Math.Abs(expected - actual) <= tol, "expected " + expected + " +/- " + tol + " but was " + actual);
        }

        public static void Near(Vec3 expected, Vec3 actual, float tol = 1e-3f)
        {
            Assert.True(Vec3.Distance(expected, actual) <= tol, "expected " + expected + " +/- " + tol + " but was " + actual);
        }

        public static MeshBuilder BigFloor(float half = 300f, float y = 0f)
        {
            return new MeshBuilder().Floor(-half, -half, half, half, y);
        }
    }

    /// <summary>Drives a CharacterMotor at 1/60 s and records what happened.</summary>
    internal sealed class Sim
    {
        public readonly CollisionWorld World;
        public readonly FakeCity City;
        public readonly CharacterMotor Motor;
        public bool AnyLanded, AnyJumped, AnyClimbStarted, AnyClimbFinished, AnyEntered, AnyLeft;
        public float FirstImpact = -1f;
        public float MinY, MaxY;
        public int NotGroundedSteps;
        public int Steps;

        public Sim(CollisionWorld world, Vec3 feet, FakeCity city = null)
        {
            World = world;
            City = city ?? new FakeCity();
            Motor = new CharacterMotor(null);
            Motor.Teleport(feet);
            MinY = MaxY = feet.Y;
        }

        public MotorStepResult Step(Vec3 desired, float yawDeg = 0f, bool jump = false)
        {
            var input = new MotorInput { DesiredVelocity = desired, FacingYawDeg = yawDeg, Jump = jump };
            MotorStepResult r = Motor.Step(T.Dt, input, World, City);
            Steps++;
            if (r.Landed) { if (!AnyLanded) FirstImpact = r.ImpactSpeed; AnyLanded = true; }
            AnyJumped |= r.Jumped; AnyClimbStarted |= r.ClimbStarted; AnyClimbFinished |= r.ClimbFinished;
            AnyEntered |= r.EnteredWater; AnyLeft |= r.LeftWater;
            float y = Motor.Position.Y;
            if (y < MinY) MinY = y;
            if (y > MaxY) MaxY = y;
            if (Motor.State != MotorState.Grounded) NotGroundedSteps++;
            return r;
        }

        public void Run(int steps, Vec3 desired, float yawDeg = 0f)
        {
            for (int i = 0; i < steps; i++) Step(desired, yawDeg);
        }

        /// <summary>Lets the motor settle on the ground with no input and clears the recorded extremes.</summary>
        public Sim Settle(int steps = 15)
        {
            Run(steps, Vec3.Zero);
            MinY = MaxY = Motor.Position.Y;
            NotGroundedSteps = 0;
            AnyLanded = false; FirstImpact = -1f;
            return this;
        }

        public bool Overlapping()
        {
            PhysicsHit h;
            return World.CapsuleCast(Motor.Position, Motor.Settings.Radius, Motor.Settings.Height, Vec3.Zero, 0f, Motor.Mask, out h);
        }
    }
}
