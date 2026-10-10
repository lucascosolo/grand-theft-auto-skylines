using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Police
{
    /// <summary>
    /// The police helicopter (4★+ when <c>ICity.FindVehicleModel(VehicleKind.PoliceHelicopter, seed)</c> ≥ 0): a
    /// kinematic flyer, not a G5 body and not in the collision world. The Mod draws <see cref="ModelId"/> at
    /// <see cref="Position"/>/<see cref="YawDeg"/> and a spotlight cone toward <see cref="SpotlightTarget"/>.
    /// Motion: spawns <see cref="SpawnDistance"/> from the player at <see cref="HoverHeight"/> above the ground; flies
    /// toward (target.x, ground(target) + 40, target.z) at up to <see cref="MaxSpeed"/>, accelerating at
    /// <see cref="Acceleration"/>, slowing within 30 m to hover; target = the player while seen, else the last known
    /// position; yaw turns toward the velocity at 60°/s. Leaves (flies away and despawns at 500 m) when the stars clear.
    /// </summary>
    public sealed class PoliceHelicopter
    {
        /// <summary>Hover height above the ground under the target, metres.</summary>
        public const float HoverHeight = 40f;
        /// <summary>Top speed, m/s.</summary>
        public const float MaxSpeed = 25f;
        /// <summary>Acceleration, m/s².</summary>
        public const float Acceleration = 6f;
        /// <summary>Spawn distance from the player, metres.</summary>
        public const float SpawnDistance = 350f;

        /// <summary>True while flying.</summary>
        public bool Active;
        /// <summary>Vehicle model id, −1 none.</summary>
        public int ModelId = -1;
        /// <summary>Position, world space.</summary>
        public Vec3 Position;
        /// <summary>Velocity, m/s.</summary>
        public Vec3 Velocity;
        /// <summary>Yaw, degrees.</summary>
        public float YawDeg;
        /// <summary>Spotlight on (night, or always while searching).</summary>
        public bool Spotlight;
        /// <summary>Where the spotlight points (ground point).</summary>
        public Vec3 SpotlightTarget;
        /// <summary>True while leaving.</summary>
        public bool Leaving;

        /// <summary>Starts at a spawn point.</summary>
        public void Spawn(GameWorld world, int modelId, Vec3 position, float yawDeg)
        {
            Active = true;
            Leaving = false;
            ModelId = modelId;
            Position = position;
            Velocity = Vec3.Zero;
            YawDeg = yawDeg;
            Spotlight = true;
            SpotlightTarget = position;
        }

        /// <summary>Advances the flight toward <paramref name="target"/> (see class).</summary>
        public void Step(GameWorld world, Vec3 target, float dt)
        {
            if (!Active || dt <= 0f) return;
            Vec3 goal;
            if (Leaving)
            {
                Vec3 away = (Position - target).Flat;
                away = away.LengthSquared > 1e-4f ? away.Normalized : Vec3.Forward;
                goal = Position + away * 200f;
            }
            else
            {
                goal = new Vec3(target.X, world.City.GroundHeight(target.X, target.Z) + HoverHeight, target.Z);
            }
            Vec3 to = goal - Position;
            float dist = to.Length;
            float speed = dist < 30f ? MaxSpeed * dist / 30f : MaxSpeed;
            Vec3 desired = dist > 1e-3f ? to * (speed / dist) : Vec3.Zero;
            Vec3 dv = desired - Velocity;
            float maxDv = Acceleration * dt;
            if (dv.Length > maxDv) dv = dv.Normalized * maxDv;
            Velocity += dv;
            Position += Velocity * dt;
            if (Velocity.LengthXZ > 1f) YawDeg = MathUtil.MoveTowardsAngleDeg(YawDeg, Vec3.YawDegOf(Velocity), 60f * dt);
            Spotlight = !Leaving;
            SpotlightTarget = new Vec3(target.X, world.City.GroundHeight(target.X, target.Z), target.Z);
        }
    }
}
