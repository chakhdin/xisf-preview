using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;

namespace XisfExplorerPreview
{
    // One entry of the XISF subblocks attribute: compressed and uncompressed length in bytes.
    public struct XisfSubblock
    {
        public long CompressedSize;
        public long UncompressedSize;
    }

    // Parsed XISF compression attribute "codec:uncompressed-size[:item-size]", with the
    // optional subblocks attribute "c1,u1:c2,u2:...:cN,uN".
    public sealed class XisfCompressionSpec
    {
        // Base codec without the "+sh" suffix, e.g. "zlib"
        public string Codec { get; private set; }
        public bool Shuffled { get; private set; }
        public long UncompressedSize { get; private set; }
        public int ItemSize { get; private set; }

        // Null when the block is not divided into subblocks.
        public XisfSubblock[] Subblocks { get; private set; }

        // Returns null for a malformed attribute, an unsupported codec, a bad item size, or a
        // subblocks attribute that is present but malformed or whose uncompressed lengths do
        // not add up to the declared uncompressed size.
        public static XisfCompressionSpec Parse(string attribute, string subblocks = null)
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

            XisfSubblock[] blocks = null;
            if (subblocks != null)
            {
                blocks = ParseSubblocks(subblocks);
                if (blocks == null) return null;
                long total = 0;
                foreach (XisfSubblock b in blocks) total += b.UncompressedSize;
                if (total != size) return null;
            }

            return new XisfCompressionSpec
            {
                Codec = codec, Shuffled = shuffled, UncompressedSize = size, ItemSize = itemSize, Subblocks = blocks
            };
        }

        // Every length must be a positive number of at most int.MaxValue bytes, so the sums
        // cannot overflow (the header size limits the number of pairs).
        private static XisfSubblock[] ParseSubblocks(string attribute)
        {
            string[] pairs = attribute.Split(':');
            var blocks = new XisfSubblock[pairs.Length];
            for (int i = 0; i < pairs.Length; i++)
            {
                string[] parts = pairs[i].Split(',');
                if (parts.Length != 2 ||
                    !long.TryParse(parts[0].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out long c) ||
                    !long.TryParse(parts[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out long u) ||
                    c <= 0 || u <= 0 || c > int.MaxValue || u > int.MaxValue)
                    return null;
                blocks[i] = new XisfSubblock { CompressedSize = c, UncompressedSize = u };
            }
            return blocks;
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

            XisfSubblock[] blocks = spec.Subblocks ??
                new[] { new XisfSubblock { CompressedSize = compressed.LongLength, UncompressedSize = spec.UncompressedSize } };

            // Check every length against the stored bytes before the output buffer is allocated.
            long storedTotal = 0;
            foreach (XisfSubblock b in blocks)
            {
                storedTotal += b.CompressedSize;
                bool stored = spec.Subblocks != null && b.CompressedSize == b.UncompressedSize;
                if (!stored && b.UncompressedSize > MaxUncompressedSize(spec.Codec, b.CompressedSize)) return null;
            }
            if (storedTotal != compressed.LongLength) return null;

            byte[] data = new byte[spec.UncompressedSize];
            int ip = 0, op = 0;
            try
            {
                foreach (XisfSubblock b in blocks)
                {
                    int c = (int)b.CompressedSize, u = (int)b.UncompressedSize;
                    bool ok;
                    if (spec.Subblocks != null && c == u)
                    {
                        // An uncompressed subblock stores its bytes unmodified.
                        Buffer.BlockCopy(compressed, ip, data, op, u);
                        ok = true;
                    }
                    else
                    {
                        switch (spec.Codec)
                        {
                            case "zlib": ok = InflateZlib(compressed, ip, c, data, op, u); break;
                            case "lz4":
                            case "lz4hc": ok = DecodeLz4Block(compressed, ip, c, data, op, u); break;
                            default: return null;
                        }
                    }
                    if (!ok) return null;
                    ip += c;
                    op += u;
                }
            }
            catch
            {
                // A corrupt stream makes DeflateStream throw (the LZ4 decoder checks every bound
                // itself); it means "no preview", like any other bad block.
                return null;
            }

            // Byte shuffling applies to the whole block, so it is reversed once, after all
            // subblocks have been joined.
            return spec.Shuffled ? Unshuffle(data, spec.ItemSize) : data;
        }

        // zlib stream (RFC 1950) in input[inStart, inStart + inLength): 2-byte header, raw
        // deflate data, Adler-32 trailer. Fills exactly output[outStart, outStart + size),
        // rejects streams that end early or inflate further, and checks the restored bytes
        // against the Adler-32 in the last 4 bytes of the stream.
        private static bool InflateZlib(byte[] input, int inStart, int inLength, byte[] output, int outStart, int size)
        {
            if (inLength < 6) return false;
            int cmf = input[inStart], flg = input[inStart + 1];
            if ((cmf & 0x0F) != 8 || (cmf >> 4) > 7 || ((cmf << 8) | flg) % 31 != 0 || (flg & 0x20) != 0)
                return false;

            using (var stream = new MemoryStream(input, inStart + 2, inLength - 2, false))
            using (var inflater = new DeflateStream(stream, CompressionMode.Decompress))
            {
                int got = 0;
                while (got < size)
                {
                    int r = inflater.Read(output, outStart + got, size - got);
                    if (r <= 0) return false; // stream shorter than declared
                    got += r;
                }

                if (inflater.Read(new byte[1], 0, 1) > 0) return false; // stream longer than declared
            }

            int t = inStart + inLength - 4;
            uint stored = ((uint)input[t] << 24) | ((uint)input[t + 1] << 16) |
                          ((uint)input[t + 2] << 8) | input[t + 3];
            return Adler32(output, outStart, size) == stored;
        }

        // LZ4 block format (lz4 and lz4hc share it; XISF stores blocks, not LZ4 frames).
        // Each sequence is a token (literal length high nibble, match length - 4 low nibble),
        // extra literal length bytes, the literals, a 2-byte little-endian offset and extra
        // match length bytes; a nibble of 15 continues with bytes added until one is below 255.
        // The last sequence has literals only. Every length and offset is checked against the
        // input block and the output slice, and the output must be exactly 'size' bytes. The
        // block is input[inStart, inStart + inLength) and decodes into output[outStart, outStart + size);
        // matches never reach back before outStart, since each block is independent.
        private static bool DecodeLz4Block(byte[] input, int inStart, int inLength, byte[] output, int outStart, int size)
        {
            int ip = inStart, inEnd = inStart + inLength;
            int op = outStart, outEnd = outStart + size;
            while (ip < inEnd)
            {
                int token = input[ip++];

                long literals = token >> 4;
                if (literals == 15 && !ReadLz4Length(input, inEnd, ref ip, ref literals)) return false;
                if (literals > inEnd - ip || literals > outEnd - op) return false;
                Buffer.BlockCopy(input, ip, output, op, (int)literals);
                ip += (int)literals;
                op += (int)literals;

                if (ip == inEnd) break; // last sequence: literals only

                if (inEnd - ip < 2) return false;
                int offset = input[ip] | (input[ip + 1] << 8);
                ip += 2;
                if (offset == 0 || offset > op - outStart) return false;

                long match = token & 0x0F;
                if (match == 15 && !ReadLz4Length(input, inEnd, ref ip, ref match)) return false;
                match += 4;
                if (match > outEnd - op) return false;

                // Byte by byte, so an offset shorter than the match repeats the recent bytes.
                int from = op - offset;
                for (int end = op + (int)match; op < end; ) output[op++] = output[from++];
            }
            return op == outEnd;
        }

        // Adds the LZ4 length extension bytes to 'length'; false when the input ends first.
        private static bool ReadLz4Length(byte[] input, int inEnd, ref int ip, ref long length)
        {
            int b;
            do
            {
                if (ip >= inEnd) return false;
                b = input[ip++];
                length += b;
            } while (b == 255);
            return true;
        }

        // Adler-32 (RFC 1950). 5552 is the largest batch whose sums cannot overflow 32 bits
        // before the modulo.
        private static uint Adler32(byte[] data, int start, int length)
        {
            const uint Mod = 65521;
            uint a = 1, b = 0;
            int i = start, last = start + length;
            while (i < last)
            {
                int end = Math.Min(last, i + 5552);
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
