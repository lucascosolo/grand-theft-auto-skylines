using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Crime
{
    /// <summary>What the player did (F-HEAT-1). Severities are in <see cref="CrimeClassifier.SeverityOf"/>. Numeric values are stable.</summary>
    public enum CrimeType
    {
        /// <summary>Not a crime.</summary>
        None = 0,
        /// <summary>Took a parked civilian car (CarStolen, Value 0). Severity 1.</summary>
        CarStolen = 1,
        /// <summary>Pulled a driver out of a civilian car (CarJacked). Severity 2.</summary>
        CarJacked = 2,
        /// <summary>Punched or shoved a civilian (Punched, Shove). Severity 1.</summary>
        Assault = 3,
        /// <summary>Knocked a civilian down with the player's car (PedKnockedDown, A = player's car). Severity 1.</summary>
        PedKnockedDown = 4,
        /// <summary>Injured a civilian with the player's car (PedInjured, A = player's car). Severity 2.</summary>
        HitAndRun = 5,
        /// <summary>Injured a civilian by other means (PedInjured, A = player). Severity 2.</summary>
        PedInjured = 6,
        /// <summary>Killed a civilian (PedKilled, A = player or player's car). Severity 3.</summary>
        PedKilled = 7,
        /// <summary>Fired a weapon in public (Gunshot, A = player). Severity 1.</summary>
        Gunshot = 8,
        /// <summary>Shot at a civilian (Shot, A = player, B a person who is not a cop). Severity 2.</summary>
        ShotAtPed = 9,
        /// <summary>Caused an explosion (Explosion, A = player or a car with Owner Player). Severity 3.</summary>
        Explosion = 10,
        /// <summary>Entered a private interior (Trespass). Severity 1.</summary>
        Trespass = 11,
        /// <summary>Held up a shop clerk (Robbery). Severity 3.</summary>
        Robbery = 12,
        /// <summary>Third RecklessDriving event within 20 s (see <see cref="CrimeClassifier"/>). Severity 1.</summary>
        RecklessDriving = 13,
        /// <summary>Punched, shoved, shot at, knocked down or injured a cop. Severity 3.</summary>
        AssaultOnCop = 14,
        /// <summary>Killed a cop. Severity 4.</summary>
        CopKilled = 15,
        /// <summary>Fled a cop's arrest attempt (reported by the Police system through <see cref="CrimeSystem.ReportByCop"/>). Severity 2.</summary>
        ResistingArrest = 16,
        /// <summary>Took a police car (CarStolen with Value 1, or CarJacked of a car with Owner Police). Severity 3.</summary>
        PoliceCarStolen = 17,
        /// <summary>Aimed a weapon (not fists) at a civilian (AimedAt, A = player). Severity 1. Added from DESIGN F-HEAT-1 ("threatening").</summary>
        Threatening = 18,
    }

    /// <summary>Where a crime is in the reporting pipeline (F-HEAT-2).</summary>
    public enum CrimeState
    {
        /// <summary>Slot unused.</summary>
        Free = 0,
        /// <summary>Only civilian witnesses saw it; at least one call is in progress.</summary>
        Reported = 1,
        /// <summary>The police know it (a cop saw or heard it, or a call completed). Stars and heat were applied once.</summary>
        Known = 2,
        /// <summary>Every call was interrupted or lost. Never becomes known.</summary>
        Lost = 3,
        /// <summary>Nobody saw it. Adds nothing.</summary>
        Unwitnessed = 4,
    }

    /// <summary>
    /// One crime the player committed. Kept in <see cref="CrimeSystem"/>'s fixed ring of
    /// <see cref="CrimeSystem.CrimeCapacity"/> records (oldest overwritten); value type, no allocation.
    /// </summary>
    public struct CrimeRecord
    {
        /// <summary>Session-unique id, starting at 1. This is the Value of CallStarted/CallCompleted/CallInterrupted events.</summary>
        public int Id;
        /// <summary>Type.</summary>
        public CrimeType Type;
        /// <summary>Severity (0.5-4) from <see cref="CrimeClassifier.SeverityOf"/>; may be raised in place by an escalation on the same victim.</summary>
        public float Severity;
        /// <summary>Where it happened (the event position).</summary>
        public Vec3 Position;
        /// <summary><c>GameClock.GameSeconds</c> of the event.</summary>
        public double Time;
        /// <summary>Tag of the victim (person or car), 0 none.</summary>
        public int VictimTag;
        /// <summary>Id of the player's car involved (driven, stolen or jacked), 0 none.</summary>
        public int VehicleId;
        /// <summary>CS1 district at <see cref="Position"/> (<c>ICity.DistrictAt</c>).</summary>
        public byte District;
        /// <summary>True for crimes heard within <see cref="CrimeSystem.CopHearingRange"/> (Gunshot, ShotAtPed, Explosion, and AssaultOnCop/CopKilled by gunfire).</summary>
        public bool Loud;
        /// <summary>Pipeline state.</summary>
        public CrimeState State;
        /// <summary>The player's description at the moment of the crime.</summary>
        public SuspectDescription Description;
        /// <summary>Tag of the cop that saw it (State Known by sight), 0 otherwise.</summary>
        public int SeenByCopTag;
        /// <summary>Severity already applied to heat (so an escalation adds only the difference).</summary>
        public float AppliedSeverity;
    }

    /// <summary>A person G7's perception says could see an event (<c>Perception.WitnessesOf</c>, through <see cref="ICrimePedServices"/>).</summary>
    public struct CrimeWitness
    {
        /// <summary>Game ped tag or ambient citizen tag (<see cref="WorldTag"/>).</summary>
        public int Tag;
        /// <summary>Where the witness stands.</summary>
        public Vec3 Position;
        /// <summary>Distance to the event, metres (callers are picked nearest first, ties by lower tag).</summary>
        public float Distance;
    }

    /// <summary>A witness call in progress (F-HEAT-2). Fixed array in <see cref="CrimeSystem"/>; value type.</summary>
    public struct WitnessCall
    {
        /// <summary>True while the call is tracked.</summary>
        public bool Active;
        /// <summary>The crime it reports.</summary>
        public int CrimeId;
        /// <summary>The calling ped's id (promoted when it was an ambient citizen).</summary>
        public int WitnessPedId;
        /// <summary>Seeded call length in [4, 10) s: <c>new Rng(Rng.Seed(world seed, crimeId, callerIndex)).Range(4, 10)</c>; passed to the CallPolice brain.</summary>
        public float CallSeconds;
        /// <summary>Game seconds the call started.</summary>
        public double StartTime;
        /// <summary>Where the witness last saw the player (updated every step the witness can see the player).</summary>
        public Vec3 LastSeenPlayer;
        /// <summary>Game seconds of that sighting.</summary>
        public double LastSeenTime;
        /// <summary>The player's description at that sighting.</summary>
        public SuspectDescription LastSeenDescription;
    }
}
