using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using SharpShell.Attributes;
using SharpShell.SharpPreviewHandler;

namespace XisfExplorerPreview
{
    [Guid("D37B4D58-7F16-4BC9-9A07-4C0E51610E92")]
    [ComVisible(true)]
    [PreviewHandler]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".xisf")]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".fits")]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".fit")]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".fts")]
    public class XisfPreviewHandler : SharpPreviewHandler
    {
        private XisfPreviewControl _control;

        protected override PreviewHandlerControl DoPreview()
        {
            _control = new XisfPreviewControl();
            DoPreviewFile();
            return _control;
        }

        private void DoPreviewFile()
        {
            try
            {
                if (!string.IsNullOrEmpty(SelectedFilePath) && File.Exists(SelectedFilePath))
                {
                    using (FileStream fs = new FileStream(SelectedFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        RenderPreviewFromStream(fs);
                    }
                    return;
                }

                _control.ShowMessage("Unable to access file for preview.");
            }
            catch (Exception ex)
            {
                _control.ShowMessage("Error loading preview: " + ex.Message);
            }
        }

        private void RenderPreviewFromStream(Stream stream)
        {
            Bitmap bmp = AstroFormatDetector.RenderImageFromStream(stream, 2048);

            if (bmp != null)
            {
                _control.SetImage(bmp);
            }
            else
            {
                _control.ShowMessage("Failed to decode astronomical image preview.");
            }
        }
    }
}