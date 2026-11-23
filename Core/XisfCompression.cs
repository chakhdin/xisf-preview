using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;

namespace XisfExplorerPreview
{
    // Parsed XISF compression attribute: "codec:uncompressed-size[:item-size]".
    public sealed class XisfCompressionSpec
    {
        // Base codec without the "+sh" suffix, e.g. "zlib"
        public string Codec { get; private set; }
        public bool Shuffled { get; private set; }
        public long UncompressedSize { get; private set; }
        public int ItemSize { get; private set; }

        // Returns null for a malformed attribute, an unsupported codec or a bad item size.
        public static XisfCompressionSpec Parse(string attribute)
        {
            if (string.IsNullOrEmpty(attribute)) return null;

            string[] parts = attribute.Split(':');
            if (parts.Length < 2 || parts.Length > 3) return null;

            string codec = parts[0].Trim().ToLowerInvariant();
            bool shuffled = codec.EndsWith("+sh", StringComparison.Ordinal);
            if (shuffled) codec = codec.Substring(0, codec.Length - 3);
            if (!XisfCompression.IsSupportedCodec(codec)) return null;

            if (!long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long size) || size <= 0)
                return null;

            int itemSize = 1;
            if (parts.Length == 3)
            {
                if (!int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out itemSize) ||
                    itemSize <= 0 || itemSize > 16)
                    return null;
            }
            else if (shuffled)
            {
                return null; // byte shuffling needs the item size
            }

            return new XisfCompressionSpec { Codec = codec, Shuffled = shuffled, UncompressedSize = size, ItemSize = itemSize };
        }
    }

    // Restores compressed XISF data blocks. Every failure (unknown codec, corrupt or
    // truncated stream, wrong output length) yields null instead of an exception.
    public static class XisfCompression
    {
        public static bool IsSupportedCodec(string codec) => codec == "zlib" || codec == "lz4" || codec == "lz4hc";

        // Deflate cannot expand more than about 1032:1, so a block declaring more than that
        // is rejected before its output buffer is allocated.
        private const long MaxZlibRatio = 1032;

        // An LZ4 block restores at most 255 bytes per input byte (one length byte of 255).
        private const long MaxLz4Ratio = 255;

        private static long MaxUncompressedSize(string codec, long compressedLength)
        {
            switch (codec)
            {
                case "zlib": return compressedLength * MaxZlibRatio + 64;
                case "lz4":
                case "lz4hc": return compressedLength * MaxLz4Ratio + 64;
                default: return 0;
            }
        }

        public static byte[] Decompress(XisfCompressionSpec spec, byte[] compressed)
        {
            if (spec == null || compressed == null || spec.UncompressedSize > int.MaxValue) return null;
            if (spec.UncompressedSize > MaxUncompressedSize(spec.Codec, compressed.LongLength)) return null;

            byte[] data;
            try
            {
                switch (spec.Codec)
                {
                    case "zlib": data = InflateZlib(compressed, (int)spec.UncompressedSize); break;
                    case "lz4":
                    case "lz4hc": data = DecodeLz4Block(compressed, (int)spec.UncompressedSize); break;
                    default: return null;
                }
            }
            catch
            {
                // A corrupt stream makes DeflateStream throw (the LZ4 decoder checks every bound itself); it means "no preview", like any other bad block.
                return null;
            }

            if (data == null) return null;
            return spec.Shuffled ? Unshuffle(data, spec.ItemSize) : data;
        }

        // zlib stream (RFC 1950): 2-byte header, raw deflate data, Adler-32 trailer.
        // Fills exactly 'size' bytes, rejects streams that end early or inflate further, and
        // checks the restored bytes against the Adler-32 in the last 4 bytes of the block.
        private static byte[] InflateZlib(byte[] compressed, int size)
        {
            if (compressed.Length < 6) return null;
            int cmf = compressed[0], flg = compressed[1];
            if ((cmf & 0x0F) != 8 || (cmf >> 4) > 7 || ((cmf << 8) | flg) % 31 != 0 || (flg & 0x20) != 0)
                return null;

            byte[] output = new byte[size];
            using (var input = new MemoryStream(compressed, 2, compressed.Length - 2, false))
            using (var inflater = new DeflateStream(input, CompressionMode.Decompress))
            {
                int got = 0;
                while (got < size)
                {
                    int r = inflater.Read(output, got, size - got);
                    if (r <= 0) return null; // stream shorter than declared
                    got += r;
                }

                if (inflater.Read(new byte[1], 0, 1) > 0) return null; // stream longer than declared
            }

            int t = compressed.Length - 4;
            uint stored = ((uint)compressed[t] << 24) | ((uint)compressed[t + 1] << 16) |
                          ((uint)compressed[t + 2] << 8) | compressed[t + 3];
            return Adler32(output) == stored ? output : null;
        }

        // LZ4 block format (lz4 and lz4hc share it; XISF stores blocks, not LZ4 frames).
        // Each sequence is a token (literal length high nibble, match length - 4 low nibble),
        // extra literal length bytes, the literals, a 2-byte little-endian offset and extra
        // match length bytes; a nibble of 15 continues with bytes added until one is below 255.
        // The last sequence has literals only. Every length and offset is checked against the
        // input and the output, and the output must be exactly 'size' bytes.
        private static byte[] DecodeLz4Block(byte[] input, int size)
        {
            byte[] output = new byte[size];
            int ip = 0, op = 0;
            while (ip < input.Length)
            {
                int token = input[ip++];

                long literals = token >> 4;
                if (literals == 15 && !ReadLz4Length(input, ref ip, ref literals)) return null;
                if (literals > input.Length - ip || literals > size - op) return null;
                Buffer.BlockCopy(input, ip, output, op, (int)literals);
                ip += (int)literals;
                op += (int)literals;

                if (ip == input.Length) break; // last sequence: literals only

                if (input.Length - ip < 2) return null;
                int offset = input[ip] | (input[ip + 1] << 8);
                ip += 2;
                if (offset == 0 || offset > op) return null;

                long match = token & 0x0F;
                if (match == 15 && !ReadLz4Length(input, ref ip, ref match)) return null;
                match += 4;
                if (match > size - op) return null;

                // Byte by byte, so an offset shorter than the match repeats the recent bytes.
                int from = op - offset;
                for (int end = op + (int)match; op < end; ) output[op++] = output[from++];
            }
            return op == size ? output : null;
        }

        // Adds the LZ4 length extension bytes to 'length'; false when the input ends first.
        private static bool ReadLz4Length(byte[] input, ref int ip, ref long length)
        {
            int b;
            do
            {
                if (ip >= input.Length) return false;
                b = input[ip++];
                length += b;
            } while (b == 255);
            return true;
        }

        // Adler-32 (RFC 1950). 5552 is the largest batch whose sums cannot overflow 32 bits
        // before the modulo.
        private static uint Adler32(byte[] data)
        {
            const uint Mod = 65521;
            uint a = 1, b = 0;
            int i = 0;
            while (i < data.Length)
            {
                int end = Math.Min(data.Length, i + 5552);
                for (; i < end; i++)
                {
                    a += data[i];
                    b += a;
                }
                a %= Mod;
                b %= Mod;
            }
            return (b << 16) | a;
        }

        // Inverse of XISF byte shuffling: the stored block holds the first byte of every item,
        // then every second byte, and so on; the trailing size % itemSize bytes are unchanged.
        public static byte[] Unshuffle(byte[] shuffled, int itemSize)
        {
            if (itemSize <= 1) return shuffled;

            int items = shuffled.Length / itemSize;
            byte[] output = new byte[shuffled.Length];
            int src = 0;
            for (int j = 0; j < itemSize; j++)
                for (int i = 0, dst = j; i < items; i++, dst += itemSize)
                    output[dst] = shuffled[src++];

            int tail = items * itemSize;
            Buffer.BlockCopy(shuffled, tail, output, tail, shuffled.Length - tail);
            return output;
        }
    }
}
