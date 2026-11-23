using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XisfExplorerPreview;
using Xunit;

namespace XisfPreview.Tests
{
    public class XisfLz4Tests
    {
        private static XisfRawFrame Load(byte[] file, int target = 0) =>
            XisfParser.LoadRawFrame(new MemoryStream(file), target);

        private static byte[] Decode(byte[] block, int size) =>
            XisfCompression.Decompress(XisfCompressionSpec.Parse($"lz4:{size}"), block);

        // Minimal greedy LZ4 block encoder, written from the LZ4 block format description
        // (github.com/lz4/lz4, doc/lz4_Block_format.md) independently of the decoder, searching 1024 bytes back. It follows
        // the end-of-block rules: the last 5 bytes are literals and no match starts in the last 12.
        internal static byte[] Lz4(byte[] data)
        {
            var output = new List<byte>();
            int n = data.Length, anchor = 0, pos = 0;
            while (pos + 12 <= n)
            {
                int bestLen = 0, bestOff = 0;
                for (int cand = Math.Max(0, pos - 1024); cand < pos; cand++)
                {
                    int len = 0;
                    while (pos + len < n - 5 && data[cand + len] == data[pos + len]) len++;
                    if (len > bestLen) { bestLen = len; bestOff = pos - cand; }
                }
                if (bestLen < 4) { pos++; continue; }
                EmitSequence(output, data, anchor, pos - anchor, bestOff, bestLen);
                pos += bestLen;
                anchor = pos;
            }
            EmitSequence(output, data, anchor, n - anchor, 0, 0);
            return output.ToArray();
        }

        private static void EmitSequence(List<byte> output, byte[] data, int start, int literals, int offset, int matchLen)
        {
            int m = matchLen == 0 ? 0 : matchLen - 4;
            output.Add((byte)((Math.Min(literals, 15) << 4) | Math.Min(m, 15)));
            if (literals >= 15) EmitLength(output, literals - 15);
            for (int i = 0; i < literals; i++) output.Add(data[start + i]);
            if (matchLen == 0) return;
            output.Add((byte)offset);
            output.Add((byte)(offset >> 8));
            if (m >= 15) EmitLength(output, m - 15);
        }

        private static void EmitLength(List<byte> output, int rest)
        {
            for (; rest >= 255; rest -= 255) output.Add(255);
            output.Add((byte)rest);
        }

        // Hand-written blocks, decoded by hand from the LZ4 block format description.

        [Fact]
        public void LiteralOnlyBlock_Decodes()
        {
            // Token 0x50: 5 literals, no match (last sequence)
            byte[] block = { 0x50, (byte)'h', (byte)'e', (byte)'l', (byte)'l', (byte)'o' };
            Assert.Equal(new[] { 'h', 'e', 'l', 'l', 'o' }.Select(c => (byte)c), Decode(block, 5));
        }

        [Fact]
        public void OverlappingMatch_RepeatsRecentBytes()
        {
            // Token 0x26: 2 literals "ab", offset 2, match 6 + 4 = 10 bytes copied from 2 back
            // ("abababababab"), then token 0x10 with the final literal "c".
            byte[] block = { 0x26, (byte)'a', (byte)'b', 0x02, 0x00, 0x10, (byte)'c' };
            Assert.Equal("ababababababc".Select(c => (byte)c), Decode(block, 13));
        }

        [Fact]
        public void OffsetOne_RunLengthMatch()
        {
            // Literal 7, offset 1, match 15 + 0 + 4 = 19 copies of it, final literal 9
            byte[] block = { 0x1F, 7, 0x01, 0x00, 0x00, 0x10, 9 };
            byte[] expected = Enumerable.Repeat((byte)7, 20).Concat(new byte[] { 9 }).ToArray();
            Assert.Equal(expected, Decode(block, 21));
        }

        [Fact]
        public void LongLiteralLength_With255Bytes()
        {
            // Literal length 15 + 255 + 255 + 5 = 530
            byte[] lits = Enumerable.Range(0, 530).Select(i => (byte)(i * 7)).ToArray();
            byte[] block = new byte[] { 0xF0, 255, 255, 5 }.Concat(lits).ToArray();
            Assert.Equal(lits, Decode(block, 530));
        }

        [Fact]
        public void LongMatchLength_With255Bytes()
        {
            // 1 literal, offset 1, match 15 + 255 + 255 + 10 + 4 = 539, final literal
            byte[] block = { 0x1F, 3, 0x01, 0x00, 255, 255, 10, 0x10, 4 };
            byte[] expected = Enumerable.Repeat((byte)3, 540).Concat(new byte[] { 4 }).ToArray();
            Assert.Equal(expected, Decode(block, 541));
        }

        [Fact]
        public void LengthOf15_HasZeroExtensionByte()
        {
            // Literal nibble 15 followed by extension 0: exactly 15 literals
            byte[] lits = Enumerable.Range(1, 15).Select(i => (byte)i).ToArray();
            byte[] block = new byte[] { 0xF0, 0 }.Concat(lits).ToArray();
            Assert.Equal(lits, Decode(block, 15));
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(1000, 2)]
        [InlineData(5000, 3)]
        public void TestEncoder_RoundTrips(int length, int seed)
        {
            var rnd = new Random(seed);
            // Mixture of runs, repeats and noise
            byte[] data = new byte[length];
            for (int i = 0; i < length; i++)
                data[i] = i % 97 < 40 ? (byte)(i % 5) : i % 97 < 60 ? (byte)0 : (byte)rnd.Next(256);
            byte[] block = Lz4(data);
            if (length > 100) Assert.True(block.Length < length);
            Assert.Equal(data, Decode(block, length));
        }

        [Fact]
        public void TestEncoder_UsesLongLengths()
        {
            byte[] data = new byte[2000];
            new Random(4).NextBytes(data);
            for (int i = 600; i < 1500; i++) data[i] = 0;
            byte[] block = Lz4(data);
            Assert.Contains((byte)255, block.Skip(1).Take(3)); // 600 literals need 255 length bytes
            Assert.Equal(data, Decode(block, data.Length));
        }

        // Image files

        private static string Format(int bps) => bps == 2 ? "UInt16" : "Float32";

        private static byte[] Samples(int count, int bps, int seed)
        {
            var rnd = new Random(seed);
            if (bps == 4)
                return Enumerable.Range(0, count).SelectMany(i => BitConverter.GetBytes(i % 3 == 0 ? 0.25f : (float)rnd.NextDouble())).ToArray();
            return Enumerable.Range(0, count).SelectMany(i => BitConverter.GetBytes((ushort)(i % 4 == 0 ? 1000 : rnd.Next(65536)))).ToArray();
        }

        [Theory]
        [InlineData("lz4", 2, "Planar", false)]
        [InlineData("lz4", 2, "Normal", true)]
        [InlineData("lz4", 4, "Normal", false)]
        [InlineData("lz4", 4, "Planar", true)]
        [InlineData("lz4hc", 2, "Planar", true)]
        [InlineData("lz4hc", 4, "Normal", false)]
        [InlineData("LZ4HC", 2, "Normal", false)]
        public void Attachment_DecodesToUncompressedPixels(string codec, int bps, string storage, bool shuffle)
        {
            const int w = 13, h = 7, ch = 3;
            byte[] raw = Samples(w * h * ch, bps, bps * 10 + (shuffle ? 1 : 0));
            string attrs = $"geometry=\"{w}:{h}:{ch}\" sampleFormat=\"{Format(bps)}\" pixelStorage=\"{storage}\"";
            string comp = shuffle ? $"{codec}+sh:{raw.Length}:{bps}" : $"{codec}:{raw.Length}";
            byte[] packed = Lz4(shuffle ? XisfCompressionTests.Shuffle(raw, bps) : raw);

            XisfRawFrame expected = Load(Xisf.Attached(attrs, raw));
            XisfRawFrame actual = Load(Xisf.Attached(attrs + $" compression=\"{comp}\"", packed));

            Assert.NotNull(actual);
            Assert.Equal(expected.NormalizedData, actual.NormalizedData);
        }

        [Fact]
        public void Inline_DecodesToUncompressedPixels()
        {
            byte[] raw = Samples(20, 2, 8);
            string attrs = "geometry=\"5:4:1\" sampleFormat=\"UInt16\"";
            string b64 = Convert.ToBase64String(Lz4(XisfCompressionTests.Shuffle(raw, 2)));
            XisfRawFrame expected = Load(Xisf.Attached(attrs, raw));
            XisfRawFrame inline = Load(Xisf.Wrap(
                $"<xisf version=\"1.0\" xmlns=\"{Xisf.Ns}\"><Image {attrs} compression=\"lz4+sh:40:2\" location=\"inline:base64\">{b64}</Image></xisf>"));
            Assert.Equal(expected.NormalizedData, inline.NormalizedData);
        }

        [Fact]
        public void Downscale_MatchesUncompressedDimensions()
        {
            const int w = 257, h = 131;
            byte[] raw = Samples(w * h, 2, 3);
            string attrs = $"geometry=\"{w}:{h}:1\" sampleFormat=\"UInt16\"";
            XisfRawFrame plain = Load(Xisf.Attached(attrs, raw), 64);
            XisfRawFrame packed = Load(Xisf.Attached(attrs + $" compression=\"lz4:{raw.Length}\"", Lz4(raw)), 64);
            Assert.Equal(plain.Width, packed.Width);
            Assert.Equal(plain.Height, packed.Height);
            Assert.Equal(plain.NormalizedData, packed.NormalizedData);
        }

        // Corrupt input

        [Fact]
        public void OffsetBeforeStartOfOutput_ReturnsNull()
        {
            // 2 literals, then offset 3 points before the first output byte
            Assert.Null(Decode(new byte[] { 0x20, 1, 2, 0x03, 0x00, 0x10, 9 }, 7));
            // Offset 0 is invalid
            Assert.Null(Decode(new byte[] { 0x20, 1, 2, 0x00, 0x00, 0x10, 9 }, 7));
            // Match in the very first sequence, with no output yet
            Assert.Null(Decode(new byte[] { 0x00, 0x01, 0x00, 0x10, 9 }, 5));
        }

        [Fact]
        public void LiteralLengthPastEndOfInput_ReturnsNull()
        {
            Assert.Null(Decode(new byte[] { 0x50, 1, 2, 3 }, 5));
            // Huge literal length from many 255 bytes, then end of input
            Assert.Null(Decode(new byte[] { 0xF0, 255, 255, 255, 255, 1, 2 }, 1000));
            // Length extension cut off by end of input
            Assert.Null(Decode(new byte[] { 0xF0, 255 }, 300));
        }

        [Fact]
        public void LiteralLengthPastEndOfOutput_ReturnsNull() =>
            Assert.Null(Decode(new byte[] { 0x50, 1, 2, 3, 4, 5 }, 4));

        [Fact]
        public void MatchLengthPastEndOfOutput_ReturnsNull()
        {
            // 1 literal + match 19 = 20 bytes, declared 10
            Assert.Null(Decode(new byte[] { 0x1F, 7, 0x01, 0x00, 0x00, 0x10, 9 }, 10));
            // Huge match length
            Assert.Null(Decode(new byte[] { 0x1F, 7, 0x01, 0x00, 255, 255, 255, 255, 0, 0x10, 9 }, 100));
        }

        [Fact]
        public void TruncatedOffsetOrMatchLength_ReturnsNull()
        {
            Assert.Null(Decode(new byte[] { 0x14, 7, 0x01 }, 10));
            Assert.Null(Decode(new byte[] { 0x1F, 7, 0x01, 0x00, 255 }, 300));
        }

        [Fact]
        public void OutputShorterThanDeclared_ReturnsNull() =>
            Assert.Null(Decode(new byte[] { 0x50, 1, 2, 3, 4, 5 }, 6));

        [Fact]
        public void OutputLongerThanDeclared_ReturnsNull()
        {
            byte[] data = Samples(200, 2, 1);
            byte[] block = Lz4(data);
            Assert.NotNull(Decode(block, 400));
            Assert.Null(Decode(block, 399));
            Assert.Null(Decode(block, 401));
        }

        [Fact]
        public void EmptyInput_ReturnsNull() => Assert.Null(Decode(new byte[0], 4));

        [Fact]
        public void RandomGarbage_NeverThrows()
        {
            var rnd = new Random(11);
            for (int i = 0; i < 2000; i++)
            {
                byte[] g = new byte[rnd.Next(1, 64)];
                rnd.NextBytes(g);
                int size = rnd.Next(1, 300);
                byte[] r = Decode(g, size);
                Assert.True(r == null || r.Length == size);
            }
        }

        [Fact]
        public void CorruptFile_ReturnsNull()
        {
            byte[] raw = Samples(8, 2, 5);
            byte[] packed = Lz4(raw);
            for (int i = 0; i < packed.Length; i++) packed[i] ^= 0xA5;
            Assert.Null(Load(Xisf.Attached("geometry=\"4:2:1\" sampleFormat=\"UInt16\" compression=\"lz4:16\"", packed)));
        }

        [Fact]
        public void HugeDeclaredSize_InTinyFile_ReturnsNull()
        {
            const int w = 10000, h = 10000, ch = 3;
            long size = (long)w * h * ch * 2;
            byte[] file = Xisf.Attached($"geometry=\"{w}:{h}:{ch}\" sampleFormat=\"UInt16\" compression=\"lz4hc:{size}\"", Lz4(new byte[64]));
            Assert.Null(Load(file));
        }

        [Fact]
        public void AllZeroImage_Decodes()
        {
            // Close to LZ4's maximum ratio, still under the size limit
            byte[] raw = new byte[512 * 512 * 2];
            Assert.NotNull(Load(Xisf.Attached($"geometry=\"512:512:1\" sampleFormat=\"UInt16\" compression=\"lz4:{raw.Length}\"", Lz4Zeros(raw.Length))));
        }

        // Block for all-zero data, built directly (the greedy test encoder is too slow
        // for half a megabyte): one zero literal, an offset 1 match up to 5 bytes before the end,
        // then 5 zero literals.
        private static byte[] Lz4Zeros(int n)
        {
            var output = new List<byte>();
            EmitSequence(output, new byte[n], 0, 1, 1, n - 6);
            EmitSequence(output, new byte[n], 0, 5, 0, 0);
            return output.ToArray();
        }

        [Fact]
        public void Parse_AcceptsLz4Codecs()
        {
            XisfCompressionSpec s = XisfCompressionSpec.Parse("lz4hc+sh:4096:4");
            Assert.Equal("lz4hc", s.Codec);
            Assert.True(s.Shuffled);
            Assert.NotNull(XisfCompressionSpec.Parse("lz4:16"));
            Assert.Null(XisfCompressionSpec.Parse("lz4+sh:16"));
        }
    }
}
