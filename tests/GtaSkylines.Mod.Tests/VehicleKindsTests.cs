using GtaSkylines.Game.City;
using GtaSkylines.Mod.City;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class VehicleKindsTests
    {
        [Theory]
        [InlineData("BusAI", VehicleKind.Bus)]
        [InlineData("TaxiAI", VehicleKind.Taxi)]
        [InlineData("PoliceCarAI", VehicleKind.Police)]
        [InlineData("AmbulanceAI", VehicleKind.Ambulance)]
        [InlineData("FireTruckAI", VehicleKind.FireTruck)]
        [InlineData("HearseAI", VehicleKind.Hearse)]
        [InlineData("GarbageTruckAI", VehicleKind.Garbage)]
        [InlineData("MaintenanceTruckAI", VehicleKind.Service)]
        [InlineData("SnowTruckAI", VehicleKind.Service)]
        [InlineData("WaterTruckAI", VehicleKind.Service)]
        [InlineData("ParkMaintenanceVehicleAI", VehicleKind.Service)]
        [InlineData("PostVanAI", VehicleKind.Service)]
        [InlineData("DisasterResponseVehicleAI", VehicleKind.Service)]
        [InlineData("BankVanAI", VehicleKind.Service)]
        [InlineData("PoliceCopterAI", VehicleKind.PoliceHelicopter)]
        public void TryClassify_maps_ai_types(string ai, VehicleKind expected)
        {
            Assert.True(VehicleKinds.TryClassify(ai, 5f, out var kind));
            Assert.Equal(expected, kind);
        }

        [Theory]
        [InlineData(4.0f, VehicleKind.Car)]
        [InlineData(5.19f, VehicleKind.Car)]
        [InlineData(5.2f, VehicleKind.Van)]
        [InlineData(7f, VehicleKind.Van)]
        public void TryClassify_passenger_car_becomes_van_when_long(float len, VehicleKind expected)
        {
            Assert.True(VehicleKinds.TryClassify("PassengerCarAI", len, out var kind));
            Assert.Equal(expected, kind);
        }

        [Theory]
        [InlineData(6.4f, VehicleKind.Van)]
        [InlineData(6.5f, VehicleKind.Truck)]
        [InlineData(12f, VehicleKind.Truck)]
        public void TryClassify_cargo_truck_becomes_van_when_short(float len, VehicleKind expected)
        {
            Assert.True(VehicleKinds.TryClassify("CargoTruckAI", len, out var kind));
            Assert.Equal(expected, kind);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("CarTrailerAI")]
        [InlineData("TrolleybusAI")]
        [InlineData("TramAI")]
        [InlineData("BicycleAI")]
        [InlineData("CarAI")]
        public void TryClassify_unknown_returns_false_with_default_car(string ai)
        {
            Assert.False(VehicleKinds.TryClassify(ai, 5f, out var kind));
            Assert.Equal(VehicleKind.Car, kind);
        }

        [Theory]
        [InlineData(VehicleKind.Police, true)]
        [InlineData(VehicleKind.Ambulance, true)]
        [InlineData(VehicleKind.FireTruck, true)]
        [InlineData(VehicleKind.PoliceHelicopter, true)]
        [InlineData(VehicleKind.Car, false)]
        [InlineData(VehicleKind.Van, false)]
        [InlineData(VehicleKind.Truck, false)]
        [InlineData(VehicleKind.Bus, false)]
        [InlineData(VehicleKind.Taxi, false)]
        [InlineData(VehicleKind.Hearse, false)]
        [InlineData(VehicleKind.Garbage, false)]
        [InlineData(VehicleKind.Service, false)]
        public void HasSiren_only_emergency_kinds(VehicleKind kind, bool expected)
        {
            Assert.Equal(expected, VehicleKinds.HasSiren(kind));
        }
    }
}
