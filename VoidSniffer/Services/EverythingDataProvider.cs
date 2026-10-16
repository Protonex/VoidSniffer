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
    public sealed class EverythingDataProvider : IEverythingFileSource
    {
        private readonly EverythingClient _client;

        // Everything 1.5 "SDK3" pipe client; preferred when present, otherwise the classic
        // WM_COPYDATA IPC below is used.
        private readonly Everything3Client _sdk3 = new Everything3Client();
        private bool _sdk3Checked;
        private bool _sdk3Available;
        private int _sdk3Failures;

        // Everything's IPC handles a single query at a time; serialize all calls so
        // lazy expansions and count lookups running on different threads cannot collide.
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        public EverythingDataProvider()
        {
            _client = new EverythingClient();
            MaxResults = 200000;
        }

        /// <summary>True when the Everything 1.5 pipe (SDK3) is being used.</summary>
        public bool IsSdk3Available()
        {
            EnsureSdk3();
            return _sdk3Available;
        }

        private void EnsureSdk3()
        {
            if (_sdk3Checked) return;
            _sdk3Checked = true;

            // The Everything 1.5 named-pipe transport proved unreliable across sessions, so
            // it is opt-in: set VOIDSNIFFER_USE_SDK3=1 to prefer it. The classic WM_COPYDATA
            // IPC stays the default and the fallback.
            if (!string.Equals(Environment.GetEnvironmentVariable("VOIDSNIFFER_USE_SDK3"), "1", StringComparison.Ordinal))
            {
                _sdk3Available = false;
                return;
            }

            try
            {
                _sdk3Available = _sdk3.TryConnect();
            }
            catch (Exception)
            {
                _sdk3Available = false;
            }
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

        private bool EnsureSdk3ForQuery()
        {
            EnsureSdk3();
            return _sdk3Available;
        }

        public bool IsAvailable()
        {
            if (IsSdk3Available()) return true;
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

            _gate.Wait(cancellationToken);
            try
            {
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
                        CreatedTime = source.CreationTime,
                        ModifiedTime = source.LastWriteTime,
                        AccessedTime = source.LastAccessTime
                    });
                }

                LastTotalItems = result.TotalItems;
                LastReturnedCount = items.Count;
                return items;
            }
            finally
            {
                _gate.Release();
            }
        }

        public Task<List<FileSystemItem>> GetItemsAsync(string path, CancellationToken cancellationToken)
        {
            return Task.Run(() => GetItems(path, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// Fetches only the direct children of <paramref name="path"/> (non-recursive).
        /// This is the cheap query used for progressive, level-by-level tree building;
        /// it avoids the one big subtree request that stalls the UI.
        /// </summary>
        public List<FileSystemItem> GetChildren(string path, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("path is empty", "path");
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Prefer the Everything 1.5 pipe (SDK3): one query returns name, path, size,
            // exact on-disk size, recursive counts and dates.
            if (EnsureSdk3ForQuery())
            {
                _gate.Wait(cancellationToken);
                try
                {
                    long total;
                    bool truncated;
                    List<FileSystemItem> sdk3Items = _sdk3.GetChildren(
                        PathUtil.Normalize(path), (int)MaxResults, out total, out truncated);

                    if (sdk3Items != null)
                    {
                        _sdk3Failures = 0;
                        LastTotalItems = total < 0 ? 0 : (uint)Math.Min(total, uint.MaxValue);
                        LastReturnedCount = sdk3Items.Count;
                        return sdk3Items;
                    }

                    // Pipe hiccup: fall back to classic for this call, but keep SDK3
                    // enabled and retry it next time; disable it after repeated failures.
                    if (++_sdk3Failures >= 3)
                    {
                        _sdk3Available = false;
                    }
                }
                finally
                {
                    _gate.Release();
                }
            }

            if (!_client.IsAvailable())
            {
                throw new EverythingNotAvailableException();
            }

            cancellationToken.ThrowIfCancellationRequested();

            _gate.Wait(cancellationToken);
            try
            {
                string query = BuildChildrenQuery(path);
                EverythingResult result = _client.Search(
                    query,
                    EverythingSearchFlags.None,
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
                        CreatedTime = source.CreationTime,
                        ModifiedTime = source.LastWriteTime,
                        AccessedTime = source.LastAccessTime
                    });
                }

                LastTotalItems = result.TotalItems;
                LastReturnedCount = items.Count;
                return items;
            }
            finally
            {
                _gate.Release();
            }
        }

        public Task<List<FileSystemItem>> GetChildrenAsync(string path, CancellationToken cancellationToken)
        {
            return Task.Run(() => GetChildren(path, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// Returns the recursive number of files and folders under <paramref name="path"/>.
        /// The counts are obtained as the total match count of two count-only queries
        /// (<c>max_results = 0</c>), so no items are transferred and nothing is scanned;
        /// the numbers come straight from the Everything index.
        /// </summary>
        public void GetCounts(string path, CancellationToken cancellationToken, out long fileCount, out long folderCount)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("path is empty", "path");
            }
            if (!_client.IsAvailable())
            {
                throw new EverythingNotAvailableException();
            }

            string subtree = BuildSubtreeQuery(path);

            _gate.Wait(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                EverythingResult files = _client.Search(
                    subtree + " file:",
                    EverythingSearchFlags.MatchPath,
                    0,
                    0,
                    EverythingSort.None,
                    false,
                    EverythingBusyBehavior.WaitOrError);

                cancellationToken.ThrowIfCancellationRequested();
                EverythingResult folders = _client.Search(
                    subtree + " folder:",
                    EverythingSearchFlags.MatchPath,
                    0,
                    0,
                    EverythingSort.None,
                    false,
                    EverythingBusyBehavior.WaitOrError);

                fileCount = files.TotalItems;
                folderCount = folders.TotalItems;
            }
            finally
            {
                _gate.Release();
            }
        }

        public Task<Tuple<long, long>> GetCountsAsync(string path, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                long files, folders;
                GetCounts(path, cancellationToken, out files, out folders);
                return Tuple.Create(files, folders);
            }, cancellationToken);
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

        /// <summary>
        /// Builds an Everything query that selects only the direct children of a folder.
        /// The <c>parent:</c> function (Everything 1.4+) matches items whose immediate
        /// parent is the given path and does not descend into subfolders.
        /// </summary>
        internal static string BuildChildrenQuery(string path)
        {
            string normalized = PathUtil.Normalize(path);
            return "parent:\"" + normalized + "\"";
        }
    }
}

