using System;
using System.IO;
using System.Linq;
using System.Text;
using XisfExplorerPreview;
using Xunit;

namespace XisfPreview.Tests
{
    public class FitsParserTests
    {
        private static XisfRawFrame Load(byte[] file, int target = 0) =>
            FitsParser.LoadRawFrame(new MemoryStream(file), target);

        [Fact]
        public void Bitpix8_NormalizesTo0To1()
        {
            XisfRawFrame f = Load(Fits.File(8, 2, 1, 1, new byte[] { 0, 255 }));
            Assert.Equal(new[] { 0f, 1f }, f.NormalizedData);
        }

        [Fact]
        public void Bitpix16_WithBzero_ReadsUnsignedRange()
        {
            XisfRawFrame f = Load(Fits.U16(3, 1, new ushort[] { 0, 32768, 65535 }));
            Assert.Equal(0f, f.NormalizedData[0], 5);
            Assert.Equal(32768f / 65535f, f.NormalizedData[1], 5);
            Assert.Equal(1f, f.NormalizedData[2], 5);
        }

        [Fact]
        public void Bitpix16_AppliesBscale()
        {
            XisfRawFrame f = Load(Fits.File(16, 1, 1, 1, Fits.Be16(new short[] { 100 }), Fits.Card("BSCALE", "2.0")));
            Assert.Equal(200f / 65535f, f.NormalizedData[0], 6);
        }

        [Fact]
        public void Bitpix32_WithUnsignedBzero_ReadsFullRange()
        {
            XisfRawFrame f = Load(Fits.File(32, 2, 1, 1, Fits.Be32(new[] { int.MinValue, int.MaxValue }),
                Fits.Card("BZERO", "2147483648")));
            Assert.Equal(0f, f.NormalizedData[0], 5);
            Assert.Equal(1f, f.NormalizedData[1], 5);
        }

        [Fact]
        public void BitpixMinus32_ReadsFloatsAndClampsNonFinite()
        {
            XisfRawFrame f = Load(Fits.File(-32, 4, 1, 1,
                Fits.BeFloat(new[] { 0.25f, float.NaN, -1f, float.PositiveInfinity })));
            Assert.Equal(new[] { 0.25f, 0f, 0f, 1f }, f.NormalizedData);
        }

        [Fact]
        public void BitpixMinus32_NaNFromScaling_BecomesZero()
        {
            // 0 x Infinity is NaN on the scaled value, not on the raw one.
            XisfRawFrame f = Load(Fits.File(-32, 2, 1, 1, Fits.BeFloat(new[] { 0f, 0.5f }),
                Fits.Card("BSCALE", "Infinity")));
            Assert.Equal(new[] { 0f, 1f }, f.NormalizedData);
        }

        // Plenty of writers store float data in ADU (0..65535), not 0..1. The image must keep
        // its structure instead of clipping to solid white.
        [Fact]
        public void BitpixMinus32_InAduRange_KeepsImageStructure()
        {
            XisfRawFrame f = Load(Fits.File(-32, 2, 1, 1, Fits.BeFloat(new[] { 1000f, 40000f })));
            Assert.NotEqual(f.NormalizedData[0], f.NormalizedData[1]);
        }

        // ADU-range float data (max <= 65535) is divided by 65535, keeping relative levels.
        [Fact]
        public void BitpixMinus32_AduRange_DividesBy65535()
        {
            XisfRawFrame f = Load(Fits.File(-32, 2, 1, 1, Fits.BeFloat(new[] { 1000f, 40000f })));
            Assert.Equal(1000f / 65535f, f.NormalizedData[0], 5);
            Assert.Equal(40000f / 65535f, f.NormalizedData[1], 5);
        }

        // 8-bit values stored as float (max <= 255) are divided by 255.
        [Fact]
        public void BitpixMinus32_EightBitValues_DividesBy255()
        {
            XisfRawFrame f = Load(Fits.File(-32, 2, 1, 1, Fits.BeFloat(new[] { 51f, 255f })));
            Assert.Equal(0.2f, f.NormalizedData[0], 5);
            Assert.Equal(1f, f.NormalizedData[1], 5);
        }

        // Above 65535 the finite maximum is the scale; +Inf does not count towards it and clamps to 1.
        [Fact]
        public void BitpixMinus32_BeyondAduRange_DividesByMaxIgnoringInf()
        {
            XisfRawFrame f = Load(Fits.File(-32, 4, 1, 1,
                Fits.BeFloat(new[] { 100000f, 50000f, float.PositiveInfinity, -5f })));
            Assert.Equal(new[] { 1f, 0.5f, 1f, 0f }, f.NormalizedData);
        }

        // Inf alongside ADU data must not force the scale to infinity.
        [Fact]
        public void BitpixMinus32_InfWithMaxAboveOne_StillScalesFiniteSamples()
        {
            XisfRawFrame f = Load(Fits.File(-32, 3, 1, 1,
                Fits.BeFloat(new[] { 1000f, float.PositiveInfinity, 40000f })));
            Assert.Equal(1000f / 65535f, f.NormalizedData[0], 5);
            Assert.Equal(1f, f.NormalizedData[1]);
            Assert.Equal(40000f / 65535f, f.NormalizedData[2], 5);
        }

        [Fact]
        public void BitpixMinus32_AppliesBscaleAndBzero()
        {
            XisfRawFrame f = Load(Fits.File(-32, 1, 1, 1, Fits.BeFloat(new[] { 0.5f }),
                Fits.Card("BSCALE", "0.5")));
            Assert.Equal(0.25f, f.NormalizedData[0], 5);
        }

        [Fact]
        public void DefaultRowOrder_IsFlippedBottomUp()
        {
            XisfRawFrame f = Load(Fits.File(8, 1, 2, 1, new byte[] { 0, 255 }));
            Assert.Equal(new[] { 1f, 0f }, f.NormalizedData);
        }

        [Fact]
        public void RoworderTopDown_IsNotFlipped()
        {
            XisfRawFrame f = Load(Fits.File(8, 1, 2, 1, new byte[] { 0, 255 }, Fits.Card("ROWORDER", "'TOP-DOWN'")));
            Assert.Equal(new[] { 0f, 1f }, f.NormalizedData);
        }

        [Fact]
        public void RoworderBottomUp_IsFlipped()
        {
            XisfRawFrame f = Load(Fits.File(8, 1, 2, 1, new byte[] { 0, 255 }, Fits.Card("ROWORDER", "'BOTTOM-UP'")));
            Assert.Equal(new[] { 1f, 0f }, f.NormalizedData);
        }

        [Fact]
        public void ThreePlanes_AreInterleavedAsRgb()
        {
            XisfRawFrame f = Load(Fits.File(8, 1, 1, 3, new byte[] { 255, 0, 51 }));
            Assert.Equal(3, f.Channels);
            Assert.Equal(new[] { 1f, 0f, 51f / 255f }, f.NormalizedData);
        }

        [Fact]
        public void MoreThanThreePlanes_KeepsFirstThree()
        {
            XisfRawFrame f = Load(Fits.File(8, 1, 1, 5, new byte[] { 255, 0, 51, 9, 9 }));
            Assert.Equal(3, f.Channels);
            Assert.Equal(new[] { 1f, 0f, 51f / 255f }, f.NormalizedData);
        }

        [Fact]
        public void HeaderSpanningSeveralBlocks_IsParsed()
        {
            string[] filler = Enumerable.Range(0, 100).Select(i => Fits.Card($"KEY{i}", "1")).ToArray();
            XisfRawFrame f = Load(Fits.File(8, 1, 1, 1, new byte[] { 255 }, filler));
            Assert.Equal(1f, f.NormalizedData[0]);
        }

        [Fact]
        public void HeaderInlineComment_IsIgnored()
        {
            byte[] h = Fits.Header(new[]
            {
                Fits.Card("SIMPLE", "T / conforms"),
                Fits.Card("BITPIX", "8 / bits"),
                Fits.Card("NAXIS", "2 / axes"),
                Fits.Card("NAXIS1", "1 / w"),
                Fits.Card("NAXIS2", "1 / h")
            });
            Assert.NotNull(Load(h.Concat(new byte[] { 7 }).ToArray()));
        }

        [Fact]
        public void HistogramCountsOnePerPixel()
        {
            XisfRawFrame f = Load(Fits.File(8, 2, 2, 3, new byte[12]));
            Assert.Equal(4, f.Histogram.Sum());
        }

        [Theory]
        [InlineData(64)]   // documented as supported nowhere; must not misread
        [InlineData(-64)]
        [InlineData(12)]
        public void UnsupportedBitpix_ReturnsNull(int bitpix) =>
            Assert.Null(Load(Fits.File(bitpix, 1, 1, 1, new byte[8])));

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 0)]
        [InlineData(-1, 1)]
        [InlineData(70000, 1)]
        [InlineData(65535, 65535)]
        public void HostileDimensions_ReturnNull(int w, int h) =>
            Assert.Null(Load(Fits.File(8, w, h, 1, new byte[8])));

        [Fact]
        public void TooManyChannels_ReturnNull() =>
            Assert.Null(Load(Fits.File(8, 1, 1, 17, new byte[17])));

        [Fact]
        public void TruncatedPixels_ReturnNull() =>
            Assert.Null(Load(Fits.File(8, 4, 4, 1, new byte[5])));

        [Fact]
        public void TruncatedPixels_OnDownscaledRead_ReturnNull() =>
            Assert.Null(Load(Fits.File(8, 100, 100, 1, new byte[50]), 10));

        [Fact]
        public void MissingEnd_ReturnsNull()
        {
            byte[] block = Encoding.ASCII.GetBytes(Fits.Card("SIMPLE", "T").PadRight(2880));
            Assert.Null(Load(block));
        }

        [Fact]
        public void EndlessHeader_StopsAtBlockCap()
        {
            byte[] block = Encoding.ASCII.GetBytes(Fits.Card("SIMPLE", "T").PadRight(2880));
            var stream = new VirtualFileStream(Enumerable.Repeat(block, 3).SelectMany(b => b).ToArray(), long.MaxValue / 2);
            Assert.Null(FitsParser.LoadRawFrame(stream));
            Assert.True(stream.BytesRead <= 2000 * 2880);
        }

        [Fact]
        public void Empty_ReturnsNull() => Assert.Null(Load(new byte[0]));

        [Fact]
        public void BadBzero_ReturnsNull() =>
            Assert.Null(Load(Fits.File(8, 1, 1, 1, new byte[1], Fits.Card("BZERO", "abc"))));

        [Fact]
        public void HugeDeclaredSize_InTinyFile_DoesNotAllocate()
        {
            long before = GC.GetTotalMemory(true);
            Assert.Null(Load(Fits.File(-32, 17000, 17000, 3, new byte[8])));
            Assert.True(GC.GetTotalMemory(false) - before < 64L * 1024 * 1024);
        }

        [Theory]
        [InlineData(8)]
        [InlineData(16)]
        [InlineData(-32)]
        public void Downscale_SeekableMatchesFullBufferPath(int bitpix)
        {
            const int w = 257, h = 131, ch = 3;
            var rnd = new Random(7);
            byte[] data = new byte[w * h * ch * Math.Abs(bitpix) / 8];
            if (bitpix == -32)
                data = Fits.BeFloat(Enumerable.Range(0, w * h * ch).Select(_ => (float)rnd.NextDouble()));
            else
                rnd.NextBytes(data);
            byte[] file = Fits.File(bitpix, w, h, ch, data);

            XisfRawFrame seek = FitsParser.LoadRawFrame(new MemoryStream(file), 16);
            XisfRawFrame forward = FitsParser.LoadRawFrame(new ForwardOnlyStream(file), 16);

            Assert.Equal(w / 8, seek.Width);
            Assert.Equal(h / 8, seek.Height);
            Assert.Equal(forward.Width, seek.Width);
            Assert.Equal(forward.NormalizedData, seek.NormalizedData);
        }

        [Fact]
        public void Downscale_ReadsOnlyTheNeededRows()
        {
            const int w = 10000, h = 10000;
            var header = Fits.Header(new[]
            {
                Fits.Card("SIMPLE", "T"), Fits.Card("BITPIX", "16"), Fits.Card("NAXIS", "2"),
                Fits.Card("NAXIS1", w.ToString()), Fits.Card("NAXIS2", h.ToString())
            });
            var stream = new VirtualFileStream(header, header.Length + (2L * w * h));
            XisfRawFrame f = FitsParser.LoadRawFrame(stream, 256);

            Assert.Equal(w / 39, f.Width);
            Assert.True(stream.BytesRead < 2L * w * h / 20, $"read {stream.BytesRead} bytes");
        }

        [Fact]
        public void LoadFromPath_MatchesLoadFromStream()
        {
            string path = Path.GetTempFileName();
            try
            {
                byte[] file = Fits.File(8, 2, 2, 1, new byte[] { 1, 2, 3, 4 });
                File.WriteAllBytes(path, file);
                Assert.Equal(Load(file).NormalizedData, FitsParser.LoadRawFrame(path).NormalizedData);
            }
            finally { File.Delete(path); }
        }
    }
}
