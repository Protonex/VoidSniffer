using System;
using System.Collections.Generic;
using VoidSniffer.Model;
using VoidSniffer.Services;
using Xunit;

namespace VoidSniffer.Tests
{
    public class FileSystemTreeBuilderTests
    {
        private static FileSystemItem Folder(string parent, string name, long size)
        {
            return new FileSystemItem
            {
                Name = name,
                ParentPath = parent,
                FullPath = PathUtil.Combine(parent, name),
                IsFolder = true,
                Size = size
            };
        }

        private static FileSystemItem File(string parent, string name, long size)
        {
            return new FileSystemItem
            {
                Name = name,
                ParentPath = parent,
                FullPath = PathUtil.Combine(parent, name),
                IsFolder = false,
                Size = size
            };
        }

        [Fact]
        public void Build_NestsItemsAndAggregatesSizes()
        {
            var items = new List<FileSystemItem>
            {
                File("C:\\Root", "a.txt", 100),
                Folder("C:\\Root", "Sub", 0),
                File("C:\\Root\\Sub", "b.txt", 300),
                File("C:\\Root\\Sub", "c.txt", 200)
            };

            FileNode root = new FileSystemTreeBuilder().Build(items, "C:\\Root");

            Assert.Equal("C:\\Root", root.FullPath);
            Assert.Equal(600, root.TotalSize);
            Assert.Equal(2, root.Children.Count);

            FileNode sub = Find(root, "Sub");
            Assert.NotNull(sub);
            Assert.True(sub.IsFolder);
            Assert.Equal(500, sub.TotalSize);
            Assert.Equal(2, sub.Children.Count);
            // Children are sorted by descending size.
            for (int i = 1; i < sub.Children.Count; i++)
            {
                Assert.True(sub.Children[i - 1].TotalSize >= sub.Children[i].TotalSize);
            }
            Assert.Equal("b.txt", sub.Children[0].Name);
        }

        [Fact]
        public void Build_SynthesizesMissingIntermediateFolders()
        {
            var items = new List<FileSystemItem>
            {
                File("C:\\Root\\A\\B", "deep.txt", 42)
            };

            FileNode root = new FileSystemTreeBuilder().Build(items, "C:\\Root");

            FileNode b = Find(root, "B");
            Assert.NotNull(b);
            Assert.Equal(42, root.TotalSize);
            Assert.NotNull(Find(root, "A"));
        }

        [Fact]
        public void Build_IgnoresItemsOutsideRoot()
        {
            var items = new List<FileSystemItem>
            {
                File("D:\\Elsewhere", "x.txt", 999),
                File("C:\\Root", "inside.txt", 1)
            };

            FileNode root = new FileSystemTreeBuilder().Build(items, "C:\\Root");

            Assert.Equal(1, root.TotalSize);
            Assert.Null(Find(root, "x.txt"));
        }

        private static FileNode Find(FileNode node, string name)
        {
            if (node.Name == name) return node;
            for (int i = 0; i < node.Children.Count; i++)
            {
                FileNode found = Find(node.Children[i], name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
