using VoidSniffer.Model;
using VoidSniffer.Treemap;
using Xunit;

namespace VoidSniffer.Tests
{
    public class NodeLabelFormatterTests
    {
        [Fact]
        public void DescribeDetails_File_ShowsSizeOnly()
        {
            FileNode file = TestTree.File("a.bin", 2048);
            string text = NodeLabelFormatter.DescribeDetails(file);
            Assert.Contains("2 КБ", text);
        }

        [Fact]
        public void DescribeDetails_FolderWithCounts_ShowsSizeAndCounts()
        {
            FileNode folder = TestTree.Folder("docs");
            folder.TotalSize = 1024 * 1024;
            folder.FileCount = 5;
            folder.FolderCount = 2;

            string text = NodeLabelFormatter.DescribeDetails(folder);

            Assert.Contains("1 МБ", text);
            Assert.Contains("2 папки", text);
            Assert.Contains("5 файлов", text);
        }

        [Fact]
        public void DescribeDetails_ShowsOnDiskWhenResolved()
        {
            FileNode file = TestTree.File("a.bin", 100);
            file.OnDiskSize = 4096;

            string text = NodeLabelFormatter.DescribeDetails(file);
            Assert.Contains("≈4 КБ на диске", text);
        }

        [Fact]
        public void DescribeDetails_FolderWithoutCounts_OmitsCounts()
        {
            FileNode folder = TestTree.Folder("docs");
            folder.TotalSize = 500;

            string text = NodeLabelFormatter.DescribeDetails(folder);
            Assert.DoesNotContain("папк", text);
            Assert.DoesNotContain("файл", text);
        }

        [Theory]
        [InlineData(1, "файл")]
        [InlineData(2, "файла")]
        [InlineData(4, "файла")]
        [InlineData(5, "файлов")]
        [InlineData(11, "файлов")]
        [InlineData(21, "файл")]
        [InlineData(22, "файла")]
        [InlineData(25, "файлов")]
        [InlineData(111, "файлов")]
        public void Plural_HandlesRussianRules(long value, string expected)
        {
            Assert.Equal(expected, NodeLabelFormatter.Plural(value, "файл", "файла", "файлов"));
        }

        [Theory]
        [InlineData(512, "512 Б")]
        [InlineData(1024, "1 КБ")]
        [InlineData(1536, "1,5 КБ")]
        [InlineData(1024 * 1024, "1 МБ")]
        public void FormatSize_UsesBinaryUnits(long bytes, string expected)
        {
            // The test runs under ru-RU where the decimal separator is a comma; normalize.
            string text = NodeLabelFormatter.FormatSize(bytes).Replace('.', ',');
            Assert.Equal(expected, text);
        }
    }
}
