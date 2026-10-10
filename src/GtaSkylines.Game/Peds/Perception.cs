using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// True when <paramref name="ped"/> can see <paramref name="point"/> with the civilian range (see the class),
        /// facing its <c>YawDeg</c>. Dead and KnockedDown peds see nothing. An InVehicle ped sees from its car: the eye is
        /// the car's bounds centre + (0, 0.5, 0) and the facing is the car's yaw.
        /// </summary>
        public static bool CanSee(GameWorld world, Ped ped, Vec3 point)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// The general sight test: eye at <paramref name="eye"/>, facing <paramref name="yawDeg"/>, range
        /// <paramref name="range"/>, cone and occlusion as on the class. G8's cop recognition uses it with its own range.
        /// </summary>
        public static bool CanSee(GameWorld world, Vec3 eye, float yawDeg, float range, Vec3 point)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// True when <paramref name="ped"/> (alive) hears <paramref name="e"/>: Gunshot within its Value (or
        /// <see cref="GunshotHearingRadius"/> when Value ≤ 0) of Position; Explosion within
        /// <see cref="ExplosionHearingRadius"/>; any other type → false. Distance is 3D from the ped's feet. Walls do not
        /// block sound. Dead peds hear nothing.
        /// </summary>
        public static bool Hears(GameWorld world, Ped ped, GameEvent e)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }
    }
}
