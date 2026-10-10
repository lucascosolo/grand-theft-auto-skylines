using System;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Camera
{
    /// <summary>On-foot views, cycled by CameraView in this order (wrapping). Values match <c>GameSettings.FootView</c>.</summary>
    public enum FootView
    {
        /// <summary>Eyes at 1.62 m, gentle bob.</summary>
        FirstPerson = 0,
        /// <summary>Orbit at 2.2 m.</summary>
        ThirdNear = 1,
        /// <summary>Orbit at 3.5 m.</summary>
        ThirdMid = 2,
        /// <summary>Orbit at 5 m.</summary>
        ThirdFar = 3,
    }

    /// <summary>In-vehicle views, cycled by CameraView in this order (wrapping). Values match <c>GameSettings.VehicleView</c>.</summary>
    public enum VehicleView
    {
        /// <summary>Driver's eyes.</summary>
        FirstPerson = 0,
        /// <summary>On the bonnet.</summary>
        Hood = 1,
        /// <summary>Chase camera, near.</summary>
        ChaseNear = 2,
        /// <summary>Chase camera, far.</summary>
        ChaseFar = 3,
    }

    /// <summary>What the rig is doing this step (derived from state, not chosen directly).</summary>
    public enum CameraMode
    {
        /// <summary>On foot, first person.</summary>
        FootFirstPerson,
        /// <summary>On foot, third-person orbit.</summary>
        FootOrbit,
        /// <summary>On foot, aiming: over the shoulder (any foot view except first person, which stays first person).</summary>
        Aim,
        /// <summary>In a vehicle, driver's eyes.</summary>
        VehicleFirstPerson,
        /// <summary>In a vehicle, hood.</summary>
        Hood,
        /// <summary>In a vehicle, chase.</summary>
        Chase,
        /// <summary>Wasted: slow orbit around the body.</summary>
        Death,
    }

    /// <summary>
    /// The game camera (G6 system, order 95, runs while paused). Each step it computes a target pose from the player's
    /// state and PlayerState.ViewYawDeg/ViewPitchDeg, then <see cref="Present"/> interpolates between the last two step
    /// poses for rendering. World space, metres, degrees; rotation <c>Quat.Euler(−pitch, yaw, 0)</c> (positive pitch looks
    /// up).
    /// <list type="bullet">
    /// <item>Cycling: CameraView pressed advances <see cref="FootView"/> on foot or <see cref="VehicleView"/> in a car
    /// ((v + 1) mod 4); each is kept separately; initial values from settings.</item>
    /// <item>Pitch limits: on foot and first-person in vehicles −80..+80; hood and chase −30..+45. The rig clamps
    /// PlayerState.ViewPitchDeg to the current mode's limits.</item>
    /// <item>Foot first person: position = feet + (0, 1.62, 0) + bob (vertical 0.04 m × sin(phase), phase advancing
    /// 2π × 1.8 Hz × (horizontal speed ÷ 3.6) per second; 0 when still or airborne); rotation from view yaw and pitch.</item>
    /// <item>Foot orbit: pivot = feet + (0, 1.6, 0); desired = pivot − forward(view) × distance (2.2 / 3.5 / 5).</item>
    /// <item>Aim: pivot = feet + (0, 1.6, 0) + right(view yaw) × 0.5; desired = pivot − forward × 1.2; FOV 50.</item>
    /// <item>Vehicle first person: seat = car rotation × (−0.35, 0.55 × 2 × halfY − halfY, 0.1 × 2 × halfZ) + bounds
    /// centre (0.35 m left of centre, at 0.55 of the height from the bottom, 0.1 of the length forward); view yaw and pitch
    /// relative to the car's heading.</item>
    /// <item>Hood: car rotation × (0, halfY + 0.15, 0.25 × 2 × halfZ) + centre, looking along the car forward + view
    /// offsets.</item>
    /// <item>Chase: pivot = centre + (0, halfY + 0.4, 0); distance near = 2 × 2 × halfZ + 1, far = 3 × 2 × halfZ + 2;
    /// orbit yaw from the view; after 1.5 s without look input the view yaw eases back toward the heading of the
    /// velocity (or the car forward below 2 m/s) with a 0.35 s half-life, and the pitch toward 8° down.</item>
    /// <item>LookBehind held (car or foot orbit): the orbit yaw is turned 180°; first person and hood look backward.</item>
    /// <item>Spring arm (orbit, aim, chase): sphere cast radius 0.2 from the pivot toward the desired position against
    /// the collision world, mask: ignore the player's tag (in a car: the car's tag), exclude NoCamera surfaces (Interior
    /// is not excluded); the arm length is min(desired, hit distance); it shortens immediately and lengthens with a
    /// 0.3 s half-life. The camera position is never inside geometry.</item>
    /// <item>Death: orbit at 4 m around the body's feet + (0, 1.5, 0), 12°/s, pitch −25°, spring arm applied.</item>
    /// <item>FOV: settings FOV; +8° while sprinting (eased, 0.25 s half-life); in a car + 8 × clamp01(speed ÷ 30); Aim 50.</item>
    /// <item>Near clip: 0.05 in first person (foot and vehicle), 0.1 otherwise.</item>
    /// </list>
    /// After each step the rig writes <c>world.ViewPosition/ViewRotation/ViewFovDeg</c>.
    /// </summary>
    public sealed class CameraRig : IGameSystem
    {
        /// <summary>First-person eye height above the feet.</summary>
        public const float EyeHeight = 1.62f;
        /// <summary>Third-person pivot height above the feet.</summary>
        public const float PivotHeight = 1.6f;
        /// <summary>Third near distance.</summary>
        public const float ThirdNearDistance = 2.2f;
        /// <summary>Third mid distance.</summary>
        public const float ThirdMidDistance = 3.5f;
        /// <summary>Third far distance.</summary>
        public const float ThirdFarDistance = 5f;
        /// <summary>Aim arm length.</summary>
        public const float AimDistance = 1.2f;
        /// <summary>Aim shoulder offset to the right.</summary>
        public const float AimRightOffset = 0.5f;
        /// <summary>Aim FOV, degrees.</summary>
        public const float AimFovDeg = 50f;
        /// <summary>Foot and first-person pitch limit, degrees (±).</summary>
        public const float FootPitchLimit = 80f;
        /// <summary>Chase/hood lowest pitch.</summary>
        public const float ChasePitchMin = -30f;
        /// <summary>Chase/hood highest pitch.</summary>
        public const float ChasePitchMax = 45f;
        /// <summary>Seconds without look input before chase recentring.</summary>
        public const float RecentreDelay = 1.5f;
        /// <summary>Spring-arm sphere radius.</summary>
        public const float ArmRadius = 0.2f;
        /// <summary>Spring-arm ease-out half-life, seconds.</summary>
        public const float ArmEaseOutHalfLife = 0.3f;
        /// <summary>FOV added while sprinting / at 30 m/s in a car, degrees.</summary>
        public const float SpeedFovBonus = 8f;
        /// <summary>Seat offset to the left of centre.</summary>
        public const float SeatLeft = 0.35f;
        /// <summary>Seat height as a fraction of the model height.</summary>
        public const float SeatHeightFraction = 0.55f;
        /// <summary>Seat forward as a fraction of the model length.</summary>
        public const float SeatForwardFraction = 0.1f;
        /// <summary>Death orbit radius.</summary>
        public const float DeathOrbitDistance = 4f;
        /// <summary>Death orbit rate, degrees per second.</summary>
        public const float DeathOrbitDegPerSecond = 12f;

        /// <summary>A rig with settings' default views (the session creates it).</summary>
        public CameraRig(int footView, int vehicleView)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public int Order { get { return SystemOrder.Camera; } }

        /// <summary>Current on-foot view.</summary>
        public FootView FootView { get; set; }
        /// <summary>Current vehicle view.</summary>
        public VehicleView VehicleView { get; set; }
        /// <summary>Mode of the last step.</summary>
        public CameraMode Mode { get; private set; }
        /// <summary>Current spring-arm length (0 in first person and hood).</summary>
        public float ArmLength { get; private set; }

        /// <summary>Interpolated position after <see cref="Present"/>.</summary>
        public Vec3 Position { get; private set; }
        /// <summary>Interpolated rotation (Slerp) after <see cref="Present"/>.</summary>
        public Quat Rotation { get; private set; }
        /// <summary>Interpolated vertical field of view, degrees.</summary>
        public float FovDeg { get; private set; }
        /// <summary>Near clip plane, metres (of the latest step).</summary>
        public float NearClip { get; private set; }

        /// <summary>Computes this step's pose (see the class).</summary>
        public void Step(GameWorld world, float dt)
        {
            throw new NotImplementedException();
        }

        /// <summary>Sets the outputs to the blend of the previous and latest step poses at <paramref name="alpha"/> (0..1, clamped).</summary>
        public void Present(float alpha)
        {
            throw new NotImplementedException();
        }

        /// <summary>Distance of the third-person view (2.2, 3.5, 5; 0 for first person).</summary>
        public static float OrbitDistance(FootView view)
        {
            switch (view)
            {
                case FootView.ThirdNear: return ThirdNearDistance;
                case FootView.ThirdMid: return ThirdMidDistance;
                case FootView.ThirdFar: return ThirdFarDistance;
                default: return 0f;
            }
        }

        /// <summary>Chase distance for a model length (2 × halfZ): near 2 × length + 1, far 3 × length + 2.</summary>
        public static float ChaseDistance(VehicleView view, float modelLength)
        {
            return view == VehicleView.ChaseFar ? 3f * modelLength + 2f : 2f * modelLength + 1f;
        }

        /// <summary>The driver's eye in the car's local frame (origin at the bounds centre) from the model's half extents.</summary>
        public static Vec3 SeatLocal(Vec3 halfExtents)
        {
            return new Vec3(-SeatLeft, SeatHeightFraction * 2f * halfExtents.Y - halfExtents.Y, SeatForwardFraction * 2f * halfExtents.Z);
        }
    }
}
