using System;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.World
{
    /// <summary>
    /// What happened. Actors are <see cref="WorldTag"/> values (0 = none). "Player" below means the player ped's tag.
    /// Unless stated, <see cref="GameEvent.Position"/> is where it happened, <see cref="GameEvent.Value"/> is 0 and
    /// <see cref="GameEvent.Text"/> is null. Emitter in brackets. Numeric values are stable (append only).
    /// </summary>
    public enum GameEventType
    {
        /// <summary>A walking ped or the player brushed into a person. A: who moved into B; B: the person. Value: closing speed m/s. [G6 for the player on foot, G7]</summary>
        Bumped = 0,
        /// <summary>A deliberate push. A: shover; B: shoved. [G7]</summary>
        Shove = 1,
        /// <summary>A melee hit. A: attacker; B: victim (ped or ambient citizen tag before promotion); Value: damage; Text: weapon name. [G7]</summary>
        Punched = 2,
        /// <summary>A hitscan shot (tracer, muzzle flash). A: shooter; B: what was hit (0 static or nothing); Position: muzzle origin; Value: damage dealt. End point: <see cref="GameEvent.Position2"/>. [G7]</summary>
        Shot = 3,
        /// <summary>Gun noise heard around Position. A: shooter; Value: hearing radius m. [G7]</summary>
        Gunshot = 4,
        /// <summary>An explosion. A: the exploding car's tag (or the cause); Value: damage radius m. [G6 for car explosions]</summary>
        Explosion = 5,
        /// <summary>A ped went down. A: cause (car tag, attacker ped); B: the ped. Value: impact speed m/s for vehicle hits, else 0. [G7, including for <see cref="PedHitByVehicle"/> events from G6]</summary>
        PedKnockedDown = 6,
        /// <summary>A ped's health fell below 35 (once per ped). A: cause; B: the ped. [G7]</summary>
        PedInjured = 7,
        /// <summary>A ped died. A: cause (the killer's ped tag, a car tag, or 0); B: the ped. [G7; G6 for the player in an exploding car]</summary>
        PedKilled = 8,
        /// <summary>A driver was pulled out. A: the jacker; B: the car; Value: the driver ped id (0 if none was created). [G6]</summary>
        CarJacked = 9,
        /// <summary>A parked car was taken. A: the taker; B: the new car. Value: 1 when the car is a police vehicle. [G6]</summary>
        CarStolen = 10,
        /// <summary>A Game car lost health. A: cause (other actor tag, 0 for static); B: the car; Value: health lost. [G6 for impacts, G7 for shots]</summary>
        CarDamaged = 11,
        /// <summary>A Game car exploded. A: last driver tag or 0; B: the car. [G6]</summary>
        CarDestroyed = 12,
        /// <summary>The player sat in a car's driver seat. A: player; B: car. [G6]</summary>
        PlayerEnteredCar = 13,
        /// <summary>The player left a car. A: player; B: car; Value: 1 for a bail-out at speed. [G6]</summary>
        PlayerExitedCar = 14,
        /// <summary>The player finished entering an interior. A: player; Value: building id; Position: the outside door. [G6]</summary>
        EnteredInterior = 15,
        /// <summary>The player is back outside. A: player; Value: building id; Position: the outside door. [G6]</summary>
        ExitedInterior = 16,
        /// <summary>A shop or service purchase succeeded. A: player; Value: price paid; Text: the <c>ShopItem.Id</c>. [G6]</summary>
        Purchase = 17,
        /// <summary>The player's health reached 0. A: cause; B: player. [G6]</summary>
        PlayerDied = 18,
        /// <summary>The player was busted. A: the arresting cop tag (0 unknown); B: player. [G6, on the request from G8]</summary>
        PlayerArrested = 19,
        /// <summary>The player entered a private interior they do not own (homes, police/office back rooms). A: player; Value: building id. [G6]</summary>
        Trespass = 20,
        /// <summary>The player's car hit or nearly hit traffic or peds. A: player's car; B: the other actor; Value: speed m/s. [G6]</summary>
        RecklessDriving = 21,
        /// <summary>A ped raised its hands. A: the threat (player); B: the ped. Also emitted with B = player when the player holds HandsUp. [G6 for the player, G7]</summary>
        HandsUp = 22,
        /// <summary>A Game car hit a person. A: car; B: the person (ped tag, or ambient citizen tag); Value: relative speed m/s; Position: contact point. G7 turns it into knock-down/injury/death. [G6]</summary>
        PedHitByVehicle = 23,
        /// <summary>The player took fall damage. A: player; Value: health lost ((v − 9) × 9). [G6]</summary>
        Fell = 24,
        /// <summary>A ped started aiming at another. A: aimer; B: target. [G7]</summary>
        AimedAt = 25,
        /// <summary>A shop clerk was held up. A: player; B: clerk; Value: building id. [G7]</summary>
        Robbery = 26,
        /// <summary>A witness started a police call. A: witness; Value: crime id. [G7 brain, requested by G8]</summary>
        CallStarted = 27,
        /// <summary>A witness finished a police call. A: witness; Value: crime id. [G7]</summary>
        CallCompleted = 28,
        /// <summary>A witness call was interrupted. A: witness; Value: crime id. [G7]</summary>
        CallInterrupted = 29,
        /// <summary>The player talked to someone. A: player; B: the person; Text: the line shown. [G6]</summary>
        Talked = 30,
        /// <summary>The player used an interaction a system added (<c>InteriorState.AddCustomInteraction</c>). A: player; Value: the custom id. [G6]</summary>
        CustomInteraction = 31,
        /// <summary>The player's car was resprayed: description changed. A: player; B: car; Value: new colour 0xRRGGBB. [G6]</summary>
        Resprayed = 32,
        /// <summary>The player's outfit changed. A: player; Value: new model id. [G6]</summary>
        OutfitChanged = 33,
        /// <summary>The player respawned after Wasted or Busted. A: player; Value: 0 Wasted, 1 Busted; Position: the respawn door. [G6]</summary>
        PlayerRespawned = 34,
        /// <summary>The player slept in a safehouse. A: player; Value: hours skipped. [G6]</summary>
        Slept = 35,
        /// <summary>A Game car hit another vehicle. A: car; B: other vehicle tag; Value: relative speed m/s. [G6]</summary>
        CarCollision = 36,
        /// <summary>Police units entered the interior the player is in. Value: building id. [G8]</summary>
        CopsEnteredInterior = 37,
    }

    /// <summary>One entry of <see cref="GameEvents"/>. Strings in <see cref="Text"/> must not be built per event (constants or cached).</summary>
    public struct GameEvent
    {
        /// <summary>Sequence number, starting at 1 for the session's first event, +1 per event.</summary>
        public long Seq;
        /// <summary>Type.</summary>
        public GameEventType Type;
        /// <summary><c>GameClock.GameSeconds</c> when emitted.</summary>
        public double GameTime;
        /// <summary>World position (see the type).</summary>
        public Vec3 Position;
        /// <summary>Second position: the end point for <see cref="GameEventType.Shot"/>, else zero.</summary>
        public Vec3 Position2;
        /// <summary>First actor, a <see cref="WorldTag"/> (0 none).</summary>
        public int ActorA;
        /// <summary>Second actor, a <see cref="WorldTag"/> (0 none).</summary>
        public int ActorB;
        /// <summary>Numeric payload (see the type).</summary>
        public float Value;
        /// <summary>Text payload (see the type), or null.</summary>
        public string Text;
    }

    /// <summary>
    /// Append-only ring buffer of the session's events, capacity <see cref="Capacity"/>. Each consumer keeps its own
    /// cursor (the next sequence number it wants) and reads with <see cref="Read"/>, so events emitted by any system in
    /// any step are seen by every consumer exactly once while the consumer reads at least once per 256 events.
    /// Emission and reading allocate nothing.
    /// </summary>
    public sealed class GameEvents
    {
        /// <summary>Entries kept.</summary>
        public const int Capacity = 256;

        private readonly GameEvent[] _ring = new GameEvent[Capacity];
        private long _next = 1;

        /// <summary>Sequence number the next emitted event will get (1 before any). A new consumer starts its cursor here to see only future events.</summary>
        public long NextSeq { get { return _next; } }

        /// <summary>Oldest sequence number still held (= NextSeq when empty).</summary>
        public long OldestSeq { get { return Math.Max(1L, _next - Capacity); } }

        /// <summary>
        /// Appends an event, stamping <see cref="GameEvent.Seq"/> (and overwriting the oldest when full); returns its
        /// sequence number. <see cref="GameEvent.GameTime"/> is taken from the argument as given.
        /// </summary>
        public long Emit(GameEvent e)
        {
            e.Seq = _next;
            _ring[(int)(_next % Capacity)] = e;
            return _next++;
        }

        /// <summary>
        /// Reads the event at <paramref name="cursor"/> and advances the cursor; false (cursor unchanged) when the cursor
        /// has reached <see cref="NextSeq"/>. A cursor older than <see cref="OldestSeq"/> first jumps to OldestSeq (the
        /// overwritten events are lost; <see cref="Missed"/> counts them).
        /// </summary>
        public bool Read(ref long cursor, out GameEvent e)
        {
            long oldest = OldestSeq;
            if (cursor < oldest)
            {
                Missed += oldest - cursor;
                cursor = oldest;
            }
            if (cursor >= _next)
            {
                e = default(GameEvent);
                return false;
            }
            e = _ring[(int)(cursor % Capacity)];
            cursor++;
            return true;
        }

        /// <summary>Total events consumers skipped because they fell more than <see cref="Capacity"/> behind (debug).</summary>
        public long Missed { get; private set; }

    }
}
