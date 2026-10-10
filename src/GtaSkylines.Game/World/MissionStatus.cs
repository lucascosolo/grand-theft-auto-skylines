using System.Collections.Generic;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.World
{
    /// <summary>Highlight colour of an objective span (M4: yellow, blue, red, green).</summary>
    public enum HighlightKind
    {
        /// <summary>Places and destinations (yellow).</summary>
        Place,
        /// <summary>Vehicles and friendly actors (blue).</summary>
        Friendly,
        /// <summary>Targets and enemies (red).</summary>
        Target,
        /// <summary>Pick-ups and goals (green).</summary>
        Goal,
    }

    /// <summary>A highlighted character range of an objective text.</summary>
    public struct TextSpan
    {
        /// <summary>First character index.</summary>
        public int Start;
        /// <summary>Number of characters.</summary>
        public int Length;
        /// <summary>Colour.</summary>
        public HighlightKind Kind;
    }

    /// <summary>What a blip shows (M4 picks the icon and colour).</summary>
    public enum BlipKind
    {
        /// <summary>The player arrow (the HUD adds it).</summary>
        Player,
        /// <summary>The map waypoint (the HUD adds it).</summary>
        Waypoint,
        /// <summary>A mission start marker (yellow).</summary>
        MissionStart,
        /// <summary>A mission destination (yellow).</summary>
        Destination,
        /// <summary>A mission or job target to chase or kill (red).</summary>
        Target,
        /// <summary>A friendly mission actor or vehicle (blue).</summary>
        Friendly,
        /// <summary>A side-job pickup or drop (blue).</summary>
        Job,
        /// <summary>A shop.</summary>
        Shop,
        /// <summary>A safehouse.</summary>
        Safehouse,
        /// <summary>A police unit (the HUD adds it while wanted).</summary>
        Cop,
        /// <summary>A race checkpoint.</summary>
        Checkpoint,
    }

    /// <summary>A minimap blip. When <see cref="FollowTag"/> is set, the HUD places it at that actor every frame.</summary>
    public struct Blip
    {
        /// <summary>Caller-chosen id (unique within the list), for updates and removal.</summary>
        public int Id;
        /// <summary>Kind.</summary>
        public BlipKind Kind;
        /// <summary>World position (ignored while FollowTag resolves to a live actor).</summary>
        public Vec3 Position;
        /// <summary>WorldTag of a Game ped or car to follow, 0 none.</summary>
        public int FollowTag;
        /// <summary>Flashing.</summary>
        public bool Flashing;
    }

    /// <summary>An in-world marker the Mod draws (yellow cylinder for mission starts, checkpoints).</summary>
    public struct WorldMarker
    {
        /// <summary>Caller-chosen id.</summary>
        public int Id;
        /// <summary>Ground centre.</summary>
        public Vec3 Position;
        /// <summary>Radius, metres.</summary>
        public float Radius;
        /// <summary>Colour, 0xRRGGBB.</summary>
        public int ColourRgb;
    }

    /// <summary>
    /// The mission blackboard. G9 writes it; G6 creates it, clears it at <c>Begin</c>, and the HUD and renderer read it.
    /// Lists are kept between steps; writers update in place.
    /// </summary>
    public sealed class MissionStatus
    {
        /// <summary>True while a mission or side job runs.</summary>
        public bool Active;
        /// <summary>Mission title ("Fresh Start"), or null.</summary>
        public string Title;
        /// <summary>Objective line, or null for none.</summary>
        public string Objective;
        /// <summary>Highlighted ranges of <see cref="Objective"/>.</summary>
        public readonly List<TextSpan> ObjectiveSpans = new List<TextSpan>();
        /// <summary>Seconds left on the step timer; negative = no timer shown.</summary>
        public float TimerSeconds = -1f;
        /// <summary>Extra status line under the objective (race "Position 2/4"), or null.</summary>
        public string StatusLine;
        /// <summary>Blips for missions, jobs, markers, shops and safehouses (the HUD adds player, waypoint and cops).</summary>
        public readonly List<Blip> Blips = new List<Blip>();
        /// <summary>In-world markers.</summary>
        public readonly List<WorldMarker> Markers = new List<WorldMarker>();
        /// <summary>True when the GPS should route to <see cref="GpsTarget"/> (takes priority over the waypoint).</summary>
        public bool HasGpsTarget;
        /// <summary>GPS destination.</summary>
        public Vec3 GpsTarget;

        /// <summary>Clears everything to the inactive state (Active false, texts null, lists empty, timer −1, no GPS target).</summary>
        public void Clear()
        {
            Active = false;
            Title = null;
            Objective = null;
            ObjectiveSpans.Clear();
            TimerSeconds = -1f;
            StatusLine = null;
            Blips.Clear();
            Markers.Clear();
            HasGpsTarget = false;
            GpsTarget = Vec3.Zero;
        }
    }
}
