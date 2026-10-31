using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XisfExplorerPreview;
using Xunit;

namespace XisfPreview.Tests
{
    // Builds tile-compressed FITS files (RICE_1, 32-bit, one row per tile) with a small encoder.
    internal static class RiceFits
    {
        private sealed class Bits
        {
            public readonly List<byte> Bytes = new List<byte>();
            private ulong _acc; private int _n;
            public void Put(ulong value, int count)
            {
                for (int i = count - 1; i >= 0; i--)
                {
                    _acc = (_acc << 1) | ((value >> i) & 1); _n++;
                    if (_n == 8) { Bytes.Add((byte)_acc); _acc = 0; _n = 0; }
                }
            }
            public void Flush() { while (_n != 0) Put(0, 1); }
        }

        // forcedFs >= 0 makes every non-constant block use that split, which produces long zero runs.
        public static byte[] EncodeRow(int[] row, int blk, int forcedFs = -1)
        {
            var b = new Bits();
            b.Put((uint)row[0], 32);
            uint last = (uint)row[0];
            for (int s = 0; s < row.Length; s += blk)
            {
                int n = Math.Min(blk, row.Length - s);
                var d = new uint[n];
                for (int i = 0; i < n; i++)
                {
                    int diff = (int)((uint)row[s + i] - last);
                    d[i] = diff < 0 ? ((uint)(-(long)diff) << 1) - 1 : (uint)diff << 1;
                    last = (uint)row[s + i];
                }
                int fs = -1;
                if (d.Any(x => x != 0))
                {
                    long best = 32L * n; fs = 25;
                    for (int f = 0; f <= 24; f++)
                    {
                        long bits = d.Sum(x => (long)(x >> f) + 1 + f);
                        if (bits < best) { best = bits; fs = f; }
                    }
                    if (forcedFs >= 0) fs = forcedFs;
                }
                b.Put((ulong)(fs + 1), 5);
                if (fs == 25) foreach (uint x in d) b.Put(x, 32);
                else if (fs >= 0)
                    foreach (uint x in d) { b.Put(0, (int)(x >> fs)); b.Put(1, 1); b.Put(x & ((1u << fs) - 1), fs); }
            }
            b.Flush();
            return b.Bytes.ToArray();
        }

        // pixels are stored bottom-up (file row 0 is the bottom of the image), as in plain FITS.
        public static byte[] File(int w, int h, int[] pixels, Dictionary<string, string> over = null,
            Action<int[], int[]> mutate = null, int forcedFs = -1, params string[] extra)
        {
            var heap = new List<byte>();
            var lens = new int[h]; var offs = new int[h];
            for (int y = 0; y < h; y++)
            {
                byte[] c = EncodeRow(pixels.Skip(y * w).Take(w).ToArray(), 32, forcedFs);
                lens[y] = c.Length; offs[y] = heap.Count; heap.AddRange(c);
            }
            int pcount = heap.Count;
            mutate?.Invoke(lens, offs);

            var f = new Dictionary<string, string>
            {
                ["XTENSION"] = "'BINTABLE'", ["BITPIX"] = "8", ["NAXIS"] = "2", ["NAXIS1"] = "8",
                ["NAXIS2"] = h.ToString(), ["PCOUNT"] = pcount.ToString(), ["GCOUNT"] = "1", ["TFIELDS"] = "1",
                ["TTYPE1"] = "'COMPRESSED_DATA'", ["TFORM1"] = "'1PB(" + w + ")'", ["ZIMAGE"] = "T",
                ["ZCMPTYPE"] = "'RICE_1'", ["ZBITPIX"] = "32", ["ZNAXIS"] = "2", ["ZNAXIS1"] = w.ToString(),
                ["ZNAXIS2"] = h.ToString(), ["ZNAME1"] = "'BLOCKSIZE'", ["ZVAL1"] = "32",
                ["ZNAME2"] = "'BYTEPIX'", ["ZVAL2"] = "4"
            };
            if (over != null) foreach (var kv in over) { if (kv.Value == null) f.Remove(kv.Key); else f[kv.Key] = kv.Value; }

            var data = new List<byte>(Fits.Be32(lens.Zip(offs, (l, o) => new[] { l, o }).SelectMany(x => x)));
            data.AddRange(heap);
            while (data.Count % 2880 != 0) data.Add(0);

            byte[] primary = Fits.Header(new[]
            {
                Fits.Card("SIMPLE", "T"), Fits.Card("BITPIX", "8"), Fits.Card("NAXIS", "0"), Fits.Card("EXTEND", "T")
            });
            byte[] ext = Fits.Header(f.Select(kv => Fits.Card(kv.Key, kv.Value)).Concat(extra));
            return primary.Concat(ext).Concat(data).ToArray();
        }
    }

    public class FitsCompressedTests
    {
        private static XisfRawFrame Load(byte[] file, int target = 0) =>
            FitsParser.LoadRawFrame(new MemoryStream(file), target);

        private static int[] Ramp(int w, int h, int max = 60000) =>
            Enumerable.Range(0, w * h).Select(i => 1 + ((i * 7919) % max)).ToArray();

        private static Dictionary<string, string> With(string key, string value) =>
            new Dictionary<string, string> { [key] = value };

        [Fact]
        public void RiceRows_DecodeLosslesslyAndScaleBy65535()
        {
            const int w = 70, h = 6; // 70 is not a multiple of the block size
            int[] px = Ramp(w, h);
            XisfRawFrame f = Load(RiceFits.File(w, h, px));

            Assert.Equal(w, f.Width);
            Assert.Equal(h, f.Height);
            Assert.Equal(1, f.Channels);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    Assert.Equal(px[((h - 1 - y) * w) + x] / 65535f, f.NormalizedData[(y * w) + x], 6);
        }

        [Fact]
        public void ValuesAbove65535_UseTheFull32BitRange()
        {
            XisfRawFrame f = Load(RiceFits.File(2, 1, new[] { 1000, 100000 }));
            Assert.Equal(100000f / 4294967295f, f.NormalizedData[1], 9);
        }

        [Fact]
        public void Bzero_KeepsTheFull32BitRange()
        {
            XisfRawFrame f = Load(RiceFits.File(2, 1, new[] { 1000, 2000 }, With("BZERO", "2147483648")));
            Assert.Equal((2000 + 2147483648.0) / 4294967295.0, f.NormalizedData[1], 6);
        }

        [Fact]
        public void LongZeroRun_IsDecoded()
        {
            // fs = 0 on a large step gives a run of 200 zero bits, far beyond 64.
            int[] px = Enumerable.Repeat(5, 32).Concat(Enumerable.Repeat(105, 32)).ToArray();
            XisfRawFrame f = Load(RiceFits.File(64, 1, px, forcedFs: 0));
            Assert.Equal(105 / 65535f, f.NormalizedData[63], 6);
        }

        [Fact]
        public void Downscale_PicksTheSameSamplesAsThePlainPath()
        {
            const int w = 64, h = 64;
            int[] px = Ramp(w, h);
            XisfRawFrame f = Load(RiceFits.File(w, h, px), 16);

            Assert.Equal(16, f.Width);
            Assert.Equal(16, f.Height);
            Assert.Equal(px[((h - 1 - (3 * 4)) * w) + (5 * 4)] / 65535f, f.NormalizedData[(3 * 16) + 5], 6);
        }

        [Fact]
        public void RowOrderTopDown_IsHonoured()
        {
            int[] px = Ramp(8, 2);
            XisfRawFrame f = Load(RiceFits.File(8, 2, px, With("ROWORDER", "'TOP-DOWN'")));
            Assert.Equal(px[0] / 65535f, f.NormalizedData[0], 6);
        }

        public static IEnumerable<object[]> Unsupported() => new[]
        {
            new object[] { "ZCMPTYPE", "'GZIP_1'" },
            new object[] { "ZBITPIX", "16" },
            new object[] { "ZTILE2", "2" },
            new object[] { "ZTILE1", "4" },
            new object[] { "ZQUANTIZ", "'NO_DITHER'" },
            new object[] { "ZBLANK", "-1" },
            new object[] { "ZSCALE", "1.0" },
            new object[] { "ZIMAGE", "F" },
            new object[] { "ZNAXIS", "3" },
            new object[] { "ZVAL2", "2" },
            new object[] { "ZVAL1", "0" },
            new object[] { "ZVAL1", "100000" },
            new object[] { "TFORM1", "'1QB(8)'" },
            new object[] { "NAXIS1", "16" },
            new object[] { "TFIELDS", "2" },
            new object[] { "THEAP", "4096" },
            new object[] { "XTENSION", "'IMAGE'" },
        };

        [Theory]
        [MemberData(nameof(Unsupported))]
        public void UnsupportedVariants_ReturnNull(string key, string value) =>
            Assert.Null(Load(RiceFits.File(8, 2, Ramp(8, 2), With(key, value))));

        public static IEnumerable<object[]> Malformed() => new[]
        {
            new object[] { "ZNAXIS1", "65535" },     // far more pixels than the file can hold
            new object[] { "ZNAXIS1", "-8" },
            new object[] { "ZNAXIS1", "70000" },
            new object[] { "ZNAXIS2", "3" },         // tile count does not match the descriptor count
            new object[] { "NAXIS2", "100000" },
            new object[] { "PCOUNT", "-1" },
            new object[] { "PCOUNT", "2000000000" }, // heap larger than the file
            new object[] { "NAXIS2", "abc" },
        };

        [Theory]
        [MemberData(nameof(Malformed))]
        public void MalformedHeader_ReturnsNull(string key, string value) =>
            Assert.Null(Load(RiceFits.File(8, 2, Ramp(8, 2), With(key, value))));

        [Theory]
        [InlineData(-1, 0)]          // negative length
        [InlineData(4, 0)]           // shorter than the smallest valid encoding
        [InlineData(100000, 0)]      // longer than a worst-case row
        [InlineData(40, -1)]         // negative heap offset
        [InlineData(40, 100000)]     // offset beyond the heap
        [InlineData(40, 2147483647)] // offset plus length overflows int
        public void MalformedDescriptor_ReturnsNull(int len, int off)
        {
            byte[] file = RiceFits.File(8, 2, Ramp(8, 2), mutate: (lens, offs) => { lens[0] = len; offs[0] = off; });
            Assert.Null(Load(file));
        }

        [Theory]
        [InlineData("BSCALE", "NaN")]
        [InlineData("BSCALE", "Infinity")]
        [InlineData("BSCALE", "-Infinity")]
        [InlineData("BZERO", "NaN")]
        [InlineData("BZERO", "Infinity")]
        [InlineData("BZERO", "-Infinity")]
        public void NonFiniteScaling_ReturnsNull(string key, string value) =>
            Assert.Null(Load(RiceFits.File(8, 2, Ramp(8, 2), With(key, value))));

        [Fact]
        public void StreamShorterThanItsLength_ReturnsNullOnEarlyEof()
        {
            // Length claims the whole file, so the truncation check passes; the table read then hits EOF.
            byte[] file = RiceFits.File(8, 2, Ramp(8, 2));
            int tableStart = file.Length - 2880;
            Assert.Null(FitsParser.LoadRawFrame(new LyingLengthStream(file.Take(tableStart + 8).ToArray(), file.Length), 0));
        }

        [Fact]
        public void TruncatedFile_ReturnsNull()
        {
            byte[] file = RiceFits.File(70, 6, Ramp(70, 6));
            Assert.Null(Load(file.Take(file.Length - 2880).ToArray()));
        }

        [Fact]
        public void NonSeekableStream_ReturnsNull()
        {
            byte[] file = RiceFits.File(8, 2, Ramp(8, 2));
            Assert.Null(FitsParser.LoadRawFrame(new ForwardOnlyStream(file)));
        }

        // Corrupt payload bytes inside valid descriptors must never throw out of the parser.
        [Fact]
        public void CorruptPayload_NeverThrows()
        {
            var rnd = new Random(1234);
            byte[] good = RiceFits.File(70, 3, Ramp(70, 3));
            int dataStart = good.Length - 2880;
            for (int n = 0; n < 300; n++)
            {
                byte[] file = (byte[])good.Clone();
                for (int k = 0; k < 20; k++) file[dataStart + 48 + rnd.Next(400)] = (byte)rnd.Next(256);
                XisfRawFrame f = Load(file);
                if (f != null) Assert.Equal(70 * 3, f.NormalizedData.Length);
            }
        }

        [Fact]
        public void AllRowsSharingOneTile_ReturnsNull()
        {
            // Row 0 is a real ramp, the rest are constant rows; every descriptor then points at row 0's
            // bytes, which would decode the same heap bytes once per row.
            const int w = 70, h = 8;
            int[] px = Ramp(w, 1).Concat(Enumerable.Repeat(5, w * (h - 1))).ToArray();
            byte[] file = RiceFits.File(w, h, px, mutate: (lens, offs) =>
            {
                for (int y = 1; y < h; y++) { lens[y] = lens[0]; offs[y] = 0; }
            });
            Assert.Null(Load(file));
        }

        [Fact]
        public void FrameOverThePixelCap_ReturnsNull()
        {
            const int w = 65535, h = 245; // 16,056,075 pixels
            Assert.Null(Load(RiceFits.File(w, h, new int[w * h])));
        }

        [Fact]
        public void FrameJustUnderThePixelCap_StillDecodes()
        {
            const int w = 65535, h = 244; // 15,990,540 pixels
            XisfRawFrame f = Load(RiceFits.File(w, h, Enumerable.Repeat(1000, w * h).ToArray()));
            Assert.NotNull(f);
            Assert.Equal(h, f.Height);
            Assert.Equal(1000 / 65535f, f.NormalizedData[0], 6);
        }

        [Fact]
        public void OneNegativePixel_KeepsThe16BitDivisor()
        {
            XisfRawFrame f = Load(RiceFits.File(3, 1, new[] { -5, 1000, 2000 }));
            Assert.Equal(0f, f.NormalizedData[0], 9);
            Assert.Equal(1000 / 65535f, f.NormalizedData[1], 6);
            Assert.Equal(2000 / 65535f, f.NormalizedData[2], 6);
        }

        [Fact]
        public void ShortReadingStream_StillLoads()
        {
            const int w = 70, h = 6;
            int[] px = Ramp(w, h);
            XisfRawFrame f = FitsParser.LoadRawFrame(new ShortReadStream(RiceFits.File(w, h, px)), 0);
            Assert.NotNull(f);
            Assert.Equal(px[((h - 1) * w)] / 65535f, f.NormalizedData[0], 6);
        }

        private sealed class ShortReadStream : MemoryStream
        {
            public ShortReadStream(byte[] b) : base(b) { }
            public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, 3));
        }

        // Reports a larger Length than it can deliver, to reach the early-EOF branches of the reads.
        private sealed class LyingLengthStream : MemoryStream
        {
            private readonly long _length;
            public LyingLengthStream(byte[] b, long length) : base(b) { _length = length; }
            public override long Length => _length;
        }

        private sealed class ForwardOnlyStream : MemoryStream
        {
            public ForwardOnlyStream(byte[] b) : base(b) { }
            public override bool CanSeek => false;
        }
    }
}
