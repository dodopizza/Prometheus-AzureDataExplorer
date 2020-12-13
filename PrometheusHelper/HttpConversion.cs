using System.IO;
using Snappy.Sharp;

namespace PromADX.PrometheusHelper
{
    public static class HttpConversion
    {
        private static readonly SnappyDecompressor _decompressor = new SnappyDecompressor();

        public static byte[] DecompressBody(Stream body)
        {
            var ms = new MemoryStream();
            body.CopyTo(ms);
            var source = ms.ToArray();
            if (source.Length <= 0) return null;
            var decompressed = _decompressor.Decompress(source, 0, source.Length);
            return decompressed;
        }
    }
}