namespace VoidSniffer.Model
{
    /// <summary>
    /// Flat entry produced by <see cref="Services.EverythingDataProvider"/> and consumed
    /// by <see cref="Services.FileSystemTreeBuilder"/>.
    /// </summary>
    public sealed class FileSystemItem
    {
        public FileSystemItem()
        {
            // -1 means "not reported by the source" (the classic IPC path does not provide
            // these; only the Everything 1.5 index does).
            OnDiskSize = -1;
            FileCount = -1;
            FolderCount = -1;
        }

        public string Name { get; set; }

        /// <summary>Directory containing the item, without a trailing separator.</summary>
        public string ParentPath { get; set; }

        /// <summary>Full path of the item (parent path + name).</summary>
        public string FullPath { get; set; }

        public bool IsFolder { get; set; }

        /// <summary>Size in bytes. Zero for folders or when unknown.</summary>
        public long Size { get; set; }

        /// <summary>Creation time reported by the index, when available.</summary>
        public System.DateTime? CreatedTime { get; set; }

        /// <summary>Last write time reported by the index, when available.</summary>
        public System.DateTime? ModifiedTime { get; set; }

        /// <summary>Last access time reported by the index, when available.</summary>
        public System.DateTime? AccessedTime { get; set; }

        /// <summary>Exact allocated size reported by the Everything 1.5 index, or -1 when unknown.</summary>
        public long OnDiskSize { get; set; }

        /// <summary>Recursive number of files reported by the Everything 1.5 index, or -1 when unknown.</summary>
        public long FileCount { get; set; }

        /// <summary>Recursive number of folders reported by the Everything 1.5 index, or -1 when unknown.</summary>
        public long FolderCount { get; set; }
    }
}
