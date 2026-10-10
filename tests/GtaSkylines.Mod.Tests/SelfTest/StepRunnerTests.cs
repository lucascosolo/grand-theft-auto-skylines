using System;
using System.Collections.Generic;
using GtaSkylines.Mod.SelfTest;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class StepRunnerTests
    {
        private static SelfTestStep Step(string id, double timeout, Action<StepRecord> begin, Action<StepRecord, double> tick)
        {
            return new SelfTestStep(id, "Name " + id, timeout, begin, tick);
        }

        private static SelfTestStep Instant(string id, StepStatus how)
        {
            return Step(id, 10, r =>
            {
                if (how == StepStatus.Pass) r.Pass("ok");
                else if (how == StepStatus.Fail) r.Fail("bad");
                else if (how == StepStatus.Skip) r.Skip("meh");
                else r.Error("boom");
            }, null);
        }

        private static StepRunner Runner(params SelfTestStep[] steps)
        {
            return new StepRunner(new List<SelfTestStep>(steps));
        }

        // ---- Measurements ----

        [Fact]
        public void Measurements_keep_insertion_order_and_replace_in_place()
        {
            var m = new Measurements();
            m.Set("a", 1);
            m.Set("b", 2);
            m.Set("a", 9);
            Assert.Equal(2, m.Count);
            var keys = new List<string>();
            foreach (KeyValuePair<string, object> kv in m.Items) keys.Add(kv.Key);
            Assert.Equal(new[] { "a", "b" }, keys.ToArray());
            Assert.Equal(9, m.Get("a"));
        }

        [Fact]
        public void Measurements_Get_of_absent_key_is_null()
        {
            Assert.Null(new Measurements().Get("nope"));
        }

        [Fact]
        public void Measurements_With_sets_and_returns_the_same_instance()
        {
            var m = new Measurements();
            Assert.Same(m, m.With("x", 1.5).With("y", "s"));
            Assert.Equal(2, m.Count);
            Assert.Equal("s", m.Get("y"));
        }

        // ---- StepRecord ----

        [Fact]
        public void Record_is_unfinished_until_a_verdict()
        {
            var r = new StepRecord();
            Assert.False(r.Finished);
            Assert.Null(r.Status);
            r.Pass("fine");
            Assert.True(r.Finished);
            Assert.Equal(StepStatus.Pass, r.Status);
            Assert.Equal("fine", r.Reason);
        }

        [Theory]
        [InlineData(StepStatus.Pass)]
        [InlineData(StepStatus.Fail)]
        [InlineData(StepStatus.Skip)]
        [InlineData(StepStatus.Error)]
        public void Record_verdict_methods_set_status_and_reason(StepStatus s)
        {
            var r = new StepRecord();
            if (s == StepStatus.Pass) r.Pass("why");
            else if (s == StepStatus.Fail) r.Fail("why");
            else if (s == StepStatus.Skip) r.Skip("why");
            else r.Error("why");
            Assert.Equal(s, r.Status);
            Assert.Equal("why", r.Reason);
        }

        [Fact]
        public void Record_first_verdict_wins()
        {
            var r = new StepRecord();
            r.Fail("first");
            r.Pass("second");
            r.Error("third");
            Assert.Equal(StepStatus.Fail, r.Status);
            Assert.Equal("first", r.Reason);
        }

        [Fact]
        public void Record_null_reason_becomes_empty()
        {
            var r = new StepRecord();
            r.Pass(null);
            Assert.Equal("", r.Reason);
        }

        // ---- construction ----

        [Fact]
        public void Constructor_creates_one_record_per_step_up_front()
        {
            StepRunner run = Runner(Instant("a", StepStatus.Pass), Step("b", 7.5, null, null));
            Assert.Equal(2, run.Records.Count);
            Assert.Equal("a", run.Records[0].Id);
            Assert.Equal("Name b", run.Records[1].Name);
            Assert.Equal(7.5, run.Records[1].TimeoutSeconds);
            Assert.False(run.Records[0].Finished);
            Assert.Null(run.Current);
            Assert.False(run.Done);
            Assert.False(run.Aborted);
        }

        [Fact]
        public void Step_exposes_its_properties_and_Needs_sets_Requires()
        {
            SelfTestStep s = Step("x", 3, null, null);
            Assert.Equal("x", s.Id);
            Assert.Equal("Name x", s.Name);
            Assert.Equal(3, s.TimeoutSeconds);
            Assert.Empty(s.Requires);
            Assert.Same(s, s.Needs("a", "b"));
            Assert.Equal(new[] { "a", "b" }, s.Requires);
        }

        // ---- sequencing ----

        [Fact]
        public void Begin_runs_on_first_tick_and_tick_delegate_waits_for_the_next_tick()
        {
            int began = 0, ticked = 0;
            StepRunner run = Runner(Step("a", 10, r => began++, (r, e) => ticked++));
            run.Tick(100);
            Assert.Equal(1, began);
            Assert.Equal(0, ticked);
            Assert.NotNull(run.Current);
            Assert.Equal("a", run.Current.Id);
            run.Tick(100.5);
            Assert.Equal(1, ticked);
        }

        [Fact]
        public void Tick_delegate_receives_elapsed_since_step_start()
        {
            var elapsed = new List<double>();
            StepRunner run = Runner(Step("a", 10, null, (r, e) => elapsed.Add(e)));
            run.Tick(50);
            run.Tick(52);
            run.Tick(54.5);
            Assert.Equal(new[] { 2.0, 4.5 }, elapsed.ToArray());
        }

        [Fact]
        public void Begin_that_finishes_completes_immediately_and_next_step_begins_in_same_tick()
        {
            var order = new List<string>();
            StepRunner run = Runner(
                Step("a", 10, r => { order.Add("a"); r.Pass("x"); }, null),
                Step("b", 10, r => { order.Add("b"); r.Pass("y"); }, null),
                Step("c", 10, r => order.Add("c"), null));
            run.Tick(1);
            Assert.Equal(new[] { "a", "b", "c" }, order.ToArray());
            Assert.Equal(StepStatus.Pass, run.Records[0].Status);
            Assert.Equal(0, run.Records[0].DurationSeconds);
            Assert.Equal("c", run.Current.Id);
        }

        [Fact]
        public void Step_finishing_in_tick_is_completed_and_next_step_starts_on_the_next_tick()
        {
            bool bBegan = false;
            StepRunner run = Runner(
                Step("a", 10, null, (r, e) => { if (e >= 1) r.Pass("done"); }),
                Step("b", 10, r => bBegan = true, null));
            run.Tick(0);
            run.Tick(1);
            Assert.True(run.Records[0].Finished);
            Assert.Equal(1, run.Records[0].DurationSeconds);
            Assert.False(bBegan);
            Assert.Null(run.Current);
            run.Tick(1.1);
            Assert.True(bBegan);
        }

        [Fact]
        public void StartedSeconds_are_relative_to_the_first_tick()
        {
            StepRunner run = Runner(
                Step("a", 10, null, (r, e) => { if (e >= 2) r.Pass("d"); }),
                Step("b", 10, r => r.Pass("d"), null));
            run.Tick(1000);
            run.Tick(1002);
            run.Tick(1003);
            Assert.Equal(0, run.Records[0].StartedSeconds);
            Assert.Equal(3, run.Records[1].StartedSeconds);
        }

        [Fact]
        public void Done_after_all_steps_finish_and_further_ticks_do_nothing()
        {
            int ticks = 0;
            StepRunner run = Runner(Step("a", 10, r => r.Pass("ok"), (r, e) => ticks++));
            Assert.False(run.Done);
            run.Tick(0);
            Assert.True(run.Done);
            run.Tick(1);
            run.Tick(2);
            Assert.Equal(0, ticks);
            Assert.Null(run.Current);
        }

        [Fact]
        public void Finished_callback_fires_once_per_record_in_order()
        {
            var seen = new List<string>();
            StepRunner run = Runner(Instant("a", StepStatus.Pass), Instant("b", StepStatus.Fail), Instant("c", StepStatus.Skip));
            run.Finished = r => seen.Add(r.Id + ":" + r.Status);
            run.Tick(0);
            run.Tick(1);
            run.Tick(2);
            Assert.Equal(new[] { "a:Pass", "b:Fail", "c:Skip" }, seen.ToArray());
        }

        [Fact]
        public void Null_begin_and_tick_are_allowed_and_step_times_out()
        {
            StepRunner run = Runner(Step("a", 2, null, null));
            run.Tick(0);
            run.Tick(1);
            Assert.False(run.Records[0].Finished);
            run.Tick(2);
            Assert.Equal(StepStatus.Fail, run.Records[0].Status);
        }

        // ---- timeout ----

        [Fact]
        public void Timeout_fails_the_step_with_formatted_reason_and_flag()
        {
            StepRunner run = Runner(Step("a", 30, null, null));
            run.Tick(0);
            run.Tick(29.9);
            Assert.False(run.Records[0].Finished);
            run.Tick(30);
            StepRecord r = run.Records[0];
            Assert.Equal(StepStatus.Fail, r.Status);
            Assert.True(r.TimedOut);
            Assert.Equal("timeout after 30 s", r.Reason);
            Assert.Equal(30, r.DurationSeconds);
        }

        [Fact]
        public void Timeout_reason_formats_fractions_with_one_decimal()
        {
            StepRunner run = Runner(Step("a", 2.5, null, null));
            run.Tick(0);
            run.Tick(3);
            Assert.Equal("timeout after 2.5 s", run.Records[0].Reason);
        }

        [Fact]
        public void Timeout_appends_progress_when_set()
        {
            StepRunner run = Runner(Step("a", 5, null, (r, e) => r.Progress = "3/10 done"));
            run.Tick(0);
            run.Tick(1);
            run.Tick(6);
            Assert.Equal("timeout after 5 s; 3/10 done", run.Records[0].Reason);
        }

        [Fact]
        public void Step_that_passes_on_the_timeout_tick_is_a_pass_not_a_timeout()
        {
            StepRunner run = Runner(Step("a", 5, null, (r, e) => { if (e >= 5) r.Pass("just in time"); }));
            run.Tick(0);
            run.Tick(5);
            Assert.Equal(StepStatus.Pass, run.Records[0].Status);
            Assert.False(run.Records[0].TimedOut);
        }

        [Fact]
        public void Run_continues_with_next_step_after_a_timeout()
        {
            StepRunner run = Runner(Step("a", 1, null, null), Instant("b", StepStatus.Pass));
            run.Tick(0);
            run.Tick(1);
            run.Tick(1.5);
            Assert.Equal(StepStatus.Pass, run.Records[1].Status);
            Assert.True(run.Done);
        }

        // ---- requires ----

        [Fact]
        public void Step_whose_requirement_failed_is_skipped_with_needs_reason()
        {
            StepRunner run = Runner(
                Instant("a", StepStatus.Fail),
                Instant("b", StepStatus.Pass).Needs("a"));
            run.Tick(0);
            StepRecord b = run.Records[1];
            Assert.Equal(StepStatus.Skip, b.Status);
            Assert.Contains("needs a", b.Reason);
            Assert.Equal(0, b.DurationSeconds);
        }

        [Theory]
        [InlineData(StepStatus.Fail)]
        [InlineData(StepStatus.Skip)]
        [InlineData(StepStatus.Error)]
        public void Any_non_pass_requirement_skips_the_dependent(StepStatus how)
        {
            StepRunner run = Runner(Instant("a", how), Instant("b", StepStatus.Pass).Needs("a"));
            run.Tick(0);
            Assert.Equal(StepStatus.Skip, run.Records[1].Status);
        }

        [Fact]
        public void Step_whose_requirement_passed_runs()
        {
            StepRunner run = Runner(Instant("a", StepStatus.Pass), Instant("b", StepStatus.Pass).Needs("a"));
            run.Tick(0);
            Assert.Equal(StepStatus.Pass, run.Records[1].Status);
        }

        [Fact]
        public void Unknown_requirement_id_skips_the_step()
        {
            StepRunner run = Runner(Instant("a", StepStatus.Pass).Needs("ghost"));
            run.Tick(0);
            Assert.Equal(StepStatus.Skip, run.Records[0].Status);
            Assert.Contains("needs ghost", run.Records[0].Reason);
        }

        [Fact]
        public void Skips_cascade_in_the_same_tick_and_next_runnable_step_starts()
        {
            bool dBegan = false;
            StepRunner run = Runner(
                Instant("a", StepStatus.Fail),
                Instant("b", StepStatus.Pass).Needs("a"),
                Instant("c", StepStatus.Pass).Needs("b"),
                Step("d", 10, r => dBegan = true, null));
            run.Tick(0);
            Assert.Equal(StepStatus.Skip, run.Records[1].Status);
            Assert.Equal(StepStatus.Skip, run.Records[2].Status);
            Assert.True(dBegan);
            Assert.Equal("d", run.Current.Id);
        }

        [Fact]
        public void Skipped_step_never_calls_begin()
        {
            bool began = false;
            StepRunner run = Runner(Instant("a", StepStatus.Fail), Step("b", 5, r => began = true, null).Needs("a"));
            run.Tick(0);
            Assert.False(began);
        }

        [Fact]
        public void Finished_fires_for_skipped_steps_too()
        {
            var seen = new List<string>();
            StepRunner run = Runner(Instant("a", StepStatus.Fail), Instant("b", StepStatus.Pass).Needs("a"));
            run.Finished = r => seen.Add(r.Id);
            run.Tick(0);
            Assert.Equal(new[] { "a", "b" }, seen.ToArray());
        }

        // ---- exceptions ----

        [Fact]
        public void Exception_in_begin_becomes_Error_with_message_and_run_continues()
        {
            StepRunner run = Runner(
                Step("a", 10, r => { throw new InvalidOperationException("kaboom"); }, null),
                Instant("b", StepStatus.Pass));
            run.Tick(0);
            Assert.Equal(StepStatus.Error, run.Records[0].Status);
            Assert.Contains("kaboom", run.Records[0].Reason);
            Assert.Equal(StepStatus.Pass, run.Records[1].Status);
        }

        [Fact]
        public void Exception_in_tick_becomes_Error_and_next_step_starts_next_tick()
        {
            StepRunner run = Runner(
                Step("a", 10, null, (r, e) => { throw new ArgumentException("tick-fail"); }),
                Instant("b", StepStatus.Pass));
            run.Tick(0);
            run.Tick(1);
            Assert.Equal(StepStatus.Error, run.Records[0].Status);
            Assert.Contains("tick-fail", run.Records[0].Reason);
            Assert.False(run.Records[1].Finished);
            run.Tick(2);
            Assert.Equal(StepStatus.Pass, run.Records[1].Status);
        }

        [Fact]
        public void Exception_after_a_verdict_does_not_override_it()
        {
            StepRunner run = Runner(Step("a", 10, r => { r.Pass("ok"); throw new Exception("late"); }, null));
            run.Tick(0);
            Assert.Equal(StepStatus.Pass, run.Records[0].Status);
        }

        // ---- abort ----

        [Fact]
        public void Abort_errors_current_and_skips_the_rest()
        {
            StepRunner run = Runner(Step("a", 100, null, null), Instant("b", StepStatus.Pass), Instant("c", StepStatus.Pass));
            run.Tick(10);
            run.Tick(12);
            run.Abort("level unloaded", 15);
            Assert.True(run.Aborted);
            Assert.Equal("level unloaded", run.AbortReason);
            Assert.True(run.Done);
            Assert.Equal(StepStatus.Error, run.Records[0].Status);
            Assert.Equal("aborted: level unloaded", run.Records[0].Reason);
            Assert.Equal(5, run.Records[0].DurationSeconds);
            Assert.Equal(StepStatus.Skip, run.Records[1].Status);
            Assert.Equal("not run: aborted", run.Records[1].Reason);
            Assert.Equal(StepStatus.Skip, run.Records[2].Status);
            Assert.Null(run.Current);
        }

        [Fact]
        public void Abort_with_no_current_step_skips_all_unstarted()
        {
            StepRunner run = Runner(Instant("a", StepStatus.Pass), Instant("b", StepStatus.Pass));
            run.Abort("early", 0);
            Assert.True(run.Done);
            Assert.Equal(StepStatus.Skip, run.Records[0].Status);
            Assert.Equal(StepStatus.Skip, run.Records[1].Status);
        }

        [Fact]
        public void Abort_keeps_earlier_verdicts()
        {
            StepRunner run = Runner(Instant("a", StepStatus.Pass), Step("b", 50, null, null), Instant("c", StepStatus.Pass));
            run.Tick(0);
            run.Abort("stop", 1);
            Assert.Equal(StepStatus.Pass, run.Records[0].Status);
            Assert.Equal(StepStatus.Error, run.Records[1].Status);
            Assert.Equal(StepStatus.Skip, run.Records[2].Status);
        }

        [Fact]
        public void Abort_fires_Finished_for_each_in_order()
        {
            var seen = new List<string>();
            StepRunner run = Runner(Step("a", 50, null, null), Instant("b", StepStatus.Pass), Instant("c", StepStatus.Pass));
            run.Finished = r => seen.Add(r.Id);
            run.Tick(0);
            run.Abort("x", 1);
            Assert.Equal(new[] { "a", "b", "c" }, seen.ToArray());
        }

        [Fact]
        public void Abort_when_done_is_a_noop()
        {
            StepRunner run = Runner(Instant("a", StepStatus.Pass));
            run.Tick(0);
            int fired = 0;
            run.Finished = r => fired++;
            run.Abort("too late", 5);
            Assert.False(run.Aborted);
            Assert.Equal(0, fired);
            Assert.Equal(StepStatus.Pass, run.Records[0].Status);
        }

        [Fact]
        public void Tick_after_abort_does_nothing()
        {
            bool began = false;
            StepRunner run = Runner(Step("a", 5, r => began = true, null));
            run.Abort("x", 0);
            run.Tick(1);
            Assert.False(began);
        }
    }
}
