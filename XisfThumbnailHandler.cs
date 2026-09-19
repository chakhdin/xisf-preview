using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using SharpShell.Attributes;
using SharpShell.SharpThumbnailHandler;

namespace XisfExplorerPreview
{
    [ComVisible(true)]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".xisf")]
    [DisplayName("XISF Thumbnail Handler")]
    [Guid("A71BC054-942C-4A5F-A4F7-D8B6F3E6C1D2")]
    public class XisfThumbnailHandler : SharpThumbnailHandler
    {
        protected override Bitmap GetThumbnailImage(uint width)
        {
            try
            {
                if (SelectedItemStream == null)
                    return null;

                // Pass requested size down so parser downsamples on I/O read
                using (Bitmap bmp = XisfParser.GetPreviewImage(SelectedItemStream, (int)width))
                {
                    if (bmp == null)
                        return null;

                    return ScaleThumbnail(bmp, (int)width);
                }
            }
            catch
            {
                return null;
            }
        }

        private Bitmap ScaleThumbnail(Bitmap source, int maxDimension)
        {
            if (source.Width <= maxDimension && source.Height <= maxDimension)
            {
                return new Bitmap(source);
            }

            float scale = Math.Min((float)maxDimension / source.Width, (float)maxDimension / source.Height);
            int newWidth = Math.Max(1, (int)(source.Width * scale));
            int newHeight = Math.Max(1, (int)(source.Height * scale));

            Bitmap dest = new Bitmap(newWidth, newHeight, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(dest))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.DrawImage(source, 0, 0, newWidth, newHeight);
            }
            return dest;
        }
    }
}