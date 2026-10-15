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
        private readonly ToolTip _toolTip = new ToolTip();
        private Pen _borderPen;

        private Font _labelFont;
        private Font _detailFont;
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

            BackColor = TreemapColorizer.Background;
            _labelFont = new Font("Segoe UI", 7.5f, FontStyle.Regular, GraphicsUnit.Point);
            _detailFont = new Font("Segoe UI", 6.5f, FontStyle.Regular, GraphicsUnit.Point);
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

        /// <summary>Resolves the fill color of a block (folder palette or file/extension tint).</summary>
        private static Color GetFill(TreemapBlock block)
        {
            return block.Node.IsFolder
                ? TreemapColorizer.GetFill(block.ColorIndex, block.Depth)
                : TreemapColorizer.GetFileFill(block.Node);
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

            // Pass 1: fills and 1px borders for every visible block.
            for (int i = 0; i < blocks.Count; i++)
            {
                TreemapBlock block = blocks[i];
                Rectangle rect = TreemapLayoutCache.ToClientRect(block.Bounds);
                if (rect.Width < 1 || rect.Height < 1) continue;
                if (!rect.IntersectsWith(clip)) continue;

                g.FillRectangle(GetBrush(GetFill(block)), rect);
                g.DrawRectangle(GetBorderPen(), rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
            }

            DrawHighlight(g, clip, _cache.Find(_hoverNode), Color.FromArgb(120, 90, 60, 20), 1);
            DrawHighlight(g, clip, _cache.Find(_selected), Color.FromArgb(255, 255, 255), 2);

            // Pass 2: labels on top, so an inner container's label is never painted over
            // by an adjacent outer tile.
            int labelBudget = MaxLabels;
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

            Color text = TreemapColorizer.GetTextColor(GetFill(block));
            Color subText = Color.FromArgb(200, text.R, text.G, text.B);

            int nameH = _labelFont.Height + 1;
            int detailH = _detailFont.Height + 1;
            int inset = 3;

            if (block.Node.IsFolder)
            {
                // Name top-left, then details centered in the remaining width.
                var nameRect = new Rectangle(rect.X + inset, rect.Y + 1, rect.Width - (inset * 2), nameH);
                DrawLabelLine(g, _labelFont, NodeLabelFormatter.DescribeName(block.Node), nameRect, text,
                    TextFormatFlags.Left);

                if (rect.Width >= 60 && rect.Height >= nameH + detailH + 3)
                {
                    var detailRect = new Rectangle(rect.X + inset, rect.Y + 1 + nameH,
                        rect.Width - (inset * 2), detailH);
                    DrawLabelLine(g, _detailFont, NodeLabelFormatter.DescribeDetails(block.Node), detailRect, subText,
                        TextFormatFlags.HorizontalCenter);
                }
            }
            else
            {
                // Files: name centered, details centered below.
                var nameRect = new Rectangle(rect.X + inset, rect.Y + 1, rect.Width - (inset * 2), nameH);
                DrawLabelLine(g, _labelFont, NodeLabelFormatter.DescribeName(block.Node), nameRect, text,
                    TextFormatFlags.HorizontalCenter);

                if (rect.Width >= 60 && rect.Height >= nameH + detailH + 3)
                {
                    var detailRect = new Rectangle(rect.X + inset, rect.Y + 1 + nameH,
                        rect.Width - (inset * 2), detailH);
                    DrawLabelLine(g, _detailFont, NodeLabelFormatter.DescribeDetails(block.Node), detailRect, subText,
                        TextFormatFlags.HorizontalCenter);
                }
            }

            return 1;
        }

        private static void DrawLabelLine(Graphics g, Font font, string text, Rectangle rect, Color color, TextFormatFlags alignment)
        {
            if (string.IsNullOrEmpty(text) || rect.Width <= 0) return;

            TextRenderer.DrawText(
                g,
                text,
                font,
                rect,
                color,
                alignment | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
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

        private Pen GetBorderPen()
        {
            Pen pen = _borderPen;
            if (pen == null)
            {
                pen = new Pen(TreemapColorizer.Border, 1f);
                _borderPen = pen;
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

            UpdateToolTip(node);
            Raise(HoverChanged, new TreemapNodeEventArgs(node));
        }

        private void UpdateToolTip(FileNode node)
        {
            if (node == null)
            {
                _toolTip.SetToolTip(this, null);
                return;
            }

            _toolTip.SetToolTip(this, node.FullPath + Environment.NewLine + NodeLabelFormatter.DescribeDetails(node));
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
                if (_detailFont != null)
                {
                    _detailFont.Dispose();
                    _detailFont = null;
                }
                if (_borderPen != null)
                {
                    _borderPen.Dispose();
                    _borderPen = null;
                }
                _toolTip.Dispose();
                ClearBrushCache();
            }
            base.Dispose(disposing);
        }
    }
}
