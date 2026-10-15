using System;
using System.Collections.Generic;
using System.Drawing;
using VoidSniffer.Model;

namespace VoidSniffer.Treemap
{
    /// <summary>
    /// Caches the render layout of one <see cref="FileNode"/> subtree for a given
    /// client size, and provides fast lookups on top of it:
    /// <list type="bullet">
    /// <item>node -> block mapping for highlight painting;</item>
    /// <item>a uniform spatial grid so hit-testing does not scan every block.</item>
    /// </list>
    /// The cache is rebuilt only when the content, the size or an explicit
    /// <see cref="Invalidate()"/> requires it.
    /// </summary>
    public sealed class TreemapLayoutCache
    {
        private readonly TreemapOptions _options;
        private readonly int _cellSize;

        private List<TreemapBlock> _blocks = new List<TreemapBlock>();
        private Dictionary<FileNode, TreemapBlock> _byNode = new Dictionary<FileNode, TreemapBlock>();
        private List<int>[] _grid = new List<int>[0];
        private int _columns;
        private int _rows;

        private FileNode _content;
        private Size _size = new Size(-1, -1);
        private bool _dirty = true;

        public TreemapLayoutCache(TreemapOptions options, int cellSize = 64)
        {
            if (options == null) throw new ArgumentNullException("options");
            if (cellSize <= 0) throw new ArgumentOutOfRangeException("cellSize");

            _options = options;
            _cellSize = cellSize;
        }

        /// <summary>Number of laid-out blocks currently cached.</summary>
        public int BlockCount
        {
            get { return _blocks.Count; }
        }

        /// <summary>Number of times the layout has been recomputed (diagnostics/tests).</summary>
        public int RebuildCount { get; private set; }

        /// <summary>Marks the layout as stale; it will be recomputed on next access.</summary>
        public void Invalidate()
        {
            _dirty = true;
        }

        /// <summary>Rebuilds the layout if the content, the size or the cache is stale.</summary>
        public void Ensure(FileNode content, Size size)
        {
            if (!_dirty && _size == size && ReferenceEquals(_content, content)) return;
            Rebuild(content, size);
        }

        private void Rebuild(FileNode content, Size size)
        {
            _content = content;

            if (content == null || size.Width <= 0 || size.Height <= 0)
            {
                _blocks = new List<TreemapBlock>();
            }
            else
            {
                _blocks = TreemapLayout.Layout(content, new Rectangle(0, 0, size.Width, size.Height), _options);
            }

            _byNode = new Dictionary<FileNode, TreemapBlock>(_blocks.Count);
            for (int i = 0; i < _blocks.Count; i++)
            {
                TreemapBlock block = _blocks[i];
                if (block.Node != null)
                {
                    _byNode[block.Node] = block;
                }
            }

            BuildGrid(size);
            _size = size;
            _dirty = false;
            RebuildCount++;
        }

        private void BuildGrid(Size size)
        {
            _columns = Math.Max(1, (size.Width + _cellSize - 1) / _cellSize);
            _rows = Math.Max(1, (size.Height + _cellSize - 1) / _cellSize);
            _grid = new List<int>[_columns * _rows];

            for (int i = 0; i < _blocks.Count; i++)
            {
                Rectangle rect = Clamp(ToClientRect(_blocks[i].Bounds), size);
                if (rect.Width <= 0 || rect.Height <= 0) continue;

                int col0 = rect.Left / _cellSize;
                int col1 = (rect.Right - 1) / _cellSize;
                int row0 = rect.Top / _cellSize;
                int row1 = (rect.Bottom - 1) / _cellSize;

                for (int row = row0; row <= row1; row++)
                {
                    int rowOffset = row * _columns;
                    for (int col = col0; col <= col1; col++)
                    {
                        int cell = rowOffset + col;
                        List<int> bucket = _grid[cell];
                        if (bucket == null)
                        {
                            bucket = new List<int>(4);
                            _grid[cell] = bucket;
                        }
                        bucket.Add(i);
                    }
                }
            }
        }

        /// <summary>Block of the given node, or <c>null</c> when it is not rendered.</summary>
        public TreemapBlock Find(FileNode node)
        {
            if (node == null) return null;
            TreemapBlock block;
            return _byNode.TryGetValue(node, out block) ? block : null;
        }

        /// <summary>Deepest block containing the point, or <c>null</c>.</summary>
        public TreemapBlock HitTest(Point point)
        {
            if (point.X < 0 || point.Y < 0) return null;
            if (_grid.Length == 0) return null;

            int col = point.X / _cellSize;
            int row = point.Y / _cellSize;
            if (col < 0 || col >= _columns || row < 0 || row >= _rows) return null;

            List<int> bucket = _grid[row * _columns + col];
            if (bucket == null) return null;

            // Blocks are stored in paint order, so the last match is the deepest (topmost) one.
            for (int k = bucket.Count - 1; k >= 0; k--)
            {
                TreemapBlock block = _blocks[bucket[k]];
                if (block.Bounds.Width < 1f || block.Bounds.Height < 1f) continue;
                if (block.Bounds.Contains(point.X, point.Y)) return block;
            }
            return null;
        }

        /// <summary>Rounds a float layout rectangle to the pixel rectangle used for painting.</summary>
        public static Rectangle ToClientRect(RectangleF f)
        {
            int left = (int)Math.Floor(f.Left);
            int top = (int)Math.Floor(f.Top);
            int right = (int)Math.Ceiling(f.Right);
            int bottom = (int)Math.Ceiling(f.Bottom);
            if (right <= left) right = left + 1;
            if (bottom <= top) bottom = top + 1;
            return Rectangle.FromLTRB(left, top, right, bottom);
        }

        private static Rectangle Clamp(Rectangle rect, Size size)
        {
            int left = Math.Max(0, rect.Left);
            int top = Math.Max(0, rect.Top);
            int right = Math.Min(size.Width, rect.Right);
            int bottom = Math.Min(size.Height, rect.Bottom);
            if (right <= left || bottom <= top) return Rectangle.Empty;
            return Rectangle.FromLTRB(left, top, right, bottom);
        }

        /// <summary>
        /// Cached blocks in paint order (containers before their children).
        /// Treat as read-only.
        /// </summary>
        public IList<TreemapBlock> Blocks
        {
            get { return _blocks; }
        }
    }
}
