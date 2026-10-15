using System;
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
            file.OnDiskSize = 4096;
            string text = NodeLabelFormatter.DescribeDetails(file);
            Assert.Equal("2 КБ", text.Replace('.', ','));
        }

        [Fact]
        public void DescribeDetails_Folder_ShowsSizeOnly_CountsAndDiskMovedToTooltip()
        {
            FileNode folder = TestTree.Folder("docs");
            folder.TotalSize = 1024 * 1024;
            folder.FileCount = 5;
            folder.FolderCount = 2;
            folder.OnDiskSize = 1024 * 1024 + 4096;

            string text = NodeLabelFormatter.DescribeDetails(folder);

            Assert.Equal("1 МБ", text);
            Assert.DoesNotContain("папк", text);
            Assert.DoesNotContain("файл", text);
            Assert.DoesNotContain("диске", text);
        }

        [Fact]
        public void DescribeDetails_FolderWithoutCounts_ShowsSizeOnly()
        {
            FileNode folder = TestTree.Folder("docs");
            folder.TotalSize = 500;

            string text = NodeLabelFormatter.DescribeDetails(folder);
            Assert.Equal("500 Б", text);
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

        [Fact]
        public void TooltipTitle_IsNodeName()
        {
            Assert.Equal("a.bin", NodeLabelFormatter.TooltipTitle(TestTree.File("a.bin", 10)));
        }

        [Fact]
        public void TooltipLines_IncludeSizeOnDiskCountsDatesAndPath()
        {
            FileNode folder = TestTree.Folder("docs");
            folder.Name = "docs";
            folder.FullPath = "C:\\docs";
            folder.TotalSize = 1024 * 1024;
            folder.OnDiskSize = 1024 * 1024 + 4096;
            folder.FileCount = 3;
            folder.FolderCount = 1;
            folder.CreatedTime = new DateTime(2020, 1, 2, 3, 4, 0);
            folder.ModifiedTime = new DateTime(2021, 5, 6, 7, 8, 0);
            folder.LastAccessTime = new DateTime(2022, 9, 10, 11, 12, 0);

            var lines = NodeLabelFormatter.TooltipLines(folder);
            string text = string.Join("\n", lines);

            Assert.Contains("1 МБ", text);
            Assert.Contains("на диске", text);
            Assert.Contains("1 папка, 3 файла", text);
            Assert.Contains("Создан: 2020-01-02 03:04", text);
            Assert.Contains("Изменён: 2021-05-06 07:08", text);
            Assert.Contains("Открыт: 2022-09-10 11:12", text);
            Assert.Contains("C:\\docs", text);
        }

        [Fact]
        public void TooltipLines_MinimalNode_OnlySizeAndPath()
        {
            FileNode file = TestTree.File("a", 512);
            file.FullPath = "C:\\a";

            var lines = NodeLabelFormatter.TooltipLines(file);
            Assert.Equal(2, lines.Count);
            Assert.Equal("512 Б", lines[0]);
            Assert.Equal("C:\\a", lines[1]);
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
