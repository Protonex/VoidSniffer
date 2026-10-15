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

    /// <summary>Request to show a context menu for a node at a screen location.</summary>
    public sealed class TreemapContextMenuEventArgs : EventArgs
    {
        public TreemapContextMenuEventArgs(FileNode node, Point screenLocation)
        {
            Node = node;
            ScreenLocation = screenLocation;
        }

        public FileNode Node { get; private set; }

        public Point ScreenLocation { get; private set; }
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
        private readonly Timer _hoverTimer = new Timer();
        private readonly Timer _rightPressTimer = new Timer();
        private Pen _borderPen;

        private Font _labelFont;
        private Font _detailFont;
        private Font _tipTitleFont;
        private FileNode _content;
        private FileNode _hoverNode;
        private FileNode _toolTipNode;
        private FileNode _selected;
        private DateTime _hoverStartedUtc;
        private Point _rightPressPoint;
        private FileNode _rightPressNode;
        private bool _longPressTriggered;

        private const int MaxLabels = 4000;
        private const int HighlightMargin = 3;
        private const int HoverAnimationMs = 380;
        private const int LongPressMs = 550;
        private const int LongPressMoveTolerance = 4;

        /// <summary>A block was double-clicked (drill in).</summary>
        public event EventHandler<TreemapNodeEventArgs> BlockActivated;

        /// <summary>Zoom-out was requested (right click / back gesture).</summary>
        public event EventHandler ZoomOutRequested;

        public event EventHandler<TreemapNodeEventArgs> HoverChanged;
        public event EventHandler<TreemapNodeEventArgs> SelectionChanged;

        /// <summary>A long right-button press requests the context menu for a node.</summary>
        public event EventHandler<TreemapContextMenuEventArgs> ContextMenuRequested;

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

            _hoverTimer.Interval = 15;
            _hoverTimer.Tick += (s, e) => OnHoverTick();

            _rightPressTimer.Interval = LongPressMs;
            _rightPressTimer.Tick += (s, e) => OnRightPressTick();

            _tipTitleFont = new Font("Segoe UI", 8f, FontStyle.Bold, GraphicsUnit.Point);
            _toolTip.OwnerDraw = true;
            _toolTip.Popup += OnToolTipPopup;
            _toolTip.Draw += OnToolTipDraw;

            // Keep the tooltip visible for a long time so it does not vanish while reading.
            _toolTip.AutoPopDelay = 30000;
            _toolTip.InitialDelay = 300;
            _toolTip.ReshowDelay = 100;
            _toolTip.ShowAlways = true;
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
            _hoverTimer.Stop();
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

            // Pass 2: the animated hover lightening, painted over the whole tile (and its
            // children) before any text so the labels stay crisp and readable.
            DrawHover(g, clip);

            // Pass 3: labels on top, so an inner container's label is never painted over
            // by an adjacent outer tile and never washed out by the hover overlay.
            int labelBudget = MaxLabels;
            for (int i = 0; i < blocks.Count && labelBudget > 0; i++)
            {
                labelBudget -= DrawLabel(g, clip, blocks[i]);
            }

            DrawHighlight(g, clip, _cache.Find(_selected), Color.FromArgb(255, 255, 255), 2);
        }

        private void DrawHover(Graphics g, Rectangle clip)
        {
            if (_hoverNode == null) return;

            TreemapBlock block = _cache.Find(_hoverNode);
            if (block == null) return;

            Rectangle rect = TreemapLayoutCache.ToClientRect(block.Bounds);
            if (rect.Width < 2 || rect.Height < 2) return;
            if (!rect.IntersectsWith(clip)) return;

            int alpha = CurrentHoverAlpha();
            using (var brush = new SolidBrush(Color.FromArgb(alpha, TreemapColorizer.HoverOverlay)))
            {
                g.FillRectangle(brush, rect);
            }
        }

        private int CurrentHoverAlpha()
        {
            double elapsed = (DateTime.UtcNow - _hoverStartedUtc).TotalMilliseconds;
            double progress = elapsed / HoverAnimationMs;
            return TreemapColorizer.HoverAlpha(progress);
        }

        private void OnHoverTick()
        {
            if (_hoverNode == null)
            {
                _hoverTimer.Stop();
                return;
            }

            InvalidateBlock(_hoverNode);

            if ((DateTime.UtcNow - _hoverStartedUtc).TotalMilliseconds >= HoverAnimationMs)
            {
                _hoverTimer.Stop();
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
            _hoverTimer.Stop();
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
                // A short right-click zooms out (on release); a long press opens the
                // context menu.
                TreemapBlock block = HitTestBlock(e.Location);
                _rightPressNode = block != null ? block.Node : null;
                _rightPressPoint = e.Location;
                _longPressTriggered = false;
                if (_rightPressNode != null) _rightPressTimer.Start();
                return;
            }
            if (e.Button == MouseButtons.Left)
            {
                TreemapBlock block = HitTestBlock(e.Location);
                Select(block != null ? block.Node : null);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.Button != MouseButtons.Right) return;

            if (_rightPressTimer.Enabled) _rightPressTimer.Stop();

            if (!_longPressTriggered)
            {
                Raise(ZoomOutRequested, EventArgs.Empty);
            }

            _longPressTriggered = false;
            _rightPressNode = null;
        }

        private void OnRightPressTick()
        {
            _rightPressTimer.Stop();
            if (_rightPressNode == null) return;

            _longPressTriggered = true;
            Point screen = PointToScreen(_rightPressPoint);
            Raise(ContextMenuRequested, new TreemapContextMenuEventArgs(_rightPressNode, screen));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // Moving while the right button is held means it is a drag, not a long press.
            if (_rightPressTimer.Enabled &&
                (Math.Abs(e.X - _rightPressPoint.X) > LongPressMoveTolerance ||
                 Math.Abs(e.Y - _rightPressPoint.Y) > LongPressMoveTolerance))
            {
                _rightPressTimer.Stop();
            }

            TreemapBlock block = HitTestBlock(e.Location);
            FileNode node = block != null ? block.Node : null;
            if (ReferenceEquals(node, _hoverNode)) return;

            FileNode previous = _hoverNode;
            _hoverNode = node;

            if (node != null)
            {
                // Restart the flash-and-fade for the newly hovered tile.
                _hoverStartedUtc = DateTime.UtcNow;
                if (!_hoverTimer.Enabled) _hoverTimer.Start();
            }
            else
            {
                _hoverTimer.Stop();
            }

            InvalidateBlock(previous);
            InvalidateBlock(node);

            UpdateToolTip(node);
            Raise(HoverChanged, new TreemapNodeEventArgs(node));
        }

        private void UpdateToolTip(FileNode node)
        {
            _toolTipNode = node;
            // A non-empty text is required for the popup to appear; the Draw handler
            // renders the actual content.
            _toolTip.SetToolTip(this, node == null ? null : " ");
        }

        private void OnToolTipPopup(object sender, PopupEventArgs e)
        {
            if (_toolTipNode == null)
            {
                e.Cancel = true;
                return;
            }

            e.ToolTipSize = MeasureTooltip(_toolTipNode);
        }

        private Size MeasureTooltip(FileNode node)
        {
            int width = TextRenderer.MeasureText(NodeLabelFormatter.TooltipTitle(node), _tipTitleFont).Width;
            int height = _tipTitleFont.Height + 3;

            System.Collections.Generic.IList<string> lines = NodeLabelFormatter.TooltipLines(node);
            for (int i = 0; i < lines.Count; i++)
            {
                int lineWidth = TextRenderer.MeasureText(lines[i], _labelFont).Width;
                if (lineWidth > width) width = lineWidth;
                height += _labelFont.Height;
            }

            return new Size(width + 14, height + 10);
        }

        private void OnToolTipDraw(object sender, DrawToolTipEventArgs e)
        {
            FileNode node = _toolTipNode;
            if (node == null) return;

            Graphics g = e.Graphics;
            Rectangle bounds = e.Bounds;

            using (var background = new SolidBrush(Color.FromArgb(255, 255, 225)))
            {
                g.FillRectangle(background, bounds);
            }
            using (var border = new Pen(Color.FromArgb(150, 140, 110)))
            {
                g.DrawRectangle(border, 0, 0, bounds.Width - 1, bounds.Height - 1);
            }

            int x = 7;
            int y = 5;
            TextRenderer.DrawText(g, NodeLabelFormatter.TooltipTitle(node), _tipTitleFont,
                new Point(x, y), Color.FromArgb(20, 20, 20));
            y += _tipTitleFont.Height + 3;

            System.Collections.Generic.IList<string> lines = NodeLabelFormatter.TooltipLines(node);
            for (int i = 0; i < lines.Count; i++)
            {
                TextRenderer.DrawText(g, lines[i], _labelFont, new Point(x, y), Color.FromArgb(45, 40, 30));
                y += _labelFont.Height;
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _rightPressTimer.Stop();
            if (_hoverNode == null) return;

            FileNode previous = _hoverNode;
            _hoverNode = null;
            _hoverTimer.Stop();
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

        private void Raise(EventHandler<TreemapContextMenuEventArgs> handler, TreemapContextMenuEventArgs args)
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
                if (_tipTitleFont != null)
                {
                    _tipTitleFont.Dispose();
                    _tipTitleFont = null;
                }
                if (_borderPen != null)
                {
                    _borderPen.Dispose();
                    _borderPen = null;
                }
                _hoverTimer.Dispose();
                _rightPressTimer.Dispose();
                _toolTip.Dispose();
                ClearBrushCache();
            }
            base.Dispose(disposing);
        }
    }
}
