using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace XisfExplorerPreview
{
    public static class FitsParser
    {
        public static XisfRawFrame LoadRawFrame(string filePath, int targetDimension = 0)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536, FileOptions.SequentialScan))
            {
                return LoadRawFrame(fs, targetDimension);
            }
        }

        // Hard ceilings guarding against corrupt/hostile headers: a malformed NAXISn
        // should fail gracefully instead of triggering a huge allocation or an
        // out-of-bounds unsafe read (which, unlike a normal exception, can crash the
        // shell/COM host rather than just this call).
        private const int MaxDimension = 65535;
        private const long MaxTotalPixels = 300_000_000L;
        private const int MaxHeaderBlocks = 2000; // 2000 x 2880 B, about 5.5 MB

        // Largest byte[] the CLR will allocate (without gcAllowVeryLargeObjects).
        private const long MaxArrayBytes = 0x7FFFFFC7L;

        // Float data is nominally 0..1, but many writers store ADU (0..255 or 0..65535) or other
        // unbounded values. When the finite maximum exceeds 1.01, divide by 255 / 65535 for the common
        // 8/16-bit ranges, otherwise by the maximum. A maximum up to 1.01 counts as a finished 0..1
        // image whose few overshooting samples (writer rounding) just clamp, so one 1.0001 sample
        // cannot make the whole image 255 times darker. One scale for all channels, so colour balance
        // survives. Never triggers on negative values: those just clamp to 0. Runs on the samples
        // that survived subsampling, so seekable and forward-only reads agree.
        private static void NormalizeFloatRange(float[] data)
        {
            object gate = new object();
            float max = 0.0f;
            Parallel.For(0, (data.Length + 65535) / 65536, () => 0.0f, (chunk, state, local) =>
            {
                int end = Math.Min(data.Length, (chunk + 1) * 65536);
                for (int i = chunk * 65536; i < end; i++)
                {
                    float v = data[i];
                    if (!float.IsInfinity(v) && v > local) local = v;
                }
                return local;
            }, local => { lock (gate) { if (local > max) max = local; } });

            double scale = 1.0;
            if (max > 1.01f)
                scale = max <= 255.0f ? 255.0 : (max <= 65535.0f ? 65535.0 : max);

            Parallel.For(0, (data.Length + 65535) / 65536, chunk =>
            {
                int end = Math.Min(data.Length, (chunk + 1) * 65536);
                for (int i = chunk * 65536; i < end; i++)
                {
                    double n = scale == 1.0 ? data[i] : data[i] / scale;
                    data[i] = n <= 0.0 ? 0.0f : (n >= 1.0 ? 1.0f : (float)n);
                }
            });
        }

        private static bool TryGetSeekableLength(Stream stream, out long length)
        {
            length = 0;
            try
            {
                if (!stream.CanSeek) return false;
                length = stream.Length;
                return true;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        // Returns the first activeChannels planes as a compact (width/step x height/step)
        // top-down buffer holding exactly the samples the conversion loop would pick for this
        // step. Reads one source row at a time, so memory and I/O scale with the output.
        private static byte[] ReadSubsampledPlanes(Stream stream, long dataStart, int width, int height,
            int activeChannels, int bytesPerPixel, int step, bool flipVertically)
        {
            int outWidth = width / step;
            int outHeight = height / step;
            long planeSize = (long)width * height * bytesPerPixel;
            byte[] row = new byte[width * bytesPerPixel];
            byte[] result = new byte[(long)outWidth * outHeight * activeChannels * bytesPerPixel];

            for (int c = 0; c < activeChannels; c++)
            {
                for (int y = 0; y < outHeight; y++)
                {
                    int srcY = flipVertically ? (height - 1) - (y * step) : y * step;
                    stream.Seek(dataStart + (c * planeSize) + ((long)srcY * width * bytesPerPixel), SeekOrigin.Begin);

                    int got = 0;
                    while (got < row.Length)
                    {
                        int r = stream.Read(row, got, row.Length - got);
                        if (r <= 0) return null; // truncated file
                        got += r;
                    }

                    long dst = (((long)c * outWidth * outHeight) + ((long)y * outWidth)) * bytesPerPixel;
                    for (int x = 0; x < outWidth; x++)
                        Buffer.BlockCopy(row, x * step * bytesPerPixel, result, (int)(dst + (x * bytesPerPixel)), bytesPerPixel);
                }
            }

            return result;
        }

        public static unsafe XisfRawFrame LoadRawFrame(Stream stream, int targetDimension = 0)
        {
            try
            {
                Dictionary<string, string> header = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                byte[] block = new byte[2880];
                bool endFound = false;
                int headerBlocks = 0;

                while (!endFound)
                {
                    // Real headers are a handful of blocks; without a cap a file lacking END
                    // (or full of unique keywords) would be read and indexed end to end.
                    if (++headerBlocks > MaxHeaderBlocks) return null;

                    int bytesRead = 0;
                    while (bytesRead < 2880)
                    {
                        int r = stream.Read(block, bytesRead, 2880 - bytesRead);
                        if (r <= 0) break;
                        bytesRead += r;
                    }

                    if (bytesRead < 2880) return null;

                    for (int i = 0; i < 2880; i += 80)
                    {
                        string key = Encoding.ASCII.GetString(block, i, 8).Trim();
                        if (key.Equals("END", StringComparison.OrdinalIgnoreCase))
                        {
                            endFound = true;
                            break;
                        }

                        if (i + 10 < 2880 && block[i + 8] == '=')
                        {
                            string record = Encoding.ASCII.GetString(block, i, 80);
                            string val = record.Substring(10).Split('/')[0].Trim().Trim('\'').Trim();
                            if (!header.ContainsKey(key))
                                header[key] = val;
                        }
                    }
                }

                if (!header.ContainsKey("SIMPLE") || !header.ContainsKey("BITPIX") || !header.ContainsKey("NAXIS"))
                    return null;

                var ic = System.Globalization.CultureInfo.InvariantCulture;

                if (!int.TryParse(header["BITPIX"], out int bitpix)) return null;
                if (bitpix != 8 && bitpix != 16 && bitpix != 32 && bitpix != -32) return null; // unsupported BITPIX

                if (!int.TryParse(header["NAXIS"], out int naxis) || naxis < 2) return null;

                if (!header.ContainsKey("NAXIS1") || !header.ContainsKey("NAXIS2")) return null;
                if (!int.TryParse(header["NAXIS1"], out int width) || !int.TryParse(header["NAXIS2"], out int height))
                    return null;

                int channels = 1;
                if (naxis >= 3 && header.ContainsKey("NAXIS3") && !int.TryParse(header["NAXIS3"], out channels))
                    return null;

                if (width <= 0 || height <= 0 || channels <= 0 ||
                    width > MaxDimension || height > MaxDimension || channels > 16)
                    return null;

                if ((long)width * height > MaxTotalPixels)
                    return null;

                int activeChannels = Math.Min(channels, 3);

                double bzero = 0.0, bscale = 1.0;
                if (header.ContainsKey("BZERO") && !double.TryParse(header["BZERO"], System.Globalization.NumberStyles.Float, ic, out bzero))
                    return null;
                if (header.ContainsKey("BSCALE") && !double.TryParse(header["BSCALE"], System.Globalization.NumberStyles.Float, ic, out bscale))
                    return null;

                int bytesPerPixel = Math.Abs(bitpix) / 8;
                long totalDataBytes = (long)width * height * activeChannels * bytesPerPixel;

                // Classic FITS/IRAF convention stores row 1 at the bottom of the image, so a
                // bottom-up-to-top-down flip is needed for on-screen display. Some writers
                // (e.g. PixInsight) instead store row 1 at the top and say so explicitly via
                // ROWORDER; honor that when present instead of always flipping.
                bool flipVertically = true;
                if (header.TryGetValue("ROWORDER", out string rowOrder) &&
                    rowOrder.Equals("TOP-DOWN", StringComparison.OrdinalIgnoreCase))
                {
                    flipVertically = false;
                }

                int step = 1;
                if (targetDimension > 0 && (width > targetDimension || height > targetDimension))
                {
                    step = Math.Max(1, Math.Min(width / targetDimension, height / targetDimension));
                }

                byte[] rawBytes = null;

                // Downscaled request on a seekable stream (shell thumbnails): read only the rows
                // that survive the subsampling instead of the whole data block. The header loop
                // consumed whole 2880-byte blocks, so the stream is now at the start of the data.
                if (step > 1 && TryGetSeekableLength(stream, out long streamLength))
                {
                    long dataStart = stream.Position;
                    if (streamLength < dataStart + totalDataBytes) return null; // truncated pixel data

                    rawBytes = ReadSubsampledPlanes(stream, dataStart, width, height, activeChannels,
                        bytesPerPixel, step, flipVertically);
                    if (rawBytes == null) return null;

                    // rawBytes is now a compact, already top-down copy of the image.
                    width /= step;
                    height /= step;
                    step = 1;
                    flipVertically = false;
                }
                else
                {
                    if (totalDataBytes > MaxArrayBytes) return null;

                    // Don't allocate the buffer for data the file cannot contain.
                    if (TryGetSeekableLength(stream, out long fullLength) &&
                        fullLength < stream.Position + totalDataBytes)
                        return null;

                    rawBytes = new byte[totalDataBytes];
                    int totalRead = 0;
                    while (totalRead < totalDataBytes)
                    {
                        int r = stream.Read(rawBytes, totalRead, (int)Math.Min(totalDataBytes - totalRead, 4194304));
                        if (r <= 0) break;
                        totalRead += r;
                    }

                    if (totalRead < totalDataBytes) return null; // truncated pixel data
                }

                int outWidth = width / step;
                int outHeight = height / step;

                XisfRawFrame frame = new XisfRawFrame
                {
                    Width = outWidth,
                    Height = outHeight,
                    Channels = activeChannels,
                    NormalizedData = new float[outWidth * outHeight * activeChannels]
                };

                long planeSize = (long)width * height * bytesPerPixel;

                fixed (byte* pRaw = rawBytes)
                fixed (float* pNorm = frame.NormalizedData)
                {
                    IntPtr pNormPtr = (IntPtr)pNorm;

                    for (int c = 0; c < activeChannels; c++)
                    {
                        IntPtr pPlanePtr = (IntPtr)(pRaw + (c * planeSize));
                        int currentChannel = c;

                        Parallel.For(0, outHeight, y =>
                        {
                            byte* pPlaneLocal = (byte*)pPlanePtr.ToPointer();
                            float* pNormLocal = (float*)pNormPtr.ToPointer();

                            int srcY = flipVertically ? (height - 1) - (y * step) : y * step;
                            long srcRowOffset = (long)srcY * width * bytesPerPixel;
                            int dstRowOffset = y * outWidth;

                            for (int x = 0; x < outWidth; x++)
                            {
                                int srcX = x * step;
                                byte* ptr = pPlaneLocal + srcRowOffset + (srcX * bytesPerPixel);
                                float norm = 0.0f;

                                if (bitpix == 16)
                                {
                                    short raw = (short)((ptr[0] << 8) | ptr[1]);
                                    double val = raw * bscale + bzero;
                                    norm = (float)(val / 65535.0);
                                }
                                else if (bitpix == -32)
                                {
                                    uint u = (uint)((ptr[0] << 24) | (ptr[1] << 16) | (ptr[2] << 8) | ptr[3]);
                                    float f = *(float*)&u;
                                    // Stored unclamped; NormalizeFloatRange scales and clamps it once the
                                    // whole frame is known.
                                    float scaled = (float)(f * bscale + bzero);
                                    pNormLocal[((dstRowOffset + x) * activeChannels) + currentChannel] =
                                        float.IsNaN(scaled) ? 0.0f : scaled;
                                    continue;
                                }
                                else if (bitpix == 8)
                                {
                                    norm = (float)((*ptr * bscale + bzero) / 255.0);
                                }
                                else if (bitpix == 32)
                                {
                                    int raw = (ptr[0] << 24) | (ptr[1] << 16) | (ptr[2] << 8) | ptr[3];
                                    norm = (float)((raw * bscale + bzero) / 4294967295.0);
                                }

                                if (norm < 0.0f) norm = 0.0f;
                                else if (norm > 1.0f) norm = 1.0f;

                                pNormLocal[((dstRowOffset + x) * activeChannels) + currentChannel] = norm;
                            }
                        });
                    }
                }

                if (bitpix == -32) NormalizeFloatRange(frame.NormalizedData);

                for (int i = 0; i < outWidth * outHeight; i++)
                {
                    int bin = (int)(frame.NormalizedData[i * activeChannels] * 255.0f);
                    if (bin > 255) bin = 255;
                    else if (bin < 0) bin = 0;
                    frame.Histogram[bin]++;
                }

                StfEngine.ComputeAutoStf(frame, false);
                return frame;
            }
            catch
            {
                // Any malformed/truncated/corrupt file falls back to "no preview" rather than propagating.
                return null;
            }
        }
    }
}