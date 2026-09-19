using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using SharpShell.Attributes;
using SharpShell.SharpThumbnailHandler;

namespace XisfExplorerPreview
{
    [Guid("D37B4D58-7F16-4BC9-9A07-4C0E51610E91")]
    [ComVisible(true)]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".xisf")]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".fits")]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".fit")]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".fts")]
    public class XisfThumbnailHandler : SharpThumbnailHandler
    {
        protected override Bitmap GetThumbnailImage(uint width)
        {
            try
            {
                if (SelectedItemStream == null)
                    return null;

                MemoryStream ms = new MemoryStream();
                if (SelectedItemStream.CanSeek)
                {
                    SelectedItemStream.Seek(0, SeekOrigin.Begin);
                }
                SelectedItemStream.CopyTo(ms);
                ms.Seek(0, SeekOrigin.Begin);

                return AstroFormatDetector.RenderImageFromStream(ms, (int)width);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}