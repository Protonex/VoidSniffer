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
        /// Line 2: logical size, plus recursive file/folder counts for folders and the
        /// on-disk size when it has been resolved.
        /// </summary>
        public static string DescribeDetails(FileNode node)
        {
            if (node == null) return string.Empty;

            var sb = new StringBuilder();
            sb.Append(FormatSize(node.TotalSize));

            if (node.IsFolder && node.HasCounts)
            {
                sb.Append("  ·  ");
                sb.Append(FormatCount(node.FolderCount, "папка", "папки", "папок"));
                sb.Append(", ");
                sb.Append(FormatCount(node.FileCount, "файл", "файла", "файлов"));
            }

            if (node.HasOnDisk)
            {
                sb.Append("  ·  ≈");
                sb.Append(FormatSize(node.OnDiskSize));
                sb.Append(" на диске");
            }

            return sb.ToString();
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
