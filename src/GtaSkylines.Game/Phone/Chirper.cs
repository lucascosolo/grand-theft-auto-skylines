using System;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Phone
{
    /// <summary>What a Chirper post reacts to.</summary>
    public enum ChirpTopic
    {
        /// <summary>A crime became known (wanted stars rose from 0, or rose further below 3).</summary>
        Crime,
        /// <summary>Gunfire (<see cref="GameEventType.Gunshot"/> with A = the player).</summary>
        Shooting,
        /// <summary>An explosion.</summary>
        Explosion,
        /// <summary>A carjacking by the player.</summary>
        Carjacking,
        /// <summary>A police chase: stars reached 3 or more.</summary>
        Chase,
        /// <summary>A mission was passed ({mission} = title).</summary>
        MissionPassed,
        /// <summary>A side job reached a new level ({job} = job name).</summary>
        JobMilestone,
    }

    /// <summary>
    /// Chirper posts reacting to the player. <see cref="TryPost"/> is the only way to post; it enforces the limits:
    /// at most one post per <see cref="MinIntervalSeconds"/> of <c>GameClock.RealSeconds</c> (the first post is always
    /// allowed) and none while <c>world.Interior.IsInside</c>. A post picks a template of its topic with the Chirper's
    /// own seeded Rng, fills <c>{street}</c> (<c>StreetNameNear(position, 80)</c>, else "downtown"), <c>{district}</c>,
    /// <c>{city}</c>, <c>{mission}</c>/<c>{job}</c> from the argument, emits <c>CityEffect.Chirp(sender, text)</c>
    /// and a HUD notification (Chirper kind, sender name from the citizen's profile or null). Sender: the snapshot
    /// citizen nearest the position (0 when the snapshot is empty: the Mod picks one). Posts allocate their text; they
    /// happen at most once per 45 s.
    /// </summary>
    public sealed class Chirper
    {
        /// <summary>Minimum real seconds between posts.</summary>
        public const float MinIntervalSeconds = 45f;

        /// <summary>Creates a feed with its own seeded random source.</summary>
        public Chirper(uint seed)
        {
            Rng = new Rng(Rng.Seed(seed, 0xC41Bu));
            LastPostRealSeconds = double.NegativeInfinity;
        }

        /// <summary>Template picks.</summary>
        public Rng Rng { get; private set; }

        /// <summary>Real time of the last post (−∞ before any).</summary>
        public double LastPostRealSeconds { get; private set; }

        /// <summary>Posts made this session.</summary>
        public int PostCount { get; private set; }

        /// <summary>The last post's text, or null.</summary>
        public string LastText { get; private set; }

        /// <summary>
        /// Posts when the limits allow (see the class). <paramref name="name"/> fills <c>{mission}</c> and
        /// <c>{job}</c> (null otherwise). Returns true when posted.
        /// </summary>
        public bool TryPost(GameWorld world, ChirpTopic topic, Vec3 position, string name)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Called by the runner once per step with that step's events and the wanted state: Gunshot by the player →
        /// Shooting; Explosion → Explosion; CarJacked by the player → Carjacking; Wanted.Stars rising (vs the previous
        /// step) → Chase when the new value ≥ 3, else Crime. Several candidates in one step → the first in that order.
        /// No allocation unless a post is made.
        /// </summary>
        public void Observe(GameWorld world, GameEvent[] events, int count)
        {
            throw new NotImplementedException();
        }

        /// <summary>Templates of a topic.</summary>
        public static string[] Templates(ChirpTopic topic)
        {
            return ChirperTemplates.ByTopic[(int)topic];
        }
    }

    /// <summary>Authored Chirper templates, indexed by <see cref="ChirpTopic"/>.</summary>
    public static class ChirperTemplates
    {
        /// <summary>Templates per topic.</summary>
        public static readonly string[][] ByTopic =
        {
            new[]
            {
                "Cops all over {street} right now. What did I miss? #{district}",
                "Just saw someone run from the police on {street}. Only in {city}.",
                "Sirens again in {district}. Some of us are trying to nap.",
                "Somebody on {street} is having a very bad day. Or a very good one.",
                "Note to self: avoid {street} today. #{city}",
            },
            new[]
            {
                "Was that gunfire on {street}?! Staying inside. #{district}",
                "Heard shots near {street}. Everyone okay out there?",
                "{district} sounding like a movie set right now. Not a fun one.",
                "Dear whoever is shooting on {street}: please don't.",
                "Pretty sure those weren't fireworks. {city}, stay safe.",
            },
            new[]
            {
                "Something just EXPLODED on {street}. My windows are still shaking.",
                "Huge boom in {district}. Is the power plant okay??",
                "Fireball on {street}. Fire trucks, any time now. #{city}",
                "Well, that's one way to wake up {district}.",
                "Explosion on {street}. Insurance companies hate this one trick.",
            },
            new[]
            {
                "Someone just got pulled out of their car on {street}. In broad daylight!",
                "Carjacking in {district}. Lock your doors, people.",
                "Guy stole a car on {street} like it was his. Bold. #{city}",
                "Watched a carjacking on {street} while waiting for the bus. The bus was still late.",
                "Remember when {district} was quiet? Me neither.",
            },
            new[]
            {
                "Police chase tearing through {district}! Half the force is out there.",
                "Just got passed by six cop cars on {street}. SIX.",
                "Helicopter over {district}. Whoever they're after is FAST.",
                "Chase on {street}! Pulling over to watch. For safety.",
                "{city} police vs. one very determined driver. Live from {street}.",
            },
            new[]
            {
                "Word on {street} is somebody pulled off \"{mission}\". Who ARE they?",
                "Something big just happened in {district}. People are calling it \"{mission}\".",
                "Can't confirm it, but \"{mission}\" is all anyone in {city} is talking about.",
                "Heard \"{mission}\" went down without a hitch. {city} has a new name to whisper.",
            },
            new[]
            {
                "Shout-out to the {job} driver working {district} tonight. Legend.",
                "Best {job} service in {city}, no contest. Five stars.",
                "{job} runs in {district} have never been faster. Who is this?",
                "That {job} driver on {street} is a menace and I love it.",
            },
        };
    }
}
