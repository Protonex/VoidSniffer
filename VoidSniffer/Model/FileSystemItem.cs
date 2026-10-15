namespace VoidSniffer.Model
{
    /// <summary>
    /// Flat entry produced by <see cref="Services.EverythingDataProvider"/> and consumed
    /// by <see cref="Services.FileSystemTreeBuilder"/>.
    /// </summary>
    public sealed class FileSystemItem
    {
        public string Name { get; set; }

        /// <summary>Directory containing the item, without a trailing separator.</summary>
        public string ParentPath { get; set; }

        /// <summary>Full path of the item (parent path + name).</summary>
        public string FullPath { get; set; }

        public bool IsFolder { get; set; }

        /// <summary>Size in bytes. Zero for folders or when unknown.</summary>
        public long Size { get; set; }

        /// <summary>Last write time reported by the index, when available.</summary>
        public System.DateTime? ModifiedTime { get; set; }
    }
}
