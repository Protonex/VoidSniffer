using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace VoidSniffer.Services
{
    /// <summary>
    /// The Everything index options that VoidSniffer depends on, read from Everything.ini.
    /// A <c>null</c> member means "not found / unknown".
    /// </summary>
    public sealed class EverythingIndexOptions
    {
        public bool? FileSizes { get; set; }
        public bool? FolderSizes { get; set; }
        public bool? DateCreated { get; set; }
        public bool? DateModified { get; set; }
        public bool? DateAccessed { get; set; }
        public bool? Attributes { get; set; }
        public bool? RecentChanges { get; set; }

        /// <summary>True when at least one option was found.</summary>
        public bool Loaded
        {
            get
            {
                return FileSizes.HasValue || FolderSizes.HasValue || DateCreated.HasValue ||
                       DateModified.HasValue || DateAccessed.HasValue || Attributes.HasValue ||
                       RecentChanges.HasValue;
            }
        }

        /// <summary>Finds and parses Everything.ini; returns an empty (unknown) result on failure.</summary>
        public static EverythingIndexOptions Load()
        {
            foreach (string path in CandidateIniPaths())
            {
                try
                {
                    if (!File.Exists(path)) continue;
                    EverythingIndexOptions options = Parse(File.ReadAllText(path));
                    if (options.Loaded) return options;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
            return new EverythingIndexOptions();
        }

        /// <summary>Parses the relevant keys out of Everything.ini text.</summary>
        public static EverythingIndexOptions Parse(string iniText)
        {
            var options = new EverythingIndexOptions();
            if (string.IsNullOrEmpty(iniText)) return options;

            foreach (string rawLine in iniText.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line[0] == ';' || line[0] == '[') continue;

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;

                string key = line.Substring(0, eq).Trim();
                bool value = line.Substring(eq + 1).Trim() != "0";

                switch (key)
                {
                    case "index_size": options.FileSizes = value; break;
                    case "index_folder_size": options.FolderSizes = value; break;
                    case "index_date_created": options.DateCreated = value; break;
                    case "index_date_modified": options.DateModified = value; break;
                    case "index_date_accessed": options.DateAccessed = value; break;
                    case "index_attributes": options.Attributes = value; break;
                    case "index_recent_changes": options.RecentChanges = value; break;
                }
            }
            return options;
        }

        private static IEnumerable<string> CandidateIniPaths()
        {
            string appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Everything", "Everything.ini");
            yield return appData;

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            yield return Path.Combine(programFiles, "Everything", "Everything.ini");

            // Portable install: Everything.ini next to the running Everything.exe.
            foreach (Process process in SafeGetEverythingProcesses())
            {
                string dir = null;
                try { dir = Path.GetDirectoryName(process.MainModule.FileName); }
                catch (Exception) { }
                if (!string.IsNullOrEmpty(dir)) yield return Path.Combine(dir, "Everything.ini");
            }
        }

        private static IEnumerable<Process> SafeGetEverythingProcesses()
        {
            try { return Process.GetProcessesByName("Everything"); }
            catch (Exception) { return new Process[0]; }
        }
    }
}
