using System;
using System.IO;
using System.Linq;
using System.Text;
using XisfExplorerPreview;
using Xunit;

namespace XisfPreview.Tests
{
    // Compressed <Thumbnail> elements: used for small requests, and any thumbnail that cannot be
    // decoded falls back to the main image.
    public class XisfThumbnailTests
    {
        private const int MainW = 64, MainH = 48, ThumbW = 8, ThumbH = 6, Ch = 3;

        private static XisfRawFrame Load(byte[] file, int target) =>
            XisfParser.LoadRawFrame(new MemoryStream(file), target);

        private static readonly byte[] MainRaw =
            Enumerable.Range(0, MainW * MainH * Ch).SelectMany(i => BitConverter.GetBytes((ushort)(1000 + i % 7))).ToArray();

        private static readonly byte[] ThumbRaw =
            Enumerable.Range(0, ThumbW * ThumbH * Ch).SelectMany(i => BitConverter.GetBytes((ushort)(i * 397))).ToArray();

        private const string ThumbAttrs = "geometry=\"8:6:3\" sampleFormat=\"UInt16\" pixelStorage=\"Planar\"";

        private static byte[] Pack(string codec, byte[] raw)
        {
            bool shuffle = codec.EndsWith("+sh");
            byte[] data = shuffle ? XisfCompressionTests.Shuffle(raw, 2) : raw;
            return codec.StartsWith("zlib") ? XisfCompressionTests.Zlib(data) : XisfLz4Tests.Lz4(data);
        }

        private static string Compression(string codec, int size) =>
            codec.EndsWith("+sh", StringComparison.OrdinalIgnoreCase) ? $"{codec}:{size}:2" : $"{codec}:{size}";

        // Main image as an uncompressed attachment, the thumbnail inline inside it.
        private static byte[] WithInlineThumbnail(string compression, byte[] thumbData) =>
            Xisf.Attached($"geometry=\"{MainW}:{MainH}:{Ch}\" sampleFormat=\"UInt16\" pixelStorage=\"Planar\"", MainRaw,
                $"<Thumbnail {ThumbAttrs} " + (compression == null ? "" : $"compression=\"{compression}\" ") +
                $"location=\"inline:base64\">{Convert.ToBase64String(thumbData)}</Thumbnail>");

        // Main image embedded, the thumbnail as an attachment; the offset has a fixed width.
        private static byte[] WithAttachedThumbnail(string compression, byte[] thumbData)
        {
            string Xml(long pos) =>
                $"<xisf version=\"1.0\" xmlns=\"{Xisf.Ns}\">" +
                $"<Image geometry=\"{MainW}:{MainH}:{Ch}\" sampleFormat=\"UInt16\" location=\"embedded\">" +
                $"<Data encoding=\"base64\">{Convert.ToBase64String(MainRaw)}</Data>" +
                $"<Thumbnail {ThumbAttrs} compression=\"{compression}\" location=\"attachment:{pos:D12}:{thumbData.Length}\"/>" +
                "</Image></xisf>";
            return Xisf.Wrap(Xml(16 + Encoding.UTF8.GetByteCount(Xml(0))), thumbData);
        }

        [Theory]
        [InlineData("zlib", 96)]
        [InlineData("zlib+sh", 256)]
        [InlineData("lz4", 96)]
        [InlineData("lz4+sh", 256)]
        [InlineData("lz4hc", 512)]
        [InlineData("lz4hc+sh", 16)]
        [InlineData("ZLIB+SH", 96)]
        public void CompressedInlineThumbnail_UsedForSmallRequests(string codec, int target)
        {
            XisfRawFrame expected = Load(WithInlineThumbnail(null, ThumbRaw), target);
            XisfRawFrame actual = Load(WithInlineThumbnail(Compression(codec, ThumbRaw.Length), Pack(codec.ToLowerInvariant(), ThumbRaw)), target);

            Assert.NotNull(actual);
            Assert.Equal(ThumbW, actual.Width);
            Assert.Equal(ThumbH, actual.Height);
            Assert.Equal(expected.NormalizedData, actual.NormalizedData);
        }

        [Theory]
        [InlineData("zlib+sh")]
        [InlineData("lz4")]
        public void CompressedAttachedThumbnail_UsedForSmallRequests(string codec)
        {
            XisfRawFrame expected = Load(WithInlineThumbnail(null, ThumbRaw), 96);
            XisfRawFrame actual = Load(WithAttachedThumbnail(Compression(codec, ThumbRaw.Length), Pack(codec, ThumbRaw)), 96);

            Assert.NotNull(actual);
            Assert.Equal(ThumbW, actual.Width);
            Assert.Equal(expected.NormalizedData, actual.NormalizedData);
        }

        [Theory]
        [InlineData("zlib", 0)]
        [InlineData("lz4+sh", 513)]
        [InlineData("lz4hc", 1024)]
        public void CompressedThumbnail_NotUsedForLargeRequests(string codec, int target)
        {
            XisfRawFrame f = Load(WithInlineThumbnail(Compression(codec, ThumbRaw.Length), Pack(codec, ThumbRaw)), target);

            Assert.NotNull(f);
            Assert.Equal(MainW, f.Width);
            Assert.Equal(MainH, f.Height);
        }

        public static TheoryData<string, byte[]> Unusable()
        {
            byte[] zlib = XisfCompressionTests.Zlib(ThumbRaw);
            byte[] corruptZlib = (byte[])zlib.Clone();
            corruptZlib[corruptZlib.Length / 2] ^= 0x5A;
            byte[] lz4 = XisfLz4Tests.Lz4(ThumbRaw);
            return new TheoryData<string, byte[]>
            {
                { $"zstd:{ThumbRaw.Length}", zlib },
                { $"zstd+sh:{ThumbRaw.Length}:2", zlib },
                { $"brotli:{ThumbRaw.Length}", zlib },
                { $"zlib:{ThumbRaw.Length}", corruptZlib },
                { $"zlib:{ThumbRaw.Length}", zlib.Take(zlib.Length - 10).ToArray() },
                { $"lz4:{ThumbRaw.Length}", lz4.Take(lz4.Length / 2).ToArray() },
                { $"lz4:{ThumbRaw.Length + 2}", lz4 },
                { $"zlib+sh:{ThumbRaw.Length}:0", zlib },
                { "zlib", zlib },
            };
        }

        [Theory]
        [MemberData(nameof(Unusable))]
        public void UnusableCompressedThumbnail_FallsBackToMainImage(string compression, byte[] data)
        {
            XisfRawFrame expected = Load(WithInlineThumbnail(null, ThumbRaw), 600);
            XisfRawFrame inline = Load(WithInlineThumbnail(compression, data), 96);
            XisfRawFrame attached = Load(WithAttachedThumbnail(compression, data), 256);

            Assert.NotNull(inline);
            Assert.Equal(MainW, inline.Width);
            Assert.Equal(MainH, inline.Height);
            Assert.Equal(expected.NormalizedData, inline.NormalizedData);
            Assert.NotNull(attached);
            Assert.Equal(MainW, attached.Width);
        }

        [Fact]
        public void ThumbnailPastEndOfFile_FallsBackToMainImage()
        {
            byte[] file = WithAttachedThumbnail($"zlib:{ThumbRaw.Length}", XisfCompressionTests.Zlib(ThumbRaw));
            XisfRawFrame f = Load(file.Take(file.Length - 3).ToArray(), 96);

            Assert.NotNull(f);
            Assert.Equal(MainW, f.Width);
        }

        [Fact]
        public void BadBase64Thumbnail_FallsBackToMainImage()
        {
            byte[] file = Xisf.Attached($"geometry=\"{MainW}:{MainH}:{Ch}\" sampleFormat=\"UInt16\"", MainRaw,
                $"<Thumbnail {ThumbAttrs} compression=\"zlib:{ThumbRaw.Length}\" location=\"inline:base64\">!!not base64!!</Thumbnail>");
            XisfRawFrame f = Load(file, 96);

            Assert.NotNull(f);
            Assert.Equal(MainW, f.Width);
        }

        // Decode failures fall back for every thumbnail, not only compressed ones.
        [Fact]
        public void TruncatedUncompressedThumbnail_FallsBackToMainImage()
        {
            XisfRawFrame f = Load(WithInlineThumbnail(null, ThumbRaw.Take(10).ToArray()), 96);

            Assert.NotNull(f);
            Assert.Equal(MainW, f.Width);
        }
    }
}
