using System;
using System.Collections.Generic;
using System.Text.Json;
using GtaSkylines.Mod.SelfTest;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class SelfTestReportTests
    {
        private static StepRecord Rec(string id, StepStatus? s, string reason)
        {
            var r = new StepRecord { Id = id, Name = "Name " + id };
            if (s == StepStatus.Pass) r.Pass(reason);
            else if (s == StepStatus.Fail) r.Fail(reason);
            else if (s == StepStatus.Skip) r.Skip(reason);
            else if (s == StepStatus.Error) r.Error(reason);
            return r;
        }

        private static SelfTestReport Sample()
        {
            var rep = new SelfTestReport
            {
                RunStartedUtc = new DateTime(2026, 10, 10, 12, 30, 45, 123, DateTimeKind.Utc),
                Trigger = "launch.cfg",
                GameVersion = "1.20.1",
                ModVersion = "0.6.0",
                CityName = "Test \"City\""
            };
            StepRecord a = Rec("load", StepStatus.Pass, "city loaded");
            a.StartedSeconds = 0; a.DurationSeconds = 4.5; a.TimeoutSeconds = 60;
            a.Values.Set("fps", 59.5).Set("count", 7).Set("ok", true);
            a.Screenshots.Add("shots/load.png");
            StepRecord b = Rec("walk", StepStatus.Fail, "timeout after 20 s; 3/10");
            b.StartedSeconds = 4.5; b.DurationSeconds = 20; b.TimeoutSeconds = 20; b.TimedOut = true;
            StepRecord c = Rec("drive", StepStatus.Skip, "needs walk");
            StepRecord d = Rec("boom", StepStatus.Error, "exception");
            rep.Steps.Add(a); rep.Steps.Add(b); rep.Steps.Add(c); rep.Steps.Add(d);
            rep.Performance.Set("avg_fps", 58.25);
            rep.ExceptionCount = 2;
            rep.ExceptionLines.Add("12:00:00.000 ERROR one");
            rep.ExceptionLines.Add("12:00:01.000 ERROR two");
            return rep;
        }

        // ---- Json ----

        [Fact] public void Json_null() { Assert.Equal("null", Json.Write(null)); }
        [Fact] public void Json_bools() { Assert.Equal("true", Json.Write(true)); Assert.Equal("false", Json.Write(false)); }

        [Fact]
        public void Json_string_escapes_quote_backslash_and_control_chars()
        {
            Assert.Equal("\"a\\\"b\\\\c\\nd\\re\\tf\"", Json.Write("a\"b\\c\nd\re\tf"));
            Assert.Equal("\"\\u0001\"", Json.Write("\u0001"));
            Assert.Equal("\"plain\"", Json.Write("plain"));
        }

        [Fact]
        public void Json_integers_are_plain()
        {
            Assert.Equal("5", Json.Write(5));
            Assert.Equal("-12", Json.Write(-12));
            Assert.Equal("9000000000", Json.Write(9000000000L));
            Assert.Equal("7", Json.Write((uint)7));
            Assert.Equal("3", Json.Write((short)3));
            Assert.Equal("3", Json.Write((ushort)3));
            Assert.Equal("200", Json.Write((byte)200));
        }

        [Fact]
        public void Json_doubles_use_invariant_round_trip_format()
        {
            Assert.Equal("1.5", Json.Write(1.5));
            Assert.Equal("0.1", Json.Write(0.1));
            Assert.Equal("2.5", Json.Write(2.5f));
        }

        [Fact]
        public void Json_nan_and_infinity_are_null()
        {
            Assert.Equal("null", Json.Write(double.NaN));
            Assert.Equal("null", Json.Write(double.PositiveInfinity));
            Assert.Equal("null", Json.Write(float.NegativeInfinity));
        }

        [Fact]
        public void Json_measurements_is_an_object_in_insertion_order()
        {
            var m = new Measurements().With("z", 1).With("a", "x");
            Assert.Equal("{\"z\":1,\"a\":\"x\"}", Json.Write(m).Replace(" ", "").Replace("\n", "").Replace("\r", ""));
        }

        [Fact]
        public void Json_dictionary_is_an_object()
        {
            var d = new Dictionary<string, object> { { "k", 2 } };
            using (JsonDocument doc = JsonDocument.Parse(Json.Write(d)))
                Assert.Equal(2, doc.RootElement.GetProperty("k").GetInt32());
        }

        [Fact]
        public void Json_enumerable_is_an_array()
        {
            using (JsonDocument doc = JsonDocument.Parse(Json.Write(new List<object> { 1, "two", true, null })))
            {
                Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
                Assert.Equal(4, doc.RootElement.GetArrayLength());
                Assert.Equal("two", doc.RootElement[1].GetString());
                Assert.Equal(JsonValueKind.Null, doc.RootElement[3].ValueKind);
            }
        }

        [Fact]
        public void Json_nested_values_round_trip_through_a_parser()
        {
            var m = new Measurements().With("list", new[] { 1, 2 }).With("inner", new Measurements().With("q", "a\"b"));
            using (JsonDocument doc = JsonDocument.Parse(Json.Write(m)))
            {
                Assert.Equal(2, doc.RootElement.GetProperty("list").GetArrayLength());
                Assert.Equal("a\"b", doc.RootElement.GetProperty("inner").GetProperty("q").GetString());
            }
        }

        [Fact]
        public void Iso_formats_utc_with_milliseconds()
        {
            Assert.Equal("2026-10-10T12:30:45.123Z", Json.Iso(new DateTime(2026, 10, 10, 12, 30, 45, 123, DateTimeKind.Utc)));
        }

        // ---- Count / SummaryLine ----

        [Fact]
        public void Count_tallies_by_status_and_unfinished_counts_as_error()
        {
            SelfTestReport rep = Sample();
            rep.Steps.Add(Rec("pending", null, ""));
            Assert.Equal(1, rep.Count(StepStatus.Pass));
            Assert.Equal(1, rep.Count(StepStatus.Fail));
            Assert.Equal(1, rep.Count(StepStatus.Skip));
            Assert.Equal(2, rep.Count(StepStatus.Error));
        }

        [Fact]
        public void SummaryLine_exact_format()
        {
            Assert.Equal("Self-test: 1 pass, 1 fail, 1 skip, 1 error", Sample().SummaryLine());
        }

        [Fact]
        public void SummaryLine_for_empty_report()
        {
            Assert.Equal("Self-test: 0 pass, 0 fail, 0 skip, 0 error", new SelfTestReport().SummaryLine());
        }

        [Fact]
        public void SummaryLine_appends_abort_reason()
        {
            SelfTestReport rep = Sample();
            rep.Aborted = true;
            rep.AbortReason = "safety timeout";
            Assert.Equal("Self-test: 1 pass, 1 fail, 1 skip, 1 error (aborted: safety timeout)", rep.SummaryLine());
        }

        // ---- ToJson ----

        [Fact]
        public void ToJson_top_level_keys_in_contract_order()
        {
            using (JsonDocument doc = JsonDocument.Parse(Sample().ToJson()))
            {
                var keys = new List<string>();
                foreach (JsonProperty p in doc.RootElement.EnumerateObject()) keys.Add(p.Name);
                Assert.Equal(new[]
                {
                    "run_started_utc", "trigger", "game_version", "mod_version", "city_name", "aborted",
                    "abort_reason", "summary", "steps", "performance", "exceptions"
                }, keys.ToArray());
            }
        }

        [Fact]
        public void ToJson_header_fields()
        {
            using (JsonDocument doc = JsonDocument.Parse(Sample().ToJson()))
            {
                JsonElement r = doc.RootElement;
                Assert.Equal("2026-10-10T12:30:45.123Z", r.GetProperty("run_started_utc").GetString());
                Assert.Equal("launch.cfg", r.GetProperty("trigger").GetString());
                Assert.Equal("1.20.1", r.GetProperty("game_version").GetString());
                Assert.Equal("0.6.0", r.GetProperty("mod_version").GetString());
                Assert.Equal("Test \"City\"", r.GetProperty("city_name").GetString());
                Assert.False(r.GetProperty("aborted").GetBoolean());
                Assert.Equal("", r.GetProperty("abort_reason").GetString());
            }
        }

        [Fact]
        public void ToJson_summary_counts_and_total()
        {
            using (JsonDocument doc = JsonDocument.Parse(Sample().ToJson()))
            {
                JsonElement s = doc.RootElement.GetProperty("summary");
                Assert.Equal(1, s.GetProperty("pass").GetInt32());
                Assert.Equal(1, s.GetProperty("fail").GetInt32());
                Assert.Equal(1, s.GetProperty("skip").GetInt32());
                Assert.Equal(1, s.GetProperty("error").GetInt32());
                Assert.Equal(4, s.GetProperty("total").GetInt32());
            }
        }

        [Fact]
        public void ToJson_step_keys_in_contract_order()
        {
            using (JsonDocument doc = JsonDocument.Parse(Sample().ToJson()))
            {
                var keys = new List<string>();
                foreach (JsonProperty p in doc.RootElement.GetProperty("steps")[0].EnumerateObject()) keys.Add(p.Name);
                Assert.Equal(new[]
                {
                    "id", "name", "result", "reason", "started_s", "duration_s", "timeout_s", "timed_out", "values", "screenshots"
                }, keys.ToArray());
            }
        }

        [Fact]
        public void ToJson_step_values()
        {
            using (JsonDocument doc = JsonDocument.Parse(Sample().ToJson()))
            {
                JsonElement steps = doc.RootElement.GetProperty("steps");
                Assert.Equal(4, steps.GetArrayLength());
                JsonElement a = steps[0];
                Assert.Equal("load", a.GetProperty("id").GetString());
                Assert.Equal("Name load", a.GetProperty("name").GetString());
                Assert.Equal("pass", a.GetProperty("result").GetString());
                Assert.Equal("city loaded", a.GetProperty("reason").GetString());
                Assert.Equal(4.5, a.GetProperty("duration_s").GetDouble());
                Assert.Equal(60, a.GetProperty("timeout_s").GetDouble());
                Assert.False(a.GetProperty("timed_out").GetBoolean());
                Assert.Equal(59.5, a.GetProperty("values").GetProperty("fps").GetDouble());
                Assert.Equal(7, a.GetProperty("values").GetProperty("count").GetInt32());
                Assert.True(a.GetProperty("values").GetProperty("ok").GetBoolean());
                Assert.Equal("shots/load.png", a.GetProperty("screenshots")[0].GetString());
            }
        }

        [Fact]
        public void ToJson_results_are_lower_case_and_timed_out_is_reported()
        {
            using (JsonDocument doc = JsonDocument.Parse(Sample().ToJson()))
            {
                JsonElement steps = doc.RootElement.GetProperty("steps");
                Assert.Equal("fail", steps[1].GetProperty("result").GetString());
                Assert.True(steps[1].GetProperty("timed_out").GetBoolean());
                Assert.Equal("skip", steps[2].GetProperty("result").GetString());
                Assert.Equal("error", steps[3].GetProperty("result").GetString());
                Assert.Equal(0, steps[2].GetProperty("screenshots").GetArrayLength());
            }
        }

        [Fact]
        public void ToJson_performance_and_exceptions()
        {
            using (JsonDocument doc = JsonDocument.Parse(Sample().ToJson()))
            {
                Assert.Equal(58.25, doc.RootElement.GetProperty("performance").GetProperty("avg_fps").GetDouble());
                JsonElement ex = doc.RootElement.GetProperty("exceptions");
                Assert.Equal(2, ex.GetProperty("count").GetInt32());
                Assert.Equal(2, ex.GetProperty("lines").GetArrayLength());
                Assert.Equal("12:00:01.000 ERROR two", ex.GetProperty("lines")[1].GetString());
            }
        }

        [Fact]
        public void ToJson_aborted_report()
        {
            SelfTestReport rep = Sample();
            rep.Aborted = true;
            rep.AbortReason = "level unloaded";
            using (JsonDocument doc = JsonDocument.Parse(rep.ToJson()))
            {
                Assert.True(doc.RootElement.GetProperty("aborted").GetBoolean());
                Assert.Equal("level unloaded", doc.RootElement.GetProperty("abort_reason").GetString());
            }
        }

        [Fact]
        public void ToJson_of_empty_report_is_valid()
        {
            using (JsonDocument doc = JsonDocument.Parse(new SelfTestReport().ToJson()))
            {
                Assert.Equal(0, doc.RootElement.GetProperty("steps").GetArrayLength());
                Assert.Equal(0, doc.RootElement.GetProperty("summary").GetProperty("total").GetInt32());
            }
        }

        // ---- ToText ----

        [Fact]
        public void ToText_starts_with_the_summary_line()
        {
            SelfTestReport rep = Sample();
            Assert.StartsWith(rep.SummaryLine(), rep.ToText());
        }

        [Fact]
        public void ToText_lists_each_step_with_upper_case_result_id_and_reason()
        {
            string t = Sample().ToText();
            string[] lines = t.Split('\n');
            Assert.Contains(lines, l => l.Contains("PASS") && l.Contains("load") && l.Contains("city loaded"));
            Assert.Contains(lines, l => l.Contains("FAIL") && l.Contains("walk") && l.Contains("timeout"));
            Assert.Contains(lines, l => l.Contains("SKIP") && l.Contains("drive") && l.Contains("needs walk"));
            Assert.Contains(lines, l => l.Contains("ERROR") && l.Contains("boom") && l.Contains("exception"));
        }

        [Fact]
        public void ToText_prints_step_values_as_key_value_pairs()
        {
            string t = Sample().ToText();
            Assert.Contains("fps=59.5", t);
            Assert.Contains("count=7", t);
        }

        [Fact]
        public void ToText_has_performance_section_and_exception_count()
        {
            string t = Sample().ToText();
            Assert.Contains("performance", t);
            Assert.Contains("avg_fps=58.25", t);
            Assert.Contains("exceptions: 2", t);
        }
    }
}
