using System;
using System.Collections.Generic;
using GtaSkylines.Game.Profile;
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
    /// <item>Every new event (own cursor, starting at sequence 1, so events emitted before the first step count) is read in sequence:
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

        private const int SightingCapacity = 16;

        private readonly uint _seed;
        private readonly CrimeRecord[] _crimes = new CrimeRecord[CrimeCapacity];
        private int _crimeHead, _crimeCount, _nextCrimeId = 1;
        private readonly WitnessCall[] _calls = new WitnessCall[CallCapacity];
        private int _callCount;
        private readonly List<CrimeWitness> _witnesses = new List<CrimeWitness>(32);
        private readonly int[] _sightTags = new int[SightingCapacity];
        private readonly double[] _sightTimes = new double[SightingCapacity];
        private int _sightNext;
        private WantedEpisode _episode;
        private long _cursor = 1;

        /// <summary>Creates the system.</summary>
        /// <param name="services">G7 perception, call brain and firing.</param>
        /// <param name="seed">Seed for call lengths (use the session seed).</param>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public CrimeSystem(ICrimePedServices services, uint seed)
        {
            if (services == null) throw new ArgumentNullException("services");
            Services = services;
            Classifier = new CrimeClassifier();
            _seed = seed;
            for (int i = 0; i < SightingCapacity; i++) _sightTimes[i] = double.NegativeInfinity;
        }

        /// <inheritdoc />
        public int Order { get { return SystemOrder.Crime; } }

        /// <summary>The G7 services passed in.</summary>
        public ICrimePedServices Services { get; private set; }

        /// <summary>The classifier (holds reckless-driving memory).</summary>
        public CrimeClassifier Classifier { get; private set; }

        /// <summary>Star-rule memory of the current episode.</summary>
        public WantedEpisode Episode { get { return _episode; } }

        /// <summary>Records held (≤ <see cref="CrimeCapacity"/>).</summary>
        public int CrimeCount { get { return _crimeCount; } }

        /// <summary>A record by index, 0 = newest.</summary>
        /// <exception cref="ArgumentOutOfRangeException">index outside [0, CrimeCount).</exception>
        public CrimeRecord GetCrime(int index)
        {
            if (index < 0 || index >= _crimeCount) throw new ArgumentOutOfRangeException("index");
            return _crimes[SlotOf(index)];
        }

        /// <summary>The record with this id, if still held.</summary>
        public bool TryGetCrime(int id, out CrimeRecord crime)
        {
            int slot = FindSlot(id);
            crime = slot >= 0 ? _crimes[slot] : default(CrimeRecord);
            return slot >= 0;
        }

        /// <summary>Calls in progress.</summary>
        public int ActiveCallCount { get { return _callCount; } }

        /// <summary>An active call by index (creation order).</summary>
        /// <exception cref="ArgumentOutOfRangeException">index outside [0, ActiveCallCount).</exception>
        public WitnessCall GetCall(int index)
        {
            if (index < 0 || index >= _callCount) throw new ArgumentOutOfRangeException("index");
            return _calls[index];
        }

        /// <summary>
        /// A crime a cop witnessed directly (the Police system reports ResistingArrest this way): Known at once with
        /// SeenByCopTag = <paramref name="copTag"/>, applied as above (same-victim escalation included). Returns the crime
        /// id (the escalated or kept record's id when it merged into an earlier one).
        /// </summary>
        public int ReportByCop(GameWorld world, CrimeType type, Vec3 position, int victimTag, int copTag)
        {
            return Handle(world, type, position, victimTag, false, copTag, true, default(GameEvent));
        }

        /// <summary>
        /// A scripted crime (G9 missions) made known as if a cop had seen it: same as <see cref="ReportByCop"/> with no
        /// cop tag. Returns the crime id.
        /// </summary>
        public int ReportKnownCrime(GameWorld world, CrimeType type, Vec3 position, int victimTag)
        {
            return Handle(world, type, position, victimTag, false, 0, true, default(GameEvent));
        }

        /// <summary>True when the cop with <paramref name="copTag"/> saw a crime within the last <see cref="CrimeInSightSeconds"/> (recognition rule c).</summary>
        public bool CopSawCrimeRecently(GameWorld world, int copTag)
        {
            if (copTag == 0) return false;
            double now = world.Clock.GameSeconds;
            for (int i = 0; i < SightingCapacity; i++)
                if (_sightTags[i] == copTag && now - _sightTimes[i] <= CrimeInSightSeconds) return true;
            return false;
        }

        /// <inheritdoc />
        public void Step(GameWorld world, float dt)
        {
            WantedStatus wanted = world.Wanted;
            if (wanted.Stars <= 0) _episode = default(WantedEpisode);

            GameEvent e;
            while (world.Events.Read(ref _cursor, out e))
            {
                if (e.Type == GameEventType.CallCompleted) { CallCompleted(world, e); continue; }
                if (e.Type == GameEventType.CallInterrupted) { CallEnded(e.ActorA, (int)e.Value); continue; }
                int victim;
                bool loud;
                CrimeType type = Classifier.Classify(world, e, out victim, out loud);
                if (type != CrimeType.None) Handle(world, type, e.Position, victim, loud, 0, false, e);
            }

            UpdateCalls(world);

            bool inSafehouse = world.Interior.IsInside && world.Interior.Building != null && world.Interior.Building.OwnedByPlayer;
            wanted.PlayerHeat = WantedRules.DecayPlayerHeat(wanted.PlayerHeat, wanted.IsWanted, inSafehouse, dt);
            for (int d = 0; d < 256; d++)
            {
                float h = wanted.GetDistrictHeat((byte)d);
                if (h > 0f) wanted.SetDistrictHeat((byte)d, WantedRules.DecayDistrictHeat(h, dt));
            }
        }

        // ---- crimes ------------------------------------------------------------------------------------------------

        private int Handle(GameWorld world, CrimeType type, Vec3 position, int victim, bool loud, int copTag, bool byCop, GameEvent e)
        {
            double now = world.Clock.GameSeconds;
            float severity = CrimeClassifier.SeverityOf(type);
            int slot = victim != 0 ? FindEscalation(victim, now) : -1;
            if (slot >= 0)
            {
                if (severity <= _crimes[slot].Severity) return _crimes[slot].Id;
                _crimes[slot].Type = type;
                _crimes[slot].Severity = severity;
                _crimes[slot].Loud |= loud;
                if (_crimes[slot].State == CrimeState.Known) { ApplyKnown(world, slot, true, -1); return _crimes[slot].Id; }
                if (_crimes[slot].State == CrimeState.Reported) return _crimes[slot].Id;
            }
            else
            {
                slot = _crimeHead;
                _crimeHead = (_crimeHead + 1) % CrimeCapacity;
                if (_crimeCount < CrimeCapacity) _crimeCount++;
                CrimeRecord r = new CrimeRecord();
                r.Id = _nextCrimeId++;
                r.Type = type;
                r.Severity = severity;
                r.Position = position;
                r.Time = now;
                r.VictimTag = victim;
                r.VehicleId = world.Player != null ? world.Player.VehicleId : 0;
                r.District = world.City.DistrictAt(position.X, position.Z);
                r.Loud = loud;
                r.Description = Police.Recognition.Describe(world);
                _crimes[slot] = r;
            }

            if (byCop)
            {
                _crimes[slot].SeenByCopTag = copTag;
                if (copTag != 0) NoteSighting(copTag, now);
                ApplyKnown(world, slot, true, -1);
                return _crimes[slot].Id;
            }

            int seer = CopWitness(world, position, _crimes[slot].Loud, now);
            if (seer != 0)
            {
                _crimes[slot].SeenByCopTag = seer;
                ApplyKnown(world, slot, true, -1);
                return _crimes[slot].Id;
            }

            _crimes[slot].State = StartCalls(world, e, slot) > 0 ? CrimeState.Reported : CrimeState.Unwitnessed;
            return _crimes[slot].Id;
        }

        private int CopWitness(GameWorld world, Vec3 position, bool loud, double now)
        {
            int first = 0;
            IList<Ped> peds = world.Peds;
            for (int i = 0; i < peds.Count; i++)
            {
                Ped p = peds[i];
                if (p.Kind != PedKind.Cop || p.IsDead) continue;
                bool sees = Police.Recognition.CopSeesPoint(world, p, position)
                    || (loud && Vec3.Distance(CopPosition(world, p), position) <= CopHearingRange);
                if (!sees) continue;
                NoteSighting(p.Tag, now);
                if (first == 0) first = p.Tag;
            }
            return first;
        }

        private static Vec3 CopPosition(GameWorld world, Ped cop)
        {
            if (cop.VehicleId != 0)
            {
                Car car = world.FindCar(cop.VehicleId);
                if (car != null) return car.Position;
            }
            return cop.Position;
        }

        private void NoteSighting(int tag, double now)
        {
            _sightTags[_sightNext] = tag;
            _sightTimes[_sightNext] = now;
            _sightNext = (_sightNext + 1) % SightingCapacity;
        }

        private int StartCalls(GameWorld world, GameEvent e, int slot)
        {
            if (e.Seq == 0) return 0;
            e.Position = _crimes[slot].Position;
            Services.WitnessesOf(world, e, _witnesses);
            Ped player = world.Player;
            int started = 0;
            int lastTag = 0;
            float lastDist = -1f;
            while (started < MaxCallersPerCrime && _callCount < CallCapacity)
            {
                // next witness in (distance, tag) order after the previous pick
                int best = -1;
                for (int i = 0; i < _witnesses.Count; i++)
                {
                    CrimeWitness w = _witnesses[i];
                    if (!After(w.Distance, w.Tag, lastDist, lastTag)) continue;
                    if (best >= 0 && !After(_witnesses[best].Distance, _witnesses[best].Tag, w.Distance, w.Tag)) continue;
                    best = i;
                }
                if (best < 0) break;
                CrimeWitness pick = _witnesses[best];
                lastDist = pick.Distance;
                lastTag = pick.Tag;
                if (!WorldTag.IsPerson(pick.Tag) || pick.Tag == player.Tag) continue;
                Ped ped = world.PedFromTag(pick.Tag, true, player.Tag);
                if (ped == null || ped.IsDead || ped.Kind == PedKind.Cop || ped.Kind == PedKind.Player || IsCalling(ped.Id)) continue;
                float secs = new Rng(Rng.Seed(_seed, (uint)_crimes[slot].Id, (uint)started)).Range(CallMinSeconds, CallMaxSeconds);
                ped.Brain = Services.CreateCallPolice(world, ped, _crimes[slot].Id, secs);
                WitnessCall c = new WitnessCall();
                c.Active = true;
                c.CrimeId = _crimes[slot].Id;
                c.WitnessPedId = ped.Id;
                c.CallSeconds = secs;
                c.StartTime = world.Clock.GameSeconds;
                c.LastSeenPlayer = player.Position;
                c.LastSeenTime = world.Clock.GameSeconds;
                c.LastSeenDescription = _crimes[slot].Description;
                _calls[_callCount++] = c;
                started++;
            }
            return started;
        }

        /// <summary>True when (d, tag) sorts strictly after (d0, tag0); d0 &lt; 0 means "before everything".</summary>
        private static bool After(float d, int tag, float d0, int tag0)
        {
            if (d0 < 0f) return true;
            return d > d0 || (d == d0 && tag > tag0);
        }

        private bool IsCalling(int pedId)
        {
            for (int i = 0; i < _callCount; i++) if (_calls[i].WitnessPedId == pedId) return true;
            return false;
        }

        private void ApplyKnown(GameWorld world, int slot, bool byCop, int call)
        {
            WantedStatus wanted = world.Wanted;
            CrimeRecord r = _crimes[slot];
            float delta = r.Severity - r.AppliedSeverity;
            bool first = r.State != CrimeState.Known;
            wanted.Stars = WantedRules.StarsAfterCrime(wanted.Stars, r.Severity, r.Type == CrimeType.CopKilled, ref _episode);
            wanted.PlayerHeat = WantedRules.PlayerHeatAfterCrime(wanted.PlayerHeat, delta);
            wanted.SetDistrictHeat(r.District, WantedRules.DistrictHeatAfterCrime(wanted.GetDistrictHeat(r.District), delta));
            _crimes[slot].AppliedSeverity = r.Severity;
            if (r.Type == CrimeType.AssaultOnCop || r.Type == CrimeType.CopKilled) _episode.CopAttacked = true;
            _crimes[slot].State = CrimeState.Known;

            double now = world.Clock.GameSeconds;
            if (byCop)
            {
                wanted.Description = Police.Recognition.Describe(world);
                wanted.LastKnownPosition = world.Player.Position;
                wanted.SecondsSinceSeen = 0f;
                wanted.HasLastKnownPosition = true;
            }
            else
            {
                WitnessCall c = _calls[call];
                float since = (float)(now - c.LastSeenTime);
                if (!wanted.HasLastKnownPosition || since < wanted.SecondsSinceSeen)
                {
                    wanted.Description = c.LastSeenDescription;
                    wanted.LastKnownPosition = c.LastSeenPlayer;
                    wanted.SecondsSinceSeen = since;
                    wanted.HasLastKnownPosition = true;
                }
            }
            if (first) world.Profile.AddStat(StatNames.Crimes, 1);
            world.Emit(GameEventType.CrimeKnown, r.Position, world.Player.Tag, r.VictimTag, wanted.Stars, null);
        }

        private int FindEscalation(int victim, double now)
        {
            for (int i = 0; i < _crimeCount; i++)
            {
                int slot = SlotOf(i);
                if (now - _crimes[slot].Time > CrimeClassifier.EscalationSeconds) break;
                if (_crimes[slot].VictimTag == victim) return slot;
            }
            return -1;
        }

        private int SlotOf(int index)
        {
            return (_crimeHead - 1 - index + 2 * CrimeCapacity) % CrimeCapacity;
        }

        private int FindSlot(int id)
        {
            for (int i = 0; i < _crimeCount; i++)
            {
                int slot = SlotOf(i);
                if (_crimes[slot].Id == id) return slot;
            }
            return -1;
        }

        // ---- calls -------------------------------------------------------------------------------------------------

        private void CallCompleted(GameWorld world, GameEvent e)
        {
            int crimeId = (int)e.Value;
            int pedId = WorldTag.KindOf(e.ActorA) == TagKind.Ped ? WorldTag.IdOf(e.ActorA) : 0;
            int call = -1;
            for (int i = 0; i < _callCount; i++)
                if (_calls[i].CrimeId == crimeId && _calls[i].WitnessPedId == pedId) { call = i; break; }
            if (call < 0) return;
            int slot = FindSlot(crimeId);
            if (slot >= 0 && _crimes[slot].State == CrimeState.Reported) ApplyKnown(world, slot, false, call);
            for (int i = _callCount - 1; i >= 0; i--)
                if (_calls[i].CrimeId == crimeId) RemoveCall(i);
        }

        private void CallEnded(int witnessTag, int crimeId)
        {
            int pedId = WorldTag.KindOf(witnessTag) == TagKind.Ped ? WorldTag.IdOf(witnessTag) : 0;
            for (int i = 0; i < _callCount; i++)
                if (_calls[i].CrimeId == crimeId && _calls[i].WitnessPedId == pedId) { DropCall(i); return; }
        }

        private void DropCall(int index)
        {
            int crimeId = _calls[index].CrimeId;
            RemoveCall(index);
            for (int i = 0; i < _callCount; i++) if (_calls[i].CrimeId == crimeId) return;
            int slot = FindSlot(crimeId);
            if (slot >= 0 && _crimes[slot].State == CrimeState.Reported) _crimes[slot].State = CrimeState.Lost;
        }

        private void RemoveCall(int index)
        {
            for (int i = index; i < _callCount - 1; i++) _calls[i] = _calls[i + 1];
            _callCount--;
            _calls[_callCount] = default(WitnessCall);
        }

        private void UpdateCalls(GameWorld world)
        {
            Ped player = world.Player;
            if (player == null) return;
            Vec3 chest = player.Position + new Vec3(0f, 1.2f, 0f);
            for (int i = _callCount - 1; i >= 0; i--)
            {
                Ped w = world.FindPed(_calls[i].WitnessPedId);
                if (w == null || w.IsDead) { DropCall(i); continue; }
                if (Services.CanSee(world, w, chest))
                {
                    _calls[i].LastSeenPlayer = player.Position;
                    _calls[i].LastSeenTime = world.Clock.GameSeconds;
                    _calls[i].LastSeenDescription = Police.Recognition.Describe(world);
                }
                else if (Vec3.DistanceXZ(w.Position, player.Position) > CallLostDistance)
                {
                    DropCall(i);
                }
            }
        }
    }
}
