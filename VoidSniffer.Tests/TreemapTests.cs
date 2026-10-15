using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using VoidSniffer.Model;
using VoidSniffer.Treemap;
using Xunit;

namespace VoidSniffer.Tests
{
    public class TreemapLayoutTests
    {
        [Fact]
        public void Squarify_FillsContainerWithoutOverlap()
        {
            var areas = new List<double> { 6, 6, 4, 3, 2, 2, 1 };
            var output = new List<RectangleF>();
            var container = new RectangleF(0, 0, 100, 60);

            TreemapLayout.Squarify(areas, container, output);

            Assert.Equal(areas.Count, output.Count);
            double area = output.Sum(r => (double)r.Width * r.Height);
            // The total area of the assigned rectangles equals the container area.
            Assert.InRange(area, container.Width * container.Height * 0.98, container.Width * container.Height * 1.02);

            // No pair of rectangles overlaps.
            for (int i = 0; i < output.Count; i++)
            {
                for (int j = i + 1; j < output.Count; j++)
                {
                    RectangleF a = output[i];
                    RectangleF b = output[j];
                    bool overlaps = a.Left < b.Right - 0.5f && b.Left < a.Right - 0.5f &&
                                    a.Top < b.Bottom - 0.5f && b.Top < a.Bottom - 0.5f;
                    Assert.False(overlaps, "rectangles overlap: " + a + " / " + b);
                }
            }
        }

        [Fact]
        public void Layout_EmitsContainerBeforeItsChildren()
        {
            FileNode root = TestTree.Folder("root",
                TestTree.Folder("sub",
                    TestTree.File("a", 60),
                    TestTree.File("b", 40)),
                TestTree.File("c", 50));

            List<TreemapBlock> blocks = TreemapLayout.Layout(
                root, new Rectangle(0, 0, 400, 300), new TreemapOptions());

            int subIndex = blocks.FindIndex(b => b.Node.Name == "sub");
            int aIndex = blocks.FindIndex(b => b.Node.Name == "a");
            Assert.True(subIndex >= 0);
            Assert.True(aIndex > subIndex, "child must be painted after its container");
        }

        [Fact]
        public void Layout_IgnoresZeroSizedChildren()
        {
            FileNode root = TestTree.Folder("root",
                TestTree.File("empty", 0),
                TestTree.File("real", 100));

            List<TreemapBlock> blocks = TreemapLayout.Layout(
                root, new Rectangle(0, 0, 200, 200), new TreemapOptions());

            Assert.DoesNotContain(blocks, b => b.Node.Name == "empty");
            Assert.Contains(blocks, b => b.Node.Name == "real");
        }

        [Fact]
        public void Layout_EmptyRootProducesNoBlocks()
        {
            FileNode root = TestTree.Folder("root");
            List<TreemapBlock> blocks = TreemapLayout.Layout(
                root, new Rectangle(0, 0, 200, 200), new TreemapOptions());
            Assert.Empty(blocks);
        }
    }

    public class TreemapColorizerTests
    {
        [Fact]
        public void GetFill_IsDeterministic()
        {
            FileNode node = TestTree.File("a.txt", 10);
            Assert.Equal(TreemapColorizer.GetFill(node, 0), TreemapColorizer.GetFill(node, 0));
        }

        [Fact]
        public void GetTextColor_ContrastsWithFill()
        {
            Color light = Color.FromArgb(240, 240, 240);
            Color dark = Color.FromArgb(20, 20, 20);

            Assert.Equal(Color.FromArgb(20, 20, 20), TreemapColorizer.GetTextColor(light));
            Assert.Equal(Color.FromArgb(240, 240, 240), TreemapColorizer.GetTextColor(dark));
        }
    }

    public class TreemapLayoutCacheTests
    {
        [Fact]
        public void Ensure_IsCachedUntilInvalidated()
        {
            FileNode root = TestTree.Folder("root",
                TestTree.File("a", 60),
                TestTree.File("b", 40));

            var cache = new TreemapLayoutCache(new TreemapOptions());
            cache.Ensure(root, new Size(100, 100));
            int first = cache.RebuildCount;

            cache.Ensure(root, new Size(100, 100));
            Assert.Equal(first, cache.RebuildCount);

            cache.Invalidate();
            cache.Ensure(root, new Size(100, 100));
            Assert.Equal(first + 1, cache.RebuildCount);
        }

        [Fact]
        public void HitTest_ReturnsDeepestBlockUnderPoint()
        {
            FileNode leaf = TestTree.File("a", 100);
            FileNode root = TestTree.Folder("root", leaf);

            var cache = new TreemapLayoutCache(new TreemapOptions());
            cache.Ensure(root, new Size(100, 100));

            TreemapBlock hit = cache.HitTest(new Point(50, 50));
            Assert.NotNull(hit);
            Assert.Same(leaf, hit.Node);
        }
    }
}
