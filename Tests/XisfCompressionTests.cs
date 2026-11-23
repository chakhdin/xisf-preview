using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using XisfExplorerPreview;
using Xunit;

namespace XisfPreview.Tests
{
    public class XisfCompressionTests
    {
        private static XisfRawFrame Load(byte[] file, int target = 0) =>
            XisfParser.LoadRawFrame(new MemoryStream(file), target);

        private static string Doc(string body) => $"<xisf version=\"1.0\" xmlns=\"{Xisf.Ns}\">{body}</xisf>";

        // zlib stream (RFC 1950): header 78 9C, raw deflate, big-endian Adler-32 of the input.
        internal static byte[] Zlib(byte[] data)
        {
            using (var ms = new MemoryStream())
            {
                ms.WriteByte(0x78);
                ms.WriteByte(0x9C);
                using (var d = new DeflateStream(ms, CompressionMode.Compress, true))
                    d.Write(data, 0, data.Length);
                uint a = 1, b = 0;
                foreach (byte x in data) { a = (a + x) % 65521; b = (b + a) % 65521; }
                uint adler = (b << 16) | a;
                ms.WriteByte((byte)(adler >> 24)); ms.WriteByte((byte)(adler >> 16));
                ms.WriteByte((byte)(adler >> 8)); ms.WriteByte((byte)adler);
                return ms.ToArray();
            }
        }

        // XISF byte shuffling, written independently of the decoder: all first bytes of every
        // item, then all second bytes, ...; the trailing length % itemSize bytes stay as they are.
        internal static byte[] Shuffle(byte[] data, int itemSize)
        {
            int n = data.Length / itemSize;
            var result = new byte[data.Length];
            int k = 0;
            for (int j = 0; j < itemSize; j++)
                for (int i = 0; i < n; i++)
                    result[k++] = data[i * itemSize + j];
            for (int i = n * itemSize; i < data.Length; i++) result[k++] = data[i];
            return result;
        }

        private static byte[] RandomBytes(int count, int seed)
        {
            var b = new byte[count];
            new Random(seed).NextBytes(b);
            return b;
        }

        private static string Format(int bps) => bps == 1 ? "UInt8" : bps == 2 ? "UInt16" : "Float32";

        // Float32 samples in 0..1 so the normalized values are comparable.
        private static byte[] Samples(int count, int bps, int seed)
        {
            if (bps != 4) return RandomBytes(count * bps, seed);
            var rnd = new Random(seed);
            return Enumerable.Range(0, count).SelectMany(_ => BitConverter.GetBytes((float)rnd.NextDouble())).ToArray();
        }

        private static string Codec(bool shuffle, int bps) => shuffle ? $"zlib+sh:{{0}}:{bps}" : "zlib:{0}";

        [Theory]
        [InlineData(1, "Planar", false)]
        [InlineData(1, "Normal", true)]
        [InlineData(2, "Planar", false)]
        [InlineData(2, "Planar", true)]
        [InlineData(2, "Normal", true)]
        [InlineData(4, "Normal", false)]
        [InlineData(4, "Planar", true)]
        public void Attachment_DecodesToUncompressedPixels(int bps, string storage, bool shuffle)
        {
            const int w = 13, h = 7, ch = 3;
            byte[] raw = Samples(w * h * ch, bps, bps * 10 + (shuffle ? 1 : 0));
            string attrs = $"geometry=\"{w}:{h}:{ch}\" sampleFormat=\"{Format(bps)}\" pixelStorage=\"{storage}\"";
            byte[] packed = Zlib(shuffle ? Shuffle(raw, bps) : raw);

            XisfRawFrame expected = Load(Xisf.Attached(attrs, raw));
            XisfRawFrame actual = Load(Xisf.Attached(attrs + $" compression=\"{string.Format(Codec(shuffle, bps), raw.Length)}\"", packed));

            Assert.NotNull(actual);
            Assert.Equal(expected.NormalizedData, actual.NormalizedData);
        }

        [Theory]
        [InlineData(2, false)]
        [InlineData(2, true)]
        [InlineData(4, true)]
        public void InlineAndEmbedded_DecodeToUncompressedPixels(int bps, bool shuffle)
        {
            const int w = 5, h = 4, ch = 1;
            byte[] raw = Samples(w * h * ch, bps, 7);
            string attrs = $"geometry=\"{w}:{h}:{ch}\" sampleFormat=\"{Format(bps)}\"";
            string comp = string.Format(Codec(shuffle, bps), raw.Length);
            string b64 = Convert.ToBase64String(Zlib(shuffle ? Shuffle(raw, bps) : raw));

            XisfRawFrame expected = Load(Xisf.Attached(attrs, raw));
            XisfRawFrame inline = Load(Xisf.Wrap(Doc($"<Image {attrs} compression=\"{comp}\" location=\"inline:base64\">{b64}</Image>")));
            XisfRawFrame embedded = Load(Xisf.Wrap(Doc(
                $"<Image {attrs} compression=\"{comp}\" location=\"embedded\"><Data encoding=\"base64\">{b64}</Data></Image>")));

            Assert.Equal(expected.NormalizedData, inline.NormalizedData);
            Assert.Equal(expected.NormalizedData, embedded.NormalizedData);
        }

        [Fact]
        public void BigEndian_IsSwappedAfterUnshuffling()
        {
            byte[] be = { 0x00, 0x01, 0x12, 0x34 };
            XisfRawFrame f = Load(Xisf.Attached(
                "geometry=\"2:1:1\" sampleFormat=\"UInt16\" byteOrder=\"big\" compression=\"zlib+sh:4:2\"", Zlib(Shuffle(be, 2))));
            Assert.Equal(1f / 65535f, f.NormalizedData[0], 7);
            Assert.Equal(0x1234 / 65535f, f.NormalizedData[1], 7);
        }

        [Theory]
        [InlineData(16)]
        [InlineData(100)]
        public void Downscale_MatchesUncompressedDimensions(int target)
        {
            const int w = 257, h = 131, ch = 3;
            byte[] raw = Samples(w * h * ch, 2, 3);
            string attrs = $"geometry=\"{w}:{h}:{ch}\" sampleFormat=\"UInt16\" pixelStorage=\"Planar\"";

            XisfRawFrame plain = Load(Xisf.Attached(attrs, raw), target);
            XisfRawFrame packed = Load(Xisf.Attached(attrs + $" compression=\"zlib+sh:{raw.Length}:2\"", Zlib(Shuffle(raw, 2))), target);

            Assert.Equal(plain.Width, packed.Width);
            Assert.Equal(plain.Height, packed.Height);
            Assert.Equal(plain.NormalizedData, packed.NormalizedData);
        }

        private static byte[] U16File(string compression, byte[] packed, long? declaredSize = null) =>
            Xisf.Attached($"geometry=\"4:2:1\" sampleFormat=\"UInt16\" compression=\"{compression}\"", packed, declaredSize: declaredSize);

        private static readonly byte[] Raw16 = RandomBytes(16, 5);

        [Fact]
        public void ValidFile_Decodes() => Assert.NotNull(Load(U16File("zlib:16", Zlib(Raw16))));

        [Theory]
        [InlineData("zlib:15")]
        [InlineData("zlib:17")]
        [InlineData("zlib:0")]
        [InlineData("zlib:-16")]
        [InlineData("zlib:abc")]
        [InlineData("zlib")]
        [InlineData("zstd:16")]
        [InlineData("zstd+sh:16:2")]
        [InlineData("bzip2:16")]
        [InlineData("zlib+sh:16")]
        [InlineData("zlib+sh:16:0")]
        [InlineData("zlib+sh:16:-2")]
        [InlineData("zlib+sh:16:x")]
        [InlineData("zlib+sh:16:1000")]
        [InlineData("zlib:16:2:1")]
        public void BadCompressionAttribute_ReturnsNull(string compression) =>
            Assert.Null(Load(U16File(compression, Zlib(Raw16))));

        [Fact]
        public void HugeDeclaredSize_ReturnsNullWithoutAllocating()
        {
            // 60000 x 60000 x 4 Float32 would be about 57 GB; geometry and declared size agree.
            long size = 60000L * 60000 * 4 * 4;
            byte[] file = Xisf.Attached($"geometry=\"60000:60000:4\" sampleFormat=\"Float32\" compression=\"zlib:{size}\"", Zlib(new byte[64]));
            Assert.Null(Load(file));

            // Within the pixel cap but still far beyond int.MaxValue bytes
            size = 17000L * 17000 * 3 * 4;
            file = Xisf.Attached($"geometry=\"17000:17000:3\" sampleFormat=\"Float32\" compression=\"zlib:{size}\"", Zlib(new byte[64]));
            Assert.Null(Load(file));
        }

        [Fact]
        public void HugeDeclaredSize_InTinyFile_ReturnsNull()
        {
            // Matching geometry (about 600 MB) declared by a file of a few hundred bytes: the
            // size is rejected before the output buffer is allocated.
            const int w = 10000, h = 10000, ch = 3;
            long size = (long)w * h * ch * 2;
            byte[] file = Xisf.Attached($"geometry=\"{w}:{h}:{ch}\" sampleFormat=\"UInt16\" compression=\"zlib:{size}\"", Zlib(new byte[64]));
            Assert.Null(Load(file));
        }

        [Fact]
        public void HighlyCompressibleData_Decodes()
        {
            // All-zero image near the real deflate ratio stays accepted.
            byte[] raw = new byte[512 * 512 * 2];
            XisfRawFrame f = Load(Xisf.Attached($"geometry=\"512:512:1\" sampleFormat=\"UInt16\" compression=\"zlib:{raw.Length}\"", Zlib(raw)));
            Assert.NotNull(f);
        }

        [Fact]
        public void CompressedSizePastEof_ReturnsNull()
        {
            byte[] packed = Zlib(Raw16);
            Assert.Null(Load(U16File("zlib:16", packed, declaredSize: packed.Length + 1)));
        }

        [Fact]
        public void Truncated_ReturnsNull()
        {
            byte[] packed = Zlib(Raw16);
            byte[] cut = packed.Take(packed.Length / 2).ToArray();
            Assert.Null(Load(U16File("zlib:16", cut)));
        }

        [Fact]
        public void Corrupt_ReturnsNull()
        {
            byte[] packed = Zlib(Raw16);
            for (int i = 2; i < packed.Length; i++) packed[i] ^= 0xA5;
            Assert.Null(Load(U16File("zlib:16", packed)));
        }

        [Fact]
        public void WrongAdler32Trailer_ReturnsNull()
        {
            // The deflate data inflates cleanly, but the checksum of the restored bytes differs.
            byte[] packed = Zlib(Raw16);
            packed[packed.Length - 1] ^= 0x01;
            Assert.Null(Load(U16File("zlib:16", packed)));
        }

        [Fact]
        public void MissingAdler32Trailer_ReturnsNull()
        {
            byte[] packed = Zlib(Raw16);
            Assert.Null(Load(U16File("zlib:16", packed.Take(packed.Length - 4).ToArray())));
        }

        [Fact]
        public void Adler32_IsCheckedOnLargeBlocks()
        {
            // More than one 5552-byte Adler-32 batch, with high byte values
            byte[] raw = Enumerable.Range(0, 300 * 100 * 2).Select(i => (byte)(255 - i % 7)).ToArray();
            XisfRawFrame f = Load(Xisf.Attached($"geometry=\"300:100:1\" sampleFormat=\"UInt16\" compression=\"zlib:{raw.Length}\"", Zlib(raw)));
            Assert.NotNull(f);
        }

        [Fact]
        public void BadZlibHeader_ReturnsNull()
        {
            byte[] packed = Zlib(Raw16);
            packed[0] = 0x00;
            Assert.Null(Load(U16File("zlib:16", packed)));
        }

        [Fact]
        public void StreamInflatingPastDeclaredSize_ReturnsNull()
        {
            byte[] longer = Zlib(RandomBytes(32, 5));
            Assert.Null(Load(U16File("zlib:16", longer)));
            Assert.Null(XisfCompression.Decompress(XisfCompressionSpec.Parse("zlib:16"), longer));
        }

        [Fact]
        public void StreamShorterThanDeclaredSize_ReturnsNull() =>
            Assert.Null(Load(U16File("zlib:16", Zlib(RandomBytes(8, 5)))));

        [Fact]
        public void Garbage_ReturnsNull()
        {
            Assert.Null(Load(U16File("zlib:16", new byte[] { 0x78, 0x9C })));
            Assert.Null(Load(U16File("zlib:16", new byte[] { 0x78 })));
            Assert.Null(Load(U16File("zlib:16", RandomBytes(40, 9))));
        }

        [Theory]
        [InlineData(2, 10)]
        [InlineData(4, 18)] // trailing 2 bytes left unshuffled
        [InlineData(3, 1)]
        [InlineData(8, 64)]
        public void Unshuffle_InvertsShuffle(int itemSize, int length)
        {
            byte[] data = RandomBytes(length, length);
            Assert.Equal(data, XisfCompression.Unshuffle(Shuffle(data, itemSize), itemSize));
        }

        [Fact]
        public void Unshuffle_KnownLayout()
        {
            // Items AB CD EF plus trailing X stored as A C E B D F X
            byte[] stored = { 1, 3, 5, 2, 4, 6, 9 };
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 9 }, XisfCompression.Unshuffle(stored, 2));
        }

        [Fact]
        public void Parse_ReadsCodecSizeAndItemSize()
        {
            XisfCompressionSpec s = XisfCompressionSpec.Parse("zlib+sh:4096:4");
            Assert.Equal("zlib", s.Codec);
            Assert.True(s.Shuffled);
            Assert.Equal(4096, s.UncompressedSize);
            Assert.Equal(4, s.ItemSize);
        }
    }
}
