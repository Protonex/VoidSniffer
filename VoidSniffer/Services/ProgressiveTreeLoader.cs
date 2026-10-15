using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VoidSniffer.Model;

namespace VoidSniffer.Services
{
    /// <summary>
    /// Builds the file-system hierarchy lazily, one folder at a time, directly from the
    /// Everything index. The index reports the exact recursive size of every folder, so a
    /// single query per folder yields a fully sized level of the tree; deeper levels are
    /// loaded on demand when the user drills into them.
    /// </summary>
    /// <remarks>
    /// This is what makes the view appear instantly instead of blocking on one giant
    /// subtree request: scanning a path costs one cheap <c>parent:</c> query, and each
    /// drill-down costs one more. The disk is never scanned; only the Everything index is
    /// read. <see cref="ExpandAsync"/> is idempotent per folder.
    /// <para>
    /// <see cref="LoadRootAsync"/> and <see cref="ExpandAsync"/> are meant to be awaited
    /// from the UI thread: each query runs on a thread-pool thread and the continuation
    /// (which mutates the tree) resumes on the captured context, so the tree is only ever
    /// touched by one thread at a time.
    /// </para>
    /// </remarks>
    public sealed class ProgressiveTreeLoader
    {
        private readonly IEverythingFileSource _provider;
        private readonly IDiskUsageInfo _diskUsage;
        private readonly HashSet<string> _expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public ProgressiveTreeLoader(IEverythingFileSource provider)
            : this(provider, new DiskUsageInfo())
        {
        }

        public ProgressiveTreeLoader(IEverythingFileSource provider, IDiskUsageInfo diskUsage)
        {
            if (provider == null) throw new ArgumentNullException("provider");
            if (diskUsage == null) throw new ArgumentNullException("diskUsage");
            _provider = provider;
            _diskUsage = diskUsage;
        }

        /// <summary>Root of the tree built by the last <see cref="LoadRootAsync"/> call.</summary>
        public FileNode Root { get; private set; }

        /// <summary>Number of nodes discovered so far (including the root).</summary>
        public int NodeCount { get; private set; }

        /// <summary>Number of folders whose children have been loaded.</summary>
        public int ExpandedFolderCount { get; private set; }

        /// <summary>Number of folder queries whose child list was cut off by the result limit.</summary>
        public int TruncatedFolderCount { get; private set; }

        /// <summary>Raised after the root is created and after every folder is expanded.</summary>
        public event EventHandler ProgressChanged;

        /// <summary>Creates the root node and loads its direct children.</summary>
        public async Task LoadRootAsync(string rootPath, CancellationToken cancellationToken)
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

            _expanded.Clear();
            NodeCount = 1;
            ExpandedFolderCount = 0;
            TruncatedFolderCount = 0;

            await ExpandAsync(Root, cancellationToken).ConfigureAwait(true);

            long total = 0;
            for (int i = 0; i < Root.Children.Count; i++)
            {
                total += Root.Children[i].TotalSize;
            }
            Root.TotalSize = total;
        }

        /// <summary>True when the folder's children have already been loaded.</summary>
        public bool IsExpanded(FileNode folder)
        {
            return folder != null && _expanded.Contains(PathUtil.Normalize(folder.FullPath));
        }

        /// <summary>
        /// Loads the direct children of <paramref name="folder"/> once. Returns <c>true</c>
        /// when children were loaded, <c>false</c> when the folder was already expanded.
        /// </summary>
        public async Task<bool> ExpandAsync(FileNode folder, CancellationToken cancellationToken)
        {
            if (folder == null || !folder.IsFolder) return false;

            string path = PathUtil.Normalize(folder.FullPath);
            if (!_expanded.Add(path)) return false;

            List<FileSystemItem> children;
            try
            {
                children = await _provider.GetChildrenAsync(path, cancellationToken).ConfigureAwait(true);
            }
            catch
            {
                _expanded.Remove(path);
                throw;
            }

            if (_provider.LastResultTruncated)
            {
                TruncatedFolderCount++;
            }

            NodeCount += Attach(folder, children);
            ExpandedFolderCount++;
            RaiseProgress();
            return true;
        }

        /// <summary>
        /// Attaches the children of <paramref name="folder"/> using the sizes reported by
        /// the index. A folder's size is the exact recursive total, so no re-aggregation
        /// is required and ancestors stay correct without walking back up the tree.
        /// </summary>
        private int Attach(FileNode folder, List<FileSystemItem> items)
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
                int cluster = _diskUsage.GetClusterSize(fullPath);
                var node = new FileNode
                {
                    Name = string.IsNullOrEmpty(item.Name) ? PathUtil.GetName(fullPath) : item.Name,
                    FullPath = fullPath,
                    IsFolder = item.IsFolder,
                    OwnSize = item.IsFolder ? 0 : size,
                    TotalSize = size,
                    ModifiedTime = item.ModifiedTime,
                    Parent = folder,
                    Depth = folder.Depth + 1
                };

                // Files have an exact allocated size; folders only have an estimate, which
                // needs their recursive file count and is therefore filled in later.
                if (!item.IsFolder)
                {
                    node.OnDiskSize = DiskUsage.AllocatedFileSize(size, cluster);
                }

                folder.Children.Add(node);
                added++;
            }

            folder.Children.Sort((a, b) => b.TotalSize.CompareTo(a.TotalSize));
            return added;
        }

        private void RaiseProgress()
        {
            EventHandler handler = ProgressChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }
    }
}
