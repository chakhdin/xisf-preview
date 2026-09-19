using System;
using System.Runtime.InteropServices;
using SharpShell.Attributes;
using SharpShell.SharpPreviewHandler;

namespace XisfExplorerPreview
{
    [ComVisible(true)]
    [PreviewHandler]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".xisf")]
    [DisplayName("XISF Preview Handler")]
    [Guid("8F4C479B-84C1-4D56-8A5E-44E281F275E1")]
    public class XisfPreviewHandler : SharpPreviewHandler
    {
        protected override PreviewHandlerControl DoPreview()
        {
            var control = new XisfPreviewControl();
            control.LoadFile(SelectedFilePath);
            return control;
        }
    }
}