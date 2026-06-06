namespace Majorsilence.Drawing
{
    /// <summary>
    /// Extracts the embedded fallback fonts to a temporary directory so that
    /// renderers needing file-system font paths (e.g. the iTextSharp PDF renderer)
    /// can locate them without requiring the fonts to be installed on the host.
    /// </summary>
    public static class FontResourceLoader
    {
        private static string? _extractedDir;

#if NET10_OR_GREATER
        private static readonly Lock _lock = new();
#else
        private static readonly object _lock = new();
#endif

        /// <summary>
        /// Returns a directory path containing all bundled .ttf files.
        /// Fonts are extracted from the assembly on the first call; subsequent
        /// calls return the cached path immediately.
        /// </summary>
        public static string GetFontDirectory()
        {
            if (_extractedDir != null)
                return _extractedDir;

            lock (_lock)
            {
                if (_extractedDir != null)
                    return _extractedDir;

                var dir = Path.Combine(Path.GetTempPath(), "majorsilence-fonts");
                Directory.CreateDirectory(dir);
                ExtractAll(dir);
                _extractedDir = dir;
                return dir;
            }
        }

        private static void ExtractAll(string dir)
        {
            var assembly = typeof(FontResourceLoader).Assembly;
            foreach (var name in assembly.GetManifestResourceNames())
            {
                if (!name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Resource name format: Majorsilence.Drawing.Common.Fonts.LiberationSans-Regular.ttf
                // Reconstruct the filename from the last two dot-separated segments.
                var parts = name.Split('.');
                if (parts.Length < 2) continue;
                var filename = parts[^2] + "." + parts[^1]; // e.g. "LiberationSans-Regular.ttf"

                var dest = Path.Combine(dir, filename);
                if (File.Exists(dest)) continue;

                try
                {
                    using var stream = assembly.GetManifestResourceStream(name);
                    if (stream == null) continue;
                    using var file = File.Create(dest);
                    stream.CopyTo(file);
                }
                catch { /* skip any unreadable resource */ }
            }
        }
    }
}
