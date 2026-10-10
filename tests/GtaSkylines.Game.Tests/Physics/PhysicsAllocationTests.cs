using System;
using System.Collections.Generic;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Tests.Fakes;
using Xunit;

namespace GtaSkylines.Game.Tests.PhysicsTests
{
    public class PhysicsAllocationTests
    {
        private static CollisionWorld Scene()
        {
            var w = new MeshBuilder()
                .Floor(-100, -100, 100, 100, 0)
                .Box(new Vec3(5, 0, -5), new Vec3(8, 2, 5))
                .Wall(-20, -50, -20, 50, 0, 6)
                .ToWorld();
            w.AddBox(new Vec3(0, 1, 6), new Vec3(1, 1, 2), Quat.Yaw(20f), 1);
            w.AddCapsule(new Vec3(-4, 0, 0), 0.35f, 1.8f, 2);
            return w;
        }

        private static void QueryRound(CollisionWorld w, List<Contact> contacts, float i)
        {
            PhysicsHit hit;
            float o = (i % 10f) - 5f;
            w.Raycast(new Vec3(o, 10, 0), new Vec3(0, -1, 0.01f), 50f, QueryMask.All, out hit);
            w.Raycast(new Vec3(-10, 1, o), new Vec3(1, 0, 0), 50f, QueryMask.All.Ignoring(2), out hit);
            w.SphereCast(new Vec3(o, 5, 1), 0.5f, new Vec3(0, -1, 0), 20f, QueryMask.All, out hit);
            w.CapsuleCast(new Vec3(o, 0, -3), 0.35f, 1.8f, new Vec3(0, 0, 1), 30f, QueryMask.All, out hit);
            w.CapsuleCast(new Vec3(-30, 0, o), 0.35f, 1.8f, new Vec3(1, 0, 0), 60f, QueryMask.StaticOnly, out hit);
            w.OverlapSphere(new Vec3(o, 0.3f, 0), 1f, QueryMask.All, contacts);
        }

        [Fact]
        public void QueriesAllocateNothingInSteadyState()
        {
            var w = Scene();
            var contacts = new List<Contact>(64);
            for (int i = 0; i < 300; i++) QueryRound(w, contacts, i);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) QueryRound(w, contacts, i);
            long grown = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.True(grown < 1024, "1000 query rounds allocated " + grown + " bytes");
        }

        [Fact]
        public void ReplacingDynamicShapesEveryFrameAllocatesNothingInSteadyState()
        {
            var w = Scene();
            for (int i = 0; i < 100; i++) { w.ClearDynamic(); w.AddBox(new Vec3(i * 0.01f, 1, 6), new Vec3(1, 1, 2), Quat.Yaw(20f), 1); w.AddCapsule(new Vec3(-4, 0, 0), 0.35f, 1.8f, 2); }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                w.ClearDynamic();
                w.AddBox(new Vec3(i * 0.001f, 1, 6), new Vec3(1, 1, 2), Quat.Yaw(20f), 1);
                w.AddCapsule(new Vec3(-4, 0, 0), 0.35f, 1.8f, 2);
            }
            long grown = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.True(grown < 1024, "1000 frames of dynamic shapes allocated " + grown + " bytes");
        }

        [Fact]
        public void MotorStepAllocatesNothingInSteadyState()
        {
            var w = Scene();
            var city = new FakeCity();
            var m = new CharacterMotor(null);
            m.Teleport(new Vec3(-10, 0, 0));
            var input = new MotorInput { DesiredVelocity = new Vec3(2, 0, 1), FacingYawDeg = 30f };
            for (int i = 0; i < 300; i++) m.Step(T.Dt, input, w, city);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                input.DesiredVelocity = (i / 120) % 2 == 0 ? new Vec3(2, 0, 1) : new Vec3(-2, 0, -1);
                input.Jump = i % 90 == 0;
                m.Step(T.Dt, input, w, city);
            }
            long grown = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.True(grown < 1024, "1000 motor steps allocated " + grown + " bytes");
        }
    }
}
