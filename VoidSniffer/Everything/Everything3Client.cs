using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using VoidSniffer.Model;
using VoidSniffer.Services;

namespace VoidSniffer.Everything
{
    /// <summary>
    /// Client for the Everything 1.5 "SDK3" named-pipe IPC. It lists the direct children of a
    /// folder in a single query, returning the logical size, the exact on-disk size, the
    /// recursive file/folder counts and the timestamps straight from the index.
    /// </summary>
    /// <remarks>
    /// Only the subset actually used is implemented: connect, protocol version and a
    /// <c>SEARCH</c> for the direct children of a folder. When the pipe server is not
    /// present (Everything 1.4 or older), <see cref="TryConnect"/> returns false and the
    /// caller falls back to the classic WM_COPYDATA IPC.
    /// </remarks>
    public sealed class Everything3Client : IDisposable
    {
        // Command / response codes (Everything3.c).
        private const uint CmdGetIpcPipeVersion = 0;
        private const uint CmdSearch = 7;
        private const uint RespOkMoreData = 100;
        private const uint RespOk = 200;

        private const uint SearchFlag64Bit = 0x00004000;

        // Property ids (Everything3.h).
        private const uint PropName = 0;
        private const uint PropPath = 1;
        private const uint PropSize = 2;
        private const uint PropDateModified = 5;
        private const uint PropDateCreated = 6;
        private const uint PropDateAccessed = 7;
        private const uint PropTotalSizeOnDisk = 162;
        private const uint PropFullPath = 240;
        private const uint PropDescendantFolderCount = 283;
        private const uint PropDescendantFileCount = 284;

        private const int ErrorPipeBusy = 231;
        private const int PipeTimeoutMs = 5000;
        private const int ConnectTimeoutMs = 800;

        private int _timeoutMs = PipeTimeoutMs;

        private readonly object _sync = new object();
        private FileStream _pipe;
        private uint _version;
        private bool _disposed;

        /// <summary>True when a pipe connection is available.</summary>
        public bool IsConnected
        {
            get { return _pipe != null; }
        }

        /// <summary>
        /// Connects to the Everything 1.5 pipe, or returns false if it is absent. The pipe
        /// server is recreated right after a client connects, so a fresh connection is
        /// validated with a version request and retried until it responds.
        /// </summary>
        public bool TryConnect()
        {
            lock (_sync)
            {
                if (_pipe != null) return true;

                for (int attempt = 0; attempt < 3; attempt++)
                {
                    foreach (string name in PipeNames())
                    {
                        FileStream pipe = null;
                        try
                        {
                            pipe = OpenPipe(name);
                        }
                        catch (IOException)
                        {
                            continue;
                        }
                        catch (UnauthorizedAccessException)
                        {
                            continue;
                        }

                        _pipe = pipe;
                        if (PingVersion()) return true;
                        ResetConnection();
                    }

                    System.Threading.Thread.Sleep(200);
                }
                return false;
            }
        }

        private bool PingVersion()
        {
            _timeoutMs = ConnectTimeoutMs;
            try
            {
                Write(CmdGetIpcPipeVersion, new byte[0]);
                Header header = ReadHeader();
                byte[] payload = ReadPayload(header);
                if (header.Code != RespOk || payload.Length < 4) return false;
                _version = BitConverter.ToUInt32(payload, 0);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            finally
            {
                _timeoutMs = PipeTimeoutMs;
            }
        }

        /// <summary>Protocol version reported by the pipe, or 0 when unavailable.</summary>
        public uint GetProtocolVersion()
        {
            lock (_sync)
            {
                if (!TryConnect()) return 0;
                return _version;
            }
        }

        /// <summary>
        /// Returns the direct children of <paramref name="folderPath"/> with the full set of
        /// fields, or <c>null</c> when the pipe is unavailable or the query failed.
        /// </summary>
        public List<FileSystemItem> GetChildren(string folderPath, int maxResults, out long totalCount, out bool truncated)
        {
            truncated = false;
            totalCount = 0;
            lock (_sync)
            {
                if (!TryConnect()) return null;

                try
                {
                    uint[] properties =
                    {
                        PropName, PropFullPath, PropSize, PropTotalSizeOnDisk,
                        PropDescendantFileCount, PropDescendantFolderCount,
                        PropDateCreated, PropDateModified, PropDateAccessed
                    };

                    byte[] response = Search("parent:\"" + folderPath + "\"", 0, maxResults, properties);
                    return ParseResults(response, folderPath, out totalCount, out truncated);
                }
                catch (IOException)
                {
                    ResetConnection();
                    return null;
                }
            }
        }

        private byte[] Search(string searchText, ulong offset, int count, uint[] properties)
        {
            byte[] text = Encoding.UTF8.GetBytes(searchText);

            using (var payload = new MemoryStream())
            {
                WriteRaw(payload, BitConverter.GetBytes(SearchFlag64Bit)); // search flags
                WriteVlq(payload, (ulong)text.Length);                     // search text length
                payload.Write(text, 0, text.Length);                       // search text (UTF-8)
                WriteSizeT(payload, offset);                               // viewport offset
                WriteSizeT(payload, (ulong)count);                         // viewport count (limit)
                WriteVlq(payload, 0);                                      // sort count

                WriteVlq(payload, (ulong)properties.Length);               // property request count
                foreach (uint id in properties)
                {
                    WriteRaw(payload, BitConverter.GetBytes(id));
                    WriteRaw(payload, BitConverter.GetBytes(0u));          // flags
                }

                Write(CmdSearch, payload.ToArray());
            }

            using (var acc = new MemoryStream())
            {
                for (int guard = 0; guard < 100000; guard++)
                {
                    Header header = ReadHeader();
                    byte[] chunk = ReadPayload(header);

                    if (header.Code == RespOkMoreData)
                    {
                        acc.Write(chunk, 0, chunk.Length);
                        continue;
                    }
                    if (header.Code == RespOk)
                    {
                        acc.Write(chunk, 0, chunk.Length);
                        break;
                    }

                    throw new IOException("Everything 1.5 pipe search failed with response code " + header.Code);
                }
                return acc.ToArray();
            }
        }

        private List<FileSystemItem> ParseResults(byte[] data, string folderPath, out long totalCount, out bool truncated)
        {
            truncated = false;
            totalCount = 0;
            int o = 0;

            uint validFlags = ReadU32(data, ref o);
            bool sizeT64 = (validFlags & SearchFlag64Bit) != 0;

            ulong folderTotal = ReadSizeT(data, ref o, sizeT64);
            ulong fileTotal = ReadSizeT(data, ref o, sizeT64);
            if ((validFlags & 0x00000800) != 0) o += 8; // total size echo

            ReadSizeT(data, ref o, sizeT64);                    // viewport offset
            ulong viewportCount = ReadSizeT(data, ref o, sizeT64);

            ulong sortCount = ReadVlq(data, ref o);
            o += (int)(sortCount * 8);                          // {DWORD id, DWORD flags}

            ulong propertyCount = ReadVlq(data, ref o);
            var valueTypes = new byte[propertyCount];
            for (ulong i = 0; i < propertyCount; i++)
            {
                o += 8;                                         // {DWORD id, DWORD flags}
                valueTypes[i] = data[o++];                      // BYTE value_type
            }

            ulong total = folderTotal + fileTotal;
            totalCount = (long)total;
            truncated = total > viewportCount;

            var items = new List<FileSystemItem>((int)viewportCount);
            for (ulong i = 0; i < viewportCount; i++)
            {
                byte itemFlags = data[o++];
                bool isFolder = (itemFlags & 0x01) != 0;

                string name = null, fullPath = null;
                long size = -1, onDisk = -1, fileCount = -1, folderCount = -1;
                DateTime? created = null, modified = null, accessed = null;

                // Values appear in the same order as the requested properties.
                for (int p = 0; p < valueTypes.Length; p++)
                {
                    byte type = valueTypes[p];
                    switch (p)
                    {
                        case 0: name = ReadString(data, ref o, type); break;
                        case 1: fullPath = ReadString(data, ref o, type); break;
                        case 2: size = (long)ReadUInt64(data, ref o, type, sizeT64); break;
                        case 3: onDisk = (long)ReadUInt64(data, ref o, type, sizeT64); break;
                        case 4: fileCount = (long)ReadUInt64(data, ref o, type, sizeT64); break;
                        case 5: folderCount = (long)ReadUInt64(data, ref o, type, sizeT64); break;
                        case 6: created = ReadFileTime(data, ref o, type); break;
                        case 7: modified = ReadFileTime(data, ref o, type); break;
                        case 8: accessed = ReadFileTime(data, ref o, type); break;
                        default: SkipValue(data, ref o, type); break;
                    }
                }

                if (string.IsNullOrEmpty(fullPath))
                {
                    fullPath = name == null ? null : PathUtil.Combine(folderPath, name);
                }

                items.Add(new FileSystemItem
                {
                    Name = name,
                    ParentPath = folderPath,
                    FullPath = fullPath,
                    IsFolder = isFolder,
                    Size = size < 0 ? 0 : size,
                    OnDiskSize = onDisk,
                    FileCount = fileCount,
                    FolderCount = folderCount,
                    CreatedTime = created,
                    ModifiedTime = modified,
                    AccessedTime = accessed
                });
            }

            return items;
        }

        // The folder being queried; set before parsing so FullPath can be reconstructed if
        // the index omits it.
        private static string ReadString(byte[] d, ref int o, byte type)
        {
            ulong len = ReadVlq(d, ref o);
            string s = Encoding.UTF8.GetString(d, o, (int)len);
            o += (int)len;
            return s;
        }

        private static DateTime? ReadFileTime(byte[] d, ref int o, byte type)
        {
            if (type != 5) { SkipValue(d, ref o, type); return null; }
            long value = (long)BitConverter.ToUInt64(d, o);
            o += 8;
            if (value <= 0) return null;
            try { return DateTime.FromFileTime(value); }
            catch (ArgumentException) { return null; }
        }

        private static ulong ReadUInt64(byte[] d, ref int o, byte type, bool sizeT64)
        {
            switch (type)
            {
                case 5: { ulong v = BitConverter.ToUInt64(d, o); o += 8; return v; }
                case 11: { ulong v = sizeT64 ? BitConverter.ToUInt64(d, o) : BitConverter.ToUInt32(d, o); o += sizeT64 ? 8 : 4; return v; }
                case 3: case 4: case 12: case 13: case 17: { uint v = BitConverter.ToUInt32(d, o); o += 4; return v; }
                case 2: case 18: { ushort v = BitConverter.ToUInt16(d, o); o += 2; return v; }
                case 1: case 20: { byte v = d[o++]; return v; }
                default: SkipValue(d, ref o, type); return 0;
            }
        }

        private static void SkipValue(byte[] d, ref int o, byte type)
        {
            switch (type)
            {
                case 8: case 9: case 10: case 14: case 15:
                    { ulong len = ReadVlq(d, ref o); o += (int)len; break; }
                case 5: o += 8; break;
                case 11: o += 8; break;
                case 3: case 4: case 12: case 13: case 17: o += 4; break;
                case 2: case 18: o += 2; break;
                case 1: case 20: o += 1; break;
                case 6: o += 16; break;
                case 7: o += 8; break;
                case 16: { byte len = d[o++]; o += len; break; }
                case 19: { ushort len = BitConverter.ToUInt16(d, o); o += 2; o += len; break; }
                default: break;
            }
        }

        private void Write(uint code, byte[] payload)
        {
            var header = new byte[8];
            BitConverter.GetBytes(code).CopyTo(header, 0);
            BitConverter.GetBytes((uint)payload.Length).CopyTo(header, 4);

            Await(_pipe.WriteAsync(header, 0, 8), "write");
            if (payload.Length > 0) Await(_pipe.WriteAsync(payload, 0, payload.Length), "write");
            Await(_pipe.FlushAsync(), "flush");        }

        private struct Header
        {
            public uint Code;
            public uint Size;
        }

        private Header ReadHeader()
        {
            byte[] b = ReadExact(8);
            return new Header { Code = BitConverter.ToUInt32(b, 0), Size = BitConverter.ToUInt32(b, 4) };
        }

        private byte[] ReadPayload(Header header)
        {
            return header.Size == 0 ? new byte[0] : ReadExact((int)header.Size);
        }

        private byte[] ReadExact(int count)
        {
            var buffer = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int read = Await(_pipe.ReadAsync(buffer, offset, count - offset), "read");
                if (read <= 0) throw new EndOfStreamException("Everything 1.5 pipe closed");
                offset += read;
            }
            return buffer;
        }

        // The pipe is opened for overlapped I/O (as the SDK does); runs the async operation
        // synchronously with a timeout so a stalled server can never hang the caller.
        private T Await<T>(System.Threading.Tasks.Task<T> task, string what)
        {
            if (!task.Wait(_timeoutMs)) throw new IOException("Everything 1.5 pipe " + what + " timed out");
            return task.Result;
        }

        private void Await(System.Threading.Tasks.Task task, string what)
        {
            if (!task.Wait(_timeoutMs)) throw new IOException("Everything 1.5 pipe " + what + " timed out");
        }

        private void ResetConnection()
        {
            if (_pipe != null)
            {
                try { _pipe.Dispose(); }
                catch (Exception) { }
                _pipe = null;
            }
        }

        private static IEnumerable<string> PipeNames()
        {
            int sessionId = Process.GetCurrentProcess().SessionId;
            yield return @"\\.\PIPE\Everything IPC Session " + sessionId;
            yield return @"\\.\PIPE\Everything IPC";
        }

        private static FileStream OpenPipe(string name)
        {
            const uint GenericReadWrite = 0xC0000000;
            const uint OpenExisting = 3;
            const uint FileFlagOverlapped = 0x40000000;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                SafeFileHandle handle = CreateFileW(name, GenericReadWrite, 0, IntPtr.Zero, OpenExisting, FileFlagOverlapped, IntPtr.Zero);
                if (!handle.IsInvalid)
                {
                    // The SDK opens the pipe for overlapped I/O; doing the same avoids reads
                    // that can otherwise block indefinitely on this pipe.
                    return new FileStream(handle, FileAccess.ReadWrite, 4096, true);
                }

                int error = Marshal.GetLastWin32Error();
                handle.Dispose();
                if (error == ErrorPipeBusy)
                {
                    WaitNamedPipeW(name, 1000);
                    continue;
                }
                throw new IOException("Everything 1.5 pipe open failed, error " + error);
            }
            throw new IOException("Everything 1.5 pipe busy");
        }

        private static void WriteRaw(Stream s, byte[] bytes)
        {
            s.Write(bytes, 0, bytes.Length);
        }

        private static void WriteSizeT(Stream s, ulong value)
        {
            WriteRaw(s, BitConverter.GetBytes(value)); // 64-bit client
        }

        private static void WriteVlq(Stream s, ulong value)
        {
            if (value < 0xFF) { s.WriteByte((byte)value); return; }

            ulong rest = value - 0xFF;
            if (rest < 0xFFFF)
            {
                s.WriteByte(0xFF);
                WriteRaw(s, BitConverter.GetBytes((ushort)rest));
                return;
            }

            rest -= 0xFFFF;
            if (rest < 0xFFFFFFFF)
            {
                s.WriteByte(0xFF);
                s.WriteByte(0xFF);
                WriteRaw(s, BitConverter.GetBytes((uint)rest));
                return;
            }

            rest -= 0xFFFFFFFF;
            s.WriteByte(0xFF);
            s.WriteByte(0xFF);
            s.WriteByte(0xFF);
            s.WriteByte(0xFF);
            WriteRaw(s, BitConverter.GetBytes(rest));
        }

        private static ulong ReadVlq(byte[] d, ref int o)
        {
            byte b = d[o++];
            if (b != 0xFF) return b;

            byte b1 = d[o++];
            if (b1 != 0xFF) { ushort r = BitConverter.ToUInt16(d, o); o += 2; return (ulong)r + 0xFF; }

            byte b2 = d[o++];
            if (b2 != 0xFF)
            {
                uint r = (uint)(b2 | (d[o] << 8) | (d[o + 1] << 16) | (d[o + 2] << 24));
                o += 3;
                return (ulong)r + 0xFF + 0xFFFF;
            }

            o++; // fourth 0xFF
            ulong big = BitConverter.ToUInt64(d, o);
            o += 8;
            return big + 0xFF + 0xFFFF + 0xFFFFFFFF;
        }

        private static uint ReadU32(byte[] d, ref int o)
        {
            uint v = BitConverter.ToUInt32(d, o);
            o += 4;
            return v;
        }

        private static ulong ReadSizeT(byte[] d, ref int o, bool sizeT64)
        {
            ulong v = sizeT64 ? BitConverter.ToUInt64(d, o) : BitConverter.ToUInt32(d, o);
            o += sizeT64 ? 8 : 4;
            return v;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            lock (_sync)
            {
                ResetConnection();
            }
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool WaitNamedPipeW(string name, uint timeout);
    }
}
