using GtaSkylines.Game.Interiors;

namespace GtaSkylines.Game.World
{
    /// <summary>Why a ped was created or promoted (chooses its first brain).</summary>
    public enum PedOrigin
    {
        /// <summary>A CS1 citizen promoted by an interaction (bump, hit, threat, talk, job fare).</summary>
        PromotedCitizen,
        /// <summary>The driver pulled out of a car by the player.</summary>
        CarjackedDriver,
        /// <summary>The driver of a CS1 vehicle the player crashed into (sits in the car, shocked).</summary>
        ShockedDriver,
        /// <summary>An interior occupant at an NpcSpot.</summary>
        InteriorOccupant,
        /// <summary>Spawned by a system for its own purposes (cops, mission actors); the spawner sets the brain.</summary>
        Scripted,
    }

    /// <summary>The situation a dialogue line is for (G9's DialogueLines, used by G6 and G7).</summary>
    public enum DialogueContext
    {
        /// <summary>The player pressed E to talk.</summary>
        Greeting,
        /// <summary>Bumped into.</summary>
        Bumped,
        /// <summary>Pulled out of their car.</summary>
        Carjacked,
        /// <summary>Held up.</summary>
        Robbed,
        /// <summary>On the phone to the police.</summary>
        WitnessCalling,
        /// <summary>A cop giving an order.</summary>
        CopOrder,
    }

    /// <summary>Creates the first brain for peds G6 creates (G7 implements it). Without one, those peds get no brain.</summary>
    public interface IBrainFactory
    {
        /// <summary>
        /// A brain for <paramref name="ped"/> just created by G6 for <paramref name="origin"/>. <paramref name="cause"/>
        /// is the tag of the actor that caused it (the jacker, the player's car, the player), 0 none;
        /// <paramref name="spot"/> is meaningful only for <see cref="PedOrigin.InteriorOccupant"/>. Null = no brain.
        /// </summary>
        IPedBrain Create(GameWorld world, Ped ped, PedOrigin origin, int cause, NpcSpot spot);
    }

    /// <summary>Supplies dialogue lines (G9 implements it).</summary>
    public interface IDialogueHook
    {
        /// <summary>
        /// A line for a person in a context: <paramref name="citizen"/> is the CS1 citizen (0 for none),
        /// <paramref name="ped"/> the Game ped or null for an ambient citizen. Returns null for silence. Must return cached
        /// strings (no per-call allocation for a repeated citizen and context).
        /// </summary>
        string Line(GameWorld world, DialogueContext context, uint citizen, Ped ped);
    }

    /// <summary>Extension points later chunks install on <see cref="GameWorld.Hooks"/>. Every member may be null.</summary>
    public sealed class GameHooks
    {
        /// <summary>Brains for peds G6 creates (G7).</summary>
        public IBrainFactory Brains;
        /// <summary>Dialogue (G9). Without it, talking shows nothing but still emits <see cref="GameEventType.Talked"/>.</summary>
        public IDialogueHook Dialogue;
    }
}
