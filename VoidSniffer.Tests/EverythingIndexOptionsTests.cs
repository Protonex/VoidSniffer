using VoidSniffer.Model;
using VoidSniffer.Services;
using Xunit;

namespace VoidSniffer.Tests
{
    public class EverythingIndexOptionsTests
    {
        [Fact]
        public void Parse_ReadsIndexFlags()
        {
            const string ini =
                "[Everything]\n" +
                "index_size=1\n" +
                "index_folder_size=0\n" +
                "index_date_created=1\n" +
                "index_date_modified=1\n" +
                "index_date_accessed=0\n" +
                "index_attributes=1\n" +
                "index_recent_changes=1\n";

            EverythingIndexOptions options = EverythingIndexOptions.Parse(ini);

            Assert.True(options.Loaded);
            Assert.True(options.FileSizes);
            Assert.False(options.FolderSizes);
            Assert.True(options.DateCreated);
            Assert.False(options.DateAccessed);
        }

        [Fact]
        public void Parse_IgnoresUnrelatedAndCommentLines()
        {
            const string ini =
                "; comment\n[Everything]\nthumbnail_size=64\nsize_column_width=96\nfile=x\nindex_folder_size=1\n";

            EverythingIndexOptions options = EverythingIndexOptions.Parse(ini);

            Assert.True(options.FolderSizes);
            Assert.Null(options.FileSizes);
        }

        [Fact]
        public void Parse_EmptyText_NotLoaded()
        {
            Assert.False(EverythingIndexOptions.Parse("").Loaded);
            Assert.False(EverythingIndexOptions.Parse(null).Loaded);
        }
    }

    public class IndexDiagnosticsTests
    {
        [Fact]
        public void BuildWarning_NoProblems_ReturnsNull()
        {
            var options = new EverythingIndexOptions
            {
                FileSizes = true,
                FolderSizes = true,
                DateCreated = true,
                DateModified = true,
                DateAccessed = true
            };
            Assert.Null(IndexDiagnostics.BuildWarning(options, TestTree.Folder("root")));
        }

        [Fact]
        public void BuildWarning_ReportsDisabledFolderSizes()
        {
            var options = new EverythingIndexOptions { FolderSizes = false, FileSizes = true };
            string warning = IndexDiagnostics.BuildWarning(options, TestTree.Folder("root"));

            Assert.NotNull(warning);
            Assert.Contains("Размеры папок", warning);
        }

        [Fact]
        public void BuildWarning_ReportsDisabledDates()
        {
            var options = new EverythingIndexOptions
            {
                FileSizes = true,
                FolderSizes = true,
                DateModified = false
            };
            string warning = IndexDiagnostics.BuildWarning(options, TestTree.Folder("root"));

            Assert.NotNull(warning);
            Assert.Contains("Дат", warning);
        }

        [Fact]
        public void BuildWarning_EmpiricallyDetectsMissingFolderSizesWhenIniUnknown()
        {
            // No ini options loaded: folders with zero size while a file has a size.
            FileNode root = TestTree.Folder("root",
                TestTree.Folder("empty-looking"),
                TestTree.File("a.bin", 1000));
            ByName(root, "empty-looking").TotalSize = 0;

            string warning = IndexDiagnostics.BuildWarning(new EverythingIndexOptions(), root);

            Assert.NotNull(warning);
            Assert.Contains("Размеры папок", warning);
        }

        [Fact]
        public void BuildWarning_EmpiricallyOk_ReturnsNull()
        {
            FileNode root = TestTree.Folder("root",
                TestTree.Folder("sub"),
                TestTree.File("a.bin", 1000));
            ByName(root, "sub").TotalSize = 5000; // folder size present

            Assert.Null(IndexDiagnostics.BuildWarning(new EverythingIndexOptions(), root));
        }

        private static FileNode ByName(FileNode root, string name)
        {
            for (int i = 0; i < root.Children.Count; i++)
            {
                if (root.Children[i].Name == name) return root.Children[i];
            }
            return null;
        }
    }
}
