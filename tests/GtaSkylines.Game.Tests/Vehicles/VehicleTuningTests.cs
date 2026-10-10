using System;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Vehicles;
using Xunit;

namespace GtaSkylines.Game.Tests.Vehicles
{
    public class VehicleTuningTests
    {
        private static VehicleModel Model(VehicleKind kind, float hx, float hy, float hz, float maxSpeed = 30f)
        {
            return new VehicleModel { Kind = kind, HalfExtents = new Vec3(hx, hy, hz), MaxSpeed = maxSpeed };
        }

        [Fact]
        public void Mass_FollowsBoxVolumeByKind()
        {
            V.Near(1400f, VehicleTuning.FromModel(Model(VehicleKind.Car, 0.9f, 0.75f, 2.2f)).Mass, 70f);
            V.Near(9000f, VehicleTuning.FromModel(Model(VehicleKind.Truck, 1.25f, 1.6f, 4.5f)).Mass, 450f);
            V.Near(11000f, VehicleTuning.FromModel(Model(VehicleKind.Bus, 1.3f, 1.6f, 6f)).Mass, 550f);
        }

        [Fact]
        public void Mass_IsClampedTo800And15000()
        {
            Assert.Equal(800f, VehicleTuning.FromModel(Model(VehicleKind.Car, 0.5f, 0.4f, 1f)).Mass, 0.01);
            Assert.Equal(15000f, VehicleTuning.FromModel(Model(VehicleKind.Bus, 3f, 4f, 15f)).Mass, 0.01);
        }

        [Fact]
        public void TinyOrNonFiniteHalfExtents_AreRaisedToMinimum()
        {
            var t = VehicleTuning.FromModel(Model(VehicleKind.Car, 0f, float.NaN, -3f));
            Assert.Equal(VehicleTuning.MinHalfExtent, t.HalfExtents.X, 5);
            Assert.Equal(VehicleTuning.MinHalfExtent, t.HalfExtents.Y, 5);
            Assert.Equal(VehicleTuning.MinHalfExtent, t.HalfExtents.Z, 5);
            Assert.Equal(800f, t.Mass, 0.01);
        }

        [Fact]
        public void Inertia_IsThatOfASolidBox()
        {
            VehicleTuning t = VehicleTuning.FromModel(Model(VehicleKind.Car, 0.9f, 0.75f, 2.2f));
            float wx = 1.8f, wy = 1.5f, wz = 4.4f;
            V.Near(t.Mass / 12f * (wy * wy + wz * wz), t.Inertia.X, t.Inertia.X * 0.01f);
            V.Near(t.Mass / 12f * (wx * wx + wz * wz), t.Inertia.Y, t.Inertia.Y * 0.01f);
            V.Near(t.Mass / 12f * (wx * wx + wy * wy), t.Inertia.Z, t.Inertia.Z * 0.01f);
        }

        [Fact]
        public void WheelLayout_FollowsHalfExtents()
        {
            VehicleTuning t = VehicleTuning.FromModel(Model(VehicleKind.Car, 0.9f, 0.75f, 2.2f));
            V.Near(0.65f * 2.2f, t.AxleHalfSpacing, 1e-3f);
            V.Near(0.85f * 0.9f, t.TrackHalfWidth, 1e-3f);
            Vec3 fl = t.WheelMountLocal(0), fr = t.WheelMountLocal(1), rl = t.WheelMountLocal(2), rr = t.WheelMountLocal(3);
            Assert.True(fl.X < 0f && fl.Z > 0f);
            Assert.True(fr.X > 0f && fr.Z > 0f);
            Assert.True(rl.X < 0f && rl.Z < 0f);
            Assert.True(rr.X > 0f && rr.Z < 0f);
            Assert.Throws<ArgumentOutOfRangeException>(() => t.WheelMountLocal(4));
        }

        [Theory]
        [InlineData(VehicleKind.Car, 0.32f)]
        [InlineData(VehicleKind.Taxi, 0.32f)]
        [InlineData(VehicleKind.Police, 0.32f)]
        [InlineData(VehicleKind.Hearse, 0.32f)]
        [InlineData(VehicleKind.Van, 0.38f)]
        [InlineData(VehicleKind.Ambulance, 0.38f)]
        [InlineData(VehicleKind.Service, 0.38f)]
        [InlineData(VehicleKind.Truck, 0.5f)]
        [InlineData(VehicleKind.Garbage, 0.5f)]
        [InlineData(VehicleKind.FireTruck, 0.5f)]
        [InlineData(VehicleKind.Bus, 0.5f)]
        public void WheelRadius_DependsOnKind(VehicleKind kind, float radius)
        {
            Assert.Equal(radius, VehicleTuning.FromModel(Model(kind, 1f, 1f, 3f)).WheelRadius, 1e-4);
        }

        [Fact]
        public void Suspension_Gives1p6HzAndDampingRatio0p35()
        {
            VehicleTuning t = VehicleTuning.FromModel(Model(VehicleKind.Car, 0.9f, 0.75f, 2.2f));
            float corner = t.Mass / 4f;
            double hz = Math.Sqrt(t.SpringStiffness / corner) / (2.0 * Math.PI);
            double ratio = t.DamperCoefficient / (2.0 * Math.Sqrt(t.SpringStiffness * corner));
            Assert.Equal(1.6, hz, 0.02);
            Assert.Equal(0.35, ratio, 0.01);
            Assert.Equal(0.35f, t.SuspensionRestLength, 1e-4);
            Assert.Equal(0.5f * t.SpringStiffness, t.AntiRollStiffness, t.SpringStiffness * 0.01);
        }

        [Fact]
        public void MountHeight_PutsRestingWheelsOnTheBoxBottom()
        {
            VehicleTuning t = VehicleTuning.FromModel(Model(VehicleKind.Car, 0.9f, 0.75f, 2.2f));
            double w = 2.0 * Math.PI * 1.6;
            double sag = 9.81 / (w * w);
            double expected = -0.75 + t.WheelRadius + 0.35 - sag;
            Assert.Equal(expected, t.MountHeight, 3);
        }

        [Theory]
        [InlineData(VehicleKind.Car, 30f, 42f)]
        [InlineData(VehicleKind.Car, 10f, 25f)]
        [InlineData(VehicleKind.Car, float.NaN, 25f)]
        [InlineData(VehicleKind.Car, -5f, 25f)]
        [InlineData(VehicleKind.Taxi, 40f, 56f)]
        [InlineData(VehicleKind.Van, 20f, 28f)]
        [InlineData(VehicleKind.Bus, 20f, 20f)]
        [InlineData(VehicleKind.Bus, 8f, 20f)]
        [InlineData(VehicleKind.Truck, 22f, 22f)]
        [InlineData(VehicleKind.Garbage, 5f, 20f)]
        public void TopSpeed_FollowsKindRules(VehicleKind kind, float cs1MaxSpeed, float expected)
        {
            Assert.Equal(expected, VehicleTuning.FromModel(Model(kind, 1f, 1f, 3f, cs1MaxSpeed)).TopSpeed, 0.01);
        }

        [Theory]
        [InlineData(VehicleKind.Police, 30f, 48.3f)]
        [InlineData(VehicleKind.Police, 10f, 28.75f)]
        [InlineData(VehicleKind.Ambulance, 30f, 48.3f)]
        [InlineData(VehicleKind.FireTruck, 22f, 25.3f)]
        [InlineData(VehicleKind.FireTruck, 5f, 23f)]
        public void TopSpeed_EmergencyVehiclesGet15PercentMore(VehicleKind kind, float cs1MaxSpeed, float expected)
        {
            Assert.Equal(expected, VehicleTuning.FromModel(Model(kind, 1f, 1f, 3f, cs1MaxSpeed)).TopSpeed, 0.01);
        }

        [Fact]
        public void EngineForce_GivesZeroTo27InAboutSixSecondsForCars_AndSlowerForHeavies()
        {
            VehicleTuning car = VehicleTuning.FromModel(Model(VehicleKind.Car, 0.9f, 0.75f, 2.2f));
            V.Near(1.1f * car.Mass * 27f / 6f, car.EngineForce, car.EngineForce * 0.01f);
            VehicleTuning bus = VehicleTuning.FromModel(Model(VehicleKind.Bus, 1.3f, 1.6f, 6f, 20f));
            V.Near(1.1f * bus.Mass * 20f / 10f, bus.EngineForce, bus.EngineForce * 0.01f);
            V.Near(0.6f * car.EngineForce, car.ReverseForce, car.ReverseForce * 0.01f);
        }

        [Fact]
        public void HandlingConstants_MatchTheContract()
        {
            VehicleTuning t = VehicleTuning.FromModel(Model(VehicleKind.Car, 0.9f, 0.75f, 2.2f));
            Assert.Equal(9f, t.ReverseTopSpeed, 1e-3);
            V.Near(t.Mass * 9f, t.BrakeForce, t.BrakeForce * 0.05f);
            Assert.Equal(35f, t.MaxSteerDeg, 1e-3);
            Assert.Equal(8f, t.MinSteerDeg, 1e-3);
            Assert.Equal(1.6f, t.LateralGrip, 1e-3);
            Assert.Equal(0.35f, t.HandbrakeGripFactor, 1e-3);
            Assert.Equal(0.1f, t.Restitution, 1e-3);
            Assert.Equal(0.4f, t.ImpactFriction, 1e-3);
            Assert.Equal(40f, t.SmokingHealth, 1e-3);
            Assert.Equal(15f, t.OnFireHealth, 1e-3);
            Assert.Equal(6f, t.FireSeconds, 1e-3);
            Assert.Equal(3f, t.FlipRecoverSeconds, 1e-3);
            Assert.Equal(4f, t.DamageSpeedThreshold, 1e-3);
        }

        [Fact]
        public void FromModel_NullModelThrows()
        {
            Assert.Throws<ArgumentNullException>(() => VehicleTuning.FromModel(null));
        }
    }
}
