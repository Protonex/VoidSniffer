using VoidSniffer.Services;
using Xunit;

namespace VoidSniffer.Tests
{
    public class PathUtilTests
    {
        [Theory]
        [InlineData("C:\\Windows\\", "C:\\Windows")]
        [InlineData("C:/Windows/System32", "C:\\Windows\\System32")]
        [InlineData("  C:\\Temp  ", "C:\\Temp")]
        [InlineData("C:\\", "C:")]
        [InlineData("", "")]
        public void Normalize_CanonicalizesSeparatorsAndTrailingSlash(string input, string expected)
        {
            Assert.Equal(expected, PathUtil.Normalize(input));
        }

        [Theory]
        [InlineData("C:\\Windows\\notepad.exe", "notepad.exe")]
        [InlineData("C:\\Windows", "Windows")]
        [InlineData("C:\\", "C:")]
        public void GetName_ReturnsLastSegment(string input, string expected)
        {
            Assert.Equal(expected, PathUtil.GetName(input));
        }

        [Theory]
        [InlineData("C:\\Windows\\System32", "C:\\Windows")]
        [InlineData("C:\\Windows", "C:")]
        [InlineData("C:\\", "")]
        [InlineData("Windows", "")]
        public void GetParentPath_ReturnsParent(string input, string expected)
        {
            Assert.Equal(expected, PathUtil.GetParentPath(input));
        }

        [Theory]
        [InlineData("C:\\Windows\\System32", "C:\\Windows", true)]
        [InlineData("C:\\Windows", "C:\\Windows", true)]
        [InlineData("C:\\WindowsApps", "C:\\Windows", false)]
        [InlineData("D:\\Data", "C:\\Windows", false)]
        public void IsUnderRoot_DetectsContainment(string path, string root, bool expected)
        {
            Assert.Equal(expected, PathUtil.IsUnderRoot(path, root));
        }

        [Theory]
        [InlineData("C:\\Windows", "System32", "C:\\Windows\\System32")]
        [InlineData("C:\\Windows\\", "System32", "C:\\Windows\\System32")]
        public void Combine_JoinsWithoutDuplicateSeparators(string dir, string name, string expected)
        {
            Assert.Equal(expected, PathUtil.Combine(dir, name));
        }

        [Fact]
        public void EqualsPath_IsCaseInsensitive()
        {
            Assert.True(PathUtil.EqualsPath("C:\\WINDOWS\\System32", "c:\\windows\\system32\\"));
        }
    }
}
