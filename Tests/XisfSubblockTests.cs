using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XisfExplorerPreview;
using Xunit;

namespace XisfPreview.Tests
{
    // Compressed data blocks divided into subblocks (XISF 1.0 section 10.6, subblocks attribute).
    public class XisfSubblockTests
    {
        private const int W = 64, H = 48, Ch = 3;

        private static XisfRawFrame Load(byte[] file, int target = 0) =>
            XisfParser.LoadRawFrame(new MemoryStream(file), target);

        // Smooth gradients with a little noise: compressible, so no compressed subblock comes
        // out the same length as its input (which would mean "stored" to the decoder).
        private static byte[] Samples(int count, int bps, int seed)
        {
            var rnd = new Random(seed);
            if (bps == 4)
                return Enumerable.Range(0, count)
                    .SelectMany(i => BitConverter.GetBytes((float)((i % 97) / 97.0 + rnd.Next(4) / 1000.0))).ToArray();
            return Enumerable.Range(0, count)
                .SelectMany(i => BitConverter.GetBytes((ushort)(i % 251 * 200 + rnd.Next(3)))).ToArray();
        }

        private static string Attrs(int bps) =>
            $"geometry=\"{W}:{H}:{Ch}\" sampleFormat=\"{XisfCompressionTests.Format(bps)}\" pixelStorage=\"Planar\"";

        private static byte[] Encode(string codec, byte[] data) =>
            codec.StartsWith("zlib") ? XisfCompressionTests.Zlib(data) : XisfLz4Tests.Lz4(data);

        // Shuffles the whole block (for +sh), splits it at the given uncompressed lengths and
        // compresses each piece on its own; pieces listed in 'stored' are kept uncompressed.
        private static byte[] Pack(string codec, byte[] raw, int itemSize, int[] sizes, out string subblocks, params int[] stored)
        {
            Assert.Equal(raw.Length, sizes.Sum());
            byte[] data = codec.EndsWith("+sh") ? XisfCompressionTests.Shuffle(raw, itemSize) : raw;
            var output = new List<byte>();
            var pairs = new List<string>();
            int pos = 0;
            for (int i = 0; i < sizes.Length; i++)
            {
                byte[] piece = data.Skip(pos).Take(sizes[i]).ToArray();
                byte[] packed = stored.Contains(i) ? piece : Encode(codec, piece);
                if (!stored.Contains(i)) Assert.NotEqual(piece.Length, packed.Length);
                output.AddRange(packed);
                pairs.Add($"{packed.Length},{piece.Length}");
                pos += sizes[i];
            }
            subblocks = string.Join(":", pairs);
            return output.ToArray();
        }

        private static string Compression(string codec, int size, int itemSize) =>
            codec.EndsWith("+sh") ? $"{codec}:{size}:{itemSize}" : $"{codec}:{size}";

        // Uncompressed lengths with boundaries that do not fall on item boundaries.
        private static int[] Split(int total, int count) =>
            count == 2 ? new[] { 4097, total - 4097 } : new[] { 1001, 6668, total - 7669 };

        private static byte[] AttachedFile(string codec, int bps, byte[] raw, int count, params int[] stored)
        {
            byte[] packed = Pack(codec, raw, bps, Split(raw.Length, count), out string sub, stored);
            return Xisf.Attached(Attrs(bps) + $" compression=\"{Compression(codec, raw.Length, bps)}\" subblocks=\"{sub}\"", packed);
        }

        [Theory]
        [InlineData("zlib", 2, 2)]
        [InlineData("zlib", 4, 3)]
        [InlineData("zlib+sh", 2, 2)]
        [InlineData("zlib+sh", 4, 3)]
        [InlineData("lz4", 2, 3)]
        [InlineData("lz4", 4, 2)]
        [InlineData("lz4hc+sh", 2, 3)]
        [InlineData("lz4hc+sh", 4, 2)]
        public void Attachment_DecodesToUncompressedPixels(string codec, int bps, int count)
        {
            byte[] raw = Samples(W * H * Ch, bps, count * 10 + bps);
            byte[] file = AttachedFile(codec, bps, raw, count);

            foreach (int target in new[] { 0, 16 })
            {
                XisfRawFrame expected = Load(Xisf.Attached(Attrs(bps), raw), target);
                XisfRawFrame actual = Load(file, target);

                Assert.NotNull(actual);
                Assert.Equal(expected.Width, actual.Width);
                Assert.Equal(expected.Height, actual.Height);
                Assert.Equal(expected.NormalizedData, actual.NormalizedData);
            }
        }

        [Theory]
        [InlineData("zlib+sh", 0)]
        [InlineData("lz4", 1)]
        [InlineData("lz4hc+sh", 2)]
        public void MixedStoredAndCompressedSubblocks_Decode(string codec, int storedIndex)
        {
            byte[] raw = Samples(W * H * Ch, 2, 5);
            byte[] file = AttachedFile(codec, 2, raw, 3, storedIndex);

            foreach (int target in new[] { 0, 16 })
            {
                XisfRawFrame expected = Load(Xisf.Attached(Attrs(2), raw), target);
                XisfRawFrame actual = Load(file, target);

                Assert.NotNull(actual);
                Assert.Equal(expected.Width, actual.Width);
                Assert.Equal(expected.Height, actual.Height);
                Assert.Equal(expected.NormalizedData, actual.NormalizedData);
            }
        }

        [Theory]
        [InlineData("zlib")]
        [InlineData("lz4+sh")]
        public void InlineAndEmbedded_Decode(string codec)
        {
            byte[] raw = Samples(W * H * Ch, 2, 9);
            byte[] packed = Pack(codec, raw, 2, Split(raw.Length, 3), out string sub);
            string attrs = Attrs(2) + $" compression=\"{Compression(codec, raw.Length, 2)}\" subblocks=\"{sub}\"";
            string b64 = Convert.ToBase64String(packed);
            string Doc(string body) => $"<xisf version=\"1.0\" xmlns=\"{Xisf.Ns}\">{body}</xisf>";

            XisfRawFrame expected = Load(Xisf.Attached(Attrs(2), raw));
            XisfRawFrame inline = Load(Xisf.Wrap(Doc($"<Image {attrs} location=\"inline:base64\">{b64}</Image>")));
            XisfRawFrame embedded = Load(Xisf.Wrap(Doc(
                $"<Image {attrs} location=\"embedded\"><Data encoding=\"base64\">{b64}</Data></Image>")));

            Assert.NotNull(inline);
            Assert.Equal(expected.NormalizedData, inline.NormalizedData);
            Assert.NotNull(embedded);
            Assert.Equal(expected.NormalizedData, embedded.NormalizedData);
        }

        private const string ThumbAttrs = "geometry=\"16:12:3\" sampleFormat=\"UInt16\" pixelStorage=\"Planar\"";

        private static readonly byte[] ThumbRaw = Samples(16 * 12 * 3, 2, 77);

        // Main image as an uncompressed attachment, the thumbnail inline inside it.
        private static byte[] WithThumbnail(string thumbAttrs, byte[] thumbData) =>
            Xisf.Attached(Attrs(2), Samples(W * H * Ch, 2, 1),
                $"<Thumbnail {ThumbAttrs} {thumbAttrs} location=\"inline:base64\">{Convert.ToBase64String(thumbData)}</Thumbnail>");

        [Theory]
        [InlineData("zlib+sh")]
        [InlineData("lz4hc")]
        public void CompressedThumbnailWithSubblocks_UsedForSmallRequests(string codec)
        {
            byte[] packed = Pack(codec, ThumbRaw, 2, new[] { 333, 401, ThumbRaw.Length - 734 }, out string sub);
            byte[] file = WithThumbnail(
                $"compression=\"{Compression(codec, ThumbRaw.Length, 2)}\" subblocks=\"{sub}\"", packed);

            // 96: the thumbnail at full size; 8: the thumbnail downscaled.
            foreach (int target in new[] { 96, 8 })
            {
                XisfRawFrame expected = Load(WithThumbnail("", ThumbRaw), target);
                XisfRawFrame actual = Load(file, target);

                Assert.NotNull(actual);
                Assert.True(actual.Width <= 16 && actual.Height <= 12); // the thumbnail, not the 64x48 image
                Assert.Equal(expected.Width, actual.Width);
                Assert.Equal(expected.Height, actual.Height);
                Assert.Equal(expected.NormalizedData, actual.NormalizedData);
            }
            Assert.Equal(16, Load(file, 96).Width);
        }

        // Each case: a subblocks attribute and the stored bytes, built from a valid two-subblock
        // zlib block with pieces of 4097 and n - 4097 bytes.
        public static TheoryData<string, string> BadSubblocks()
        {
            byte[] raw = Samples(W * H * Ch, 2, 3);
            int n = raw.Length;
            byte[] a = XisfCompressionTests.Zlib(raw.Take(4097).ToArray());
            byte[] b = XisfCompressionTests.Zlib(raw.Skip(4097).ToArray());
            int ca = a.Length, cb = b.Length, ua = 4097, ub = n - 4097;
            string ok = Convert.ToBase64String(a.Concat(b).ToArray());
            byte[] corrupt = a.Concat(b).ToArray();
            corrupt[ca + cb / 2] ^= 0x5A;
            return new TheoryData<string, string>
            {
                { $"{ca}{ua}:{cb},{ub}", ok },                       // missing comma
                { $"{ca},{ua}::{cb},{ub}", ok },                     // empty pair
                { $"{ca},{ua}:{cb},{ub}:", ok },                     // trailing empty pair
                { $"{ca},x{ua}:{cb},{ub}", ok },                     // non-numeric
                { $"{ca},{ua}:{cb},-{ub}", ok },                     // negative
                { $"{ca},{ua},1:{cb},{ub}", ok },                    // three numbers
                { "", ok },                                          // zero pairs
                { $"{ca},{ua}:0,0:{cb},{ub}", ok },                  // empty subblock
                { $"{ca},{ua}:{cb},{ub + 2}", ok },                  // uncompressed sum too large
                { $"{ca},{ua}:{cb},{ub - 2}", ok },                  // uncompressed sum too small
                { $"{ca},{ua}:{cb - 1},{ub}", ok },                  // compressed sum short of the block
                { $"{ca},{ua}:{cb + 1},{ub}", ok },                  // last subblock runs past the end
                { $"{ca + 1},{ua}:{cb - 1},{ub}", ok },              // boundary in the wrong place
                { $"{ca},{ua + 2}:{cb},{ub - 2}", ok },              // first gives fewer bytes than ui
                { $"{ca},{ua - 2}:{cb},{ub + 2}", ok },              // first gives more bytes than ui
                { $"{ca},{ua}:{cb},{ub}", Convert.ToBase64String(corrupt) }, // corrupt subblock
            };
        }

        [Theory]
        [MemberData(nameof(BadSubblocks))]
        public void BadSubblocks_GiveNull(string subblocks, string base64)
        {
            byte[] packed = Convert.FromBase64String(base64);
            int n = W * H * Ch * 2;
            XisfRawFrame f = Load(Xisf.Attached(Attrs(2) + $" compression=\"zlib:{n}\" subblocks=\"{subblocks}\"", packed));

            Assert.Null(f);
        }

        [Fact]
        public void ValidSubblocksOfBadCases_Decode()
        {
            byte[] raw = Samples(W * H * Ch, 2, 3);
            byte[] a = XisfCompressionTests.Zlib(raw.Take(4097).ToArray());
            byte[] b = XisfCompressionTests.Zlib(raw.Skip(4097).ToArray());
            XisfRawFrame f = Load(Xisf.Attached(
                Attrs(2) + $" compression=\"zlib:{raw.Length}\" subblocks=\"{a.Length},4097:{b.Length},{raw.Length - 4097}\"",
                a.Concat(b).ToArray()));

            Assert.NotNull(f);
        }

        [Theory]
        [InlineData("zlib")]
        [InlineData("lz4")]
        public void BadThumbnailSubblocks_FallBackToMainImage(string codec)
        {
            byte[] packed = Pack(codec, ThumbRaw, 2, new[] { 333, ThumbRaw.Length - 333 }, out string sub);
            string wrong = sub.Replace(",333:", ",335:");
            XisfRawFrame f = Load(WithThumbnail($"compression=\"{codec}:{ThumbRaw.Length}\" subblocks=\"{wrong}\"", packed), 96);

            Assert.NotNull(f);
            Assert.Equal(W, f.Width);
            Assert.Equal(H, f.Height);
        }

        // Each LZ4 subblock is independent: a match may not reach back into an earlier subblock.
        [Fact]
        public void Lz4MatchIntoPreviousSubblock_GivesNull()
        {
            // Second block: token with no literals and a match of 5 at offset 4, then 5 literals
            // (9 stored bytes restoring 10, so it is not taken for an uncompressed subblock).
            byte[] first = { 0x40, 1, 2, 3, 4 };
            byte[] second = { 0x01, 4, 0, 0x50, 9, 9, 9, 9, 9 };
            var spec = XisfCompressionSpec.Parse("lz4:14", $"{first.Length},4:{second.Length},10");

            Assert.Null(XisfCompression.Decompress(spec, first.Concat(second).ToArray()));
        }

        [Fact]
        public void Decompress_WithSubblocks_Works()
        {
            byte[] first = { 0x40, 1, 2, 3, 4 };
            byte[] second = { 0x50, 5, 6, 7, 8, 9 };
            var spec = XisfCompressionSpec.Parse("lz4:9", $"{first.Length},4:{second.Length},5");

            Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, XisfCompression.Decompress(spec, first.Concat(second).ToArray()));
        }

        // Declared sizes far beyond what the stored bytes can restore are rejected before the
        // output buffer is allocated; the codec's bound applies to each compressed subblock.
        [Theory]
        [InlineData("zlib:2000000000", "20,1000000000:20,1000000000")]
        [InlineData("lz4:1000000", "10,999990:10,10")]
        [InlineData("zlib:2147483647", "40,2147483647")]
        public void OutOfProportionSizes_AreRejected(string compression, string subblocks)
        {
            var spec = XisfCompressionSpec.Parse(compression, subblocks);
            Assert.NotNull(spec);
            int stored = subblocks.Split(':').Sum(p => int.Parse(p.Split(',')[0]));

            Assert.Null(XisfCompression.Decompress(spec, new byte[stored]));
        }

        [Theory]
        [InlineData("zlib:10", "4,4:6,7")]
        [InlineData("zlib:10", "4,4:6,5")]
        [InlineData("zlib:10", "4,3000000000:6,6")]
        [InlineData("zlib:10", " ")]
        [InlineData("zlib:10", ",")]
        public void Parse_RejectsBadSubblocks(string compression, string subblocks)
        {
            Assert.Null(XisfCompressionSpec.Parse(compression, subblocks));
        }

        [Fact]
        public void Parse_WithoutSubblocks_HasNone()
        {
            Assert.Null(XisfCompressionSpec.Parse("zlib:10").Subblocks);
            Assert.Equal(2, XisfCompressionSpec.Parse("zlib:10", "4,4:5,6").Subblocks.Length);
        }
    }
}
