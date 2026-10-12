using GtaSkylines.Game.City;

namespace GtaSkylines.Mod.City
{
    /// <summary>Road vehicle kind from a CS1 vehicle AI type name (callers walk the AI's base types). Pure.</summary>
    public static class VehicleKinds
    {
        /// <summary>Kind for a drivable AI type; false for trailers, rail, bicycles and anything unknown.</summary>
        public static bool TryClassify(string aiTypeName, float lengthM, out VehicleKind kind)
        {
            kind = VehicleKind.Car;
            switch (aiTypeName)
            {
                case "PassengerCarAI": kind = lengthM >= 5.2f ? VehicleKind.Van : VehicleKind.Car; return true;
                case "CargoTruckAI": kind = lengthM < 6.5f ? VehicleKind.Van : VehicleKind.Truck; return true;
                case "BusAI": kind = VehicleKind.Bus; return true;
                case "TaxiAI": kind = VehicleKind.Taxi; return true;
                case "PoliceCarAI": kind = VehicleKind.Police; return true;
                case "AmbulanceAI": kind = VehicleKind.Ambulance; return true;
                case "FireTruckAI": kind = VehicleKind.FireTruck; return true;
                case "HearseAI": kind = VehicleKind.Hearse; return true;
                case "GarbageTruckAI": kind = VehicleKind.Garbage; return true;
                case "MaintenanceTruckAI":
                case "SnowTruckAI":
                case "WaterTruckAI":
                case "ParkMaintenanceVehicleAI":
                case "PostVanAI":
                case "DisasterResponseVehicleAI":
                case "BankVanAI": kind = VehicleKind.Service; return true;
                case "PoliceCopterAI": kind = VehicleKind.PoliceHelicopter; return true;
                default: return false;
            }
        }

        /// <summary>
        /// True for a scooter or motorbike: exactly two generated tyres on one line (spread across x below
        /// <see cref="TwoWheelerMaxTyreGauge"/>), or no usable tyre data (0 or 1 tyre) and a body narrower than
        /// <see cref="TwoWheelerMaxWidth"/>. Three or more tyres is never a two-wheeler.
        /// </summary>
        public static bool IsTwoWheeler(int tyreCount, float tyreGaugeM, float widthM)
        {
            if (tyreCount == 2) return tyreGaugeM < TwoWheelerMaxTyreGauge;
            return tyreCount <= 1 && widthM < TwoWheelerMaxWidth;
        }

        /// <summary>Largest x spread of two tyres that still counts as one track, m.</summary>
        public const float TwoWheelerMaxTyreGauge = 0.3f;

        /// <summary>Width below which a model without tyre data counts as a two-wheeler, m.</summary>
        public const float TwoWheelerMaxWidth = 1.0f;

        /// <summary>True for emergency kinds (siren and light bar).</summary>
        public static bool HasSiren(VehicleKind kind)
        {
            return kind == VehicleKind.Police || kind == VehicleKind.Ambulance || kind == VehicleKind.FireTruck
                || kind == VehicleKind.PoliceHelicopter;
        }
    }
}
