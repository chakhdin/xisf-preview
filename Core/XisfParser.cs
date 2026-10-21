using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

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

        // True when the image already looks stretched, so auto-STF was left as identity
        public bool AutoStfSkipped { get; set; }

        // Linked STF parameters applied identically across channels
        public float AutoShadows { get; set; }
        public float AutoMidtones { get; set; } = 0.5f;
        public float AutoHighlights { get; set; } = 1.0f;

        // Unlinked per-channel STF parameters (populated only for RGB frames; null otherwise)
        public float[] AutoShadowsPerChannel { get; set; }
        public float[] AutoMidtonesPerChannel { get; set; }
        public float[] AutoHighlightsPerChannel { get; set; }
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

            return StfEngine.RenderAutoStretch(frame, 1);
        }

        public static XisfRawFrame LoadRawFrame(string filePath, int targetDimension = 0)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return LoadRawFrame(fs, targetDimension);
            }
        }

        // Hard ceilings guarding against corrupt/hostile files: a malformed header should
        // fail gracefully instead of triggering a huge allocation or an out-of-bounds
        // unsafe read (which, unlike a normal exception, can crash the shell/COM host).
        // XDocument inflates the XML roughly tenfold in memory, and real XISF headers
        // (even with an inline thumbnail) stay far below this.
        private const long MaxHeaderBytes = 16L * 1024 * 1024;
        private const int MaxDimension = 65535;
        private const long MaxTotalPixels = 300_000_000L;

        public static XisfRawFrame LoadRawFrame(Stream stream, int targetDimension = 0)
        {
            try
            {
                BinaryReader reader = new BinaryReader(stream);

                byte[] signature = reader.ReadBytes(8);
                if (!signature.SequenceEqual(XisfSignature))
                    return null;

                uint headerLength = reader.ReadUInt32();
                reader.ReadUInt32(); // reserved

                if (headerLength == 0 || headerLength > MaxHeaderBytes)
                    return null;

                byte[] xmlBytes = reader.ReadBytes((int)headerLength);
                if (xmlBytes.Length != headerLength)
                    return null;

                string xmlContent = Encoding.UTF8.GetString(xmlBytes);

                // XDocument.Parse enables DTD processing on .NET Framework, which allows
                // entity-expansion bombs; prohibit DTDs and never resolve external entities.
                XmlReaderSettings xmlSettings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = xmlContent.Length
                };
                XDocument xdoc;
                using (XmlReader xmlReader = XmlReader.Create(new StringReader(xmlContent), xmlSettings))
                    xdoc = XDocument.Load(xmlReader);
                XNamespace ns = "http://www.pixinsight.com/xisf";

                XElement imgElem = xdoc.Root?.Descendants(ns + "Image").FirstOrDefault();
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

                int bytesPerSample;
                if (sampleFormat == "UInt8") bytesPerSample = 1;
                else if (sampleFormat == "UInt16") bytesPerSample = 2;
                else if (sampleFormat == "Float32") bytesPerSample = 4;
                else return null; // unsupported/unknown sample format

                string[] geoParts = geometry.Split(':');
                if (geoParts.Length < 3) return null;

                if (!int.TryParse(geoParts[0], out int srcWidth) ||
                    !int.TryParse(geoParts[1], out int srcHeight) ||
                    !int.TryParse(geoParts[2], out int channels))
                    return null;

                if (srcWidth <= 0 || srcHeight <= 0 || channels <= 0 ||
                    srcWidth > MaxDimension || srcHeight > MaxDimension || channels > 4)
                    return null;

                if ((long)srcWidth * srcHeight > MaxTotalPixels)
                    return null;

                byte[] pixelData = null;

                if (location.StartsWith("attachment:"))
                {
                    string[] locParts = location.Split(':');
                    if (locParts.Length < 3) return null;
                    if (!long.TryParse(locParts[1], out long position) ||
                        !int.TryParse(locParts[2], out int size) ||
                        position < 0 || size < 0)
                        return null;

                    long requiredAttachmentBytes = (long)srcWidth * srcHeight * channels * bytesPerSample;

                    // Validate against the declared geometry and the real file length before
                    // allocating anything, so a tiny file cannot request a huge buffer.
                    if (size < requiredAttachmentBytes || requiredAttachmentBytes > int.MaxValue)
                        return null;
                    if (stream.CanSeek && position + requiredAttachmentBytes > stream.Length)
                        return null;

                    int step = ComputeStep(srcWidth, srcHeight, targetDimension);

                    if (step > 1 && stream.CanSeek)
                    {
                        // Downscaled request on a seekable stream (shell thumbnails): read only the
                        // rows that survive the subsampling instead of the whole pixel block.
                        pixelData = ReadSubsampled(stream, position, srcWidth, srcHeight, channels,
                            bytesPerSample, pixelStorage, step);
                        if (pixelData == null) return null;

                        srcWidth /= step;
                        srcHeight /= step;
                        targetDimension = 0;
                    }
                    else
                    {
                        reader.BaseStream.Seek(position, SeekOrigin.Begin);
                        pixelData = reader.ReadBytes((int)requiredAttachmentBytes);
                    }
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

                long requiredBytes = (long)srcWidth * srcHeight * channels * bytesPerSample;
                if (pixelData.LongLength < requiredBytes) return null;

                XisfRawFrame result = ProcessRawBuffer(srcWidth, srcHeight, channels, sampleFormat, pixelStorage, pixelData, isThumbnail, targetDimension);
                StfEngine.ComputeAutoStf(result, isThumbnail);
                return result;
            }
            catch
            {
                // Any malformed/truncated/corrupt file falls back to "no preview" rather than propagating.
                return null;
            }
        }

        private static int ComputeStep(int srcWidth, int srcHeight, int targetDimension)
        {
            if (targetDimension > 0 && (srcWidth > targetDimension || srcHeight > targetDimension))
                return Math.Max(1, Math.Min(srcWidth / targetDimension, srcHeight / targetDimension));
            return 1;
        }

        // Returns a compact (srcWidth/step x srcHeight/step) buffer in the same pixel storage
        // as the source, containing exactly the samples ProcessRawBuffer would pick with this
        // step. Reads one source row at a time, so memory and I/O scale with the output.
        private static byte[] ReadSubsampled(Stream stream, long position, int srcWidth, int srcHeight,
            int channels, int bytesPerSample, string pixelStorage, int step)
        {
            int outWidth = srcWidth / step;
            int outHeight = srcHeight / step;
            bool planar = pixelStorage == "Planar";

            long channelSize = (long)srcWidth * srcHeight * bytesPerSample;
            int rowSamples = planar ? srcWidth : srcWidth * channels;
            byte[] row = new byte[rowSamples * bytesPerSample];
            byte[] result = new byte[(long)outWidth * outHeight * channels * bytesPerSample];

            for (int c = 0; c < (planar ? channels : 1); c++)
            {
                for (int y = 0; y < outHeight; y++)
                {
                    long rowStart = planar
                        ? position + (c * channelSize) + ((long)y * step * srcWidth * bytesPerSample)
                        : position + ((long)y * step * srcWidth * channels * bytesPerSample);

                    stream.Seek(rowStart, SeekOrigin.Begin);
                    int got = 0;
                    while (got < row.Length)
                    {
                        int r = stream.Read(row, got, row.Length - got);
                        if (r <= 0) return null; // truncated file
                        got += r;
                    }

                    if (planar)
                    {
                        long dst = (c * (long)outWidth * outHeight + (long)y * outWidth) * bytesPerSample;
                        for (int x = 0; x < outWidth; x++)
                            Buffer.BlockCopy(row, x * step * bytesPerSample, result, (int)(dst + x * bytesPerSample), bytesPerSample);
                    }
                    else
                    {
                        int pixelBytes = channels * bytesPerSample;
                        long dst = (long)y * outWidth * pixelBytes;
                        for (int x = 0; x < outWidth; x++)
                            Buffer.BlockCopy(row, x * step * pixelBytes, result, (int)(dst + x * pixelBytes), pixelBytes);
                    }
                }
            }

            return result;
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
    }
}