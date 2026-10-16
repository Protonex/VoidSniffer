using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using VoidSniffer.Everything;
using VoidSniffer.Model;
using VoidSniffer.Services;
using VoidSniffer.Treemap;

namespace VoidSniffer
{
    public partial class MainForm : Form
    {
        private readonly EverythingDataProvider _provider = new EverythingDataProvider();
        private readonly NavigationController _navigation = new NavigationController();
        private readonly FilterService _filterService = new FilterService();
        private readonly DiskUsageInfo _diskUsage = new DiskUsageInfo();

        private CancellationTokenSource _cts;
        private FileNode _fullRoot;
        private ProgressiveTreeLoader _loader;
        private bool _expanding;
        private CancellationTokenSource _enrichCts;
        private CancellationTokenSource _autoExpandCts;
        private string _autoExpandRootPath;
        private int _autoExpandDepth = -1;
        private readonly string _initialPath;
        private int _recursionDepth = 4;
        private bool _showFreeSpace;
        private readonly List<FileNode> _syntheticNodes = new List<FileNode>();
        private readonly Dictionary<string, Tuple<long, long>> _countCache =
            new Dictionary<string, Tuple<long, long>>(StringComparer.OrdinalIgnoreCase);

        public MainForm()
            : this(null)
        {
        }

        public MainForm(string initialPath)
        {
            _initialPath = initialPath;
            InitializeComponent();
            WireEvents();
            treemap.MaxDepth = _recursionDepth;
            UpdateDepthLabel();
        }

        private void WireEvents()
        {
            Load += MainForm_Load;
            FormClosing += MainForm_FormClosing;
            KeyPreview = true;
            KeyDown += MainForm_KeyDown;

            scanButton.Click += ScanButton_Click;
            openButton.Click += (s, e) => OpenButton_Click();
            pathTextBox.KeyDown += PathTextBox_KeyDown;

            backButton.Click += (s, e) => _navigation.GoBack();
            forwardButton.Click += (s, e) => _navigation.GoForward();
            upButton.Click += (s, e) => _navigation.ZoomUp();
            homeButton.Click += (s, e) => _navigation.GoHome();

            depthDownButton.Click += (s, e) => AdjustDepth(-1);
            depthUpButton.Click += (s, e) => AdjustDepth(1);
            depthDownButton.ToolTipText = "Меньше уровней вложенности";
            depthUpButton.ToolTipText = "Больше уровней вложенности";
            depthLabel.ToolTipText = "Уровней вложенности от текущей папки";
            freeSpaceButton.Click += (s, e) => ToggleFreeSpace();
            freeSpaceButton.ToolTipText = "Показывать свободное место тома отдельным тайлом";

            applyFilterButton.Click += (s, e) => ApplyFilter();
            resetFilterButton.Click += (s, e) => ResetFilter();
            nameFilterTextBox.KeyDown += FilterTextBox_KeyDown;
            minSizeTextBox.KeyDown += FilterTextBox_KeyDown;
            maxSizeTextBox.KeyDown += FilterTextBox_KeyDown;

            _navigation.FocusChanged += Navigation_FocusChanged;

            treemap.BlockActivated += Treemap_BlockActivated;
            treemap.ZoomOutRequested += (s, e) => _navigation.ZoomUp();
            treemap.HoverChanged += Treemap_HoverChanged;
            treemap.SelectionChanged += Treemap_SelectionChanged;
            treemap.ContextMenuRequested += Treemap_ContextMenuRequested;
        }

        private void OpenButton_Click()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Выберите папку для сканирования";
                dialog.ShowNewFolderButton = false;
                dialog.SelectedPath = Directory.Exists(pathTextBox.Text) ? pathTextBox.Text : "C:\\";

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    pathTextBox.Text = dialog.SelectedPath;
                    _ = ScanAsync();
                }
            }
        }

        private void UpdateDepthLabel()
        {
            depthLabel.Text = "Глубина: " + _recursionDepth;
        }

        private void AdjustDepth(int delta)
        {
            int next = _recursionDepth + delta;
            if (next < 1) next = 1;
            if (next > 8) next = 8;
            if (next == _recursionDepth) return;

            _recursionDepth = next;
            UpdateDepthLabel();
            treemap.MaxDepth = _recursionDepth;

            FileNode focus = _navigation.Focus;
            FileNode canonical = focus != null && _fullRoot != null ? FindByPath(_fullRoot, focus.FullPath) : null;
            if (canonical != null) StartAutoExpansion(canonical);
            RefreshTreemapAfterExpansion();
        }

        private void ToggleFreeSpace()
        {
            _showFreeSpace = freeSpaceButton.Checked;
            UpdateFreeSpaceTile();
            treemap.RefreshLayout();
        }

        /// <summary>
        /// Adds or removes a synthetic "free space" tile for the current view's volume.
        /// The tile is attached to the focused node in the displayed view.
        /// </summary>
        private void UpdateFreeSpaceTile()
        {
            for (int i = 0; i < _syntheticNodes.Count; i++)
            {
                FileNode node = _syntheticNodes[i];
                if (node.Parent != null) node.Parent.Children.Remove(node);
            }
            _syntheticNodes.Clear();

            FileNode focus = _navigation.Focus;
            if (!_showFreeSpace || focus == null || !focus.IsFolder) return;

            long free;
            if (!DiskUsageInfo.TryGetFreeSpace(focus.FullPath, out free) || free <= 0) return;

            var freeNode = new FileNode
            {
                Name = "Свободно",
                FullPath = focus.FullPath + "\\<свободно>",
                IsFolder = false,
                IsSynthetic = true,
                OwnSize = free,
                TotalSize = free,
                Parent = focus,
                Depth = focus.Depth + 1
            };
            focus.Children.Add(freeNode);
            _syntheticNodes.Add(freeNode);
        }

        private void Treemap_ContextMenuRequested(object sender, TreemapContextMenuEventArgs e)
        {
            if (e.Node == null || string.IsNullOrEmpty(e.Node.FullPath)) return;

            treemap.Select(e.Node);
            ShellContextMenu.Show(Handle, e.Node.FullPath, e.ScreenLocation.X, e.ScreenLocation.Y);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            if (!_provider.IsAvailable())
            {
                SetStatus("Everything не запущен — запустите его и повторите.");
                MessageBox.Show(
                    this,
                    "Не удалось найти окно Everything IPC.\nЗапустите Everything (voidtools) и попробуйте снова.",
                    "Everything недоступен",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            SetStatus("Everything " + _provider.GetVersion().ToString(3) + " — готов. Укажите путь и нажмите «Сканировать».");

            if (!string.IsNullOrWhiteSpace(_initialPath))
            {
                pathTextBox.Text = _initialPath;
                _ = ScanAsync();
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            CancelPending();
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Alt && e.KeyCode == Keys.Left)
            {
                _navigation.GoBack();
                e.Handled = true;
            }
            else if (e.Alt && e.KeyCode == Keys.Right)
            {
                _navigation.GoForward();
                e.Handled = true;
            }
            else if (e.Alt && e.KeyCode == Keys.Up)
            {
                _navigation.ZoomUp();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Back)
            {
                _navigation.ZoomUp();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F5)
            {
                e.Handled = true;
                _ = ScanAsync();
            }
        }

        private void PathTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                _ = ScanAsync();
            }
        }

        private void FilterTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                ApplyFilter();
            }
        }

        private async void ScanButton_Click(object sender, EventArgs e)
        {
            await ScanAsync();
        }

        private async Task ScanAsync()
        {
            string path = (pathTextBox.Text ?? string.Empty).Trim();
            if (path.Length == 0)
            {
                SetStatus("Укажите путь к папке.");
                return;
            }

            string normalized = PathUtil.Normalize(path);
            if (!Directory.Exists(normalized))
            {
                MessageBox.Show(this, "Папка не найдена:\n" + normalized, "Неверный путь",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            CancelPending();
            var cts = new CancellationTokenSource();
            _cts = cts;

            SetBusy(true, "Запрос к индексу Everything…");

            // One cheap query loads the root's direct children; the index already knows
            // the exact recursive size of every folder, so the treemap is complete and
            // correctly sized immediately. Deeper levels load on demand when drilled into.
            var loader = new ProgressiveTreeLoader(_provider);
            _loader = loader;
            _expanding = false;

            try
            {
                await loader.LoadRootAsync(normalized, cts.Token);

                _fullRoot = loader.Root;
                _navigation.SetRoot(_fullRoot);
                RefreshView();

                string suffix = loader.TruncatedFolderCount > 0
                    ? string.Format(" (усечено папок: {0:N0})", loader.TruncatedFolderCount)
                    : string.Empty;
                SetStatus(string.Format("{0} — узлов: {1:N0}, объём: {2}{3}",
                    normalized, loader.NodeCount, FormatSize(loader.Root.TotalSize), suffix));

                StartAutoExpansion(_fullRoot);
            }
            catch (OperationCanceledException)
            {
                SetStatus("Операция отменена.");
            }
            catch (EverythingNotAvailableException ex)
            {
                SetStatus("Everything недоступен.");
                MessageBox.Show(this, ex.Message, "Everything недоступен",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                SetStatus("Ошибка: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (ReferenceEquals(_cts, cts))
                {
                    _cts = null;
                    SetBusy(false, null);
                }
                cts.Dispose();
            }
        }

        /// <summary>Applies the current filter criteria to the loaded tree and re-anchors navigation.</summary>
        private void ApplyFilter()
        {
            if (_fullRoot == null) return;

            int nodes = RefreshView();
            FilterCriteria criteria = BuildCriteria();
            string note = criteria.HasActiveFilters ? "  |  фильтр: " + DescribeCriteria(criteria) : string.Empty;
            SetStatus(string.Format("{0} — узлов: {1:N0}, объём: {2}{3}",
                _navigation.Root.FullPath, nodes, FormatSize(_navigation.Root.TotalSize), note));
        }

        /// <summary>
        /// Recomputes the filtered view over the current tree, restores the previously
        /// focused folder when possible and repaints. Navigation history is preserved.
        /// Safe to call repeatably (e.g. after a lazy folder expansion).
        /// </summary>
        private int RefreshView()
        {
            FilterCriteria criteria = BuildCriteria();
            FileNode view = _filterService.Apply(_fullRoot, criteria);
            _navigation.RebuildRoot(view);
            return FilterService.CountNodes(_navigation.Root);
        }

        private void ResetFilter()
        {
            nameFilterTextBox.Text = string.Empty;
            minSizeTextBox.Text = string.Empty;
            maxSizeTextBox.Text = string.Empty;
            ApplyFilter();
        }

        private FilterCriteria BuildCriteria()
        {
            var criteria = new FilterCriteria();
            criteria.NameQuery = (nameFilterTextBox.Text ?? string.Empty).Trim();
            criteria.MinSize = ParseMegabytes(minSizeTextBox.Text);
            criteria.MaxSize = ParseMegabytes(maxSizeTextBox.Text);
            return criteria;
        }

        private static long? ParseMegabytes(string text)
        {
            text = (text ?? string.Empty).Trim();
            if (text.Length == 0) return null;

            double megabytes;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out megabytes) &&
                !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out megabytes))
            {
                return null;
            }
            if (megabytes <= 0) return null;
            return (long)(megabytes * 1024 * 1024);
        }

        private static string DescribeCriteria(FilterCriteria criteria)
        {
            var parts = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(criteria.NameQuery))
            {
                parts.Append("имя~'").Append(criteria.NameQuery).Append('\'');
            }
            if (criteria.MinSize.HasValue)
            {
                if (parts.Length > 0) parts.Append(", ");
                parts.Append('≥').Append(FormatSize(criteria.MinSize.Value));
            }
            if (criteria.MaxSize.HasValue)
            {
                if (parts.Length > 0) parts.Append(", ");
                parts.Append('≤').Append(FormatSize(criteria.MaxSize.Value));
            }
            return parts.Length == 0 ? "нет" : parts.ToString();
        }

        private static FileNode FindByPath(FileNode root, string fullPath)
        {
            if (root == null || string.IsNullOrEmpty(fullPath)) return null;
            if (PathUtil.EqualsPath(root.FullPath, fullPath)) return root;

            for (int i = 0; i < root.Children.Count; i++)
            {
                FileNode found = FindByPath(root.Children[i], fullPath);
                if (found != null) return found;
            }
            return null;
        }

        private void Navigation_FocusChanged(object sender, EventArgs e)
        {
            treemap.SetContent(_navigation.Focus);
            UpdateNavigation();
            UpdateFreeSpaceTile();
            EnsureFocusExpanded(_navigation.Focus);
            StartCountEnrichment();
        }

        /// <summary>
        /// Expands the largest folders under <paramref name="root"/> in the background,
        /// breadth-first and depth-limited, so subfolders render recursively from the
        /// current level downwards. Bounded by a time budget and the folder size threshold.
        /// A run already in progress for the same root and depth is left alone; a different
        /// root or a changed depth cancels and restarts it.
        /// </summary>
        private void StartAutoExpansion(FileNode root)
        {
            if (root == null || _loader == null) return;

            string path = root.FullPath;

            if (_autoExpandCts != null)
            {
                if (string.Equals(_autoExpandRootPath, path, StringComparison.OrdinalIgnoreCase) &&
                    _autoExpandDepth == _recursionDepth)
                {
                    return; // already expanding this subtree at this depth
                }

                try { _autoExpandCts.Cancel(); }
                catch (ObjectDisposedException) { }
                _autoExpandCts = null;
            }

            var cts = new CancellationTokenSource();
            _autoExpandCts = cts;
            _autoExpandRootPath = path;
            _autoExpandDepth = _recursionDepth;
            _ = AutoExpandAsync(root, cts);
        }

        private async Task AutoExpandAsync(FileNode root, CancellationTokenSource cts)
        {
            CancellationToken token = cts.Token;
            long threshold = AutoExpansionPolicy.SizeThreshold(root.TotalSize);

            // Depth limit is relative to the current level, so drilling in keeps nesting.
            int maxDepth = root.Depth + _recursionDepth;
            const int budget = 300;

            var pending = new List<FileNode>();
            for (int i = 0; i < root.Children.Count; i++)
            {
                FileNode child = root.Children[i];
                if (AutoExpansionPolicy.IsEligible(child, threshold, maxDepth)) pending.Add(child);
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            int expanded = 0;
            int visited = 0;
            long lastRefreshMs = 0;
            const int visitBudget = 20000;

            while (pending.Count > 0 && expanded < budget && visited < visitBudget &&
                   stopwatch.ElapsedMilliseconds < 12000)
            {
                if (token.IsCancellationRequested || !ReferenceEquals(_autoExpandCts, cts)) break;

                int index = AutoExpansionPolicy.IndexOfLargest(pending);
                if (index < 0) break;

                FileNode folder = pending[index];
                pending.RemoveAt(index);
                visited++;

                if (!_loader.IsExpanded(folder))
                {
                    try
                    {
                        await _loader.ExpandAsync(folder, token);
                        expanded++;
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception)
                    {
                        // Ignore this folder but keep descending through the rest.
                    }

                    // The cached layout must be discarded so the new nested blocks show
                    // up; throttle the full rebuild so a long run stays responsive.
                    if (stopwatch.ElapsedMilliseconds - lastRefreshMs >= 200)
                    {
                        lastRefreshMs = stopwatch.ElapsedMilliseconds;
                        RefreshTreemapAfterExpansion();
                    }
                }

                // Descend even through folders that were already expanded, otherwise a
                // depth increase would never reach the deeper frontier.
                for (int i = 0; i < folder.Children.Count; i++)
                {
                    FileNode child = folder.Children[i];
                    if (AutoExpansionPolicy.IsEligible(child, threshold, maxDepth)) pending.Add(child);
                }
            }

            RefreshTreemapAfterExpansion();

            if (ReferenceEquals(_autoExpandCts, cts))
            {
                _autoExpandCts = null;
                _autoExpandRootPath = null;
                _autoExpandDepth = -1;
            }
        }

        private void RefreshTreemapAfterExpansion()
        {
            if (ReferenceEquals(_navigation.Root, _fullRoot))
            {
                treemap.RefreshLayout();
            }
            else
            {
                RefreshView();
            }
        }

        /// <summary>
        /// Applies cached recursive file/folder counts to the current view so labels show
        /// them without re-querying.
        /// </summary>
        private void ApplyCachedCounts(FileNode node)
        {
            if (node == null) return;

            Tuple<long, long> counts;
            if (node.IsFolder && _countCache.TryGetValue(node.FullPath, out counts))
            {
                node.FileCount = counts.Item1;
                node.FolderCount = counts.Item2;
            }

            for (int i = 0; i < node.Children.Count; i++)
            {
                ApplyCachedCounts(node.Children[i]);
            }
        }

        /// <summary>
        /// Resolves the recursive file/folder counts for the folders currently visible as
        /// tiles, one query pair at a time, updating the labels as results arrive. Results
        /// are cached by path, and a new view cancels any in-flight enrichment.
        /// </summary>
        private void StartCountEnrichment()
        {
            if (_enrichCts != null)
            {
                try { _enrichCts.Cancel(); }
                catch (ObjectDisposedException) { }
                _enrichCts = null;
            }

            if (_fullRoot == null || _navigation.Root == null) return;

            ApplyCachedCounts(_navigation.Root);

            var pending = new List<FileNode>();
            IList<FileNode> labelNodes = treemap.GetLabelNodes();
            for (int i = 0; i < labelNodes.Count; i++)
            {
                FileNode node = labelNodes[i];
                if (node.IsFolder && !node.HasCounts && !_countCache.ContainsKey(node.FullPath))
                {
                    pending.Add(node);
                }
            }
            if (pending.Count == 0) return;

            var cts = new CancellationTokenSource();
            _enrichCts = cts;
            _ = EnrichCountsAsync(pending, cts);
        }

        private async Task EnrichCountsAsync(List<FileNode> nodes, CancellationTokenSource cts)
        {
            CancellationToken token = cts.Token;
            bool repainted = false;

            try
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (token.IsCancellationRequested) break;

                    FileNode node = nodes[i];
                    Tuple<long, long> counts;
                    try
                    {
                        counts = await _provider.GetCountsAsync(node.FullPath, token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    _countCache[node.FullPath] = counts;
                    node.FileCount = counts.Item1;
                    node.FolderCount = counts.Item2;
                    node.OnDiskSize = DiskUsage.EstimatedFolderAllocated(
                        node.TotalSize, counts.Item1, _diskUsage.GetClusterSize(node.FullPath));

                    FileNode canonical = FindByPath(_fullRoot, node.FullPath);
                    if (canonical != null && !ReferenceEquals(canonical, node))
                    {
                        canonical.FileCount = counts.Item1;
                        canonical.FolderCount = counts.Item2;
                        canonical.OnDiskSize = node.OnDiskSize;
                    }

                    repainted = true;
                    treemap.Invalidate();
                }
            }
            finally
            {
                if (repainted)
                {
                    treemap.Invalidate();
                }
            }
        }

        /// <summary>
        /// Lazily loads the children of the focused folder when they have not been loaded
        /// yet, then refreshes the view. This is what makes drilling into a folder cost a
        /// single index query instead of pre-loading the whole subtree.
        /// </summary>
        private void EnsureFocusExpanded(FileNode focus)
        {
            if (_loader == null || focus == null || !focus.IsFolder) return;

            FileNode canonical = FindByPath(_fullRoot, focus.FullPath);
            if (canonical == null) return;

            if (_loader.IsExpanded(canonical))
            {
                // Already loaded: just nest deeper from this level.
                StartAutoExpansion(canonical);
                return;
            }

            _ = ExpandFolderAsync(canonical);
        }

        private async Task ExpandFolderAsync(FileNode folder)
        {
            if (_expanding || _loader == null) return;

            _expanding = true;
            SetStatus("Запрос к индексу Everything…");
            try
            {
                CancellationToken token = _cts != null ? _cts.Token : CancellationToken.None;
                if (await _loader.ExpandAsync(folder, token))
                {
                    RefreshTreemapAfterExpansion();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (EverythingNotAvailableException ex)
            {
                SetStatus("Everything недоступен.");
                MessageBox.Show(this, ex.Message, "Everything недоступен",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                SetStatus("Ошибка: " + ex.Message);
            }
            finally
            {
                _expanding = false;
            }

            // Nest deeper from the folder the user just entered.
            StartAutoExpansion(folder);
        }

        private void Treemap_BlockActivated(object sender, TreemapNodeEventArgs e)
        {
            if (e.Node == null || e.Node.IsSynthetic) return;
            if (e.Node.IsFolder)
            {
                _navigation.NavigateTo(e.Node);
            }
            else if (e.Node.Parent != null)
            {
                _navigation.NavigateTo(e.Node.Parent);
            }
        }

        private void CancelPending()
        {
            if (_autoExpandCts != null)
            {
                try { _autoExpandCts.Cancel(); }
                catch (ObjectDisposedException) { }
                _autoExpandCts = null;
                _autoExpandRootPath = null;
            }

            if (_enrichCts != null)
            {
                try { _enrichCts.Cancel(); }
                catch (ObjectDisposedException) { }
                _enrichCts = null;
            }

            if (_cts != null)
            {
                try { _cts.Cancel(); }
                catch (ObjectDisposedException) { }
                _cts = null;
            }
        }

        private void SetBusy(bool busy, string statusText)
        {
            progressBar.Visible = busy;
            scanButton.Enabled = !busy;
            pathTextBox.Enabled = !busy;
            if (statusText != null)
            {
                SetStatus(statusText);
            }
        }

        private void UpdateNavigation()
        {
            backButton.Enabled = _navigation.CanGoBack;
            forwardButton.Enabled = _navigation.CanGoForward;
            upButton.Enabled = _navigation.CanZoomUp;
            homeButton.Enabled = _navigation.Root != null && !_navigation.IsAtRoot;
        }

        private void Treemap_HoverChanged(object sender, TreemapNodeEventArgs e)
        {
            if (e.Node == null) return;

            string kind = e.Node.IsFolder ? "Папка" : "Файл";
            string modified = e.Node.ModifiedTime.HasValue
                ? "   |   " + e.Node.ModifiedTime.Value.ToString("yyyy-MM-dd HH:mm")
                : string.Empty;
            SetStatus(string.Format("{0}: {1}   |   {2}{3}",
                kind, e.Node.FullPath, FormatSize(e.Node.TotalSize), modified));
        }

        private void Treemap_SelectionChanged(object sender, TreemapNodeEventArgs e)
        {
            if (e.Node == null) return;
            SetStatus(string.Format("{0}   |   {1}", e.Node.FullPath, FormatSize(e.Node.TotalSize)));
        }

        private void SetStatus(string text)
        {
            statusLabel.Text = text;
        }

        internal static string FormatSize(long bytes)
        {
            if (bytes < 0) return "0 B";
            string[] units = { "B", "KB", "MB", "GB", "TB", "PB" };
            double value = bytes;
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }
            return unit == 0
                ? string.Format("{0} {1}", bytes, units[unit])
                : string.Format("{0:0.##} {1}", value, units[unit]);
        }
    }
}
