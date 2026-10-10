using System;
using System.Collections.Generic;
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
        private const float WaterDrag = 20f;
        private const float FloatGain = 4f;
        private const float SwimExitMargin = 0.3f;
        private const float ClimbRisePart = 0.6f;
        private const float LedgeSampleStep = 0.05f;
        private const float PushMargin = 1e-3f;

        private static readonly Vec3 Down = new Vec3(0f, -1f, 0f);

        private readonly MotorSettings _s;
        private readonly List<Contact> _contacts = new List<Contact>(32);
        private QueryMask _mask;
        private Vec3 _pos, _vel;
        private MotorState _state;
        private Vec3 _groundNormal = Vec3.Up;
        private SurfaceFlags _groundFlags;
        private float _breath;
        private float _waterY;
        private float _climbT;
        private Vec3 _climbStart, _climbEnd, _climbNormal;
        private SurfaceFlags _climbFlags;

        /// <summary>
        /// Creates a motor at the origin, Airborne, at rest, breath full. A null <paramref name="settings"/> uses a new
        /// <see cref="MotorSettings"/> with the defaults; otherwise the instance is kept (not copied).
        /// </summary>
        public CharacterMotor(MotorSettings settings)
        {
            _s = settings ?? new MotorSettings();
            _breath = _s.BreathSeconds;
        }

        /// <summary>The tuning in use.</summary>
        public MotorSettings Settings
        {
            get { return _s; }
        }

        /// <summary>
        /// Which shapes the motor collides with; default <see cref="QueryMask.All"/>. A ped whose own capsule is in the
        /// world sets <c>QueryMask.All.Ignoring(itsTag)</c>.
        /// </summary>
        public QueryMask Mask
        {
            get { return _mask; }
            set { _mask = value; }
        }

        /// <summary>Feet position (capsule's lowest point), world space.</summary>
        public Vec3 Position
        {
            get { return _pos; }
        }

        /// <summary>
        /// Velocity in m/s, world space. Setting it (knockback, leaving a vehicle) takes effect next step; a positive y while
        /// Grounded makes the motor Airborne. Ignored while Climbing.
        /// </summary>
        public Vec3 Velocity
        {
            get { return _vel; }
            set
            {
                if (_state != MotorState.Climbing && value.IsFinite) _vel = value;
            }
        }

        /// <summary>Current state.</summary>
        public MotorState State
        {
            get { return _state; }
        }

        /// <summary>True when Grounded or Sliding.</summary>
        public bool OnGround
        {
            get { return _state == MotorState.Grounded || _state == MotorState.Sliding; }
        }

        /// <summary>Unit normal of the ground under the feet when <see cref="OnGround"/>; <see cref="Vec3.Up"/> otherwise.</summary>
        public Vec3 GroundNormal
        {
            get { return OnGround ? _groundNormal : Vec3.Up; }
        }

        /// <summary>Surface flags of the ground under the feet when <see cref="OnGround"/>; <see cref="SurfaceFlags.None"/> otherwise or on a dynamic shape.</summary>
        public SurfaceFlags GroundFlags
        {
            get { return OnGround ? _groundFlags : SurfaceFlags.None; }
        }

        /// <summary>Fraction of the current ledge climb done, in [0, 1]; 0 when not Climbing.</summary>
        public float ClimbProgress
        {
            get { return _state == MotorState.Climbing ? _climbT : 0f; }
        }

        /// <summary>Breath left in seconds, in [0, <see cref="MotorSettings.BreathSeconds"/>].</summary>
        public float Breath
        {
            get { return _breath; }
        }

        /// <summary>
        /// True while Swimming with no breath left. The motor deals no damage; the health owner applies drowning damage
        /// for every step this is true.
        /// </summary>
        public bool IsDrowning
        {
            get { return _state == MotorState.Swimming && _breath <= 0f; }
        }

        /// <summary>
        /// Places the feet at <paramref name="feet"/> with zero velocity, state Airborne, any climb cancelled and breath
        /// full. Does not test for overlap; the next step depenetrates and finds the ground (which may report a landing
        /// with ImpactSpeed ≈ 0).
        /// </summary>
        public void Teleport(Vec3 feet)
        {
            _pos = feet;
            _vel = Vec3.Zero;
            _state = MotorState.Airborne;
            _climbT = 0f;
            _breath = _s.BreathSeconds;
            _groundNormal = Vec3.Up;
            _groundFlags = SurfaceFlags.None;
        }

        /// <summary>
        /// Advances the motor by <paramref name="dt"/> seconds (normally 1/60) against <paramref name="world"/>.
        /// <paramref name="city"/> supplies water surfaces; null means no water anywhere. A dt that is ≤ 0 or non-finite
        /// changes nothing and returns <c>default</c>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="world"/> is null.</exception>
        public MotorStepResult Step(float dt, MotorInput input, CollisionWorld world, ICity city)
        {
            if (world == null) throw new ArgumentNullException("world");
            var res = new MotorStepResult();
            if (!(dt > 0f) || float.IsInfinity(dt)) return res;

            if (_state == MotorState.Climbing)
            {
                AdvanceClimb(dt, ref res);
                UpdateBreath(dt);
                return res;
            }

            Depenetrate(world);
            bool wasSwimming = _state == MotorState.Swimming;
            if (_state == MotorState.Grounded && _vel.Y > 0f) _state = MotorState.Airborne;

            if (input.Jump && _state != MotorState.Sliding)
            {
                if (TryStartClimb(world, input.FacingYawDeg))
                {
                    res.ClimbStarted = true;
                    res.LeftWater = wasSwimming;
                    UpdateBreath(dt);
                    return res;
                }
                if (_state == MotorState.Grounded)
                {
                    _vel = new Vec3(_vel.X, _s.JumpSpeed, _vel.Z);
                    _state = MotorState.Airborne;
                    res.Jumped = true;
                }
            }

            bool snap = _state == MotorState.Grounded || _state == MotorState.Sliding;
            UpdateVelocity(dt, input.DesiredVelocity);
            float vyBefore = _vel.Y;
            CollideAndSlide(world, Displacement(dt));
            ResolveState(world, city, snap, vyBefore, wasSwimming, ref res);
            UpdateBreath(dt);
            return res;
        }

        private float CosMaxSlope
        {
            get { return (float)Math.Cos(_s.MaxSlopeDeg * MathUtil.Deg2Rad); }
        }

        private static Vec3 MoveTowards(Vec3 current, Vec3 target, float maxDelta)
        {
            Vec3 delta = target - current;
            float dist = delta.Length;
            return dist <= maxDelta || dist < 1e-9f ? target : current + delta * (maxDelta / dist);
        }

        private void UpdateBreath(float dt)
        {
            _breath = _state == MotorState.Swimming
                ? Math.Max(0f, _breath - dt)
                : Math.Min(_s.BreathSeconds, _breath + _s.BreathRefillPerSecond * dt);
        }

        private void Depenetrate(CollisionWorld world)
        {
            for (int it = 0; it < 4; it++)
            {
                if (world.OverlapCapsule(_pos, _s.Radius, _s.Height, _mask, _contacts) == 0) return;
                Contact deepest = _contacts[0];
                for (int i = 1; i < _contacts.Count; i++)
                {
                    if (_contacts[i].Depth > deepest.Depth) deepest = _contacts[i];
                }
                _pos += deepest.Normal * (deepest.Depth + PushMargin);
                float vn = Vec3.Dot(_vel, deepest.Normal);
                if (vn < 0f) _vel -= deepest.Normal * vn;
            }
        }

        private void UpdateVelocity(float dt, Vec3 desired)
        {
            Vec3 want = desired.IsFinite ? desired.Flat : Vec3.Zero;
            float accel = _s.GroundAcceleration * dt;
            switch (_state)
            {
                case MotorState.Grounded:
                    _vel = MoveTowards(_vel.Flat, want, accel);
                    break;
                case MotorState.Airborne:
                {
                    Vec3 h = MoveTowards(_vel.Flat, want, accel * _s.AirControl);
                    float vy = Math.Max(_vel.Y - _s.Gravity * dt, -_s.TerminalVelocity);
                    _vel = new Vec3(h.X, vy, h.Z);
                    break;
                }
                case MotorState.Sliding:
                {
                    Vec3 n = _groundNormal;
                    Vec3 v = _vel + Vec3.ProjectOnPlane(new Vec3(0f, -_s.Gravity, 0f), n) * dt;
                    Vec3 dv = Vec3.ClampLength(want - v.Flat, accel * _s.AirControl);
                    Vec3 downhill = n.Flat.Normalized;
                    float along = Vec3.Dot(dv, downhill);
                    if (along < 0f) dv -= downhill * along;
                    v = Vec3.ProjectOnPlane(v + dv, n);
                    if (v.Y < -_s.TerminalVelocity) v = v * (_s.TerminalVelocity / -v.Y);
                    _vel = v;
                    break;
                }
                case MotorState.Swimming:
                {
                    Vec3 h = MoveTowards(_vel.Flat, Vec3.ClampLength(want, _s.SwimSpeed), accel);
                    float target = MathUtil.Clamp((_waterY - _s.SwimDepth - _pos.Y) * FloatGain, -_s.FloatSpeed, _s.FloatSpeed);
                    float vy = MathUtil.MoveTowards(_vel.Y, target, WaterDrag * dt);
                    _vel = new Vec3(h.X, vy, h.Z);
                    break;
                }
            }
        }

        /// <summary>This step's intended motion; grounded motion follows the ground plane keeping its horizontal part.</summary>
        private Vec3 Displacement(float dt)
        {
            Vec3 d = _vel * dt;
            if (_state == MotorState.Grounded) d = AlongPlane(d, _groundNormal);
            return d;
        }

        private static Vec3 AlongPlane(Vec3 d, Vec3 n)
        {
            return n.Y > 1e-3f ? new Vec3(d.X, -(n.X * d.X + n.Z * d.Z) / n.Y, d.Z) : d;
        }

        private void CollideAndSlide(CollisionWorld world, Vec3 d)
        {
            bool grounded = _state == MotorState.Grounded;
            float cosMax = CosMaxSlope, skin = _s.SkinWidth;
            Vec3 prevN = Vec3.Zero;
            bool hasPrev = false;
            for (int i = 0; i < _s.MaxSlideIterations; i++)
            {
                float len = d.Length;
                if (len < 1e-6f) return;
                Vec3 dir = d / len;
                PhysicsHit hit;
                if (!world.CapsuleCast(_pos, _s.Radius, _s.Height, dir, len, _mask, out hit))
                {
                    _pos += d;
                    return;
                }
                float adv = Math.Max(0f, hit.Distance - skin);
                _pos += dir * adv;
                Vec3 rem = dir * (len - adv);
                Vec3 n = hit.Normal;
                bool walkable = n.Y >= cosMax;

                if (grounded && walkable)
                {
                    d = AlongPlane(rem, n);
                    continue;
                }
                if (grounded)
                {
                    Vec3 flat = rem.Flat;
                    if (n.Y > -0.5f && flat.LengthSquared > 1e-10f && TryStepUp(world, flat)) return;
                    Vec3 nh = n.Flat;
                    if (nh.LengthSquared > 1e-8f) n = nh.Normalized;
                }
                rem -= n * Vec3.Dot(rem, n);
                float vn = Vec3.Dot(_vel, n);
                if (vn < 0f) _vel -= n * vn;
                if (hasPrev && Vec3.Dot(rem, prevN) < -1e-7f)
                {
                    Vec3 crease = Vec3.Cross(prevN, n);
                    float cl = crease.Length;
                    if (cl < 1e-4f)
                    {
                        rem = Vec3.Zero;
                    }
                    else
                    {
                        crease /= cl;
                        rem = crease * Vec3.Dot(rem, crease);
                        _vel = crease * Vec3.Dot(_vel, crease);
                    }
                }
                prevN = n;
                hasPrev = true;
                d = rem;
            }
        }

        /// <summary>Lifts the capsule by up to StepHeight, moves it along <paramref name="move"/>, and puts it down on supporting ground.</summary>
        private bool TryStepUp(CollisionWorld world, Vec3 move)
        {
            float r = _s.Radius, h = _s.Height, skin = _s.SkinWidth;
            PhysicsHit hit;
            float up = world.CapsuleCast(_pos, r, h, Vec3.Up, _s.StepHeight + skin, _mask, out hit) ? Math.Max(0f, hit.Distance - skin) : _s.StepHeight;
            if (up < 0.02f) return false;
            Vec3 p = new Vec3(_pos.X, _pos.Y + up, _pos.Z);
            float len = move.Length;
            Vec3 dir = move / len;
            if (world.CapsuleCast(p, r, h, dir, len, _mask, out hit))
            {
                float adv = Math.Max(0f, hit.Distance - skin);
                if (adv < 1e-4f) return false;
                p += dir * adv;
            }
            else
            {
                p += move;
            }
            if (!world.CapsuleCast(p, r, h, Down, up + 2f * skin, _mask, out hit)) return false;
            if (hit.Point.Y - _pos.Y > _s.StepHeight + 1e-3f) return false;
            Vec3 gn;
            SurfaceFlags gf;
            if (!Supports(world, hit, out gn, out gf)) return false;
            p.Y -= Math.Max(0f, hit.Distance - skin);
            if (p.Y <= _pos.Y + 1e-3f) return false;
            _pos = p;
            return true;
        }

        /// <summary>
        /// True when a downward contact can be stood on: a walkable contact normal, or the edge of a walkable face (the
        /// face just inside the contact point).
        /// </summary>
        private bool Supports(CollisionWorld world, PhysicsHit hit, out Vec3 normal, out SurfaceFlags flags)
        {
            float cosMax = CosMaxSlope;
            normal = hit.Normal;
            flags = hit.Flags;
            if (hit.Normal.Y >= cosMax) return true;
            Vec3 inward = new Vec3(-hit.Normal.X, 0f, -hit.Normal.Z);
            float l = inward.Length;
            if (l < 1e-4f) return false;
            Vec3 o = hit.Point + inward * (0.02f / l) + new Vec3(0f, 0.1f, 0f);
            PhysicsHit ray;
            if (!world.Raycast(o, Down, 0.2f, _mask, out ray) || ray.Normal.Y < cosMax) return false;
            normal = ray.Normal;
            flags = ray.Flags;
            return true;
        }

        private void ResolveState(CollisionWorld world, ICity city, bool snap, float vyBefore, bool wasSwimming, ref MotorStepResult res)
        {
            float waterY = 0f;
            bool hasWater = city != null && city.TryWaterSurface(_pos.X, _pos.Z, out waterY);
            float swimDepth = wasSwimming ? _s.SwimDepth - SwimExitMargin : _s.SwimDepth;
            if (hasWater && waterY - _pos.Y >= swimDepth)
            {
                res.EnteredWater = !wasSwimming;
                _state = MotorState.Swimming;
                _waterY = waterY;
                return;
            }
            res.LeftWater = wasSwimming;
            if (wasSwimming) _state = MotorState.Airborne;

            if (_vel.Y > 1e-3f)
            {
                _state = MotorState.Airborne;
                return;
            }
            float skin = _s.SkinWidth;
            PhysicsHit hit;
            if (!world.CapsuleCast(_pos, _s.Radius, _s.Height, Down, snap ? _s.GroundSnapDistance + skin : 2f * skin, _mask, out hit))
            {
                _state = MotorState.Airborne;
                return;
            }
            Vec3 gn;
            SurfaceFlags gf;
            bool walkable = Supports(world, hit, out gn, out gf);
            _pos.Y -= Math.Max(0f, hit.Distance - skin);
            if (_state == MotorState.Airborne && !wasSwimming)
            {
                res.Landed = true;
                res.ImpactSpeed = Math.Max(0f, -vyBefore);
            }
            _groundNormal = gn;
            _groundFlags = gf;
            if (walkable)
            {
                _state = MotorState.Grounded;
                _vel = _vel.Flat;
            }
            else
            {
                _state = MotorState.Sliding;
                _vel = Vec3.ProjectOnPlane(_vel, gn);
            }
        }

        /// <summary>Starts a climb when a ledge qualifies (see the class remarks).</summary>
        private bool TryStartClimb(CollisionWorld world, float yawDeg)
        {
            float r = _s.Radius, h = _s.Height, skin = _s.SkinWidth, cosMax = CosMaxSlope;
            Vec3 fwd = Vec3.FromYawDeg(yawDeg);
            float top = _pos.Y + _s.LedgeMaxHeight + 0.05f;
            float rayLen = _s.LedgeMaxHeight - _s.LedgeMinHeight + 0.05f;
            int samples = (int)Math.Floor(_s.LedgeReach / LedgeSampleStep + 1e-3f);
            PhysicsHit hit;
            for (int i = 0; i <= samples; i++)
            {
                float dist = r + i * LedgeSampleStep;
                if (!world.Raycast(new Vec3(_pos.X + fwd.X * dist, top, _pos.Z + fwd.Z * dist), Down, rayLen, _mask, out hit) || hit.Normal.Y < cosMax) continue;

                float reach = dist + r + 0.02f;
                PhysicsHit ground;
                if (!world.Raycast(new Vec3(_pos.X + fwd.X * reach, top, _pos.Z + fwd.Z * reach), Down, rayLen, _mask, out ground) || ground.Normal.Y < cosMax) return false;
                Vec3 end = new Vec3(_pos.X + fwd.X * reach, ground.Point.Y + skin, _pos.Z + fwd.Z * reach);
                if (world.CapsuleCast(_pos, r, h, Vec3.Up, end.Y - _pos.Y, _mask, out hit)) return false;
                Vec3 raised = new Vec3(_pos.X, end.Y, _pos.Z);
                Vec3 across = end - raised;
                float al = across.Length;
                if (al > 1e-5f && world.CapsuleCast(raised, r, h, across / al, al, _mask, out hit)) return false;
                if (world.CapsuleCast(end, r, h, Vec3.Zero, 0f, _mask, out hit)) return false;

                _climbStart = _pos;
                _climbEnd = end;
                _climbNormal = ground.Normal;
                _climbFlags = ground.Flags;
                _climbT = 0f;
                _vel = Vec3.Zero;
                _state = MotorState.Climbing;
                return true;
            }
            return false;
        }

        private void AdvanceClimb(float dt, ref MotorStepResult res)
        {
            _climbT += dt / Math.Max(1e-3f, _s.ClimbSeconds);
            if (_climbT >= 1f - 1e-4f)
            {
                _pos = _climbEnd;
                _climbT = 0f;
                _state = MotorState.Grounded;
                _groundNormal = _climbNormal;
                _groundFlags = _climbFlags;
                res.ClimbFinished = true;
                return;
            }
            if (_climbT < ClimbRisePart)
            {
                _pos = new Vec3(_climbStart.X, MathUtil.Lerp(_climbStart.Y, _climbEnd.Y, _climbT / ClimbRisePart), _climbStart.Z);
            }
            else
            {
                float k = (_climbT - ClimbRisePart) / (1f - ClimbRisePart);
                _pos = new Vec3(MathUtil.Lerp(_climbStart.X, _climbEnd.X, k), _climbEnd.Y, MathUtil.Lerp(_climbStart.Z, _climbEnd.Z, k));
            }
        }
    }
}
