using System;
using System.Diagnostics;
using System.IO;
using ColossalFramework.UI;
using GtaSkylines.Mod.Lifecycle;
using ICities;
using Skylines.Host;
using UnityEngine;
using UInput = UnityEngine.Input;

namespace GtaSkylines.Mod.SelfTest
{
    /// <summary>
    /// Process-wide self-test plumbing, alive from mod enable to disable: reads <c>launch.cfg</c> from the mod data folder,
    /// runs the <c>autoload</c> from the main menu (<see cref="SaveAutoloader"/>; never saves), starts the self-test on
    /// Ctrl+Shift+T or after a city load with <c>selftest = city_load</c>, and with <c>selftest_quit = true</c> quits the
    /// game without saving after the report (<see cref="UnattendedPolicy"/>). Main thread only; every callback guarded.
    /// </summary>
    internal static class SelfTestHost
    {
        private const double AutoStartSettleSeconds = 8;

        private static readonly Stopwatch s_clock = new Stopwatch();
        private static MainThreadPump s_pump;
        private static LaunchConfig s_cfg;
        private static SaveAutoloader s_autoload;
        private static UnattendedPolicy s_unattended;
        private static SelfTestRun s_run;
        private static bool s_autoloadIssued;
        private static double s_autoStartAt = -1;

        /// <summary>From <c>Mod.OnEnabled</c>.</summary>
        public static void Start()
        {
            if (s_pump != null) return;
            HostLog log = ModRuntime.Log;
            s_clock.Start();
            s_cfg = LaunchConfig.Load(Path.Combine(ModRuntime.DataFolder, LaunchConfig.FileName));
            foreach (string p in s_cfg.Problems) log.Warn("launch.cfg: " + p);
            log.Info("launch.cfg " + (s_cfg.Found ? "read" : "absent") + ": selftest " + s_cfg.SelfTest + ", selftest_quit "
                + s_cfg.SelfTestQuit + ", autoload '" + s_cfg.Autoload + "'");
            s_autoload = new SaveAutoloader(log, s_cfg.Autoload);
            s_unattended = new UnattendedPolicy(s_cfg.SelfTestQuit);
            if (s_cfg.SelfTestQuit)
            {
                // Nobody watches an unattended run: a sleeping display must not throttle frames through vsync
                // (reference, owner's run 2026-10-05). Runtime only; the game's settings are not written.
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 60;
                log.Info("unattended: vsync off, target 60 fps for this session");
            }
            s_pump = MainThreadPump.Install("GtaSkylines.SelfTest");
            s_pump.OnHandlerError = (what, e) => log.Error("self-test pump " + what, e);
            s_pump.Updated += Update;
        }

        /// <summary>From <c>Mod.OnDisabled</c>.</summary>
        public static void Stop()
        {
            AbortRun("mod disabled");
            if (s_pump == null) return;
            s_pump.Updated -= Update;
            s_pump.Uninstall();
            s_pump = null;
        }

        /// <summary>A city finished loading.</summary>
        public static void OnLevelLoaded()
        {
            if (s_cfg == null) return;
            double now = Now();
            if (s_autoloadIssued)
            {
                s_unattended.AutoloadLevelLoaded(now);
                if (SaveAutoloader.InOurNewGame())
                {
                    SaveAutoloader.PauseAutosave();
                    ModRuntime.Log.Info("self-test: autosave paused for this level (not persisted)");
                }
            }
            if (s_cfg.SelfTest == SelfTestMode.CityLoad) s_autoStartAt = now + AutoStartSettleSeconds;
        }

        /// <summary>The city is unloading: a running self-test stops and writes what it has.</summary>
        public static void OnLevelUnloading()
        {
            s_autoStartAt = -1;
            AbortRun("city unloading");
        }

        private static double Now() { return s_clock.Elapsed.TotalSeconds; }

        private static void Update()
        {
            double now = Now();
            Unattended(now);
            if (s_run != null)
            {
                if (TriggerPressed()) s_run.Abort("Ctrl+Shift+T pressed again");
                s_run.Update();
                if (s_run.Done) s_run = null;
                return;
            }
            if (PlayController.Current == null) return;
            string trigger = null;
            if (s_autoStartAt >= 0 && now >= s_autoStartAt)
            {
                s_autoStartAt = -1;
                trigger = "launch.cfg selftest = city_load";
            }
            else if (TriggerPressed()) trigger = "Ctrl+Shift+T";
            if (trigger == null) return;
            if (PlayController.Current.Playing)
            {
                ModRuntime.Log.Info("self-test: leave play mode first (" + trigger + " ignored)");
                return;
            }
            s_run = new SelfTestRun(ModRuntime.Log, trigger, ModRuntime.DataFolder, ReportWritten);
        }

        private static void ReportWritten()
        {
            s_unattended.ReportWritten(Now());
            if (s_cfg.SelfTestQuit) ModRuntime.Log.Info("unattended: report written; quitting in " + UnattendedPolicy.QuitDelaySeconds + " s");
        }

        private static void Unattended(double now)
        {
            SaveAutoloader.Outcome o = s_autoload.Update();
            if (o == SaveAutoloader.Outcome.Loaded)
            {
                s_autoloadIssued = true;
                s_unattended.AutoloadIssued(now);
            }
            else if (o == SaveAutoloader.Outcome.NotFound || o == SaveAutoloader.Outcome.Failed) s_unattended.AutoloadNotFound(now);
            switch (s_unattended.Tick(now))
            {
                case UnattendedAction.AbortSelfTest:
                    ModRuntime.Log.Warn("unattended: " + s_unattended.QuitReason + "; aborting the self-test");
                    if (s_run != null) s_run.Abort("safety timeout");
                    else s_unattended.ReportWritten(now);
                    break;
                case UnattendedAction.Quit:
                    ModRuntime.Log.Info("unattended: quitting the game (" + s_unattended.QuitReason + "); no save");
                    SaveAutoloader.QuitGame();
                    break;
            }
        }

        private static void AbortRun(string reason)
        {
            if (s_run == null) return;
            ModRuntime.Guard("self-test abort", () => s_run.Abort(reason));
            ModRuntime.Guard("self-test abort", s_run.Update);
            s_run = null;
        }

        private static bool TriggerPressed()
        {
            return UInput.GetKeyDown(KeyCode.T)
                && (UInput.GetKey(KeyCode.LeftControl) || UInput.GetKey(KeyCode.RightControl))
                && (UInput.GetKey(KeyCode.LeftShift) || UInput.GetKey(KeyCode.RightShift))
                && !UIView.HasInputFocus();
        }
    }

    /// <summary>Forwards city load and unload to <see cref="SelfTestHost"/> (CS1 calls every ILoadingExtension, LoadingWrapper.cs:49-53).</summary>
    public sealed class SelfTestLoading : LoadingExtensionBase
    {
        /// <inheritdoc />
        public override void OnLevelLoaded(LoadMode mode)
        {
            if (mode != LoadMode.NewGame && mode != LoadMode.LoadGame && mode != LoadMode.NewGameFromScenario) return;
            ModRuntime.Guard("self-test level loaded", SelfTestHost.OnLevelLoaded);
        }

        /// <inheritdoc />
        public override void OnLevelUnloading()
        {
            ModRuntime.Guard("self-test level unloading", SelfTestHost.OnLevelUnloading);
        }
    }
}
