using System;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.Vehicles
{
    /// <summary>Size class that picks density, wheel size, speed and acceleration rules in <see cref="VehicleTuning.FromModel"/>.</summary>
    public enum VehicleClass
    {
        /// <summary>Car, Taxi, Police, Hearse, Van, Ambulance, Service, PoliceHelicopter.</summary>
        Car = 0,

        /// <summary>Truck, Garbage, FireTruck.</summary>
        Truck = 1,

        /// <summary>Bus.</summary>
        Bus = 2,
    }

    /// <summary>
    /// Physical and handling parameters of one <see cref="VehicleBody"/> (PLAN G5). Built by <see cref="FromModel"/>; the
    /// fields are public so tests and the session can adjust them. The body reads them every step, so a change applies
    /// from the next step. SI units throughout (kg, m, s, N); angles in degrees.
    /// <para>
    /// Body frame: the model's bounding-box centre is the origin, +x right, +y up, +z forward (CS1/Unity, left-handed).
    /// Wheels are indexed 0 front-left (−x, +z), 1 front-right (+x, +z), 2 rear-left (−x, −z), 3 rear-right (+x, −z). Front
    /// wheels steer, rear wheels drive, all four brake, the handbrake acts on the rear pair.
    /// </para>
    /// </summary>
    public sealed class VehicleTuning
    {
        /// <summary>Density of the bounding box for <see cref="VehicleClass.Car"/>, kg/m³ (1.8 × 1.5 × 4.4 m → ≈ 1400 kg).</summary>
        public const float CarDensity = 118f;

        /// <summary>Density for <see cref="VehicleClass.Truck"/>, kg/m³ (2.5 × 3.2 × 9 m → 9000 kg).</summary>
        public const float TruckDensity = 125f;

        /// <summary>Density for <see cref="VehicleClass.Bus"/>, kg/m³ (2.6 × 3.2 × 12 m → ≈ 11000 kg).</summary>
        public const float BusDensity = 110f;

        /// <summary>Smallest mass <see cref="FromModel"/> produces, kg.</summary>
        public const float MinMass = 800f;

        /// <summary>Largest mass <see cref="FromModel"/> produces, kg.</summary>
        public const float MaxMass = 15000f;

        /// <summary>Smallest half extent <see cref="FromModel"/> accepts, m; smaller or non-finite model values are raised to it.</summary>
        public const float MinHalfExtent = 0.2f;

        /// <summary>Suspension natural frequency per corner, Hz.</summary>
        public const float SuspensionFrequencyHz = 1.6f;

        /// <summary>Suspension damping ratio.</summary>
        public const float SuspensionDampingRatio = 0.35f;

        private VehicleTuning()
        {
        }

        /// <summary>The model's kind.</summary>
        public VehicleKind Kind;

        /// <summary>Size class derived from <see cref="Kind"/>.</summary>
        public VehicleClass Class;

        /// <summary>
        /// Half size of the model's bounding box (x width, y height, z length), m, each ≥ <see cref="MinHalfExtent"/>.
        /// <see cref="VehicleBody.Position"/> is the centre of this box.
        /// </summary>
        public Vec3 HalfExtents;

        /// <summary>
        /// Height of the bottom slice of the bounding box (wheels, sills) that body collision ignores, m. The collision box
        /// spans local y from <c>−HalfExtents.Y + BodyClearance</c> to <c>+HalfExtents.Y</c> and the full x and z extents.
        /// Default 0.2, never more than <c>HalfExtents.Y</c>.
        /// </summary>
        public float BodyClearance;

        /// <summary>
        /// Mass, kg: <c>clamp(8 · hx · hy · hz · density(Class), MinMass, MaxMass)</c> with the clamped half extents.
        /// </summary>
        public float Mass;

        /// <summary>
        /// Principal moments of inertia about the local x, y, z axes through the centre of mass, kg·m²: those of a solid
        /// box of <see cref="Mass"/> and the full bounding-box size (<c>Ix = m/12 · ((2hy)² + (2hz)²)</c> and so on).
        /// </summary>
        public Vec3 Inertia;

        /// <summary>Centre of mass in the body frame, m: <c>(0, −0.3 · hy, 0)</c>, lowered for stability over kerbs.</summary>
        public Vec3 CenterOfMassOffset;

        /// <summary>Half the distance between the axles, m: <c>0.65 · hz</c>. Axles sit at local z = ±this.</summary>
        public float AxleHalfSpacing;

        /// <summary>Half the track width, m: <c>0.85 · hx</c>. Wheels sit at local x = ±this.</summary>
        public float TrackHalfWidth;

        /// <summary>Wheel radius, m: 0.32 for Car, Taxi, Police, Hearse, PoliceHelicopter; 0.38 for Van, Ambulance, Service; 0.5 for trucks and buses.</summary>
        public float WheelRadius;

        /// <summary>Suspension travel from fully extended to fully compressed, m. 0.35.</summary>
        public float SuspensionRestLength;

        /// <summary>
        /// Local y of each wheel's suspension mount (top of travel), m:
        /// <c>−hy + WheelRadius + SuspensionRestLength − g / ω²</c> with <c>ω = 2π · SuspensionFrequencyHz</c>, so that at
        /// rest on flat ground (static sag g/ω² ≈ 0.097 m) the wheels touch the bounding box's bottom face and
        /// <see cref="VehicleBody.Position"/>.y ≈ ground + hy.
        /// </summary>
        public float MountHeight;

        /// <summary>Spring stiffness per wheel, N/m: <c>(Mass / 4) · ω²</c>.</summary>
        public float SpringStiffness;

        /// <summary>Damper coefficient per wheel, N·s/m: <c>2 · SuspensionDampingRatio · (Mass / 4) · ω</c>.</summary>
        public float DamperCoefficient;

        /// <summary>Anti-roll bar stiffness per axle, N/m of left-right compression difference: <c>0.5 · SpringStiffness</c>.</summary>
        public float AntiRollStiffness;

        /// <summary>
        /// Forward top speed, m/s. Car class: <c>max(1.4 · model.MaxSpeed, 25)</c>; truck and bus class:
        /// <c>max(model.MaxSpeed, 20)</c>; then × 1.15 for Police, Ambulance and FireTruck. A non-finite or negative
        /// model.MaxSpeed counts as 0. Drive force fades linearly to zero between 0.85 × and 1 × this speed, so a car on
        /// flat ground never exceeds it by more than 2 %.
        /// </summary>
        public float TopSpeed;

        /// <summary>Reverse top speed, m/s. 9.</summary>
        public float ReverseTopSpeed;

        /// <summary>
        /// Total drive force at full throttle below 0.85 · <see cref="TopSpeed"/>, N, split over the rear wheels and limited by
        /// their grip: <c>1.1 · Mass · 27 / 6</c> for the car class (0-27 m/s in ≈ 6 s plus 10 % for drag and rolling
        /// resistance), <c>1.1 · Mass · 20 / 10</c> for trucks and buses.
        /// </summary>
        public float EngineForce;

        /// <summary>Total drive force in reverse at full brake input, N: <c>0.6 · EngineForce</c>.</summary>
        public float ReverseForce;

        /// <summary>Total braking force at full brake, N: <c>Mass · 9</c> (≈ 9 m/s²), 60 % front, 40 % rear, limited by grip.</summary>
        public float BrakeForce;

        /// <summary>|Speed| below which S / W choose reverse / forward drive instead of braking, m/s. 1.</summary>
        public float ReverseEngageSpeed;

        /// <summary>Front wheel lock at standstill, degrees. 35.</summary>
        public float MaxSteerDeg;

        /// <summary>Front wheel lock at <see cref="TopSpeed"/>, degrees; linear in |Speed| / TopSpeed between the two. 8.</summary>
        public float MinSteerDeg;

        /// <summary>Largest change of the smoothed steering value per second, in input units (−1..1), both away from and back to centre. 4.</summary>
        public float SteerRate;

        /// <summary>Lateral tyre friction coefficient (peak force / normal load). 1.6.</summary>
        public float LateralGrip;

        /// <summary>Longitudinal tyre friction coefficient for drive, brake and locked wheels. 1.6.</summary>
        public float LongitudinalGrip;

        /// <summary>
        /// Slip angle at which lateral force peaks, degrees. 8. Below it the force rises as <c>x(2 − x)</c> of the peak
        /// (x = slip / peak); beyond it the force falls linearly to 0.7 of the peak at 3 × this angle (sliding grip), which
        /// keeps a drift controllable.
        /// </summary>
        public float PeakSlipAngleDeg;

        /// <summary>Rear lateral grip multiplier while the handbrake is held. 0.35.</summary>
        public float HandbrakeGripFactor;

        /// <summary>Rolling resistance plus engine braking, as force / normal load on rolling wheels in contact. 0.04 (≈ 0.4 m/s² of coast-down).</summary>
        public float RollingResistance;

        /// <summary>Air drag, N per (m/s)²: <c>0.5 · 1.225 · 0.35 · (2hx · 2hy)</c>.</summary>
        public float DragCoefficient;

        /// <summary>Downforce while any wheel touches, N per (m/s)² of forward speed: <c>0.0015 · Mass</c>.</summary>
        public float DownforceCoefficient;

        /// <summary>Gravity, m/s² downward. 9.81.</summary>
        public float Gravity;

        /// <summary>Angular velocity decay while no wheel touches, fraction per second. 0.5.</summary>
        public float AirAngularDamping;

        /// <summary>Yaw acceleration from full steer while no wheel touches, rad/s². 2.</summary>
        public float AirYawControl;

        /// <summary>Pitch acceleration from full throttle (nose down) or brake (nose up) while no wheel touches, rad/s². 1.5.</summary>
        public float AirPitchControl;

        /// <summary>Restitution of body impacts. 0.1.</summary>
        public float Restitution;

        /// <summary>
        /// Coulomb friction of body impacts against the world. 0.4. It slows the body's linear motion only and adds no spin,
        /// so a glancing hit turns the car along the wall (from the normal impulse) and it scrapes on instead of sliding
        /// sideways to a stop.
        /// </summary>
        public float ImpactFriction;

        /// <summary>Mass a pedestrian capsule counts as for impulses, damage and the impact cap, kg. 80.</summary>
        public float PedestrianMass;

        /// <summary>Relative normal speed below which an impact costs no health, m/s. 4.</summary>
        public float DamageSpeedThreshold;

        /// <summary>Health lost per (m/s)² of (speed − threshold) at mass ratio 1. 0.15 (a 20 m/s wall hit costs ≈ 38).</summary>
        public float DamageScale;

        /// <summary>Relative normal speed below which a contact is not reported as a <see cref="VehicleImpact"/>, m/s. 1.</summary>
        public float ImpactReportSpeed;

        /// <summary>Health below which the state is Smoking. 40.</summary>
        public float SmokingHealth;

        /// <summary>Health at or below which the car catches fire. 15.</summary>
        public float OnFireHealth;

        /// <summary>Seconds on fire before the car explodes. 6.</summary>
        public float FireSeconds;

        /// <summary>Seconds upside down and nearly stopped before <see cref="VehicleBody.CanRecover"/> turns true. 3.</summary>
        public float FlipRecoverSeconds;

        /// <summary>
        /// Builds the tuning for a CS1 vehicle model with the formulas documented on each field.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
        public static VehicleTuning FromModel(VehicleModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException("model");
            }

            VehicleClass cls = ClassOf(model.Kind);
            float hx = SafeHalf(model.HalfExtents.X);
            float hy = SafeHalf(model.HalfExtents.Y);
            float hz = SafeHalf(model.HalfExtents.Z);
            float density = cls == VehicleClass.Bus ? BusDensity : cls == VehicleClass.Truck ? TruckDensity : CarDensity;
            float mass = MathUtil.Clamp(8f * hx * hy * hz * density, MinMass, MaxMass);
            float wx = 2f * hx, wy = 2f * hy, wz = 2f * hz;
            float omega = (float)(2.0 * Math.PI * SuspensionFrequencyHz);
            const float gravity = 9.81f;
            float sag = gravity / (omega * omega);
            float cornerMass = mass * 0.25f;

            float wheelRadius;
            switch (model.Kind)
            {
                case VehicleKind.Van:
                case VehicleKind.Ambulance:
                case VehicleKind.Service:
                    wheelRadius = 0.38f;
                    break;
                default:
                    wheelRadius = cls == VehicleClass.Car ? 0.32f : 0.5f;
                    break;
            }

            float cs1Speed = model.MaxSpeed >= 0f && !float.IsInfinity(model.MaxSpeed) ? model.MaxSpeed : 0f;
            float top = cls == VehicleClass.Car ? Math.Max(1.4f * cs1Speed, 25f) : Math.Max(cs1Speed, 20f);
            if (model.Kind == VehicleKind.Police || model.Kind == VehicleKind.Ambulance || model.Kind == VehicleKind.FireTruck)
            {
                top *= 1.15f;
            }

            float engine = cls == VehicleClass.Car ? 1.1f * mass * 27f / 6f : 1.1f * mass * 20f / 10f;
            float spring = cornerMass * omega * omega;
            const float restLength = 0.35f;

            var t = new VehicleTuning();
            t.Kind = model.Kind;
            t.Class = cls;
            t.HalfExtents = new Vec3(hx, hy, hz);
            t.BodyClearance = Math.Min(0.2f, hy);
            t.Mass = mass;
            t.Inertia = new Vec3(
                mass / 12f * (wy * wy + wz * wz),
                mass / 12f * (wx * wx + wz * wz),
                mass / 12f * (wx * wx + wy * wy));
            t.CenterOfMassOffset = new Vec3(0f, -0.3f * hy, 0f);
            t.AxleHalfSpacing = 0.65f * hz;
            t.TrackHalfWidth = 0.85f * hx;
            t.WheelRadius = wheelRadius;
            t.SuspensionRestLength = restLength;
            t.MountHeight = -hy + wheelRadius + restLength - sag;
            t.SpringStiffness = spring;
            t.DamperCoefficient = 2f * SuspensionDampingRatio * cornerMass * omega;
            t.AntiRollStiffness = 0.5f * spring;
            t.TopSpeed = top;
            t.ReverseTopSpeed = 9f;
            t.EngineForce = engine;
            t.ReverseForce = 0.6f * engine;
            t.BrakeForce = mass * 9f;
            t.ReverseEngageSpeed = 1f;
            t.MaxSteerDeg = 35f;
            t.MinSteerDeg = 8f;
            t.SteerRate = 4f;
            t.LateralGrip = 1.6f;
            t.LongitudinalGrip = 1.6f;
            t.PeakSlipAngleDeg = 8f;
            t.HandbrakeGripFactor = 0.35f;
            t.RollingResistance = 0.04f;
            t.DragCoefficient = 0.5f * 1.225f * 0.35f * (wx * wy);
            t.DownforceCoefficient = 0.0015f * mass;
            t.Gravity = gravity;
            t.AirAngularDamping = 0.5f;
            t.AirYawControl = 2f;
            t.AirPitchControl = 1.5f;
            t.Restitution = 0.1f;
            t.ImpactFriction = 0.4f;
            t.PedestrianMass = 80f;
            t.DamageSpeedThreshold = 4f;
            t.DamageScale = 0.15f;
            t.ImpactReportSpeed = 1f;
            t.SmokingHealth = 40f;
            t.OnFireHealth = 15f;
            t.FireSeconds = 6f;
            t.FlipRecoverSeconds = 3f;
            return t;
        }

        /// <summary>Size class of a vehicle kind (see <see cref="VehicleClass"/>).</summary>
        public static VehicleClass ClassOf(VehicleKind kind)
        {
            switch (kind)
            {
                case VehicleKind.Bus:
                    return VehicleClass.Bus;
                case VehicleKind.Truck:
                case VehicleKind.Garbage:
                case VehicleKind.FireTruck:
                    return VehicleClass.Truck;
                default:
                    return VehicleClass.Car;
            }
        }

        /// <summary>
        /// Suspension mount of wheel <paramref name="index"/> (0 FL, 1 FR, 2 RL, 3 RR) in the body frame, m:
        /// <c>(±TrackHalfWidth, MountHeight, ±AxleHalfSpacing)</c>, left and rear negative. The suspension ray runs from here
        /// along the body's −y for <c>SuspensionRestLength + WheelRadius</c>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not 0..3.</exception>
        public Vec3 WheelMountLocal(int index)
        {
            if (index < 0 || index >= VehicleBody.WheelCount)
            {
                throw new ArgumentOutOfRangeException("index");
            }

            float x = (index & 1) == 0 ? -TrackHalfWidth : TrackHalfWidth;
            float z = index < 2 ? AxleHalfSpacing : -AxleHalfSpacing;
            return new Vec3(x, MountHeight, z);
        }

        private static float SafeHalf(float v)
        {
            return v >= MinHalfExtent && !float.IsInfinity(v) ? v : MinHalfExtent;
        }
    }
}
