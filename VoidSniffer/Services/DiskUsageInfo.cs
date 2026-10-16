using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace VoidSniffer.Services
{
    /// <summary>
    /// Best-effort resolution of the volume cluster size for a path, used to derive the
    /// allocated (on-disk) size of files: <c>allocated = ceil(size / cluster) * cluster</c>.
    /// </summary>
    public interface IDiskUsageInfo
    {
        int GetClusterSize(string path);
    }

    /// <summary>
    /// Cluster size per volume root, resolved once via <see cref="DriveInfo"/> and cached.
    /// Falls back to a conservative 4096 bytes when the volume cannot be queried (e.g. a
    /// path with no drive letter), or when an injected default is used for tests.
    /// </summary>
    public sealed class DiskUsageInfo : IDiskUsageInfo
    {
        public const int DefaultClusterSize = 4096;

        private readonly int _fallback;
        private readonly Dictionary<string, int> _cache =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public DiskUsageInfo()
            : this(DefaultClusterSize)
        {
        }

        public DiskUsageInfo(int fallbackClusterSize)
        {
            if (fallbackClusterSize <= 0) throw new ArgumentOutOfRangeException("fallbackClusterSize");
            _fallback = fallbackClusterSize;
        }

        public int GetClusterSize(string path)
        {
            string root = GetVolumeRoot(path);
            if (root == null) return _fallback;

            int cached;
            if (_cache.TryGetValue(root, out cached)) return cached;

            int size = _fallback;
            try
            {
                int sectorsPerCluster;
                int bytesPerSector;
                uint serial;
                uint maxComponent;
                uint flags;
                if (GetVolumeInformation(root, null, 0, out serial, out maxComponent, out flags, null, 0) &&
                    GetDiskFreeSpace(root, out sectorsPerCluster, out bytesPerSector, out _, out _))
                {
                    if (GetDriveType(root) != DriveType.Remote &&
                        sectorsPerCluster > 0 && bytesPerSector > 0)
                    {
                        size = sectorsPerCluster * bytesPerSector;
                    }
                }
            }
            catch (ArgumentException)
            {
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            _cache[root] = size;
            return size;
        }

        /// <summary>Free space (bytes) available on the volume containing <paramref name="path"/>.</summary>
        public static bool TryGetFreeSpace(string path, out long freeBytes)
        {
            freeBytes = 0;
            string root = GetVolumeRoot(path);
            if (root == null) return false;

            try
            {
                var drive = new DriveInfo(root);
                if (drive.IsReady)
                {
                    freeBytes = drive.AvailableFreeSpace;
                    return true;
                }
            }
            catch (ArgumentException)
            {
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return false;
        }

        /// <summary>Volume root ("C:\") of a path, or null when the path has no drive.</summary>
        public static string GetVolumeRoot(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            string normalized = PathUtil.Normalize(path);
            if (normalized.Length >= 2 && normalized[1] == ':' &&
                ((normalized[0] >= 'A' && normalized[0] <= 'Z') ||
                 (normalized[0] >= 'a' && normalized[0] <= 'z')))
            {
                return char.ToUpperInvariant(normalized[0]) + ":\\";
            }

            if (normalized.StartsWith("\\\\", StringComparison.Ordinal))
            {
                // UNC path: \\server\share\...  ->  \\server\share\
                int serverEnd = normalized.IndexOf('\\', 2);
                if (serverEnd < 0) return normalized + "\\";
                int shareEnd = normalized.IndexOf('\\', serverEnd + 1);
                return shareEnd < 0 ? normalized + "\\" : normalized.Substring(0, shareEnd) + "\\";
            }

            return null;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetVolumeInformation(
            string rootPathName,
            string volumeNameBuffer,
            int volumeNameSize,
            out uint volumeSerialNumber,
            out uint maximumComponentLength,
            out uint fileSystemFlags,
            string fileSystemNameBuffer,
            int fileSystemNameSize);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetDiskFreeSpace(
            string rootPathName,
            out int sectorsPerCluster,
            out int bytesPerSector,
            out int numberOfFreeClusters,
            out int totalNumberOfClusters);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern DriveType GetDriveType(string rootPathName);

        private enum DriveType : uint
        {
            Unknown = 0,
            NoRootDirectory = 1,
            Removable = 2,
            Fixed = 3,
            Remote = 4,
            CdRom = 5,
            RamDisk = 6
        }
    }

    /// <summary>
    /// Pure helpers for the on-disk (allocated) size. Kept free of WinForms so they can be
    /// unit tested; the treemap uses <see cref="Treemap.NodeLabelFormatter"/> for display.
    /// </summary>
    public static class DiskUsage
    {
        /// <summary>
        /// Allocated size of a file: the smallest whole number of clusters that fits it.
        /// A zero-length file occupies no clusters.
        /// </summary>
        public static long AllocatedFileSize(long size, int clusterSize)
        {
            if (size <= 0) return 0;
            if (clusterSize <= 1) return size;

            long clusters = (size + clusterSize - 1) / clusterSize;
            return clusters * clusterSize;
        }

        /// <summary>
        /// Estimated allocated size of a folder's subtree: every file occupies whole
        /// clusters, while directories themselves are assumed to consume none. This is an
        /// estimate because per-file allocation requires filesystem allocation data that
        /// the Everything index does not expose; it is exact when the average file size is
        /// an exact multiple of the cluster size.
        /// </summary>
        public static long EstimatedFolderAllocated(long logicalSize, long fileCount, int clusterSize)
        {
            if (logicalSize <= 0 || fileCount <= 0) return 0;
            if (clusterSize <= 1) return logicalSize;

            long slackPerFile = (clusterSize - 1) / 2;
            return logicalSize + (fileCount * slackPerFile);
        }
    }
}
