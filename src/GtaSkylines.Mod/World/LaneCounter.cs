using GtaSkylines.Game.Roads;

namespace GtaSkylines.Mod.World
{
    /// <summary>One CS1 lane as the road builder reads it.</summary>
    public struct LaneSpec
    {
        /// <summary>A car lane (<c>LaneType.Vehicle | TransportVehicle</c> with <c>VehicleType.Car</c>).</summary>
        public bool Car;
        /// <summary><c>NetInfo.Lane.m_finalDirection</c> bits (Forward 1, Backward 2); traffic side already applied.</summary>
        public byte Direction;
        /// <summary>Lane width, metres.</summary>
        public float Width;
        /// <summary>CS1 speed limit (1.0 = 50 km/h).</summary>
        public float SpeedLimit;
    }

    /// <summary>Car lanes of a segment, start-to-end (Forward) and end-to-start (Backward).</summary>
    public struct LaneSummary
    {
        /// <summary>Lanes from the start node to the end node.</summary>
        public int Forward;
        /// <summary>Lanes from the end node to the start node.</summary>
        public int Backward;
        /// <summary>Mean width of the counted lanes, 0 when none.</summary>
        public float LaneWidth;
        /// <summary>Highest CS1 speed limit of the counted lanes, 0 when none.</summary>
        public float SpeedLimit;
    }

    /// <summary>Counts lanes the way <c>NetInfo.InitializePrefab</c> does (NetInfo.cs:1108-1120), per segment. Pure.</summary>
    public static class LaneCounter
    {
        /// <summary>Counts the first <paramref name="count"/> lanes; <c>NetSegment.Flags.Invert</c> swaps the directions.</summary>
        public static LaneSummary Count(LaneSpec[] lanes, int count, bool segmentInverted)
        {
            var s = new LaneSummary();
            if (lanes == null || count <= 0) return s;
            int n = count < lanes.Length ? count : lanes.Length, counted = 0;
            float width = 0f;
            for (int i = 0; i < n; i++)
            {
                if (!lanes[i].Car) continue;
                bool fwd = (lanes[i].Direction & 1) != 0, back = (lanes[i].Direction & 2) != 0;
                if (!fwd && !back) continue;
                if (fwd) s.Forward++;
                if (back) s.Backward++;
                width += lanes[i].Width;
                counted++;
                if (lanes[i].SpeedLimit > s.SpeedLimit) s.SpeedLimit = lanes[i].SpeedLimit;
            }
            if (counted > 0) s.LaneWidth = width / counted;
            if (segmentInverted)
            {
                int t = s.Forward;
                s.Forward = s.Backward;
                s.Backward = t;
            }
            return s;
        }

        /// <summary>Road flags of a segment.</summary>
        public static RoadFlags Flags(bool highway, int forward, int backward, bool bridge, bool tunnel, bool pedestrianOnly)
        {
            RoadFlags f = RoadFlags.None;
            if (highway) f |= RoadFlags.Highway;
            if ((forward > 0) != (backward > 0)) f |= RoadFlags.OneWay;
            if (bridge) f |= RoadFlags.Bridge;
            if (tunnel) f |= RoadFlags.Tunnel;
            if (pedestrianOnly) f |= RoadFlags.PedestrianOnly;
            return f;
        }
    }
}
