using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VoidSniffer.Model;

namespace VoidSniffer.Tests
{
    /// <summary>Builds small <see cref="FileNode"/> trees for tests without touching Everything.</summary>
    internal static class TestTree
    {
        public static FileNode Folder(string name, params FileNode[] children)
        {
            return Build(name, true, 0, children);
        }

        public static FileNode File(string name, long size)
        {
            return Build(name, false, size, new FileNode[0]);
        }

        private static FileNode Build(string name, bool isFolder, long size, FileNode[] children)
        {
            var node = new FileNode
            {
                Name = name,
                FullPath = name,
                IsFolder = isFolder,
                OwnSize = isFolder ? 0 : size,
                TotalSize = size
            };

            foreach (FileNode child in children)
            {
                child.Parent = node;
                child.Depth = 1;
                node.Children.Add(child);
                node.TotalSize += child.TotalSize;
            }

            node.Children.Sort((a, b) => b.TotalSize.CompareTo(a.TotalSize));
            return node;
        }
    }

    /// <summary>In-memory <see cref="Services.IEverythingFileSource"/> for loader tests.</summary>
    internal sealed class FakeFileSource : Services.IEverythingFileSource
    {
        private readonly Dictionary<string, List<FileSystemItem>> _children =
            new Dictionary<string, List<FileSystemItem>>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, Tuple<long, long>> _counts =
            new Dictionary<string, Tuple<long, long>>(StringComparer.OrdinalIgnoreCase);

        public int ChildQueryCount { get; private set; }

        public bool LastResultTruncated { get; set; }

        public void AddFolder(string path, params FileSystemItem[] children)
        {
            _children[Services.PathUtil.Normalize(path)] = new List<FileSystemItem>(children);
        }

        public void AddCounts(string path, long files, long folders)
        {
            _counts[Services.PathUtil.Normalize(path)] = Tuple.Create(files, folders);
        }

        public Task<List<FileSystemItem>> GetChildrenAsync(string path, System.Threading.CancellationToken cancellationToken)
        {
            ChildQueryCount++;

            List<FileSystemItem> list;
            if (!_children.TryGetValue(Services.PathUtil.Normalize(path), out list))
            {
                list = new List<FileSystemItem>();
            }
            return Task.FromResult(new List<FileSystemItem>(list));
        }

        public Task<Tuple<long, long>> GetCountsAsync(string path, System.Threading.CancellationToken cancellationToken)
        {
            Tuple<long, long> value;
            if (!_counts.TryGetValue(Services.PathUtil.Normalize(path), out value))
            {
                value = Tuple.Create(0L, 0L);
            }
            return Task.FromResult(value);
        }

        public static FileSystemItem Dir(string parent, string name)
        {
            return new FileSystemItem
            {
                Name = name,
                ParentPath = parent,
                FullPath = parent + "\\" + name,
                IsFolder = true,
                Size = 0
            };
        }

        public static FileSystemItem FileItem(string parent, string name, long size)
        {
            return new FileSystemItem
            {
                Name = name,
                ParentPath = parent,
                FullPath = parent + "\\" + name,
                IsFolder = false,
                Size = size
            };
        }

        public static FileNode Find(FileNode root, string path)
        {
            if (root == null) return null;
            if (string.Equals(root.FullPath, path, StringComparison.OrdinalIgnoreCase)) return root;
            return root.Children.Select(c => Find(c, path)).FirstOrDefault(f => f != null);
        }
    }

    internal sealed class FakeDiskUsage : Services.IDiskUsageInfo
    {
        private readonly int _cluster;

        public FakeDiskUsage(int cluster)
        {
            _cluster = cluster;
        }

        public int GetClusterSize(string path)
        {
            return _cluster;
        }
    }
}
