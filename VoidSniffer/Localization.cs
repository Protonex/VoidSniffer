using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace VoidSniffer
{
    /// <summary>A language pack discovered next to the application (or built in).</summary>
    public sealed class LanguageInfo
    {
        public LanguageInfo(string code, string name, bool embedded)
        {
            Code = code;
            Name = name;
            Embedded = embedded;
        }

        public string Code { get; private set; }
        public string Name { get; private set; }
        public bool Embedded { get; private set; }
    }

    /// <summary>
    /// Localization through editable language packs. A pack is an INI-like UTF-8 file of
    /// <c>key=value</c> lines in <c>lang\&lt;code&gt;.lang</c> next to the executable; the
    /// built-in Russian pack is the fallback, so a partial pack can translate only some keys.
    /// </summary>
    public static class Localization
    {
        private const string ResourcePrefix = "VoidSniffer.lang.";
        private const string DefaultCode = "ru";

        private static Dictionary<string, string> _active = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, string> _fallback = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static string _pluralRule = "en";

        public static string Code { get; private set; }

        public static string Name { get; private set; }

        public static bool Initialized { get; private set; }

        public static string LanguageDirectory
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lang"); }
        }

        /// <summary>Raised after the active language changes.</summary>
        public static event EventHandler Changed;

        /// <summary>
        /// Loads the requested language (falls back to the built-in default). A null code
        /// means auto-detect from the current UI culture or the VOIDSNIFFER_LANG variable.
        /// </summary>
        public static void Initialize(string requestedCode = null)
        {
            _fallback = ParseEmbedded(DefaultCode);

            string code = requestedCode;
            if (string.IsNullOrWhiteSpace(code))
            {
                code = Environment.GetEnvironmentVariable("VOIDSNIFFER_LANG");
            }
            if (string.IsNullOrWhiteSpace(code))
            {
                code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            }
            code = (code ?? DefaultCode).Trim().ToLowerInvariant();

            _active = new Dictionary<string, string>(_fallback, StringComparer.OrdinalIgnoreCase);

            Dictionary<string, string> embedded = ParseEmbedded(code);
            Merge(_active, embedded);

            Dictionary<string, string> external = ParseExternal(code);
            Merge(_active, external);

            if (embedded.Count == 0 && external.Count == 0 && !string.Equals(code, DefaultCode, StringComparison.OrdinalIgnoreCase))
            {
                code = DefaultCode; // unknown language: stay on the built-in default
            }

            Code = code;
            Name = Get("language.name") ?? code;
            _pluralRule = (Get("language.plural") ?? "en").Trim().ToLowerInvariant();
            Initialized = true;
        }

        /// <summary>Activates a language and notifies listeners.</summary>
        public static void SetLanguage(string code)
        {
            Initialize(code);
            EventHandler handler = Changed;
            if (handler != null) handler(null, EventArgs.Empty);
        }

        /// <summary>Localized string for a key; falls back to the built-in pack, then the key.</summary>
        public static string T(string key, params object[] args)
        {
            Ensure();
            string value = Get(key);
            if (value == null) return key;

            if (args != null && args.Length > 0)
            {
                try { return string.Format(CultureInfo.CurrentCulture, value, args); }
                catch (FormatException) { return value; }
            }
            return value;
        }

        /// <summary>Localized count with the active language's plural form (e.g. "5 files").</summary>
        public static string Plural(string baseKey, long value)
        {
            Ensure();
            string form = SelectPluralForm(value);
            string word = Get(baseKey + "." + form)
                          ?? Get(baseKey + ".many")
                          ?? Get(baseKey + ".other")
                          ?? baseKey;
            return value.ToString("N0", CultureInfo.CurrentCulture) + " " + word;
        }

        /// <summary>Lists built-in and external language packs.</summary>
        public static IList<LanguageInfo> AvailableLanguages()
        {
            var result = new List<LanguageInfo>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string code in EmbeddedCodes())
            {
                Dictionary<string, string> pack = ParseEmbedded(code);
                string name = GetFrom(pack, "language.name") ?? code;
                if (seen.Add(code)) result.Add(new LanguageInfo(code, name, true));
            }

            try
            {
                if (Directory.Exists(LanguageDirectory))
                {
                    foreach (string file in Directory.GetFiles(LanguageDirectory, "*.lang"))
                    {
                        Dictionary<string, string> pack = Parse(File.ReadAllText(file));
                        string code = GetFrom(pack, "language.code") ?? Path.GetFileNameWithoutExtension(file);
                        string name = GetFrom(pack, "language.name") ?? code;
                        if (seen.Add(code)) result.Add(new LanguageInfo(code, name, false));
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            return result;
        }

        /// <summary>Parses a language pack into a key/value dictionary (unit tested).</summary>
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

        private static string Get(string key)
        {
            string value;
            if (_active.TryGetValue(key, out value)) return value;
            if (_fallback.TryGetValue(key, out value)) return value;
            return null;
        }

        // Fallback for callers (and tests) that never called Initialize: use the built-in
        // default pack rather than the OS culture, so behaviour is deterministic.
        private static void Ensure()
        {
            if (!Initialized) Initialize(DefaultCode);
        }

        private static string GetFrom(Dictionary<string, string> map, string key)
        {
            string value;
            return map.TryGetValue(key, out value) ? value : null;
        }

        private static string SelectPluralForm(long value)
        {
            long mod100 = value % 100;
            long mod10 = value % 10;

            if (_pluralRule == "ru")
            {
                if (mod10 == 1 && mod100 != 11) return "one";
                if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return "few";
                return "many";
            }

            // en and most others: singular only for exactly one.
            return value == 1 ? "one" : "many";
        }

        private static void Merge(Dictionary<string, string> target, Dictionary<string, string> source)
        {
            foreach (KeyValuePair<string, string> pair in source)
            {
                target[pair.Key] = pair.Value;
            }
        }

        private static Dictionary<string, string> ParseEmbedded(string code)
        {
            try
            {
                string name = ResourcePrefix + code + ".lang";
                using (Stream stream = typeof(Localization).Assembly.GetManifestResourceStream(name))
                {
                    if (stream == null) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        return Parse(reader.ReadToEnd());
                    }
                }
            }
            catch (Exception)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static Dictionary<string, string> ParseExternal(string code)
        {
            try
            {
                string path = Path.Combine(LanguageDirectory, code + ".lang");
                if (File.Exists(path)) return Parse(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> EmbeddedCodes()
        {
            var codes = new List<string>();
            try
            {
                foreach (string name in typeof(Localization).Assembly.GetManifestResourceNames())
                {
                    if (name.StartsWith(ResourcePrefix, StringComparison.OrdinalIgnoreCase) &&
                        name.EndsWith(".lang", StringComparison.OrdinalIgnoreCase))
                    {
                        string code = name.Substring(ResourcePrefix.Length);
                        code = code.Substring(0, code.Length - ".lang".Length);
                        codes.Add(code);
                    }
                }
            }
            catch (Exception)
            {
            }
            if (codes.Count == 0) codes.Add(DefaultCode);
            return codes;
        }

        private static string Unescape(string value)
        {
            if (value.IndexOf('\\') < 0) return value;
            return value.Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\\\", "\\");
        }
    }
}
