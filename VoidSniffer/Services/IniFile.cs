using System;
using System.Collections.Generic;

namespace VoidSniffer.Services
{
    /// <summary>Minimal INI-like parser shared by the language packs and the settings file.</summary>
    public static class IniFile
    {
        public static Dictionary<string, string> Parse(string text)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(text)) return map;

            foreach (string rawLine in text.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line[0] == ';' || line[0] == '#' || line[0] == '[') continue;

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;

                string key = line.Substring(0, eq).Trim();
                string value = Unescape(line.Substring(eq + 1).Trim());
                map[key] = value;
            }
            return map;
        }

        public static string Unescape(string value)
        {
            if (value.IndexOf('\\') < 0) return value;
            return value.Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\\\", "\\");
        }
    }
}
