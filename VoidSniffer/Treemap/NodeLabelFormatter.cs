using System;
using System.Globalization;
using System.Text;
using VoidSniffer.Model;

namespace VoidSniffer.Treemap
{
    /// <summary>
    /// Formats the text drawn on treemap tiles (name, size, recursive counts and the
    /// on-disk size). Kept free of WinForms so it can be unit tested.
    /// </summary>
    public static class NodeLabelFormatter
    {
        /// <summary>Line 1: the node name.</summary>
        public static string DescribeName(FileNode node)
        {
            return node == null ? string.Empty : (node.Name ?? string.Empty);
        }

        /// <summary>
        /// Line 2 of a tile label: just the size. Everything else (on-disk size, counts,
        /// dates, path) lives in the hover tooltip to keep the tiles uncluttered.
        /// </summary>
        public static string DescribeDetails(FileNode node)
        {
            return node == null ? string.Empty : FormatSize(node.TotalSize);
        }

        /// <summary>Title line of the hover tooltip (the node name).</summary>
        public static string TooltipTitle(FileNode node)
        {
            return node == null ? string.Empty : (node.Name ?? string.Empty);
        }

        /// <summary>
        /// Body lines of the hover tooltip: size (with the on-disk size in brackets),
        /// recursive counts for folders, the creation/modified/access times when known,
        /// and the full path.
        /// </summary>
        public static System.Collections.Generic.IList<string> TooltipLines(FileNode node)
        {
            var lines = new System.Collections.Generic.List<string>();
            if (node == null) return lines;

            var size = new StringBuilder();
            size.Append(FormatSize(node.TotalSize));
            if (node.HasOnDisk)
            {
                size.Append("   [");
                size.Append(FormatSize(node.OnDiskSize));
                size.Append(" на диске]");
            }
            lines.Add(size.ToString());

            if (node.IsFolder && node.HasCounts)
            {
                lines.Add(FormatCount(node.FolderCount, "папка", "папки", "папок") + ", " +
                          FormatCount(node.FileCount, "файл", "файла", "файлов"));
            }

            AppendDate(lines, "Создан", node.CreatedTime);
            AppendDate(lines, "Изменён", node.ModifiedTime);
            AppendDate(lines, "Открыт", node.LastAccessTime);

            if (!string.IsNullOrEmpty(node.FullPath))
            {
                lines.Add(node.FullPath);
            }

            return lines;
        }

        private static void AppendDate(System.Collections.Generic.IList<string> lines, string label, DateTime? value)
        {
            if (value.HasValue)
            {
                lines.Add(label + ": " + value.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture));
            }
        }

        /// <summary>Formats a byte count using binary units.</summary>
        public static string FormatSize(long bytes)
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

        /// <summary>Formats a count with the correct Russian plural form.</summary>
        public static string FormatCount(long value, string one, string few, string many)
        {
            return value.ToString("N0", CultureInfo.CurrentCulture) + " " + Plural(value, one, few, many);
        }

        public static string Plural(long value, string one, string few, string many)
        {
            long mod100 = value % 100;
            long mod10 = value % 10;
            if (mod10 == 1 && mod100 != 11) return one;
            if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return few;
            return many;
        }
    }
}
