namespace VoidSniffer.Treemap
{
    /// <summary>Tuning knobs for the treemap layout.</summary>
    public sealed class TreemapOptions
    {
        public TreemapOptions()
        {
            MaxDepth = 8;
            MinContainerSize = 18f;
            TitleHeight = 14f;
            Padding = 1f;
        }

        /// <summary>Maximum nesting depth that is rendered.</summary>
        public int MaxDepth { get; set; }

        /// <summary>Smallest container (width or height, in px) worth recursing into.</summary>
        public float MinContainerSize { get; set; }

        /// <summary>Height of the header strip reserved for a folder label.</summary>
        public float TitleHeight { get; set; }

        /// <summary>Gap kept between a container and its children.</summary>
        public float Padding { get; set; }
    }
}
