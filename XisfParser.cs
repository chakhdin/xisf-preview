using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Threading.Tasks;

namespace XisfExplorerPreview
{
    public class XisfRawFrame
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int Channels { get; set; }
        public float[] NormalizedData { get; set; }
        public int[] Histogram { get; set; } = new int[256];
        public float Median { get; set; }
        public float MAD { get; set; }

        // Linked STF parameters applied identically across channels
        public float AutoShadows { get; set; }
        public float AutoMidtones { get; set; } = 0.5f;
        public float AutoHighlights { get; set; } = 1.0f;
    }

    public static class XisfParser
    {
        private static readonly byte[] XisfSignature = { 88, 73, 83, 70, 48, 49, 48, 48 };

        public static Bitmap GetPreviewImage(string filePath, int targetDimension = 0)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return GetPreviewImage(fs, targetDimension);
            }
        }

        public static Bitmap GetPreviewImage(Stream stream, int targetDimension = 0)
        {
            XisfRawFrame frame = LoadRawFrame(stream, targetDimension);
            if (frame == null) return null;

            return RenderBitmapFromRaw(frame, frame.AutoShadows, frame.AutoMidtones, frame.AutoHighlights, 1);
        }

        public static XisfRawFrame LoadRawFrame(string filePath, int targetDimension = 0)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return LoadRawFrame(fs, targetDimension);
            }
        }

        public static XisfRawFrame LoadRawFrame(Stream stream, int targetDimension = 0)
        {
            BinaryReader reader = new BinaryReader(stream);

            byte[] signature = reader.ReadBytes(8);
            if (!signature.SequenceEqual(XisfSignature))
                return null;

            uint headerLength = reader.ReadUInt32();
            uint reserved = reader.ReadUInt32();

            byte[] xmlBytes = reader.ReadBytes((int)headerLength);
            string xmlContent = Encoding.UTF8.GetString(xmlBytes);

            XDocument xdoc = XDocument.Parse(xmlContent);
            XNamespace ns = "http://www.pixinsight.com/xisf";

            XElement imgElem = xdoc.Root.Descendants(ns + "Image").FirstOrDefault();
            bool isThumbnail = false;

            if (targetDimension > 0 && targetDimension <= 512)
            {
                XElement thumbElem = xdoc.Root.Descendants(ns + "Thumbnail").FirstOrDefault();
                if (thumbElem != null)
                {
                    imgElem = thumbElem;
                    isThumbnail = true;
                }
            }

            if (imgElem == null) return null;

            string geometry = imgElem.Attribute("geometry")?.Value;
            string sampleFormat = imgElem.Attribute("sampleFormat")?.Value;
            string location = imgElem.Attribute("location")?.Value;
            string pixelStorage = imgElem.Attribute("pixelStorage")?.Value ?? "Planar";
            string compression = imgElem.Attribute("compression")?.Value;

            if (string.IsNullOrEmpty(geometry) || string.IsNullOrEmpty(location) || !string.IsNullOrEmpty(compression))
                return null;

            string[] geoParts = geometry.Split(':');
            int srcWidth = int.Parse(geoParts[0]);
            int srcHeight = int.Parse(geoParts[1]);
            int channels = int.Parse(geoParts[2]);

            byte[] pixelData = null;

            if (location.StartsWith("attachment:"))
            {
                string[] locParts = location.Split(':');
                long position = long.Parse(locParts[1]);
                int size = int.Parse(locParts[2]);

                reader.BaseStream.Seek(position, SeekOrigin.Begin);
                pixelData = reader.ReadBytes(size);
            }
            else if (location.StartsWith("inline:base64"))
            {
                pixelData = Convert.FromBase64String(imgElem.Value.Trim());
            }
            else if (location == "embedded")
            {
                XElement dataElem = imgElem.Element(ns + "Data") ?? imgElem.Element("Data");
                if (dataElem != null && dataElem.Attribute("encoding")?.Value == "base64")
                {
                    pixelData = Convert.FromBase64String(dataElem.Value.Trim());
                }
            }

            if (pixelData == null) return null;

            return ProcessRawBuffer(srcWidth, srcHeight, channels, sampleFormat, pixelStorage, pixelData, isThumbnail, targetDimension);
        }

        private static unsafe XisfRawFrame ProcessRawBuffer(int srcWidth, int srcHeight, int channels, string sampleFormat, string pixelStorage, byte[] pixelData, bool isThumbnail, int targetDimension)
        {
            int step = 1;
            if (targetDimension > 0 && (srcWidth > targetDimension || srcHeight > targetDimension))
            {
                step = Math.Max(1, Math.Min(srcWidth / targetDimension, srcHeight / targetDimension));
            }

            int outWidth = srcWidth / step;
            int outHeight = srcHeight / step;

            int bytesPerSample = sampleFormat == "UInt8" ? 1 : (sampleFormat == "UInt16" ? 2 : 4);
            int channelSize = srcWidth * srcHeight * bytesPerSample;

            XisfRawFrame frame = new XisfRawFrame
            {
                Width = outWidth,
                Height = outHeight,
                Channels = channels,
                NormalizedData = new float[outWidth * outHeight * channels]
            };

            fixed (byte* pData = pixelData)
            fixed (float* pNorm = frame.NormalizedData)
            {
                int dstPixel = 0;
                for (int y = 0; y < outHeight; y++)
                {
                    int srcY = y * step;
                    for (int x = 0; x < outWidth; x++)
                    {
                        int srcX = x * step;
                        int pixelOffset = (srcY * srcWidth) + srcX;

                        for (int c = 0; c < channels; c++)
                        {
                            int byteOffset;
                            if (pixelStorage == "Planar")
                            {
                                byteOffset = (c * channelSize) + (pixelOffset * bytesPerSample);
                            }
                            else
                            {
                                byteOffset = ((pixelOffset * channels) + c) * bytesPerSample;
                            }

                            float val = ReadNorm(pData + byteOffset, sampleFormat);
                            pNorm[(dstPixel * channels) + c] = val;

                            if (c == 0)
                            {
                                int bin = (int)(val * 255.0f);
                                if (bin < 0) bin = 0;
                                else if (bin > 255) bin = 255;
                                frame.Histogram[bin]++;
                            }
                        }
                        dstPixel++;
                    }
                }
            }

            ComputeLinkedAutoStf(frame, isThumbnail);
            return frame;
        }

        private static unsafe float ReadNorm(byte* ptr, string format)
        {
            if (format == "UInt8")
                return *ptr / 255.0f;
            if (format == "UInt16")
                return (*(ushort*)ptr) / 65535.0f;
            if (format == "Float32")
            {
                float f = *(float*)ptr;
                return float.IsNaN(f) || f < 0.0f ? 0.0f : (f > 1.0f ? 1.0f : f);
            }
            return 0.0f;
        }

        // Standard Linked PixInsight AutoSTF (Joint median & MAD across all channels)
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
            const int maxSamples = 65536;
            int step = Math.Max(1, totalPixels / maxSamples);
            int count = (totalPixels + step - 1) / step;
            int activeChannels = Math.Min(frame.Channels, 3);

            // Pool samples across all channels for linked statistics
            float[] pooledSamples = new float[count * activeChannels];
            int idx = 0;

            for (int i = 0; i < totalPixels && idx < pooledSamples.Length; i += step)
            {
                for (int c = 0; c < activeChannels; c++)
                {
                    pooledSamples[idx++] = frame.NormalizedData[(i * frame.Channels) + c];
                }
            }

            Array.Sort(pooledSamples, 0, idx);
            float median = pooledSamples[idx / 2];
            frame.Median = median;

            if (median > 0.5f) // Non-linear or bright
            {
                frame.AutoShadows = 0.0f;
                frame.AutoMidtones = 0.5f;
                frame.AutoHighlights = 1.0f;
                return;
            }

            float[] absDev = new float[idx];
            for (int i = 0; i < idx; i++)
                absDev[i] = Math.Abs(pooledSamples[i] - median);

            Array.Sort(absDev);
            float mad = absDev[idx / 2];
            frame.MAD = mad;

            float nmad = 1.4826f * mad;

            const float B = 0.25f; // PixInsight Target Background
            const float C = -2.8f; // PixInsight Shadows Clipping

            float c0 = 0.0f;
            if (nmad > 0.000001f)
            {
                c0 = median + (C * nmad);
                if (c0 < 0.0f) c0 = 0.0f;
            }

            float c1 = 1.0f;

            // Solve MTF(m, median - c0) = B
            float x = median - c0;
            float m = 0.5f;

            if (x > 0.000001f && x < 0.999999f)
            {
                float num = x * (1.0f - B);
                float den = (x * (1.0f - 2.0f * B)) + B;
                if (Math.Abs(den) > 1e-7f)
                {
                    m = num / den;
                }
                m = Math.Max(0.0001f, Math.Min(0.9999f, m));
            }

            frame.AutoShadows = c0;
            frame.AutoMidtones = m;
            frame.AutoHighlights = c1;
        }

        public static float MTF(float m, float x)
        {
            if (x <= 0.0f) return 0.0f;
            if (x >= 1.0f) return 1.0f;
            if (Math.Abs(x - 0.5f) < 1e-6f) return m;

            float den = ((2.0f * m - 1.0f) * x) - m;
            if (Math.Abs(den) < 1e-8f) return 0.0f;

            float val = ((m - 1.0f) * x) / den;
            return Math.Max(0.0f, Math.Min(1.0f, val));
        }

        public static unsafe Bitmap RenderBitmapFromRaw(XisfRawFrame frame, float shadows, float midtones, float highlights, int renderStep = 1)
        {
            byte[] lut = BuildStfLut(shadows, midtones, highlights);

            int outWidth = frame.Width / renderStep;
            int outHeight = frame.Height / renderStep;

            Bitmap bmp = new Bitmap(outWidth, outHeight, PixelFormat.Format24bppRgb);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, outWidth, outHeight), ImageLockMode.WriteOnly, bmp.PixelFormat);

            IntPtr scan0Ptr = bmpData.Scan0;
            int stride = bmpData.Stride;
            int channels = frame.Channels;
            int srcWidth = frame.Width;
            float[] normData = frame.NormalizedData;

            fixed (float* pNorm = normData)
            {
                IntPtr pNormPtr = (IntPtr)pNorm;

                Parallel.For(0, outHeight, y =>
                {
                    float* localNorm = (float*)pNormPtr.ToPointer();
                    byte* row = (byte*)scan0Ptr.ToPointer() + (y * stride);
                    int srcY = y * renderStep;
                    int srcRowOffset = srcY * srcWidth;

                    for (int x = 0; x < outWidth; x++)
                    {
                        int srcX = x * renderStep;
                        int srcIdx = (srcRowOffset + srcX) * channels;

                        float rNorm = localNorm[srcIdx];
                        float gNorm = channels > 1 ? localNorm[srcIdx + 1] : rNorm;
                        float bNorm = channels > 2 ? localNorm[srcIdx + 2] : (channels > 1 ? gNorm : rNorm);

                        byte r = SampleLut(rNorm, lut);
                        byte g = SampleLut(gNorm, lut);
                        byte b = SampleLut(bNorm, lut);

                        int bIdx = x * 3;
                        row[bIdx] = b;
                        row[bIdx + 1] = g;
                        row[bIdx + 2] = r;
                    }
                });
            }

            bmp.UnlockBits(bmpData);
            return bmp;
        }

        private static byte SampleLut(float norm, byte[] lut)
        {
            int idx = (int)(norm * 4095.0f + 0.5f);
            if (idx < 0) idx = 0;
            else if (idx > 4095) idx = 4095;
            return lut[idx];
        }

        private static byte[] BuildStfLut(float c0, float m, float c1)
        {
            const int lutSize = 4096;
            byte[] lut = new byte[lutSize];
            float range = Math.Max(0.00001f, c1 - c0);

            for (int i = 0; i < lutSize; i++)
            {
                float x = i / (float)(lutSize - 1);

                if (x <= c0)
                {
                    lut[i] = 0;
                    continue;
                }
                if (x >= c1)
                {
                    lut[i] = 255;
                    continue;
                }

                float clipped = (x - c0) / range;
                float stretched = MTF(m, clipped);

                lut[i] = (byte)Math.Max(0, Math.Min(255, (int)(stretched * 255.0f + 0.5f)));
            }

            return lut;
        }
    }
}