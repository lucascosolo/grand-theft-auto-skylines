using System;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Crime
{
    /// <summary>
    /// Crimes, witnesses, stars and heat (F-HEAT-1/2/3/6), order <see cref="SystemOrder.Crime"/> (60). Writes
    /// <see cref="WantedStatus"/> Stars, Description, LastKnownPosition, HasLastKnownPosition and SecondsSinceSeen (only
    /// when a crime becomes known), and the heat (profile). Evading, the search zone and clearing stars belong to the
    /// Police system.
    /// <para>Each step, in this order:</para>
    /// <list type="number">
    /// <item>If <c>Wanted.Stars</c> is 0 the <see cref="Episode"/> is reset.</item>
    /// <item>Every new event (own cursor, starting at <c>Events.NextSeq</c> on the first step) is read in sequence:
    /// CallCompleted/CallInterrupted (Value = crime id) update calls; anything else goes through the
    /// <see cref="CrimeClassifier"/>. A new crime gets the next id, the district at its position and the player's
    /// current description, then:
    /// <list type="bullet">
    /// <item>Cop witness: any living Game ped of kind Cop (on foot, or seated in a car: eye = car centre + 0.6 m up,
    /// facing the car's yaw) for which <c>Police.Recognition.CopSeesPoint</c> is true for the crime position, or, for a
    /// Loud crime, any cop within <see cref="CopHearingRange"/> → Known at once (see "apply" below);
    /// SeenByCopTag = the first such cop in <c>world.Peds</c> order.</item>
    /// <item>Otherwise <see cref="ICrimePedServices.WitnessesOf"/>; the nearest <see cref="MaxCallersPerCrime"/> (ties:
    /// lower tag) become callers: ambient ones are promoted with <c>world.PedFromTag(tag, true, player tag)</c>; each gets
    /// <see cref="ICrimePedServices.CreateCallPolice"/> with CallSeconds = <c>new Rng(Rng.Seed(seed, crimeId,
    /// callerIndex)).Range(4f, 10f)</c> and a <see cref="WitnessCall"/> (LastSeenPlayer = the player's position,
    /// LastSeenTime = now). State Reported. No witness → Unwitnessed (nothing else happens).</item>
    /// </list></item>
    /// <item>Each active call: witness removed or dead → dropped; if <see cref="ICrimePedServices.CanSee"/>(witness, player
    /// position + 1.2 m) → LastSeen* updated; else if the witness is farther than <see cref="CallLostDistance"/> from
    /// the player → dropped. A crime whose last call is dropped or interrupted becomes Lost. The first CallCompleted of a
    /// Reported crime makes it Known with that call's LastSeen* values.</item>
    /// <item>Heat decay (<see cref="WantedRules.DecayPlayerHeat"/>, and <see cref="WantedRules.DecayDistrictHeat"/> for
    /// every district 0-255 with heat &gt; 0).</item>
    /// </list>
    /// <para>
    /// Apply (once per crime, when it becomes Known): Stars = <see cref="WantedRules.StarsAfterCrime"/>; player heat and
    /// the crime's district heat raised by the crime's severity (an escalation in place raises them by the difference
    /// only); Description = the crime's description (by a cop) or the call's LastSeenDescription; LastKnownPosition = the
    /// player's position now (by a cop; SecondsSinceSeen = 0) or the call's LastSeenPlayer (SecondsSinceSeen = now −
    /// LastSeenTime); HasLastKnownPosition = true; profile stat <c>crimes</c> += 1.
    /// </para>
    /// Allocates nothing in steady state: crimes, calls and witness lists are fixed-capacity and reused.
    /// </summary>
    public sealed class CrimeSystem : IGameSystem
    {
        /// <summary>Crime records kept (ring; the oldest is overwritten).</summary>
        public const int CrimeCapacity = 32;
        /// <summary>Calls tracked at once; a crime that finds no free slot uses fewer callers.</summary>
        public const int CallCapacity = 8;
        /// <summary>Most civilian callers per crime.</summary>
        public const int MaxCallersPerCrime = 2;
        /// <summary>Shortest call, seconds.</summary>
        public const float CallMinSeconds = 4f;
        /// <summary>Longest call (exclusive), seconds.</summary>
        public const float CallMaxSeconds = 10f;
        /// <summary>A caller who cannot see the player and is farther than this loses the call, metres.</summary>
        public const float CallLostDistance = 60f;
        /// <summary>Cops hear Loud crimes within this distance regardless of sight, metres.</summary>
        public const float CopHearingRange = 20f;
        /// <summary>How long a cop's sighting of a crime counts as "commits a crime in sight" for recognition, seconds.</summary>
        public const float CrimeInSightSeconds = 1f;

        /// <summary>Creates the system.</summary>
        /// <param name="services">G7 perception, call brain and firing.</param>
        /// <param name="seed">Seed for call lengths (use the session seed).</param>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public CrimeSystem(ICrimePedServices services, uint seed)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public int Order { get { return SystemOrder.Crime; } }

        /// <summary>The G7 services passed in.</summary>
        public ICrimePedServices Services { get; private set; }

        /// <summary>The classifier (holds reckless-driving memory).</summary>
        public CrimeClassifier Classifier { get; private set; }

        /// <summary>Star-rule memory of the current episode.</summary>
        public WantedEpisode Episode { get { throw new NotImplementedException(); } }

        /// <summary>Records held (≤ <see cref="CrimeCapacity"/>).</summary>
        public int CrimeCount { get { throw new NotImplementedException(); } }

        /// <summary>A record by index, 0 = newest.</summary>
        /// <exception cref="ArgumentOutOfRangeException">index outside [0, CrimeCount).</exception>
        public CrimeRecord GetCrime(int index)
        {
            throw new NotImplementedException();
        }

        /// <summary>The record with this id, if still held.</summary>
        public bool TryGetCrime(int id, out CrimeRecord crime)
        {
            throw new NotImplementedException();
        }

        /// <summary>Calls in progress.</summary>
        public int ActiveCallCount { get { throw new NotImplementedException(); } }

        /// <summary>An active call by index (creation order).</summary>
        /// <exception cref="ArgumentOutOfRangeException">index outside [0, ActiveCallCount).</exception>
        public WitnessCall GetCall(int index)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// A crime a cop witnessed directly (the Police system reports ResistingArrest this way): Known at once with
        /// SeenByCopTag = <paramref name="copTag"/>, applied as above. Returns the crime id.
        /// </summary>
        public int ReportByCop(GameWorld world, CrimeType type, Vec3 position, int victimTag, int copTag)
        {
            throw new NotImplementedException();
        }

        /// <summary>True when the cop with <paramref name="copTag"/> saw a crime within the last <see cref="CrimeInSightSeconds"/> (recognition rule c).</summary>
        public bool CopSawCrimeRecently(GameWorld world, int copTag)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void Step(GameWorld world, float dt)
        {
            throw new NotImplementedException();
        }
    }
}
