using System;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Peds
{
    /// <summary>
    /// Per-ped temperament, fixed when the first brain is made and carried from brain to brain (every
    /// <see cref="PedBrain"/> passes its traits to the next one). Rolled from <c>Rng.Seed(ped.Citizen, (uint)ped.Id)</c>
    /// (not the session Rng), so the same ped id and citizen always get the same traits.
    /// </summary>
    public struct PedTraits
    {
        /// <summary>Base chance an adult answers a punch with fists (0.15).</summary>
        public const float FighterChance = 0.15f;
        /// <summary>Chance for a male model of age group 1-2 (teen, young adult) (0.35).</summary>
        public const float YoungMaleFighterChance = 0.35f;
        /// <summary>Chance a carjacked driver fights instead of fleeing (0.30).</summary>
        public const float CarjackedFightChance = 0.30f;

        /// <summary>Answers a punch with fists (<see cref="FightBrain"/>). Never for children (age group 0) or seniors.</summary>
        public bool Fighter;
        /// <summary>Senior model (age group 4): cowers instead of fleeing from gunfire.</summary>
        public bool Senior;
        /// <summary>Shop clerk (interior occupant at a <see cref="NpcRole.Clerk"/> spot): held up → Robbery.</summary>
        public bool Clerk;
        /// <summary>Interior occupant: <see cref="HasHome"/> is its NpcSpot.</summary>
        public bool HasHome;
        /// <summary>Home spot feet position (world) for interior occupants.</summary>
        public Vec3 Home;
        /// <summary>Home spot facing, degrees.</summary>
        public float HomeYawDeg;
        /// <summary>Sits or lies at the home spot.</summary>
        public bool Seated;
        /// <summary>Flee speed, metres per second, uniform in [5, 6].</summary>
        public float FleeSpeed;
        /// <summary>The seed the traits were rolled from (brain timers come from the session <c>world.Rng</c>, drawn when each brain is constructed).</summary>
        public uint Seed;

        /// <summary>
        /// Rolls traits for <paramref name="ped"/>: seed = Rng.Seed(ped.Citizen, (uint)ped.Id); Senior from the model's age
        /// group; Fighter with <see cref="YoungMaleFighterChance"/> for non-female age group 1-2 models, else
        /// <see cref="FighterChance"/> for age group 2-3, else false; FleeSpeed = Range(5, 6). For
        /// <see cref="PedOrigin.InteriorOccupant"/> the spot fills Home, HomeYawDeg (spot yaw + interior yaw, Home via
        /// <c>Interior.ToWorld</c>), Seated and Clerk. Unknown models count as adult (age group 3), not female.
        /// </summary>
        public static PedTraits Roll(GameWorld world, Ped ped, PedOrigin origin, NpcSpot spot)
        {
            throw new NotImplementedException();
        }
    }
}
