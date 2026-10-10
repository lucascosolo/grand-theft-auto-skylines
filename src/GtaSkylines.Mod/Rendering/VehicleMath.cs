using System;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Mod.Rendering
{
    /// <summary>Inputs of the static <c>Vehicle.RenderInstance</c> (Vehicle.cs:363) from a Game car body.</summary>
    public static class VehicleMath
    {
        /// <summary>CS1's vehicle origin from the body's bounds centre (<c>VehicleModel.CenterOffset</c> is the mesh bounds centre).</summary>
        public static Vec3 OriginFromBoundsCentre(Vec3 centre, Quat rotation, Vec3 centerOffset)
        {
            return centre - rotation * centerOffset;
        }

        /// <summary>Inverse of <see cref="OriginFromBoundsCentre"/>.</summary>
        public static Vec3 BoundsCentreFromOrigin(Vec3 origin, Quat rotation, Vec3 centerOffset)
        {
            return origin + rotation * centerOffset;
        }

        /// <summary>
        /// CS1 <c>swayPosition</c> from suspension compressions: <c>VehicleAI.CalculateBodyMatrix</c> (VehicleAI.cs:72-79)
        /// rotates the body by Euler(sway.z rad, 0, −sway.x rad) and lifts it by sway.y.
        /// </summary>
        public static Vec3 SwayFromWheels(float fl, float fr, float rl, float rr, float track, float wheelBase)
        {
            track = Math.Max(track, 0.01f);
            wheelBase = Math.Max(wheelBase, 0.01f);
            double roll = Math.Atan(((fl + rl) * 0.5 - (fr + rr) * 0.5) / track);
            double pitch = Math.Atan(((fl + fr) * 0.5 - (rl + rr) * 0.5) / wheelBase);
            return new Vec3((float)-roll, -(fl + fr + rl + rr) * 0.25f, (float)pitch);
        }

        /// <summary>
        /// The light state vector as CarAI fills <c>Frame.m_lightIntensity</c> (CarAI.cs:587-590): x headlights, y tail/brake
        /// (0.5 running, 5 braking), z/w left/right indicator.
        /// </summary>
        public static void LightState(bool headlights, bool brake, int blinker, bool blinkPhaseOn, out float x, out float y, out float z, out float w)
        {
            x = headlights ? 5f : 0f;
            y = brake ? 5f : (headlights ? 0.5f : 0f);
            z = blinker < 0 && blinkPhaseOn ? 5f : 0f;
            w = blinker > 0 && blinkPhaseOn ? 5f : 0f;
        }

        /// <summary>A wreck's colour: every channel at 15 %.</summary>
        public static int ExplodedColour(int rgb)
        {
            int r = ((rgb >> 16) & 0xFF) * 15 / 100;
            int g = ((rgb >> 8) & 0xFF) * 15 / 100;
            int b = (rgb & 0xFF) * 15 / 100;
            return (r << 16) | (g << 8) | b;
        }

        /// <summary>Rolled distance (CS1's <c>tyrePosition.y</c>, Vehicle.cs:339), kept within [0, 1000).</summary>
        public static float WheelTravel(float previousMetres, float speedMs, float dt)
        {
            double t = previousMetres + (double)speedMs * dt;
            t -= Math.Floor(t / 1000.0) * 1000.0;
            return t >= 1000.0 ? 0f : (float)t;
        }
    }
}
