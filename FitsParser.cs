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

        public static unsafe XisfRawFrame LoadRawFrame(Stream stream, int targetDimension = 0)
        {
            Dictionary<string, string> header = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            byte[] block = new byte[2880];
            bool endFound = false;

            while (!endFound)
            {
                int read = stream.Read(block, 0, 2880);
                if (read < 2880) return null;

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

            int bitpix = int.Parse(header["BITPIX"]);
            int naxis = int.Parse(header["NAXIS"]);
            if (naxis < 2) return null;

            int width = int.Parse(header["NAXIS1"]);
            int height = int.Parse(header["NAXIS2"]);
            int channels = (naxis >= 3 && header.ContainsKey("NAXIS3")) ? int.Parse(header["NAXIS3"]) : 1;
            int activeChannels = Math.Min(channels, 3);

            double bzero = header.ContainsKey("BZERO") ? double.Parse(header["BZERO"], System.Globalization.CultureInfo.InvariantCulture) : 0.0;
            double bscale = header.ContainsKey("BSCALE") ? double.Parse(header["BSCALE"], System.Globalization.CultureInfo.InvariantCulture) : 1.0;

            int bytesPerPixel = Math.Abs(bitpix) / 8;
            long totalDataBytes = (long)width * height * activeChannels * bytesPerPixel;

            byte[] rawBytes = new byte[totalDataBytes];
            int totalRead = 0;
            while (totalRead < totalDataBytes)
            {
                int r = stream.Read(rawBytes, totalRead, (int)Math.Min(totalDataBytes - totalRead, 4194304));
                if (r <= 0) break;
                totalRead += r;
            }

            int step = 1;
            if (targetDimension > 0 && (width > targetDimension || height > targetDimension))
            {
                step = Math.Max(1, Math.Min(width / targetDimension, height / targetDimension));
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

                        int srcY = (height - 1) - (y * step);
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
                                norm = float.IsNaN(f) || f < 0.0f ? 0.0f : (f > 1.0f ? 1.0f : f);
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

            for (int i = 0; i < outWidth * outHeight; i++)
            {
                int bin = (int)(frame.NormalizedData[i * activeChannels] * 255.0f);
                if (bin > 255) bin = 255;
                else if (bin < 0) bin = 0;
                frame.Histogram[bin]++;
            }

            ComputeLinkedAutoStf(frame, targetDimension > 0 && targetDimension <= 512);
            return frame;
        }

        private static void ComputeLinkedAutoStf(XisfRawFrame frame, bool isThumbnail)
        {
            if (isThumbnail)
            {
                frame.AutoShadows = 0.0f;
                frame.AutoMidtones = 0.5f;
                frame.AutoHighlights = 1.0f;
                return;
            }

            int totalPixels = frame.Width * frame.Height;
            const int maxSamples = 32768;
            int step = Math.Max(1, totalPixels / maxSamples);
            int count = (totalPixels + step - 1) / step;
            int activeChannels = frame.Channels;

            float[] pooled = new float[count * activeChannels];
            int idx = 0;

            for (int i = 0; i < totalPixels && idx < pooled.Length; i += step)
            {
                for (int c = 0; c < activeChannels; c++)
                {
                    pooled[idx++] = frame.NormalizedData[(i * activeChannels) + c];
                }
            }

            Array.Sort(pooled, 0, idx);
            float median = pooled[idx / 2];
            frame.Median = median;

            if (median > 0.5f)
            {
                frame.AutoShadows = 0.0f;
                frame.AutoMidtones = 0.5f;
                frame.AutoHighlights = 1.0f;
                return;
            }

            float[] absDev = new float[idx];
            for (int i = 0; i < idx; i++) absDev[i] = Math.Abs(pooled[i] - median);
            Array.Sort(absDev);
            float mad = absDev[idx / 2];
            frame.MAD = mad;

            float nmad = 1.4826f * mad;
            const float B = 0.25f;
            const float C = -2.8f;

            float c0 = 0.0f;
            if (nmad > 0.000001f)
            {
                c0 = median + (C * nmad);
                if (c0 < 0.0f) c0 = 0.0f;
            }

            float x = median - c0;
            float m = 0.5f;
            if (x > 0.000001f && x < 0.999999f)
            {
                float num = x * (1.0f - B);
                float den = (x * (1.0f - 2.0f * B)) + B;
                if (Math.Abs(den) > 1e-7f) m = num / den;
                m = Math.Max(0.0001f, Math.Min(0.9999f, m));
            }

            frame.AutoShadows = c0;
            frame.AutoMidtones = m;
            frame.AutoHighlights = 1.0f;
        }
    }
}