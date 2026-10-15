using System;
using System.Collections.Generic;
using System.Drawing;
using VoidSniffer.Model;

namespace VoidSniffer.Treemap
{
    /// <summary>
    /// Squarified treemap layout (Bruls, Huizing, van Wijk). Produces a flat,
    /// paint-ordered list of blocks: a container is emitted before its children,
    /// so drawing the list in order yields the nested treemap look.
    /// </summary>
    public static class TreemapLayout
    {
        public static List<TreemapBlock> Layout(FileNode root, Rectangle bounds, TreemapOptions options)
        {
            if (options == null) options = new TreemapOptions();
            var blocks = new List<TreemapBlock>();
            if (root == null || root.Children.Count == 0) return blocks;
            if (bounds.Width <= 2 || bounds.Height <= 2) return blocks;

            LayoutNode(root, new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height), 0, blocks, options);
            return blocks;
        }

        private static void LayoutNode(
            FileNode node, RectangleF rect, int depth, List<TreemapBlock> blocks, TreemapOptions options)
        {
            var children = new List<FileNode>();
            for (int i = 0; i < node.Children.Count; i++)
            {
                FileNode child = node.Children[i];
                if (child.TotalSize > 0)
                {
                    children.Add(child);
                }
            }
            if (children.Count == 0) return;

            children.Sort((a, b) => b.TotalSize.CompareTo(a.TotalSize));

            var areas = new List<double>(children.Count);
            for (int i = 0; i < children.Count; i++)
            {
                areas.Add(children[i].TotalSize);
            }

            var rects = new List<RectangleF>(children.Count);
            Squarify(areas, rect, rects);

            for (int i = 0; i < children.Count && i < rects.Count; i++)
            {
                FileNode child = children[i];
                RectangleF childRect = rects[i];
                blocks.Add(new TreemapBlock { Node = child, Bounds = childRect, Depth = depth });

                if (child.IsFolder && child.Children.Count > 0 && depth + 1 < options.MaxDepth)
                {
                    RectangleF inner = InsetForTitle(childRect, options);
                    if (inner.Width >= options.MinContainerSize && inner.Height >= options.MinContainerSize)
                    {
                        LayoutNode(child, inner, depth + 1, blocks, options);
                    }
                }
            }
        }

        private static RectangleF InsetForTitle(RectangleF rect, TreemapOptions options)
        {
            float pad = options.Padding;
            float top = pad;
            if (rect.Height >= options.TitleHeight + (2 * pad) + options.MinContainerSize)
            {
                top = options.TitleHeight;
            }
            return new RectangleF(
                rect.X + pad,
                rect.Y + top,
                Math.Max(0f, rect.Width - (2 * pad)),
                Math.Max(0f, rect.Height - top - pad));
        }

        /// <summary>
        /// Assigns a rectangle to every area. Emitted rectangles correspond
        /// one-to-one, in order, with <paramref name="areas"/>.
        /// </summary>
        public static void Squarify(IList<double> areas, RectangleF container, IList<RectangleF> output)
        {
            if (areas == null || areas.Count == 0) return;

            double total = 0;
            for (int i = 0; i < areas.Count; i++) total += areas[i];
            if (total <= 0) return;

            double scale = (double)container.Width * container.Height / total;
            var scaled = new List<double>(areas.Count);
            for (int i = 0; i < areas.Count; i++) scaled.Add(areas[i] * scale);

            RectangleF remaining = container;
            var row = new List<double>();
            int index = 0;

            while (index < scaled.Count)
            {
                double side = Math.Min(remaining.Width, remaining.Height);
                double currentWorst = Worst(row, side);
                double nextWorst = WorstWith(row, scaled[index], side);

                if (row.Count == 0 || nextWorst <= currentWorst)
                {
                    row.Add(scaled[index]);
                    index++;
                }
                else
                {
                    remaining = LayoutRow(row, remaining, output);
                    row.Clear();
                }
            }

            if (row.Count > 0)
            {
                LayoutRow(row, remaining, output);
            }
        }

        private static double Worst(List<double> row, double side)
        {
            if (row.Count == 0 || side <= 0) return double.MaxValue;

            double sum = 0, max = 0, min = double.MaxValue;
            for (int i = 0; i < row.Count; i++)
            {
                double a = row[i];
                sum += a;
                if (a > max) max = a;
                if (a < min) min = a;
            }
            if (sum <= 0 || min <= 0) return double.MaxValue;

            double sumSq = sum * sum;
            double sideSq = side * side;
            return Math.Max(sideSq * max / sumSq, sumSq / (sideSq * min));
        }

        private static double WorstWith(List<double> row, double next, double side)
        {
            var probe = new List<double>(row.Count + 1);
            probe.AddRange(row);
            probe.Add(next);
            return Worst(probe, side);
        }

        private static RectangleF LayoutRow(List<double> row, RectangleF remaining, IList<RectangleF> output)
        {
            double sum = 0;
            for (int i = 0; i < row.Count; i++) sum += row[i];

            if (remaining.Width >= remaining.Height)
            {
                // Vertical strip on the left-hand side.
                double thickness = remaining.Height > 0 ? sum / remaining.Height : 0;
                if (thickness > remaining.Width) thickness = remaining.Width;

                float y = remaining.Y;
                for (int i = 0; i < row.Count; i++)
                {
                    float height = (i == row.Count - 1)
                        ? remaining.Bottom - y
                        : (float)(row[i] / thickness);
                    output.Add(new RectangleF(remaining.X, y, (float)thickness, height));
                    y += height;
                }

                return RectangleF.FromLTRB(
                    remaining.X + (float)thickness, remaining.Y, remaining.Right, remaining.Bottom);
            }
            else
            {
                // Horizontal strip along the top.
                double thickness = remaining.Width > 0 ? sum / remaining.Width : 0;
                if (thickness > remaining.Height) thickness = remaining.Height;

                float x = remaining.X;
                for (int i = 0; i < row.Count; i++)
                {
                    float width = (i == row.Count - 1)
                        ? remaining.Right - x
                        : (float)(row[i] / thickness);
                    output.Add(new RectangleF(x, remaining.Y, width, (float)thickness));
                    x += width;
                }

                return RectangleF.FromLTRB(
                    remaining.X, remaining.Y + (float)thickness, remaining.Right, remaining.Bottom);
            }
        }
    }
}
