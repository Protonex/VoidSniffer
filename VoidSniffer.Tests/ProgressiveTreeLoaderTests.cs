using System;
using System.Threading;
using System.Threading.Tasks;
using VoidSniffer.Model;
using VoidSniffer.Services;
using Xunit;

namespace VoidSniffer.Tests
{
    public class ProgressiveTreeLoaderTests
    {
        private static FakeFileSource Source()
        {
            var source = new FakeFileSource();
            source.AddFolder(@"C:\Root",
                FakeFileSource.Dir(@"C:\Root", "Alpha"),
                FakeFileSource.Dir(@"C:\Root", "Beta"),
                FakeFileSource.FileItem(@"C:\Root", "readme.txt", 500));
            source.AddFolder(@"C:\Root\Alpha",
                FakeFileSource.FileItem(@"C:\Root\Alpha", "big.bin", 4096));
            return source;
        }

        [Fact]
        public async Task LoadRootAsync_LoadsOnlyFirstLevel()
        {
            FakeFileSource source = Source();
            var loader = new ProgressiveTreeLoader(source, new FakeDiskUsage(4096));

            await loader.LoadRootAsync(@"C:\Root", CancellationToken.None);

            Assert.Equal("Root", loader.Root.Name);
            Assert.Equal(3, loader.Root.Children.Count);
            // Children not expanded yet.
            FileNode alpha = FakeFileSource.Find(loader.Root, @"C:\Root\Alpha");
            Assert.NotNull(alpha);
            Assert.Empty(alpha.Children);
            Assert.Equal(1, source.ChildQueryCount);
        }

        [Fact]
        public async Task ExpandAsync_IsIdempotent()
        {
            FakeFileSource source = Source();
            var loader = new ProgressiveTreeLoader(source, new FakeDiskUsage(4096));
            await loader.LoadRootAsync(@"C:\Root", CancellationToken.None);

            FileNode alpha = FakeFileSource.Find(loader.Root, @"C:\Root\Alpha");

            Assert.True(await loader.ExpandAsync(alpha, CancellationToken.None));
            Assert.Single(alpha.Children);
            Assert.Equal(2, source.ChildQueryCount);

            Assert.False(await loader.ExpandAsync(alpha, CancellationToken.None));
            Assert.Equal(2, source.ChildQueryCount);
        }

        [Fact]
        public async Task LoadRootAsync_SumsChildSizesIntoRootTotal()
        {
            var source = new FakeFileSource();
            source.AddFolder(@"C:\R",
                FakeFileSource.FileItem(@"C:\R", "a", 100),
                FakeFileSource.FileItem(@"C:\R", "b", 250));

            var loader = new ProgressiveTreeLoader(source, new FakeDiskUsage(4096));
            await loader.LoadRootAsync(@"C:\R", CancellationToken.None);

            Assert.Equal(350, loader.Root.TotalSize);
        }

        [Fact]
        public async Task Attach_ComputesExactOnDiskSizeForFiles()
        {
            var source = new FakeFileSource();
            source.AddFolder(@"C:\R",
                FakeFileSource.FileItem(@"C:\R", "one-cluster", 1),
                FakeFileSource.FileItem(@"C:\R", "three-cluster", 12289));

            var loader = new ProgressiveTreeLoader(source, new FakeDiskUsage(4096));
            await loader.LoadRootAsync(@"C:\R", CancellationToken.None);

            FileNode one = FakeFileSource.Find(loader.Root, @"C:\R\one-cluster");
            FileNode three = FakeFileSource.Find(loader.Root, @"C:\R\three-cluster");

            Assert.Equal(4096, one.OnDiskSize);          // 1 byte -> 1 cluster
            Assert.Equal(12288 + 4096, three.OnDiskSize); // 12289 -> 4 clusters
        }

        [Fact]
        public async Task GetCounts_AreAggregatedBeforeExpand()
        {
            var source = new FakeFileSource();
            source.AddFolder(@"C:\R");
            source.AddCounts(@"C:\R", 7, 3);

            var loader = new ProgressiveTreeLoader(source, new FakeDiskUsage(4096));
            await loader.LoadRootAsync(@"C:\R", CancellationToken.None);

            Tuple<long, long> counts = await source.GetCountsAsync(@"C:\R", CancellationToken.None);
            Assert.Equal(7, counts.Item1);
            Assert.Equal(3, counts.Item2);
        }
    }
}
