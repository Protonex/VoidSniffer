using System;

namespace VoidSniffer.SystemTests
{
    /// <summary>
    /// Entry point for the VoidSniffer system tests. Exercises the application against the
    /// live Everything index and the real treemap pipeline. Usage:
    /// <code>VoidSniffer.SystemTests.exe [scanRoot]</code>
    /// Defaults to <c>C:\</c>. Exit code is 0 when all checks pass, 1 otherwise.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string root = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : "C:\\";

            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("VoidSniffer — системные тесты");
            Console.WriteLine("Корень сканирования: " + root);
            Console.WriteLine(new string('-', 60));

            var report = new TestReport();
            try
            {
                new SystemTests(report).Run(root);
            }
            catch (Exception ex)
            {
                report.Fail("Системные тесты", ex.GetType().Name + ": " + ex.Message);
            }

            report.Summarize();
            return report.FailedCount == 0 ? 0 : 1;
        }
    }
}
