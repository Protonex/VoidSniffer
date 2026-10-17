using System;
using System.Windows.Forms;

namespace VoidSniffer
{
    public static class Program
    {
        /// <summary>
        /// Главная точка входа. Аргументы командной строки:
        /// <code>VoidSniffer.exe scan "C:\Program Files"</code>
        /// <code>VoidSniffer.exe scan C:\</code>
        /// <code>VoidSniffer.exe --lang=en scan C:\</code>
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Localization.Initialize(ParseLanguage(args));

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(ParseScanPath(args)));
        }

        /// <summary>Path to scan from the command line, or null.</summary>
        public static string ParseScanPath(string[] args)
        {
            if (args == null) return null;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (arg.StartsWith("--scan=", StringComparison.OrdinalIgnoreCase))
                {
                    return Clean(arg.Substring("--scan=".Length));
                }
                if (string.Equals(arg, "--scan", StringComparison.OrdinalIgnoreCase))
                {
                    return i + 1 < args.Length ? Clean(args[i + 1]) : null;
                }
                if (string.Equals(arg, "scan", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (arg.StartsWith("--lang=", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (string.Equals(arg, "--lang", StringComparison.OrdinalIgnoreCase))
                {
                    i++; // skip the language value
                    continue;
                }
                if (arg.StartsWith("--", StringComparison.Ordinal))
                {
                    continue;
                }

                return Clean(arg);
            }
            return null;
        }

        /// <summary>Language code from the command line (--lang=xx or --lang xx), or null.</summary>
        public static string ParseLanguage(string[] args)
        {
            if (args == null) return null;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.StartsWith("--lang=", StringComparison.OrdinalIgnoreCase))
                {
                    return Clean(arg.Substring("--lang=".Length));
                }
                if (string.Equals(arg, "--lang", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    return Clean(args[i + 1]);
                }
            }
            return null;
        }

        private static string Clean(string value)
        {
            if (value == null) return null;
            value = value.Trim().Trim('"');
            return value.Length == 0 ? null : value;
        }
    }
}
