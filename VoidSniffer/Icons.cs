using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace VoidSniffer
{
    /// <summary>
    /// Loads the small toolbar icons embedded in the assembly (from the FatCow icon set).
    /// Returns <c>null</c> when an icon is missing so callers can fall back to text.
    /// </summary>
    internal static class Icons
    {
        private const string ResourcePrefix = "VoidSniffer.Resources.Icons.";
        private const int Size = 16;

        private static readonly Dictionary<string, Image> Cache =
            new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

        public static Image Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            Image image;
            if (Cache.TryGetValue(name, out image)) return image;

            image = Load(name);
            Cache[name] = image;
            return image;
        }

        private static Image Load(string name)
        {
            try
            {
                Assembly assembly = typeof(Icons).Assembly;
                using (Stream stream = assembly.GetManifestResourceStream(ResourcePrefix + name + ".png"))
                {
                    if (stream == null) return null;

                    using (Image source = Image.FromStream(stream))
                    {
                        return new Bitmap(source, Size, Size);
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
