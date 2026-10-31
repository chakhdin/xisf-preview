using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace XisfExplorerPreview
{
    // Tile-compressed FITS images (the "fpack" layout: empty primary HDU followed by a BINTABLE
    // extension with ZIMAGE = T). Only the lossless integer subset seen in practice is handled:
    // RICE_1, 32-bit pixels, one image row per tile. Anything else returns null, like any other
    // unsupported file. The header is hostile input: every size and offset is validated against
    // the file before it is used for an allocation or a seek.
    internal static class FitsCompressedImage
    {
        private const int MaxDimension = 65535;
        private const long MaxTotalPixels = 16_000_000L; // real frames are at most about 11.7 MP
        private const int MaxHeaderBlocks = 2000;
        private const int MaxBlockSize = 64;

        // Reads one HDU header into a dictionary (first occurrence of a keyword wins).
        private static Dictionary<string, string> ReadHeader(Stream stream)
        {
            var header = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            byte[] block = new byte[2880];
            for (int n = 0; n < MaxHeaderBlocks; n++)
            {
                int got = 0;
                while (got < 2880)
                {
                    int r = stream.Read(block, got, 2880 - got);
                    if (r <= 0) return null;
                    got += r;
                }

                for (int i = 0; i < 2880; i += 80)
                {
                    string key = Encoding.ASCII.GetString(block, i, 8).Trim();
                    if (key.Equals("END", StringComparison.OrdinalIgnoreCase)) return header;
                    if (block[i + 8] == '=' && !header.ContainsKey(key))
                        header[key] = Encoding.ASCII.GetString(block, i + 10, 70).Split('/')[0].Trim().Trim('\'').Trim();
                }
            }
            return null;
        }

        private static bool TryInt(Dictionary<string, string> h, string key, out int value)
        {
            value = 0;
            return h.TryGetValue(key, out string s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static int BeInt(byte[] b, int p) => (b[p] << 24) | (b[p + 1] << 16) | (b[p + 2] << 8) | b[p + 3];

        // Returns null when the stream is not a supported compressed image. The stream must be
        // positioned just after the primary header, which has NAXIS = 0 and therefore no data.
        public static XisfRawFrame Load(Stream stream, Dictionary<string, string> primary, int targetDimension)
        {
            if (!stream.CanSeek) return null;
            var h = ReadHeader(stream);
            if (h == null) return null;

            if (!h.TryGetValue("XTENSION", out string xt) || xt != "BINTABLE") return null;
            if (!h.TryGetValue("ZIMAGE", out string zi) || zi != "T") return null;
            if (!h.TryGetValue("ZCMPTYPE", out string cmp) || cmp != "RICE_1") return null;
            if (!TryInt(h, "ZBITPIX", out int zbitpix) || zbitpix != 32) return null;
            if (!TryInt(h, "ZNAXIS", out int znaxis) || znaxis != 2) return null;
            if (!TryInt(h, "ZNAXIS1", out int width) || !TryInt(h, "ZNAXIS2", out int height)) return null;
            if (width <= 0 || height <= 0 || width > MaxDimension || height > MaxDimension) return null;
            if ((long)width * height > MaxTotalPixels) return null;

            // One row per tile; ZTILEn default to a full row when absent.
            if (TryInt(h, "ZTILE1", out int tile1) ? tile1 != width : h.ContainsKey("ZTILE1")) return null;
            if (TryInt(h, "ZTILE2", out int tile2) ? tile2 != 1 : h.ContainsKey("ZTILE2")) return null;

            // Rice parameters: ZNAMEn / ZVALn pairs, defaults BLOCKSIZE 32 and BYTEPIX 4.
            int blockSize = 32, bytePix = 4;
            for (int i = 1; i <= 8; i++)
            {
                if (!h.TryGetValue("ZNAME" + i, out string name)) continue;
                if (!TryInt(h, "ZVAL" + i, out int val)) return null;
                if (name == "BLOCKSIZE") blockSize = val;
                else if (name == "BYTEPIX") bytePix = val;
            }
            if (blockSize < 1 || blockSize > MaxBlockSize || bytePix != 4) return null;

            // Lossless integer data only: a quantised or blanked image needs a different decoder.
            foreach (string k in new[] { "ZQUANTIZ", "ZDITHER0", "ZBLANK", "ZSCALE", "ZZERO" })
                if (h.ContainsKey(k)) return null;

            // Exactly one column of 8-byte variable-length descriptors (count, heap offset), one per row.
            if (!TryInt(h, "BITPIX", out int bp) || bp != 8) return null;
            if (!TryInt(h, "NAXIS", out int naxis) || naxis != 2) return null;
            if (!TryInt(h, "NAXIS1", out int rowBytes) || rowBytes != 8) return null;
            if (!TryInt(h, "NAXIS2", out int rows) || rows != height) return null;
            if (!TryInt(h, "TFIELDS", out int fields) || fields != 1) return null;
            if (!h.TryGetValue("TFORM1", out string tform) || !tform.StartsWith("1PB", StringComparison.Ordinal)) return null;
            if (!h.TryGetValue("TTYPE1", out string ttype) || ttype != "COMPRESSED_DATA") return null;
            if (!TryInt(h, "PCOUNT", out int pcount) || pcount < 0) return null;
            long tableBytes = 8L * rows;
            if (TryInt(h, "THEAP", out int theap) ? theap != tableBytes : h.ContainsKey("THEAP")) return null;

            double bzero = 0.0, bscale = 1.0;
            bool hasBzero = h.ContainsKey("BZERO");
            if (hasBzero && !double.TryParse(h["BZERO"], NumberStyles.Float, CultureInfo.InvariantCulture, out bzero)) return null;
            if (h.ContainsKey("BSCALE") && !double.TryParse(h["BSCALE"], NumberStyles.Float, CultureInfo.InvariantCulture, out bscale)) return null;
            // TryParse accepts "NaN" and "Infinity"; with finite values the pixel formula cannot yield NaN.
            if (double.IsNaN(bzero) || double.IsInfinity(bzero) || double.IsNaN(bscale) || double.IsInfinity(bscale)) return null;

            long tableStart = stream.Position;
            long heapStart = tableStart + tableBytes;
            if (heapStart + pcount > stream.Length) return null; // truncated file

            byte[] table = new byte[tableBytes];
            for (int got = 0; got < table.Length;)
            {
                int r = stream.Read(table, got, table.Length - got);
                if (r <= 0) return null;
                got += r;
            }

            // Validate every descriptor before allocating anything sized by the image: each tile must
            // lie inside the heap, be no larger than a worst-case row, and be no smaller than the
            // cheapest possible encoding (4 bytes for the first value plus 5 bits per block). Tiles may
            // overlap, so the minimum size does not tie the pixel count to the file size. The work is
            // bounded by MaxTotalPixels. Bounds: rows * minTile <= sum of tile lengths <= PCOUNT, and the
            // zero-run scan of each row is limited to that row's tile length, so the total scan is
            // bounded by the summed tile bytes. A padded PCOUNT does allow tiles to share bytes, but
            // the sum still caps the total bytes decoded, so there is no amplification.
            long totalTileBytes = 0;
            long maxTile = 5L * width + 16;
            long minTile = 4 + ((((long)width + blockSize - 1) / blockSize * 5) + 7) / 8;
            int largest = 0;
            for (int y = 0; y < rows; y++)
            {
                int len = BeInt(table, y * 8), off = BeInt(table, y * 8 + 4);
                if (len < minTile || len > maxTile || off < 0 || (long)off + len > pcount) return null;
                totalTileBytes += len;
                if (totalTileBytes > pcount) return null;
                if (len > largest) largest = len;
            }

            int step = 1;
            if (targetDimension > 0 && (width > targetDimension || height > targetDimension))
                step = Math.Max(1, Math.Min(width / targetDimension, height / targetDimension));
            int outWidth = width / step, outHeight = height / step;

            // Classic FITS stores row 1 at the bottom; honor an explicit ROWORDER like the plain path.
            bool flip = true;
            if ((h.TryGetValue("ROWORDER", out string ro) || primary.TryGetValue("ROWORDER", out ro)) &&
                ro.Equals("TOP-DOWN", StringComparison.OrdinalIgnoreCase))
                flip = false;

            int[] raw = new int[outWidth * outHeight];
            int[] row = new int[width];
            byte[] comp = new byte[largest];
            long max = 0;

            for (int y = 0; y < outHeight; y++)
            {
                int srcY = flip ? (height - 1) - (y * step) : y * step;
                int len = BeInt(table, srcY * 8), off = BeInt(table, srcY * 8 + 4);
                stream.Seek(heapStart + off, SeekOrigin.Begin);
                int got = 0;
                while (got < len)
                {
                    int r = stream.Read(comp, got, len - got);
                    if (r <= 0) return null;
                    got += r;
                }
                if (!RiceDecode32(comp, len, row, width, blockSize)) return null;

                for (int x = 0; x < outWidth; x++)
                {
                    int v = row[x * step];
                    raw[(y * outWidth) + x] = v;
                    if (v > max) max = v;
                }
            }

            // Frames written as plain 16-bit ADU values in a 32-bit container would render black when
            // divided by the full 32-bit range, so use the 16-bit range when the data allows it.
            double divisor = !hasBzero && max <= 65535 ? 65535.0 : 4294967295.0;

            XisfRawFrame frame = new XisfRawFrame
            {
                Width = outWidth,
                Height = outHeight,
                Channels = 1,
                NormalizedData = new float[outWidth * outHeight]
            };
            for (int i = 0; i < raw.Length; i++)
            {
                float norm = (float)((raw[i] * bscale + bzero) / divisor);
                if (norm < 0.0f) norm = 0.0f;
                else if (norm > 1.0f) norm = 1.0f;
                frame.NormalizedData[i] = norm;
                int bin = (int)(norm * 255.0f);
                if (bin > 255) bin = 255;
                else if (bin < 0) bin = 0;
                frame.Histogram[bin]++;
            }

            StfEngine.ComputeAutoStf(frame, false);
            return frame;
        }

        // Port of the fits_rdecomp logic for 32-bit pixels (FSBITS 5, FSMAX 25). Every read is
        // bounded by the input length; that also bounds the zero-run scan, which has no fixed limit.
        private static bool RiceDecode32(byte[] c, int len, int[] o, int nx, int blk)
        {
            const int FSBITS = 5, FSMAX = 25;
            if (len < 4) return false;
            uint last = ((uint)c[0] << 24) | ((uint)c[1] << 16) | ((uint)c[2] << 8) | c[3];
            int p = 4;
            ulong buf = 0;
            int nb = 0;
            for (int i = 0; i < nx;)
            {
                while (nb < FSBITS) { if (p >= len) return false; buf = (buf << 8) | c[p++]; nb += 8; }
                nb -= FSBITS;
                int fs = (int)((buf >> nb) & 31) - 1;
                int end = Math.Min(i + blk, nx);
                if (fs < 0)
                {
                    for (; i < end; i++) o[i] = (int)last;
                }
                else if (fs == FSMAX)
                {
                    for (; i < end; i++)
                    {
                        while (nb < 32) { if (p >= len) return false; buf = (buf << 8) | c[p++]; nb += 8; }
                        nb -= 32;
                        uint diff = (uint)((buf >> nb) & 0xFFFFFFFFUL);
                        diff = (diff & 1) == 0 ? diff >> 1 : ~(diff >> 1);
                        last += diff;
                        o[i] = (int)last;
                    }
                }
                else if (fs > FSMAX)
                {
                    return false;
                }
                else
                {
                    for (; i < end; i++)
                    {
                        uint nz = 0;
                        while (true)
                        {
                            if (nb == 0) { if (p >= len) return false; buf = c[p++]; nb = 8; }
                            nb--;
                            if (((buf >> nb) & 1) != 0) break;
                            nz++;
                        }
                        while (nb < fs) { if (p >= len) return false; buf = (buf << 8) | c[p++]; nb += 8; }
                        nb -= fs;
                        uint diff = (nz << fs) | (uint)((buf >> nb) & ((1UL << fs) - 1));
                        diff = (diff & 1) == 0 ? diff >> 1 : ~(diff >> 1);
                        last += diff;
                        o[i] = (int)last;
                    }
                }
                if (nb > 32) buf &= (1UL << nb) - 1;
            }
            return true;
        }
    }
}
