using System;
using System.Collections.Generic;
using GtaSkylines.Game.Dialogue;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Profile;
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

        /// <summary>
        /// Where mission and side-job wanted writes go (both contexts). Default <see cref="DirectWantedControl.Instance"/>;
        /// <c>GameSession.AddSystem</c> installs a <see cref="CrimeWantedControl"/> when the session has a crime system.
        /// </summary>
        public IWantedControl WantedControl
        {
            get { return Context.Wanted; }
            set
            {
                if (value == null) throw new ArgumentNullException("value");
                Context.Wanted = value;
                _jobCtx.Wanted = value;
            }
        }

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
        public Vec3 RaceMarker { get; private set; }

        /// <inheritdoc />
        public void Step(GameWorld world, float dt)
        {
            if (Campaign == null) Init(world);
            if (world.Roads.Version != _raceGraphVersion) PlaceRaceMarker(world);
            ReadEvents(world);
            Context.StepWitness(dt);
            Phone.HandleInput(world, this);
            if (State == RunnerState.Mission) StepMission(world, dt);
            else if (State == RunnerState.SideJob && !JobSuspended) StepJob(world, dt);
            if (State == RunnerState.Idle) StepMarkers(world);
            Chirper.Observe(world, Context.Events, Context.EventCount);
            Phone.RefreshModel(world, this);
        }

        // ---- setup and events ---------------------------------------------------------------------------------

        private readonly MissionContext _jobCtx = new MissionContext();
        private readonly List<MissionDefinition> _available = new List<MissionDefinition>();
        private readonly bool[] _armed = new bool[MissionIds.All.Length];
        private long _cursor;
        private int _raceGraphVersion = -1;
        private bool _raceArmed = true;
        private int _missionIndex = -1;
        private int _jobCarId;
        private int _jobStep;
        private float _jobWait;
        private bool _jobRoundRunning;
        private Rng _jobRng;

        private const int RaceBlipId = 100;

        private void Init(GameWorld world)
        {
            Campaign = CampaignGenerator.Generate(world.City, world.Roads, world.Profile, Seed);
            Places = new PlaceFinder(world.City, world.Roads);
            _jobRng = new Rng(Rng.Seed(Seed, 0x10B5u));
            for (int i = 0; i < _armed.Length; i++) _armed[i] = true;
            for (int i = 0; i < Campaign.Missions.Count; i++)
            {
                MissionDefinition m = Campaign.Missions[i];
                if (world.Profile.IsMissionComplete(m.Id) || m.IsAvailable(world.Profile)) Phone.Unlock(m.Contact);
            }
            SendUnlockTexts(world);
            PlaceRaceMarker(world);
            _cursor = world.Events.NextSeq;
            RefreshMarkers(world);
        }

        private void PlaceRaceMarker(GameWorld world)
        {
            _raceGraphVersion = world.Roads.Version;
            if (Places == null) return;
            RaceMarker = Places.NearestRoadPoint(Places.CityCentre());
            if (State == RunnerState.Idle) RefreshMarkers(world);
        }

        private void SendUnlockTexts(GameWorld world)
        {
            Campaign.Available(world.Profile, _available);
            for (int i = 0; i < _available.Count; i++)
            {
                MissionDefinition m = _available[i];
                Phone.Unlock(m.Contact);
                string sender = Contacts.Name(m.Contact);
                if (!string.IsNullOrEmpty(m.UnlockText) && !Phone.HasText(sender, m.UnlockText)) Phone.ReceiveText(world, sender, m.UnlockText);
            }
        }

        private void ReadEvents(GameWorld world)
        {
            MissionContext ctx = Context;
            ctx.World = world;
            ctx.EventCount = 0;
            GameEvent e;
            while (world.Events.Read(ref _cursor, out e))
            {
                if (ctx.EventCount < MissionContext.EventCapacity) ctx.Events[ctx.EventCount++] = e;
                else ctx.DroppedEvents++;
            }
            _jobCtx.World = world;
            _jobCtx.EventCount = ctx.EventCount;
            Array.Copy(ctx.Events, _jobCtx.Events, ctx.EventCount);
        }

        // ---- markers ------------------------------------------------------------------------------------------

        private void ClearMarkers(GameWorld world)
        {
            MissionStatus m = world.Mission;
            for (int i = m.Blips.Count - 1; i >= 0; i--) if (m.Blips[i].Id >= 1 && m.Blips[i].Id <= RaceBlipId) m.Blips.RemoveAt(i);
            for (int i = m.Markers.Count - 1; i >= 0; i--) if (m.Markers[i].Id >= 1 && m.Markers[i].Id <= RaceBlipId) m.Markers.RemoveAt(i);
        }

        private void RefreshMarkers(GameWorld world)
        {
            ClearMarkers(world);
            if (Campaign == null) return;
            Campaign.Available(world.Profile, _available);
            MissionStatus m = world.Mission;
            for (int i = 0; i < _available.Count; i++)
            {
                int id = Campaign.Missions.IndexOf(_available[i]) + 1;
                m.Blips.Add(new Blip { Id = id, Kind = BlipKind.MissionStart, Position = _available[i].StartMarker });
                m.Markers.Add(new WorldMarker { Id = id, Position = _available[i].StartMarker, Radius = MarkerRadius, ColourRgb = MarkerColour });
            }
            m.Blips.Add(new Blip { Id = RaceBlipId, Kind = BlipKind.Checkpoint, Position = RaceMarker });
        }

        private void StepMarkers(GameWorld world)
        {
            Ped player = world.Player;
            if (player == null || world.Interior.IsInside || world.PlayerState.Life != LifeState.Alive) return;
            Car car = player.VehicleId != 0 ? world.FindCar(player.VehicleId) : null;
            Vec3 at = car != null ? car.Position : player.Position;
            bool slow = car == null || car.Body.Speed < 3f;
            for (int i = 0; i < _available.Count; i++)
            {
                MissionDefinition m = _available[i];
                int index = Campaign.Missions.IndexOf(m);
                float d = Vec3.DistanceXZ(at, m.StartMarker);
                if (!_armed[index])
                {
                    if (d > MarkerRadius + 2f) _armed[index] = true;
                    continue;
                }
                if (d > MarkerRadius || !slow) continue;
                MissionDefinition fresh = CampaignGenerator.Rebuild(m.Id, Campaign, world.City, world.Roads, world.Profile, Seed);
                if (fresh != null) Campaign.Missions[index] = fresh;
                if (TryStartMission(world, m.Id)) return;
            }
            float race = Vec3.DistanceXZ(at, RaceMarker);
            if (!_raceArmed)
            {
                if (race > RaceMarkerRadius + 5f) _raceArmed = true;
                return;
            }
            if (car != null && race <= RaceMarkerRadius && player.Seat == 0 && SideJobTable.AcceptsVehicle(SideJobKind.StreetRace, car.Model.Kind))
            {
                _raceArmed = false;
                TryStartSideJob(world, SideJobKind.StreetRace);
            }
        }

        // ---- missions -----------------------------------------------------------------------------------------

        private void StepMission(GameWorld world, float dt)
        {
            MissionContext ctx = Context;
            MissionDefinition def = ActiveMission;
            ctx.StepSeconds += dt;
            ctx.RunSeconds += dt;
            for (int i = 0; i < ctx.EventCount; i++)
            {
                if (ctx.Events[i].Type == GameEventType.PlayerDied) { EndMission(world, false, FailReasons.Wasted); return; }
                if (ctx.Events[i].Type == GameEventType.PlayerArrested) { EndMission(world, false, FailReasons.Busted); return; }
            }
            string fail = CheckFails(ctx, def);
            if (fail != null) { EndMission(world, false, fail); return; }
            MissionStep step = def.Steps[ctx.StepIndex];
            StepState state = step.Step(ctx, dt);
            if (state == StepState.Failed) { EndMission(world, false, ctx.FailReason ?? FailReasons.Cancelled); return; }
            if (state != StepState.Done) return;
            step.End(ctx);
            if (ctx.StepIndex + 1 >= def.Steps.Length) { EndMission(world, true, null); return; }
            BeginStep(ctx, def.Actors, def.Steps, ctx.StepIndex + 1);
        }

        private static string CheckFails(MissionContext ctx, MissionDefinition def)
        {
            GameWorld world = ctx.World;
            if ((def.Fail & MissionFail.VehicleDestroyed) != 0 && def.VehicleSlot >= 0 && ctx.ActorIds[def.VehicleSlot] != 0)
            {
                Car car = ctx.ActorCar(def.VehicleSlot);
                if (car == null || car.Destroyed) return FailReasons.VehicleDestroyed;
            }
            if ((def.Fail & MissionFail.TargetEscaped) != 0 && def.TargetSlot >= 0 && ctx.ActorIds[def.TargetSlot] != 0)
            {
                Car car = ctx.ActorCar(def.TargetSlot);
                Ped ped = car == null ? ctx.ActorPed(def.TargetSlot) : null;
                Vec3 at = car != null ? car.Position : ped != null ? ped.Position : ctx.PlayerPosition;
                if (Vec3.DistanceXZ(at, ctx.PlayerPosition) > def.EscapeDistance) return FailReasons.TargetEscaped;
            }
            if ((def.Fail & MissionFail.PassengerKilled) != 0 && def.PassengerSlot >= 0 && ctx.ActorIds[def.PassengerSlot] != 0)
            {
                Ped ped = ctx.ActorPed(def.PassengerSlot);
                if (ped == null || ped.IsDead) return FailReasons.PassengerKilled;
            }
            return null;
        }

        private static void BeginStep(MissionContext ctx, MissionActorSpec[] actors, MissionStep[] steps, int index)
        {
            ctx.StepIndex = index;
            ctx.StepSeconds = 0f;
            for (int i = 0; i < actors.Length && i < MissionContext.MaxActors; i++)
                if (actors[i].SpawnAtStep == index && ctx.ActorIds[i] == 0) ctx.SpawnActor(actors[i], i);
            steps[index].Begin(ctx);
        }

        private void EndMission(GameWorld world, bool passed, string reason)
        {
            MissionContext ctx = Context;
            MissionDefinition def = ActiveMission;
            if (ctx.StepIndex < def.Steps.Length) def.Steps[ctx.StepIndex].End(ctx);
            ctx.RemoveActors(def.Actors.Length);
            world.Mission.Clear();
            State = RunnerState.Idle;
            ActiveMission = null;
            if (_missionIndex >= 0) _armed[_missionIndex] = false;
            _missionIndex = -1;
            if (passed)
            {
                world.Wallet.Earn(def.Reward.Cash, def.Reward.Reason);
                world.Profile.CompleteMission(def.Id);
                world.Profile.AddStat(StatNames.JobsDone, 1);
                world.Messages.ShowBigScreen(BigScreenKind.MissionPassed, null, def.Reward.Cash, BigScreenSeconds);
                Chirper.TryPost(world, ChirpTopic.MissionPassed, world.Player.Position, def.Title);
                if (!string.IsNullOrEmpty(def.PassedText)) Phone.ReceiveText(world, Contacts.Name(def.Contact), def.PassedText);
                SendUnlockTexts(world);
            }
            else
            {
                LastFailReason = reason;
                if (reason != FailReasons.Wasted && reason != FailReasons.Busted)
                    world.Messages.ShowBigScreen(BigScreenKind.MissionFailed, reason, 0, BigScreenSeconds);
            }
            if (ActiveJob != null && JobSuspended) ResumeJob(world);
            if (State == RunnerState.Idle) RefreshMarkers(world);
        }

        // ---- side jobs ----------------------------------------------------------------------------------------

        private void StepJob(GameWorld world, float dt)
        {
            Car car = world.FindCar(_jobCarId);
            if (car == null || car.Destroyed || !Context.PlayerDrives(car))
            {
                EndJob(world, car == null || car.Destroyed ? FailReasons.VehicleDestroyed : FailReasons.LeftJobVehicle, true);
                return;
            }
            MissionContext ctx = _jobCtx;
            SideJob job = ActiveJob;
            if (!_jobRoundRunning)
            {
                _jobWait -= dt;
                if (_jobWait > 0f) return;
                ctx.RemoveActors(MissionContext.MaxActors);
                ctx.Reset(world, null);
                if (!job.BuildRound(ctx, Places, _jobRng) || job.Steps.Length == 0)
                {
                    world.Messages.ShowHelp(SideJobText.Waiting, 1, 4f);
                    _jobWait = 5f;
                    return;
                }
                _jobRoundRunning = true;
                _jobStep = 0;
                BeginStep(ctx, job.Actors, job.Steps, 0);
                return;
            }
            ctx.StepSeconds += dt;
            ctx.RunSeconds += dt;
            MissionStep step = job.Steps[_jobStep];
            StepState state = step.Step(ctx, dt);
            if (state == StepState.Failed) { EndJob(world, ctx.FailReason ?? FailReasons.Cancelled, true); return; }
            if (state != StepState.Done) return;
            step.End(ctx);
            if (++_jobStep < job.Steps.Length)
            {
                BeginStep(ctx, job.Actors, job.Steps, _jobStep);
                return;
            }
            CompleteRound(world, ctx, job);
        }

        private void CompleteRound(GameWorld world, MissionContext ctx, SideJob job)
        {
            long pay = job.RoundPay(ctx);
            ctx.RemoveActors(MissionContext.MaxActors);
            _jobRoundRunning = false;
            _jobWait = 3f;
            PlayerProfile profile = world.Profile;
            string id = SideJobTable.Id(job.Kind);
            if (pay > 0) world.Wallet.Earn(pay, job.RewardReason);
            profile.AddStat(SideJobTable.RoundsStat(job.Kind), 1);
            profile.AddStat(StatNames.JobsDone, 1);
            job.RoundsThisShift++;
            job.EarnedThisShift += pay;
            int level = Math.Max(SideJobTable.LevelForRounds(profile.GetStat(SideJobTable.RoundsStat(job.Kind))), profile.GetSideJobLevel(id));
            level = SideJobTable.Clamp(level);
            profile.SideJobLevels[id] = level;
            if (level > job.Level)
            {
                job.Level = level;
                world.Messages.Notify(SideJobTable.Name(job.Kind), SideJobText.LevelUp[level - 2], NotificationKind.System);
                Chirper.TryPost(world, ChirpTopic.JobMilestone, world.Player.Position, SideJobTable.Name(job.Kind));
            }
            if (job.Kind == SideJobKind.StreetRace) EndJob(world, null, false);
        }

        private void EndJob(GameWorld world, string reason, bool failed)
        {
            SideJob job = ActiveJob;
            if (job == null) return;
            if (_jobRoundRunning && !JobSuspended && _jobStep < job.Steps.Length) job.Steps[_jobStep].End(_jobCtx);
            _jobCtx.RemoveActors(MissionContext.MaxActors);
            _jobRoundRunning = false;
            if (failed)
            {
                LastFailReason = reason;
                world.Messages.ShowHelp(SideJobTable.Name(job.Kind) + " over: " + reason, 3, 6f);
            }
            else world.Messages.ShowHelp(SideJobTable.Name(job.Kind) + " over. Earned $" + job.EarnedThisShift + ".", 3, 6f);
            ActiveJob = null;
            JobSuspended = false;
            if (State == RunnerState.SideJob)
            {
                world.Mission.Clear();
                State = RunnerState.Idle;
                RefreshMarkers(world);
            }
        }

        private void ResumeJob(GameWorld world)
        {
            Car car = world.FindCar(_jobCarId);
            JobSuspended = false;
            State = RunnerState.SideJob;
            if (car == null || !Context.PlayerDrives(car))
            {
                EndJob(world, FailReasons.LeftJobVehicle, true);
                return;
            }
            world.Mission.Active = true;
            world.Mission.Title = SideJobTable.Name(ActiveJob.Kind);
            if (_jobRoundRunning) ActiveJob.Steps[_jobStep].Begin(_jobCtx);
        }

        /// <summary>
        /// Starts an available mission now (markers removed, briefing queued, title set, a running side job suspended).
        /// False when a mission runs, the id is unknown or not available, or the campaign is not generated yet.
        /// </summary>
        public bool TryStartMission(GameWorld world, string id)
        {
            if (Campaign == null || State == RunnerState.Mission) return false;
            MissionDefinition def = Campaign.Find(id);
            if (def == null || !def.IsAvailable(world.Profile)) return false;
            if (State == RunnerState.SideJob)
            {
                if (_jobRoundRunning && _jobStep < ActiveJob.Steps.Length) ActiveJob.Steps[_jobStep].End(_jobCtx);
                JobSuspended = true;
            }
            ClearMarkers(world);
            MissionContext ctx = Context;
            ctx.Reset(world, def);
            _cursor = world.Events.NextSeq;
            ActiveMission = def;
            _missionIndex = Campaign.Missions.IndexOf(def);
            State = RunnerState.Mission;
            LastFailReason = null;
            MissionStatus m = world.Mission;
            m.Active = true;
            m.Title = def.Title;
            m.StatusLine = null;
            m.TimerSeconds = -1f;
            for (int i = 0; i < def.Briefing.Length; i++) world.Messages.QueueSubtitle(def.Briefing[i].Speaker, def.Briefing[i].Text, def.Briefing[i].Seconds);
            if (def.Steps.Length == 0)
            {
                EndMission(world, true, null);
                return true;
            }
            BeginStep(ctx, def.Actors, def.Steps, 0);
            return true;
        }

        /// <summary>Starts a side-job shift at the profile's level (see <see cref="SideJobStartResult"/>).</summary>
        public SideJobStartResult TryStartSideJob(GameWorld world, SideJobKind kind)
        {
            if (State == RunnerState.Mission) return SideJobStartResult.MissionRunning;
            if (ActiveJob != null) return SideJobStartResult.AlreadyRunning;
            Ped player = world.Player;
            Car car = player != null && player.VehicleId != 0 ? world.FindCar(player.VehicleId) : null;
            if (car == null || player.Seat != 0 || !SideJobTable.AcceptsVehicle(kind, car.Model.Kind)) return SideJobStartResult.WrongVehicle;
            if (kind == SideJobKind.StreetRace && (Campaign == null || Vec3.DistanceXZ(car.Position, RaceMarker) > 30f)) return SideJobStartResult.WrongVehicle;
            if (Campaign == null) Init(world);
            string id = SideJobTable.Id(kind);
            int level = Math.Max(world.Profile.GetSideJobLevel(id), SideJobTable.LevelForRounds(world.Profile.GetStat(SideJobTable.RoundsStat(kind))));
            ActiveJob = SideJob.Create(kind, level);
            _jobCarId = car.Id;
            _jobRoundRunning = false;
            _jobWait = 0f;
            JobSuspended = false;
            _jobCtx.Reset(world, null);
            State = RunnerState.SideJob;
            LastFailReason = null;
            ClearMarkers(world);
            world.Mission.Active = true;
            world.Mission.Title = SideJobTable.Name(kind);
            return SideJobStartResult.Started;
        }

        /// <summary>Abandons the running mission (M7 interaction menu): it fails with <see cref="FailReasons.Cancelled"/>. No-op when no mission runs.</summary>
        public void AbortMission(GameWorld world)
        {
            if (State == RunnerState.Mission) EndMission(world, false, FailReasons.Cancelled);
        }

        /// <summary>Ends the running side job (no pay for the unfinished round, help "<i>Name</i> over: <i>reason</i>"). No-op when none.</summary>
        public void EndSideJob(GameWorld world, string reason)
        {
            EndJob(world, reason, true);
        }
    }
}
