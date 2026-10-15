using System;
using System.Collections.Generic;
using VoidSniffer.Model;

namespace VoidSniffer.Services
{
    /// <summary>
    /// Decides which folders to expand automatically so that subfolders render recursively
    /// inside the treemap, without ever descending into the whole disk. Only folders that
    /// are large enough to matter and shallower than a depth limit are expanded, largest
    /// first, so the biggest areas nest first.
    /// </summary>
    public static class AutoExpansionPolicy
    {
        /// <summary>Smallest size (bytes) a folder may have to be auto-expanded.</summary>
        public const long MinAbsoluteSize = 1024L * 1024L;

        /// <summary>
        /// Folder size threshold: folders smaller than <c>rootTotal / divisor</c> (and
        /// smaller than <see cref="MinAbsoluteSize"/>) are not expanded.
        /// </summary>
        public const long SizeDivisor = 2000;

        /// <summary>Maximum nesting depth that is expanded automatically.</summary>
        public const int DefaultMaxDepth = 4;

        public static long SizeThreshold(long rootTotalSize)
        {
            long relative = SizeDivisor > 0 ? rootTotalSize / SizeDivisor : MinAbsoluteSize;
            return Math.Max(MinAbsoluteSize, relative);
        }

        public static bool IsEligible(FileNode folder, long threshold, int maxDepth)
        {
            return folder != null
                && folder.IsFolder
                && folder.TotalSize >= threshold
                && folder.Depth < maxDepth;
        }

        /// <summary>Index of the largest folder in <paramref name="pending"/>, or -1.</summary>
        public static int IndexOfLargest(List<FileNode> pending)
        {
            if (pending == null || pending.Count == 0) return -1;

            int best = 0;
            for (int i = 1; i < pending.Count; i++)
            {
                if (pending[i].TotalSize > pending[best].TotalSize) best = i;
            }
            return best;
        }
    }
}
