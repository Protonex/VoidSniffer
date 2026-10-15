using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using VoidSniffer.Model;

namespace VoidSniffer.Services
{
    /// <summary>
    /// Prunes a <see cref="FileNode"/> hierarchy according to a <see cref="FilterCriteria"/>,
    /// returning a new tree whose sizes are re-aggregated over the kept subset.
    /// </summary>
    /// <remarks>
    /// Rules:
    /// <list type="number">
    /// <item>A file is kept when it passes every active predicate (name, size, date).</item>
    /// <item>A folder whose name matches the name predicate is kept together with its whole
    /// subtree, unfiltered (so searching for a folder reveals its contents).</item>
    /// <item>Any other folder is kept only when it has at least one surviving descendant,
    /// and its total size becomes the sum of the surviving descendants.</item>
    /// <item>When the name predicate is empty, rule 2 never applies.</item>
    /// </list>
    /// </remarks>
    public sealed class FilterService
    {
        /// <summary>
        /// Returns the filtered tree. When no criterion is active the original instance is
        /// returned unchanged (no copy). A root that matches nothing yields an empty tree
        /// with the same name/path as the input root, never <c>null</c>.
        /// </summary>
        public FileNode Apply(FileNode root, FilterCriteria criteria)
        {
            if (root == null) return null;
            if (criteria == null || !criteria.HasActiveFilters) return root;

            FileNode filtered = Build(root, criteria, 0);
            if (filtered != null) return filtered;

            return new FileNode
            {
                Name = root.Name,
                FullPath = root.FullPath,
                IsFolder = true,
                ModifiedTime = root.ModifiedTime,
                TotalSize = 0
            };
        }

        public static int CountNodes(FileNode node)
        {
            if (node == null) return 0;
            int count = 1;
            for (int i = 0; i < node.Children.Count; i++)
            {
                count += CountNodes(node.Children[i]);
            }
            return count;
        }

        private static FileNode Build(FileNode node, FilterCriteria criteria, int depth)
        {
            if (node.IsFolder)
            {
                bool hasNamePredicate = !string.IsNullOrWhiteSpace(criteria.NameQuery);
                if (hasNamePredicate && criteria.IncludeFolders && NameMatches(node.Name, criteria))
                {
                    return Clone(node, depth);
                }

                // A folder whose children have not been loaded yet (lazy tree) is treated
                // as an opaque leaf: its contents cannot be inspected, so it is kept as-is
                // instead of being dropped for having no surviving descendants.
                if (node.Children.Count == 0)
                {
                    return criteria.IncludeFolders && NameMatches(node.Name, criteria)
                        ? Clone(node, depth)
                        : null;
                }

                var keptChildren = new List<FileNode>();
                long total = 0;
                for (int i = 0; i < node.Children.Count; i++)
                {
                    FileNode child = Build(node.Children[i], criteria, depth + 1);
                    if (child == null) continue;
                    child.Parent = null;
                    keptChildren.Add(child);
                    total += child.TotalSize;
                }

                if (keptChildren.Count == 0) return null;

                var folder = new FileNode
                {
                    Name = node.Name,
                    FullPath = node.FullPath,
                    IsFolder = true,
                    ModifiedTime = node.ModifiedTime,
                    OwnSize = 0,
                    TotalSize = total,
                    Depth = depth
                };
                for (int i = 0; i < keptChildren.Count; i++)
                {
                    FileNode child = keptChildren[i];
                    child.Parent = folder;
                    child.Depth = depth + 1;
                    folder.Children.Add(child);
                }
                return folder;
            }

            if (!criteria.IncludeFiles || !FileMatches(node, criteria)) return null;

            return new FileNode
            {
                Name = node.Name,
                FullPath = node.FullPath,
                IsFolder = false,
                ModifiedTime = node.ModifiedTime,
                OwnSize = node.OwnSize,
                TotalSize = node.OwnSize,
                Depth = depth
            };
        }

        private static FileNode Clone(FileNode node, int depth)
        {
            var copy = new FileNode
            {
                Name = node.Name,
                FullPath = node.FullPath,
                IsFolder = node.IsFolder,
                ModifiedTime = node.ModifiedTime,
                OwnSize = node.OwnSize,
                TotalSize = node.TotalSize,
                Depth = depth
            };
            for (int i = 0; i < node.Children.Count; i++)
            {
                FileNode child = Clone(node.Children[i], depth + 1);
                child.Parent = copy;
                copy.Children.Add(child);
            }
            return copy;
        }

        private static bool FileMatches(FileNode node, FilterCriteria criteria)
        {
            if (!NameMatches(node.Name, criteria)) return false;

            if (criteria.MinSize.HasValue && node.OwnSize < criteria.MinSize.Value) return false;
            if (criteria.MaxSize.HasValue && node.OwnSize > criteria.MaxSize.Value) return false;

            if (criteria.ModifiedAfter.HasValue &&
                (!node.ModifiedTime.HasValue || node.ModifiedTime.Value < criteria.ModifiedAfter.Value))
            {
                return false;
            }
            if (criteria.ModifiedBefore.HasValue &&
                (!node.ModifiedTime.HasValue || node.ModifiedTime.Value > criteria.ModifiedBefore.Value))
            {
                return false;
            }

            return true;
        }

        private static bool NameMatches(string name, FilterCriteria criteria)
        {
            if (string.IsNullOrWhiteSpace(criteria.NameQuery)) return true;
            if (name == null) return false;

            if (criteria.RegexMode)
            {
                try
                {
                    return Regex.IsMatch(name, criteria.NameQuery, RegexOptions.IgnoreCase);
                }
                catch (ArgumentException)
                {
                    // Invalid pattern: fall back to a plain substring match.
                }
            }

            return name.IndexOf(criteria.NameQuery, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
