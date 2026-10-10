using System;
using GtaSkylines.Game.Crime;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Police
{
    /// <summary>Why the radio speaks.</summary>
    public enum RadioCue
    {
        /// <summary>A crime became known (line by crime type).</summary>
        CrimeReported = 0,
        /// <summary>A unit recognised the player (pursuit starts, or resumes after evading).</summary>
        Spotted = 1,
        /// <summary>Evading began (stars flash).</summary>
        LostVisual = 2,
        /// <summary>Periodic update while evading (every 15 s): last seen position and description.</summary>
        LastSeen = 3,
        /// <summary>A roadblock car reached its junction.</summary>
        Roadblock = 4,
        /// <summary>The helicopter spawned.</summary>
        AirUnit = 5,
        /// <summary>The stars cleared by evasion.</summary>
        Cleared = 6,
    }

    /// <summary>
    /// Police radio chatter as HUD subtitles (speaker <see cref="Speaker"/>, <see cref="LineSeconds"/> s). At most one
    /// line per <see cref="MinInterval"/> game seconds; a cue arriving earlier is dropped, except Cleared and Spotted,
    /// which wait for the interval. Templates (street from <c>ICity.StreetNameNear(position, 60)</c>, "the area" when
    /// null; direction from the player's heading by <see cref="CompassWord"/>; car words from the description:
    /// <see cref="ColourName"/>(CarColour) + " " + the model's Name lower-cased; on foot: "on foot"):
    /// <list type="bullet">
    /// <item>CrimeReported: "Dispatch, we have a report of {crime} near {street}." with {crime} from <see cref="CrimeWords"/>.</item>
    /// <item>Spotted: "Suspect spotted on {street}, {in a red sedan | on foot}. Moving in."</item>
    /// <item>LostVisual: "We've lost visual. Searching the area."</item>
    /// <item>LastSeen: "Suspect last seen heading {north} on {street} {in a red sedan | on foot}."</item>
    /// <item>Roadblock: "Roadblock in position on {street}."</item>
    /// <item>AirUnit: "Air unit en route, spotlight on."</item>
    /// <item>Cleared: "No sign of the suspect. All units, resume patrol."</item>
    /// </list>
    /// Allocates only when a line is produced (string formatting), never in a step without a line.
    /// </summary>
    public sealed class PoliceRadio
    {
        /// <summary>Subtitle speaker.</summary>
        public const string Speaker = "Police radio";
        /// <summary>Seconds each line shows.</summary>
        public const float LineSeconds = 4f;
        /// <summary>Minimum game seconds between lines.</summary>
        public const float MinInterval = 8f;
        /// <summary>Seconds between LastSeen lines while evading.</summary>
        public const float LastSeenInterval = 15f;

        private static readonly string[] Compass = { "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest" };

        private static readonly string[] Crimes =
        {
            "an incident", "a car theft", "a carjacking", "an assault", "a hit and run", "a hit and run",
            "an assault", "a homicide", "shots fired", "shots fired", "an explosion", "a trespasser",
            "an armed robbery", "reckless driving", "an officer assaulted", "an officer down",
            "a suspect resisting arrest", "a stolen police vehicle", "a man with a gun",
        };

        private static readonly int[] PaletteRgb =
        {
            0xC0392B, 0x2E5FA8, 0x2E8B57, 0xF2F2F2, 0x1A1A1A, 0xA8A8A8, 0xE8C21E, 0xE67E22, 0x7B4A2A, 0x7D3C98, 0x606060,
        };

        private static readonly string[] PaletteNames =
        {
            "red", "blue", "green", "white", "black", "silver", "yellow", "orange", "brown", "purple", "grey",
        };

        /// <summary>
        /// Compass word for a yaw (0 = +z = north, 90 = +x = east): the nearest of the 8 directions; implemented (data).
        /// </summary>
        public static string CompassWord(float yawDeg)
        {
            float a = yawDeg % 360f;
            if (a < 0f) a += 360f;
            int i = (int)Math.Floor((a + 22.5f) / 45f) % 8;
            return Compass[i];
        }

        /// <summary>The palette colour name nearest (RGB squared distance) to 0xRRGGBB; implemented (data).</summary>
        public static string ColourName(int rgb)
        {
            int r = (rgb >> 16) & 0xFF, g = (rgb >> 8) & 0xFF, b = rgb & 0xFF;
            int best = 0;
            int bestD = int.MaxValue;
            for (int i = 0; i < PaletteRgb.Length; i++)
            {
                int p = PaletteRgb[i];
                int dr = r - ((p >> 16) & 0xFF), dg = g - ((p >> 8) & 0xFF), db = b - (p & 0xFF);
                int d = dr * dr + dg * dg + db * db;
                if (d < bestD) { bestD = d; best = i; }
            }
            return PaletteNames[best];
        }

        /// <summary>Words for a crime type in radio lines (indexed by <see cref="CrimeType"/>); implemented (data).</summary>
        public static string CrimeWords(CrimeType type)
        {
            int i = (int)type;
            return i >= 0 && i < Crimes.Length ? Crimes[i] : Crimes[0];
        }

        /// <summary>Game seconds of the last line, −∞ before any.</summary>
        public double LastLineTime = double.NegativeInfinity;

        /// <summary>The last line produced (cached), or null.</summary>
        public string LastLine { get; private set; }

        /// <summary>
        /// Produces the line for a cue (subject to the interval rule) and queues it with
        /// <c>Messages.QueueSubtitle(Speaker, line, LineSeconds)</c>; returns true when a line was queued.
        /// <paramref name="crime"/> is used for CrimeReported only.
        /// </summary>
        public bool Say(GameWorld world, RadioCue cue, CrimeType crime)
        {
            throw new NotImplementedException();
        }
    }
}
