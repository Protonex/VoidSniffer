using System;
using System.Windows.Forms;

namespace VoidSniffer
{
    public static class Program
    {
        /// <summary>
        /// Главная точка входа для приложения. Поддерживает аргументы командной строки:
        /// <code>VoidSniffer.exe scan "C:\Program Files"</code>
        /// <code>VoidSniffer.exe scan C:\</code>
        /// <code>VoidSniffer.exe "C:\Users"</code>
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(ParseScanPath(args)));
        }

        /// <summary>Extracts the path to scan from the command line, or null.</summary>
        public static string ParseScanPath(string[] args)
        {
            if (args == null || args.Length == 0) return null;

            int index = 0;
            if (string.Equals(args[0], "scan", StringComparison.OrdinalIgnoreCase))
            {
                index = 1;
            }
            else if (args[0].StartsWith("--scan=", StringComparison.OrdinalIgnoreCase))
            {
                string value = args[0].Substring("--scan=".Length).Trim('"');
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }

            if (index >= args.Length) return null;

            string path = args[index];
            return string.IsNullOrWhiteSpace(path) ? null : path.Trim('"');
        }
    }
}
