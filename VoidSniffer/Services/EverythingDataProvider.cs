using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VoidSniffer.Everything;
using VoidSniffer.Model;

namespace VoidSniffer.Services
{
    /// <summary>
    /// Retrieves file system contents for a folder from the Everything index
    /// (no direct disk scanning).
    /// </summary>
    public sealed class EverythingDataProvider
    {
        private readonly EverythingClient _client;

        public EverythingDataProvider()
        {
            _client = new EverythingClient();
            MaxResults = 200000;
        }

        /// <summary>Upper bound on the number of entries fetched for one query.</summary>
        public uint MaxResults { get; set; }

        /// <summary>Total number of matches Everything reported for the last query.</summary>
        public uint LastTotalItems { get; private set; }

        /// <summary>Number of entries actually returned for the last query.</summary>
        public int LastReturnedCount { get; private set; }

        /// <summary>True when the last query hit <see cref="MaxResults"/> and more matches remain.</summary>
        public bool LastResultTruncated
        {
            get { return LastTotalItems > (uint)LastReturnedCount; }
        }

        public bool IsAvailable()
        {
            return _client.IsAvailable();
        }

        public Version GetVersion()
        {
            return _client.GetVersion();
        }

        /// <summary>Fetches all files and folders under <paramref name="path"/> (recursively).</summary>
        public List<FileSystemItem> GetItems(string path, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("path is empty", "path");
            }
            if (!_client.IsAvailable())
            {
                throw new EverythingNotAvailableException();
            }

            cancellationToken.ThrowIfCancellationRequested();

            string query = BuildSubtreeQuery(path);
            EverythingResult result = _client.Search(
                query,
                EverythingSearchFlags.MatchPath,
                MaxResults,
                0,
                EverythingSort.None,
                false,
                EverythingBusyBehavior.WaitOrError);

            cancellationToken.ThrowIfCancellationRequested();

            var items = new List<FileSystemItem>(result.Items.Length);
            for (int i = 0; i < result.Items.Length; i++)
            {
                EverythingItem source = result.Items[i];
                long size = source.Size.HasValue ? (long)source.Size.Value : 0;
                items.Add(new FileSystemItem
                {
                    Name = source.Name,
                    ParentPath = source.Path,
                    FullPath = PathUtil.Combine(source.Path, source.Name),
                    IsFolder = source.IsFolder,
                    Size = size,
                    ModifiedTime = source.LastWriteTime
                });
            }

            LastTotalItems = result.TotalItems;
            LastReturnedCount = items.Count;
            return items;
        }

        public Task<List<FileSystemItem>> GetItemsAsync(string path, CancellationToken cancellationToken)
        {
            return Task.Run(() => GetItems(path, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// Builds an Everything query that selects the whole subtree of a folder.
        /// The trailing separator is significant: it prevents sibling folders whose
        /// names share a prefix from matching.
        /// </summary>
        internal static string BuildSubtreeQuery(string path)
        {
            string normalized = PathUtil.Normalize(path);
            if (!normalized.EndsWith("\\", StringComparison.Ordinal))
            {
                normalized += "\\";
            }
            return "\"" + normalized + "\"";
        }
    }
}
