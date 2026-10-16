using System;
using System.Drawing;
using VoidSniffer.Model;

namespace VoidSniffer.Treemap
{
    /// <summary>
    /// Palette and colors for the treemap, styled after SpaceSniffer: a warm cream
    /// background, terracotta/brown tiles and leaf files tinted by extension. Colors are a
    /// pure function of the block's palette index and depth, so they stay stable across
    /// repaints.
    /// </summary>
    public static class TreemapColorizer
    {
        /// <summary>Warm cream backdrop between tiles.</summary>
        public static readonly Color Background = Color.FromArgb(250, 246, 234);

        /// <summary>Border shared by every tile (dark enough to separate them clearly).</summary>
        public static readonly Color Border = Color.FromArgb(96, 76, 48);

        public const int PaletteSize = 12;

        /// <summary>Bottom-level folder tiles: warm tans and browns.</summary>
        private static readonly Color[] FolderPalette =
        {
            Color.FromArgb(197, 178, 141),
            Color.FromArgb(210, 191, 152),
            Color.FromArgb(188, 166, 131),
            Color.FromArgb(216, 200, 166),
            Color.FromArgb(203, 182, 143),
            Color.FromArgb(224, 210, 178),
            Color.FromArgb(192, 171, 137),
            Color.FromArgb(208, 188, 149),
            Color.FromArgb(184, 163, 128),
            Color.FromArgb(200, 180, 144),
            Color.FromArgb(214, 196, 160),
            Color.FromArgb(190, 169, 133)
        };

        /// <summary>Deeper folders stay in the brown family but read a little darker.</summary>
        public static Color GetFill(int colorIndex, int depth)
        {
            Color c = FolderPalette[Mod(colorIndex, FolderPalette.Length)];
            return depth <= 1 ? c : Darken(c, (depth - 1) * 0.05);
        }

        /// <summary>
        /// Leaf files are tinted by extension, like SpaceSniffer's color-coded files.
        /// </summary>
        public static Color GetFileFill(FileNode node)
        {
            if (node.IsSynthetic)
            {
                return Color.FromArgb(178, 205, 165);
            }

            string extension = GetExtension(node);
            switch (extension)
            {
                case "exe": case "dll": case "sys": case "msi": case "ocx": return Color.FromArgb(137, 177, 200);
                case "obj": case "o": case "lib": case "pdb": case "ilk": case "exp": return Color.FromArgb(155, 200, 152);
                case "jpg": case "jpeg": case "png": case "gif": case "bmp": case "ico": case "webp": return Color.FromArgb(214, 150, 171);
                case "zip": case "rar": case "7z": case "gz": case "tar": case "cab": return Color.FromArgb(212, 178, 118);
                case "txt": case "md": case "log": case "ini": case "cfg": case "csv": return Color.FromArgb(201, 196, 179);
                case "zipx": return Color.FromArgb(212, 178, 118);
                case "cs": case "js": case "ts": case "py": case "cpp": case "c": case "h": case "java": case "xml": case "json": case "html": case "css": return Color.FromArgb(190, 165, 214);
                default: return Color.FromArgb(206, 199, 185);
            }
        }

        public static Color GetTextColor(Color fill)
        {
            double luminance = ((0.299 * fill.R) + (0.587 * fill.G) + (0.114 * fill.B)) / 255.0;
            return luminance > 0.58 ? Color.FromArgb(48, 40, 30) : Color.FromArgb(250, 248, 244);
        }

        /// <summary>Hover overlay color (painted over the hovered tile to lighten it).</summary>
        public static readonly Color HoverOverlay = Color.White;

        /// <summary>Overlay alpha right when the pointer enters a tile (bright flash).</summary>
        public const int HoverStartAlpha = 150;

        /// <summary>Overlay alpha the hover settles at (slightly lighter than the tile).</summary>
        public const int HoverEndAlpha = 45;

        /// <summary>
        /// Eased hover overlay alpha for an animation <paramref name="progress"/> in [0, 1]:
        /// starts bright and fades quickly (ease-out) to the subtle resting highlight.
        /// </summary>
        public static int HoverAlpha(double progress)
        {
            if (progress < 0.0) progress = 0.0;
            if (progress > 1.0) progress = 1.0;

            double eased = 1.0 - Math.Pow(1.0 - progress, 2.0);
            return (int)Math.Round(HoverStartAlpha + ((HoverEndAlpha - HoverStartAlpha) * eased));
        }

        private static string GetExtension(FileNode node)
        {
            string name = node.Name ?? string.Empty;
            int dot = name.LastIndexOf('.');
            if (dot <= 0 || dot >= name.Length - 1) return string.Empty;
            return name.Substring(dot + 1).ToLowerInvariant();
        }

        private static int Mod(int value, int mod)
        {
            int result = value % mod;
            return result < 0 ? result + mod : result;
        }

        private static Color Darken(Color color, double amount)
        {
            double factor = 1.0 - amount;
            if (factor < 0.5) factor = 0.5;
            return Color.FromArgb(
                color.A,
                (int)(color.R * factor),
                (int)(color.G * factor),
                (int)(color.B * factor));
        }
    }
}
