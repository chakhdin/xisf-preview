using System;
using System.IO;
using System.Linq;
using System.Text;
using XisfExplorerPreview;
using Xunit;

namespace XisfPreview.Tests
{
    public class XisfParserTests
    {
        private static XisfRawFrame Load(byte[] file, int target = 0) =>
            XisfParser.LoadRawFrame(new MemoryStream(file), target);

        private static string Doc(string body) => $"<xisf version=\"1.0\" xmlns=\"{Xisf.Ns}\">{body}</xisf>";

        [Fact]
        public void UInt8_Normalizes()
        {
            XisfRawFrame f = Load(Xisf.Attached("geometry=\"2:1:1\" sampleFormat=\"UInt8\"", new byte[] { 0, 255 }));
            Assert.Equal(new[] { 0f, 1f }, f.NormalizedData);
        }

        [Fact]
        public void UInt16_Normalizes()
        {
            XisfRawFrame f = Load(Xisf.U16(2, 1, 1, new ushort[] { 0, 65535 }));
            Assert.Equal(new[] { 0f, 1f }, f.NormalizedData);
        }

        [Fact]
        public void Float32_ClampsNonFinite()
        {
            XisfRawFrame f = Load(Xisf.F32(4, 1, 1, new[] { 0.5f, float.NaN, -1f, 2f }));
            Assert.Equal(new[] { 0.5f, 0f, 0f, 1f }, f.NormalizedData);
        }

        // The spec requires a bounds attribute for float images; data outside 0..1 must be
        // rescaled by it, not clipped to white.
        [Fact]
        public void Float32_HonorsBoundsAttribute()
        {
            XisfRawFrame f = Load(Xisf.F32(2, 1, 1, new[] { 1000f, 40000f }, "bounds=\"0:65535\""));
            Assert.Equal(1000f / 65535f, f.NormalizedData[0], 4);
            Assert.Equal(40000f / 65535f, f.NormalizedData[1], 4);
        }

        [Fact]
        public void Planar_AndNormal_DecodeToSamePixels()
        {
            ushort[] planar = { 10, 20, 30, 40, 50, 60 };    // RR GG BB
            ushort[] normal = { 10, 30, 50, 20, 40, 60 };    // RGB RGB
            XisfRawFrame a = Load(Xisf.U16(2, 1, 3, planar, "Planar"));
            XisfRawFrame b = Load(Xisf.U16(2, 1, 3, normal, "Normal"));
            Assert.Equal(a.NormalizedData, b.NormalizedData);
        }

        [Fact]
        public void PixelStorage_DefaultsToPlanar()
        {
            XisfRawFrame f = Load(Xisf.Attached("geometry=\"1:1:2\" sampleFormat=\"UInt8\"", new byte[] { 255, 0 }));
            Assert.Equal(new[] { 1f, 0f }, f.NormalizedData);
        }

        [Fact]
        public void RowsAreTopDown()
        {
            XisfRawFrame f = Load(Xisf.Attached("geometry=\"1:2:1\" sampleFormat=\"UInt8\"", new byte[] { 0, 255 }));
            Assert.Equal(new[] { 0f, 1f }, f.NormalizedData);
        }

        [Fact]
        public void InlineBase64_IsDecoded()
        {
            string b64 = Convert.ToBase64String(new byte[] { 0, 255 });
            byte[] file = Xisf.Wrap(Doc($"<Image geometry=\"2:1:1\" sampleFormat=\"UInt8\" location=\"inline:base64\">{b64}</Image>"));
            Assert.Equal(new[] { 0f, 1f }, Load(file).NormalizedData);
        }

        // Real images carry Property/FITSKeyword children; their text is not pixel data.
        [Fact]
        public void InlineBase64_WithPropertyChild_IsDecoded()
        {
            string b64 = Convert.ToBase64String(new byte[] { 0, 255 });
            byte[] file = Xisf.Wrap(Doc(
                $"<Image geometry=\"2:1:1\" sampleFormat=\"UInt8\" location=\"inline:base64\">{b64}" +
                "<Property id=\"Observer:Name\" type=\"String\">MyName</Property></Image>"));
            Assert.NotNull(Load(file));
        }

        [Fact]
        public void EmbeddedData_IsDecoded()
        {
            string b64 = Convert.ToBase64String(new byte[] { 0, 255 });
            byte[] file = Xisf.Wrap(Doc(
                $"<Image geometry=\"2:1:1\" sampleFormat=\"UInt8\" location=\"embedded\"><Data encoding=\"base64\">{b64}</Data></Image>"));
            Assert.Equal(new[] { 0f, 1f }, Load(file).NormalizedData);
        }

        // byteOrder is an XISF attribute; ignoring it yields garbage pixels, not "no preview".
        [Fact]
        public void BigEndian_IsDecodedOrRejected()
        {
            byte[] be = { 0x00, 0x01 }; // 1 in big endian, 256 if misread as little endian
            XisfRawFrame f = Load(Xisf.Attached("geometry=\"1:1:1\" sampleFormat=\"UInt16\" byteOrder=\"big\"", be));
            if (f != null) Assert.Equal(1f / 65535f, f.NormalizedData[0], 7);
        }

        [Fact]
        public void Compressed_ReturnsNull() =>
            Assert.Null(Load(Xisf.Attached("geometry=\"1:1:1\" sampleFormat=\"UInt8\" compression=\"zlib:1\"", new byte[] { 0 })));

        [Theory]
        [InlineData("Float64")]
        [InlineData("UInt32")]
        [InlineData("Complex32")]
        [InlineData("Nonsense")]
        public void UnsupportedSampleFormat_ReturnsNull(string fmt) =>
            Assert.Null(Load(Xisf.Attached($"geometry=\"1:1:1\" sampleFormat=\"{fmt}\"", new byte[16])));

        [Theory]
        [InlineData("0:1:1")]
        [InlineData("1:0:1")]
        [InlineData("1:1:0")]
        [InlineData("-1:1:1")]
        [InlineData("1:1:5")]
        [InlineData("70000:1:1")]
        [InlineData("20000:20000:1")]
        [InlineData("a:b:c")]
        [InlineData("1:1")]
        public void BadGeometry_ReturnsNull(string geometry) =>
            Assert.Null(Load(Xisf.Attached($"geometry=\"{geometry}\" sampleFormat=\"UInt8\"", new byte[16])));

        [Fact]
        public void BadSignature_ReturnsNull()
        {
            byte[] file = Xisf.U16(1, 1, 1, new ushort[] { 1 });
            file[4] = (byte)'9';
            Assert.Null(Load(file));
        }

        [Fact]
        public void Empty_ReturnsNull() => Assert.Null(Load(new byte[0]));

        [Fact]
        public void HeaderLengthPastEof_ReturnsNull()
        {
            byte[] file = Xisf.U16(1, 1, 1, new ushort[] { 1 });
            BitConverter.GetBytes(100000u).CopyTo(file, 8);
            Assert.Null(Load(file));
        }

        [Fact]
        public void HeaderLengthOverCap_ReturnsNull()
        {
            byte[] file = Xisf.U16(1, 1, 1, new ushort[] { 1 });
            BitConverter.GetBytes(uint.MaxValue).CopyTo(file, 8);
            Assert.Null(Load(file));
        }

        [Fact]
        public void MalformedXml_ReturnsNull() => Assert.Null(Load(Xisf.Wrap("<xisf><Image")));

        [Fact]
        public void BillionLaughsDtd_IsRejected()
        {
            string xml = "<!DOCTYPE lolz [<!ENTITY a \"aaaaaaaaaa\"><!ENTITY b \"&a;&a;&a;&a;&a;&a;&a;&a;\">]>" +
                Doc("<Image geometry=\"1:1:1\" sampleFormat=\"UInt8\" location=\"inline:base64\">&b;</Image>");
            Assert.Null(Load(Xisf.Wrap(xml)));
        }

        [Fact]
        public void NoImageElement_ReturnsNull() => Assert.Null(Load(Xisf.Wrap(Doc("<Metadata/>"))));

        [Fact]
        public void AttachmentPastEof_ReturnsNull() =>
            Assert.Null(Load(Xisf.Attached("geometry=\"100:100:1\" sampleFormat=\"UInt8\"", new byte[10], "", 10000)));

        [Fact]
        public void AttachmentSmallerThanGeometry_ReturnsNull() =>
            Assert.Null(Load(Xisf.Attached("geometry=\"100:100:1\" sampleFormat=\"UInt8\"", new byte[10000], "", 10)));

        [Fact]
        public void InlineDataTooShort_ReturnsNull()
        {
            string b64 = Convert.ToBase64String(new byte[] { 1 });
            Assert.Null(Load(Xisf.Wrap(Doc($"<Image geometry=\"9:9:1\" sampleFormat=\"UInt8\" location=\"inline:base64\">{b64}</Image>"))));
        }

        [Fact]
        public void UnknownLocation_ReturnsNull() =>
            Assert.Null(Load(Xisf.Wrap(Doc("<Image geometry=\"1:1:1\" sampleFormat=\"UInt8\" location=\"url(http://x/y)\"/>"))));

        private static byte[] WithThumbnail(string thumbAttrs = "")
        {
            // Main image 4x4 at value 0; 2x2 thumbnail at value 255, both base64 inline.
            string main = Convert.ToBase64String(new byte[16]);
            string thumb = Convert.ToBase64String(Enumerable.Repeat((byte)255, 4).ToArray());
            return Xisf.Wrap(Doc(
                "<Image geometry=\"4:4:1\" sampleFormat=\"UInt8\" location=\"embedded\">" +
                $"<Data encoding=\"base64\">{main}</Data>" +
                $"<Thumbnail geometry=\"2:2:1\" sampleFormat=\"UInt8\" location=\"embedded\" {thumbAttrs}><Data encoding=\"base64\">{thumb}</Data></Thumbnail>" +
                "</Image>"));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(512)]
        public void EmbeddedThumbnail_UsedForSmallTargets(int target)
        {
            XisfRawFrame f = Load(WithThumbnail(), target);
            Assert.Equal(1f, f.NormalizedData[0]);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(513)]
        public void EmbeddedThumbnail_NotUsedForLargeTargets(int target)
        {
            XisfRawFrame f = Load(WithThumbnail(), target);
            Assert.Equal(4, f.Width);
            Assert.Equal(0f, f.NormalizedData[0]);
        }

        // A thumbnail we cannot decode should not cost the user a picture we can decode.
        [Fact]
        public void UnusableThumbnail_FallsBackToMainImage()
        {
            XisfRawFrame f = Load(WithThumbnail("compression=\"zlib:4\""), 256);
            Assert.NotNull(f);
            Assert.Equal(4, f.Width);
        }

        [Theory]
        [InlineData("Planar")]
        [InlineData("Normal")]
        public void Downscale_SeekableMatchesFullBufferPath(string storage)
        {
            const int w = 257, h = 131, ch = 3;
            var rnd = new Random(3);
            ushort[] samples = Enumerable.Range(0, w * h * ch).Select(_ => (ushort)rnd.Next(65536)).ToArray();
            byte[] file = Xisf.U16(w, h, ch, samples, storage);

            XisfRawFrame seek = Load(file, 16);

            // Same request through the non-subsampling path: inline data keeps the full buffer.
            byte[] raw = samples.SelectMany(BitConverter.GetBytes).ToArray();
            byte[] inline = Xisf.Wrap(Doc(
                $"<Image geometry=\"{w}:{h}:{ch}\" sampleFormat=\"UInt16\" pixelStorage=\"{storage}\" location=\"inline:base64\">{Convert.ToBase64String(raw)}</Image>"));
            XisfRawFrame full = Load(inline, 16);

            Assert.Equal(w / 8, seek.Width);
            Assert.Equal(h / 8, seek.Height);
            Assert.Equal(full.NormalizedData, seek.NormalizedData);
        }

        [Fact]
        public void Downscale_ReadsOnlyTheNeededRows()
        {
            string Xml(long pos) => Doc($"<Image geometry=\"10000:10000:1\" sampleFormat=\"UInt16\" location=\"attachment:{pos:D12}:200000000\"/>");
            int len = Encoding.UTF8.GetByteCount(Xml(0));
            byte[] header = Xisf.Wrap(Xml(16 + len));
            var stream = new VirtualFileStream(header, header.Length + 200000000L);

            XisfRawFrame f = XisfParser.LoadRawFrame(stream, 256);

            Assert.Equal(10000 / 39, f.Width);
            Assert.True(stream.BytesRead < 200000000L / 20, $"read {stream.BytesRead} bytes");
        }

        // XisfThumbnailHandler's comment promises this for "possibly multi-GB" files, and the
        // 300 MP pixel cap allows a 3-channel Float32 image of 3.6 GB.
        [Fact]
        public void Downscale_WorksOnAttachmentOver2GB()
        {
            const long size = 15000L * 15000 * 3 * 4; // 2.7 GB
            string Xml(long pos) => Doc($"<Image geometry=\"15000:15000:3\" sampleFormat=\"Float32\" location=\"attachment:{pos:D12}:{size}\"/>");
            int len = Encoding.UTF8.GetByteCount(Xml(0));
            byte[] header = Xisf.Wrap(Xml(16 + len));

            XisfRawFrame f = XisfParser.LoadRawFrame(new VirtualFileStream(header, header.Length + size), 256);

            Assert.NotNull(f);
            Assert.Equal(15000 / 58, f.Width);
        }

        [Fact]
        public void GetPreviewImage_FromPath_RendersBitmap()
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(path, Xisf.U16(3, 2, 1, new ushort[] { 1, 2, 3, 4, 5, 6 }));
                using (var bmp = XisfParser.GetPreviewImage(path))
                {
                    Assert.Equal(3, bmp.Width);
                    Assert.Equal(2, bmp.Height);
                }
            }
            finally { File.Delete(path); }
        }
    }
}
