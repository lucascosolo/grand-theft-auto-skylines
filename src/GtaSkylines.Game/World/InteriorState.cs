using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.World
{
    /// <summary>Where the player is in an interior transition.</summary>
    public enum InteriorPhase
    {
        /// <summary>Outside, no transition.</summary>
        Outside,
        /// <summary>Fading to black before going in (<see cref="InteriorState.FadeSeconds"/>).</summary>
        FadingOut,
        /// <summary>Inside, fading in from black.</summary>
        FadingIn,
        /// <summary>Inside, no transition.</summary>
        Inside,
        /// <summary>Fading to black before going out (or riding the elevator).</summary>
        FadingOutToExit,
        /// <summary>Back outside, fading in.</summary>
        FadingInOutside,
    }

    /// <summary>An interaction a system added to the current interior (G9's evidence locker).</summary>
    public struct CustomInteraction
    {
        /// <summary>Caller-chosen id, reported as the Value of <see cref="GameEventType.CustomInteraction"/>.</summary>
        public int Id;
        /// <summary>Interior-local floor position (the player must be within 1.2 m).</summary>
        public Vec3 LocalPosition;
        /// <summary>Help prompt, e.g. "Press {Interact} to take the evidence".</summary>
        public string Prompt;
    }

    /// <summary>
    /// The one loaded interior. G6 writes it; the renderer, G7 (occupant brains, nav grid), G8 (cops entering) and G9
    /// read it.
    /// <para>
    /// Placement: an interior of building B is placed at <see cref="Origin"/> = (B.Position.x, <see cref="FloorY"/>,
    /// B.Position.z) turned by <see cref="YawDeg"/> = B.YawDeg; every layout-local point p maps to world as
    /// <c>InteriorLayout.ToWorld(p, Origin, YawDeg)</c> = Origin + Quat.Yaw(YawDeg) × p. Its collision triangles are
    /// loaded into the collision world as region <see cref="RegionKey"/> with flags <c>SurfaceFlags.Interior</c>.
    /// </para>
    /// </summary>
    public sealed class InteriorState
    {
        /// <summary>Collision region key of the loaded interior: a dedicated sentinel no streamed region uses.</summary>
        public const long RegionKey = long.MinValue;
        /// <summary>World y of every interior's floor.</summary>
        public const float FloorY = -1500f;
        /// <summary>Fade duration of each half of a transition, seconds.</summary>
        public const float FadeSeconds = 0.4f;
        /// <summary>Apartment variants: an elevator ride from a lobby loads variant 1 + building id % 20.</summary>
        public const int ApartmentVariants = 20;

        /// <summary>Phase.</summary>
        public InteriorPhase Phase;
        /// <summary>Seconds spent in the current phase.</summary>
        public float PhaseSeconds;
        /// <summary>True while the player is in the interior (phases FadingIn, Inside, FadingOutToExit).</summary>
        public bool IsInside { get { return Phase == InteriorPhase.FadingIn || Phase == InteriorPhase.Inside || Phase == InteriorPhase.FadingOutToExit; } }
        /// <summary>The building (null outside).</summary>
        public BuildingDescriptor Building;
        /// <summary>The loaded layout (null outside).</summary>
        public InteriorLayout Layout;
        /// <summary>World placement origin (see the class).</summary>
        public Vec3 Origin;
        /// <summary>Placement yaw, degrees.</summary>
        public float YawDeg;
        /// <summary>Outside door used to enter (world, on the ground); the player returns here.</summary>
        public Vec3 Door;
        /// <summary>True when the building is private and not owned (entering it was a Trespass).</summary>
        public bool Trespassing;
        /// <summary>Ids of the occupant peds (kind Npc) created for this interior; removed on exit.</summary>
        public readonly List<int> Occupants = new List<int>();
        /// <summary>Interactions added by systems; cleared on exit.</summary>
        public readonly List<CustomInteraction> CustomInteractions = new List<CustomInteraction>();

        /// <summary>Interior-local → world for the loaded interior.</summary>
        public Vec3 ToWorld(Vec3 local) { return InteriorLayout.ToWorld(local, Origin, YawDeg); }

        /// <summary>World → interior-local for the loaded interior.</summary>
        public Vec3 ToLocal(Vec3 world) { return InteriorLayout.ToLocal(world, Origin, YawDeg); }

        /// <summary>The origin an interior of <paramref name="building"/> is placed at: (x, −1500, z) of its position.</summary>
        public static Vec3 OriginFor(BuildingDescriptor building)
        {
            if (building == null) throw new ArgumentNullException("building");
            return new Vec3(building.Position.X, FloorY, building.Position.Z);
        }

        /// <summary>Adds an interaction to the current interior (ignored outside). Allocates only the list growth.</summary>
        public void AddCustomInteraction(int id, Vec3 localPosition, string prompt)
        {
            if (!IsInside) return;
            CustomInteractions.Add(new CustomInteraction { Id = id, LocalPosition = localPosition, Prompt = prompt });
        }
    }
}
