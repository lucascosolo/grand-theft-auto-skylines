using System;
using System.Collections.Generic;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;

namespace GtaSkylines.Game.Vehicles
{
    /// <summary>Damage state from <see cref="VehicleBody.Health"/> and the fire timer.</summary>
    public enum VehicleDamageState
    {
        /// <summary>Health ≥ <see cref="VehicleTuning.SmokingHealth"/>.</summary>
        Intact = 0,

        /// <summary>Health below <see cref="VehicleTuning.SmokingHealth"/> and above <see cref="VehicleTuning.OnFireHealth"/>.</summary>
        Smoking = 1,

        /// <summary>Health reached <see cref="VehicleTuning.OnFireHealth"/> or less; explodes when <see cref="VehicleBody.FireSeconds"/> runs out.</summary>
        OnFire = 2,

        /// <summary>Exploded: permanent, controls ignored, health 0.</summary>
        Exploded = 3,
    }

    /// <summary>One wheel's state after the last step, for rendering, audio and skid marks. World space unless stated.</summary>
    public struct WheelState
    {
        /// <summary>True when the suspension ray hit ground this step.</summary>
        public bool Contact;

        /// <summary>
        /// Suspension compression, m: 0 fully extended (and always 0 without contact) up to
        /// <see cref="VehicleTuning.SuspensionRestLength"/> fully compressed. About 0.097 at rest on flat ground.
        /// </summary>
        public float Compression;

        /// <summary>
        /// Wheel centre: the mount moved along the body's −y by <c>SuspensionRestLength − Compression</c>.
        /// </summary>
        public Vec3 Center;

        /// <summary>Where the suspension ray hit the ground; with no contact, the lowest point of the fully extended wheel.</summary>
        public Vec3 GroundPoint;

        /// <summary>Ground normal at <see cref="GroundPoint"/>; the body's up without contact.</summary>
        public Vec3 GroundNormal;

        /// <summary>Surface flags of the ground; <see cref="SurfaceFlags.None"/> without contact.</summary>
        public SurfaceFlags GroundFlags;

        /// <summary>
        /// Rolling angle in degrees, wrapped to [0, 360). Increases when rolling forward: render as
        /// <c>Quat.AngleAxis(SpinDeg, Vec3.Right)</c> in the wheel's frame. Locked wheels do not turn; wheels without
        /// contact keep their last spin rate decaying.
        /// </summary>
        public float SpinDeg;

        /// <summary>Steering angle about the body's +y, degrees; positive turns the wheel's front toward +x (right). 0 for rear wheels.</summary>
        public float SteerDeg;

        /// <summary>Speed of the contact patch sliding over the ground, m/s (≥ 0; 0 without contact). For skid sounds and marks.</summary>
        public float SlipSpeed;
    }

    /// <summary>
    /// A rigid-body car (PLAN G5): the player's, and every police or mission car the AI drives. Stepped at a fixed 1/60 s
    /// against a <see cref="CollisionWorld"/>. Four raycast wheels with spring-damper suspension and slip-based tyre
    /// forces carry the body. The collision box (see <see cref="VehicleTuning.BodyClearance"/>) is approximated by a
    /// lattice of overlapping spheres that fill it plus a small sphere in each corner; their contacts with static
    /// triangles and dynamic shapes are resolved with impulses and position correction.
    /// <para>
    /// World frame: CS1/Unity metres, y up, left-handed. Body frame: see <see cref="VehicleTuning"/>. Angular velocities in
    /// rad/s about world axes, positive by the left-hand rule (positive y yaws the nose toward +x, i.e. turns right seen
    /// from above).
    /// </para>
    /// <para>
    /// Steady-state stepping allocates nothing. Not thread-safe; one thread owns the instance and the world.
    /// </para>
    /// </summary>
    public sealed class VehicleBody
    {
        /// <summary>Number of wheels: 0 front-left, 1 front-right, 2 rear-left, 3 rear-right.</summary>
        public const int WheelCount = 4;

        /// <summary>Longest dt one <see cref="Step"/> simulates, s; longer steps are clamped to it.</summary>
        public const float MaxStepSeconds = 0.1f;

        /// <summary>Longest internal sub-step, s.</summary>
        public const float MaxSubstepSeconds = 1f / 60f;

        /// <summary>Most internal sub-steps per <see cref="Step"/>.</summary>
        public const int MaxSubsteps = 8;

        /// <summary>Penetration into static geometry left after a step, at most, m.</summary>
        public const float PenetrationTolerance = 0.05f;

        /// <summary>Below this linear speed (m/s), held for <see cref="SleepSettleSeconds"/>, an idle grounded body sleeps.</summary>
        public const float SleepLinearSpeed = 0.05f;

        /// <summary>Below this angular speed (rad/s), held for <see cref="SleepSettleSeconds"/>, an idle grounded body sleeps.</summary>
        public const float SleepAngularSpeed = 0.05f;

        /// <summary>Seconds an idle grounded body must stay below the sleep speeds before it sleeps.</summary>
        public const float SleepSettleSeconds = 0.5f;

        private const float Skin = 0.02f;
        private const float QueryMargin = 0.1f;
        private const float CornerRadius = 0.1f;
        private const int MaxBigSpheres = 24;
        private const int MaxContacts = 64;
        private const float LandedAfterSeconds = 0.25f;
        private const float PedCooldownSeconds = 0.5f;
        private const float DriveFadeFraction = 0.15f;
        private const int TyreIterations = 6;
        private const float SlideFalloff = 0.3f;

        private readonly List<Contact> _overlap = new List<Contact>(32);
        private readonly BodyContact[] _contacts = new BodyContact[MaxContacts];
        private readonly Vec3[] _sphereLocal = new Vec3[MaxBigSpheres + 8];
        private readonly float[] _sphereRadius = new float[MaxBigSpheres + 8];
        private readonly Vec3[] _spherePrev = new Vec3[MaxBigSpheres + 8];
        private readonly WheelState[] _wheels = new WheelState[WheelCount];
        private readonly float[] _spinRate = new float[WheelCount];
        private readonly float[] _load = new float[WheelCount];
        private readonly TyreRow[] _rows = new TyreRow[WheelCount];
        private readonly float[] _compression = new float[WheelCount];
        private readonly int[] _coolTag = new int[8];
        private readonly float[] _coolTime = new float[8];

        private int _sphereCount;
        private Vec3 _shapeHalf;
        private float _shapeClearance = -1f;
        private float _bigRadius;

        private Vec3 _com;
        private Quat _rot = Quat.Identity;
        private Vec3 _vel;
        private Vec3 _ang;
        private float _health = 100f;
        private bool _onFire;
        private bool _exploded;
        private float _fire;
        private float _steer;
        private int _grounded;
        private float _airborne;
        private float _upsideDown;
        private bool _reversing;
        private bool _asleep;
        private float _still;
        private int _sleepVersion;
        private long _integrated;
        private int _lastSubsteps;

        // Per-step inputs shared with the sub-steps.
        private float _drive;
        private float _brake;
        private bool _handbrake;
        private float _airPitch;
        private int _contactCount;

        private struct TyreRow
        {
            public bool Active;
            public Vec3 F, S, R;
            public float KL, KT, LimitL, LimitT, Circle, Drive, AccL, AccT;
        }

        private struct BodyContact
        {
            public Vec3 Point;
            public Vec3 Normal;
            public float Depth;
            public ImpactKind Kind;
            public int Tag;
        }

        /// <summary>
        /// A body at the origin, identity rotation, at rest, full health, no wheel contact.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="tuning"/> is null.</exception>
        public VehicleBody(VehicleTuning tuning)
        {
            if (tuning == null)
            {
                throw new ArgumentNullException("tuning");
            }

            Tuning = tuning;
            _com = tuning.CenterOfMassOffset;
            ResetWheels();
        }

        /// <summary>Tuning, read every step.</summary>
        public VehicleTuning Tuning { get; private set; }

        /// <summary>World centre of the model's bounding box, m (not the centre of mass; see <see cref="VehicleTuning.CenterOfMassOffset"/>).</summary>
        public Vec3 Position
        {
            get { return _com - _rot * Tuning.CenterOfMassOffset; }
        }

        /// <summary>Orientation, unit length; the body frame's +z is <c>Rotation.Forward</c>.</summary>
        public Quat Rotation
        {
            get { return _rot; }
        }

        /// <summary>Linear velocity of the centre of mass, world m/s.</summary>
        public Vec3 Velocity
        {
            get { return _vel; }
        }

        /// <summary>Angular velocity, world rad/s (left-hand rule).</summary>
        public Vec3 AngularVelocity
        {
            get { return _ang; }
        }

        /// <summary>Signed speed along the body's forward, m/s: <c>Dot(Velocity, Rotation.Forward)</c>; negative when rolling backward.</summary>
        public float Speed
        {
            get { return Vec3.Dot(_vel, _rot.Forward); }
        }

        /// <summary><c>Dot(Rotation.Up, Vec3.Up)</c>: 1 upright, 0 on its side, −1 on its roof.</summary>
        public float Upright
        {
            get { return _rot.Up.Y; }
        }

        /// <summary>Health, 0..100; starts at 100; 0 once exploded. Impacts and <see cref="ApplyDamage"/> change it.</summary>
        public float Health
        {
            get { return _health; }
        }

        /// <summary>Damage state (see <see cref="VehicleDamageState"/>); never leaves Exploded.</summary>
        public VehicleDamageState DamageState
        {
            get
            {
                if (_exploded) return VehicleDamageState.Exploded;
                if (_onFire) return VehicleDamageState.OnFire;
                return _health < Tuning.SmokingHealth ? VehicleDamageState.Smoking : VehicleDamageState.Intact;
            }
        }

        /// <summary>
        /// Seconds left before the explosion while <see cref="VehicleDamageState.OnFire"/> (starts at
        /// <see cref="VehicleTuning.FireSeconds"/> and counts down by each later step's dt); 0 in every other state.
        /// </summary>
        public float FireSeconds
        {
            get { return _onFire && !_exploded ? _fire : 0f; }
        }

        /// <summary>Wheels touching ground after the last step, 0..4.</summary>
        public int GroundedWheelCount
        {
            get { return _grounded; }
        }

        /// <summary>Seconds since the last step with any wheel contact; 0 while some wheel touches.</summary>
        public float AirborneSeconds
        {
            get { return _airborne; }
        }

        /// <summary>
        /// Seconds the body has continuously had <see cref="Upright"/> &lt; 0.3 (on its side or roof) with |Velocity| &lt; 1 m/s
        /// and |AngularVelocity| &lt; 1 rad/s; reset to 0 whenever that stops holding, and by <see cref="Teleport"/> and
        /// <see cref="Recover"/>.
        /// </summary>
        public float UpsideDownSeconds
        {
            get { return _upsideDown; }
        }

        /// <summary>True when <see cref="UpsideDownSeconds"/> ≥ <see cref="VehicleTuning.FlipRecoverSeconds"/>: the session may call <see cref="Recover"/>.</summary>
        public bool CanRecover
        {
            get { return _upsideDown >= Tuning.FlipRecoverSeconds; }
        }

        /// <summary>The rate-limited steering value actually applied, −1..1 (see <see cref="VehicleTuning.SteerRate"/>).</summary>
        public float SmoothedSteer
        {
            get { return _steer; }
        }

        /// <summary>
        /// For audio: −1 while driving in reverse or Speed &lt; −ReverseEngageSpeed; 0 once exploded; otherwise
        /// <c>1 + min(4, floor(5 · max(Speed, 0) / TopSpeed))</c>.
        /// </summary>
        public int Gear
        {
            get
            {
                if (_exploded) return 0;
                float sp = Speed;
                if (_reversing || sp < -Tuning.ReverseEngageSpeed) return -1;
                return 1 + Math.Min(4, (int)Math.Floor(Fifths(sp)));
            }
        }

        /// <summary>
        /// For audio, 0..1: 0 once exploded; in a forward gear <c>0.2 + 0.8 · f</c> where f is how far Speed lies through that
        /// gear's fifth of TopSpeed (1 at or above TopSpeed); in reverse <c>0.2 + 0.8 · min(1, |Speed| / ReverseTopSpeed)</c>;
        /// 0.2 (idle) when stopped.
        /// </summary>
        public float EngineRpm
        {
            get
            {
                if (_exploded) return 0f;
                float sp = Speed;
                if (_reversing || sp < -Tuning.ReverseEngageSpeed)
                {
                    return 0.2f + 0.8f * Math.Min(1f, Math.Abs(sp) / Tuning.ReverseTopSpeed);
                }

                float f = Fifths(sp);
                if (f >= 5f) return 1f;
                return 0.2f + 0.8f * (f - (float)Math.Floor(f));
            }
        }

        /// <summary>
        /// True while the body sleeps: <see cref="Step"/> runs no physics and no world query and keeps the pose. It falls
        /// asleep after <see cref="SleepSettleSeconds"/> with idle controls (throttle equal to brake, steer at the smoothed
        /// value), a grounded wheel, no fire, and speeds below <see cref="SleepLinearSpeed"/> / <see cref="SleepAngularSpeed"/>.
        /// It wakes on <see cref="Wake"/>, <see cref="Teleport"/>, <see cref="SetVelocity"/>, <see cref="Recover"/>,
        /// damage, a <see cref="ResolveCollision"/> contact, non-idle controls, a static geometry change, or a dynamic shape
        /// touching its bounds. Its collision shape stays in the world.
        /// </summary>
        public bool IsSleeping
        {
            get { return _asleep; }
        }

        /// <summary>Number of <see cref="Step"/> calls that ran physics.</summary>
        public long IntegratedSteps
        {
            get { return _integrated; }
        }

        /// <summary>Sub-steps run by the last <see cref="Step"/>; 0 when it slept through.</summary>
        public int LastSubsteps
        {
            get { return _lastSubsteps; }
        }

        /// <summary>Wakes the body (no-op when awake) and restarts its settle timer.</summary>
        public void Wake()
        {
            _asleep = false;
            _still = 0f;
        }

        /// <summary>State of wheel <paramref name="index"/> after the last step (0 FL, 1 FR, 2 RL, 3 RR).</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not 0..3.</exception>
        public WheelState GetWheel(int index)
        {
            if (index < 0 || index >= WheelCount)
            {
                throw new ArgumentOutOfRangeException("index");
            }

            return _wheels[index];
        }

        /// <summary>
        /// Places the body with its bounding-box centre at <paramref name="position"/> and orientation
        /// <paramref name="rotation"/> (normalised). Clears linear and angular velocity, the steering smoothing, wheel
        /// contacts and spin, <see cref="AirborneSeconds"/> and <see cref="UpsideDownSeconds"/>. Health, damage state and
        /// fire timer are kept. Makes no world query: the caller picks a free spot.
        /// </summary>
        /// <exception cref="ArgumentException">A component of <paramref name="position"/> or <paramref name="rotation"/> is not finite, or the rotation has zero length.</exception>
        public void Teleport(Vec3 position, Quat rotation)
        {
            if (!position.IsFinite)
            {
                throw new ArgumentException("position must be finite", "position");
            }

            float len2 = rotation.X * rotation.X + rotation.Y * rotation.Y + rotation.Z * rotation.Z + rotation.W * rotation.W;
            if (float.IsNaN(len2) || float.IsInfinity(len2) || len2 < 1e-12f)
            {
                throw new ArgumentException("rotation must be finite and non-zero", "rotation");
            }

            _rot = rotation.Normalized;
            _com = position + _rot * Tuning.CenterOfMassOffset;
            _vel = Vec3.Zero;
            _ang = Vec3.Zero;
            _steer = 0f;
            _airborne = 0f;
            _upsideDown = 0f;
            _reversing = false;
            ResetWheels();
            Wake();
        }

        /// <summary>
        /// Sets the linear velocity of the centre of mass (world m/s) and the angular velocity (world rad/s), e.g. to spawn
        /// a car already moving. Nothing else changes.
        /// </summary>
        /// <exception cref="ArgumentException">A component is not finite.</exception>
        public void SetVelocity(Vec3 linear, Vec3 angular)
        {
            if (!linear.IsFinite || !angular.IsFinite)
            {
                throw new ArgumentException("velocities must be finite");
            }

            _vel = linear;
            _ang = angular;
            Wake();
        }

        /// <summary>
        /// Removes <paramref name="amount"/> health (gunfire, explosions) exactly as an impact would: Health is clamped at
        /// 0; dropping below <see cref="VehicleTuning.SmokingHealth"/> makes it Smoking; reaching
        /// <see cref="VehicleTuning.OnFireHealth"/> or less sets it on fire with a full <see cref="VehicleTuning.FireSeconds"/>
        /// timer (a fire already burning keeps its timer), and a later <see cref="Step"/> explodes it. Zero, negative and
        /// non-finite amounts, and any damage to an exploded body, are ignored.
        /// </summary>
        public void ApplyDamage(float amount)
        {
            if (_exploded || !(amount > 0f))
            {
                return;
            }

            Wake();
            _health = Math.Max(0f, _health - amount);
            if (!_onFire && _health <= Tuning.OnFireHealth)
            {
                _onFire = true;
                _fire = Tuning.FireSeconds;
            }
        }

        /// <summary>
        /// Flip assist: rights the body in place. Keeps the heading (yaw of the forward axis; if forward is near vertical,
        /// of the up axis instead), sets pitch and roll to 0, raises <see cref="Position"/> by <c>HalfExtents.Y</c>, and clears
        /// velocities and <see cref="UpsideDownSeconds"/>. The next steps let it drop onto its wheels. Allowed in any
        /// state (the session decides when, normally on <see cref="CanRecover"/>).
        /// </summary>
        public void Recover()
        {
            Vec3 pos = Position;
            Vec3 fwd = _rot.Forward;
            Vec3 heading = Math.Abs(fwd.Y) > 0.9f ? _rot.Up * (fwd.Y > 0f ? -1f : 1f) : fwd;
            _rot = Quat.Yaw(Vec3.YawDegOf(heading));
            pos.Y += Tuning.HalfExtents.Y;
            _com = pos + _rot * Tuning.CenterOfMassOffset;
            _vel = Vec3.Zero;
            _ang = Vec3.Zero;
            _upsideDown = 0f;
            Wake();
        }

        /// <summary>
        /// Advances the body by <paramref name="dt"/> seconds (1/60 normally).
        /// <para>
        /// Edges: dt ≤ 0 or non-finite → nothing changes and <c>default(VehicleStepResult)</c> is returned; dt above
        /// <see cref="MaxStepSeconds"/> is clamped to it. The step is split into equal sub-steps of at most
        /// <see cref="MaxSubstepSeconds"/>, more when fast (at most <see cref="MaxSubsteps"/>), so no collision sphere moves
        /// more than half its radius per sub-step: it never tunnels through a 0.1 m wall at 50 m/s and never ends the step
        /// more than <see cref="PenetrationTolerance"/> inside static geometry. Control values are clamped to their ranges;
        /// non-finite ones count as 0 (<see cref="VehicleControls"/> describes drive, brake and reverse).
        /// </para>
        /// <para>
        /// Ground: each wheel casts a ray from its mount along the body's −y for rest length + wheel radius against static
        /// triangles; a hit gives spring + damper + anti-roll force along the body's up and tyre forces in the ground plane.
        /// Upside down (Upright ≤ 0) no wheel exerts force. With no wheel contact the body is airborne: gravity only,
        /// angular velocity decays by <see cref="VehicleTuning.AirAngularDamping"/>, steer yaws
        /// (<see cref="VehicleTuning.AirYawControl"/>) and throttle/brake pitch (<see cref="VehicleTuning.AirPitchControl"/>).
        /// </para>
        /// <para>
        /// Body collisions: static triangles and dynamic boxes are immovable; dynamic capsules are 80 kg pedestrians whose
        /// impulse on the body is capped at that of a perfectly inelastic hit on a free 80 kg mass, so a car above 5 m/s is
        /// not stopped by one; a capsule tag that was hit is ignored for the next 0.5 s. Shapes tagged
        /// <paramref name="selfTag"/> are ignored; boxes of other Game-driven bodies should not be in the world when this
        /// body steps (they would be immovable): use <see cref="ResolveCollision"/>. Every impact at or above
        /// <see cref="VehicleTuning.ImpactReportSpeed"/> is reported (see <see cref="VehicleStepResult"/>), and costs health
        /// once per step per other: <c>DamageScale · (v − DamageSpeedThreshold)² · r</c> with r = 1 for static and boxes and
        /// <c>2 · 80 / (Mass + 80)</c> for capsules.
        /// </para>
        /// <para>
        /// Damage: Health ≤ OnFireHealth starts the fire timer; when it runs out the body explodes (result.Exploded on that
        /// step only, Health 0). An exploded body ignores <paramref name="controls"/>: no drive, no steering change, all four
        /// wheels locked; it still falls, collides and reports impacts but takes no further damage.
        /// </para>
        /// <para>
        /// If the integration ever produces a non-finite state, the body returns to its pose at the start of the step with
        /// zero velocities.
        /// </para>
        /// </summary>
        /// <param name="dt">Seconds to advance.</param>
        /// <param name="controls">Driver input.</param>
        /// <param name="world">Collision scene.</param>
        /// <param name="selfTag">This body's own tag in the world's dynamic shapes, ignored by every query.</param>
        /// <exception cref="ArgumentNullException"><paramref name="world"/> is null.</exception>
        public VehicleStepResult Step(float dt, VehicleControls controls, CollisionWorld world, int selfTag)
        {
            if (world == null)
            {
                throw new ArgumentNullException("world");
            }

            var result = default(VehicleStepResult);
            if (!(dt > 0f) || float.IsInfinity(dt))
            {
                return result;
            }

            if (dt > MaxStepSeconds)
            {
                dt = MaxStepSeconds;
            }

            VehicleTuning t = Tuning;
            EnsureShape();
            if (_asleep)
            {
                Vec3 lo, hi;
                Bounds(Skin, out lo, out hi);
                if (Idle(controls, _steer) && world.StaticVersion == _sleepVersion && !world.AnyDynamicTouching(lo, hi, selfTag))
                {
                    _lastSubsteps = 0;
                    return result;
                }

                Wake();
            }

            _integrated++;
            Vec3 com0 = _com;
            Quat rot0 = _rot;
            bool wasGrounded = _grounded > 0;
            float airBefore = _airborne;

            if (_onFire && !_exploded)
            {
                _fire -= dt;
                if (_fire <= 0f)
                {
                    _exploded = true;
                    _onFire = false;
                    _fire = 0f;
                    _health = 0f;
                    result.Exploded = true;
                }
            }

            for (int i = 0; i < _coolTime.Length; i++)
            {
                _coolTime[i] -= dt;
            }

            ReadControls(controls, dt);

            float sweepSpeed = _vel.Length + _ang.Length * _shapeHalf.Length;
            int n = (int)Math.Ceiling(dt / MaxSubstepSeconds - 1e-3f);
            int byTravel = (int)Math.Ceiling(sweepSpeed * dt / (0.5f * _bigRadius));
            n = MathUtil.Clamp(Math.Max(n, byTravel), 1, MaxSubsteps);
            float h = dt / n;
            _lastSubsteps = n;
            QueryMask mask = QueryMask.All.Ignoring(selfTag);
            for (int i = 0; i < n; i++)
            {
                Substep(h, world, mask, ref result);
            }

            if (!(_com.IsFinite && _vel.IsFinite && _ang.IsFinite && QuatFinite(_rot)))
            {
                _com = com0;
                _rot = rot0;
                _vel = Vec3.Zero;
                _ang = Vec3.Zero;
            }

            if (_grounded > 0)
            {
                _airborne = 0f;
                result.Landed = !wasGrounded && airBefore >= LandedAfterSeconds;
            }
            else
            {
                _airborne += dt;
                result.LeftGround = wasGrounded;
            }

            if (Upright < 0.3f && _vel.Length < 1f && _ang.Length < 1f)
            {
                _upsideDown += dt;
            }
            else
            {
                _upsideDown = 0f;
            }

            for (int i = 0; i < result.ImpactCount; i++)
            {
                VehicleImpact imp = result.GetImpact(i);
                float ratio = imp.Kind == ImpactKind.Capsule ? 2f * t.PedestrianMass / (t.Mass + t.PedestrianMass) : 1f;
                imp.Damage = DamageFor(imp.RelativeNormalSpeed, ratio);
                ApplyDamage(imp.Damage);
                result.SetImpact(i, imp);
            }

            bool still = Idle(controls, _steer) && _grounded > 0 && !_onFire
                && _vel.LengthSquared < SleepLinearSpeed * SleepLinearSpeed && _ang.LengthSquared < SleepAngularSpeed * SleepAngularSpeed;
            _still = still ? _still + dt : 0f;
            if (_still >= SleepSettleSeconds)
            {
                _asleep = true;
                _still = 0f;
                _vel = Vec3.Zero;
                _ang = Vec3.Zero;
                _sleepVersion = world.StaticVersion;
            }

            return result;
        }

        /// <summary>
        /// Collides two Game-driven bodies (oriented collision boxes; call once per pair after both stepped). When the boxes
        /// overlap: separates them along the axis of least penetration, split inversely to mass, and exchanges an impulse at
        /// the contact point with <see cref="VehicleTuning.Restitution"/> and <see cref="VehicleTuning.ImpactFriction"/> (the
        /// smaller of each body's values), conserving linear momentum. Each body gets an <see cref="ImpactKind.Vehicle"/>
        /// impact naming the other's tag (its <see cref="VehicleImpact.RelativeNormalSpeed"/> is the closing speed, 0 when
        /// the boxes merely overlap), and, unless exploded, loses health with <c>r = 2 · mOther / (mSelf + mOther)</c> in the
        /// damage formula of <see cref="Step"/>. A fire started here explodes in a later Step. An overlap wakes both bodies.
        /// Returns false and default impacts when the boxes do not overlap, both bodies sleep, or <c>a == b</c>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="a"/> or <paramref name="b"/> is null.</exception>
        public static bool ResolveCollision(VehicleBody a, int tagA, VehicleBody b, int tagB, out VehicleImpact impactOnA, out VehicleImpact impactOnB)
        {
            if (a == null) throw new ArgumentNullException("a");
            if (b == null) throw new ArgumentNullException("b");
            impactOnA = default(VehicleImpact);
            impactOnB = default(VehicleImpact);
            if (a == b || (a._asleep && b._asleep))
            {
                return false;
            }

            Vec3 ca, ea, cb, eb;
            a.CollisionBox(out ca, out ea);
            b.CollisionBox(out cb, out eb);
            Vec3 a0 = a._rot.Right, a1 = a._rot.Up, a2 = a._rot.Forward;
            Vec3 b0 = b._rot.Right, b1 = b._rot.Up, b2 = b._rot.Forward;
            Vec3 d = cb - ca;

            float best = float.MaxValue;
            Vec3 normal = Vec3.Zero;
            for (int i = 0; i < 15; i++)
            {
                Vec3 axis;
                if (i < 3) axis = Axis(a0, a1, a2, i);
                else if (i < 6) axis = Axis(b0, b1, b2, i - 3);
                else axis = Vec3.Cross(Axis(a0, a1, a2, (i - 6) / 3), Axis(b0, b1, b2, (i - 6) % 3));

                float len = axis.Length;
                if (len < 1e-4f) continue;
                axis = axis / len;
                float ra = Project(ea, a0, a1, a2, axis);
                float rb = Project(eb, b0, b1, b2, axis);
                float dist = Vec3.Dot(d, axis);
                float pen = ra + rb - Math.Abs(dist);
                if (pen <= 0f) return false;
                float score = i < 6 ? pen : pen * 1.05f + 1e-3f;
                if (score < best)
                {
                    best = score;
                    normal = dist >= 0f ? axis : -axis;
                }
            }

            float penetration = Project(ea, a0, a1, a2, normal) + Project(eb, b0, b1, b2, normal) - Vec3.Dot(d, normal);
            a.Wake();
            b.Wake();

            // Contact point: average of corners inside the other box, else the midpoint of the two support corners.
            Vec3 sum = Vec3.Zero;
            int count = 0;
            for (int i = 0; i < 8; i++)
            {
                Vec3 pa = Corner(ca, ea, a0, a1, a2, i);
                if (Inside(pa, cb, eb, b0, b1, b2)) { sum += pa; count++; }
                Vec3 pb = Corner(cb, eb, b0, b1, b2, i);
                if (Inside(pb, ca, ea, a0, a1, a2)) { sum += pb; count++; }
            }

            Vec3 point = count > 0
                ? sum / count
                : (Support(ca, ea, a0, a1, a2, normal) + Support(cb, eb, b0, b1, b2, -normal)) * 0.5f;

            float ma = a.Tuning.Mass, mb = b.Tuning.Mass;
            float sep = penetration + 1e-3f;
            a._com -= normal * (sep * mb / (ma + mb));
            b._com += normal * (sep * ma / (ma + mb));

            Vec3 ra2 = point - a._com, rb2 = point - b._com;
            Vec3 vrel = b.PointVelocity(rb2) - a.PointVelocity(ra2);
            float vn = Vec3.Dot(vrel, normal);
            float closing = Math.Max(0f, -vn);
            if (vn < 0f)
            {
                float e = closing > 1f ? Math.Min(a.Tuning.Restitution, b.Tuning.Restitution) : 0f;
                float k = a.InvMassAt(ra2, normal) + b.InvMassAt(rb2, normal);
                float jn = -(1f + e) * vn / k;
                a.ApplyImpulse(ra2, normal * -jn);
                b.ApplyImpulse(rb2, normal * jn);

                vrel = b.PointVelocity(rb2) - a.PointVelocity(ra2);
                Vec3 vt = vrel - normal * Vec3.Dot(vrel, normal);
                float vtl = vt.Length;
                if (vtl > 1e-4f)
                {
                    Vec3 tdir = vt / vtl;
                    float kt = a.InvMassAt(ra2, tdir) + b.InvMassAt(rb2, tdir);
                    float mu = Math.Min(a.Tuning.ImpactFriction, b.Tuning.ImpactFriction);
                    float jt = Math.Min(vtl / kt, mu * jn);
                    a.ApplyImpulse(ra2, tdir * jt);
                    b.ApplyImpulse(rb2, tdir * -jt);
                }
            }

            impactOnA.Point = point;
            impactOnA.Normal = -normal;
            impactOnA.RelativeNormalSpeed = closing;
            impactOnA.OtherTag = tagB;
            impactOnA.Kind = ImpactKind.Vehicle;
            impactOnB.Point = point;
            impactOnB.Normal = normal;
            impactOnB.RelativeNormalSpeed = closing;
            impactOnB.OtherTag = tagA;
            impactOnB.Kind = ImpactKind.Vehicle;
            if (!a._exploded)
            {
                impactOnA.Damage = a.DamageFor(closing, 2f * mb / (ma + mb));
                a.ApplyDamage(impactOnA.Damage);
            }

            if (!b._exploded)
            {
                impactOnB.Damage = b.DamageFor(closing, 2f * ma / (ma + mb));
                b.ApplyDamage(impactOnB.Damage);
            }

            return true;
        }

        // ---- step internals ----

        private void ReadControls(VehicleControls c, float dt)
        {
            VehicleTuning t = Tuning;
            float throttle = Pedal(c.Throttle), brake = Pedal(c.Brake);
            float steer = float.IsNaN(c.Steer) || float.IsInfinity(c.Steer) ? 0f : MathUtil.Clamp(c.Steer, -1f, 1f);
            _drive = 0f;
            _brake = 0f;
            _handbrake = false;
            _airPitch = 0f;
            _reversing = false;
            if (_exploded)
            {
                return;
            }

            _steer = MathUtil.MoveTowards(_steer, steer, t.SteerRate * dt);
            _handbrake = c.Handbrake;
            _airPitch = throttle - brake;
            float sp = Speed, e = t.ReverseEngageSpeed;
            if (sp > e)
            {
                if (throttle > brake) _drive = throttle;
                else _brake = brake;
            }
            else if (sp < -e)
            {
                if (brake > throttle) _drive = -brake;
                else _brake = throttle;
            }
            else if (throttle > brake)
            {
                _drive = throttle;
            }
            else if (brake > throttle)
            {
                _drive = -brake;
            }
            else
            {
                _brake = 1f;
            }

            _reversing = _drive < 0f;
        }

        private void Substep(float h, CollisionWorld world, QueryMask mask, ref VehicleStepResult result)
        {
            VehicleTuning t = Tuning;
            float m = t.Mass;
            Vec3 up = _rot.Up, fwd = _rot.Forward, right = _rot.Right;
            Vec3 pos = _com - _rot * t.CenterOfMassOffset;
            float speed = Vec3.Dot(_vel, fwd);
            float steerDeg = _steer * MathUtil.Lerp(t.MaxSteerDeg, t.MinSteerDeg, MathUtil.Clamp01(Math.Abs(speed) / t.TopSpeed));
            float rayLength = t.SuspensionRestLength + t.WheelRadius;
            Vec3 qlo, qhi;
            Bounds(rayLength + Skin + QueryMargin + 2f * h * (_vel.Length + _ang.Length * _shapeHalf.Length), out qlo, out qhi);
            world.GatherCandidates(qlo, qhi);

            Vec3 force = new Vec3(0f, -t.Gravity * m, 0f) - _vel * (t.DragCoefficient * _vel.Length);
            Vec3 torque = Vec3.Zero;
            _grounded = 0;

            for (int i = 0; i < WheelCount; i++)
            {
                Vec3 mount = pos + _rot * t.WheelMountLocal(i);
                PhysicsHit hit;
                WheelState w = _wheels[i];
                w.SteerDeg = i < 2 ? steerDeg : 0f;
                _load[i] = 0f;
                _compression[i] = 0f;
                if (Upright > 0f && Raycast(world, mount, -up, rayLength, out hit))
                {
                    float c = rayLength - hit.Distance;
                    float rate = -Vec3.Dot(PointVelocity(mount - _com), up);
                    float f = t.SpringStiffness * c + t.DamperCoefficient * rate;
                    if (c > t.SuspensionRestLength)
                    {
                        f += 10f * t.SpringStiffness * (c - t.SuspensionRestLength);
                    }

                    _load[i] = f;
                    _compression[i] = c;
                    w.Contact = true;
                    w.Compression = Math.Min(c, t.SuspensionRestLength);
                    w.GroundPoint = hit.Point;
                    w.GroundNormal = hit.Normal;
                    w.GroundFlags = hit.Flags;
                    _grounded++;
                }
                else
                {
                    w.Contact = false;
                    w.Compression = 0f;
                    w.GroundNormal = up;
                    w.GroundFlags = SurfaceFlags.None;
                    w.SlipSpeed = 0f;
                }

                w.Center = mount - up * (t.SuspensionRestLength - w.Compression);
                if (!w.Contact)
                {
                    w.GroundPoint = w.Center - up * t.WheelRadius;
                }

                _wheels[i] = w;
            }

            for (int axle = 0; axle < 4; axle += 2)
            {
                if (_wheels[axle].Contact && _wheels[axle + 1].Contact)
                {
                    float roll = t.AntiRollStiffness * (_compression[axle] - _compression[axle + 1]);
                    _load[axle] += roll;
                    _load[axle + 1] -= roll;
                }
            }

            float maxLoad = m * t.Gravity * 20f;
            for (int i = 0; i < WheelCount; i++)
            {
                if (!_wheels[i].Contact) continue;
                _load[i] = MathUtil.Clamp(_load[i], 0f, maxLoad);
                Vec3 f = up * _load[i];
                force += f;
                torque += Vec3.Cross(_wheels[i].GroundPoint - _com, f);
            }

            if (_grounded > 0)
            {
                force -= up * (t.DownforceCoefficient * speed * speed);
            }

            _vel += force * (h / m);
            _ang += InvInertia(torque) * h;

            if (_grounded > 0)
            {
                Tyres(h, speed, fwd, right);
            }
            else
            {
                for (int i = 0; i < WheelCount; i++)
                {
                    _spinRate[i] *= Math.Max(0f, 1f - h);
                }

                if (!_exploded)
                {
                    Vec3 local = new Vec3(t.AirPitchControl * _airPitch, t.AirYawControl * _steer, 0f);
                    _ang += _rot * local * h;
                }

                _ang *= Math.Max(0f, 1f - t.AirAngularDamping * h);
            }

            for (int i = 0; i < WheelCount; i++)
            {
                WheelState w = _wheels[i];
                w.SpinDeg = Wrap360(w.SpinDeg + _spinRate[i] * h * (float)MathUtil.Rad2Deg);
                _wheels[i] = w;
            }

            for (int i = 0; i < _sphereCount; i++)
            {
                _spherePrev[i] = pos + _rot * _sphereLocal[i];
            }

            _com += _vel * h;
            var spin = new Quat(_ang.X, _ang.Y, _ang.Z, 0f) * _rot;
            float half = 0.5f * h;
            _rot = new Quat(_rot.X + spin.X * half, _rot.Y + spin.Y * half, _rot.Z + spin.Z * half, _rot.W + spin.W * half).Normalized;

            Collide(world, mask, ref result);
        }

        private void Tyres(float h, float speed, Vec3 fwd, Vec3 right)
        {
            VehicleTuning t = Tuning;
            float driveForce = 0f;
            if (_drive > 0f)
            {
                driveForce = t.EngineForce * _drive * Fade(speed / t.TopSpeed);
            }
            else if (_drive < 0f)
            {
                driveForce = t.ReverseForce * _drive * Fade(-speed / t.ReverseTopSpeed);
            }

            float peak = t.PeakSlipAngleDeg * (float)MathUtil.Deg2Rad;
            for (int i = 0; i < WheelCount; i++)
            {
                TyreRow row = default(TyreRow);
                WheelState w = _wheels[i];
                if (w.Contact)
                {
                    bool rear = i >= 2;
                    bool locked = _exploded || (_handbrake && rear);
                    float steerRad = w.SteerDeg * (float)MathUtil.Deg2Rad;
                    Vec3 wf = fwd * (float)Math.Cos(steerRad) + right * (float)Math.Sin(steerRad);
                    Vec3 nrm = w.GroundNormal;
                    row.F = (wf - nrm * Vec3.Dot(wf, nrm)).Normalized;
                    row.Active = row.F.LengthSquared > 0.5f;
                    row.S = Vec3.Cross(nrm, row.F);
                    row.R = w.GroundPoint - _com;
                    row.KL = 1f / InvMassAt(row.R, row.F);
                    row.KT = 1f / InvMassAt(row.R, row.S);
                    Vec3 vp = PointVelocity(row.R);
                    float vl = Vec3.Dot(vp, row.F), vt = Vec3.Dot(vp, row.S);
                    float nh = _load[i] * h;
                    float muL = t.LongitudinalGrip * nh;
                    float latGrip = t.LateralGrip * (_handbrake && rear && !_exploded ? t.HandbrakeGripFactor : 1f);
                    float alpha = (float)Math.Atan2(vt, Math.Max(Math.Abs(vl), 0.5f));
                    row.LimitT = latGrip * nh * Math.Abs(SlipCurve(alpha / peak));
                    row.Circle = Math.Max(t.LongitudinalGrip, latGrip) * nh;
                    if (locked) row.LimitL = muL;
                    else if (_brake > 0f) row.LimitL = Math.Min(t.BrakeForce * _brake * (rear ? 0.2f : 0.3f) * h, muL);
                    else row.LimitL = t.RollingResistance * nh;
                    if (rear && !locked && _brake <= 0f)
                    {
                        row.Drive = MathUtil.Clamp(0.5f * driveForce * h, -muL, muL);
                    }

                    _spinRate[i] = locked ? 0f : vl / t.WheelRadius;
                    w.SlipSpeed = locked ? (float)Math.Sqrt(vl * vl + vt * vt) : Math.Abs(vt);
                    _wheels[i] = w;
                }

                _rows[i] = row;
                if (row.Active && row.Drive != 0f)
                {
                    ApplyImpulse(row.R, row.F * row.Drive);
                }
            }

            for (int iter = 0; iter < TyreIterations; iter++)
            {
                for (int axle = 0; axle < WheelCount; axle += 2)
                {
                    Vec3 dv0 = TyreDelta(axle), dv1 = TyreDelta(axle + 1);
                    ApplyImpulse(_rows[axle].R, dv0);
                    ApplyImpulse(_rows[axle + 1].R, dv1);
                }
            }
        }

        /// <summary>Projected Gauss-Seidel step for one tyre row (half-relaxed: the axle partner is solved from the same state).</summary>
        private Vec3 TyreDelta(int i)
        {
            TyreRow row = _rows[i];
            if (!row.Active)
            {
                return Vec3.Zero;
            }

            Vec3 vp = PointVelocity(row.R);
            float accL = MathUtil.Clamp(row.AccL - 0.5f * Vec3.Dot(vp, row.F) * row.KL, -row.LimitL, row.LimitL);
            float accT = MathUtil.Clamp(row.AccT - 0.5f * Vec3.Dot(vp, row.S) * row.KT, -row.LimitT, row.LimitT);
            float totalL = accL + row.Drive;
            float mag = (float)Math.Sqrt(totalL * totalL + accT * accT);
            if (mag > row.Circle && mag > 0f)
            {
                float k = row.Circle / mag;
                accL = totalL * k - row.Drive;
                accT *= k;
            }

            Vec3 delta = row.F * (accL - row.AccL) + row.S * (accT - row.AccT);
            row.AccL = accL;
            row.AccT = accT;
            _rows[i] = row;
            return delta;
        }

        private void Collide(CollisionWorld world, QueryMask mask, ref VehicleStepResult result)
        {
            VehicleTuning t = Tuning;
            Vec3 pos = _com - _rot * t.CenterOfMassOffset;
            _contactCount = 0;
            for (int i = 0; i < _sphereCount; i++)
            {
                Vec3 centre = pos + _rot * _sphereLocal[i];
                float radius = _sphereRadius[i];
                Vec3 moved = centre - _spherePrev[i];
                float rs = radius + Skin;
                Vec3 ext = new Vec3(rs, rs, rs);
                if (world.CandidatesCover(centre - ext, centre + ext)) world.OverlapSphereCandidates(centre, rs, mask, _overlap);
                else world.OverlapSphere(centre, rs, mask, _overlap);
                for (int k = 0; k < _overlap.Count && _contactCount < MaxContacts; k++)
                {
                    Contact c = _overlap[k];
                    // A contact pushing along this sphere's own motion means it entered from the far side: ignore it.
                    if (Vec3.Dot(c.Normal, moved) > 0.25f * radius) continue;
                    var bc = new BodyContact { Point = c.Point, Normal = c.Normal, Depth = c.Depth - Skin, Tag = c.Tag };
                    bc.Kind = c.Kind == HitKind.Box ? ImpactKind.Box : c.Kind == HitKind.Capsule ? ImpactKind.Capsule : ImpactKind.Static;
                    if (bc.Kind == ImpactKind.Static) bc.Tag = -1;
                    _contacts[_contactCount++] = bc;
                }
            }

            if (_contactCount == 0)
            {
                return;
            }

            for (int iter = 0; iter < 3; iter++)
            {
                for (int k = 0; k < _contactCount; k++)
                {
                    BodyContact c = _contacts[k];
                    Vec3 r = c.Point - _com;
                    float vn = Vec3.Dot(PointVelocity(r), c.Normal);
                    if (vn >= 0f) continue;

                    float kn = InvMassAt(r, c.Normal);
                    if (c.Kind == ImpactKind.Capsule)
                    {
                        if (iter > 0 || Cooling(c.Tag)) continue;
                        StartCooldown(c.Tag);
                        Record(ref result, c, -vn);
                        ApplyImpulse(r, c.Normal * (-vn / (kn + 1f / t.PedestrianMass)));
                        continue;
                    }

                    if (iter == 0)
                    {
                        Record(ref result, c, -vn);
                    }

                    float e = iter == 0 && -vn > 1f ? t.Restitution : 0f;
                    float jn = -(1f + e) * vn / kn;
                    ApplyImpulse(r, c.Normal * jn);

                    Vec3 vp = PointVelocity(r);
                    Vec3 vt = vp - c.Normal * Vec3.Dot(vp, c.Normal);
                    float vtl = vt.Length;
                    if (vtl > 1e-4f)
                    {
                        Vec3 dir = vt / vtl;
                        float jt = Math.Min(vtl * t.Mass, t.ImpactFriction * jn);
                        _vel -= dir * (jt / t.Mass);
                    }
                }
            }

            Vec3 corr = Vec3.Zero;
            for (int k = 0; k < _contactCount; k++)
            {
                BodyContact c = _contacts[k];
                if (c.Kind == ImpactKind.Capsule || c.Depth <= 0f) continue;
                float need = c.Depth - Vec3.Dot(corr, c.Normal);
                if (need > 0f) corr += c.Normal * need;
            }

            _com += Vec3.ClampLength(corr, 1f);
        }

        private static bool Raycast(CollisionWorld world, Vec3 origin, Vec3 dir, float length, out PhysicsHit hit)
        {
            Vec3 end = origin + dir * length;
            if (world.CandidatesCover(Vec3.Min(origin, end), Vec3.Max(origin, end)))
            {
                return world.RaycastCandidates(origin, dir, length, QueryMask.StaticOnly, out hit);
            }

            return world.Raycast(origin, dir, length, QueryMask.StaticOnly, out hit);
        }

        /// <summary>World bounds of the collision box grown by <paramref name="margin"/> on every side.</summary>
        private void Bounds(float margin, out Vec3 lo, out Vec3 hi)
        {
            Vec3 c, half;
            CollisionBox(out c, out half);
            Vec3 e = CollisionWorld.BoxExtent(_rot, half) + new Vec3(margin, margin, margin);
            lo = c - e;
            hi = c + e;
        }

        private bool Idle(VehicleControls c, float steer)
        {
            if (_exploded) return true;
            float target = float.IsNaN(c.Steer) || float.IsInfinity(c.Steer) ? 0f : MathUtil.Clamp(c.Steer, -1f, 1f);
            return Pedal(c.Throttle) == Pedal(c.Brake) && target == steer;
        }

        private void Record(ref VehicleStepResult result, BodyContact c, float speed)
        {
            if (speed < Tuning.ImpactReportSpeed)
            {
                return;
            }

            result.AddImpact(new VehicleImpact { Point = c.Point, Normal = c.Normal, RelativeNormalSpeed = speed, OtherTag = c.Tag, Kind = c.Kind });
        }

        private bool Cooling(int tag)
        {
            for (int i = 0; i < _coolTag.Length; i++)
            {
                if (_coolTime[i] > 0f && _coolTag[i] == tag) return true;
            }

            return false;
        }

        private void StartCooldown(int tag)
        {
            int slot = 0;
            for (int i = 1; i < _coolTime.Length; i++)
            {
                if (_coolTime[i] < _coolTime[slot]) slot = i;
            }

            _coolTag[slot] = tag;
            _coolTime[slot] = PedCooldownSeconds;
        }

        private float DamageFor(float speed, float ratio)
        {
            if (_exploded) return 0f;
            float over = speed - Tuning.DamageSpeedThreshold;
            return over > 0f ? Tuning.DamageScale * over * over * ratio : 0f;
        }

        // ---- shape and rigid-body helpers ----

        private void EnsureShape()
        {
            VehicleTuning t = Tuning;
            if (t.HalfExtents == _shapeHalf && t.BodyClearance == _shapeClearance)
            {
                return;
            }

            _shapeHalf = t.HalfExtents;
            _shapeClearance = t.BodyClearance;
            Vec3 c, e;
            CollisionBoxLocal(out c, out e);
            float r = Math.Min(e.X, Math.Min(e.Y, e.Z));
            _bigRadius = r;
            int nx = Lattice(e.X - r, r), ny = Lattice(e.Y - r, r), nz = Lattice(e.Z - r, r);
            while (nx * ny * nz > MaxBigSpheres)
            {
                nz--;
            }

            _sphereCount = 0;
            for (int ix = 0; ix < nx; ix++)
            {
                for (int iy = 0; iy < ny; iy++)
                {
                    for (int iz = 0; iz < nz; iz++)
                    {
                        _sphereLocal[_sphereCount] = c + new Vec3(Spread(e.X - r, ix, nx), Spread(e.Y - r, iy, ny), Spread(e.Z - r, iz, nz));
                        _sphereRadius[_sphereCount++] = r;
                    }
                }
            }

            float cr = Math.Min(CornerRadius, r);
            for (int i = 0; i < 8; i++)
            {
                _sphereLocal[_sphereCount] = c + new Vec3(
                    (i & 1) == 0 ? -(e.X - cr) : e.X - cr,
                    (i & 2) == 0 ? -(e.Y - cr) : e.Y - cr,
                    (i & 4) == 0 ? -(e.Z - cr) : e.Z - cr);
                _sphereRadius[_sphereCount++] = cr;
            }
        }

        private static int Lattice(float inner, float r)
        {
            return inner < 1e-3f ? 1 : 1 + (int)Math.Ceiling(2f * inner / r);
        }

        private static float Spread(float inner, int i, int n)
        {
            return n == 1 ? 0f : -inner + 2f * inner * i / (n - 1);
        }

        private void CollisionBoxLocal(out Vec3 centre, out Vec3 half)
        {
            Vec3 e = Tuning.HalfExtents;
            float clearance = MathUtil.Clamp(Tuning.BodyClearance, 0f, e.Y);
            half = new Vec3(e.X, Math.Max(e.Y - 0.5f * clearance, 0.05f), e.Z);
            centre = new Vec3(0f, 0.5f * clearance, 0f);
        }

        private void CollisionBox(out Vec3 centre, out Vec3 half)
        {
            Vec3 local;
            CollisionBoxLocal(out local, out half);
            centre = Position + _rot * local;
        }

        private void ResetWheels()
        {
            Vec3 pos = Position;
            Vec3 up = _rot.Up;
            for (int i = 0; i < WheelCount; i++)
            {
                Vec3 centre = pos + _rot * Tuning.WheelMountLocal(i) - up * Tuning.SuspensionRestLength;
                _wheels[i] = new WheelState { Center = centre, GroundPoint = centre - up * Tuning.WheelRadius, GroundNormal = up };
                _spinRate[i] = 0f;
            }

            _grounded = 0;
        }

        private Vec3 PointVelocity(Vec3 r)
        {
            return _vel + Vec3.Cross(_ang, r);
        }

        private Vec3 InvInertia(Vec3 v)
        {
            Vec3 l = _rot.Inverse * v;
            Vec3 i = Tuning.Inertia;
            return _rot * new Vec3(l.X / i.X, l.Y / i.Y, l.Z / i.Z);
        }

        private float InvMassAt(Vec3 r, Vec3 dir)
        {
            return 1f / Tuning.Mass + Vec3.Dot(Vec3.Cross(InvInertia(Vec3.Cross(r, dir)), r), dir);
        }

        private void ApplyImpulse(Vec3 r, Vec3 j)
        {
            _vel += j / Tuning.Mass;
            _ang += InvInertia(Vec3.Cross(r, j));
        }

        private float Fifths(float speed)
        {
            return 5f * Math.Max(speed, 0f) / Tuning.TopSpeed;
        }

        private static float Fade(float fraction)
        {
            return MathUtil.Clamp01((1f - fraction) / DriveFadeFraction);
        }

        private static float SlipCurve(float x)
        {
            float a = Math.Abs(x);
            if (a < 1f) return x * (2f - a);
            return Math.Sign(x) * (1f - SlideFalloff * Math.Min(1f, (a - 1f) * 0.5f));
        }

        private static float Pedal(float v)
        {
            return float.IsNaN(v) || float.IsInfinity(v) ? 0f : MathUtil.Clamp01(v);
        }

        private static float Wrap360(float deg)
        {
            deg %= 360f;
            return deg < 0f ? deg + 360f : deg;
        }

        private static bool QuatFinite(Quat q)
        {
            float s = q.X + q.Y + q.Z + q.W;
            return !float.IsNaN(s) && !float.IsInfinity(s);
        }

        private static Vec3 Axis(Vec3 x, Vec3 y, Vec3 z, int i)
        {
            return i == 0 ? x : i == 1 ? y : z;
        }

        private static float Project(Vec3 e, Vec3 x, Vec3 y, Vec3 z, Vec3 axis)
        {
            return e.X * Math.Abs(Vec3.Dot(x, axis)) + e.Y * Math.Abs(Vec3.Dot(y, axis)) + e.Z * Math.Abs(Vec3.Dot(z, axis));
        }

        private static Vec3 Corner(Vec3 c, Vec3 e, Vec3 x, Vec3 y, Vec3 z, int i)
        {
            return c + x * ((i & 1) == 0 ? -e.X : e.X) + y * ((i & 2) == 0 ? -e.Y : e.Y) + z * ((i & 4) == 0 ? -e.Z : e.Z);
        }

        private static Vec3 Support(Vec3 c, Vec3 e, Vec3 x, Vec3 y, Vec3 z, Vec3 dir)
        {
            return c + x * (Vec3.Dot(x, dir) >= 0f ? e.X : -e.X) + y * (Vec3.Dot(y, dir) >= 0f ? e.Y : -e.Y) + z * (Vec3.Dot(z, dir) >= 0f ? e.Z : -e.Z);
        }

        private static bool Inside(Vec3 p, Vec3 c, Vec3 e, Vec3 x, Vec3 y, Vec3 z)
        {
            Vec3 d = p - c;
            const float tol = 1e-3f;
            return Math.Abs(Vec3.Dot(d, x)) <= e.X + tol && Math.Abs(Vec3.Dot(d, y)) <= e.Y + tol && Math.Abs(Vec3.Dot(d, z)) <= e.Z + tol;
        }
    }
}
