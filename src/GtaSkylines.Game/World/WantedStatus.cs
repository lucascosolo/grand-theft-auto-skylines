using System;
using GtaSkylines.Game.Crime;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Profile;

namespace GtaSkylines.Game.World
{
    /// <summary>What the police believe the suspect looks like (F-HEAT-4). Recognition compares the player against it.</summary>
    public struct SuspectDescription
    {
        /// <summary>Citizen model id of the outfit seen; −1 unknown.</summary>
        public int OutfitModel;
        /// <summary>Vehicle model id of the car seen; −1 on foot or unknown.</summary>
        public int CarModel;
        /// <summary>Colour of that car, 0xRRGGBB (meaningful when CarModel ≥ 0).</summary>
        public int CarColour;
        /// <summary>Id of that Game car, 0 none (lets G8 tell "same car" from "same model").</summary>
        public int CarId;

        /// <summary>No description: every field unknown.</summary>
        public static readonly SuspectDescription Unknown = new SuspectDescription { OutfitModel = -1, CarModel = -1 };
    }

    /// <summary>
    /// The police blackboard. G8 owns the rules and writes it; G6 creates it, calls <see cref="Reset"/> on Wasted and
    /// Busted, and the HUD reads it. Heat lives in the profile so it persists: <see cref="PlayerHeat"/> and the district
    /// heat accessors read and write <see cref="PlayerProfile.Heat"/> and <see cref="PlayerProfile.DistrictHeat"/>.
    /// </summary>
    public sealed class WantedStatus
    {
        /// <summary>Most stars.</summary>
        public const int MaxStars = 5;

        private readonly PlayerProfile _profile;

        /// <summary>Creates an empty status over a profile.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="profile"/> is null.</exception>
        public WantedStatus(PlayerProfile profile)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            _profile = profile;
            Description = SuspectDescription.Unknown;
        }

        /// <summary>Wanted level 0-5 (G8).</summary>
        public int Stars;
        /// <summary>True while wanted and no cop has seen the player for 2 s: the HUD flashes the stars (G8).</summary>
        public bool Evading;
        /// <summary>True once any cop has seen the player during this wanted episode.</summary>
        public bool HasLastKnownPosition;
        /// <summary>Where a cop last saw the player (G8).</summary>
        public Vec3 LastKnownPosition;
        /// <summary>Game seconds since a cop last saw the player (G8).</summary>
        public float SecondsSinceSeen;
        /// <summary>Centre of the search zone drawn on the minimap (G8).</summary>
        public Vec3 SearchCentre;
        /// <summary>Radius of the search zone in metres; 0 = no zone shown (G8).</summary>
        public float SearchRadius;
        /// <summary>The current description (G8; G6 updates the car colour on a respray when the description names that car).</summary>
        public SuspectDescription Description;
        /// <summary>Set by G8 when an arrest succeeds; G6's Respawn system consumes it (BUSTED flow) and clears it.</summary>
        public bool BustedRequested;
        /// <summary>Tag of the arresting cop for the PlayerArrested event (G8), 0 unknown.</summary>
        public int BustedByTag;
        /// <summary>Star-rule memory of the current wanted episode (G8's crime rules write it; cops read CopAttacked).</summary>
        public WantedEpisode Episode;

        private bool _inEpisode;

        /// <summary>Counts wanted episodes opened by <see cref="TrackEpisode"/>; a change means a new episode began.</summary>
        public int EpisodeNumber { get; private set; }

        /// <summary>
        /// Episode bookkeeping, called by every system that depends on episodes before it reads them: Stars 0 closes the
        /// episode and clears <see cref="Episode"/>; Stars &gt; 0 with none open opens one (EpisodeNumber + 1) and keeps
        /// <see cref="Episode"/> (a crime earlier in the step may already have written it).
        /// </summary>
        public void TrackEpisode()
        {
            if (Stars <= 0)
            {
                _inEpisode = false;
                Episode = default(WantedEpisode);
            }
            else if (!_inEpisode)
            {
                _inEpisode = true;
                EpisodeNumber++;
            }
        }

        /// <summary>Player heat 0-100 (the profile's).</summary>
        public float PlayerHeat
        {
            get { return _profile.Heat; }
            set { _profile.Heat = value; }
        }

        /// <summary>Heat 0-100 of a CS1 district (0 when never heated).</summary>
        public float GetDistrictHeat(byte district) { return _profile.GetDistrictHeat(district); }

        /// <summary>Sets a district's heat, clamped to 0-100.</summary>
        public void SetDistrictHeat(byte district, float heat)
        {
            _profile.DistrictHeat[district] = MathUtil.Clamp(heat, 0f, PlayerProfile.MaxHeat);
        }

        /// <summary>True when Stars &gt; 0.</summary>
        public bool IsWanted { get { return Stars > 0; } }

        /// <summary>
        /// Clears the wanted episode: Stars 0, Evading false, no last known position, SecondsSinceSeen 0, search zone
        /// cleared (radius 0), Description unknown, BustedRequested false, BustedByTag 0, Episode cleared and closed. Heat is
        /// kept.
        /// </summary>
        public void Reset()
        {
            Stars = 0;
            Evading = false;
            HasLastKnownPosition = false;
            LastKnownPosition = Vec3.Zero;
            SecondsSinceSeen = 0f;
            SearchCentre = Vec3.Zero;
            SearchRadius = 0f;
            Description = SuspectDescription.Unknown;
            BustedRequested = false;
            BustedByTag = 0;
            Episode = default(WantedEpisode);
            _inEpisode = false;
        }
    }
}
