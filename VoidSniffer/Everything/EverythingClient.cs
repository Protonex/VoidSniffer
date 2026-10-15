using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace VoidSniffer.Everything
{
    /// <summary>Search flags understood by the Everything IPC.</summary>
    [Flags]
    public enum EverythingSearchFlags
    {
        None = 0,
        MatchCase = 1,
        MatchWholeWord = 2,
        MatchPath = 4,
        RegEx = 8
    }

    /// <summary>Sort column for a search request.</summary>
    public enum EverythingSort
    {
        None,
        Name,
        Path,
        Size,
        Extension,
        DateCreated,
        DateModified
    }

    /// <summary>What to do when the Everything database is busy with another query.</summary>
    public enum EverythingBusyBehavior
    {
        /// <summary>Wait, then throw <see cref="EverythingBusyException"/> if still busy.</summary>
        WaitOrError,

        /// <summary>Wait, then issue anyway (may abort someone else's query).</summary>
        WaitOrContinue,

        /// <summary>Throw immediately.</summary>
        BusyError,

        /// <summary>Issue immediately.</summary>
        Continue
    }

    public sealed class EverythingBusyException : Exception
    {
        public EverythingBusyException() : base("The Everything database is busy with another query.") { }
    }

    public sealed class EverythingNotAvailableException : Exception
    {
        public EverythingNotAvailableException()
            : base("Everything is not running or its IPC window is unavailable.") { }
    }

    public sealed class EverythingResultsNotReceivedException : Exception
    {
        public EverythingResultsNotReceivedException() : base("No results were received from Everything.") { }
    }

    /// <summary>
    /// Managed client for the Everything (voidtools) IPC interface.
    /// Requires Everything to be running; no native SDK DLL is needed.
    /// </summary>
    public sealed class EverythingClient
    {
        private const uint DefaultTimeoutMs = 60 * 1000;

        private const string IpcWindowClass = EverythingIpc.IpcWindowClass;

        private IntPtr _ipcHwnd = IntPtr.Zero;

        /// <summary>How long to wait for the result payload before giving up.</summary>
        public TimeSpan ReceiveTimeout { get; set; }

        public EverythingClient()
        {
            ReceiveTimeout = TimeSpan.FromSeconds(15);
        }

        public bool IsAvailable()
        {
            if (_ipcHwnd != IntPtr.Zero && IsWindow(_ipcHwnd) && IsIpcWindow(_ipcHwnd))
            {
                return true;
            }

            _ipcHwnd = FindWindow(IpcWindowClass, null);
            return _ipcHwnd != IntPtr.Zero && IsWindow(_ipcHwnd) && IsIpcWindow(_ipcHwnd);
        }

        public bool IsBusy()
        {
            if (!IsAvailable()) throw new EverythingNotAvailableException();
            return SendMessage(_ipcHwnd, EverythingIpc.WmIpc, EverythingIpc.IsDbBusy, UIntPtr.Zero) != UIntPtr.Zero;
        }

        public Version GetVersion()
        {
            if (!IsAvailable()) throw new EverythingNotAvailableException();
            int major = (int)SendMessage(_ipcHwnd, EverythingIpc.WmIpc, EverythingIpc.GetMajorVersion, UIntPtr.Zero);
            int minor = (int)SendMessage(_ipcHwnd, EverythingIpc.WmIpc, EverythingIpc.GetMinorVersion, UIntPtr.Zero);
            int revision = (int)SendMessage(_ipcHwnd, EverythingIpc.WmIpc, EverythingIpc.GetRevision, UIntPtr.Zero);
            int build = (int)SendMessage(_ipcHwnd, EverythingIpc.WmIpc, EverythingIpc.GetBuildNumber, UIntPtr.Zero);
            return new Version(major, minor, revision, build);
        }

        /// <summary>
        /// Runs a synchronous search. Blocks until the result arrives, so callers
        /// on a UI thread should marshal this onto a background thread.
        /// </summary>
        public EverythingResult Search(
            string query,
            EverythingSearchFlags flags = EverythingSearchFlags.None,
            uint maxResults = EverythingIpc.AllResults,
            uint offset = 0,
            EverythingSort sort = EverythingSort.None,
            bool sortDescending = false,
            EverythingBusyBehavior whenBusy = EverythingBusyBehavior.WaitOrError,
            uint timeoutMs = DefaultTimeoutMs)
        {
            if (!IsAvailable()) throw new EverythingNotAvailableException();

            using (var receiver = new EverythingReceiverWindow())
            {
                bool sent = false;

                // Prefer query API v2 (it exposes size/date/attributes).
                if (receiver.BuildQuery2(query, flags, maxResults, offset, MapSort(sort, sortDescending)))
                {
                    HandleBusy(whenBusy, timeoutMs);
                    sent = receiver.SendQuery(_ipcHwnd);
                }

                if (!sent)
                {
                    if (!receiver.BuildQuery(query, flags, maxResults, offset))
                    {
                        throw new InvalidOperationException("Failed to build the Everything query.");
                    }
                    HandleBusy(whenBusy, timeoutMs);
                    if (!receiver.SendQuery(_ipcHwnd))
                    {
                        throw new InvalidOperationException("Failed to send the Everything query.");
                    }
                }

                bool pumping = true;
                var watchdog = new Thread(() =>
                {
                    DateTime last = DateTime.Now;
                    while (pumping)
                    {
                        Thread.Sleep(10);
                        if (IsBusy())
                        {
                            last = DateTime.Now;
                        }
                        else if ((DateTime.Now - last) > ReceiveTimeout)
                        {
                            receiver.SendClose();
                            break;
                        }
                    }
                });
                watchdog.IsBackground = true;
                watchdog.Start();

                try
                {
                    receiver.MessagePump();
                }
                finally
                {
                    pumping = false;
                    watchdog.Join();
                }

                if (receiver.Result == null)
                {
                    throw new EverythingResultsNotReceivedException();
                }

                return receiver.Result;
            }
        }

        private static uint MapSort(EverythingSort sort, bool descending)
        {
            if (sort == EverythingSort.None) return 0;
            switch (sort)
            {
                case EverythingSort.Name:
                    return descending ? EverythingIpc.SortNameDescending : EverythingIpc.SortNameAscending;
                case EverythingSort.Path:
                    return descending ? EverythingIpc.SortPathDescending : EverythingIpc.SortPathAscending;
                case EverythingSort.Size:
                    return descending ? EverythingIpc.SortSizeDescending : EverythingIpc.SortSizeAscending;
                case EverythingSort.Extension:
                    return descending ? EverythingIpc.SortExtensionDescending : EverythingIpc.SortExtensionAscending;
                case EverythingSort.DateCreated:
                    return descending ? EverythingIpc.SortDateCreatedDescending : EverythingIpc.SortDateCreatedAscending;
                case EverythingSort.DateModified:
                    return descending ? EverythingIpc.SortDateModifiedDescending : EverythingIpc.SortDateModifiedAscending;
                default:
                    return 0;
            }
        }

        private void HandleBusy(EverythingBusyBehavior whenBusy, uint timeoutMs)
        {
            if (!IsBusy()) return;

            switch (whenBusy)
            {
                case EverythingBusyBehavior.Continue:
                    return;
                case EverythingBusyBehavior.BusyError:
                    throw new EverythingBusyException();
                case EverythingBusyBehavior.WaitOrContinue:
                    WaitForIdle(timeoutMs);
                    return;
                case EverythingBusyBehavior.WaitOrError:
                    if (!WaitForIdle(timeoutMs)) throw new EverythingBusyException();
                    return;
                default:
                    throw new ArgumentOutOfRangeException("whenBusy");
            }
        }

        private bool WaitForIdle(uint timeoutMs)
        {
            DateTime start = DateTime.Now;
            while (IsBusy())
            {
                if (timeoutMs > 0 && (DateTime.Now - start).TotalMilliseconds > timeoutMs)
                {
                    return false;
                }
                Thread.Sleep(10);
            }
            return true;
        }

        private static bool IsIpcWindow(IntPtr hwnd)
        {
            var className = new StringBuilder(256);
            int len = GetClassName(hwnd, className, className.Capacity);
            if (len <= 0) return false;
            return string.Equals(className.ToString(), IpcWindowClass, StringComparison.Ordinal);
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern UIntPtr SendMessage(IntPtr hWnd, uint msg, uint wParam, UIntPtr lParam);
    }
}
