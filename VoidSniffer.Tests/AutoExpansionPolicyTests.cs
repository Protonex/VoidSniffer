using System.Collections.Generic;
using VoidSniffer.Model;
using VoidSniffer.Services;
using Xunit;

namespace VoidSniffer.Tests
{
    public class AutoExpansionPolicyTests
    {
        [Fact]
        public void SizeThreshold_UsesMinAbsoluteForSmallTrees()
        {
            Assert.Equal(AutoExpansionPolicy.MinAbsoluteSize, AutoExpansionPolicy.SizeThreshold(1000));
        }

        [Fact]
        public void SizeThreshold_ScalesWithRootSize()
        {
            long threshold = AutoExpansionPolicy.SizeThreshold(462L * 1024 * 1024 * 1024);
            Assert.True(threshold > AutoExpansionPolicy.MinAbsoluteSize);
            Assert.Equal(462L * 1024 * 1024 * 1024 / AutoExpansionPolicy.SizeDivisor, threshold);
        }

        [Fact]
        public void IsEligible_RequiresFolderBigEnoughAndShallowEnough()
        {
            FileNode folder = TestTree.Folder("big");
            folder.TotalSize = 5_000_000;
            folder.Depth = 1;

            Assert.True(AutoExpansionPolicy.IsEligible(folder, 1_000_000, 4));
            Assert.False(AutoExpansionPolicy.IsEligible(folder, 10_000_000, 4)); // too small

            folder.Depth = 5;
            Assert.False(AutoExpansionPolicy.IsEligible(folder, 1_000_000, 4)); // too deep
        }

        [Fact]
        public void IsEligible_RejectsFiles()
        {
            FileNode file = TestTree.File("a", 10_000_000);
            Assert.False(AutoExpansionPolicy.IsEligible(file, 1, 8));
        }

        [Fact]
        public void IndexOfLargest_FindsBiggestFolder()
        {
            FileNode a = TestTree.Folder("a");
            a.TotalSize = 10;
            FileNode b = TestTree.Folder("b");
            b.TotalSize = 100;
            FileNode c = TestTree.Folder("c");
            c.TotalSize = 50;

            Assert.Equal(1, AutoExpansionPolicy.IndexOfLargest(new List<FileNode> { a, b, c }));
            Assert.Equal(-1, AutoExpansionPolicy.IndexOfLargest(new List<FileNode>()));
        }
    }
}
