using System;
using System.Collections.Generic;

namespace VoidSniffer.SystemTests
{
    /// <summary>Outcome of a single system-test check.</summary>
    public sealed class TestResult
    {
        public TestResult(string name, bool passed, string message)
        {
            Name = name;
            Passed = passed;
            Message = message;
        }

        public string Name { get; private set; }

        public bool Passed { get; private set; }

        public string Message { get; private set; }
    }

    /// <summary>
    /// Collects system-test results and prints a human-readable report. The process exit
    /// code is non-zero when any check failed, so it can be used in CI.
    /// </summary>
    public sealed class TestReport
    {
        private readonly List<TestResult> _results = new List<TestResult>();

        public void Check(string name, Func<string> assertion)
        {
            try
            {
                string message = assertion();
                Pass(name, message);
            }
            catch (Exception ex)
            {
                Fail(name, ex.GetType().Name + ": " + ex.Message);
            }
        }

        public void Pass(string name, string message)
        {
            _results.Add(new TestResult(name, true, message));
            Write(ConsoleColor.Green, "  PASS  " + name + (string.IsNullOrEmpty(message) ? string.Empty : " — " + message));
        }

        public void Fail(string name, string message)
        {
            _results.Add(new TestResult(name, false, message));
            Write(ConsoleColor.Red, "  FAIL  " + name + " — " + message);
        }

        public void Info(string message)
        {
            Write(ConsoleColor.Gray, "        " + message);
        }

        public void Section(string title)
        {
            Console.WriteLine();
            Write(ConsoleColor.Cyan, "== " + title + " ==");
        }

        public int FailedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _results.Count; i++)
                {
                    if (!_results[i].Passed) count++;
                }
                return count;
            }
        }

        public int TotalCount
        {
            get { return _results.Count; }
        }

        public void Summarize()
        {
            Console.WriteLine();
            ConsoleColor color = FailedCount == 0 ? ConsoleColor.Green : ConsoleColor.Red;
            Write(color, string.Format("Итог: пройдено {0} из {1}, провалено {2}.",
                TotalCount - FailedCount, TotalCount, FailedCount));
        }

        private static void Write(ConsoleColor color, string text)
        {
            ConsoleColor previous = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = previous;
        }
    }
}
