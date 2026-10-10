using GtaSkylines.Game.City;

namespace GtaSkylines.Mod.City
{
    /// <summary>CS1 <c>ItemClass</c> (service, sub-service, level) to the Game's building use. Pure.</summary>
    public static class ServiceMap
    {
        /// <summary>Use of a building from its <c>ItemClass.Service</c> name and building AI type name.</summary>
        public static ServiceKind Map(string service, string subService, string buildingAi)
        {
            if (buildingAi == "CemeteryAI") return ServiceKind.Deathcare;
            switch (service)
            {
                case "Residential": return ServiceKind.Residential;
                case "Commercial":
                case "Tourism": return ServiceKind.Commercial;
                case "Office": return ServiceKind.Office;
                case "Industrial":
                case "PlayerIndustry":
                case "Fishing": return ServiceKind.Industrial;
                case "PoliceDepartment": return ServiceKind.Police;
                case "HealthCare": return ServiceKind.Health;
                case "FireDepartment": return ServiceKind.Fire;
                case "Education":
                case "PlayerEducation": return ServiceKind.Education;
                case "Garbage": return ServiceKind.Garbage;
                case "Electricity": return ServiceKind.Electricity;
                case "Water": return ServiceKind.Water;
                case "PublicTransport": return ServiceKind.Transport;
                case "Beautification":
                case "VarsitySports": return ServiceKind.Park;
                case "Monument":
                case "Museums": return ServiceKind.Monument;
                case "Disaster": return ServiceKind.Government;
                default: return ServiceKind.Other;
            }
        }

        /// <summary>Sub-service name, empty for none.</summary>
        public static string SubServiceName(string subService)
        {
            return subService == null || subService == "None" ? "" : subService;
        }

        /// <summary>CS1 <c>ItemClass.Level</c> value (Level1 = 0) as 1-5; 1 when out of range.</summary>
        public static int Level(int cs1Level)
        {
            return cs1Level >= 0 && cs1Level <= 4 ? cs1Level + 1 : 1;
        }
    }
}
