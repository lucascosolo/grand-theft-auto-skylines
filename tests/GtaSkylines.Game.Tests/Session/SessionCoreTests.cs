using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Session;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.SessionTests
{
    public class SessionLifecycleTests
    {
        [Fact]
        public void Constructor_RejectsNulls()
        {
            var city = new Fakes.FakeCity();
            Assert.Throws<ArgumentNullException>(() => new GameSession(null, new GtaSkylines.Game.Settings.GameSettings(), new PlayerProfile()));
            Assert.Throws<ArgumentNullException>(() => new GameSession(city, null, new PlayerProfile()));
            Assert.Throws<ArgumentNullException>(() => new GameSession(city, new GtaSkylines.Game.Settings.GameSettings(), null));
        }

        [Fact]
        public void Begin_CreatesPlayerPedId1_StandingOnTheFloor()
        {
            var sim = new Sim(begin: false);
            Assert.False(sim.S.Running);
            sim.S.Begin(new Vec3(10f, 0.4f, -5f), 90f);
            sim.Run(1.0, In.Idle);
            Assert.True(sim.S.Running);
            Ped p = sim.Player;
            Assert.Equal(1, p.Id);
            Assert.Equal(PedKind.Player, p.Kind);
            Assert.Single(sim.W.Peds);
            Assert.Same(p, sim.W.FindPed(1));
            Sim.Near(0f, p.Position.Y, 0.06f, "feet y");
            Sim.Near(10f, p.Position.X, 0.05f, "feet x");
            Sim.Near(-5f, p.Position.Z, 0.05f, "feet z");
            Sim.Near(90f, p.YawDeg, 1f, "yaw");
            Sim.Near(90f, sim.PS.ViewYawDeg, 1f, "view yaw");
            Sim.Near(100f, p.Health, 0.01f, "health");
            Sim.Near(100f, sim.PS.Stamina, 0.01f, "stamina");
        }

        [Fact]
        public void Begin_TakesModelHealthAndArmourFromTheProfile()
        {
            var sim = new Sim(begin: false, profile: p => { p.OutfitId = 5; p.Health = 40; p.Armour = 30; });
            sim.S.Begin(Vec3.Zero, 0f);
            Assert.Equal(5, sim.Player.Model);
            Sim.Near(40f, sim.Player.Health, 0.01f);
            Sim.Near(30f, sim.Player.Armour, 0.01f);
        }

        [Fact]
        public void Begin_ProfileHealthZeroStartsAt100_AndBadOutfitFallsBackToModel0()
        {
            var sim = new Sim(begin: false, profile: p => { p.OutfitId = 99; p.Health = 0; });
            sim.S.Begin(Vec3.Zero, 0f);
            Assert.Equal(0, sim.Player.Model);
            Sim.Near(100f, sim.Player.Health, 0.01f);
        }

        [Fact]
        public void Begin_TakesCameraViewsFromSettings()
        {
            var sim = new Sim(settings: s => { s.FootView = 2; s.VehicleView = 3; });
            Assert.Equal(GtaSkylines.Game.Camera.FootView.ThirdMid, sim.S.Camera.FootView);
            Assert.Equal(GtaSkylines.Game.Camera.VehicleView.ChaseFar, sim.S.Camera.VehicleView);
        }

        [Fact]
        public void Begin_WhileRunning_Throws()
        {
            var sim = new Sim();
            Assert.Throws<InvalidOperationException>(() => sim.S.Begin(Vec3.Zero, 0f));
        }

        [Fact]
        public void Tick_BeforeBegin_Throws()
        {
            var sim = new Sim(begin: false);
            Assert.Throws<InvalidOperationException>(() => sim.S.Tick(Sim.Dt, In.Idle, sim.Snap));
        }

        [Fact]
        public void Begin_ClearsTheBlackboards()
        {
            var sim = new Sim(begin: false);
            sim.W.Mission.Active = true;
            sim.W.Mission.Objective = "stale";
            sim.W.Wanted.Stars = 4;
            sim.S.Begin(Vec3.Zero, 0f);
            Assert.False(sim.W.Mission.Active);
            Assert.Null(sim.W.Mission.Objective);
            Assert.Equal(0, sim.W.Wanted.Stars);
            Assert.Empty(sim.W.Cars);
            Assert.False(sim.W.Menu.IsOpen);
        }

        [Fact]
        public void End_ReturnsProfileWithVitalsAndOutfit_AndStopsRunning()
        {
            var sim = new Sim();
            sim.Player.Health = 62.4f;
            sim.Player.Armour = 17.6f;
            sim.Player.Model = 4;
            PlayerProfile p = sim.S.End();
            Assert.Same(sim.Profile, p);
            Assert.False(sim.S.Running);
            Assert.Equal(62, p.Health);
            Assert.Equal(18, p.Armour);
            Assert.Equal(4, p.OutfitId);
        }

        [Fact]
        public void End_DeadPlayerSavesFullHealth_AndIsIdempotent()
        {
            var sim = new Sim();
            sim.Player.Health = 0f;
            sim.Player.Flags |= PedFlags.Dead;
            PlayerProfile a = sim.S.End();
            PlayerProfile b = sim.S.End();
            Assert.Same(a, b);
            Assert.Equal(100, a.Health);
        }

        [Fact]
        public void End_FlushesPendingWalkedDistanceIntoStats()
        {
            var sim = new Sim();
            sim.Run(3.0, In.Forward);
            PlayerProfile p = sim.S.End();
            Assert.InRange(p.GetStat(PlayerRules.StatDistanceWalked), 8, 12);
            Assert.InRange(p.GetStat(StatNames.DistanceM), 8, 12);
        }

        [Fact]
        public void Waypoint_SetAndClear()
        {
            var sim = new Sim();
            sim.S.SetWaypoint(new Vec3(3f, 0f, 200f));
            Assert.True(sim.W.HasWaypoint);
            Sim.Near(new Vec3(3f, 0f, 200f), sim.W.Waypoint, 1e-3f);
            sim.S.ClearWaypoint();
            Assert.False(sim.W.HasWaypoint);
        }

        [Fact]
        public void SaveRequest_IsFalseUntilRequested()
        {
            var sim = new Sim();
            Assert.False(sim.S.SaveRequested);
            Assert.False(sim.S.ConsumeSaveRequest());
        }
    }

    public class SessionStepTests
    {
        [Fact]
        public void Tick_RunsOneStepPerFixedDt()
        {
            var sim = new Sim();
            long s0 = sim.W.Clock.Steps;
            sim.S.Tick(Sim.Dt, In.Idle, sim.Snap);
            Assert.Equal(1, sim.S.StepsLastTick);
            sim.S.Tick(2f * Sim.Dt + 0.0005f, In.Idle, sim.Snap);
            Assert.Equal(2, sim.S.StepsLastTick);
            Assert.Equal(s0 + 3, sim.W.Clock.Steps);
        }

        [Fact]
        public void Tick_LongFrameIsCappedAtFiveSteps_AndDropsTheRest()
        {
            var sim = new Sim();
            long s0 = sim.W.Clock.Steps;
            sim.S.Tick(0.5f, In.Idle, sim.Snap);
            Assert.Equal(5, sim.S.StepsLastTick);
            Assert.Equal(s0 + 5, sim.W.Clock.Steps);
            sim.S.Tick(Sim.Dt, In.Idle, sim.Snap);
            Assert.InRange(sim.S.StepsLastTick, 1, 2);
        }

        [Fact]
        public void Tick_ShortFramesAccumulate_AndInterpolationIsAFractionOfAStep()
        {
            var sim = new Sim();
            sim.S.Tick(Sim.Dt * 0.4f, In.Idle, sim.Snap);
            int total = sim.S.StepsLastTick;
            sim.S.Tick(Sim.Dt * 0.4f, In.Idle, sim.Snap);
            total += sim.S.StepsLastTick;
            sim.S.Tick(Sim.Dt * 0.4f, In.Idle, sim.Snap);
            total += sim.S.StepsLastTick;
            Assert.Equal(1, total);
            Assert.InRange(sim.S.Interpolation, 0f, 1f);
            // 0.2 of a step is left over, then 0.5 is added: 0.7 of a step remains and no step runs.
            sim.S.Tick(Sim.Dt * 0.5f, In.Idle, sim.Snap);
            Sim.Near(0.7f, sim.S.Interpolation, 0.05f, "interpolation");
        }

        [Fact]
        public void Clock_AdvancesGameAndRealSeconds()
        {
            var sim = new Sim();
            double g0 = sim.W.Clock.GameSeconds, r0 = sim.W.Clock.RealSeconds;
            sim.Run(1.0, In.Idle);
            Assert.InRange(sim.W.Clock.GameSeconds - g0, 0.95, 1.05);
            Assert.InRange(sim.W.Clock.RealSeconds - r0, 0.95, 1.05);
        }

        [Fact]
        public void Clock_HourComesFromTheCity_AndNightFollowsIt()
        {
            var sim = new Sim(city: c => c.Hours = 22.5f);
            sim.Run(0.1, In.Idle);
            Sim.Near(22.5f, sim.W.Clock.Hour, 1e-3f);
            Assert.True(sim.W.Clock.IsNight);
            sim.City.Hours = 12f;
            sim.Run(0.1, In.Idle);
            Assert.False(sim.W.Clock.IsNight);
        }

        [Fact]
        public void TimeScale_DefaultsToOne_ClampsAndScalesGameTime()
        {
            var sim = new Sim();
            Assert.Equal(1f, sim.S.TimeScale);
            sim.S.TimeScale = 3f;
            Assert.Equal(1f, sim.S.TimeScale);
            sim.S.TimeScale = -2f;
            Assert.Equal(0f, sim.S.TimeScale);
            sim.S.TimeScale = 0.5f;
            double g0 = sim.W.Clock.GameSeconds, r0 = sim.W.Clock.RealSeconds;
            sim.Run(2.0, In.Idle);
            Sim.Near(1.0f, (float)(sim.W.Clock.GameSeconds - g0), 0.08f, "game seconds at half speed");
            Sim.Near(2.0f, (float)(sim.W.Clock.RealSeconds - r0), 0.08f, "real seconds");
        }

        [Fact]
        public void Paused_FreezesTheSimulationButNotRealTimeOrTheHud()
        {
            var sim = new Sim();
            sim.Run(0.5, In.Forward);
            Vec3 where = sim.Player.Position;
            double g0 = sim.W.Clock.GameSeconds, r0 = sim.W.Clock.RealSeconds;
            long steps0 = sim.W.Clock.Steps;
            sim.W.Messages.Notify("Sam", "hello", NotificationKind.Text);
            sim.S.Paused = true;
            sim.Run(1.0, In.Forward);
            Sim.Near(where, sim.Player.Position, 1e-3f, "player frozen");
            Assert.Equal(g0, sim.W.Clock.GameSeconds, 6);
            Assert.Equal(steps0, sim.W.Clock.Steps);
            Assert.InRange(sim.W.Clock.RealSeconds - r0, 0.95, 1.05);
            Assert.True(sim.S.Hud.Notifications.Count == 1 && sim.S.Hud.Notifications[0].Age > 0.9f, "HUD ages notifications while paused");
            sim.S.Paused = false;
            sim.Run(0.5, In.Forward);
            Assert.True((sim.Player.Position - where).Length > 1f, "moves again after unpausing");
        }

        [Fact]
        public void Edges_ArriveInExactlyOneStep_EvenWhenTheFrameRunsNoStep()
        {
            var sim = new Sim();
            var seen = new CountingSystem();
            sim.S.AddSystem(seen);
            sim.S.Tick(Sim.Dt * 0.1f, In.Press(GameButton.Headlights), sim.Snap);
            Assert.Equal(0, sim.S.StepsLastTick);
            sim.S.Tick(Sim.Dt * 3.2f, In.Hold(GameButton.Headlights), sim.Snap);
            Assert.Equal(3, sim.S.StepsLastTick);
            Assert.Equal(1, seen.PressedSteps);
            Assert.Equal(3, seen.HeldSteps);
        }

        [Fact]
        public void Edges_AMultiStepFrameGivesThePressToTheFirstStepOnly()
        {
            var sim = new Sim();
            var seen = new CountingSystem();
            sim.S.AddSystem(seen);
            sim.S.Tick(Sim.Dt * 4.1f, In.Press(GameButton.Interact), sim.Snap);
            Assert.Equal(4, sim.S.StepsLastTick);
            Assert.Equal(1, seen.PressedSteps);
        }

        [Fact]
        public void Edges_SneakPressedInATwoStepFrameTogglesOnce()
        {
            var sim = new Sim();
            sim.S.Tick(Sim.Dt * 2.2f, In.Press(GameButton.Sneak), sim.Snap);
            Assert.Equal(2, sim.S.StepsLastTick);
            Assert.True(sim.PS.Sneaking);
            Assert.True(sim.Player.Has(PedFlags.Sneaking));
        }

        [Fact]
        public void LookDelta_IsAppliedOnceAcrossAMultiStepFrame()
        {
            var sim = new Sim();
            float before = sim.PS.ViewYawDeg;
            sim.S.Tick(Sim.Dt * 3.2f, In.Look(10f, 0f), sim.Snap);
            Assert.Equal(3, sim.S.StepsLastTick);
            Sim.Near(before + 10f, sim.PS.ViewYawDeg, 0.01f, "view yaw");
        }

        [Fact]
        public void LookDelta_ClampsPitchAndAccumulatesYaw()
        {
            var sim = new Sim();
            sim.Tick(In.Look(15f, 0f));
            Sim.Near(15f, sim.PS.ViewYawDeg, 0.01f);
            sim.Tick(In.Look(0f, 500f));
            Sim.Near(80f, sim.PS.ViewPitchDeg, 0.01f, "on-foot pitch limit up");
            sim.Tick(In.Look(0f, -900f));
            Sim.Near(-80f, sim.PS.ViewPitchDeg, 0.01f, "on-foot pitch limit down");
        }

        [Fact]
        public void Effects_AreReplacedAtEachTick()
        {
            var sim = new Sim();
            sim.AddCitizen(5, 500, new Vec3(3f, 0f, 3f));
            sim.Run(0.2, In.Idle);
            sim.W.PromoteCitizen(sim.Snap.Citizens[0], PedOrigin.PromotedCitizen, 0);
            sim.S.Tick(Sim.Dt, In.Idle, sim.Snap);
            Assert.Equal(1, CountOf(sim.S.Effects, CityEffectKind.TakeCitizen));
            sim.S.Tick(Sim.Dt, In.Idle, sim.Snap);
            Assert.Equal(0, CountOf(sim.S.Effects, CityEffectKind.TakeCitizen));
        }

        private static int CountOf(IList<CityEffect> fx, CityEffectKind k)
        {
            int n = 0;
            for (int i = 0; i < fx.Count; i++) if (fx[i].Kind == k) n++;
            return n;
        }
    }

    internal sealed class CountingSystem : IGameSystem
    {
        public int Order { get { return 200; } }
        public int PressedSteps, HeldSteps;
        public void Step(GameWorld world, float dt)
        {
            if (world.Input.WasPressed(GameButton.Headlights) || world.Input.WasPressed(GameButton.Interact)) PressedSteps++;
            if (world.Input.IsHeld(GameButton.Headlights)) HeldSteps++;
        }
    }

    internal sealed class RecordingSystem : IGameSystem
    {
        private readonly int _order;
        private readonly string _name;
        private readonly List<string> _log;
        public float LastDt;
        public int Steps;
        public RecordingSystem(int order, string name, List<string> log) { _order = order; _name = name; _log = log; }
        public int Order { get { return _order; } }
        public void Step(GameWorld world, float dt) { _log.Add(_name); LastDt = dt; Steps++; }
    }

    public class SessionSystemsTests
    {
        [Fact]
        public void AddSystem_RunsInAscendingOrder_StableForEqualOrders()
        {
            var sim = new Sim();
            var log = new List<string>();
            sim.S.AddSystem(new RecordingSystem(60, "crime", log));
            sim.S.AddSystem(new RecordingSystem(30, "peds-a", log));
            sim.S.AddSystem(new RecordingSystem(30, "peds-b", log));
            sim.S.AddSystem(new RecordingSystem(80, "missions", log));
            log.Clear();
            sim.Tick(In.Idle);
            Assert.Equal(new[] { "peds-a", "peds-b", "crime", "missions" }, log.ToArray());
        }

        [Fact]
        public void AddSystem_RejectsNullAndDuplicates()
        {
            var sim = new Sim();
            Assert.Throws<ArgumentNullException>(() => sim.S.AddSystem(null));
            var s = new RecordingSystem(10, "x", new List<string>());
            sim.S.AddSystem(s);
            Assert.Throws<InvalidOperationException>(() => sim.S.AddSystem(s));
        }

        [Fact]
        public void SimulationSystems_GetScaledDt_PresentationSystemsGetFixedDt_AndPauseStopsOnlyTheFirst()
        {
            var sim = new Sim();
            var log = new List<string>();
            var simSys = new RecordingSystem(SystemOrder.Combat, "sim", log);
            var presSys = new RecordingSystem(SystemOrder.Presentation, "pres", log);
            sim.S.AddSystem(simSys);
            sim.S.AddSystem(presSys);
            sim.S.TimeScale = 0.5f;
            sim.Tick(In.Idle);
            Sim.Near(Sim.Dt * 0.5f, simSys.LastDt, 1e-5f, "sim dt");
            Sim.Near(Sim.Dt, presSys.LastDt, 1e-5f, "presentation dt");
            sim.S.Paused = true;
            int simSteps = simSys.Steps, presSteps = presSys.Steps;
            sim.Run(0.5, In.Idle);
            Assert.Equal(simSteps, simSys.Steps);
            Assert.True(presSys.Steps > presSteps + 20, "presentation systems keep running while paused");
        }

        [Fact]
        public void SystemsSeeTheStepInputOnTheWorld()
        {
            var sim = new Sim();
            var probe = new CountingSystem();
            sim.S.AddSystem(probe);
            sim.Tick(In.Hold(GameButton.Headlights));
            Assert.Equal(1, probe.HeldSteps);
        }

        [Fact]
        public void CameraAndHudSystemsFillTheirOutputsFromTheFirstStep()
        {
            var sim = new Sim();
            sim.Tick(In.Idle);
            Assert.True(sim.W.ViewFovDeg > 1f);
            Assert.NotNull(sim.S.Hud.CashText);
        }
    }

    public class SessionDeterminismTests
    {
        private static uint Script(uint seed, bool variant)
        {
            var sim = new Sim(seed: seed);
            sim.AddParkedCar(20, new Vec3(30f, 0.75f, 30f), 0f);
            sim.AddCitizen(9, 900, new Vec3(-10f, 0f, 12f));
            uint h = 17;
            for (int i = 0; i < 240; i++)
            {
                InputFrame f = In.Move(i % 90 < 45 ? 0.5f : -0.5f, 1f);
                if (i % 120 < 60) f = f.WithHold(GameButton.Sprint);
                f.LookYawDeg = i % 30 == 0 ? 7f : 0f;
                if (i == 100) f = f.WithPress(GameButton.Jump);
                if (variant && i == 150) f = f.WithPress(GameButton.Sneak);
                sim.Tick(f);
                h = h * 31 + sim.S.StateHash();
            }
            return h;
        }

        [Fact]
        public void SameSeedAndInputs_GiveTheSameStateHashEveryStep()
        {
            Assert.Equal(Script(777u, false), Script(777u, false));
        }

        [Fact]
        public void DifferentInput_ChangesTheHash()
        {
            Assert.NotEqual(Script(777u, false), Script(777u, true));
        }

        [Fact]
        public void StateHash_ChangesWhenThePlayerMoves()
        {
            var sim = new Sim();
            uint a = sim.S.StateHash();
            sim.Run(0.5, In.Forward);
            Assert.NotEqual(a, sim.S.StateHash());
        }

        [Fact]
        public void StateHash_ReflectsCashAndStars()
        {
            var sim = new Sim();
            uint a = sim.S.StateHash();
            sim.Profile.AddCash(5);
            uint b = sim.S.StateHash();
            sim.W.Wanted.Stars = 2;
            uint c = sim.S.StateHash();
            Assert.NotEqual(a, b);
            Assert.NotEqual(b, c);
        }
    }

    public class SessionAllocationTests
    {
        [Fact]
        public void SteadyWalkingTick_AllocatesNothing()
        {
            var sim = new Sim();
            sim.AddParkedCar(31, new Vec3(60f, 0.75f, 60f), 0f);
            sim.AddCitizen(32, 3200, new Vec3(-40f, 0f, 20f));
            InputFrame walk = In.Move(0f, 1f);
            for (int i = 0; i < 400; i++) { walk.LookYawDeg = 1.5f; sim.S.Tick(Sim.Dt, walk, sim.Snap); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 600; i++) { walk.LookYawDeg = 1.5f; sim.S.Tick(Sim.Dt, walk, sim.Snap); }
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.Equal(0L, after - before);
        }

        [Fact]
        public void SteadyIdleTick_WithHelpPromptShowing_AllocatesNothing()
        {
            var sim = new Sim();
            sim.AddParkedCar(31, new Vec3(0f, 0.75f, 0f), 0f);
            sim.Place(new Vec3(-3f, 0f, 0.44f), 90f);
            for (int i = 0; i < 300; i++) sim.S.Tick(Sim.Dt, In.Idle, sim.Snap);
            Assert.Equal(InteractionTargetKind.EnterVehicle, sim.PS.Interaction.Kind);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 600; i++) sim.S.Tick(Sim.Dt, In.Idle, sim.Snap);
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.Equal(0L, after - before);
        }
    }
}
