using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

// Unity-free part of the in-game self-test, compiled into GtaSkylines.Mod.Tests as well.
namespace GtaSkylines.Mod.SelfTest
{
    /// <summary>Minimal JSON writer for the report (no dependency available on net35).</summary>
    internal static class Json
    {
        public static string Write(object value)
        {
            var sb = new StringBuilder();
            Append(sb, value);
            return sb.ToString();
        }

        public static string Iso(DateTime utc)
        {
            return utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        }

        private static void Append(StringBuilder sb, object v)
        {
            if (v == null) sb.Append("null");
            else if (v is bool) sb.Append((bool)v ? "true" : "false");
            else if (v is string) Quote(sb, (string)v);
            else if (v is double || v is float)
            {
                double d = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                if (double.IsNaN(d) || double.IsInfinity(d)) sb.Append("null");
                else sb.Append(v is float ? ((float)v).ToString("R", CultureInfo.InvariantCulture) : d.ToString("R", CultureInfo.InvariantCulture));
            }
            else if (v is int || v is long || v is short || v is ushort || v is uint || v is byte)
                sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture));
            else if (v is Measurements) Object(sb, ((Measurements)v).Items);
            else if (v is IDictionary<string, object>) Object(sb, (IDictionary<string, object>)v);
            else if (v is IEnumerable)
            {
                sb.Append('[');
                bool first = true;
                foreach (object o in (IEnumerable)v)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    Append(sb, o);
                }
                sb.Append(']');
            }
            else Quote(sb, Convert.ToString(v, CultureInfo.InvariantCulture));
        }

        private static void Object(StringBuilder sb, IEnumerable<KeyValuePair<string, object>> items)
        {
            sb.Append('{');
            bool first = true;
            foreach (KeyValuePair<string, object> kv in items)
            {
                if (!first) sb.Append(',');
                first = false;
                Quote(sb, kv.Key);
                sb.Append(':');
                Append(sb, kv.Value);
            }
            sb.Append('}');
        }

        private static void Quote(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }

    /// <summary>The self-test report: JSON for tools, text for people.</summary>
    internal sealed class SelfTestReport
    {
        public DateTime RunStartedUtc;
        public string Trigger = "", GameVersion = "", ModVersion = "", CityName = "";
        public bool Aborted;
        public string AbortReason = "";
        public readonly List<StepRecord> Steps = new List<StepRecord>();
        public readonly Measurements Performance = new Measurements();
        public int ExceptionCount;
        public readonly List<string> ExceptionLines = new List<string>();

        public int Count(StepStatus s)
        {
            int n = 0;
            foreach (StepRecord r in Steps)
                if ((r.Status ?? StepStatus.Error) == s) n++;
            return n;
        }

        public string SummaryLine()
        {
            return "Self-test: " + Count(StepStatus.Pass) + " pass, " + Count(StepStatus.Fail) + " fail, "
                + Count(StepStatus.Skip) + " skip, " + Count(StepStatus.Error) + " error"
                + (Aborted ? " (aborted: " + AbortReason + ")" : "");
        }

        public string ToJson()
        {
            var steps = new List<object>();
            foreach (StepRecord r in Steps)
            {
                steps.Add(new Measurements()
                    .With("id", r.Id).With("name", r.Name).With("result", Result(r))
                    .With("reason", r.Reason).With("started_s", r.StartedSeconds).With("duration_s", r.DurationSeconds)
                    .With("timeout_s", r.TimeoutSeconds).With("timed_out", r.TimedOut)
                    .With("values", r.Values).With("screenshots", r.Screenshots));
            }
            var summary = new Measurements()
                .With("pass", Count(StepStatus.Pass)).With("fail", Count(StepStatus.Fail))
                .With("skip", Count(StepStatus.Skip)).With("error", Count(StepStatus.Error)).With("total", Steps.Count);
            return Json.Write(new Measurements()
                .With("run_started_utc", Json.Iso(RunStartedUtc)).With("trigger", Trigger).With("game_version", GameVersion)
                .With("mod_version", ModVersion).With("city_name", CityName).With("aborted", Aborted)
                .With("abort_reason", AbortReason).With("summary", summary).With("steps", steps)
                .With("performance", Performance)
                .With("exceptions", new Measurements().With("count", ExceptionCount).With("lines", ExceptionLines)));
        }

        public string ToText()
        {
            var sb = new StringBuilder();
            sb.Append(SummaryLine()).Append('\n');
            sb.Append("started ").Append(Json.Iso(RunStartedUtc)).Append(" by ").Append(Trigger).Append("; game ").Append(GameVersion)
              .Append(", mod ").Append(ModVersion).Append(", city '").Append(CityName).Append("'\n\n");
            foreach (StepRecord r in Steps)
            {
                sb.Append(Result(r).ToUpperInvariant().PadRight(6)).Append(r.Id.PadRight(18))
                  .Append(r.DurationSeconds.ToString("0.0", CultureInfo.InvariantCulture).PadLeft(7)).Append(" s  ").Append(r.Reason).Append('\n');
                if (r.Values.Count > 0)
                {
                    sb.Append("      ");
                    foreach (KeyValuePair<string, object> kv in r.Values.Items) sb.Append(' ').Append(kv.Key).Append('=').Append(Json.Write(kv.Value));
                    sb.Append('\n');
                }
                foreach (string s in r.Screenshots) sb.Append("       screenshot ").Append(s).Append('\n');
            }
            sb.Append("\nperformance\n");
            foreach (KeyValuePair<string, object> kv in Performance.Items) sb.Append("  ").Append(kv.Key).Append('=').Append(Json.Write(kv.Value)).Append('\n');
            sb.Append("exceptions: ").Append(ExceptionCount).Append('\n');
            foreach (string l in ExceptionLines) sb.Append("  ").Append(l).Append('\n');
            return sb.ToString();
        }

        private static string Result(StepRecord r)
        {
            return (r.Status ?? StepStatus.Error).ToString().ToLowerInvariant();
        }
    }
}
