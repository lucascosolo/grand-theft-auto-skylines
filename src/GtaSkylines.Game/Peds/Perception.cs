using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Peds
{
    /// <summary>A person who could have seen an event (<see cref="Perception.WitnessesOf"/>).</summary>
    public struct WitnessCandidate
    {
        /// <summary>WorldTag: a Game ped tag, or an ambient citizen tag.</summary>
        public int Tag;
        /// <summary>The Game ped, or null for an ambient citizen.</summary>
        public Ped Ped;
        /// <summary>The snapshot entry when <see cref="Ped"/> is null (pass it to <c>PromoteCitizen</c> to make a caller).</summary>
        public AmbientCitizen Ambient;
        /// <summary>CS1 citizen id (0 for a Game ped without one).</summary>
        public uint Citizen;
        /// <summary>Feet position.</summary>
        public Vec3 Position;
        /// <summary>Eye-to-point distance, metres.</summary>
        public float Distance;
    }

    /// <summary>
    /// What civilians notice (G7 contract "Perception"; G8 builds witnesses on it). Pure queries over the world:
    /// no state, no allocation (the caller owns the output list), no randomness.
    /// <para>
    /// Sight: the eye is feet + <see cref="EyeHeight"/>; the cone is <see cref="ViewConeDeg"/> wide (a point is inside when
    /// the horizontal angle between the facing yaw and the direction to it is ≤ 60°; a point within
    /// <see cref="NearSenseRange"/> is always inside); the range is <see cref="SightRangeDay"/> by day and
    /// <see cref="SightRangeNight"/> by night (<c>Clock.IsNight</c>); line of sight is a static-only raycast from the eye to
    /// the point (<c>QueryMask.StaticOnly</c>), blocked by any hit closer than the point minus 0.05 m. Interior walls are
    /// static triangles, so rooms occlude.
    /// </para>
    /// </summary>
    public static class Perception
    {
        /// <summary>Eye height above the feet, metres (1.6).</summary>
        public const float EyeHeight = 1.6f;
        /// <summary>Full view cone, degrees (120).</summary>
        public const float ViewConeDeg = 120f;
        /// <summary>Civilian sight range by day, metres (40).</summary>
        public const float SightRangeDay = 40f;
        /// <summary>Civilian sight range at night, metres (25).</summary>
        public const float SightRangeNight = 25f;
        /// <summary>Within this distance (m) the cone is ignored (people notice what happens right next to them) (2).</summary>
        public const float NearSenseRange = 2f;
        /// <summary>Range multiplier when the observed actor is the sneaking player (0.5).</summary>
        public const float SneakRangeFactor = 0.5f;
        /// <summary>Gunshots are heard within this radius, metres (60).</summary>
        public const float GunshotHearingRadius = 60f;
        /// <summary>Explosions are heard within this radius, metres (100).</summary>
        public const float ExplosionHearingRadius = 100f;
        /// <summary>The point looked at on a person: feet + this height (1.2, chest).</summary>
        public const float TargetHeight = 1.2f;

        /// <summary>Sight range now: 40 m by day, 25 m at night.</summary>
        public static float SightRange(GameWorld world)
        {
            return world.Clock.IsNight ? SightRangeNight : SightRangeDay;
        }

        /// <summary>
        /// True when <paramref name="ped"/> can see <paramref name="point"/> with the civilian range (see the class),
        /// facing its <c>YawDeg</c>. Dead and KnockedDown peds see nothing. An InVehicle ped sees from its car: the eye is
        /// the car's bounds centre + (0, 0.5, 0) and the facing is the car's yaw.
        /// </summary>
        public static bool CanSee(GameWorld world, Ped ped, Vec3 point)
        {
            Vec3 eye;
            float yaw;
            if (!EyeOf(world, ped, out eye, out yaw)) return false;
            return CanSee(world, eye, yaw, SightRange(world), point);
        }

        /// <summary>
        /// The general sight test: eye at <paramref name="eye"/>, facing <paramref name="yawDeg"/>, range
        /// <paramref name="range"/>, cone and occlusion as on the class. G8's cop recognition uses it with its own range.
        /// </summary>
        public static bool CanSee(GameWorld world, Vec3 eye, float yawDeg, float range, Vec3 point)
        {
            Vec3 to = point - eye;
            float flat = to.LengthXZ;
            if (flat > range) return false;
            if (flat > NearSenseRange && Math.Abs(MathUtil.DeltaAngleDeg(yawDeg, Vec3.YawDegOf(to))) > ViewConeDeg * 0.5f) return false;
            float len = to.Length;
            if (len < 1e-3f) return true;
            PhysicsHit hit;
            return !world.Collision.Raycast(eye, to, len, QueryMask.StaticOnly, out hit) || hit.Distance >= len - 0.05f;
        }

        /// <summary>
        /// True when <paramref name="ped"/> (alive) hears <paramref name="e"/>: Gunshot within its Value (or
        /// <see cref="GunshotHearingRadius"/> when Value ≤ 0) of Position; Explosion within
        /// <see cref="ExplosionHearingRadius"/>; any other type → false. Distance is 3D from the ped's feet. Walls do not
        /// block sound. Dead peds hear nothing.
        /// </summary>
        public static bool Hears(GameWorld world, Ped ped, GameEvent e)
        {
            if (ped == null || ped.IsDead) return false;
            float radius;
            if (e.Type == GameEventType.Gunshot) radius = e.Value > 0f ? e.Value : GunshotHearingRadius;
            else if (e.Type == GameEventType.Explosion) radius = ExplosionHearingRadius;
            else return false;
            return Vec3.Distance(ped.Position, e.Position) <= radius;
        }

        /// <summary>
        /// Clears <paramref name="into"/> and fills it with every civilian who could see <paramref name="e"/>, nearest
        /// first (ties: lower tag); returns the count.
        /// <para>Seen point: <c>e.Position</c> + (0, <see cref="TargetHeight"/>, 0). Range: <see cref="SightRange"/>,
        /// × <see cref="SneakRangeFactor"/> when ActorA is the player and the player is Sneaking.</para>
        /// <para>Candidates: Game peds of kind Civilian or Npc that are alive, not KnockedDown, not Invisible, not
        /// Arrested, and are neither ActorA (the perpetrator) nor the player (the victim ActorB may witness); InVehicle
        /// peds see from their car. Ambient snapshot citizens (facing their YawDeg) unless inside an interior, minus those
        /// already promoted. Each passes <see cref="CanSee(GameWorld, Vec3, float, float, Vec3)"/>. Cops are excluded (G8
        /// handles them).</para>
        /// </summary>
        public static int WitnessesOf(GameWorld world, GameEvent e, List<WitnessCandidate> into)
        {
            into.Clear();
            Vec3 point = e.Position + new Vec3(0f, TargetHeight, 0f);
            float range = SightRange(world);
            Ped player = world.Player;
            if (player != null && e.ActorA == player.Tag && player.Has(PedFlags.Sneaking)) range *= SneakRangeFactor;
            IList<Ped> peds = world.Peds;
            for (int i = 0; i < peds.Count; i++)
            {
                Ped p = peds[i];
                if (p == player || (p.Kind != PedKind.Civilian && p.Kind != PedKind.Npc) || p.Tag == e.ActorA) continue;
                if (p.Has(PedFlags.Invisible) || p.Has(PedFlags.Arrested)) continue;
                Vec3 eye;
                float yaw;
                if (!EyeOf(world, p, out eye, out yaw) || !CanSee(world, eye, yaw, range, point)) continue;
                Insert(into, new WitnessCandidate { Tag = p.Tag, Ped = p, Citizen = p.Citizen, Position = p.Position, Distance = Vec3.Distance(eye, point) });
            }
            if (!world.Interior.IsInside)
            {
                List<AmbientCitizen> cs = world.Ambient.Citizens;
                for (int i = 0; i < cs.Count; i++)
                {
                    AmbientCitizen c = cs[i];
                    int tag = WorldTag.ForAmbientCitizen(c.Instance);
                    if (tag == e.ActorA || world.FindPedByCitizen(c.Citizen) != null) continue;
                    Vec3 eye = c.Position + new Vec3(0f, EyeHeight, 0f);
                    if (!CanSee(world, eye, c.YawDeg, range, point)) continue;
                    Insert(into, new WitnessCandidate { Tag = tag, Ambient = c, Citizen = c.Citizen, Position = c.Position, Distance = Vec3.Distance(eye, point) });
                }
            }
            return into.Count;
        }

        private static bool EyeOf(GameWorld world, Ped ped, out Vec3 eye, out float yaw)
        {
            eye = Vec3.Zero;
            yaw = ped.YawDeg;
            if (ped.IsDead || ped.Has(PedFlags.KnockedDown)) return false;
            if (ped.Has(PedFlags.InVehicle))
            {
                Car car = world.FindCar(ped.VehicleId);
                if (car == null) return false;
                eye = car.Position + new Vec3(0f, 0.5f, 0f);
                yaw = car.Rotation.YawDeg;
                return true;
            }
            eye = ped.Position + new Vec3(0f, EyeHeight, 0f);
            return true;
        }

        private static void Insert(List<WitnessCandidate> into, WitnessCandidate w)
        {
            int i = into.Count;
            while (i > 0 && (into[i - 1].Distance > w.Distance || (into[i - 1].Distance == w.Distance && into[i - 1].Tag > w.Tag))) i--;
            into.Insert(i, w);
        }
    }
}
