using VoidSniffer;
using Xunit;

namespace VoidSniffer.Tests
{
    public class CommandLineTests
    {
        [Fact]
        public void NoArgs_ReturnsNull()
        {
            Assert.Null(Program.ParseScanPath(new string[0]));
            Assert.Null(Program.ParseScanPath(null));
        }

        [Fact]
        public void ScanCommand_ReturnsPath()
        {
            Assert.Equal(@"C:\", Program.ParseScanPath(new[] { "scan", @"C:\" }));
        }

        [Fact]
        public void ScanCommand_KeepsQuotedPathWithSpaces()
        {
            // The shell passes a quoted argument as a single element.
            Assert.Equal(@"C:\Program Files", Program.ParseScanPath(new[] { "scan", @"C:\Program Files" }));
        }

        [Fact]
        public void BarePath_IsAccepted()
        {
            Assert.Equal(@"C:\Users", Program.ParseScanPath(new[] { @"C:\Users" }));
        }

        [Fact]
        public void ScanEqualsForm_IsAccepted()
        {
            Assert.Equal(@"D:\Data", Program.ParseScanPath(new[] { @"--scan=D:\Data" }));
        }

        [Fact]
        public void ScanWithoutPath_ReturnsNull()
        {
            Assert.Null(Program.ParseScanPath(new[] { "scan" }));
        }
    }
}
