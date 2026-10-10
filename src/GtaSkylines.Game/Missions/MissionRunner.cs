using System;
using GtaSkylines.Game.Dialogue;
using GtaSkylines.Game.Phone;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Missions
{
    /// <summary>What the runner is doing.</summary>
    public enum RunnerState
    {
        /// <summary>No mission or job: markers are live.</summary>
        Idle,
        /// <summary>A mission runs.</summary>
        Mission,
        /// <summary>A side job shift runs.</summary>
        SideJob,
    }

    /// <summary>Outcome of <see cref="MissionRunner.TryStartSideJob"/>.</summary>
    public enum SideJobStartResult
    {
        /// <summary>The shift started.</summary>
        Started,
        /// <summary>The player is not driving a vehicle the job accepts (or, for a race, is not at the race marker).</summary>
        WrongVehicle,
        /// <summary>A mission is running.</summary>
        MissionRunning,
        /// <summary>A side job is already running.</summary>
        AlreadyRunning,
    }

    /// <summary>
    /// The G9 system (order <see cref="SystemOrder.Missions"/>): missions, side jobs, phone, Chirper. One instance per
    /// session, added with <c>GameSession.AddSystem</c>. The constructor installs <see cref="Dialogue"/> as
    /// <c>world.Hooks.Dialogue</c>.
    /// <para>Each <see cref="Step"/>, in order:</para>
    /// <list type="number">
    /// <item>First step only: <see cref="Campaign"/> = <c>CampaignGenerator.Generate(world.City, world.Roads,
    /// world.Profile, seed)</c>; contacts unlocked for every contact that has a completed or available mission; the
    /// unlock text of each available mission is sent if not already in the phone; the event cursor starts at
    /// <c>Events.NextSeq</c>.</item>
    /// <item>Events since the last step are copied into <see cref="Context"/> (one cursor).</item>
    /// <item>Phone input (<see cref="PhoneService.HandleInput"/>), which may start or end side jobs.</item>
    /// <item>Mission: always-on fails (PlayerDied → <see cref="FailReasons.Wasted"/>, PlayerArrested →
    /// <see cref="FailReasons.Busted"/>), then the definition's fail conditions, then the current step; Done → next
    /// step's actors spawn, it begins and is stepped once more only on the following fixed step. After the last step:
    /// pass. Pass: <c>Wallet.Earn(Reward.Cash, Reward.Reason)</c> once, <c>Profile.CompleteMission(id)</c>,
    /// <c>AddStat(jobs_done, 1)</c>, big screen MissionPassed (reward, <see cref="BigScreenSeconds"/>), Chirper post
    /// (MissionPassed), the PassedText and the unlock texts of newly available missions sent through the phone, and newly
    /// needed contacts unlocked. Fail: big screen MissionFailed (reason) unless the reason is Wasted or Busted (G6 shows
    /// those screens), no reward. Either way: current step ended, actors removed, <c>world.Mission.Clear()</c>, a
    /// suspended side job resumed (if the player still drives an accepted vehicle, else ended).</item>
    /// <item>Side job: see <see cref="SideJob"/>; leaving the job's vehicle (PlayerExitedCar for it) ends the shift with
    /// <see cref="FailReasons.LeftJobVehicle"/> unless a step needs the player on foot (GoTo OnFoot).</item>
    /// <item>Idle: mission markers (one per available mission: MissionStart blip and a <see cref="MarkerColour"/>
    /// world marker of <see cref="MarkerRadius"/> at <c>StartMarker</c>; ids 1-99) and the race marker (Checkpoint blip
    /// at <see cref="RaceMarker"/>, id 100) are kept in <c>world.Mission</c>; the player entering a mission marker
    /// (XZ within the radius, on foot or in a car below 3 m/s, not inside an interior, the marker armed) starts it
    /// (<c>CampaignGenerator.Rebuild</c> then <see cref="TryStartMission"/>). A marker is disarmed while the player is
    /// within radius + 2 m after a mission from it ended, so it does not restart at once. Driving into the race marker
    /// starts a StreetRace shift.</item>
    /// <item>Chirper observes events and wanted changes (<see cref="Chirper.Observe"/>).</item>
    /// <item>The phone model is refreshed (<see cref="PhoneService.RefreshModel"/>).</item>
    /// </list>
    /// Steady-state stepping (no state change) allocates nothing.
    /// </summary>
    public sealed class MissionRunner : IGameSystem
    {
        /// <summary>Start marker radius, metres.</summary>
        public const float MarkerRadius = 1.5f;
        /// <summary>Start marker colour (yellow).</summary>
        public const int MarkerColour = 0xFFD200;
        /// <summary>Seconds of the passed/failed big screens.</summary>
        public const float BigScreenSeconds = 4f;
        /// <summary>Race marker radius, metres.</summary>
        public const float RaceMarkerRadius = 4f;

        /// <summary>Creates the runner and installs the dialogue hook on <paramref name="world"/>.</summary>
        /// <exception cref="ArgumentNullException">world is null.</exception>
        public MissionRunner(GameWorld world, uint seed)
        {
            if (world == null) throw new ArgumentNullException("world");
            Seed = seed;
            Context = new MissionContext();
            Dialogue = new DialogueLines();
            Phone = new PhoneService();
            Chirper = new Chirper(seed);
            world.Hooks.Dialogue = Dialogue;
        }

        /// <inheritdoc />
        public int Order { get { return SystemOrder.Missions; } }

        /// <summary>The seed of every generated choice.</summary>
        public uint Seed { get; private set; }

        /// <summary>The campaign (null before the first step).</summary>
        public Campaign Campaign { get; private set; }

        /// <summary>Place finder over the world's city (null before the first step).</summary>
        public PlaceFinder Places { get; private set; }

        /// <summary>Shared step context.</summary>
        public MissionContext Context { get; private set; }

        /// <summary>The phone.</summary>
        public PhoneService Phone { get; private set; }

        /// <summary>The Chirper feed.</summary>
        public Chirper Chirper { get; private set; }

        /// <summary>The dialogue hook.</summary>
        public DialogueLines Dialogue { get; private set; }

        /// <summary>What runs now.</summary>
        public RunnerState State { get; private set; }

        /// <summary>The running mission, or null.</summary>
        public MissionDefinition ActiveMission { get; private set; }

        /// <summary>The running (or suspended) side job, or null.</summary>
        public SideJob ActiveJob { get; private set; }

        /// <summary>True while <see cref="ActiveJob"/> is paused by a mission.</summary>
        public bool JobSuspended { get; private set; }

        /// <summary>Reason of the last mission or job failure, or null.</summary>
        public string LastFailReason { get; private set; }

        /// <summary>Street-race marker: the midpoint of the longest road segment (RoadGraph), chosen on the first step.</summary>
        public Mathx.Vec3 RaceMarker { get; private set; }

        /// <inheritdoc />
        public void Step(GameWorld world, float dt)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Starts an available mission now (markers removed, briefing queued, title set, a running side job suspended).
        /// False when a mission runs, the id is unknown or not available, or the campaign is not generated yet.
        /// </summary>
        public bool TryStartMission(GameWorld world, string id)
        {
            throw new NotImplementedException();
        }

        /// <summary>Starts a side-job shift at the profile's level (see <see cref="SideJobStartResult"/>).</summary>
        public SideJobStartResult TryStartSideJob(GameWorld world, SideJobKind kind)
        {
            throw new NotImplementedException();
        }

        /// <summary>Ends the running side job (no pay for the unfinished round, help "<i>Name</i> over: <i>reason</i>"). No-op when none.</summary>
        public void EndSideJob(GameWorld world, string reason)
        {
            throw new NotImplementedException();
        }
    }
}
