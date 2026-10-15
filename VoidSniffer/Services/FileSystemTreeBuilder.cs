using System;
using System.Collections.Generic;
using VoidSniffer.Model;

namespace VoidSniffer.Services
{
    /// <summary>
    /// Converts the flat list returned by Everything into a hierarchical tree,
    /// aggregating sizes bottom-up.
    /// </summary>
    public sealed class FileSystemTreeBuilder
    {
        /// <summary>
        /// Builds a tree rooted at <paramref name="rootPath"/> from a flat item list.
        /// Items outside the root and the root item itself are ignored. Missing
        /// intermediate folders are synthesized so that no item is dropped.
        /// </summary>
        public FileNode Build(IEnumerable<FileSystemItem> items, string rootPath)
        {
            if (items == null) throw new ArgumentNullException("items");
            string rootFull = PathUtil.Normalize(rootPath);
            if (string.IsNullOrEmpty(rootFull)) throw new ArgumentException("rootPath is empty", "rootPath");

            var root = new FileNode { Name = PathUtil.GetName(rootFull), FullPath = rootFull, IsFolder = true };
            var folders = new Dictionary<string, FileNode>(StringComparer.OrdinalIgnoreCase);
            folders[rootFull] = root;

            foreach (FileSystemItem item in items)
            {
                if (item == null) continue;

                string parentPath = PathUtil.Normalize(item.ParentPath);
                if (string.IsNullOrEmpty(parentPath) || !PathUtil.IsUnderRoot(parentPath, rootFull))
                {
                    continue;
                }

                FileNode parent = EnsureFolder(folders, root, rootFull, parentPath);
                if (parent == null) continue;

                string fullPath = PathUtil.Normalize(item.FullPath);
                if (string.IsNullOrEmpty(fullPath)) continue;

                if (item.IsFolder && PathUtil.EqualsPath(fullPath, rootFull))
                {
                    continue;
                }

                if (item.IsFolder && folders.ContainsKey(fullPath))
                {
                    continue;
                }

                long size = item.Size > 0 ? item.Size : 0;
                var node = new FileNode
                {
                    Name = string.IsNullOrEmpty(item.Name) ? PathUtil.GetName(fullPath) : item.Name,
                    FullPath = fullPath,
                    IsFolder = item.IsFolder,
                    OwnSize = item.IsFolder ? 0 : size,
                    ModifiedTime = item.ModifiedTime,
                    Parent = parent
                };
                parent.Children.Add(node);

                if (item.IsFolder)
                {
                    folders[fullPath] = node;
                }
            }

            ComputeTotals(root, 0);
            SortBySizeDescending(root);
            return root;
        }

        private static FileNode EnsureFolder(
            Dictionary<string, FileNode> folders, FileNode root, string rootFull, string path)
        {
            path = PathUtil.Normalize(path);
            if (PathUtil.EqualsPath(path, rootFull)) return root;

            FileNode existing;
            if (folders.TryGetValue(path, out existing)) return existing;

            if (!PathUtil.IsUnderRoot(path, rootFull)) return null;

            string up = PathUtil.GetParentPath(path);
            if (string.IsNullOrEmpty(up) || PathUtil.EqualsPath(up, path)) return null;

            if (!PathUtil.IsUnderRoot(up, rootFull))
            {
                // The ancestor of the root itself (e.g. the UNC server of a share) maps to the root.
                return PathUtil.IsUnderRoot(rootFull, up) ? root : null;
            }

            FileNode parent = EnsureFolder(folders, root, rootFull, up);
            if (parent == null) return null;

            var node = new FileNode
            {
                Name = PathUtil.GetName(path),
                FullPath = path,
                IsFolder = true,
                OwnSize = 0,
                Parent = parent
            };
            parent.Children.Add(node);
            folders[path] = node;
            return node;
        }

        private static void ComputeTotals(FileNode node, int depth)
        {
            node.Depth = depth;
            long total = node.OwnSize;
            List<FileNode> children = node.Children;
            for (int i = 0; i < children.Count; i++)
            {
                FileNode child = children[i];
                ComputeTotals(child, depth + 1);
                total += child.TotalSize;
            }
            node.TotalSize = total;
        }

        private static void SortBySizeDescending(FileNode node)
        {
            node.Children.Sort((a, b) => b.TotalSize.CompareTo(a.TotalSize));
            List<FileNode> children = node.Children;
            for (int i = 0; i < children.Count; i++)
            {
                SortBySizeDescending(children[i]);
            }
        }
    }
}
