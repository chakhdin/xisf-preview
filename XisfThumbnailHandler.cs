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
                {
                    return null;
                }

                MemoryStream ms = new MemoryStream();
                if (SelectedItemStream.CanSeek)
                {
                    SelectedItemStream.Seek(0, SeekOrigin.Begin);
                }
                SelectedItemStream.CopyTo(ms);
                ms.Seek(0, SeekOrigin.Begin);

                if (ms.Length < 8)
                {
                    return null;
                }

                byte[] sig = new byte[8];
                ms.Read(sig, 0, 8);
                ms.Seek(0, SeekOrigin.Begin);

                string sigStr = System.Text.Encoding.ASCII.GetString(sig);

                if (sig[0] == 88 && sig[1] == 73 && sig[2] == 83 && sig[3] == 70) // 'XISF'
                {
                    return XisfParser.GetPreviewImage(ms, (int)width);
                }
                else if (sigStr.StartsWith("SIMPLE"))
                {
                    XisfRawFrame frame = FitsParser.LoadRawFrame(ms, (int)width);
                    if (frame == null)
                    {
                        return null;
                    }
                    return XisfParser.RenderBitmapFromRaw(frame, frame.AutoShadows, frame.AutoMidtones, frame.AutoHighlights, 1);
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}