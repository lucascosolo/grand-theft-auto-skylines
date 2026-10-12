using System;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Mod.Rendering
{
    /// <summary>The CS1 <c>Vehicle.Flags</c> a car is drawn with (the renderer maps each one to its CS1 flag).</summary>
    [Flags]
    public enum CarDrawFlags
    {
        /// <summary>No flags: the submeshes CS1's parked draw shows.</summary>
        None = 0,
        /// <summary><c>Vehicle.Flags.Created</c>.</summary>
        Created = 1,
        /// <summary><c>Vehicle.Flags.Spawned</c>.</summary>
        Spawned = 2,
        /// <summary><c>Vehicle.Flags.Emergency2</c> (siren and light bar).</summary>
        Emergency2 = 4,
    }

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
        /// The pose passed to <c>Vehicle.RenderInstance</c>: the interpolated body pose with zero sway. The Game body pose
        /// already carries suspension roll and pitch; <c>VehicleAI.CalculateBodyMatrix</c> (VehicleAI.cs:72-79) would rotate
        /// and lower the body a second time by any sway passed.
        /// </summary>
        public static void RenderPose(CarDraw c, float alpha, Vec3 centerOffset, out Vec3 origin, out Quat rotation, out Vec3 sway)
        {
            rotation = Quat.Slerp(c.PrevRotation, c.Rotation, alpha < 0f || float.IsNaN(alpha) ? 0f : alpha > 1f ? 1f : alpha);
            origin = OriginFromBoundsCentre(Interp.Position(c.PrevCentre, c.Centre, alpha), rotation, centerOffset);
            sway = new Vec3(0f, 0f, 0f);
        }

        /// <summary>
        /// Vehicle flags to draw a car with. CS1 shows rider and driver submeshes only when their required flags match
        /// (Vehicle.cs:428); an unoccupied car draws with none, which shows the same submeshes as CS1's parked draw
        /// (VehicleParked.cs:95: only those with no required vehicle flags).
        /// </summary>
        public static CarDrawFlags DrawFlags(bool occupied, bool siren, CarDamage damage)
        {
            if (!occupied) return CarDrawFlags.None;
            CarDrawFlags f = CarDrawFlags.Created | CarDrawFlags.Spawned;
            if (siren && damage != CarDamage.Exploded) f |= CarDrawFlags.Emergency2;
            return f;
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
