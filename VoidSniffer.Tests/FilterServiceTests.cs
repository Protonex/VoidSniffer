using VoidSniffer.Model;
using VoidSniffer.Services;
using Xunit;

namespace VoidSniffer.Tests
{
    public class FilterServiceTests
    {
        private readonly FilterService _service = new FilterService();

        private static FileNode Tree()
        {
            return TestTree.Folder("root",
                TestTree.File("big.bin", 5000),
                TestTree.File("small.txt", 10),
                TestTree.Folder("docs",
                    TestTree.File("report.pdf", 800),
                    TestTree.File("notes.txt", 20)));
        }

        [Fact]
        public void Apply_NoFilters_ReturnsSameInstance()
        {
            FileNode root = Tree();
            FileNode result = _service.Apply(root, new FilterCriteria());
            Assert.Same(root, result);
        }

        [Fact]
        public void Apply_MinSize_KeepsOnlyLargeFilesAndTheirAncestors()
        {
            var criteria = new FilterCriteria { MinSize = 1000 };
            FileNode result = _service.Apply(Tree(), criteria);

            Assert.Equal(5000, result.TotalSize);
            Assert.Contains(result.Children, c => c.Name == "big.bin");
            Assert.DoesNotContain(result.Children, c => c.Name == "small.txt");
        }

        [Fact]
        public void Apply_NameQuery_MatchingFolderKeepsEntireSubtree()
        {
            var criteria = new FilterCriteria { NameQuery = "docs" };
            FileNode result = _service.Apply(Tree(), criteria);

            FileNode docs = Find(result, "docs");
            Assert.NotNull(docs);
            Assert.Equal(2, docs.Children.Count);
        }

        [Fact]
        public void Apply_EmptyFolderWithNoChildren_IsKeptAsOpaqueLeaf()
        {
            // A lazily-loaded tree exposes folders with no children yet; they must survive
            // a filter that matches nothing else instead of being erased.
            var root = TestTree.Folder("root");
            var lazy = new FileNode { Name = "Pending", FullPath = "root\\Pending", IsFolder = true, TotalSize = 123 };
            lazy.Parent = root;
            root.Children.Add(lazy);

            var criteria = new FilterCriteria { NameQuery = "nothing-matches" };
            FileNode result = _service.Apply(root, criteria);

            // The root itself has no children to descend into, so it survives as an
            // opaque container rather than being erased.
            Assert.NotNull(result);
            Assert.True(result.IsFolder);
        }

        [Fact]
        public void CountNodes_CountsWholeTree()
        {
            // root + big.bin + small.txt + docs + report.pdf + notes.txt
            Assert.Equal(6, FilterService.CountNodes(Tree()));
        }

        private static FileNode Find(FileNode node, string name)
        {
            if (node == null) return null;
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
