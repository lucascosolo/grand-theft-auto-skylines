using System;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Hud;

namespace GtaSkylines.Game.Dialogue
{
    /// <summary>The worded lines of a ped card.</summary>
    public struct PedCardLines
    {
        /// <summary>Name line ("Ada Kowalski").</summary>
        public string Name;
        /// <summary>Age line ("Age 34").</summary>
        public string Age;
        /// <summary>Occupation line: "Works at X", "Student at X", "Retired", "Unemployed", "Tourist".</summary>
        public string Occupation;
        /// <summary>Home line: "Lives at X", "Visiting {city}" (tourists), or "No fixed address".</summary>
        public string Home;
        /// <summary>Happiness word (<see cref="PedCardText.HappinessWord"/>).</summary>
        public string Mood;
    }

    /// <summary>
    /// Words the ped card (F-PED-1). Rules, in order, for the occupation: Tourist → "Tourist"; Student with a work
    /// building → "Student at <i>name</i>"; Employed with a work building → "Works at <i>name</i>"; age ≥ 65 → "Retired";
    /// else "Unemployed". Home: Tourist → "Visiting <i>city</i>"; a home building → "Lives at <i>name</i>"; else
    /// "No fixed address". Building names come from <c>ICity.TryGetBuilding</c> (a missing building counts as none).
    /// <see cref="Format(PedCardData, string)"/> caches by citizen: called every frame for the same citizen it returns the
    /// same strings without allocating.
    /// </summary>
    public sealed class PedCardText
    {
        /// <summary>Happiness words by band of 20: Miserable, Unhappy, Okay, Happy, Thrilled.</summary>
        public static readonly string[] HappinessWords = { "Miserable", "Unhappy", "Okay", "Happy", "Thrilled" };

        /// <summary>Retired.</summary>
        public const string Retired = "Retired";
        /// <summary>Unemployed.</summary>
        public const string Unemployed = "Unemployed";
        /// <summary>Tourist.</summary>
        public const string Tourist = "Tourist";
        /// <summary>No home.</summary>
        public const string NoHome = "No fixed address";

        /// <summary>Happiness 0-100 → word: &lt;20 Miserable, &lt;40 Unhappy, &lt;60 Okay, &lt;80 Happy, else Thrilled (clamped).</summary>
        public static string HappinessWord(int happiness)
        {
            int band = happiness < 0 ? 0 : happiness / 20;
            return HappinessWords[band > 4 ? 4 : band];
        }

        /// <summary>Words a CS1 profile (allocates; use <see cref="Format(PedCardData, string)"/> per frame).</summary>
        public static PedCardLines From(CitizenProfile profile, ICity city)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Words the HUD's raw card (<c>HudModel.PedCard</c>, names already resolved). Cached: returns the previous result
        /// when <c>data.Citizen</c>, Happiness and the city name are unchanged.
        /// </summary>
        public PedCardLines Format(PedCardData data, string cityName)
        {
            throw new NotImplementedException();
        }
    }
}
