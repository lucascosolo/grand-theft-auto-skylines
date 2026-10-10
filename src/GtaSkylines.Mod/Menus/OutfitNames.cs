using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;

namespace GtaSkylines.Mod.Menus
{
    /// <summary>
    /// Readable outfit names ("Casual 3", "Business 1") for the clothes shop and the wardrobe, derived once per city from
    /// the CS1 citizen prefab names and the model order: a style word from keywords in the prefab name (else the age
    /// group), numbered per style in model order.
    /// </summary>
    public static class OutfitNames
    {
        private static readonly string[][] Keywords =
        {
            new[] { "business", "suit", "office" },
            new[] { "tourist" },
            new[] { "police", "fire", "firefighter", "fireman", "medic", "paramedic", "worker", "service", "garbage", "hearse", "uniform" },
            new[] { "sport", "sports", "jog", "jogger", "jogging", "runner", "fitness", "gym" },
            new[] { "punk", "hipster", "goth", "rocker" },
        };
        private static readonly string[] KeywordStyles = { "Business", "Tourist", "Workwear", "Sport", "Street" };

        /// <summary>
        /// The style word: the first keyword group with a whole word (case-insensitive; words split at anything but letters
        /// and digits, so "Officer" is not "office") of <paramref name="prefabName"/> — Business,
        /// Tourist, Workwear, Sport, Street — else by <paramref name="ageGroup"/>: 0 Kids, 1 Street, 4 Classic, otherwise
        /// Casual.
        /// </summary>
        public static string StyleOf(string prefabName, int ageGroup)
        {
            string n = (prefabName ?? "").ToLowerInvariant();
            for (int g = 0; g < Keywords.Length; g++)
                for (int k = 0; k < Keywords[g].Length; k++)
                    if (HasWord(n, Keywords[g][k])) return KeywordStyles[g];
            switch (ageGroup)
            {
                case 0: return "Kids";
                case 1: return "Street";
                case 4: return "Classic";
                default: return "Casual";
            }
        }

        private static bool HasWord(string text, string word)
        {
            for (int at = text.IndexOf(word, StringComparison.Ordinal); at >= 0; at = text.IndexOf(word, at + 1, StringComparison.Ordinal))
            {
                int end = at + word.Length;
                if ((at == 0 || !char.IsLetterOrDigit(text[at - 1])) && (end == text.Length || !char.IsLetterOrDigit(text[end]))) return true;
            }
            return false;
        }

        /// <summary>
        /// One name per model, in model order: <see cref="StyleOf"/> plus the 1-based count of models so far with that
        /// style. A null entry counts as an unnamed adult; a null list gives an empty array.
        /// </summary>
        public static string[] Build(IList<CitizenModel> models)
        {
            if (models == null) return new string[0];
            var names = new string[models.Count];
            var counts = new Dictionary<string, int>();
            for (int i = 0; i < names.Length; i++)
            {
                CitizenModel m = models[i];
                string style = m == null ? StyleOf(null, 3) : StyleOf(m.Name, m.AgeGroup);
                int n;
                counts.TryGetValue(style, out n);
                counts[style] = ++n;
                names[i] = style + " " + n;
            }
            return names;
        }
    }
}
