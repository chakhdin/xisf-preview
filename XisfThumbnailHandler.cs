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
    [COMServerAssociation(AssociationType.ClassOfExtension, ".fits")]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".fit")]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".fts")]
    public class XisfThumbnailHandler : SharpThumbnailHandler
    {
        protected override Bitmap GetThumbnailImage(uint width)
        {
            try
            {
                if (SelectedItemStream == null || !SelectedItemStream.CanSeek)
                    return null;

                byte[] sig = new byte[8];
                SelectedItemStream.Seek(0, SeekOrigin.Begin);
                SelectedItemStream.Read(sig, 0, 8);
                SelectedItemStream.Seek(0, SeekOrigin.Begin);

                if (sig[0] == 88 && sig[1] == 73 && sig[2] == 83 && sig[3] == 70) // 'XISF'
                {
                    return XisfParser.GetPreviewImage(SelectedItemStream, (int)width);
                }
                else if (sig[0] == 'S' && sig[1] == 'I' && sig[2] == 'M' && sig[3] == 'P') // 'SIMPLE'
                {
                    XisfRawFrame frame = FitsParser.LoadRawFrame(SelectedItemStream, (int)width);
                    return frame != null ? XisfParser.RenderBitmapFromRaw(frame, frame.AutoShadows, frame.AutoMidtones, frame.AutoHighlights, 1) : null;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}