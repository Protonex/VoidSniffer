using System;
using System.Collections.Generic;

namespace VoidSniffer.Model
{
    /// <summary>Node of the in-memory file system hierarchy used for treemap rendering.</summary>
    public sealed class FileNode
    {
        public FileNode()
        {
            Children = new List<FileNode>();
            FileCount = -1;
            FolderCount = -1;
            OnDiskSize = -1;
        }

        public string Name { get; set; }

        public string FullPath { get; set; }

        public bool IsFolder { get; set; }

        /// <summary>Last write time reported by the index, when available.</summary>
        public DateTime? ModifiedTime { get; set; }

        /// <summary>Creation time reported by the index, when available.</summary>
        public DateTime? CreatedTime { get; set; }

        /// <summary>Last access time reported by the index, when available.</summary>
        public DateTime? LastAccessTime { get; set; }

        /// <summary>Size of this node alone (files only; folders report 0).</summary>
        public long OwnSize { get; set; }

        /// <summary>Aggregate size of this node and all of its descendants.</summary>
        public long TotalSize { get; set; }

        /// <summary>Recursive number of files under this folder, or -1 when unknown.</summary>
        public long FileCount { get; set; }

        /// <summary>Recursive number of folders under this folder, or -1 when unknown.</summary>
        public long FolderCount { get; set; }

        /// <summary>True when the recursive file/folder counts have been resolved.</summary>
        public bool HasCounts
        {
            get { return FileCount >= 0 && FolderCount >= 0; }
        }

        /// <summary>True for nodes the view adds itself (e.g. the free-space tile).</summary>
        public bool IsSynthetic { get; set; }

        /// <summary>
        /// Allocated size (space occupied on disk) of this node, or -1 when unknown.
        /// Exact for files; an estimate based on the volume cluster size for folders.
        /// </summary>
        public long OnDiskSize { get; set; }

        /// <summary>True when the allocated size has been resolved.</summary>
        public bool HasOnDisk
        {
            get { return OnDiskSize >= 0; }
        }

        public int Depth { get; set; }

        public FileNode Parent { get; set; }

        public List<FileNode> Children { get; private set; }

        public override string ToString()
        {
            return FullPath;
        }
    }
}
