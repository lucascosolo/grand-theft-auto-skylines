using System;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Settings;

namespace GtaSkylines.Game.Crime
{
    /// <summary>
    /// Per-episode memory the star rules need. An episode starts when Stars goes from 0 to ≥ 1 and ends when Stars is 0
    /// again (cleared by evasion, or by <c>WantedStatus.Reset</c> on Busted/Wasted); <see cref="CrimeSystem"/> resets it
    /// at the start of a step that finds Stars = 0.
    /// </summary>
    public struct WantedEpisode
    {
        /// <summary>Severity of known crimes since the last star increase (any rule), excluding the crime that raised it.</summary>
        public float SeveritySinceStar;
        /// <summary>Cops killed (known) in this episode.</summary>
        public int CopKills;
        /// <summary>True once an AssaultOnCop or CopKilled crime became known in this episode (cops shoot from then on).</summary>
        public bool CopAttacked;
    }

    /// <summary>
    /// The wanted-level and heat rules (F-HEAT-3, F-HEAT-6) as pure functions over plain values, so tests can pin them
    /// without a world. Heat units: 0-100, <see cref="PlayerProfile.HeatPerStar"/> (20) per star-equivalent.
    /// Decay rates are per minute of simulated time (the sum of step dt; equal to real time at time scale 1).
    /// </summary>
    public static class WantedRules
    {
        /// <summary>Accumulated severity since the last star that must be exceeded for +1 star.</summary>
        public const float EscalationSeverity = 3f;
        /// <summary>Minimum stars after one known cop kill.</summary>
        public const int StarsAfterCopKill = 3;
        /// <summary>Minimum stars after two known cop kills.</summary>
        public const int StarsAfterTwoCopKills = 4;
        /// <summary>Player heat per unit of severity of a known crime.</summary>
        public const float PlayerHeatPerSeverity = 6f;
        /// <summary>District heat per unit of severity of a known crime.</summary>
        public const float DistrictHeatPerSeverity = 10f;
        /// <summary>Player heat decay per minute while not wanted.</summary>
        public const float PlayerDecayPerMinute = 1f;
        /// <summary>Player heat decay per minute while not wanted and inside an owned safehouse (<c>InteriorState.Building.OwnedByPlayer</c>).</summary>
        public const float SafehouseDecayPerMinute = 3f;
        /// <summary>District heat decay per minute, always (wanted or not), every district.</summary>
        public const float DistrictDecayPerMinute = 2f;

        /// <summary>
        /// Stars after a known crime of <paramref name="severity"/> (<paramref name="isCopKill"/> for
        /// <see cref="CrimeType.CopKilled"/>), updating <paramref name="episode"/>:
        /// <list type="number">
        /// <item>Not wanted (<paramref name="stars"/> = 0): result = ceil(severity) (at least 1); SeveritySinceStar = 0.</item>
        /// <item>Wanted: SeveritySinceStar += severity; result = max(stars, ceil(severity)); if SeveritySinceStar &gt;
        /// <see cref="EscalationSeverity"/>, result = max(result, stars + 1).</item>
        /// <item>Cop kill: CopKills += 1; result = max(result, 3) after the first, max(result, 4) from the second.</item>
        /// <item>Cap 5. Whenever result &gt; stars, SeveritySinceStar = 0.</item>
        /// </list>
        /// Example: 1★, crimes of 1, 1, 1, 1 → stays 1★ until the fourth (sum 4 &gt; 3) → 2★.
        /// </summary>
        public static int StarsAfterCrime(int stars, float severity, bool isCopKill, ref WantedEpisode episode)
        {
            int ceil = (int)Math.Ceiling(severity - 1e-4f);
            if (ceil < 1) ceil = 1;
            int result;
            if (stars <= 0)
            {
                result = ceil;
                episode.SeveritySinceStar = 0f;
            }
            else
            {
                episode.SeveritySinceStar += severity;
                result = Math.Max(stars, ceil);
                if (episode.SeveritySinceStar > EscalationSeverity + 1e-4f) result = Math.Max(result, stars + 1);
            }
            if (isCopKill)
            {
                episode.CopKills++;
                result = Math.Max(result, episode.CopKills >= 2 ? StarsAfterTwoCopKills : StarsAfterCopKill);
            }
            if (result > 5) result = 5;
            if (result > stars) episode.SeveritySinceStar = 0f;
            return result;
        }

        /// <summary>Player heat after a known crime: min(100, heat + 6 × severity).</summary>
        public static float PlayerHeatAfterCrime(float heat, float severity)
        {
            return Math.Min(PlayerProfile.MaxHeat, heat + PlayerHeatPerSeverity * severity);
        }

        /// <summary>District heat after a known crime in it: min(100, heat + 10 × severity).</summary>
        public static float DistrictHeatAfterCrime(float heat, float severity)
        {
            return Math.Min(PlayerProfile.MaxHeat, heat + DistrictHeatPerSeverity * severity);
        }

        /// <summary>
        /// Player heat after <paramref name="dt"/> seconds: unchanged while wanted; else heat − rate × dt / 60 with rate
        /// 3 in an owned safehouse, else 1; never below 0.
        /// </summary>
        public static float DecayPlayerHeat(float heat, bool wanted, bool inSafehouse, float dt)
        {
            if (wanted) return heat;
            float rate = inSafehouse ? SafehouseDecayPerMinute : PlayerDecayPerMinute;
            return Math.Max(0f, heat - rate * dt / 60f);
        }

        /// <summary>District heat after <paramref name="dt"/> seconds: max(0, heat − 2 × dt / 60).</summary>
        public static float DecayDistrictHeat(float heat, float dt)
        {
            return Math.Max(0f, heat - DistrictDecayPerMinute * dt / 60f);
        }

        /// <summary>
        /// Budget scale of a police difficulty (contract): Easy 0.6, Normal 1, Hard 1.4. Implemented (data).
        /// </summary>
        public static float BudgetScale(PoliceDifficulty difficulty)
        {
            switch (difficulty)
            {
                case PoliceDifficulty.Easy: return 0.6f;
                case PoliceDifficulty.Hard: return 1.4f;
                default: return 1f;
            }
        }

        /// <summary>
        /// Reaction-time multiplier of a police difficulty, applied to the response delay, suspicion fill times and the
        /// cop shot interval: Easy 1.4, Normal 1, Hard 0.7. Implemented (data).
        /// </summary>
        public static float ReactionScale(PoliceDifficulty difficulty)
        {
            switch (difficulty)
            {
                case PoliceDifficulty.Easy: return 1.4f;
                case PoliceDifficulty.Hard: return 0.7f;
                default: return 1f;
            }
        }
    }
}
