using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VoidSniffer.Model;

namespace VoidSniffer.Services
{
    /// <summary>Snapshot of the progressive build, raised after each breadth-first level.</summary>
    public sealed class TreeProgressEventArgs : EventArgs
    {
        public TreeProgressEventArgs(int level, int nodeCount, long totalSize, int pendingFolders, string currentPath)
        {
            Level = level;
            NodeCount = nodeCount;
            TotalSize = totalSize;
            PendingFolders = pendingFolders;
            CurrentPath = currentPath;
        }

        /// <summary>Depth reached so far (0 = only the root is known).</summary>
        public int Level { get; private set; }

        /// <summary>Number of nodes discovered so far.</summary>
        public int NodeCount { get; private set; }

        /// <summary>Aggregate size of everything discovered so far.</summary>
        public long TotalSize { get; private set; }

        /// <summary>Folders still queued for expansion at the next level.</summary>
        public int PendingFolders { get; private set; }

        /// <summary>Folder that was expanded last, for status reporting.</summary>
        public string CurrentPath { get; private set; }
    }

    /// <summary>
    /// Builds the file-system hierarchy progressively, one breadth-first level at a time,
    /// so the treemap can render partial results while the remaining levels are still
    /// loading. Every level is a set of cheap <c>parent:</c> queries against the
    /// Everything index; the disk is never scanned directly.
    /// </summary>
    /// <remarks>
    /// <see cref="LoadAsync"/> is meant to be awaited from the UI thread: each query runs
    /// on a thread-pool thread, and the continuations (which mutate the tree) resume on
    /// the captured context, so the resulting <see cref="FileNode"/> tree is only ever
    /// touched by one thread at a time.
    /// </remarks>
    public sealed class ProgressiveTreeLoader
    {
        private readonly EverythingDataProvider _provider;

        public ProgressiveTreeLoader(EverythingDataProvider provider)
        {
            if (provider == null) throw new ArgumentNullException("provider");
            _provider = provider;
        }

        /// <summary>Root of the tree built by the last <see cref="LoadAsync"/> call.</summary>
        public FileNode Root { get; private set; }

        /// <summary>Number of folders whose child list was cut off by the result limit.</summary>
        public int TruncatedFolderCount { get; private set; }

        /// <summary>Raised after the root is created and after each level is loaded.</summary>
        public event EventHandler<TreeProgressEventArgs> ProgressChanged;

        public async Task LoadAsync(string rootPath, CancellationToken cancellationToken)
        {
            string rootFull = PathUtil.Normalize(rootPath);
            if (string.IsNullOrEmpty(rootFull))
            {
                throw new ArgumentException("rootPath is empty", "rootPath");
            }

            Root = new FileNode
            {
                Name = PathUtil.GetName(rootFull),
                FullPath = rootFull,
                IsFolder = true,
                Depth = 0
            };
            TruncatedFolderCount = 0;

            int nodeCount = 1;
            int level = 0;
            var currentLevel = new List<FileNode> { Root };

            RaiseProgress(level, nodeCount, 0, currentLevel.Count, rootFull);

            while (currentLevel.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var nextLevel = new List<FileNode>();
                string lastPath = currentLevel[currentLevel.Count - 1].FullPath;

                for (int i = 0; i < currentLevel.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    FileNode folder = currentLevel[i];
                    List<FileSystemItem> children = await _provider
                        .GetChildrenAsync(folder.FullPath, cancellationToken);

                    if (_provider.LastResultTruncated)
                    {
                        TruncatedFolderCount++;
                    }

                    nodeCount += AppendChildren(folder, children, nextLevel);
                    lastPath = folder.FullPath;
                }

                level++;
                currentLevel = nextLevel;
                RaiseProgress(level, nodeCount, Root.TotalSize, currentLevel.Count, lastPath);
            }
        }

        /// <summary>
        /// Attaches the children of <paramref name="folder"/> to the tree and registers
        /// child folders in <paramref name="nextLevel"/> for the next breadth-first pass.
        /// Returns the number of nodes added.
        /// </summary>
        private static int AppendChildren(FileNode folder, List<FileSystemItem> items, List<FileNode> nextLevel)
        {
            int added = 0;
            for (int i = 0; i < items.Count; i++)
            {
                FileSystemItem item = items[i];
                if (item == null) continue;

                string fullPath = PathUtil.Normalize(item.FullPath);
                if (string.IsNullOrEmpty(fullPath)) continue;
                if (item.IsFolder && PathUtil.EqualsPath(fullPath, folder.FullPath)) continue;

                long size = item.Size > 0 ? item.Size : 0;
                long ownSize = item.IsFolder ? 0 : size;
                var node = new FileNode
                {
                    Name = string.IsNullOrEmpty(item.Name) ? PathUtil.GetName(fullPath) : item.Name,
                    FullPath = fullPath,
                    IsFolder = item.IsFolder,
                    OwnSize = ownSize,
                    TotalSize = ownSize,
                    ModifiedTime = item.ModifiedTime,
                    Parent = folder,
                    Depth = folder.Depth + 1
                };

                folder.Children.Add(node);
                added++;

                // Folders contribute no own size; propagate file sizes up so that
                // ancestor totals stay meaningful while the build is still running.
                for (FileNode ancestor = folder; ancestor != null; ancestor = ancestor.Parent)
                {
                    ancestor.TotalSize += ownSize;
                }

                if (item.IsFolder)
                {
                    nextLevel.Add(node);
                }
            }

            folder.Children.Sort((a, b) => b.TotalSize.CompareTo(a.TotalSize));
            return added;
        }

        private void RaiseProgress(int level, int nodeCount, long totalSize, int pendingFolders, string currentPath)
        {
            EventHandler<TreeProgressEventArgs> handler = ProgressChanged;
            if (handler != null)
            {
                handler(this, new TreeProgressEventArgs(level, nodeCount, totalSize, pendingFolders, currentPath));
            }
        }
    }
}
