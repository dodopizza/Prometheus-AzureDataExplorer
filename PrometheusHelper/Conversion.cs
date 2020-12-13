﻿using System.IO;
using Snappy.Sharp;

namespace PromADX.PrometheusHelper
{
    public static class Conversion
    {
        private static readonly SnappyDecompressor Decompressor = new SnappyDecompressor();
        public static byte[] DecompressBody(Stream body)
        {
            var ms = new MemoryStream();
            body.CopyTo(ms);
            var source = ms.ToArray();
            if (source.Length <= 0) return null;
            var decompressed = Decompressor.Decompress(source, 0, source.Length);
            return decompressed;
        }
    }
}
