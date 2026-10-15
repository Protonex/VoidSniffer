using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using VoidSniffer.Model;

namespace VoidSniffer.Treemap
{
    /// <summary>Event payload carrying the <see cref="FileNode"/> a control action refers to.</summary>
    public sealed class TreemapNodeEventArgs : EventArgs
    {
        public TreemapNodeEventArgs(FileNode node)
        {
            Node = node;
        }

        public FileNode Node { get; private set; }
    }

    /// <summary>
    /// GDI+ treemap view: renders the children of <see cref="Content"/>, supports
    /// hit-testing and reports user intent through events. It deliberately owns no
    /// navigation state — that lives in <see cref="Services.NavigationController"/>.
    /// </summary>
    /// <remarks>
    /// Layout is cached in <see cref="TreemapLayoutCache"/> and recomputed only when the
    /// content or the client size changes. Painting is clip-aware: only blocks that
    /// intersect the invalidated region are drawn, and hover/selection changes invalidate
    /// just the affected block rectangles.
    /// </remarks>
    public sealed class TreemapControl : Control
    {
        private readonly TreemapOptions _options = new TreemapOptions();
        private readonly TreemapLayoutCache _cache;
        private readonly Dictionary<int, SolidBrush> _brushCache = new Dictionary<int, SolidBrush>();
        private readonly Dictionary<int, Pen> _borderCache = new Dictionary<int, Pen>();

        private Font _labelFont;
        private FileNode _content;
        private FileNode _hoverNode;
        private FileNode _selected;

        private const int MaxLabels = 4000;
        private const int HighlightMargin = 3;

        /// <summary>A block was double-clicked (drill in).</summary>
        public event EventHandler<TreemapNodeEventArgs> BlockActivated;

        /// <summary>Zoom-out was requested (right click / back gesture).</summary>
        public event EventHandler ZoomOutRequested;

        public event EventHandler<TreemapNodeEventArgs> HoverChanged;
        public event EventHandler<TreemapNodeEventArgs> SelectionChanged;

        public TreemapControl()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw,
                true);

            BackColor = Color.FromArgb(28, 28, 30);
            _labelFont = new Font("Segoe UI", 7.5f, FontStyle.Regular, GraphicsUnit.Point);
            _cache = new TreemapLayoutCache(_options);
        }

        /// <summary>Node whose children are currently rendered.</summary>
        public FileNode Content
        {
            get { return _content; }
        }

        public FileNode SelectedNode
        {
            get { return _selected; }
        }

        /// <summary>Number of laid-out blocks for the current content and size.</summary>
        public int BlockCount
        {
            get
            {
                EnsureLayout();
                return _cache.BlockCount;
            }
        }

        /// <summary>Number of layout recomputations since construction (diagnostics).</summary>
        public int LayoutRebuildCount
        {
            get { return _cache.RebuildCount; }
        }

        /// <summary>Sets the node to render and clears transient hover/selection state.</summary>
        public void SetContent(FileNode node)
        {
            _content = node;
            _selected = null;
            _hoverNode = null;
            _cache.Invalidate();
            Invalidate();
        }

        public void Select(FileNode node)
        {
            if (ReferenceEquals(_selected, node)) return;

            FileNode previous = _selected;
            _selected = node;

            EnsureLayout();
            InvalidateBlock(previous);
            InvalidateBlock(node);

            Raise(SelectionChanged, new TreemapNodeEventArgs(node));
        }

        public FileNode HitTestNode(Point point)
        {
            EnsureLayout();
            TreemapBlock block = _cache.HitTest(point);
            return block != null ? block.Node : null;
        }

        private void EnsureLayout()
        {
            _cache.Ensure(_content, ClientSize);
        }

        /// <summary>Invalidates only the pixel rectangle of a node's block (plus a margin).</summary>
        private void InvalidateBlock(FileNode node)
        {
            if (node == null) return;

            TreemapBlock block = _cache.Find(node);
            if (block == null) return;

            Rectangle rect = TreemapLayoutCache.ToClientRect(block.Bounds);
            rect.Inflate(HighlightMargin, HighlightMargin);
            rect.Intersect(ClientRectangle);
            if (rect.Width > 0 && rect.Height > 0)
            {
                Invalidate(rect);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            EnsureLayout();

            Graphics g = e.Graphics;
            Rectangle clip = e.ClipRectangle;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            IList<TreemapBlock> blocks = _cache.Blocks;
            int labelBudget = MaxLabels;

            for (int i = 0; i < blocks.Count; i++)
            {
                TreemapBlock block = blocks[i];
                Rectangle rect = TreemapLayoutCache.ToClientRect(block.Bounds);
                if (rect.Width < 1 || rect.Height < 1) continue;
                if (!rect.IntersectsWith(clip)) continue;

                Color fill = TreemapColorizer.GetFill(block.Node, block.Depth);
                g.FillRectangle(GetBrush(fill), rect);
                g.DrawRectangle(GetBorderPen(block.Depth), rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
            }

            DrawHighlight(g, clip, _cache.Find(_hoverNode), Color.FromArgb(120, 255, 255, 255), 1);
            DrawHighlight(g, clip, _cache.Find(_selected), Color.FromArgb(255, 255, 210), 2);

            for (int i = 0; i < blocks.Count && labelBudget > 0; i++)
            {
                labelBudget -= DrawLabel(g, clip, blocks[i]);
            }
        }

        private void DrawHighlight(Graphics g, Rectangle clip, TreemapBlock block, Color color, int width)
        {
            if (block == null) return;

            Rectangle rect = TreemapLayoutCache.ToClientRect(block.Bounds);
            if (rect.Width < 2 || rect.Height < 2) return;
            if (!rect.IntersectsWith(clip)) return;

            using (var pen = new Pen(color, width))
            {
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
            }
        }

        private int DrawLabel(Graphics g, Rectangle clip, TreemapBlock block)
        {
            Rectangle rect = TreemapLayoutCache.ToClientRect(block.Bounds);
            if (rect.Width < 30 || rect.Height < 12) return 0;
            if (!rect.IntersectsWith(clip)) return 0;

            int left = rect.X + 2;
            int width = rect.Width - 4;
            if (width < 8) return 0;

            Color fill = TreemapColorizer.GetFill(block.Node, block.Depth);
            Color text = TreemapColorizer.GetTextColor(fill);
            Color subText = Color.FromArgb(210, text.R, text.G, text.B);

            int lineH = _labelFont.Height + 1;
            int available = block.Node.IsFolder
                ? Math.Min(rect.Height, (int)_options.TitleHeight)
                : rect.Height;
            if (available < lineH) return 0;

            DrawLabelLine(g, block.Node.Name, new Rectangle(left, rect.Y + 1, width, lineH), text);

            if (available >= lineH * 2)
            {
                DrawLabelLine(g, DescribeNode(block.Node),
                    new Rectangle(left, rect.Y + 1 + lineH, width, lineH), subText);
            }

            return 1;
        }

        private void DrawLabelLine(Graphics g, string text, Rectangle rect, Color color)
        {
            if (string.IsNullOrEmpty(text) || rect.Width <= 0) return;

            TextRenderer.DrawText(
                g,
                text,
                _labelFont,
                rect,
                color,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        /// <summary>Second label line: size and, for folders, recursive file/folder counts.</summary>
        private static string DescribeNode(FileNode node)
        {
            string size = FormatSize(node.TotalSize);
            if (!node.IsFolder || !node.HasCounts)
            {
                return size;
            }

            return string.Format("{0}  ·  {1}, {2}",
                size,
                FormatCount(node.FolderCount, "папка", "папки", "папок"),
                FormatCount(node.FileCount, "файл", "файла", "файлов"));
        }

        private static string FormatCount(long value, string one, string few, string many)
        {
            return value.ToString("N0", CultureInfo.CurrentCulture) + " " + Plural(value, one, few, many);
        }

        private static string Plural(long value, string one, string few, string many)
        {
            long mod100 = value % 100;
            long mod10 = value % 10;
            if (mod10 == 1 && mod100 != 11) return one;
            if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return few;
            return many;
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes.ToString("N0", CultureInfo.CurrentCulture) + " Б";

            string[] units = { "КБ", "МБ", "ГБ", "ТБ", "ПБ" };
            double value = bytes;
            int unit = -1;
            do
            {
                value /= 1024.0;
                unit++;
            }
            while (value >= 1024.0 && unit < units.Length - 1);

            string format = value >= 100 ? "0" : "0.#";
            return value.ToString(format, CultureInfo.CurrentCulture) + " " + units[unit];
        }

        /// <summary>
        /// Nodes whose blocks are large enough to carry a label right now. Used to request
        /// the recursive counts only for the folders that are actually visible.
        /// </summary>
        public IList<FileNode> GetLabelNodes()
        {
            EnsureLayout();

            var result = new List<FileNode>();
            IList<TreemapBlock> blocks = _cache.Blocks;
            for (int i = 0; i < blocks.Count; i++)
            {
                TreemapBlock block = blocks[i];
                if (block.Node == null) continue;

                Rectangle rect = TreemapLayoutCache.ToClientRect(block.Bounds);
                if (rect.Width >= 30 && rect.Height >= 12)
                {
                    result.Add(block.Node);
                }
            }
            return result;
        }

        private SolidBrush GetBrush(Color color)
        {
            int key = color.ToArgb();
            SolidBrush brush;
            if (!_brushCache.TryGetValue(key, out brush))
            {
                if (_brushCache.Count > 4096) ClearBrushCache();
                brush = new SolidBrush(color);
                _brushCache[key] = brush;
            }
            return brush;
        }

        private Pen GetBorderPen(int depth)
        {
            Pen pen;
            if (!_borderCache.TryGetValue(depth, out pen))
            {
                pen = new Pen(TreemapColorizer.GetBorderColor(depth), 1f);
                _borderCache[depth] = pen;
            }
            return pen;
        }

        private void ClearBrushCache()
        {
            foreach (SolidBrush brush in _brushCache.Values) brush.Dispose();
            _brushCache.Clear();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _hoverNode = null;
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button != MouseButtons.Left) return;

            TreemapBlock block = HitTestBlock(e.Location);
            if (block == null || block.Node == null) return;

            Select(block.Node);
            Raise(BlockActivated, new TreemapNodeEventArgs(block.Node));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Right)
            {
                Raise(ZoomOutRequested, EventArgs.Empty);
                return;
            }
            if (e.Button == MouseButtons.Left)
            {
                TreemapBlock block = HitTestBlock(e.Location);
                Select(block != null ? block.Node : null);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            TreemapBlock block = HitTestBlock(e.Location);
            FileNode node = block != null ? block.Node : null;
            if (ReferenceEquals(node, _hoverNode)) return;

            FileNode previous = _hoverNode;
            _hoverNode = node;

            InvalidateBlock(previous);
            InvalidateBlock(node);

            Raise(HoverChanged, new TreemapNodeEventArgs(node));
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverNode == null) return;

            FileNode previous = _hoverNode;
            _hoverNode = null;
            InvalidateBlock(previous);
            Raise(HoverChanged, new TreemapNodeEventArgs(null));
        }

        private TreemapBlock HitTestBlock(Point point)
        {
            EnsureLayout();
            return _cache.HitTest(point);
        }

        private void Raise(EventHandler handler, EventArgs args)
        {
            if (handler != null) handler(this, args);
        }

        private void Raise(EventHandler<TreemapNodeEventArgs> handler, TreemapNodeEventArgs args)
        {
            if (handler != null) handler(this, args);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_labelFont != null)
                {
                    _labelFont.Dispose();
                    _labelFont = null;
                }
                ClearBrushCache();
                foreach (Pen pen in _borderCache.Values) pen.Dispose();
                _borderCache.Clear();
            }
            base.Dispose(disposing);
        }
    }
}
