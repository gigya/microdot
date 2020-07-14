using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace Metrics.Endpoints
{
    public static class FlotWebApp
    {
        private static readonly Assembly thisAssembly = Assembly.GetAssembly(typeof(FlotWebApp));
        private const string FlotAppResource = "Metrics.Endpoints.index.full.html.gz";
        private const string FavIconResource = "Metrics.Endpoints.metrics_32.png";

        private static string ReadFromEmbededResource()
        {
            using (var stream = Assembly.GetAssembly(typeof(FlotWebApp)).GetManifestResourceStream(FlotAppResource))
            using (var gzip = new GZipStream(stream, CompressionMode.Decompress))
            using (var reader = new StreamReader(gzip))
            {
                return reader.ReadToEnd();
            }
        }

        private static readonly Lazy<string> htmlContent = new Lazy<string>(ReadFromEmbededResource);

        public static string GetFlotApp()
        {
            return htmlContent.Value;
        }

        public const string FavIconMimeType = "image/png";

        public static void WriteFavIcon(Stream output)
        {
            using (var stream = thisAssembly.GetManifestResourceStream(FavIconResource))
            {
                Debug.Assert(stream != null, "Unable to read embeded flot app");
                stream.CopyTo(output);
            }
        }

        public static Stream GetAppStream(bool decompress = false)
        {
            var stream = !decompress ? thisAssembly.GetManifestResourceStream(FlotAppResource) : new GZipStream(GetAppStream(), CompressionMode.Decompress, false);
            Debug.Assert(stream != null, "Unable to read embeded flot app");
            return stream;
        }

        public static void WriteFlotAppAsync(Stream output, bool decompress = false)
        {
            using (var stream = GetAppStream(decompress))
            {
                stream.CopyTo(output);
            }
        }
    }
}
