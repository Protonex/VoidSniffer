using System.Drawing;
using VoidSniffer.Model;

namespace VoidSniffer.Treemap
{
    /// <summary>A laid-out rectangle for one <see cref="FileNode"/>.</summary>
    public sealed class TreemapBlock
    {
        public FileNode Node { get; set; }

        public RectangleF Bounds { get; set; }

        /// <summary>Nesting level: 0 = direct children of the focused node.</summary>
        public int Depth { get; set; }

        /// <summary>Index that drives the palette color of this block.</summary>
        public int ColorIndex { get; set; }

        /// <summary>True when the block is painted and can be hit-tested.</summary>
        public bool IsLeaf
        {
            get { return Node == null || !Node.IsFolder; }
        }
    }
}
