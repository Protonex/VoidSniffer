using System;

namespace VoidSniffer.Everything
{
    /// <summary>Flags describing a single item returned by Everything.</summary>
    [Flags]
    public enum EverythingItemFlags
    {
        None = 0,
        Folder = 1,
        Drive = 2,
        Unknown = 0x80
    }

    /// <summary>Win32 file attribute flags as reported by Everything.</summary>
    [Flags]
    public enum EverythingItemAttributes
    {
        None = 0,
        ReadOnly = 1,
        Hidden = 2,
        System = 4,
        Directory = 16,
        Archive = 32,
        Normal = 128
    }

    /// <summary>One entry of an Everything search result.</summary>
    public sealed class EverythingItem
    {
        public EverythingItemFlags Flags { get; internal set; }

        /// <summary>Item name without any path segment.</summary>
        public string Name { get; internal set; }

        /// <summary>Directory that contains the item (no trailing separator, no name).</summary>
        public string Path { get; internal set; }

        /// <summary>Size on disk in bytes, if requested and known.</summary>
        public ulong? Size { get; internal set; }

        public DateTime? CreationTime { get; internal set; }

        public DateTime? LastWriteTime { get; internal set; }

        public DateTime? LastAccessTime { get; internal set; }

        public EverythingItemAttributes? Attributes { get; internal set; }

        public bool IsFolder
        {
            get { return (Flags & EverythingItemFlags.Folder) == EverythingItemFlags.Folder; }
        }
    }

    /// <summary>Result set of an Everything search.</summary>
    public sealed class EverythingResult
    {
        /// <summary>Total number of matches reported by Everything.</summary>
        public uint TotalItems { get; internal set; }

        /// <summary>Offset of <see cref="Items"/> inside the full result list.</summary>
        public uint Offset { get; internal set; }

        public EverythingItem[] Items { get; internal set; }

        public EverythingResult()
        {
            Items = new EverythingItem[0];
        }
    }
}
