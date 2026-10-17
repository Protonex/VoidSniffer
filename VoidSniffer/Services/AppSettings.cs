using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace VoidSniffer.Services
{
    /// <summary>
    /// Application settings persisted in <c>VoidSniffer.ini</c> next to the executable.
    /// Missing or invalid values fall back to defaults.
    /// </summary>
    public sealed class AppSettings
    {
        public AppSettings()
        {
            Language = null;
            Sdk3 = false;
            MaxResults = 200000;
            RecursionDepth = 4;
            ShowFreeSpace = false;
            AutoExpand = true;
            AutoExpandBudget = 300;
            AutoExpandSeconds = 12;
            WindowWidth = 960;
            WindowHeight = 620;
            WindowMaximized = false;
            LastPath = "C:\\";
        }

        public string Language { get; set; }
        public bool Sdk3 { get; set; }
        public int MaxResults { get; set; }
        public int RecursionDepth { get; set; }
        public bool ShowFreeSpace { get; set; }
        public bool AutoExpand { get; set; }
        public int AutoExpandBudget { get; set; }
        public int AutoExpandSeconds { get; set; }
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }
        public bool WindowMaximized { get; set; }
        public string LastPath { get; set; }

        /// <summary>Path to the settings file, always in the executable's directory.</summary>
        public static string FilePath
        {
            get
            {
                string directory = null;
                try
                {
                    Assembly entry = Assembly.GetEntryAssembly();
                    if (entry != null) directory = Path.GetDirectoryName(entry.Location);
                }
                catch (Exception)
                {
                }
                if (string.IsNullOrEmpty(directory)) directory = AppContext.BaseDirectory;
                return Path.Combine(directory, "VoidSniffer.ini");
            }
        }

        /// <summary>Loads the settings file, or returns defaults when it is absent.</summary>
        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    return Parse(File.ReadAllText(FilePath, Encoding.UTF8));
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            return new AppSettings();
        }

        /// <summary>Saves the settings next to the executable (errors are ignored).</summary>
        public void Save()
        {
            try
            {
                File.WriteAllText(FilePath, ToIni(), Encoding.UTF8);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        /// <summary>Parses settings text (unit tested; also the persistence format).</summary>
        public static AppSettings Parse(string text)
        {
            var settings = new AppSettings();
            var map = IniFile.Parse(text);

            settings.Language = GetString(map, "language");
            settings.Sdk3 = GetBool(map, "sdk3", settings.Sdk3);
            settings.MaxResults = GetInt(map, "max_results", settings.MaxResults);
            settings.RecursionDepth = GetInt(map, "recursion_depth", settings.RecursionDepth);
            settings.ShowFreeSpace = GetBool(map, "show_free_space", settings.ShowFreeSpace);
            settings.AutoExpand = GetBool(map, "auto_expand", settings.AutoExpand);
            settings.AutoExpandBudget = GetInt(map, "auto_expand_budget", settings.AutoExpandBudget);
            settings.AutoExpandSeconds = GetInt(map, "auto_expand_seconds", settings.AutoExpandSeconds);
            settings.WindowWidth = GetInt(map, "window_width", settings.WindowWidth);
            settings.WindowHeight = GetInt(map, "window_height", settings.WindowHeight);
            settings.WindowMaximized = GetBool(map, "window_maximized", settings.WindowMaximized);
            settings.LastPath = GetString(map, "last_path") ?? settings.LastPath;

            settings.Normalize();
            return settings;
        }

        /// <summary>Serializes the settings (INI text, unit tested).</summary>
        public string ToIni()
        {
            Normalize();
            var sb = new StringBuilder();
            sb.AppendLine("; VoidSniffer settings - this file lives next to VoidSniffer.exe.");
            sb.AppendLine("; Boolean options: 1 = on, 0 = off. Out-of-range values are clamped.");
            sb.AppendLine();
            sb.AppendLine("; Interface language: ru, en or any code from lang\\<code>.lang.");
            sb.AppendLine("; Empty = auto (command line > VOIDSNIFFER_LANG > this file > OS culture).");
            sb.AppendLine("language=" + (Language ?? string.Empty));
            sb.AppendLine();
            sb.AppendLine("; Use the Everything 1.5 pipe API (SDK3) when available (0/1).");
            sb.AppendLine("; Leave 0 for Everything 1.4 or if the 1.5 beta pipe is unstable.");
            sb.AppendLine("sdk3=" + (Sdk3 ? "1" : "0"));
            sb.AppendLine();
            sb.AppendLine("; Maximum entries returned by one index query (>= 1000).");
            sb.AppendLine("max_results=" + MaxResults.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine();
            sb.AppendLine("; Nesting depth from the current folder, used for drawing and auto-expansion (1..8).");
            sb.AppendLine("recursion_depth=" + RecursionDepth.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine();
            sb.AppendLine("; Show the volume's free space as a separate tile (0/1).");
            sb.AppendLine("show_free_space=" + (ShowFreeSpace ? "1" : "0"));
            sb.AppendLine();
            sb.AppendLine("; Expand the largest subfolders in the background so nesting appears (0/1).");
            sb.AppendLine("auto_expand=" + (AutoExpand ? "1" : "0"));
            sb.AppendLine("; Maximum number of folders auto-expanded per pass.");
            sb.AppendLine("auto_expand_budget=" + AutoExpandBudget.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("; Time limit for one auto-expansion pass, seconds.");
            sb.AppendLine("auto_expand_seconds=" + AutoExpandSeconds.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine();
            sb.AppendLine("; Main window size and state.");
            sb.AppendLine("window_width=" + WindowWidth.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("window_height=" + WindowHeight.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("window_maximized=" + (WindowMaximized ? "1" : "0"));
            sb.AppendLine();
            sb.AppendLine("; Last scanned root, prefilled on start (updated on exit).");
            sb.AppendLine("last_path=" + (LastPath ?? string.Empty));
            return sb.ToString();
        }

        private void Normalize()
        {
            RecursionDepth = Clamp(RecursionDepth, 1, 8);
            MaxResults = Clamp(MaxResults, 1000, 10000000);
            AutoExpandBudget = Clamp(AutoExpandBudget, 10, 100000);
            AutoExpandSeconds = Clamp(AutoExpandSeconds, 1, 600);
            WindowWidth = Clamp(WindowWidth, 400, 20000);
            WindowHeight = Clamp(WindowHeight, 300, 20000);
            if (string.IsNullOrEmpty(LastPath)) LastPath = "C:\\";
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static string GetString(System.Collections.Generic.Dictionary<string, string> map, string key)
        {
            string value;
            return map.TryGetValue(key, out value) && value.Length > 0 ? value : null;
        }

        private static int GetInt(System.Collections.Generic.Dictionary<string, string> map, string key, int fallback)
        {
            string value;
            int parsed;
            if (map.TryGetValue(key, out value) &&
                int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }
            return fallback;
        }

        private static bool GetBool(System.Collections.Generic.Dictionary<string, string> map, string key, bool fallback)
        {
            string value;
            if (!map.TryGetValue(key, out value)) return fallback;

            value = value.Trim();
            return value == "1" ||
                   string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
        }
    }
}
