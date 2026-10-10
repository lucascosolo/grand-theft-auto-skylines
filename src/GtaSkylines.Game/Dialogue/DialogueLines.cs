using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Dialogue
{
    /// <summary>A group of authored lines. Greeting groups describe the speaker; the last five are reactions.</summary>
    public enum LineGroup
    {
        /// <summary>Happiness ≥ 75.</summary>
        Happy,
        /// <summary>Happiness 50-74.</summary>
        Content,
        /// <summary>Happiness 25-49.</summary>
        Unhappy,
        /// <summary>Happiness below 25.</summary>
        Miserable,
        /// <summary>Wealth 2.</summary>
        Wealthy,
        /// <summary>Wealth 0.</summary>
        Poor,
        /// <summary>Education 3.</summary>
        Educated,
        /// <summary>Education 0 (adults).</summary>
        Uneducated,
        /// <summary>Age below 13.</summary>
        Child,
        /// <summary>Age 13-19.</summary>
        Teen,
        /// <summary>Age 65 and over.</summary>
        Senior,
        /// <summary>Tourists.</summary>
        Tourist,
        /// <summary>Working-age adults without a job (not students, not tourists).</summary>
        Unemployed,
        /// <summary>Students aged 13 and over.</summary>
        Student,
        /// <summary>Employed, not students.</summary>
        Worker,
        /// <summary>Sick, or health below 40.</summary>
        Sick,
        /// <summary>The player's heat is ≥ 40: they recognise the player.</summary>
        Recognised,
        /// <summary>No citizen profile (spawned peds, unknown citizens).</summary>
        Generic,
        /// <summary>Reaction: bumped into.</summary>
        Bumped,
        /// <summary>Reaction: pulled out of their car.</summary>
        Carjacked,
        /// <summary>Reaction: held up.</summary>
        Robbed,
        /// <summary>Reaction: on the phone to the police.</summary>
        WitnessCalling,
        /// <summary>Reaction: a cop giving an order.</summary>
        CopOrder,
    }

    /// <summary>
    /// G9's <see cref="IDialogueHook"/>: picks an authored line deterministically. Same (citizen, context) → same line;
    /// different profiles → usually different lines. Returns constant strings (no allocation).
    /// <para>Selection for <see cref="DialogueContext.Greeting"/>:</para>
    /// <list type="number">
    /// <item>Key: the citizen id, or 0x80000000 | ped id when the citizen is 0 (ped given), else 0 → <see cref="LineGroup.Generic"/>.
    /// h = <c>Rng.Seed(key, (uint)context + 1, 0x51A7)</c>.</item>
    /// <item>No profile (<c>ICity.TryGetCitizenProfile</c> false or citizen 0) → Generic.</item>
    /// <item>Profile: candidate groups via <see cref="Candidates"/>. When <c>world.Wanted.PlayerHeat</c> ≥
    /// <see cref="RecognisedHeat"/> and (h &amp; 1) == 0 → Recognised. Otherwise group = candidates[(h &gt;&gt; 1) %
    /// count].</item>
    /// <item>Line = group's lines[(h &gt;&gt; 8) % length].</item>
    /// </list>
    /// Other contexts map to their reaction group (Bumped, Carjacked, Robbed, WitnessCalling, CopOrder) with the same
    /// line pick. Never returns null for a known context.
    /// </summary>
    public sealed class DialogueLines : IDialogueHook
    {
        /// <summary>Heat at which citizens recognise the player.</summary>
        public const float RecognisedHeat = 40f;
        /// <summary>Maximum candidate groups for one profile.</summary>
        public const int MaxCandidates = 8;

        /// <inheritdoc />
        public string Line(GameWorld world, DialogueContext context, uint citizen, Ped ped)
        {
            uint key = citizen != 0 ? citizen : ped != null ? 0x80000000u | (uint)ped.Id : 0u;
            uint h = Rng.Seed(key, (uint)context + 1u, 0x51A7u);
            LineGroup group = GroupFor(context);
            CitizenProfile profile;
            if (context == DialogueContext.Greeting && citizen != 0 && world.City.TryGetCitizenProfile(citizen, out profile))
            {
                int n = Candidates(profile, _candidates);
                if (world.Wanted.PlayerHeat >= RecognisedHeat && (h & 1u) == 0u) group = LineGroup.Recognised;
                else if (n > 0) group = _candidates[(int)((h >> 1) % (uint)n)];
            }
            string[] lines = Lines(group);
            return lines[(int)((h >> 8) % (uint)lines.Length)];
        }

        private readonly LineGroup[] _candidates = new LineGroup[MaxCandidates];

        /// <summary>
        /// Fills <paramref name="into"/> (length ≥ <see cref="MaxCandidates"/>) with the greeting groups a profile
        /// belongs to, in this order, and returns the count: Sick (Sick or Health &lt; 40); the happiness band; Tourist
        /// (tourists stop here: no age, wealth, education or employment groups); Child / Teen / Senior by age; Student
        /// (age ≥ 13) or Worker (Employed, not Student) or Unemployed (age 20-64, not employed); Wealthy (2) or Poor
        /// (0); Educated (3) or Uneducated (0, age ≥ 20).
        /// </summary>
        public static int Candidates(CitizenProfile profile, LineGroup[] into)
        {
            int n = 0;
            if (profile.Sick || profile.Health < 40) into[n++] = LineGroup.Sick;
            int h = profile.Happiness;
            into[n++] = h >= 75 ? LineGroup.Happy : h >= 50 ? LineGroup.Content : h >= 25 ? LineGroup.Unhappy : LineGroup.Miserable;
            if (profile.Tourist)
            {
                into[n++] = LineGroup.Tourist;
                return n;
            }
            int age = profile.Age;
            if (age < 13) into[n++] = LineGroup.Child;
            else if (age <= 19) into[n++] = LineGroup.Teen;
            else if (age >= 65) into[n++] = LineGroup.Senior;
            if (profile.Student && age >= 13) into[n++] = LineGroup.Student;
            else if (profile.Employed && !profile.Student) into[n++] = LineGroup.Worker;
            else if (!profile.Employed && age >= 20 && age <= 64) into[n++] = LineGroup.Unemployed;
            if (profile.Wealth >= 2) into[n++] = LineGroup.Wealthy;
            else if (profile.Wealth <= 0) into[n++] = LineGroup.Poor;
            if (profile.Education >= 3) into[n++] = LineGroup.Educated;
            else if (profile.Education <= 0 && age >= 20) into[n++] = LineGroup.Uneducated;
            return n;
        }

        /// <summary>The reaction group for a context (Greeting → Generic).</summary>
        public static LineGroup GroupFor(DialogueContext context)
        {
            switch (context)
            {
                case DialogueContext.Bumped: return LineGroup.Bumped;
                case DialogueContext.Carjacked: return LineGroup.Carjacked;
                case DialogueContext.Robbed: return LineGroup.Robbed;
                case DialogueContext.WitnessCalling: return LineGroup.WitnessCalling;
                case DialogueContext.CopOrder: return LineGroup.CopOrder;
                default: return LineGroup.Generic;
            }
        }

        /// <summary>The authored lines of a group.</summary>
        public static string[] Lines(LineGroup group)
        {
            return DialogueContent.Groups[(int)group];
        }

        /// <summary>Total authored lines over every group.</summary>
        public static int TotalLines
        {
            get
            {
                int n = 0;
                for (int i = 0; i < DialogueContent.Groups.Length; i++) n += DialogueContent.Groups[i].Length;
                return n;
            }
        }
    }
}
