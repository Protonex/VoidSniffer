using VoidSniffer.Services;
using Xunit;

namespace VoidSniffer.Tests
{
    public class DiskUsageTests
    {
        [Theory]
        [InlineData(0, 4096, 0)]
        [InlineData(1, 4096, 4096)]
        [InlineData(4095, 4096, 4096)]
        [InlineData(4096, 4096, 4096)]
        [InlineData(4097, 4096, 8192)]
        [InlineData(12289, 4096, 16384)]
        public void AllocatedFileSize_RoundsUpToWholeClusters(long size, int cluster, long expected)
        {
            Assert.Equal(expected, DiskUsage.AllocatedFileSize(size, cluster));
        }

        [Fact]
        public void AllocatedFileSize_NonPowerCluster_PassthroughWhenTrivial()
        {
            Assert.Equal(100, DiskUsage.AllocatedFileSize(100, 1));
        }

        [Fact]
        public void EstimatedFolderAllocated_ZeroFiles_IsZero()
        {
            Assert.Equal(0, DiskUsage.EstimatedFolderAllocated(1000, 0, 4096));
        }

        [Fact]
        public void EstimatedFolderAllocated_AddsHalfClusterSlackPerFile()
        {
            // 100 files => 100 * (4095/2) = 204700 bytes of expected slack.
            Assert.Equal(1000000 + 204700, DiskUsage.EstimatedFolderAllocated(1000000, 100, 4096));
        }

        [Fact]
        public void EstimatedFolderAllocated_NoFilesOrNoSize_IsZero()
        {
            Assert.Equal(0, DiskUsage.EstimatedFolderAllocated(1000, 0, 4096));
            Assert.Equal(0, DiskUsage.EstimatedFolderAllocated(0, 10, 4096));
        }

        [Theory]
        [InlineData(@"C:\Windows\System32", @"C:\")]
        [InlineData(@"c:\temp", @"C:\")]
        [InlineData(@"\\server\share\sub", @"\\server\share\")]
        [InlineData(@"relative\path", null)]
        public void GetVolumeRoot_ResolvesDriveAndUncRoots(string path, string expected)
        {
            Assert.Equal(expected, DiskUsageInfo.GetVolumeRoot(path));
        }

        [Fact]
        public void TryGetFreeSpace_ForSystemDrive_ReturnsPositive()
        {
            long free;
            bool ok = DiskUsageInfo.TryGetFreeSpace(@"C:\", out free);
            if (ok)
            {
                Assert.True(free > 0);
            }
        }

        [Fact]
        public void TryGetFreeSpace_ForPathWithoutVolume_ReturnsFalse()
        {
            long free;
            Assert.False(DiskUsageInfo.TryGetFreeSpace(@"relative\path", out free));
        }
    }
}
