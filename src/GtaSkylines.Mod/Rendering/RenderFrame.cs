using System;
using System.Collections.Generic;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Mod.Rendering
{
    /// <summary>A citizen-model actor (player or ped). Positions are feet, yaw in degrees (Unity euler y).</summary>
    public struct PedDraw
    {
        /// <summary>Citizen model id (<c>Catalogues.Citizen</c>).</summary>
        public int ModelId;
        /// <summary>Stable actor key for the session (keeps the same CS1 prefab instance across frames).</summary>
        public int Key;
        /// <summary>Feet position at the previous and current fixed step.</summary>
        public Vec3 PrevPosition, Position;
        /// <summary>Facing at the previous and current fixed step.</summary>
        public float PrevYawDeg, YawDeg;
        /// <summary>Ground velocity, m/s.</summary>
        public Vec3 Velocity;
        /// <summary>Animation.</summary>
        public PedAnim Anim;
        /// <summary>False to skip (first-person player, player in a vehicle).</summary>
        public bool Visible;
    }

    /// <summary>Damage visuals of a car.</summary>
    public enum CarDamage
    {
        /// <summary>No effect.</summary>
        Intact,
        /// <summary>Smoke from the engine.</summary>
        Smoking,
        /// <summary>Fire and smoke.</summary>
        Burning,
        /// <summary>Wreck: blackened, fire/smoke column for <see cref="EffectMath.WreckFireLifetime"/>.</summary>
        Exploded,
    }

    /// <summary>One of our cars, drawn with its CS1 model.</summary>
    public struct CarDraw
    {
        /// <summary>Vehicle model id (<c>Catalogues.Vehicle</c>).</summary>
        public int ModelId;
        /// <summary>Body bounds centre at the previous and current fixed step.</summary>
        public Vec3 PrevCentre, Centre;
        /// <summary>Body rotation at the previous and current fixed step.</summary>
        public Quat PrevRotation, Rotation;
        /// <summary>Velocity, m/s.</summary>
        public Vec3 Velocity;
        /// <summary>Signed acceleration along the velocity, m/s².</summary>
        public float Acceleration;
        /// <summary>Body colour, 0xRRGGBB.</summary>
        public int ColourRgb;
        /// <summary>Headlights on.</summary>
        public bool Headlights;
        /// <summary>Brake lights on.</summary>
        public bool Brake;
        /// <summary>Siren and light bar (emergency models).</summary>
        public bool Siren;
        /// <summary>Front wheel steering, radians (positive right).</summary>
        public float SteerRad;
        /// <summary>Distance the wheels have rolled (<see cref="VehicleMath.WheelTravel"/>).</summary>
        public float WheelTravelMetres;
        /// <summary>Suspension compressions, metres: front-left, front-right, rear-left, rear-right.</summary>
        public float CompressionFL, CompressionFR, CompressionRL, CompressionRR;
        /// <summary>Damage state.</summary>
        public CarDamage Damage;
        /// <summary>Seconds since the explosion (when <see cref="CarDamage.Exploded"/>).</summary>
        public float SecondsSinceExplosion;
    }

    /// <summary>The police helicopter, drawn with its CS1 vehicle model plus a spotlight beam.</summary>
    public struct HeliDraw
    {
        /// <summary>True while the helicopter flies; false draws nothing.</summary>
        public bool Visible;
        /// <summary>Vehicle model id (<c>Catalogues.Vehicle</c>).</summary>
        public int ModelId;
        /// <summary>Position at the previous and current fixed step (the model origin).</summary>
        public Vec3 PrevPosition, Position;
        /// <summary>Facing, degrees.</summary>
        public float YawDeg;
        /// <summary>Velocity, m/s (drives the body tilt).</summary>
        public Vec3 Velocity;
        /// <summary>Accumulated rotor travel (<see cref="HeliMath.RotorRate"/> per second).</summary>
        public float RotorTravel;
        /// <summary>Spotlight beam on.</summary>
        public bool Spotlight;
        /// <summary>Ground point the beam ends on.</summary>
        public Vec3 SpotlightTarget;
    }

    /// <summary>The interior to draw (one at a time).</summary>
    public sealed class InteriorDraw
    {
        /// <summary>Layout, or null when outdoors.</summary>
        public InteriorLayout Layout;
        /// <summary>World position of the interior origin (building x, z, y = −1500).</summary>
        public Vec3 Origin;
        /// <summary>Rotation about +y, degrees.</summary>
        public float YawDeg;
    }

    /// <summary>Marker kinds.</summary>
    public enum MarkerKind
    {
        /// <summary>Small arrow at an enterable door.</summary>
        Door,
        /// <summary>Translucent cylinder (mission yellow, side job blue, hostile red: colour from the Game).</summary>
        Cylinder,
        /// <summary>Upright checkpoint ring facing <see cref="MarkerDraw.YawDeg"/>.</summary>
        Ring,
    }

    /// <summary>A world marker.</summary>
    public struct MarkerDraw
    {
        /// <summary>Kind.</summary>
        public MarkerKind Kind;
        /// <summary>Ground position (ring: its centre).</summary>
        public Vec3 Position;
        /// <summary>Radius, metres.</summary>
        public float Radius;
        /// <summary>Facing, degrees (door arrow and ring).</summary>
        public float YawDeg;
        /// <summary>Colour 0xRRGGBB.</summary>
        public int ColourRgb;
        /// <summary>Opacity 0..1.</summary>
        public float Alpha;
    }

    /// <summary>A shot fired this frame (tracer from muzzle to hit).</summary>
    public struct ShotDraw
    {
        /// <summary>Muzzle and hit point.</summary>
        public Vec3 From, To;
    }

    /// <summary>A muzzle flash this frame.</summary>
    public struct FlashDraw
    {
        /// <summary>Muzzle position.</summary>
        public Vec3 Position;
    }

    /// <summary>A bullet impact this frame.</summary>
    public struct ImpactDraw
    {
        /// <summary>Hit point.</summary>
        public Vec3 Position;
    }

    /// <summary>An explosion this frame.</summary>
    public struct ExplosionDraw
    {
        /// <summary>Centre.</summary>
        public Vec3 Position;
    }

    /// <summary>The first-person weapon view model.</summary>
    public struct ViewModelDraw
    {
        /// <summary>Weapon; <see cref="ViewWeapon.Fists"/> draws nothing.</summary>
        public ViewWeapon Weapon;
        /// <summary>Seconds since the last shot or swing (negative: none).</summary>
        public float SinceShotS;
        /// <summary>Reload progress 0..1 (negative: not reloading).</summary>
        public float ReloadProgress;
        /// <summary>True in first person on foot.</summary>
        public bool Visible;
    }

    /// <summary>The camera pose to apply.</summary>
    public struct CameraDraw
    {
        /// <summary>Eye position.</summary>
        public Vec3 Position;
        /// <summary>Rotation.</summary>
        public Quat Rotation;
        /// <summary>Vertical field of view, degrees.</summary>
        public float FovDeg;
        /// <summary>Near clip, metres.</summary>
        public float Near;
    }

    /// <summary>
    /// Everything the renderer draws in one frame, filled by the session each frame. Lists keep their capacity across
    /// <see cref="Clear"/> so a steady state allocates nothing. Effect lists hold events of this frame only; the renderer
    /// keeps them alive for their lifetime.
    /// </summary>
    public sealed class RenderFrame
    {
        /// <summary>Fraction of a fixed step since the last one (interpolation).</summary>
        public float Alpha = 1f;
        /// <summary>Seconds since the previous rendered frame.</summary>
        public float DeltaTime;
        /// <summary>Actors.</summary>
        public readonly List<PedDraw> Peds = new List<PedDraw>(64);
        /// <summary>Our cars.</summary>
        public readonly List<CarDraw> Cars = new List<CarDraw>(32);
        /// <summary>The police helicopter (<see cref="HeliDraw.Visible"/> false when none).</summary>
        public HeliDraw Heli;
        /// <summary>Markers.</summary>
        public readonly List<MarkerDraw> Markers = new List<MarkerDraw>(16);
        /// <summary>Shots fired this frame.</summary>
        public readonly List<ShotDraw> Shots = new List<ShotDraw>(16);
        /// <summary>Muzzle flashes this frame.</summary>
        public readonly List<FlashDraw> MuzzleFlashes = new List<FlashDraw>(16);
        /// <summary>Impacts this frame.</summary>
        public readonly List<ImpactDraw> Impacts = new List<ImpactDraw>(16);
        /// <summary>Explosions this frame.</summary>
        public readonly List<ExplosionDraw> Explosions = new List<ExplosionDraw>(4);
        /// <summary>The active interior (Layout null outdoors).</summary>
        public readonly InteriorDraw Interior = new InteriorDraw();
        /// <summary>View model.</summary>
        public ViewModelDraw ViewModel;
        /// <summary>Camera pose; applied when <see cref="HasCamera"/>.</summary>
        public CameraDraw Camera;
        /// <summary>True when <see cref="Camera"/> is set.</summary>
        public bool HasCamera;

        /// <summary>Empties every list (capacity kept) and drops the camera and interior.</summary>
        public void Clear()
        {
            Peds.Clear();
            Cars.Clear();
            Markers.Clear();
            Shots.Clear();
            MuzzleFlashes.Clear();
            Impacts.Clear();
            Explosions.Clear();
            Interior.Layout = null;
            Heli = default(HeliDraw);
            ViewModel = default(ViewModelDraw);
            HasCamera = false;
        }
    }

    /// <summary>Interpolation between fixed steps.</summary>
    public static class Interp
    {
        /// <summary>Linear, alpha clamped to [0, 1].</summary>
        public static Vec3 Position(Vec3 prev, Vec3 cur, float alpha)
        {
            return Vec3.Lerp(prev, cur, Clamp01(alpha));
        }

        /// <summary>Shortest arc, result in [0, 360).</summary>
        public static float YawDeg(float prevDeg, float curDeg, float alpha)
        {
            float d = Wrap(curDeg - prevDeg);
            if (d > 180f) d -= 360f;
            return Wrap(prevDeg + d * Clamp01(alpha));
        }

        private static float Wrap(float deg)
        {
            float r = deg % 360f;
            if (r < 0f) r += 360f;
            return r >= 360f ? 0f : r;
        }

        private static float Clamp01(float a)
        {
            return a < 0f || float.IsNaN(a) ? 0f : a > 1f ? 1f : a;
        }
    }
}
