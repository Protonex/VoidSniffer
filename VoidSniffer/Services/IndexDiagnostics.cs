using System.Collections.Generic;
using System.Text;
using VoidSniffer.Model;

namespace VoidSniffer.Services
{
    /// <summary>
    /// Builds a human-readable warning when the Everything index is missing options that
    /// VoidSniffer needs (folder/file sizes, timestamps). Uses Everything.ini when it can be
    /// read, otherwise falls back to inspecting the freshly loaded tree.
    /// </summary>
    public static class IndexDiagnostics
    {
        public static string BuildWarning(EverythingIndexOptions options, FileNode root)
        {
            var problems = new List<string>();

            bool folderSizesOff = options.FolderSizes.HasValue
                ? options.FolderSizes == false
                : EmpiricallyFolderSizesMissing(root);
            if (folderSizesOff)
            {
                problems.Add(Localization.T("index.warning.folderSizes"));
            }

            bool fileSizesOff = options.FileSizes.HasValue
                ? options.FileSizes == false
                : EmpiricallyFileSizesMissing(root);
            if (fileSizesOff)
            {
                problems.Add(Localization.T("index.warning.fileSizes"));
            }

            bool datesOff = options.DateCreated == false || options.DateModified == false || options.DateAccessed == false;
            if (datesOff)
            {
                problems.Add(Localization.T("index.warning.dates"));
            }

            if (problems.Count == 0) return null;

            var sb = new StringBuilder();
            sb.AppendLine(Localization.T("index.warning.header"));
            foreach (string p in problems)
            {
                sb.Append("  • ").AppendLine(p);
            }
            sb.AppendLine();
            sb.Append(Localization.T("index.warning.footer"));
            return sb.ToString();
        }

        private static bool EmpiricallyFolderSizesMissing(FileNode root)
        {
            if (root == null || root.Children.Count == 0) return false;

            bool hasFolder = false, hasSizedFile = false, folderHasSize = false;
            for (int i = 0; i < root.Children.Count; i++)
            {
                FileNode child = root.Children[i];
                if (child.IsFolder)
                {
                    hasFolder = true;
                    if (child.TotalSize > 0) folderHasSize = true;
                }
                else if (child.TotalSize > 0)
                {
                    hasSizedFile = true;
                }
            }

            // Folder sizes are indexed unless every folder is zero while file sizes are known.
            return hasFolder && !folderHasSize && hasSizedFile;
        }

        private static bool EmpiricallyFileSizesMissing(FileNode root)
        {
            if (root == null) return false;

            bool hasFile = false, fileHasSize = false;
            for (int i = 0; i < root.Children.Count; i++)
            {
                FileNode child = root.Children[i];
                if (!child.IsFolder)
                {
                    hasFile = true;
                    if (child.TotalSize > 0) fileHasSize = true;
                }
            }
            return hasFile && !fileHasSize;
        }
    }
}
