using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VoidSniffer.Model;

namespace VoidSniffer.Services
{
    /// <summary>
    /// Minimal contract the progressive tree loader needs from the data source. Implemented
    /// by <see cref="EverythingDataProvider"/> and faked in tests, so the loader's
    /// breadcrumb behaviour can be covered without a running Everything instance.
    /// </summary>
    public interface IEverythingFileSource
    {
        /// <summary>Direct children of a folder (non-recursive).</summary>
        Task<List<FileSystemItem>> GetChildrenAsync(string path, CancellationToken cancellationToken);

        /// <summary>Recursive file and folder counts under a folder.</summary>
        Task<Tuple<long, long>> GetCountsAsync(string path, CancellationToken cancellationToken);

        /// <summary>True when the last child query hit the result limit and more remain.</summary>
        bool LastResultTruncated { get; }
    }
}
