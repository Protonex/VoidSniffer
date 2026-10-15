using System;
using System.Drawing;
using VoidSniffer.Model;

namespace VoidSniffer.Treemap
{
    /// <summary>Deterministic, stable coloring for treemap blocks.</summary>
    public static class TreemapColorizer
    {
        public static Color GetFill(FileNode node, int depth)
        {
            string key = GetColorKey(node);
            int hash = StableHash(key);

            double hue = hash % 360;
            double saturation = node.IsFolder ? 0.42 : 0.55;
            double value = node.IsFolder ? 0.88 : 0.97;
            value -= depth * 0.05;
            if (value < 0.35) value = 0.35;

            return HsvToColor(hue, saturation, value);
        }

        public static Color GetTextColor(Color fill)
        {
            double luminance = ((0.299 * fill.R) + (0.587 * fill.G) + (0.114 * fill.B)) / 255.0;
            return luminance > 0.6 ? Color.FromArgb(20, 20, 20) : Color.FromArgb(240, 240, 240);
        }

        public static Color GetBorderColor(int depth)
        {
            int shade = 60 + (depth * 12);
            if (shade > 160) shade = 160;
            return Color.FromArgb(shade, shade, shade);
        }

        private static string GetColorKey(FileNode node)
        {
            if (node.IsFolder)
            {
                return node.Name ?? string.Empty;
            }

            string name = node.Name ?? string.Empty;
            int dot = name.LastIndexOf('.');
            if (dot > 0 && dot < name.Length - 1)
            {
                return name.Substring(dot).ToLowerInvariant();
            }
            return name.ToLowerInvariant();
        }

        /// <summary>FNV-1a so colors stay identical across runs and processes.</summary>
        private static int StableHash(string value)
        {
            unchecked
            {
                const uint offset = 2166136261;
                const uint prime = 16777619;
                uint hash = offset;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= prime;
                }
                return (int)(hash & 0x7fffffff);
            }
        }

        private static Color HsvToColor(double hue, double saturation, double value)
        {
            int hi = (int)Math.Floor(hue / 60) % 6;
            double f = (hue / 60) - Math.Floor(hue / 60);
            double v = value * 255;
            int p = (int)(v * (1 - saturation));
            int q = (int)(v * (1 - (f * saturation)));
            int t = (int)(v * (1 - ((1 - f) * saturation)));
            int vv = (int)v;

            switch (hi)
            {
                case 0: return Color.FromArgb(vv, t, p);
                case 1: return Color.FromArgb(q, vv, p);
                case 2: return Color.FromArgb(p, vv, t);
                case 3: return Color.FromArgb(p, q, vv);
                case 4: return Color.FromArgb(t, p, vv);
                default: return Color.FromArgb(vv, p, q);
            }
        }
    }
}
