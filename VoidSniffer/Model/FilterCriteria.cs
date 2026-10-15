using System;

namespace VoidSniffer.Model
{
    /// <summary>
    /// Criteria used by <see cref="Services.FilterService"/> to prune a file system tree.
    /// Every unset member is ignored, so an empty instance selects everything.
    /// </summary>
    public sealed class FilterCriteria
    {
        public FilterCriteria()
        {
            IncludeFiles = true;
            IncludeFolders = true;
        }

        /// <summary>Case-insensitive substring matched against a node name. Empty disables the name filter.</summary>
        public string NameQuery { get; set; }

        /// <summary>Treat <see cref="NameQuery"/> as a regular expression.</summary>
        public bool RegexMode { get; set; }

        /// <summary>Minimum file size in bytes, inclusive.</summary>
        public long? MinSize { get; set; }

        /// <summary>Maximum file size in bytes, inclusive.</summary>
        public long? MaxSize { get; set; }

        /// <summary>Only files modified at or after this instant are kept.</summary>
        public DateTime? ModifiedAfter { get; set; }

        /// <summary>Only files modified at or before this instant are kept.</summary>
        public DateTime? ModifiedBefore { get; set; }

        /// <summary>When false, files are removed and only folder containers remain.</summary>
        public bool IncludeFiles { get; set; }

        /// <summary>Only matters together with <see cref="NameQuery"/> (see <see cref="Services.FilterService"/>).</summary>
        public bool IncludeFolders { get; set; }

        /// <summary>True when at least one criterion actually removes nodes.</summary>
        public bool HasActiveFilters
        {
            get
            {
                return !string.IsNullOrWhiteSpace(NameQuery)
                    || MinSize.HasValue
                    || MaxSize.HasValue
                    || ModifiedAfter.HasValue
                    || ModifiedBefore.HasValue
                    || !IncludeFiles;
            }
        }

        public void Reset()
        {
            NameQuery = null;
            RegexMode = false;
            MinSize = null;
            MaxSize = null;
            ModifiedAfter = null;
            ModifiedBefore = null;
            IncludeFiles = true;
            IncludeFolders = true;
        }
    }
}
