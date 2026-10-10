using GtaSkylines.Game.Mathx;
using System;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Peds
{
    /// <summary>
    /// G7's <see cref="IBrainFactory"/> for peds G6 creates. Install once per session:
    /// <c>world.Hooks.Brains = new PedBrainFactory()</c> (G8 sets <see cref="Cops"/> for cop peds).
    /// <list type="bullet">
    /// <item>Ped kind Cop (any origin) → <see cref="Cops"/>.Create(...) when set, else null.</item>
    /// <item><see cref="PedOrigin.PromotedCitizen"/> → <see cref="IdleBrain"/>; the stimulus that caused the promotion
    /// (hit, bump, aim) then reaches it through the normal routes (Combat damage now, PedsSystem events next step).</item>
    /// <item><see cref="PedOrigin.CarjackedDriver"/> → <see cref="FightBrain"/> against the cause when
    /// <c>new Rng(Rng.Seed(traits.Seed, 1)).NextFloat()</c> &lt; <see cref="PedTraits.CarjackedFightChance"/> (30 %), else
    /// <see cref="FleeBrain"/> from the cause (70 %). Same ped id and citizen → same choice; Traits.Fighter is not
    /// consulted.</item>
    /// <item><see cref="PedOrigin.ShockedDriver"/> → <see cref="ShockedDriverBrain"/>.</item>
    /// <item><see cref="PedOrigin.InteriorOccupant"/> → <see cref="IdleBrain"/> with the spot in its traits.</item>
    /// <item><see cref="PedOrigin.Scripted"/> → null (the spawner sets the brain).</item>
    /// </list>
    /// Traits come from <see cref="PedTraits.Roll"/>.
    /// </summary>
    public sealed class PedBrainFactory : IBrainFactory
    {
        /// <summary>Brains for cop peds (G8), or null.</summary>
        public IBrainFactory Cops;

        /// <inheritdoc />
        public IPedBrain Create(GameWorld world, Ped ped, PedOrigin origin, int cause, NpcSpot spot)
        {
            if (ped.Kind == PedKind.Cop) return Cops == null ? null : Cops.Create(world, ped, origin, cause, spot);
            PedTraits traits = PedTraits.Roll(world, ped, origin, spot);
            switch (origin)
            {
                case PedOrigin.PromotedCitizen:
                case PedOrigin.InteriorOccupant:
                    return new IdleBrain(world, traits);
                case PedOrigin.CarjackedDriver:
                    Vec3 at = PedBrain.PositionOf(world, cause, ped.Position);
                    if (new Rng(Rng.Seed(traits.Seed, 1u)).NextFloat() < PedTraits.CarjackedFightChance) return new FightBrain(traits, cause);
                    return new FleeBrain(world, traits, cause, at);
                case PedOrigin.ShockedDriver:
                    return new ShockedDriverBrain(world, traits);
                default:
                    return null;
            }
        }

        /// <summary>The traits of <paramref name="ped"/>'s current <see cref="PedBrain"/>, else freshly rolled (<see cref="PedOrigin.Scripted"/>, no spot).</summary>
        public static PedTraits TraitsOf(GameWorld world, Ped ped)
        {
            var b = ped.Brain as PedBrain;
            return b != null ? b.Traits : PedTraits.Roll(world, ped, PedOrigin.Scripted, default(NpcSpot));
        }
    }
}
