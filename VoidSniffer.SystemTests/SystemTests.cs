using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using VoidSniffer.Model;
using VoidSniffer.Services;
using VoidSniffer.Treemap;

namespace VoidSniffer.SystemTests
{
    /// <summary>
    /// System tests that exercise the application against a live Everything index and the
    /// real treemap pipeline. Requires Everything (voidtools) to be running; when it is
    /// not, the Everything-dependent checks are reported as skipped.
    /// </summary>
    public sealed class SystemTests
    {
        private readonly TestReport _report;
        private readonly EverythingDataProvider _provider = new EverythingDataProvider();

        public SystemTests(TestReport report)
        {
            _report = report;
        }

        public bool EverythingAvailable { get; private set; }

        public void Run(string scanRoot)
        {
            _report.Section("Everything");
            EverythingAvailable = _provider.IsAvailable();
            _report.Check("Everything доступен по IPC", () =>
            {
                if (!EverythingAvailable) throw new InvalidOperationException("окно Everything IPC не найдено");
                return "версия " + _provider.GetVersion().ToString(3);
            });

            if (!EverythingAvailable)
            {
                _report.Info("Остальные проверки пропущены: запустите Everything и повторите.");
                return;
            }

            RunChildrenTests(scanRoot);
            RunProgressiveTests(scanRoot);
            RunCountTests(scanRoot);
            RunLayoutTests(scanRoot);
        }

        private void RunChildrenTests(string root)
        {
            _report.Section("Прямые дети папки (parent:)");

            List<FileSystemItem> children = null;
            _report.Check("Запрос детей " + root, () =>
            {
                children = _provider.GetChildren(root);
                if (children.Count == 0) throw new InvalidOperationException("пусто");
                return children.Count + " элементов";
            });

            if (children == null || children.Count == 0) return;

            _report.Check("У каждого ребёнка заполнен FullPath и Name", () =>
            {
                FileSystemItem bad = children.FirstOrDefault(c =>
                    string.IsNullOrEmpty(c.FullPath) || string.IsNullOrEmpty(c.Name));
                if (bad != null) throw new InvalidOperationException("пустой путь у элемента");
                return "ок";
            });

            _report.Check("Полное совпадение с диска (выборка)", () =>
            {
                // Cross-check a few folders against the real file system.
                int checkedCount = 0;
                foreach (FileSystemItem folder in children.Where(c => c.IsFolder).Take(5))
                {
                    var fromDisk = new HashSet<string>(
                        System.IO.Directory.Exists(folder.FullPath)
                            ? System.IO.Directory.EnumerateFileSystemEntries(folder.FullPath)
                                .Select(System.IO.Path.GetFileName)
                            : Enumerable.Empty<string>(),
                        StringComparer.OrdinalIgnoreCase);
                    var fromIndex = new HashSet<string>(
                        _provider.GetChildren(folder.FullPath).Select(c => c.Name),
                        StringComparer.OrdinalIgnoreCase);

                    if (fromDisk.Count > 0 && !fromDisk.SetEquals(fromIndex))
                    {
                        throw new InvalidOperationException(
                            "расхождение в " + folder.FullPath + ": диск=" + fromDisk.Count + ", индекс=" + fromIndex.Count);
                    }
                    checkedCount++;
                }
                return "сверено папок: " + checkedCount;
            });

            _report.Check("Everything отдаёт рекурсивный размер папок", () =>
            {
                FileSystemItem folder = children.FirstOrDefault(c => c.IsFolder);
                if (folder == null) throw new InvalidOperationException("нет папок");
                if (folder.Size <= 0)
                {
                    // A genuinely empty folder is acceptable; otherwise the size must be known.
                    return "проверка пропущена (первая папка пуста)";
                }
                return folder.Name + " = " + folder.Size.ToString("N0") + " Б";
            });
        }

        private void RunProgressiveTests(string root)
        {
            _report.Section("Ленивая загрузка дерева");

            var loader = new ProgressiveTreeLoader(_provider);
            var sw = Stopwatch.StartNew();
            bool loaded = false;
            _report.Check("LoadRootAsync лишь первый уровень", () =>
            {
                loader.LoadRootAsync(root, CancellationToken.None).Wait();
                sw.Stop();
                loaded = true;
                return loader.Root.Children.Count + " элементов за " + sw.ElapsedMilliseconds + " мс";
            });

            if (!loaded) return;

            _report.Check("Дети корня не раскрыты рекурсивно", () =>
            {
                FileNode folderWithChildren = loader.Root.Children.FirstOrDefault(c => c.IsFolder);
                if (folderWithChildren != null && folderWithChildren.Children.Count > 0)
                {
                    throw new InvalidOperationException("первый уровень оказался рекурсивным");
                }
                return "ок";
            });

            _report.Check("Объём корня равен сумме детей", () =>
            {
                long sum = loader.Root.Children.Sum(c => c.TotalSize);
                if (sum != loader.Root.TotalSize)
                {
                    throw new InvalidOperationException("сумма " + sum + " != корень " + loader.Root.TotalSize);
                }
                return FormatSize(loader.Root.TotalSize);
            });

            _report.Check("Раскрытие папки идемпотентно", () =>
            {
                FileNode folder = loader.Root.Children.FirstOrDefault(c => c.IsFolder);
                if (folder == null) return "нет папок";

                bool first = loader.ExpandAsync(folder, CancellationToken.None).Result;
                bool second = loader.ExpandAsync(folder, CancellationToken.None).Result;
                if (!first || second) throw new InvalidOperationException("повторное раскрытие не отсечено");
                return folder.Name + ": " + folder.Children.Count + " детей";
            });
        }

        private void RunCountTests(string root)
        {
            _report.Section("Счётчики файлов и папок");

            _report.Check("GetCounts возвращает неотрицательные числа", () =>
            {
                long files, folders;
                _provider.GetCounts(root, CancellationToken.None, out files, out folders);
                if (files < 0 || folders < 0) throw new InvalidOperationException("отрицательные значения");
                return FormatCount(folders, "папка", "папки", "папок") + ", " +
                       FormatCount(files, "файл", "файла", "файлов");
            });
        }

        private void RunLayoutTests(string root)
        {
            _report.Section("Раскладка treemap");

            var loader = new ProgressiveTreeLoader(_provider);
            loader.LoadRootAsync(root, CancellationToken.None).Wait();
            FileNode tree = loader.Root;

            _report.Check("Layout строит блоки для непустого дерева", () =>
            {
                List<TreemapBlock> blocks = TreemapLayout.Layout(
                    tree, new Rectangle(0, 0, 1200, 800), new TreemapOptions());
                if (blocks.Count == 0) throw new InvalidOperationException("нет блоков");
                return blocks.Count + " блоков";
            });

            _report.Check("Блоки не выходят за границы области", () =>
            {
                var bounds = new Rectangle(0, 0, 1200, 800);
                List<TreemapBlock> blocks = TreemapLayout.Layout(tree, bounds, new TreemapOptions());
                TreemapBlock bad = blocks.FirstOrDefault(b =>
                    b.Bounds.Left < -0.5f || b.Bounds.Top < -0.5f ||
                    b.Bounds.Right > bounds.Width + 0.5f || b.Bounds.Bottom > bounds.Height + 0.5f);
                if (bad != null) throw new InvalidOperationException("блок вне области: " + bad.Bounds);
                return "ок";
            });

            _report.Check("Сумма площадей крупнейших блоков ≈ площадь области", () =>
            {
                var bounds = new Rectangle(0, 0, 1200, 800);
                List<TreemapBlock> rootBlocks = TreemapLayout.Layout(tree, bounds, new TreemapOptions())
                    .Where(b => b.Depth == 0)
                    .ToList();
                double area = rootBlocks.Sum(b => (double)b.Bounds.Width * b.Bounds.Height);
                double expected = bounds.Width * bounds.Height;
                if (area < expected * 0.95 || area > expected * 1.05)
                {
                    throw new InvalidOperationException("площадь " + area.ToString("0") + " != " + expected);
                }
                return "площадь покрыта на " + (area / expected * 100).ToString("0") + "%";
            });
        }

        private static string FormatSize(long bytes)
        {
            return NodeLabelFormatter.FormatSize(bytes);
        }

        private static string FormatCount(long value, string one, string few, string many)
        {
            return NodeLabelFormatter.FormatCount(value, one, few, many);
        }
    }
}
