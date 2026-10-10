using System.Collections.Generic;

// Unity-free part of the in-game self-test, compiled into GtaSkylines.Mod.Tests as well.
namespace GtaSkylines.Mod.SelfTest
{
    /// <summary>Counts ERROR lines in the mod log (HostLog format: <c>HH:mm:ss.fff LEVEL message</c>).</summary>
    internal static class LogScan
    {
        public static int CountErrors(string logText)
        {
            int n = 0;
            foreach (string line in Lines(logText))
                if (IsError(line)) n++;
            return n;
        }

        public static List<string> ErrorLines(string logText, int max)
        {
            var result = new List<string>();
            foreach (string line in Lines(logText))
            {
                if (result.Count >= max) break;
                if (IsError(line)) result.Add(line);
            }
            return result;
        }

        public static string Since(string logText, long startLength)
        {
            string t = logText ?? "";
            if (startLength <= 0) return t;
            if (startLength >= t.Length) return "";
            return t.Substring((int)startLength);
        }

        private static IEnumerable<string> Lines(string text)
        {
            foreach (string l in (text ?? "").Split('\n')) yield return l.TrimEnd('\r');
        }

        private static bool IsError(string l)
        {
            if (l.Length < 18) return false;
            for (int i = 0; i < 12; i++)
            {
                char c = l[i];
                bool want = i == 2 || i == 5 ? c == ':' : i == 8 ? c == '.' : char.IsDigit(c);
                if (!want) return false;
            }
            return l[12] == ' ' && string.CompareOrdinal(l, 13, "ERROR", 0, 5) == 0;
        }
    }
}
