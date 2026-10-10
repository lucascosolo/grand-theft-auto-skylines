using System.Globalization;
using System.Text;
using System.Collections.Generic;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Hud
{
    /// <summary>Crosshair the HUD draws.</summary>
    public enum CrosshairKind
    {
        /// <summary>None (in a car without aiming, menus, big screens).</summary>
        None,
        /// <summary>White dot (on foot in first person, or aiming).</summary>
        Dot,
        /// <summary>Red dot: aiming at a Hostile ped or the current lock-on target.</summary>
        Hostile,
    }

    /// <summary>Identity card of the ambient citizen or promoted ped under the crosshair within 6 m (raw data; G9/M4 word it).</summary>
    public struct PedCardData
    {
        /// <summary>CS1 citizen id.</summary>
        public uint Citizen;
        /// <summary>Name.</summary>
        public string Name;
        /// <summary>Age in years.</summary>
        public int Age;
        /// <summary>Name of the work or school building, null for none.</summary>
        public string WorkName;
        /// <summary>Name of the home building, null for none.</summary>
        public string HomeName;
        /// <summary>Happiness 0-100.</summary>
        public int Happiness;
        /// <summary>Student (WorkName is a school).</summary>
        public bool Student;
        /// <summary>Tourist.</summary>
        public bool Tourist;
        /// <summary>Employed or studying.</summary>
        public bool Employed;
        /// <summary>Age in years ≥ 65 and not employed (retired).</summary>
        public bool Retired;
    }

    /// <summary>A blip resolved for this frame (FollowTag already applied).</summary>
    public struct HudBlip
    {
        /// <summary>Kind.</summary>
        public BlipKind Kind;
        /// <summary>World position.</summary>
        public Vec3 Position;
        /// <summary>Heading in degrees (the player arrow; 0 otherwise).</summary>
        public float YawDeg;
        /// <summary>Flashing.</summary>
        public bool Flashing;
    }

    /// <summary>
    /// Everything the HUD draws, as plain data, filled by <see cref="HudSystem"/> every presentation step. Strings are
    /// formatted only when their source value changes (same instance otherwise), lists are reused, so a steady frame
    /// allocates nothing. Fractions are 0..1. Text with key names uses <c>~Key~</c> (e.g. "Press ~F~ to enter Sedan").
    /// </summary>
    public sealed class HudModel
    {
        /// <summary>Seconds the street/district banner shows after a change.</summary>
        public const float LocationBannerSeconds = 4f;
        /// <summary>Seconds the vehicle name banner shows after entering.</summary>
        public const float VehicleBannerSeconds = 4f;
        /// <summary>Seconds the cash delta line shows.</summary>
        public const float CashDeltaSeconds = 3f;
        /// <summary>Ped card range, metres.</summary>
        public const float PedCardRange = 6f;
        /// <summary>Most GPS polyline points.</summary>
        public const int MaxGpsPoints = 128;
        /// <summary>GPS polyline spacing, metres.</summary>
        public const float GpsPointSpacing = 10f;
        /// <summary>Minimap radius at rest, metres.</summary>
        public const float MinimapRadiusMin = 120f;
        /// <summary>Minimap radius at 30 m/s and above, metres.</summary>
        public const float MinimapRadiusMax = 360f;

        // vitals
        /// <summary>Player health / 100.</summary>
        public float Health;
        /// <summary>Player armour / 100.</summary>
        public float Armour;
        /// <summary>Stamina / 100.</summary>
        public float Stamina;
        /// <summary>Breath fraction (motor Breath ÷ BreathSeconds).</summary>
        public float Breath;
        /// <summary>True while swimming or breath is not full (the breath bar shows).</summary>
        public bool ShowBreath;

        // wanted, cash, weapon
        /// <summary>Stars 0-5.</summary>
        public int Stars;
        /// <summary>True while evading (stars pulse).</summary>
        public bool StarsFlashing;
        /// <summary>Player heat / 100 (the heat meter).</summary>
        public float Heat;
        /// <summary>Cash.</summary>
        public long Cash;
        /// <summary>"$1,234,567" for <see cref="Cash"/>.</summary>
        public string CashText;
        /// <summary>Sum of wallet deltas since the delta line appeared (reset when it expires).</summary>
        public long CashDelta;
        /// <summary>"+$500" / "−$200", or null.</summary>
        public string CashDeltaText;
        /// <summary>Seconds since the last cash change; ≥ CashDeltaSeconds hides the line.</summary>
        public float CashDeltaAge = float.MaxValue;
        /// <summary>Weapon in hand ("Pistol"), null for fists.</summary>
        public string WeaponName;
        /// <summary>Rounds in the magazine: from <c>Hooks.Ammo</c> when it answers, else Ped.MagazineAmmo.</summary>
        public int AmmoInMagazine;
        /// <summary>Reserve rounds: from <c>Hooks.Ammo</c> when it answers (G7: profile ammo − magazine), else the profile ammo for the weapon.</summary>
        public int AmmoReserve;
        /// <summary>True for weapons that use ammo.</summary>
        public bool ShowAmmo;

        // texts
        /// <summary>Help box text (tokens substituted), or null.</summary>
        public string HelpText;
        /// <summary>Objective line, or null.</summary>
        public string Objective;
        /// <summary>Highlight spans of the objective (copied from MissionStatus).</summary>
        public readonly List<TextSpan> ObjectiveSpans = new List<TextSpan>();
        /// <summary>"m:ss" of the mission timer, null when none.</summary>
        public string TimerText;
        /// <summary>Extra mission status line, or null.</summary>
        public string StatusLine;
        /// <summary>Subtitle speaker, or null.</summary>
        public string SubtitleSpeaker;
        /// <summary>Subtitle line, or null.</summary>
        public string SubtitleText;
        /// <summary>Notification feed, newest first, ≤ 4.</summary>
        public readonly List<Notification> Notifications = new List<Notification>(HudMessages.NotificationCapacity);
        /// <summary>Street name at the player (<c>ICity.StreetNameNear</c>, 30 m), or null.</summary>
        public string StreetName;
        /// <summary>District name at the player.</summary>
        public string DistrictName;
        /// <summary>Seconds since the street or district changed; the banner shows below LocationBannerSeconds.</summary>
        public float LocationBannerAge = float.MaxValue;
        /// <summary>Model name of the car the player entered last.</summary>
        public string VehicleName;
        /// <summary>Seconds since entering it; the banner shows below VehicleBannerSeconds.</summary>
        public float VehicleBannerAge = float.MaxValue;
        /// <summary>Speed of the player's car in km/h (rounded), 0 on foot.</summary>
        public int SpeedKmh;
        /// <summary>True while driving.</summary>
        public bool ShowSpeed;

        // full screen
        /// <summary>Big screen.</summary>
        public BigScreenKind BigScreen;
        /// <summary>Its reason text (MissionFailed), or null.</summary>
        public string BigScreenText;
        /// <summary>Its reward (MissionPassed).</summary>
        public long BigScreenReward;
        /// <summary>0..1 progress through it.</summary>
        public float BigScreenProgress;
        /// <summary>Black fade 0..1 (world.FadeAlpha).</summary>
        public float FadeAlpha;
        /// <summary>Crosshair.</summary>
        public CrosshairKind Crosshair;
        /// <summary>True when <see cref="PedCard"/> is valid.</summary>
        public bool HasPedCard;
        /// <summary>Ped card.</summary>
        public PedCardData PedCard;

        // map
        /// <summary>Player feet position (minimap centre).</summary>
        public Vec3 PlayerPosition;
        /// <summary>Camera yaw in degrees (the minimap rotates by it).</summary>
        public float MapRotationDeg;
        /// <summary>Minimap radius in metres: lerp(MinimapRadiusMin, Max, clamp01(speed ÷ 30)), eased.</summary>
        public float MinimapRadius = MinimapRadiusMin;
        /// <summary>Blips: the player arrow first, the waypoint, mission blips, cops (Kind Cop peds and Police cars) while wanted.</summary>
        public readonly List<HudBlip> Blips = new List<HudBlip>();
        /// <summary>GPS polyline from the player along the route, every 10 m, at most 128 points; empty without a target.</summary>
        public readonly List<Vec3> GpsPoints = new List<Vec3>(MaxGpsPoints);
        /// <summary>Incremented whenever <see cref="GpsPoints"/> is rebuilt (route recomputed or the player moved 10 m along it).</summary>
        public int GpsRevision;
        /// <summary>True when the search zone shows.</summary>
        public bool ShowSearchZone;
        /// <summary>Search zone centre.</summary>
        public Vec3 SearchCentre;
        /// <summary>Search zone radius, metres.</summary>
        public float SearchRadius;
        /// <summary>True while inside an interior (the minimap shows the building).</summary>
        public bool InInterior;

        /// <summary>The open menu (same instance as world.Menu).</summary>
        public MenuRequest Menu;

        /// <summary>"$1,234,567" ("−$1,234" for negatives) with invariant thousands separators.</summary>
        public static string FormatCash(long amount)
        {
            ulong v = amount < 0 ? (ulong)(-(amount + 1)) + 1UL : (ulong)amount;
            string digits = v.ToString(CultureInfo.InvariantCulture);
            var sb = new StringBuilder(digits.Length + digits.Length / 3 + 2);
            if (amount < 0) sb.Append('-');
            sb.Append('$');
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0) sb.Append(',');
                sb.Append(digits[i]);
            }
            return sb.ToString();
        }
    }
}
