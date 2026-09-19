using System;
using System.Drawing;
using System.IO;
using System.Text;

namespace XisfExplorerPreview
{
    public enum AstroFileFormat
    {
        Unknown,
        Xisf,
        Fits
    }

    public static class AstroFormatDetector
    {
        public static AstroFileFormat DetectFormat(Stream stream)
        {
            if (stream == null) return AstroFileFormat.Unknown;

            long pos = stream.CanSeek ? stream.Position : 0;
            byte[] sig = new byte[8];
            int read = 0;

            while (read < 8)
            {
                int r = stream.Read(sig, read, 8 - read);
                if (r <= 0) break;
                read += r;
            }

            if (stream.CanSeek)
            {
                stream.Seek(pos, SeekOrigin.Begin);
            }

            if (read >= 4 && sig[0] == 88 && sig[1] == 73 && sig[2] == 83 && sig[3] == 70) // 'XISF'
            {
                return AstroFileFormat.Xisf;
            }

            if (read >= 6 && sig[0] == 'S' && sig[1] == 'I' && sig[2] == 'M' && sig[3] == 'P') // 'SIMPLE'
            {
                return AstroFileFormat.Fits;
            }

            return AstroFileFormat.Unknown;
        }

        public static Bitmap RenderImageFromStream(Stream stream, int targetDimension)
        {
            AstroFileFormat format = DetectFormat(stream);

            if (format == AstroFileFormat.Xisf)
            {
                return XisfParser.GetPreviewImage(stream, targetDimension);
            }
            else if (format == AstroFileFormat.Fits)
            {
                XisfRawFrame frame = FitsParser.LoadRawFrame(stream, targetDimension);
                return frame != null ? StfEngine.RenderAutoStretch(frame, 1) : null;
            }

            return null;
        }
    }
}