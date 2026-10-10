using System;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.Physics
{
    /// <summary>What the motor is doing.</summary>
    public enum MotorState
    {
        /// <summary>No ground under the feet (falling or jumping).</summary>
        Airborne = 0,

        /// <summary>Standing or walking on ground no steeper than <see cref="MotorSettings.MaxSlopeDeg"/>.</summary>
        Grounded = 1,

        /// <summary>On ground steeper than <see cref="MotorSettings.MaxSlopeDeg"/>, sliding down it.</summary>
        Sliding = 2,

        /// <summary>Performing a timed ledge climb; input is ignored.</summary>
        Climbing = 3,

        /// <summary>In water at least <see cref="MotorSettings.SwimDepth"/> deep at the feet.</summary>
        Swimming = 4,
    }

    /// <summary>One fixed step's intent, produced by the player controller or ped AI.</summary>
    public struct MotorInput
    {
        /// <summary>
        /// Wanted horizontal velocity in world space, m/s (y ignored). The walk/jog/sprint speed choice is the caller's;
        /// the motor only limits it while swimming.
        /// </summary>
        public Vec3 DesiredVelocity;

        /// <summary>Facing heading in degrees (0 = +z, 90 = +x); "ahead" for ledge detection.</summary>
        public float FacingYawDeg;

        /// <summary>Jump/climb pressed this step (an edge, not held).</summary>
        public bool Jump;
    }

    /// <summary>Events of one <see cref="CharacterMotor.Step"/>; all false/0 when nothing happened.</summary>
    public struct MotorStepResult
    {
        /// <summary>
        /// The motor went from <see cref="MotorState.Airborne"/> to <see cref="MotorState.Grounded"/> or
        /// <see cref="MotorState.Sliding"/> this step. Never set for entering water or for ground snapping while grounded.
        /// </summary>
        public bool Landed;

        /// <summary>
        /// When <see cref="Landed"/>: downward speed (m/s, ≥ 0) just before contact. Combat turns values above 8 m/s into
        /// damage. 0 otherwise.
        /// </summary>
        public float ImpactSpeed;

        /// <summary>A jump started this step.</summary>
        public bool Jumped;

        /// <summary>A ledge climb started this step.</summary>
        public bool ClimbStarted;

        /// <summary>A ledge climb finished this step (the feet are on the ledge and the state is Grounded).</summary>
        public bool ClimbFinished;

        /// <summary>The state became <see cref="MotorState.Swimming"/> this step.</summary>
        public bool EnteredWater;

        /// <summary>The state stopped being <see cref="MotorState.Swimming"/> this step.</summary>
        public bool LeftWater;
    }

    /// <summary>
    /// Kinematic vertical capsule for the player and peds, moved against a <see cref="CollisionWorld"/> at the fixed
    /// 60 Hz Game step. <see cref="Position"/> is the feet (the capsule's lowest point); the capsule spans
    /// <c>Position.y</c> to <c>Position.y + Settings.Height</c> with radius <c>Settings.Radius</c>.
    /// <para>Per <see cref="Step"/>, in order:</para>
    /// <list type="number">
    /// <item>Depenetration: a capsule that starts overlapping static geometry is pushed out along the contact normals;
    /// one overlapping dynamic shapes is pushed out of them (horizontally for capsules, along the box's least-penetration
    /// axis), so a car or citizen moving into the player shoves it.</item>
    /// <item>Climb in progress: the feet follow the climb path (up, then forward onto the ledge) over
    /// <see cref="MotorSettings.ClimbSeconds"/>; nothing else happens this step.</item>
    /// <item>Jump pressed while Grounded, Airborne or Swimming: if a ledge qualifies (below) a climb starts; otherwise, if
    /// Grounded, vertical velocity becomes <see cref="MotorSettings.JumpSpeed"/> and the state Airborne. A Jump while
    /// Sliding, or Airborne/Swimming with no ledge, does nothing.</item>
    /// <item>Velocity: Grounded moves horizontal velocity toward <see cref="MotorInput.DesiredVelocity"/> at
    /// <see cref="MotorSettings.GroundAcceleration"/> and follows the ground plane; Airborne at
    /// <c>AirControl × GroundAcceleration</c>, with gravity, downward speed capped at
    /// <see cref="MotorSettings.TerminalVelocity"/>; Sliding accelerates down the slope under gravity, input applies at
    /// air control with its uphill component removed; Swimming has no gravity, desired speed capped at
    /// <see cref="MotorSettings.SwimSpeed"/>, and the feet move toward <c>surface − SwimDepth</c> at up to
    /// <see cref="MotorSettings.FloatSpeed"/>.</item>
    /// <item>Collide-and-slide: the capsule is swept (continuously, so nothing is tunnelled at terminal velocity) and the
    /// remaining motion is projected onto each hit surface, at most <see cref="MotorSettings.MaxSlideIterations"/> times,
    /// keeping <see cref="MotorSettings.SkinWidth"/> from surfaces. Velocity loses its component into each hit. Grounded
    /// motion blocked by an obstacle whose top is at most <see cref="MotorSettings.StepHeight"/> above the feet, walkable,
    /// and with room for the capsule on top, steps up onto it instead. Ground steeper than MaxSlopeDeg blocks like a
    /// wall when walked into from walkable ground.</item>
    /// <item>Ground: if Grounded before the move, not jumping and no ground is touched, a downward sweep of up to
    /// <see cref="MotorSettings.GroundSnapDistance"/> snaps the feet to walkable ground; otherwise ground contact
    /// (normal within MaxSlopeDeg of up → Grounded, steeper → Sliding, none → Airborne) decides the state.</item>
    /// <item>Water: from <see cref="ICity.TryWaterSurface"/> at the feet's x, z; depth ≥ SwimDepth → Swimming (no landing
    /// event); a swimmer touching walkable ground where depth &lt; SwimDepth leaves the water as Grounded. Breath drains
    /// 1 s/s while Swimming down to 0 and refills at <see cref="MotorSettings.BreathRefillPerSecond"/> up to
    /// <see cref="MotorSettings.BreathSeconds"/> otherwise.</item>
    /// </list>
    /// <para>
    /// A ledge qualifies when, within <see cref="MotorSettings.LedgeReach"/> ahead of the capsule's front surface along
    /// <see cref="MotorInput.FacingYawDeg"/>, there is a walkable top surface between
    /// <see cref="MotorSettings.LedgeMinHeight"/> and <see cref="MotorSettings.LedgeMaxHeight"/> above the feet, the
    /// capsule fits standing on it with its axis at least Radius in from the ledge's front edge, and the space straight
    /// above the current capsule up to that height is clear (headroom). The climb ends with the feet on the ledge top.
    /// </para>
    /// <para>Allocates nothing per step. Not thread-safe.</para>
    /// </summary>
    public sealed class CharacterMotor
    {
        /// <summary>
        /// Creates a motor at the origin, Airborne, at rest, breath full. A null <paramref name="settings"/> uses a new
        /// <see cref="MotorSettings"/> with the defaults; otherwise the instance is kept (not copied).
        /// </summary>
        public CharacterMotor(MotorSettings settings)
        {
            throw new NotImplementedException();
        }

        /// <summary>The tuning in use.</summary>
        public MotorSettings Settings
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Which shapes the motor collides with; default <see cref="QueryMask.All"/>. A ped whose own capsule is in the
        /// world sets <c>QueryMask.All.Ignoring(itsTag)</c>.
        /// </summary>
        public QueryMask Mask
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        /// <summary>Feet position (capsule's lowest point), world space.</summary>
        public Vec3 Position
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Velocity in m/s, world space. Setting it (knockback, leaving a vehicle) takes effect next step; a positive y while
        /// Grounded makes the motor Airborne. Ignored while Climbing.
        /// </summary>
        public Vec3 Velocity
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        /// <summary>Current state.</summary>
        public MotorState State
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>True when Grounded or Sliding.</summary>
        public bool OnGround
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>Unit normal of the ground under the feet when <see cref="OnGround"/>; <see cref="Vec3.Up"/> otherwise.</summary>
        public Vec3 GroundNormal
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>Surface flags of the ground under the feet when <see cref="OnGround"/>; <see cref="SurfaceFlags.None"/> otherwise or on a dynamic shape.</summary>
        public SurfaceFlags GroundFlags
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>Fraction of the current ledge climb done, in [0, 1]; 0 when not Climbing.</summary>
        public float ClimbProgress
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>Breath left in seconds, in [0, <see cref="MotorSettings.BreathSeconds"/>].</summary>
        public float Breath
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// True while Swimming with no breath left. The motor deals no damage; the health owner applies drowning damage
        /// for every step this is true.
        /// </summary>
        public bool IsDrowning
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Places the feet at <paramref name="feet"/> with zero velocity, state Airborne, any climb cancelled and breath
        /// full. Does not test for overlap; the next step depenetrates and finds the ground (which may report a landing
        /// with ImpactSpeed ≈ 0).
        /// </summary>
        public void Teleport(Vec3 feet)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Advances the motor by <paramref name="dt"/> seconds (normally 1/60) against <paramref name="world"/>.
        /// <paramref name="city"/> supplies water surfaces; null means no water anywhere. A dt that is ≤ 0 or non-finite
        /// changes nothing and returns <c>default</c>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="world"/> is null.</exception>
        public MotorStepResult Step(float dt, MotorInput input, CollisionWorld world, ICity city)
        {
            throw new NotImplementedException();
        }
    }
}
