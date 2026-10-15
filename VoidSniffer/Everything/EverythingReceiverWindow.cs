using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace VoidSniffer.Everything
{
    /// <summary>
    /// Message-only window that receives result payloads from the Everything IPC
    /// in response to a query, and parses them into an <see cref="EverythingResult"/>.
    /// </summary>
    /// <remarks>
    /// The Everything IPC is asynchronous: a query carries the handle of a reply
    /// window plus a magic value, and Everything answers with a WM_COPYDATA message
    /// to that window. This class owns such a window and the associated parser.
    /// Implementation derived from sgrottel/EverythingSearchClient (MIT).
    /// </remarks>
    internal sealed class EverythingReceiverWindow : IDisposable
    {
        private const string WindowClassName = "VoidSnifferEverythingReceiver";
        private const uint WmCopyData = 0x004A;
        private const uint WmClose = 0x0010;
        private const uint WmUser = 0x0400;

        private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        #region Native structures

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WndClass
        {
            public uint Style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public IntPtr lpszMenuName;
            public IntPtr lpszClassName;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WndClassEx
        {
            public uint cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CopyDataStruct
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMessage
        {
            public IntPtr handle;
            public uint msg;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int x;
            public int y;
        }

        #endregion

        #region P/Invoke

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetClassInfo(IntPtr hInstance, string lpClassName, ref WndClass lpWndClass);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern ushort RegisterClassEx(ref WndClassEx wndClass);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowEx(
            uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
            int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern void PostQuitMessage(int nExitCode);

        [DllImport("user32.dll")]
        private static extern int GetMessage(out NativeMessage lpMsg, IntPtr hWnd, uint min, uint max);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TranslateMessage(ref NativeMessage lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref NativeMessage lpMsg);

        private enum MessageFilterAction : uint { Reset = 0, Allow = 1, DisAllow = 2 }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ChangeWindowMessageFilterEx(IntPtr hWnd, uint msg, MessageFilterAction action, IntPtr changeInfo);

        #endregion

        private static readonly List<KeyValuePair<uint, EverythingReceiverWindow>> Requests =
            new List<KeyValuePair<uint, EverythingReceiverWindow>>();

        private static readonly object RequestsLock = new object();

        private static int _nextReplyId = new Random().Next(1, int.MaxValue);

        // The window class is registered once per process and keeps pointing at this
        // delegate for the whole process lifetime. It must never be collected while the
        // class is alive, otherwise the callback pointer dangles and calls into freed
        // memory (AccessViolationException). Holding it in a static field roots it.
        private static readonly WndProc StaticWndProc = ReceiverWndProc;
        private static readonly IntPtr StaticWndProcPtr = Marshal.GetFunctionPointerForDelegate(StaticWndProc);

        private IntPtr _hwnd = IntPtr.Zero;
        private byte[] _rawQuery = new byte[0];
        private uint _replyId;
        private int _queryVersion;

        public EverythingResult Result { get; private set; }

        public EverythingReceiverWindow()
        {
            IntPtr hInstance = GetHInstance();
            EnsureWindowClassRegistered(hInstance);

            _hwnd = CreateWindowEx(0, WindowClassName, string.Empty, 0, 0, 0, 0, 0,
                IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
            if (_hwnd == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "Failed to create Everything receiver window. Error: " + Marshal.GetLastWin32Error());
            }

            // Allow an unelevated Everything IPC window to send data to this (possibly elevated) window.
            ChangeWindowMessageFilterEx(_hwnd, WmCopyData, MessageFilterAction.Allow, IntPtr.Zero);

            _replyId = (uint)System.Threading.Interlocked.Increment(ref _nextReplyId);
            lock (RequestsLock)
            {
                Requests.Add(new KeyValuePair<uint, EverythingReceiverWindow>(_replyId, this));
            }
        }

        private static IntPtr GetHInstance()
        {
            Module module = null;
            Assembly entry = Assembly.GetEntryAssembly();
            if (entry != null)
            {
                module = entry.ManifestModule;
            }
            if (module == null)
            {
                module = typeof(EverythingReceiverWindow).Module;
            }
            return Marshal.GetHINSTANCE(module);
        }

        public bool BuildQuery2(string query, EverythingSearchFlags flags, uint maxResults, uint offset, uint sortType)
        {
            var q = new EverythingIpc.EverythingIpcQuery2();
            q.ReplyHwnd = (uint)_hwnd;
            q.ReplyCopyDataMessage = _replyId;
            q.SearchFlags = MapSearchFlags(flags);
            q.Offset = offset;
            q.MaxResults = maxResults;
            q.RequestFlags = EverythingIpc.RequestName
                | EverythingIpc.RequestPath
                | EverythingIpc.RequestSize
                | EverythingIpc.RequestDateModified
                | EverythingIpc.RequestAttributes;
            q.SortType = sortType;

            _rawQuery = SerializeQuery(q, query);
            _queryVersion = 2;
            return _rawQuery.Length > 0;
        }

        public bool BuildQuery(string query, EverythingSearchFlags flags, uint maxResults, uint offset)
        {
            var q = new EverythingIpc.EverythingIpcQuery();
            q.ReplyHwnd = (uint)_hwnd;
            q.ReplyCopyDataMessage = _replyId;
            q.SearchFlags = MapSearchFlags(flags);
            q.Offset = offset;
            q.MaxResults = maxResults;

            _rawQuery = SerializeQuery(q, query);
            _queryVersion = 1;
            return _rawQuery.Length > 0;
        }

        private static uint MapSearchFlags(EverythingSearchFlags flags)
        {
            uint value = 0;
            if ((flags & EverythingSearchFlags.MatchCase) != 0) value |= EverythingIpc.MatchCase;
            if ((flags & EverythingSearchFlags.MatchWholeWord) != 0) value |= EverythingIpc.MatchWholeWord;
            if ((flags & EverythingSearchFlags.MatchPath) != 0) value |= EverythingIpc.MatchPath;
            if ((flags & EverythingSearchFlags.RegEx) != 0) value |= EverythingIpc.RegEx;
            return value;
        }

        private static byte[] SerializeQuery<TStruct>(TStruct query, string search) where TStruct : struct
        {
            int structSize = Marshal.SizeOf(typeof(TStruct));
            byte[] result = new byte[structSize + 2 * (search.Length + 1)];

            IntPtr tmp = Marshal.AllocHGlobal(structSize);
            try
            {
                Marshal.StructureToPtr(query, tmp, false);
                Marshal.Copy(tmp, result, 0, structSize);
            }
            finally
            {
                Marshal.FreeHGlobal(tmp);
            }

            byte[] searchBytes = Encoding.Unicode.GetBytes(search + "\0");
            Buffer.BlockCopy(searchBytes, 0, result, structSize, searchBytes.Length);
            return result;
        }

        public bool SendQuery(IntPtr ipcHwnd)
        {
            if (_hwnd == IntPtr.Zero) throw new InvalidOperationException("Receiver window is not initialized.");
            if (_rawQuery.Length <= 0) throw new InvalidOperationException("No query has been built.");
            if (_queryVersion != 1 && _queryVersion != 2) throw new InvalidOperationException("Unsupported query version.");

            IntPtr queryData = IntPtr.Zero;
            IntPtr cdsMem = IntPtr.Zero;
            try
            {
                queryData = Marshal.AllocHGlobal(_rawQuery.Length);
                Marshal.Copy(_rawQuery, 0, queryData, _rawQuery.Length);

                var cds = new CopyDataStruct();
                cds.dwData = (IntPtr)(_queryVersion == 2 ? EverythingIpc.CopyDataQuery2W : EverythingIpc.CopyDataQueryW);
                cds.cbData = _rawQuery.Length;
                cds.lpData = queryData;

                cdsMem = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(CopyDataStruct)));
                Marshal.StructureToPtr(cds, cdsMem, false);

                uint sent = SendMessage(ipcHwnd, WmCopyData, _hwnd, cdsMem);
                return sent != 0;
            }
            finally
            {
                if (cdsMem != IntPtr.Zero) Marshal.FreeHGlobal(cdsMem);
                if (queryData != IntPtr.Zero) Marshal.FreeHGlobal(queryData);
            }
        }

        /// <summary>Runs a message pump on the calling thread until a result arrives or <see cref="SendClose"/> is called.</summary>
        public void MessagePump()
        {
            NativeMessage msg;
            int ret;
            while ((ret = GetMessage(out msg, IntPtr.Zero, 0, 0)) != 0)
            {
                if (ret == -1) break;
                if (msg.msg == WmUser)
                {
                    PostQuitMessage(0);
                    break;
                }
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }

        public void SendClose()
        {
            if (_hwnd == IntPtr.Zero) return;
            PostMessage(_hwnd, WmUser, IntPtr.Zero, IntPtr.Zero);
            PostMessage(_hwnd, WmClose, IntPtr.Zero, IntPtr.Zero);
        }

        private static IntPtr ReceiverWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case WmClose:
                    PostQuitMessage(0);
                    break;
                case WmCopyData:
                    var data = (CopyDataStruct)Marshal.PtrToStructure(lParam, typeof(CopyDataStruct));
                    EverythingReceiverWindow target = null;
                    lock (RequestsLock)
                    {
                        for (int i = 0; i < Requests.Count; i++)
                        {
                            if (Requests[i].Key == (uint)data.dwData)
                            {
                                target = Requests[i].Value;
                                break;
                            }
                        }
                    }
                    if (target != null)
                    {
                        target.ParseResultData(data.lpData, data.cbData);
                    }
                    PostQuitMessage(0);
                    break;
            }
            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        private static void EnsureWindowClassRegistered(IntPtr hInstance)
        {
            var wc = new WndClass();
            if (GetClassInfo(hInstance, WindowClassName, ref wc))
            {
                return;
            }

            var wcex = new WndClassEx();
            wcex.cbSize = (uint)Marshal.SizeOf(typeof(WndClassEx));
            wcex.hInstance = hInstance;
            wcex.lpszClassName = WindowClassName;
            wcex.lpfnWndProc = StaticWndProcPtr;

            ushort atom = RegisterClassEx(ref wcex);
            if (atom == 0)
            {
                throw new InvalidOperationException(
                    "Failed to register receiver window class. Error: " + Marshal.GetLastWin32Error());
            }
        }

        private void ParseResultData(IntPtr mem, int size)
        {
            if (_queryVersion == 2)
            {
                ParseResultDataVersion2(mem);
            }
            else
            {
                ParseResultDataVersion1(mem);
            }
        }

        private void ParseResultDataVersion1(IntPtr mem)
        {
            uint total = (uint)Marshal.ReadInt32(mem + 2 * 4);
            int count = Marshal.ReadInt32(mem + 5 * 4);
            uint offset = (uint)Marshal.ReadInt32(mem + 6 * 4);

            var items = new List<EverythingItem>(count);
            for (int i = 0; i < count; i++)
            {
                EverythingItemFlags flags = MapItemFlags(Marshal.ReadInt32(mem + (7 + i * 3) * 4));
                uint nameOffset = (uint)Marshal.ReadInt32(mem + (7 + i * 3 + 1) * 4);
                string name = Marshal.PtrToStringUni(mem + (int)nameOffset);
                if (string.IsNullOrEmpty(name)) continue;
                uint pathOffset = (uint)Marshal.ReadInt32(mem + (7 + i * 3 + 2) * 4);
                string path = Marshal.PtrToStringUni(mem + (int)pathOffset);
                if (string.IsNullOrEmpty(path)) continue;

                items.Add(new EverythingItem { Flags = flags, Name = name, Path = path });
            }

            Result = new EverythingResult { TotalItems = total, Offset = offset, Items = items.ToArray() };
        }

        private void ParseResultDataVersion2(IntPtr mem)
        {
            uint total = (uint)Marshal.ReadInt32(mem);
            int count = Marshal.ReadInt32(mem + 1 * 4);
            uint offset = (uint)Marshal.ReadInt32(mem + 2 * 4);
            uint requestFlags = (uint)Marshal.ReadInt32(mem + 3 * 4);

            var items = new List<EverythingItem>(count);
            for (int i = 0; i < count; i++)
            {
                EverythingItemFlags flags = MapItemFlags(Marshal.ReadInt32(mem + (5 + i * 2) * 4));
                int dataOffset = Marshal.ReadInt32(mem + (5 + i * 2 + 1) * 4);

                string name = null;
                string path = null;
                ulong? size = null;
                DateTime? modified = null;
                EverythingItemAttributes? attributes = null;

                if ((requestFlags & EverythingIpc.RequestName) != 0)
                {
                    int len = Marshal.ReadInt32(mem + dataOffset);
                    dataOffset += 4;
                    name = Marshal.PtrToStringUni(mem + dataOffset, len);
                    dataOffset += (len + 1) * 2;
                }
                if ((requestFlags & EverythingIpc.RequestPath) != 0)
                {
                    int len = Marshal.ReadInt32(mem + dataOffset);
                    dataOffset += 4;
                    path = Marshal.PtrToStringUni(mem + dataOffset, len);
                    dataOffset += (len + 1) * 2;
                }
                if ((requestFlags & EverythingIpc.RequestFullPathAndName) != 0)
                {
                    int len = Marshal.ReadInt32(mem + dataOffset);
                    dataOffset += 4;
                    dataOffset += (len + 1) * 2;
                }
                if ((requestFlags & EverythingIpc.RequestSize) != 0)
                {
                    size = (ulong)Marshal.ReadInt64(mem + dataOffset);
                    dataOffset += 8;
                }
                if ((requestFlags & EverythingIpc.RequestExtension) != 0)
                {
                    int len = Marshal.ReadInt32(mem + dataOffset);
                    dataOffset += 4;
                    dataOffset += (len + 1) * 2;
                }
                if ((requestFlags & EverythingIpc.RequestDateCreated) != 0)
                {
                    dataOffset += 8;
                }
                if ((requestFlags & EverythingIpc.RequestDateModified) != 0)
                {
                    long timecode = Marshal.ReadInt64(mem + dataOffset);
                    if (timecode > 0)
                    {
                        try { modified = DateTime.FromFileTime(timecode); }
                        catch (ArgumentException) { modified = null; }
                    }
                    dataOffset += 8;
                }
                if ((requestFlags & EverythingIpc.RequestDateAccessed) != 0)
                {
                    dataOffset += 8;
                }
                if ((requestFlags & EverythingIpc.RequestAttributes) != 0)
                {
                    attributes = (EverythingItemAttributes)Marshal.ReadInt32(mem + dataOffset);
                }

                if (name != null && path != null)
                {
                    items.Add(new EverythingItem
                    {
                        Flags = flags,
                        Name = name,
                        Path = path,
                        Size = size,
                        LastWriteTime = modified,
                        Attributes = attributes
                    });
                }
            }

            Result = new EverythingResult { TotalItems = total, Offset = offset, Items = items.ToArray() };
        }

        private static EverythingItemFlags MapItemFlags(int value)
        {
            EverythingItemFlags flags = EverythingItemFlags.None;
            if ((value & EverythingIpc.Folder) != 0) flags |= EverythingItemFlags.Folder;
            if ((value & EverythingIpc.Drive) != 0) flags |= EverythingItemFlags.Drive;
            if ((value & ~(EverythingIpc.Folder | EverythingIpc.Drive)) != 0) flags |= EverythingItemFlags.Unknown;
            return flags;
        }

        public void Dispose()
        {
            lock (RequestsLock)
            {
                for (int i = 0; i < Requests.Count; i++)
                {
                    if (Requests[i].Value == this)
                    {
                        Requests.RemoveAt(i);
                        break;
                    }
                }
            }

            if (_hwnd != IntPtr.Zero)
            {
                DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
            }
        }
    }
}
