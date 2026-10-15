using System;
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

        private CancellationTokenSource _cts;
        private FileNode _fullRoot;
        private ProgressiveTreeLoader _loader;
        private bool _expanding;

        public MainForm()
        {
            InitializeComponent();
            WireEvents();
        }

        private void WireEvents()
        {
            Load += MainForm_Load;
            FormClosing += MainForm_FormClosing;
            KeyPreview = true;
            KeyDown += MainForm_KeyDown;

            scanButton.Click += ScanButton_Click;
            pathTextBox.KeyDown += PathTextBox_KeyDown;

            backButton.Click += (s, e) => _navigation.GoBack();
            forwardButton.Click += (s, e) => _navigation.GoForward();
            upButton.Click += (s, e) => _navigation.ZoomUp();
            homeButton.Click += (s, e) => _navigation.GoHome();

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
            EnsureFocusExpanded(_navigation.Focus);
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
            if (canonical == null || _loader.IsExpanded(canonical)) return;

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
                    RefreshView();
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
        }

        private void Treemap_BlockActivated(object sender, TreemapNodeEventArgs e)
        {
            if (e.Node == null) return;
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
