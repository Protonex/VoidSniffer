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
        public void Layout_SharesColorIndexBetweenSiblingFolderAndItsChildren()
        {
            FileNode root = TestTree.Folder("root",
                TestTree.Folder("sub",
                    TestTree.File("a", 60),
                    TestTree.File("b", 40)),
                TestTree.File("c", 50));

            List<TreemapBlock> blocks = TreemapLayout.Layout(
                root, new Rectangle(0, 0, 400, 300), new TreemapOptions());

            TreemapBlock sub = blocks.First(b => b.Node.Name == "sub");
            TreemapBlock a = blocks.First(b => b.Node.Name == "a");
            Assert.Equal(sub.ColorIndex, a.ColorIndex);
        }

        [Fact]
        public void Squarify_NeverProducesNonFiniteOrOutOfBoundsRectangles()
        {
            // Many children with wildly varying sizes inside a very thin container used to
            // produce an infinite-width rectangle; the layout must stay well-defined.
            var areas = new List<double> { 50, 40, 30, 20, 10, 5, 1, 0.001, 0.0001, 100 };
            var output = new List<RectangleF>();
            var container = new RectangleF(0, 0, 1200, 6);

            TreemapLayout.Squarify(areas, container, output);

            foreach (RectangleF r in output)
            {
                Assert.False(float.IsNaN(r.X) || float.IsInfinity(r.X));
                Assert.False(float.IsNaN(r.Y) || float.IsInfinity(r.Y));
                Assert.False(float.IsNaN(r.Width) || float.IsInfinity(r.Width));
                Assert.False(float.IsNaN(r.Height) || float.IsInfinity(r.Height));
                Assert.True(r.Width > 0f && r.Height > 0f);
                Assert.InRange(r.Left, -0.5f, container.Width + 0.5f);
                Assert.InRange(r.Right, -0.5f, container.Width + 0.5f);
            }
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
            Assert.Equal(TreemapColorizer.GetFill(3, 0), TreemapColorizer.GetFill(3, 0));
        }

        [Fact]
        public void GetFill_WrapsPaletteIndex()
        {
            Assert.Equal(
                TreemapColorizer.GetFill(0, 0),
                TreemapColorizer.GetFill(TreemapColorizer.PaletteSize, 0));
        }

        [Fact]
        public void GetFileFill_TintsByExtension()
        {
            Color exe = TreemapColorizer.GetFileFill(TestTree.File("a.exe", 10));
            Color txt = TreemapColorizer.GetFileFill(TestTree.File("a.txt", 10));
            Assert.NotEqual(exe, txt);
        }

        [Fact]
        public void GetTextColor_ContrastsWithFill()
        {
            Color light = Color.FromArgb(240, 240, 240);
            Color dark = Color.FromArgb(20, 20, 20);

            // Light backgrounds get dark text and vice versa.
            Assert.True(TreemapColorizer.GetTextColor(light).R < 128);
            Assert.True(TreemapColorizer.GetTextColor(dark).R > 128);
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
