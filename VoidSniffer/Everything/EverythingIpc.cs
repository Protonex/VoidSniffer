using System;
using System.Runtime.InteropServices;

namespace VoidSniffer.Everything
{
    /// <summary>
    /// Protocol constants and native structures of the Everything IPC interface.
    /// Based on <c>everything_ipc.h</c> from the official Everything SDK (voidtools).
    /// </summary>
    internal static class EverythingIpc
    {
        /// <summary>Window class of the Everything IPC (taskbar notification) window.</summary>
        internal const string IpcWindowClass = "EVERYTHING_TASKBAR_NOTIFICATION";

        /// <summary>Base message used to talk to the Everything IPC window (<c>WM_USER</c>).</summary>
        internal const uint WmIpc = 0x0400;

        internal const uint GetMajorVersion = 0;
        internal const uint GetMinorVersion = 1;
        internal const uint GetRevision = 2;
        internal const uint GetBuildNumber = 3;
        internal const uint IsDbBusy = 402;

        // Search flags (EVERYTHING_IPC_MATCH*).
        internal const uint MatchCase = 0x00000001;
        internal const uint MatchWholeWord = 0x00000002;
        internal const uint MatchPath = 0x00000004;
        internal const uint RegEx = 0x00000008;

        /// <summary>WM_COPYDATA query, unicode variant (first query API).</summary>
        internal const uint CopyDataQueryW = 2;

        /// <summary>WM_COPYDATA query 2, unicode variant (exposes size/date/attributes).</summary>
        internal const uint CopyDataQuery2W = 18;

        // Item flags.
        internal const uint Folder = 0x00000001;
        internal const uint Drive = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        internal struct EverythingIpcQuery
        {
            /// <summary>Window that receives the results (low 32 bits of the HWND).</summary>
            public uint ReplyHwnd;

            /// <summary>Value set into dwData of the reply WM_COPYDATA.</summary>
            public uint ReplyCopyDataMessage;

            public uint SearchFlags;
            public uint Offset;
            public uint MaxResults;
            // followed by a null-terminated wide search string
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct EverythingIpcQuery2
        {
            public uint ReplyHwnd;
            public uint ReplyCopyDataMessage;
            public uint SearchFlags;
            public uint Offset;
            public uint MaxResults;
            public uint RequestFlags;
            public uint SortType;
            // followed by a null-terminated wide search string
        }

        // Query2 request flags.
        internal const uint RequestName = 0x00000001;
        internal const uint RequestPath = 0x00000002;
        internal const uint RequestFullPathAndName = 0x00000004;
        internal const uint RequestExtension = 0x00000008;
        internal const uint RequestSize = 0x00000010;
        internal const uint RequestDateCreated = 0x00000020;
        internal const uint RequestDateModified = 0x00000040;
        internal const uint RequestDateAccessed = 0x00000080;
        internal const uint RequestAttributes = 0x00000100;

        // Query2 sort types.
        internal const uint SortNameAscending = 1;
        internal const uint SortNameDescending = 2;
        internal const uint SortPathAscending = 3;
        internal const uint SortPathDescending = 4;
        internal const uint SortSizeAscending = 5;
        internal const uint SortSizeDescending = 6;
        internal const uint SortExtensionAscending = 7;
        internal const uint SortExtensionDescending = 8;
        internal const uint SortDateCreatedAscending = 11;
        internal const uint SortDateCreatedDescending = 12;
        internal const uint SortDateModifiedAscending = 13;
        internal const uint SortDateModifiedDescending = 14;

        /// <summary>Used for <see cref="EverythingIpcQuery2.MaxResults"/> to return every result.</summary>
        internal const uint AllResults = 0xffffffff;
    }
}
