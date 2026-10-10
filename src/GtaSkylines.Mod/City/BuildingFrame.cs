using System;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Mod.City
{
    /// <summary>
    /// CS1's building frame (<c>Building.CalculatePosition</c>, Building.cs:676-683; sidewalk at Building.cs:657-661):
    /// local +x is (cos a, 0, sin a), local +z is (sin a, 0, -cos a), and the front (the sidewalk side) is
    /// (-sin a, 0, cos a). Prop positions (doors) use +z = front and are mirrored into the frame as
    /// <c>meshOffset - z</c> (BuildingAI.cs:1735, :1786-1787). Pure.
    /// </summary>
    public static class BuildingFrame
    {
        /// <summary>CS1's <c>Building.CalculatePosition(position, angle, offset)</c>.</summary>
        public static Vec3 ToWorld(Vec3 origin, float angleRad, Vec3 local)
        {
            float c = (float)Math.Cos(angleRad), s = (float)Math.Sin(angleRad);
            return new Vec3(origin.X + c * local.X + s * local.Z, origin.Y + local.Y, origin.Z + s * local.X - c * local.Z);
        }

        /// <summary>Yaw (degrees, forward = (sin yaw, 0, cos yaw)) of the building's front.</summary>
        public static float YawDeg(float angleRad)
        {
            return CityMath.NormalizeYawDeg((float)(-angleRad * 180.0 / Math.PI));
        }

        /// <summary><c>Building.CalculateLocalMeshOffset</c> (Building.cs:596-604).</summary>
        public static float LocalMeshOffset(int lengthCells, int infoCellLength, bool expandFrontYard)
        {
            float d = (lengthCells - infoCellLength) * 4f;
            return expandFrontYard ? d : -d;
        }

        /// <summary>World position of a door prop (<c>BuildingInfo.Prop.m_position</c>).</summary>
        public static Vec3 DoorWorld(Vec3 origin, float angleRad, float meshOffsetZ, Vec3 propPosition)
        {
            return ToWorld(origin, angleRad, new Vec3(propPosition.X, propPosition.Y, meshOffsetZ - propPosition.Z));
        }

        /// <summary>Centre of the lot's front edge, at the origin's height.</summary>
        public static Vec3 FrontCenter(Vec3 origin, float angleRad, int lengthCells)
        {
            float d = lengthCells * 4f;
            return new Vec3(origin.X - (float)Math.Sin(angleRad) * d, origin.Y, origin.Z + (float)Math.Cos(angleRad) * d);
        }
    }
}
