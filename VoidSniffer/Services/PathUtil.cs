using System;

namespace VoidSniffer.Services
{
    /// <summary>Path helpers shared by the tree builder and the data provider.</summary>
    public static class PathUtil
    {
        /// <summary>
        /// Canonicalizes a path for use as a dictionary key: unified separators,
        /// no trailing separator (except for a bare drive such as "C:").
        /// </summary>
        public static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;

            string result = path.Trim().Replace('/', '\\');
            while (result.Length > 1 && result.EndsWith("\\", StringComparison.Ordinal))
            {
                result = result.Substring(0, result.Length - 1);
            }
            return result;
        }

        public static bool EqualsPath(string a, string b)
        {
            return string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Last path segment ("C:\Windows\notepad.exe" -> "notepad.exe").</summary>
        public static string GetName(string path)
        {
            string normalized = Normalize(path);
            int index = normalized.LastIndexOf('\\');
            if (index < 0) return normalized;
            if (index == normalized.Length - 1) return normalized;
            return normalized.Substring(index + 1);
        }

        /// <summary>Parent directory of a path, or an empty string when there is none.</summary>
        public static string GetParentPath(string path)
        {
            string normalized = Normalize(path);
            int index = normalized.LastIndexOf('\\');
            if (index < 0) return string.Empty;
            if (index == 0) return string.Empty;

            // "C:\" style drive root: keep the drive designator.
            if (index == 2 && normalized.Length > 2 && normalized[1] == ':')
            {
                return normalized.Substring(0, 2);
            }
            return normalized.Substring(0, index);
        }

        /// <summary>True when <paramref name="path"/> equals or lies below <paramref name="root"/>.</summary>
        public static bool IsUnderRoot(string path, string root)
        {
            string p = Normalize(path);
            string r = Normalize(root);
            if (p.Length == 0 || r.Length == 0) return false;
            if (string.Equals(p, r, StringComparison.OrdinalIgnoreCase)) return true;
            return p.StartsWith(r + "\\", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Joins a directory and a name, avoiding duplicate separators.</summary>
        public static string Combine(string directory, string name)
        {
            string dir = Normalize(directory);
            if (string.IsNullOrEmpty(dir)) return name;
            if (string.IsNullOrEmpty(name)) return dir;
            return dir + "\\" + name;
        }
    }
}
